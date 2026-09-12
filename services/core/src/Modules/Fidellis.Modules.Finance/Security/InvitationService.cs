using Fidellis.Infrastructure;
using Fidellis.Infrastructure.Catalog;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Security;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Security;

public sealed record InvitationDto(
    Guid Id, string Email, string Role, string Status, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

public sealed record TenantRef(Guid Id, string Name);

/// <summary>Estado de bootstrap do tenant (D-01 §3.3): equipe mínima de governança montada?</summary>
public sealed record BootstrapStatus(DateTimeOffset? OnboardingCompletedAt, int ApproverCount, bool TeamReady)
{
    /// <summary>Enquanto não há equipe mínima, o tenant está em bootstrap (banner + override auditável).</summary>
    public bool InBootstrap => !TeamReady;
}

public enum InviteOutcome { MemberAdded, Invited, AlreadyMember }

public sealed record InviteResult(InviteOutcome Outcome, InvitationDto? Invitation);

/// <summary>
/// Convites de membro e estado de bootstrap (D-01). Convidar por e-mail + papel resolve dois casos:
/// e-mail <b>já existente</b> vira membership direto (+notificação); e-mail <b>novo</b> gera um convite
/// com link mágico (o aceite, no BFF, cria user+membership). Opera no <c>catalog</c> global; e-mails
/// e a leitura das faixas de alçada usam o schema do tenant do request.
/// </summary>
public sealed class InvitationService(
    CatalogDbContext catalog,
    TenantDbContext tenantDb,
    MessageOutbox outbox,
    IClock clock,
    InfrastructureOptions options)
{
    /// <summary>Validade do link do convite (D-01 Q5).</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    /// <summary>Mínimo de aprovadores distintos para sair do bootstrap (D-01 Q6).</summary>
    public const int MinApprovers = 2;

    public Task<Guid> ResolveTenantIdAsync(string slug, CancellationToken ct = default)
        => catalog.Tenants.Where(t => t.Slug == slug).Select(t => t.Id).FirstOrDefaultAsync(ct);

    /// <summary>Resolve id + nome do tenant (o nome vai nos e-mails de convite).</summary>
    public Task<TenantRef?> ResolveTenantAsync(string slug, CancellationToken ct = default)
        => catalog.Tenants.Where(t => t.Slug == slug)
            .Select(t => new TenantRef(t.Id, t.Name)).FirstOrDefaultAsync(ct);

    /// <summary>Convida uma pessoa por e-mail + papel. Retorna o desfecho (membership direto × convite).</summary>
    public async Task<InviteResult> CreateAsync(
        Guid tenantId, string tenantName, string email, string role, Guid? invitedBy, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("E-mail inválido.");
        if (!FinanceRoles.IsValid(role))
            throw new ArgumentException($"Papel inválido. Use um de: {string.Join(", ", FinanceRoles.All)}.");
        role = role.Trim().ToLowerInvariant();

        var user = await catalog.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is not null)
        {
            var already = await catalog.Memberships.AnyAsync(m => m.TenantId == tenantId && m.UserId == user.Id, ct);
            if (already) return new InviteResult(InviteOutcome.AlreadyMember, null);

            catalog.Memberships.Add(new Membership { UserId = user.Id, TenantId = tenantId, Role = role });
            await catalog.SaveChangesAsync(ct);
            await NotifyMemberAddedAsync(tenantId, user, tenantName, ct);
            return new InviteResult(InviteOutcome.MemberAdded, null);
        }

        // E-mail novo: (re)usa a linha pendente do par (tenant, e-mail) — assim recadastrar = reenviar.
        var invitation = await catalog.Invitations
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Email == email && i.Status == "pending", ct);
        var token = InvitationToken.Generate();
        if (invitation is null)
        {
            invitation = new Invitation
            {
                TenantId = tenantId,
                Email = email,
                Role = role,
                TokenHash = InvitationToken.Hash(token),
                InvitedBy = invitedBy,
                ExpiresAt = clock.UtcNow.Add(Ttl),
            };
            catalog.Invitations.Add(invitation);
        }
        else
        {
            invitation.Role = role;
            invitation.TokenHash = InvitationToken.Hash(token);
            invitation.ExpiresAt = clock.UtcNow.Add(Ttl);
        }
        await catalog.SaveChangesAsync(ct);
        await SendInviteEmailAsync(email, tenantName, token, ct);
        return new InviteResult(InviteOutcome.Invited, ToDto(invitation));
    }

    public async Task<List<InvitationDto>> ListPendingAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Expira preguiçosamente convites vencidos, para a lista refletir a verdade.
        await ExpireStaleAsync(tenantId, ct);
        return await catalog.Invitations
            .Where(i => i.TenantId == tenantId && i.Status == "pending")
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationDto(i.Id, i.Email, i.Role, i.Status, i.ExpiresAt, i.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Revoga um convite pendente. Retorna false se não existir/não estiver pendente.</summary>
    public async Task<bool> RevokeAsync(Guid tenantId, Guid invitationId, CancellationToken ct = default)
    {
        var inv = await catalog.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.TenantId == tenantId && i.Status == "pending", ct);
        if (inv is null) return false;
        inv.Status = "revoked";
        await catalog.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Reenvia um convite pendente: novo token, prazo reiniciado, e-mail refeito (D-01 Q5).</summary>
    public async Task<InvitationDto?> ResendAsync(Guid tenantId, Guid invitationId, string tenantName, CancellationToken ct = default)
    {
        var inv = await catalog.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.TenantId == tenantId && i.Status == "pending", ct);
        if (inv is null) return null;
        var token = InvitationToken.Generate();
        inv.TokenHash = InvitationToken.Hash(token);
        inv.ExpiresAt = clock.UtcNow.Add(Ttl);
        await catalog.SaveChangesAsync(ct);
        await SendInviteEmailAsync(inv.Email, tenantName, token, ct);
        return ToDto(inv);
    }

    /// <summary>
    /// Estado de bootstrap: conta membros distintos com papel aprovador (derivado das faixas de alçada
    /// configuradas no tenant). É a <b>fonte de verdade</b> de compliance que libera/veta o override.
    /// </summary>
    public async Task<BootstrapStatus> BootstrapStatusAsync(Guid tenantId, CancellationToken ct = default)
    {
        var completedAt = await catalog.Tenants
            .Where(t => t.Id == tenantId).Select(t => t.OnboardingCompletedAt).FirstOrDefaultAsync(ct);

        var approverRoles = await ApproverRolesAsync(ct);
        var roles = await catalog.Memberships
            .Where(m => m.TenantId == tenantId).Select(m => m.Role).ToListAsync(ct);
        var count = roles.Count(r => approverRoles.Contains(r));

        return new BootstrapStatus(completedAt, count, count >= MinApprovers);
    }

    /// <summary>Marca o onboarding em duas etapas como concluído/pulado (dirige o banner).</summary>
    public async Task<BootstrapStatus> CompleteOnboardingAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await catalog.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is not null && tenant.OnboardingCompletedAt is null)
        {
            tenant.OnboardingCompletedAt = clock.UtcNow;
            await catalog.SaveChangesAsync(ct);
        }
        return await BootstrapStatusAsync(tenantId, ct);
    }

    /// <summary>Papéis com alçada de aprovação, unidos a partir das faixas configuradas no tenant.</summary>
    private async Task<HashSet<string>> ApproverRolesAsync(CancellationToken ct)
    {
        var csvs = await tenantDb.ApprovalTiers.Select(t => t.RolesCsv).ToListAsync(ct);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var csv in csvs)
            foreach (var r in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                set.Add(r);
        return set;
    }

    private async Task ExpireStaleAsync(Guid tenantId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var stale = await catalog.Invitations
            .Where(i => i.TenantId == tenantId && i.Status == "pending" && i.ExpiresAt < now)
            .ToListAsync(ct);
        if (stale.Count == 0) return;
        foreach (var i in stale) i.Status = "expired";
        await catalog.SaveChangesAsync(ct);
    }

    private Task SendInviteEmailAsync(string email, string tenantName, string token, CancellationToken ct)
    {
        var link = $"{options.AppBaseUrl.TrimEnd('/')}/convite/{token}";
        var subject = $"Convite para a equipe de {tenantName} no Fidellis";
        var body =
            $"Olá!\n\nVocê foi convidado(a) para participar da equipe de {tenantName} no Fidellis, o sistema " +
            "de gestão financeira e contábil da instituição.\n\n" +
            $"Para aceitar o convite e definir sua senha, acesse:\n{link}\n\n" +
            "Este link é pessoal, de uso único e expira em 7 dias.\n\nEquipe Fidellis";
        return outbox.EnqueueAsync(new EnqueueRequest(
            "team.invitation", email, subject, body, DedupeKey: $"invite:{InvitationToken.Hash(token)}"), ct);
    }

    private Task NotifyMemberAddedAsync(Guid tenantId, User user, string tenantName, CancellationToken ct)
    {
        var subject = $"Você agora faz parte da equipe de {tenantName} no Fidellis";
        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? "olá" : $"olá, {user.DisplayName}";
        var link = $"{options.AppBaseUrl.TrimEnd('/')}/login";
        var body =
            $"{char.ToUpperInvariant(name[0])}{name[1..]}!\n\nSeu acesso foi vinculado à equipe de {tenantName} no Fidellis. " +
            $"Use seu e-mail e senha de sempre para entrar:\n{link}\n\nEquipe Fidellis";
        return outbox.EnqueueAsync(new EnqueueRequest(
            "team.member_added", user.Email, subject, body, DedupeKey: $"member-added:{tenantId}:{user.Id}"), ct);
    }

    private static InvitationDto ToDto(Invitation i)
        => new(i.Id, i.Email, i.Role, i.Status, i.ExpiresAt, i.CreatedAt);
}

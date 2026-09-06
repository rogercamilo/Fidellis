using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Alçadas de aprovação de Contas a Pagar (RF-FIN-112). Resolve a faixa por valor e aplica os
/// <b>guarda-corpos de compliance</b> não-desligáveis: mínimo 1 aprovação; autoaprovação bloqueada
/// (quem criou ≠ aprovador); acima de R$ 5.000, 2 assinaturas sempre; papel deve pertencer à faixa;
/// um aprovador não assina duas vezes. Roda no schema do tenant.
/// </summary>
public sealed class ApprovalService(TenantDbContext db, IClock clock)
{
    /// <summary>Teto de compliance (D13): acima disso, 2 assinaturas são sempre obrigatórias.</summary>
    public const decimal ComplianceCeiling = 5000m;

    /// <summary>Assinaturas exigidas: máximo entre a configuração da faixa (mín. 1) e o piso de compliance.</summary>
    public static int RequiredSignatures(ApprovalTier tier, decimal amount)
        => Math.Max(Math.Max(1, tier.Signatures), amount > ComplianceCeiling ? 2 : 1);

    public async Task<ApprovalTier?> ResolveTierAsync(decimal amount, CancellationToken ct = default)
    {
        var tiers = await db.ApprovalTiers.OrderBy(t => t.MinAmount).ToListAsync(ct);
        return tiers.FirstOrDefault(t => amount >= t.MinAmount && (t.MaxAmount is null || amount < t.MaxAmount));
    }

    /// <summary>Registra uma aprovação; quando as assinaturas exigidas são atingidas, o título vira <c>approved</c>.</summary>
    public async Task<Payable> ApproveAsync(Guid payableId, Guid approverId, string role, CancellationToken ct = default)
    {
        var payable = await db.Payables.FirstOrDefaultAsync(p => p.Id == payableId, ct)
            ?? throw new InvalidOperationException("Título não encontrado.");
        if (payable.Status != "awaiting_approval")
            throw new InvalidOperationException($"Título não está aguardando aprovação (status: {payable.Status}).");

        // Guarda-corpo: autoaprovação bloqueada.
        if (payable.CreatedBy is { } creator && creator == approverId)
            throw new InvalidOperationException("Quem lançou o título não pode aprová-lo (segregação de funções).");

        var tier = await ResolveTierAsync(payable.Amount, ct)
            ?? throw new InvalidOperationException("Nenhuma faixa de alçada cobre este valor.");

        // Guarda-corpo: papel deve pertencer à faixa.
        var roles = tier.RolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"O papel '{role}' não aprova títulos nesta faixa.");

        // Guarda-corpo: um aprovador não assina duas vezes.
        if (await db.PayableApprovals.AnyAsync(a => a.PayableId == payableId && a.ApproverId == approverId, ct))
            throw new InvalidOperationException("Este aprovador já assinou o título.");

        db.PayableApprovals.Add(new PayableApproval { PayableId = payableId, ApproverId = approverId, Role = role, Decision = "approved" });

        var approvals = await db.PayableApprovals.CountAsync(a => a.PayableId == payableId && a.Decision == "approved", ct) + 1;
        if (approvals >= RequiredSignatures(tier, payable.Amount))
        {
            payable.Status = "approved";
            payable.ApprovedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return payable;
    }

    public async Task<Payable> RejectAsync(Guid payableId, Guid approverId, string role, CancellationToken ct = default)
    {
        var payable = await db.Payables.FirstOrDefaultAsync(p => p.Id == payableId, ct)
            ?? throw new InvalidOperationException("Título não encontrado.");
        if (payable.Status != "awaiting_approval")
            throw new InvalidOperationException($"Título não está aguardando aprovação (status: {payable.Status}).");

        db.PayableApprovals.Add(new PayableApproval { PayableId = payableId, ApproverId = approverId, Role = role, Decision = "rejected" });
        payable.Status = "rejected";
        await db.SaveChangesAsync(ct);
        return payable;
    }
}

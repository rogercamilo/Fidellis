using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.Catalog;

/// <summary>
/// Convite de membro (D-01), no schema global <c>catalog</c>. Um coordenador/admin convida uma
/// pessoa por e-mail + papel. Para e-mail <b>novo</b>, gera-se um token de link mágico (guardado
/// só como <see cref="TokenHash"/>) que, ao ser aceito, cria o <see cref="User"/> + a
/// <see cref="Membership"/>. Para e-mail <b>já existente</b>, a membership é criada direto (sem convite).
/// Uso único; expira; o aceite invalida.
/// </summary>
public sealed class Invitation : Entity
{
    public required Guid TenantId { get; set; }

    /// <summary>E-mail normalizado (lower/trim) do convidado.</summary>
    public required string Email { get; set; }

    /// <summary>Papel a atribuir na membership ao aceitar (vocabulário de <c>FinanceRoles</c>).</summary>
    public required string Role { get; set; }

    /// <summary>Hash (SHA-256 hex) do token do link — nunca o token em claro.</summary>
    public required string TokenHash { get; set; }

    /// <summary>pending | accepted | revoked | expired.</summary>
    public string Status { get; set; } = "pending";

    public Guid? InvitedBy { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}

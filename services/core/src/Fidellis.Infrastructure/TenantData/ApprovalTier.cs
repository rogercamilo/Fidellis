using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Faixa de alçada de aprovação de Contas a Pagar (RF-FIN-112): a partir de <see cref="MinAmount"/>
/// (inclusive) até <see cref="MaxAmount"/> (exclusivo; nulo = infinito), exige <see cref="Signatures"/>
/// assinaturas dos papéis em <see cref="RolesCsv"/>. Parametrizável; guarda-corpos de compliance são
/// aplicados no serviço. Reside no schema do tenant.
/// </summary>
public sealed class ApprovalTier : Entity
{
    public decimal MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public int Signatures { get; set; } = 1;

    /// <summary>Papéis aprovadores separados por vírgula (ex.: <c>treasurer,manager</c>).</summary>
    public string RolesCsv { get; set; } = "treasurer";
}

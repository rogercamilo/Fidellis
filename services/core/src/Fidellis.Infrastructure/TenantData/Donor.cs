using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Doador (dados mínimos p/ montar o pedido no PSP). CRM completo é entregável futuro.</summary>
public sealed class Donor : Entity
{
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Document { get; set; }
    public string? Phone { get; set; }

    /// <summary>LGPD: doador optou por não receber comunicações (a régua o pula).</summary>
    public bool ContactOptOut { get; set; }

    /// <summary>LGPD: quando os dados pessoais foram anonimizados (erasure).</summary>
    public DateTimeOffset? AnonymizedAt { get; set; }

    // Configurabilidade/CRM (Onda 1). Jornada apoiador→recorrente (RF-FIN-182).
    public Guid? DonorTypeId { get; set; }

    /// <summary>Quando o doador se tornou recorrente pela primeira vez (nulo = ainda pontual).</summary>
    public DateTimeOffset? ConvertedAt { get; set; }
}

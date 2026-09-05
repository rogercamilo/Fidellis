using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Doador (dados mínimos p/ montar o pedido no PSP). CRM completo é entregável futuro.</summary>
public sealed class Donor : Entity
{
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Document { get; set; }
    public string? Phone { get; set; }
}

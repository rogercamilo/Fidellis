using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Credor/fornecedor (Contas a Pagar): fornecedor, voluntário (reembolso) ou pessoal (folha como
/// título). Guarda dados fiscais e a chave PIX para pagamento. Reside no schema do tenant.
/// </summary>
public sealed class Payee : Entity
{
    public required string Name { get; set; }
    public string? Document { get; set; }
    public string? PixKey { get; set; }

    /// <summary>supplier | volunteer | staff.</summary>
    public string Kind { get; set; } = "supplier";

    public bool Active { get; set; } = true;
}

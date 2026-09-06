using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Fundo (dimensão gerencial) do tenant, segregando recursos <b>livres</b> e <b>com restrição de
/// finalidade</b> — exigência da ITG 2002 para entidades sem finalidade de lucros. Recurso restrito
/// só pode ser consumido conforme a <see cref="Purpose"/>. Reside no schema do tenant.
/// </summary>
public sealed class Fund : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }

    /// <summary>free | restricted.</summary>
    public string Restriction { get; set; } = "free";

    /// <summary>Finalidade do recurso quando <c>restricted</c> (o que o doador/edital determinou).</summary>
    public string? Purpose { get; set; }

    /// <summary>Fundo livre padrão aplicado a lançamentos sem dimensão informada (RF-FIN-143).</summary>
    public bool IsDefault { get; set; }

    public bool Active { get; set; } = true;
}

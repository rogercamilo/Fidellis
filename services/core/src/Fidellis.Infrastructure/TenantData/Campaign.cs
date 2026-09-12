using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Campanha de arrecadação (D-08): agrupa doações por <b>finalidade/earmark</b> (não é um tipo de
/// entrada). Tem meta, janela (finita ou aberta) e vínculo opcional a um <b>fundo restrito</b> e/ou
/// projeto — recurso com restrição de finalidade (ITG 2002). Reside no schema do tenant.
/// </summary>
public sealed class Campaign : Entity
{
    public required Guid OrganizationId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }

    /// <summary>Meta de arrecadação (nula = sem meta).</summary>
    public decimal? GoalAmount { get; set; }

    /// <summary>Descrição pública da campanha.</summary>
    public string? Description { get; set; }

    /// <summary>Janela da campanha. Ambos nulos = aberta (sem prazo).</summary>
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>Fundo (restrito) que a campanha alimenta — as doações herdam para segregar o recurso.</summary>
    public Guid? FundId { get; set; }

    /// <summary>Projeto vinculado (opcional) — base para o "aplicado" da prestação de contas.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>active | closed.</summary>
    public string Status { get; set; } = "active";
}

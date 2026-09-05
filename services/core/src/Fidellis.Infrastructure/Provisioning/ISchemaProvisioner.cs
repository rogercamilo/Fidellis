namespace Fidellis.Infrastructure.Provisioning;

/// <summary>Cria e migra os schemas Postgres (catalog global e t_&lt;slug&gt; por tenant).</summary>
public interface ISchemaProvisioner
{
    /// <summary>Garante o schema global <c>catalog</c> e suas tabelas. Idempotente.</summary>
    Task EnsureCatalogAsync(CancellationToken ct = default);

    /// <summary>Cria o schema do tenant e suas tabelas (idempotente). Retorna o nome do schema.</summary>
    Task<string> ProvisionTenantAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Reaplica o DDL (idempotente) a todos os tenants já registrados em <c>catalog.tenants</c> —
    /// "migra" schemas existentes quando novas tabelas/colunas são adicionadas. Ponte até
    /// migrações EF versionadas (ADR-0002).
    /// </summary>
    Task EnsureAllTenantsAsync(CancellationToken ct = default);
}

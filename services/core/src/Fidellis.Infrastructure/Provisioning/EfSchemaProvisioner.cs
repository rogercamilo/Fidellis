using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Fidellis.Infrastructure.Provisioning;

/// <summary>
/// Provisiona/evolui schemas via <b>migrações EF versionadas</b> (DT-05), com histórico
/// (<c>__ef_migrations_history</c>) por schema. Substitui a DDL idempotente do
/// <see cref="SchemaProvisioner"/>. Schemas legados (criados pela DDL antiga) são <b>adotados</b>:
/// a baseline é carimbada como já aplicada antes de migrar o que vier depois.
/// </summary>
public sealed class EfSchemaProvisioner(
    InfrastructureOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<EfSchemaProvisioner> logger) : ISchemaProvisioner
{
    public async Task EnsureCatalogAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Adota o catalog legado (tabelas criadas pela DDL antiga, sem histórico).
        await AdoptBaselineIfLegacyAsync(
            catalog, CatalogDbContext.Schema, legacyTable: "tenants", ct);

        await catalog.Database.MigrateAsync(ct);
        logger.LogInformation("Schema catalog migrado (EF).");
    }

    public async Task<string> ProvisionTenantAsync(string slug, CancellationToken ct = default)
    {
        var schema = TenantContext.ToSchemaName(slug);

        // 1. Garante o schema (a migração cria as tabelas dentro dele via search_path).
        await using (var conn = new NpgsqlConnection(options.ConnectionString))
        {
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Abre um scope com o tenant resolvido — o TenantSearchPathInterceptor aponta o
        //    search_path para o schema; migrações e histórico resolvem nele.
        using var scope = scopeFactory.CreateScope();
        var tenantContext = (TenantContext)scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(slug);
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

        await AdoptBaselineIfLegacyAsync(db, schema, legacyTable: "organizations", ct);

        await db.Database.MigrateAsync(ct);
        logger.LogInformation("Schema do tenant {Schema} migrado (EF).", schema);
        return schema;
    }

    public async Task EnsureAllTenantsAsync(CancellationToken ct = default)
    {
        var slugs = new List<string>();
        await using (var conn = new NpgsqlConnection(options.ConnectionString))
        {
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT slug FROM \"catalog\".tenants", conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                slugs.Add(reader.GetString(0));
        }

        foreach (var slug in slugs)
            await ProvisionTenantAsync(slug, ct);

        logger.LogInformation("Migrações aplicadas a {Count} tenant(s).", slugs.Count);
    }

    /// <summary>
    /// Se o schema já tem tabelas (provisionado pela DDL legada) mas a baseline ainda não consta no
    /// histórico, cria a tabela de histórico (se preciso) e carimba a baseline como aplicada — assim
    /// o <see cref="RelationalDatabaseFacadeExtensions.MigrateAsync"/> não tenta recriar o que existe
    /// e passa a aplicar apenas as migrações posteriores.
    /// </summary>
    private async Task AdoptBaselineIfLegacyAsync(
        DbContext db, string schema, string legacyTable, CancellationToken ct)
    {
        var baselineId = db.Database.GetMigrations().First();
        var history = db.GetService<IHistoryRepository>();
        var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "9.0.4";
        var createScript = history.GetCreateScript();
        var insertScript = history.GetInsertScript(new HistoryRow(baselineId, productVersion));

        // Tudo numa única conexão com search_path fixo no schema alvo — determinístico e sem
        // depender do interceptor (que serve ao runtime, não ao provisionamento).
        await using var conn = new NpgsqlConnection(options.ConnectionString);
        await conn.OpenAsync(ct);
        await ExecAsync(conn, $"SET search_path TO \"{schema}\"", ct);

        var historyExists = await ScalarBoolAsync(conn,
            $"SELECT to_regclass('\"{schema}\".__ef_migrations_history') IS NOT NULL", ct);
        if (historyExists)
        {
            var alreadyBaseline = await ScalarBoolAsync(conn,
                $"SELECT EXISTS(SELECT 1 FROM \"{schema}\".__ef_migrations_history WHERE migration_id = '{baselineId}')", ct);
            if (alreadyBaseline)
                return; // já versionado.
        }

        // Legado = uma tabela conhecida já existe no schema alvo.
        var legacy = await ScalarBoolAsync(conn,
            $"SELECT to_regclass('\"{schema}\".{legacyTable}') IS NOT NULL", ct);
        if (!legacy)
            return; // schema novo/vazio: a migração criará tudo, incluindo o histórico.

        if (!historyExists)
            await ExecAsync(conn, createScript, ct);
        await ExecAsync(conn, insertScript, ct);
        logger.LogInformation("Baseline carimbada no schema legado {Schema}.", schema);
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> ScalarBoolAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }
}

using Fidellis.SharedKernel;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Fidellis.Infrastructure.Provisioning;

/// <summary>
/// Provisiona schemas via DDL idempotente executada diretamente no Postgres (Npgsql).
/// Mantém o bootstrap determinístico e independente de migrações EF geradas em disco —
/// adequado ao scaffold. A evolução de schema por migrações fica para entregável futuro.
/// </summary>
public sealed class SchemaProvisioner(
    InfrastructureOptions options,
    ILogger<SchemaProvisioner> logger) : ISchemaProvisioner
{
    private const string CatalogSchema = "catalog";

    public async Task EnsureCatalogAsync(CancellationToken ct = default)
    {
        var ddl = $"""
            CREATE SCHEMA IF NOT EXISTS "{CatalogSchema}";

            CREATE TABLE IF NOT EXISTS "{CatalogSchema}".tenants (
                id            uuid PRIMARY KEY,
                slug          varchar(63)  NOT NULL UNIQUE,
                name          varchar(200) NOT NULL,
                schema_name   varchar(63)  NOT NULL,
                plan          varchar(50)  NOT NULL DEFAULT 'trial',
                status        varchar(50)  NOT NULL DEFAULT 'active',
                created_at    timestamptz  NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{CatalogSchema}".users (
                id            uuid PRIMARY KEY,
                email         varchar(320) NOT NULL UNIQUE,
                password_hash text         NOT NULL,
                display_name  varchar(200),
                created_at    timestamptz  NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{CatalogSchema}".memberships (
                id         uuid PRIMARY KEY,
                user_id    uuid NOT NULL,
                tenant_id  uuid NOT NULL,
                role       varchar(50) NOT NULL DEFAULT 'member',
                created_at timestamptz NOT NULL DEFAULT now(),
                UNIQUE (user_id, tenant_id)
            );
            """;

        await ExecuteAsync(ddl, ct);
        logger.LogInformation("Schema catalog garantido.");
    }

    public async Task<string> ProvisionTenantAsync(string slug, CancellationToken ct = default)
    {
        var schema = TenantContext.ToSchemaName(slug);

        var ddl = $"""
            CREATE SCHEMA IF NOT EXISTS "{schema}";

            CREATE TABLE IF NOT EXISTS "{schema}".organizations (
                id         uuid PRIMARY KEY,
                name       varchar(200) NOT NULL,
                parent_id  uuid,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{schema}".accounts (
                id              uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                name            varchar(200) NOT NULL,
                currency        varchar(3) NOT NULL DEFAULT 'BRL',
                created_at      timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{schema}".transactions (
                id          uuid PRIMARY KEY,
                account_id  uuid NOT NULL,
                amount      numeric(18,2) NOT NULL,
                kind        varchar(20) NOT NULL DEFAULT 'credit',
                description text,
                created_at  timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{schema}".accounting_entries (
                id             uuid PRIMARY KEY,
                transaction_id uuid NOT NULL,
                ledger         varchar(100) NOT NULL,
                debit          numeric(18,2) NOT NULL DEFAULT 0,
                credit         numeric(18,2) NOT NULL DEFAULT 0,
                created_at     timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{schema}".donations (
                id              uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                amount          numeric(18,2) NOT NULL,
                method          varchar(20) NOT NULL DEFAULT 'pix',
                status          varchar(20) NOT NULL DEFAULT 'pending',
                donor_name      varchar(200),
                created_at      timestamptz NOT NULL DEFAULT now()
            );
            """;

        await ExecuteAsync(ddl, ct);
        logger.LogInformation("Schema do tenant {Schema} provisionado.", schema);
        return schema;
    }

    private async Task ExecuteAsync(string sql, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(options.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

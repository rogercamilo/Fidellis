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

            -- Índice global pedido->tenant: o webhook do PSP (sem nosso JWT) resolve o tenant por aqui.
            CREATE TABLE IF NOT EXISTS "{CatalogSchema}".psp_orders (
                provider_order_id varchar(100) PRIMARY KEY,
                tenant_slug       varchar(63)  NOT NULL,
                donation_id       uuid         NOT NULL,
                created_at        timestamptz  NOT NULL DEFAULT now()
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

            CREATE TABLE IF NOT EXISTS "{schema}".org_members (
                id              uuid PRIMARY KEY,
                user_id         uuid NOT NULL,
                organization_id uuid NOT NULL,
                role            varchar(50) NOT NULL DEFAULT 'member',
                created_at      timestamptz NOT NULL DEFAULT now(),
                UNIQUE (user_id, organization_id)
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
            ALTER TABLE "{schema}".accounting_entries ADD COLUMN IF NOT EXISTS ledger_account_id uuid;

            -- Plano de contas (chart of accounts) + recibos (passo 3).
            CREATE TABLE IF NOT EXISTS "{schema}".ledger_accounts (
                id             uuid PRIMARY KEY,
                code           varchar(20) NOT NULL UNIQUE,
                name           varchar(200) NOT NULL,
                type           varchar(20) NOT NULL,
                normal_balance varchar(10) NOT NULL,
                postable       boolean NOT NULL DEFAULT true,
                parent_id      uuid,
                created_at     timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{schema}".receipts (
                id              uuid PRIMARY KEY,
                number          varchar(30) NOT NULL,
                organization_id uuid NOT NULL,
                donation_id     uuid NOT NULL UNIQUE,
                donor_name      varchar(200) NOT NULL,
                donor_document  varchar(20),
                amount          numeric(18,2) NOT NULL,
                issued_at       timestamptz NOT NULL DEFAULT now(),
                created_at      timestamptz NOT NULL DEFAULT now(),
                UNIQUE (organization_id, number)
            );

            CREATE TABLE IF NOT EXISTS "{schema}".donors (
                id         uuid PRIMARY KEY,
                name       varchar(200) NOT NULL,
                email      varchar(320),
                document   varchar(20),
                phone      varchar(30),
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{schema}".campaigns (
                id              uuid PRIMARY KEY,
                organization_id uuid NOT NULL,
                title           varchar(200) NOT NULL,
                slug            varchar(120) NOT NULL,
                goal_amount     numeric(18,2),
                status          varchar(20) NOT NULL DEFAULT 'active',
                created_at      timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{schema}".psp_recipients (
                id                    uuid PRIMARY KEY,
                organization_id       uuid NOT NULL,
                provider_recipient_id varchar(100) NOT NULL,
                status                varchar(20) NOT NULL DEFAULT 'active',
                created_at            timestamptz NOT NULL DEFAULT now()
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

            -- Colunas de pagamento adicionadas ao passo 1 (idempotente p/ tenants já provisionados).
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS donor_id        uuid;
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS campaign_id     uuid;
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS psp_order_id    varchar(100);
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS psp_charge_id   varchar(100);
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS pix_qr_code     text;
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS pix_qr_code_url text;
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS expires_at      timestamptz;
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS paid_at         timestamptz;

            -- Recorrência (passo 2).
            CREATE TABLE IF NOT EXISTS "{schema}".recurring_donations (
                id               uuid PRIMARY KEY,
                organization_id  uuid NOT NULL,
                donor_id         uuid NOT NULL,
                amount           numeric(18,2) NOT NULL,
                frequency        varchar(20) NOT NULL DEFAULT 'monthly',
                day_of_month     int NOT NULL DEFAULT 1,
                status           varchar(20) NOT NULL DEFAULT 'active',
                next_charge_at   timestamptz NOT NULL,
                attempt          int NOT NULL DEFAULT 0,
                last_donation_id uuid,
                canceled_at      timestamptz,
                created_at       timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_recurring_due
                ON "{schema}".recurring_donations (status, next_charge_at);

            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS recurring_donation_id uuid;
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS due_at                timestamptz;
            ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS attempt               int NOT NULL DEFAULT 0;

            CREATE TABLE IF NOT EXISTS "{schema}".payment_events (
                id                 uuid PRIMARY KEY,
                provider           varchar(30)  NOT NULL DEFAULT 'pagarme',
                provider_event_id  varchar(100) NOT NULL UNIQUE,
                event_type         varchar(60)  NOT NULL,
                charge_id          varchar(100),
                payload            jsonb,
                status             varchar(20)  NOT NULL DEFAULT 'received',
                received_at        timestamptz  NOT NULL DEFAULT now(),
                processed_at       timestamptz
            );

            -- Régua de relacionamento (passo 4): outbox de mensagens.
            CREATE TABLE IF NOT EXISTS "{schema}".messages (
                id          uuid PRIMARY KEY,
                donor_id    uuid,
                channel     varchar(20)  NOT NULL DEFAULT 'email',
                event_type  varchar(40)  NOT NULL,
                template    varchar(40)  NOT NULL,
                to_address  varchar(320) NOT NULL,
                subject     varchar(300),
                body        text         NOT NULL,
                status      varchar(20)  NOT NULL DEFAULT 'queued',
                attempts    int          NOT NULL DEFAULT 0,
                dedupe_key  varchar(120) UNIQUE,
                error       text,
                created_at  timestamptz  NOT NULL DEFAULT now(),
                sent_at     timestamptz
            );
            CREATE INDEX IF NOT EXISTS ix_messages_status ON "{schema}".messages (status);

            -- LGPD: opt-out e anonimização do doador.
            ALTER TABLE "{schema}".donors ADD COLUMN IF NOT EXISTS contact_opt_out boolean NOT NULL DEFAULT false;
            ALTER TABLE "{schema}".donors ADD COLUMN IF NOT EXISTS anonymized_at   timestamptz;

            -- Trilha de auditoria (passo 6).
            CREATE TABLE IF NOT EXISTS "{schema}".audit_log (
                id            uuid PRIMARY KEY,
                actor_user_id uuid,
                action        varchar(60)  NOT NULL,
                entity        varchar(60)  NOT NULL,
                entity_id     varchar(100),
                metadata      text,
                created_at    timestamptz  NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_audit_created ON "{schema}".audit_log (created_at DESC);

            -- Dimensões gerenciais (Onda 1): centros de custo, fundos (com/sem restrição) e projetos.
            CREATE TABLE IF NOT EXISTS "{schema}".cost_centers (
                id         uuid PRIMARY KEY,
                code       varchar(20)  NOT NULL UNIQUE,
                name       varchar(200) NOT NULL,
                is_default boolean      NOT NULL DEFAULT false,
                active     boolean      NOT NULL DEFAULT true,
                created_at timestamptz  NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{schema}".funds (
                id          uuid PRIMARY KEY,
                code        varchar(20)  NOT NULL UNIQUE,
                name        varchar(200) NOT NULL,
                restriction varchar(12)  NOT NULL DEFAULT 'free',   -- free | restricted
                purpose     text,
                is_default  boolean      NOT NULL DEFAULT false,
                active      boolean      NOT NULL DEFAULT true,
                created_at  timestamptz  NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS "{schema}".projects (
                id            uuid PRIMARY KEY,
                code          varchar(20)  NOT NULL UNIQUE,
                name          varchar(200) NOT NULL,
                fund_id       uuid,
                budget_amount numeric(18,2),
                starts_at     date,
                ends_at       date,
                status        varchar(20)  NOT NULL DEFAULT 'active',
                created_at    timestamptz  NOT NULL DEFAULT now()
            );

            -- Dimensões nas transações e doações (default aplicado quando null — RF-FIN-143).
            ALTER TABLE "{schema}".transactions ADD COLUMN IF NOT EXISTS cost_center_id uuid;
            ALTER TABLE "{schema}".transactions ADD COLUMN IF NOT EXISTS project_id     uuid;
            ALTER TABLE "{schema}".transactions ADD COLUMN IF NOT EXISTS fund_id        uuid;
            ALTER TABLE "{schema}".donations    ADD COLUMN IF NOT EXISTS cost_center_id uuid;
            ALTER TABLE "{schema}".donations    ADD COLUMN IF NOT EXISTS project_id     uuid;
            ALTER TABLE "{schema}".donations    ADD COLUMN IF NOT EXISTS fund_id        uuid;
            """;

        await ExecuteAsync(ddl, ct);
        logger.LogInformation("Schema do tenant {Schema} provisionado.", schema);
        return schema;
    }

    public async Task EnsureAllTenantsAsync(CancellationToken ct = default)
    {
        var slugs = new List<string>();
        await using (var conn = new NpgsqlConnection(options.ConnectionString))
        {
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand($"SELECT slug FROM \"{CatalogSchema}\".tenants", conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                slugs.Add(reader.GetString(0));
        }

        foreach (var slug in slugs)
            await ProvisionTenantAsync(slug, ct);

        logger.LogInformation("DDL reaplicado a {Count} tenant(s).", slugs.Count);
    }

    private async Task ExecuteAsync(string sql, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(options.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

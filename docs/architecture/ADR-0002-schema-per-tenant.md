# ADR-0002 — Multi-tenancy: schema-per-tenant

- **Status:** Aceito (2026-09-04)

## Contexto

Fidellis gerencia dados **financeiros e contábeis** de muitas instituições religiosas independentes.
Requisitos: forte isolamento entre tenants (LGPD, auditoria, prestação de contas), possibilidade de
backup/restore e export por instituição, e um modelo de identidade global (um e-mail pode pertencer a
mais de uma instituição). As opções usuais são: (a) banco por tenant, (b) **schema por tenant**,
(c) tabela compartilhada com coluna `tenant_id` (RLS).

## Decisão

**Um schema Postgres por tenant**, mais um schema global `catalog`:

- **`catalog` (global):** `tenants`, `users`, `memberships`. Identidade/credencial é **global**;
  `memberships` liga `user ↔ tenant` com papel (RBAC).
- **`t_<slug>` (por tenant):** `organizations` (hierarquia Rede→Unidade), `accounts`, `transactions`,
  `accounting_entries`, `donations`.

Resolução em runtime: o core lê o claim `tenant` do JWT (`ITenantContext`), e o `TenantDbContext`
usa `HasDefaultSchema(t_<slug>)` com o **modelo compilado cacheado por schema**
(`IModelCacheKeyFactory`). O provisionamento cria o schema e as tabelas via DDL idempotente
(`SchemaProvisioner`).

## Alternativas consideradas

- **Banco por tenant:** isolamento máximo, mas custo operacional e de conexões alto; migrações e
  observabilidade mais caras em escala.
- **Tabela compartilhada + RLS:** mais barato por tenant, mas maior risco de vazamento entre tenants
  por erro de query/política; export/backup por instituição é mais trabalhoso — inaceitável para
  dados financeiros que exigem prestação de contas por unidade.

## Consequências

- **Positivas:** isolamento forte por instituição; backup/restore e export por schema; identidade
  global sem duplicar usuários; caminho natural para "consolidação da rede".
- **Negativas / trade-offs:** migrações precisam rodar em N schemas; limite prático de milhares de
  schemas por banco (mitigável com sharding por cluster no futuro); cache de modelo por schema no EF.
- **Roadmap:** migração de schema por versionamento (hoje o scaffold usa DDL idempotente), e um runner
  que aplica migrações a todos os `t_<slug>`.

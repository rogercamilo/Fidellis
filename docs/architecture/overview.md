# Visão de Arquitetura — Fidellis

Documento de visão (modelo C4, níveis 1–2) do Fidellis: SaaS multi-tenant de captação de doações
para o terceiro setor religioso. Decisões formais nos ADRs desta pasta.

## C1 — Contexto

```
   ┌───────────────┐        ┌───────────────┐        ┌────────────────────┐
   │  Doador        │        │  Gestor da     │        │  Gestor da rede     │
   │ (dízimo/oferta)│        │  unidade       │        │ (diocese/instituto) │
   └──────┬─────────┘        └──────┬────────┘        └─────────┬──────────┘
          │                         │                           │
          ▼                         ▼                           ▼
                     ┌───────────────────────────────┐
                     │            Fidellis            │
                     │  (captação + gestão + relatório)│
                     └───────────────┬───────────────┘
                                     │
             ┌───────────────────────┼───────────────────────┐
             ▼                       ▼                       ▼
       PSP (Pagar.me)          E-mail/WhatsApp         Storage (R2/S3)
    PIX / cartão / boleto      (recibos/régua)         (recibos/assets)
```

Atores: **doador** (contribui via link/portal), **gestor de unidade** (paróquia/casa), **gestor de
rede** (diocese/instituto que consolida várias unidades). Sistemas externos: **PSP** (Pagar.me/Stone),
**mensageria** (e-mail/WhatsApp) e **object storage** (Cloudflare R2/S3).

## C2 — Contêineres

```
Cloudflare (DNS + CDN + WAF)
        │  HTTPS
        ▼
┌──────────────┐   HTTPS/JSON   ┌──────────────┐   rede privada   ┌────────────────────────┐
│  web         │ ─────────────► │  bff         │ ───────────────► │  core (.NET 10)         │
│  Next.js/TS  │                │  NestJS      │   JWT c/ tenant  │  monólito modular        │
└──────────────┘                └──────┬───────┘                  └───────────┬────────────┘
                                       │ auth (catalog)                        │ EF Core + Npgsql
                                       ▼                                       ▼
                                 ┌───────────┐                          ┌───────────┐
                                 │ PostgreSQL│◄─────────────────────────│  Redis     │
                                 │ schema/tenant                        │ cache/fila │
                                 └───────────┘                          └───────────┘
```

- **web** (`apps/web`) — Next.js (App Router). Landing, login e painel. Deploy na borda Cloudflare
  (OpenNext). Fala apenas com o BFF.
- **bff** (`apps/bff`) — NestJS. **Auth standalone** (login global por e-mail contra `catalog`),
  emissão de JWT com claim de tenant, seleção de tenant e **proxy** de `/api/*` para o core. Ver
  [ADR-0004](ADR-0004-standalone-auth.md).
- **core** (`services/core`) — .NET 10, monólito modular (Tenant, Donations, Finance, Accounting,
  Reporting, Audit). Resolve o schema do tenant por request. Ver
  [ADR-0003](ADR-0003-modular-monolith.md).
- **PostgreSQL** — schema-per-tenant: `catalog` global + `t_<slug>` por instituição. Ver
  [ADR-0002](ADR-0002-schema-per-tenant.md).
- **Redis** — cache e fila (dunning/recorrência no roadmap).

## Fluxo de autenticação e resolução de tenant

1. `web` faz `POST /auth/login` no BFF com e-mail + senha (+ tenant opcional).
2. BFF valida contra `catalog.users` (hash Argon2) e consulta `catalog.memberships`.
3. BFF emite **JWT HS256** (segredo compartilhado com o core) com claim `tenant` = slug.
4. `web` chama `/api/...` no BFF; o BFF **encaminha** ao core repassando o `Authorization`.
5. O middleware do core valida o JWT, lê o claim `tenant` e resolve o schema `t_<slug>`
   (`ITenantContext`), usado pelo `TenantDbContext` (modelo compilado cacheado por schema).

## Ambientes

- **Local:** `docker compose` (Postgres 16 + Redis 7); apps via pnpm/dotnet. Ver README.
- **CI:** GitHub Actions — `core.yml` (build+test .NET) e `web-bff.yml` (lint+build+test Node).
- **Produção (roadmap):** borda Cloudflare; core em rede privada; segredos via Secrets Store.

## Fora do escopo do primeiro entregável

Fluxo real de cobrança (PIX/cartão/boleto), PIX Automático recorrente + dunning, CRM do doador,
régua de relacionamento (WhatsApp), razão contábil/recibos, dashboards e portal do doador.
Ver o [PRD](../prd/product-requirements.md).

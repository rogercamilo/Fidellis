# Fidellis

SaaS **multi-tenant** de captação de doações (dízimos, ofertas e campanhas) para o terceiro setor
religioso brasileiro — Novas Comunidades, Institutos Religiosos, dioceses, congregações e paróquias.

> **Diferencial:** assinatura pura com **0% de taxa** sobre as doações. O tenant fica com 100% do
> valor arrecadado (paga só o custo do adquirente), enquanto concorrentes cobram ~2,9% + R$0,25 por
> transação.

## Arquitetura

```
Cloudflare (DNS + CDN + WAF)
        │
        ▼
   Next.js (web)  ──►  NestJS (BFF)  ──►  .NET 10 (core, monólito modular)
                                              │
                                   ┌──────────┴──────────┐
                                   ▼                     ▼
                              PostgreSQL             Redis
                          (schema-per-tenant)    (cache/fila)
                                   │
                                   ▼
                            Cloudflare R2 / S3
```

- **Multi-tenancy:** schema-per-tenant. Um schema global `catalog` (`tenants`, `users`,
  `memberships`) e um schema por instituição `t_<slug>` (`organizations`, `accounts`,
  `transactions`, `accounting_entries`, `donations`).
- **Módulos do core:** Tenant, Donations, Finance, Accounting, Reporting, Audit.
- **Auth:** própria/standalone no BFF (JWT). Login global por e-mail resolve o tenant.
- **PSP:** Pagar.me / Stone (integração de cobrança fora deste primeiro entregável).

Detalhes em [`docs/architecture`](docs/architecture/overview.md) e requisitos em
[`docs/prd`](docs/prd/product-requirements.md).

## Estrutura do repositório

```
apps/web        Next.js (React/TS)
apps/bff        NestJS (Backend-for-Frontend)
services/core   .NET 10 monólito modular (Fidellis.slnx)
infra/          docker-compose (Postgres/Redis), Cloudflare
docs/           PRD e ADRs
```

## Desenvolvimento local

Pré-requisitos: Node 20+, pnpm 9+, .NET 10 SDK, Docker.

```bash
# 1. subir Postgres + Redis
pnpm infra:up

# 2. instalar dependências dos apps Node
pnpm install

# 3. core .NET
pnpm core:build && pnpm core:run     # http://localhost:5080/health

# 4. BFF + web
pnpm dev                             # BFF :4000  |  web :3000
```

Copie `.env.example` para `.env` antes de rodar.

## Status

🚧 Primeiro entregável: **scaffold + arquitetura** (base rodável, sem a lógica de negócio profunda
dos módulos). Roadmap no PRD.

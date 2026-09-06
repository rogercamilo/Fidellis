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
- **PSP:** Pagar.me / Stone — **cobrança PIX implementada** (checkout, webhook idempotente,
  conciliação e split 100% p/ a unidade). Ver [`ADR-0006`](docs/architecture/ADR-0006-payments-pix-pagarme.md).

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

### Cobrança PIX (Pagar.me)

O gestor gera cobranças em **/dashboard/cobranca** (o BFF anexa o tenant e encaminha ao core). Para o
fluxo completo:

1. Defina `PAGARME_API_KEY` (uma `sk_test_...` do sandbox) no `.env`.
2. Exponha o core (`:5080`) por um **túnel público** (ex.: `cloudflared tunnel --url http://localhost:5080`
   ou `ngrok http 5080`) e registre `<url-do-túnel>/api/finance/webhooks/pagarme` como webhook no
   painel do Pagar.me, com o Basic auth de `PAGARME_WEBHOOK_USER`/`PAGARME_WEBHOOK_PASSWORD`.
3. O webhook confirma o pagamento de forma idempotente e concilia (partida dobrada). O webhook fala
   **direto com o core**, não pelo BFF.

### Contabilidade + recibos

Ao confirmar um pagamento, o core lança a **partida dobrada** contra o **plano de contas** do tenant
(semeado no onboarding) e emite um **recibo** automático (número sequencial por unidade/ano). No web:
**/dashboard/recibos** (lista + recibo imprimível em `/recibo/{id}`) e **/dashboard/contabilidade**
(balancete consolidado das suas unidades). Ver
[`ADR-0009`](docs/architecture/ADR-0009-accounting-receipts.md).

### Recorrência (dízimo mensal) + dunning

O gestor cria recorrências em **/dashboard/recorrencia**. Um worker no core (`BillingWorker`) gera a
cobrança PIX de cada ciclo e aplica a régua de **dunning** (D+1/D+3/D+5 → `past_due`). Configuração no
`.env` (`BILLING_ENABLED`, `BILLING_INTERVAL_SECONDS`, `BILLING_DUNNING_DAYS`,
`BILLING_CYCLE_EXPIRY_SECONDS`). Em CI/testes use `BILLING_ENABLED=false`. Ver
[`ADR-0007`](docs/architecture/ADR-0007-recurring-donations-dunning.md).

## Status

🚧 Primeiro entregável: **scaffold + arquitetura** (base rodável, sem a lógica de negócio profunda
dos módulos). Roadmap no PRD.

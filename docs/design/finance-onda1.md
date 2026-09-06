# Onda 1 (Finance) — Plano de Implementação

> **Status:** rascunho para revisão · **Versão:** v0.1 — 2026-09-06
> **Escopo:** detalha a **Onda 1 — Fundação transacional** do módulo Finance em nível de
> implementação (modelo de dados, abstrações, contratos de API, sequência de build e testes).
> Base de requisitos: [`docs/requirements/finance.md`](../requirements/finance.md). Não altera
> comportamento até ser aprovado e desenvolvido.

---

## 1. Escopo da Onda 1

Entra (por sub-bloco de `finance.md`):

- **A — Captação & Recebimentos:** endurecimento (RF-FIN-001/002/003) + **boleto** (010/011/013) +
  **cartão** (020/021/022). *PIX Automático (030/031) fica no fim da onda, atrás da verificação
  do PSP (PSP-2).*
- **F — Contabilidade gerencial (dimensional):** centros de custo, projetos, **fundos com/sem
  restrição** e as 3 dimensões nas transações (RF-FIN-140/141/142/143).
- **J — Configurabilidade:** nomenclatura da doação recorrente/pontual, tipos de doador + jornada de
  conversão, rubricas (RF-FIN-180/181/182/183). *Painel de previsibilidade (184) fica na Onda 2, pois
  depende de AR/recorrência consolidados.*
- **I — Governança (parcial):** RBAC financeiro base — vocabulário de papéis + autorização nos
  endpoints (RF-FIN-171). *Alçadas (112) e fechamento (170) entram na Onda 2 com o AP.*

**Não entra:** AR, AP, tesouraria, conciliação, orçamento, demonstrações ITG 2002 (Ondas 2–4).

### 1.1 Estratégia de evolução de schema
Mantém o padrão atual do `SchemaProvisioner`: **DDL idempotente** (`CREATE TABLE IF NOT EXISTS`,
`ALTER TABLE ... ADD COLUMN IF NOT EXISTS`) aplicada a novos tenants (`ProvisionTenantAsync`) e
reaplicada aos existentes (`EnsureAllTenantsAsync`). Cada nova entidade também é mapeada em
`TenantDbContext.OnModelCreating`. Migrações EF versionadas seguem como item de infra futuro (não
bloqueia a Onda 1).

---

## 2. Modelo de dados

### 2.1 Novas tabelas — dimensões (Sub-bloco F)

```sql
-- Centros de custo (RF-FIN-140)
CREATE TABLE IF NOT EXISTS "{schema}".cost_centers (
    id         uuid PRIMARY KEY,
    code       varchar(20)  NOT NULL,
    name       varchar(200) NOT NULL,
    is_default boolean      NOT NULL DEFAULT false,
    active     boolean      NOT NULL DEFAULT true,
    created_at timestamptz  NOT NULL DEFAULT now(),
    UNIQUE (code)
);

-- Fundos com/sem restrição (RF-FIN-142) — base ITG 2002
CREATE TABLE IF NOT EXISTS "{schema}".funds (
    id          uuid PRIMARY KEY,
    code        varchar(20)  NOT NULL,
    name        varchar(200) NOT NULL,
    restriction varchar(12)  NOT NULL DEFAULT 'free',   -- free | restricted
    purpose     text,                                    -- finalidade, se restricted
    is_default  boolean      NOT NULL DEFAULT false,     -- o "fundo livre" default
    active      boolean      NOT NULL DEFAULT true,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    UNIQUE (code)
);

-- Projetos (RF-FIN-141)
CREATE TABLE IF NOT EXISTS "{schema}".projects (
    id            uuid PRIMARY KEY,
    code          varchar(20)  NOT NULL,
    name          varchar(200) NOT NULL,
    fund_id       uuid,                                  -- opcional: projeto vinculado a fundo restrito
    budget_amount numeric(18,2),
    starts_at     date,
    ends_at       date,
    status        varchar(20)  NOT NULL DEFAULT 'active',
    created_at    timestamptz  NOT NULL DEFAULT now(),
    UNIQUE (code)
);

-- Dimensões nas transações e doações (RF-FIN-143) — default aplicado quando null
ALTER TABLE "{schema}".transactions ADD COLUMN IF NOT EXISTS cost_center_id uuid;
ALTER TABLE "{schema}".transactions ADD COLUMN IF NOT EXISTS project_id     uuid;
ALTER TABLE "{schema}".transactions ADD COLUMN IF NOT EXISTS fund_id        uuid;
ALTER TABLE "{schema}".donations    ADD COLUMN IF NOT EXISTS cost_center_id uuid;
ALTER TABLE "{schema}".donations    ADD COLUMN IF NOT EXISTS project_id     uuid;
ALTER TABLE "{schema}".donations    ADD COLUMN IF NOT EXISTS fund_id        uuid;
```

> **Seeding no onboarding:** criar um `cost_center` "Geral" (`is_default=true`) e um `fund` "Recursos
> livres" (`restriction='free'`, `is_default=true`). São os defaults do RF-FIN-143 (D14).

### 2.2 Novas tabelas — configurabilidade (Sub-bloco J)

```sql
-- Configurações financeiras do tenant (linha única). RF-FIN-180/181 + defaults de dimensão.
CREATE TABLE IF NOT EXISTS "{schema}".finance_settings (
    id                     uuid PRIMARY KEY,
    recurring_label        varchar(60) NOT NULL DEFAULT 'Dízimo',      -- RF-FIN-180
    onetime_label          varchar(60) NOT NULL DEFAULT 'Oferta',      -- RF-FIN-181
    default_cost_center_id uuid,                                        -- RF-FIN-143
    default_fund_id        uuid,
    updated_at             timestamptz NOT NULL DEFAULT now()
);

-- Tipos de doador configuráveis (RF-FIN-182)
CREATE TABLE IF NOT EXISTS "{schema}".donor_types (
    id                   uuid PRIMARY KEY,
    name                 varchar(60) NOT NULL,   -- ex.: Membro, Apoiador
    is_recurring_default boolean     NOT NULL DEFAULT false,
    active               boolean     NOT NULL DEFAULT true,
    created_at           timestamptz NOT NULL DEFAULT now()
);

-- Jornada apoiador -> recorrente (RF-FIN-182)
ALTER TABLE "{schema}".donors ADD COLUMN IF NOT EXISTS donor_type_id uuid;
ALTER TABLE "{schema}".donors ADD COLUMN IF NOT EXISTS converted_at  timestamptz;  -- 1ª virada p/ recorrente

-- Rubricas de receita/despesa (RF-FIN-183) mapeadas ao plano de contas
CREATE TABLE IF NOT EXISTS "{schema}".finance_categories (
    id                uuid PRIMARY KEY,
    kind              varchar(10)  NOT NULL,   -- revenue | expense
    name              varchar(120) NOT NULL,
    ledger_account_id uuid,                    -- vínculo ao ledger_accounts
    active            boolean      NOT NULL DEFAULT true,
    created_at        timestamptz  NOT NULL DEFAULT now()
);
```

### 2.3 Colunas — endurecimento (Sub-bloco A / F1.1)

```sql
-- Idempotência de criação de cobrança (RF-FIN-003)
CREATE TABLE IF NOT EXISTS "{schema}".idempotency_keys (
    key         varchar(120) PRIMARY KEY,
    donation_id uuid        NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now(),
    expires_at  timestamptz NOT NULL
);
```

> RF-FIN-001 (assinatura de webhook) e RF-FIN-002 (rate limiting) são **só código/middleware** — sem
> mudança de schema.

### 2.4 Colunas — boleto (RF-FIN-010/013)

```sql
ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS boleto_line    varchar(60);   -- linha digitável
ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS boleto_barcode varchar(60);
ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS boleto_url     text;          -- PDF
ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS due_date       date;
-- 'expired' passa a ser valor válido de donations.status (RF-FIN-013)
```

### 2.5 Colunas/tabelas — cartão (RF-FIN-020/021/022)

```sql
ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS card_brand     varchar(20);
ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS card_last4     varchar(4);
ALTER TABLE "{schema}".donations ADD COLUMN IF NOT EXISTS decline_reason varchar(120);
-- 'declined' | 'refunded' | 'charged_back' passam a ser status válidos (RF-FIN-020/022)

-- Card-on-file (RF-FIN-021) — token do cartão salvo NO PSP (nunca o PAN)
CREATE TABLE IF NOT EXISTS "{schema}".payment_methods (
    id               uuid PRIMARY KEY,
    donor_id         uuid        NOT NULL,
    provider_card_id varchar(100) NOT NULL,   -- id no Pagar.me
    brand            varchar(20),
    last4            varchar(4),
    exp_month        int,
    exp_year         int,
    status           varchar(20) NOT NULL DEFAULT 'active',
    created_at       timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE "{schema}".recurring_donations ADD COLUMN IF NOT EXISTS payment_method_id uuid;  -- cartão recorrente

-- Cancelamento de recibo em estorno (RF-FIN-022 / D12)
ALTER TABLE "{schema}".receipts ADD COLUMN IF NOT EXISTS canceled_at   timestamptz;
ALTER TABLE "{schema}".receipts ADD COLUMN IF NOT EXISTS cancel_reason varchar(200);
```

### 2.6 Entidades EF / DbSets a adicionar
Em `TenantData/` + registro em `TenantDbContext`:
`CostCenter`, `Fund`, `Project`, `FinanceSettings`, `DonorType`, `FinanceCategory`, `IdempotencyKey`,
`PaymentMethod`. Colunas novas entram nas entidades existentes `Donation`, `Transaction`, `Donor`,
`Receipt`, `RecurringDonation`. Precisão `numeric(18,2)` via `HasPrecision(18,2)` no `OnModelCreating`
(padrão já usado).

---

## 3. Abstração de pagamento (`IPaymentGateway`)

Estender a interface (mantendo o *fake* dos testes em dia — RNF-05):

```csharp
// Boleto (RF-FIN-010)
Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest req, CancellationToken ct = default);

// Cartão à vista, tokenizado e SÍNCRONO (RF-FIN-020)
Task<CardChargeResult>  CreateCardOrderAsync(CreateCardOrderRequest req, CancellationToken ct = default);

// Card-on-file para recorrência (RF-FIN-021) — condicionado à verificação PSP-1
Task<SavedCardResult>   SaveCardAsync(SaveCardRequest req, CancellationToken ct = default);
Task<CardChargeResult>  ChargeSavedCardAsync(ChargeSavedCardRequest req, CancellationToken ct = default);
```

- **`CreateCardOrderRequest`** carrega `card_token` (do Pagar.me.js), `DonorDocument` (CPF
  obrigatório — D10), sem PAN. Sem campo de parcelas no MVP (D9).
- **`CardChargeResult`** já traz status síncrono (`paid`/`declined`) + `DeclineReason`.
- Estorno/chargeback continua chegando por **webhook** (novos tipos em `WebhookProcessor`), sem novo
  método de gateway.

---

## 4. Contratos de API (novos/alterados)

### 4.1 Checkout (alterado)
`POST /api/finance/donations` e `POST /api/public/{tenant}/donations`:
- Body ganha `method: "pix" | "boleto" | "card"` (default `pix`), `cardToken?` (quando `card`),
  e dimensões opcionais `costCenterId?`, `projectId?`, `fundId?` (default aplicado se ausente).
- Header `Idempotency-Key` (RF-FIN-003).
- Respostas: PIX/boleto → `pending` com dados da cobrança; cartão → `paid` ou `declined` na hora.

### 4.2 Configurabilidade (novos)
- `GET|PUT /api/finance/settings` — nomenclaturas + defaults de dimensão (RF-FIN-180/181).
- `GET|POST|PATCH /api/finance/cost-centers` · `/funds` · `/projects` · `/donor-types` ·
  `/categories` — CRUD das dimensões e rubricas (RF-FIN-140/141/142/183).

### 4.3 Guarda de autorização (RBAC — RF-FIN-171)
Vocabulário de papéis financeiros em `org_members.role` / `memberships.role`:
`treasurer` | `manager` | `fiscal_council` (somente leitura) | `accountant`. Endpoints de escrita
exigem `treasurer`/`manager`; leitura liberada a `fiscal_council`/`accountant`.

---

## 5. Configuração (`.env`)
```
PAGARME_WEBHOOK_SIGNATURE_SECRET=   # RF-FIN-001 (assinatura HMAC)
PUBLIC_RATE_LIMIT_PERMITS=10        # RF-FIN-002
PUBLIC_RATE_LIMIT_WINDOW_SECONDS=300
```

---

## 6. Sequência de build (incrementos ≈ 1 PR cada)

| # | Incremento | Entrega | Depende |
| --- | --- | --- | --- |
| **1.0** | **Dimensões (F)** | Tabelas `cost_centers`/`funds`/`projects` + colunas de dimensão + seeding default + CRUD | — |
| **1.1** | **Configurabilidade (J)** | `finance_settings`, `donor_types`, `finance_categories` + jornada de conversão + rótulos na UI | 1.0 |
| **1.2** | **Endurecimento (A/F1.1)** | Assinatura de webhook + rate limiting + `Idempotency-Key` | — |
| **1.3** | **Boleto (A/F1.2)** | `CreateBoletoOrderAsync` + colunas boleto + conciliação + `expired` | 1.2 |
| **1.4** | **Cartão (A/F1.3)** | `CreateCardOrderAsync` (síncrono) + card-on-file + estorno/cancelamento de recibo | 1.2 |
| **1.5** | **RBAC financeiro (I parcial)** | Papéis + guarda de autorização nos endpoints Finance | 1.0–1.4 |
| **1.6** | **PIX Automático (A/F1.4)** | Mandato + débito recorrente — **após verificação PSP-2** | 1.4 |

> 1.0 vem primeiro **de propósito**: sem as dimensões, todo lançamento criado nos incrementos
> seguintes nasceria incompleto. 1.2 (endurecimento) é pré-requisito de boleto/cartão por tocar o
> caminho de webhook/checkout.

---

## 7. Plano de testes (RNF-05)

- **Fake de `IPaymentGateway`:** implementar os novos métodos (boleto, cartão síncrono aprovado/
  recusado, card-on-file) — espelha `PagarmePaymentGateway` sem I/O real.
- **Integração (`Fidellis.IntegrationTests`):**
  - Boleto: checkout retorna linha digitável; webhook `boleto pago` → `paid` + partida dobrada +
    recibo; vencido → `expired`.
  - Cartão: token aprovado → `paid` na hora; recusa → `declined` + motivo; sem CPF → 400.
  - Estorno: webhook → `refunded`/`charged_back` + lançamento inverso + recibo `canceled`.
  - Idempotência: 2 chamadas com a mesma `Idempotency-Key` → 1 cobrança.
  - Webhook: assinatura inválida → 401.
  - Rate limit: flood no público → 429.
  - Dimensões: doação sem dimensão recebe os defaults; relatório cruza as 3.
  - Settings: alterar `recurring_label` reflete nas respostas.
- **CI:** `BILLING_ENABLED=false` (padrão vigente).

---

## 8. Riscos e itens de verificação

- **PSP-1 (card-on-file):** confirmar tokenização recorrente no plano Pagar.me antes do incremento
  1.4 (parte recorrente). *Fallback:* dízimo no cartão via novo checkout por ciclo.
- **PSP-2 (PIX Automático):** confirmar disponibilidade + requisitos BACEN antes do incremento 1.6.
- **Reaplicação de DDL:** `EnsureAllTenantsAsync` roda a DDL nova em todos os tenants — validar
  idempotência em base com dados (as colunas usam `ADD COLUMN IF NOT EXISTS`).
- **Cache de modelo por schema:** novas entidades exigem que o `SchemaModelCacheKeyFactory` continue
  chaveando por schema (já é o caso) para não vazar modelo entre tenants.

---

## 9. Definição de pronto (Onda 1)

- Checkout suporta PIX, boleto e cartão (à vista) com dimensões e idempotência.
- Toda doação/transação carrega centro de custo × projeto × fundo (default quando ausente).
- Nomenclatura e tipos de doador configuráveis por tenant, refletidos na UI.
- Estorno reverte contabilmente e cancela o recibo.
- RBAC financeiro aplicado aos endpoints do módulo.
- Testes de integração verdes; DDL reaplicável sem perda; `.env.example` atualizado.

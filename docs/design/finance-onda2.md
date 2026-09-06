# Onda 2 (Finance) — Plano de Implementação

> **Status:** rascunho para revisão do Product Owner · **Versão:** v0.1 — 2026-09-06
> **Escopo:** detalha a **Onda 2 — Ciclo financeiro** do módulo Finance em nível de implementação
> (modelo de dados, serviços, contratos de API, sequência de build e testes). Base de requisitos:
> [`docs/requirements/finance.md`](../requirements/finance.md) (Sub-blocos B, C, D, E, I). Não altera
> comportamento até ser aprovado e desenvolvido.

---

## 1. Escopo da Onda 2

O ciclo financeiro que transforma o Fidellis de "recebedor de doações" em **gestão financeira**:
o dinheiro que entra (AR), o que sai (AP com governança), onde está (Tesouraria) e a conferência do
caixa físico. Fecha o núcleo transacional iniciado na Onda 1.

Entra (por sub-bloco de `finance.md`):

- **B — Contas a Receber (AR):** promessas de doação (pledges), recebíveis de convênios/editais,
  aging + baixa (RF-FIN-100/101/102/103).
- **C — Contas a Pagar (AP):** fornecedores, despesas, **alçadas de aprovação** (RF-FIN-112),
  agendamento/pagamento, reembolso, rateio, folha como título (RF-FIN-110–116).
- **D — Tesouraria:** contas/caixas múltiplos, saldo consolidado, transferências internas, **fluxo de
  caixa projetado** (RF-FIN-120/121/122/124).
- **E (parcial) — Conciliação/caixa físico:** **caixa físico** com abertura/fechamento e dupla
  conferência (RF-FIN-132). *Importação OFX/CNAB (130/131/133) fica na Onda 3.*
- **I — Governança:** **fechamento de período** (RF-FIN-170) + trilha de auditoria financeira
  estendida (RF-FIN-172). *RBAC base já entregue na Onda 1.*

**Não entra:** conciliação bancária por extrato (OFX/CNAB), orçamento (G), demonstrações ITG 2002 (H),
aplicações/endowment (RF-FIN-123).

### 1.1 Estratégia de schema
Mantém o padrão do `SchemaProvisioner` (DDL idempotente) e o espelhamento em
`TenantDbContext.OnModelCreating`, como nas Ondas 1.x.

---

## 2. Modelo de dados

### 2.1 Contas a Receber (Sub-bloco B)

```sql
-- Promessas de doação / recebíveis (RF-FIN-100/101). Título a receber, distinto da doação paga.
CREATE TABLE IF NOT EXISTS "{schema}".receivables (
    id              uuid PRIMARY KEY,
    organization_id uuid         NOT NULL,
    donor_id        uuid,                       -- promessa de um doador
    source          varchar(20)  NOT NULL DEFAULT 'pledge',  -- pledge | grant | agreement
    description     varchar(200),
    amount          numeric(18,2) NOT NULL,
    due_date        date         NOT NULL,
    status          varchar(20)  NOT NULL DEFAULT 'open',     -- open | received | partial | canceled
    received_amount numeric(18,2) NOT NULL DEFAULT 0,
    cost_center_id  uuid, project_id uuid, fund_id uuid,      -- dimensões (Onda 1)
    donation_id     uuid,                        -- baixa vinculada à doação que a quitou
    created_at      timestamptz  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_receivables_due ON "{schema}".receivables (status, due_date);
```

### 2.2 Contas a Pagar (Sub-bloco C)

```sql
-- Credores/fornecedores (RF-FIN-110)
CREATE TABLE IF NOT EXISTS "{schema}".payees (
    id         uuid PRIMARY KEY,
    name       varchar(200) NOT NULL,
    document   varchar(20),
    pix_key    varchar(140),
    kind       varchar(20) NOT NULL DEFAULT 'supplier',  -- supplier | volunteer | staff
    active     boolean     NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now()
);

-- Títulos a pagar (RF-FIN-111/116)
CREATE TABLE IF NOT EXISTS "{schema}".payables (
    id              uuid PRIMARY KEY,
    payee_id        uuid         NOT NULL,
    category_id     uuid,                        -- finance_categories (Onda 1)
    description     varchar(200) NOT NULL,
    amount          numeric(18,2) NOT NULL,
    due_date        date         NOT NULL,
    status          varchar(20)  NOT NULL DEFAULT 'awaiting_approval',
                    -- awaiting_approval | approved | scheduled | paid | rejected | canceled
    document_url    text,                        -- anexo do documento fiscal
    cost_center_id  uuid, project_id uuid, fund_id uuid,
    approved_at     timestamptz, paid_at timestamptz,
    account_id      uuid,                        -- conta de tesouraria que pagou
    created_by      uuid,
    created_at      timestamptz  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_payables_status_due ON "{schema}".payables (status, due_date);

-- Rateio de um título por dimensão (RF-FIN-115)
CREATE TABLE IF NOT EXISTS "{schema}".payable_allocations (
    id             uuid PRIMARY KEY,
    payable_id     uuid NOT NULL,
    cost_center_id uuid, project_id uuid, fund_id uuid,
    amount         numeric(18,2) NOT NULL
);

-- Configuração de alçadas (RF-FIN-112) — faixas por valor + nº de assinaturas
CREATE TABLE IF NOT EXISTS "{schema}".approval_tiers (
    id            uuid PRIMARY KEY,
    min_amount    numeric(18,2) NOT NULL,     -- faixa [min, max)
    max_amount    numeric(18,2),              -- null = infinito
    signatures    int NOT NULL DEFAULT 1,
    roles_csv     varchar(200) NOT NULL,      -- papéis aprovadores (ex.: "treasurer,manager")
    created_at    timestamptz NOT NULL DEFAULT now()
);

-- Aprovações registradas de um título (trilha imutável)
CREATE TABLE IF NOT EXISTS "{schema}".payable_approvals (
    id          uuid PRIMARY KEY,
    payable_id  uuid NOT NULL,
    approver_id uuid NOT NULL,
    role        varchar(40) NOT NULL,
    decision    varchar(10) NOT NULL,          -- approved | rejected
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (payable_id, approver_id)
);
```

> **Guarda-corpos de compliance (RF-FIN-112)** aplicados no serviço, não só no schema: mínimo 1
> aprovação; **autoaprovação bloqueada** (quem criou ≠ aprovador); teto máx. **R$ 5.000** acima do
> qual 2 assinaturas são sempre exigidas (D13); faixas contínuas sem lacuna; recurso restrito
> respeitado.

### 2.3 Tesouraria (Sub-bloco D)

```sql
-- Contas financeiras: banco + caixa físico (RF-FIN-120). Reaproveita/estende "accounts" da Onda 1?
-- Decisão: nova tabela dedicada de tesouraria p/ não sobrecarregar "accounts" (contábil).
CREATE TABLE IF NOT EXISTS "{schema}".treasury_accounts (
    id              uuid PRIMARY KEY,
    organization_id uuid         NOT NULL,
    name            varchar(120) NOT NULL,
    kind            varchar(10)  NOT NULL DEFAULT 'bank',   -- bank | cash
    opening_balance numeric(18,2) NOT NULL DEFAULT 0,
    active          boolean      NOT NULL DEFAULT true,
    created_at      timestamptz  NOT NULL DEFAULT now()
);

-- Movimentos de tesouraria (entradas/saídas/transferências) — RF-FIN-121/122
CREATE TABLE IF NOT EXISTS "{schema}".treasury_movements (
    id              uuid PRIMARY KEY,
    account_id      uuid         NOT NULL,
    kind            varchar(12)  NOT NULL,      -- inflow | outflow | transfer_in | transfer_out
    amount          numeric(18,2) NOT NULL,
    description     varchar(200),
    counterpart_id  uuid,                        -- outra conta na transferência
    donation_id     uuid, payable_id uuid,       -- origem (recebimento/pagamento)
    occurred_at     timestamptz  NOT NULL DEFAULT now(),
    created_at      timestamptz  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_treasury_mov_account ON "{schema}".treasury_movements (account_id, occurred_at);
```

### 2.4 Caixa físico (Sub-bloco E parcial)

```sql
-- Sessões de caixa físico (coleta/oferta em espécie) — RF-FIN-132
CREATE TABLE IF NOT EXISTS "{schema}".cash_sessions (
    id              uuid PRIMARY KEY,
    account_id      uuid         NOT NULL,       -- treasury_account kind=cash
    opened_by       uuid         NOT NULL,
    opened_at       timestamptz  NOT NULL DEFAULT now(),
    event_label     varchar(120),               -- ex.: "Missa dom 10h"
    counted_amount  numeric(18,2),              -- conferência no fechamento
    confirmed_by    uuid,                        -- 2º responsável (dupla conferência)
    closed_at       timestamptz,
    status          varchar(12)  NOT NULL DEFAULT 'open',   -- open | closed
    deposited_movement_id uuid                   -- transferência p/ conta bancária no depósito
);
```

### 2.5 Governança / fechamento (Sub-bloco I)

```sql
-- Fechamento de período (RF-FIN-170): bloqueia lançamentos retroativos
CREATE TABLE IF NOT EXISTS "{schema}".accounting_periods (
    id          uuid PRIMARY KEY,
    year        int NOT NULL,
    month       int NOT NULL,
    status      varchar(10) NOT NULL DEFAULT 'open',  -- open | closed
    closed_by   uuid, closed_at timestamptz,
    UNIQUE (year, month)
);
```

### 2.6 Entidades EF / DbSets
`Receivable`, `Payee`, `Payable`, `PayableAllocation`, `ApprovalTier`, `PayableApproval`,
`TreasuryAccount`, `TreasuryMovement`, `CashSession`, `AccountingPeriod` + mapeamentos e seeding do
**tier default** (a tabela do RF-FIN-112, semeada no onboarding).

---

## 3. Serviços (Finance module)

- **`ReceivablesService`** — criar/baixar recebíveis; aging; baixa automática quando a doação vinculada
  é conciliada (gancho no `ReconciliationService.PostPaidAsync`).
- **`PayablesService`** — criar título, submeter a aprovação, aplicar rateio.
- **`ApprovalService`** — resolve a faixa por valor, valida guarda-corpos (autoaprovação, teto,
  assinaturas), registra aprovação/rejeição, muda status.
- **`TreasuryService`** — contas, saldo (opening + movimentos), consolidado da rede (via
  `OrgVisibility`), transferências internas (dupla perna), fluxo de caixa projetado
  (recorrências ativas + receivables + payables agendados por horizonte D+30/60/90).
- **`CashSessionService`** — abre/fecha caixa, conferência dupla, depósito → transferência.
- **`PeriodService`** — abre/fecha período; guarda que rejeita lançamentos em período fechado
  (aplicada em AP/AR/tesouraria).

Integrações: pagamento de AP debita tesouraria + gera partida dobrada (despesa) via um
`ReconciliationService` estendido; recebimento credita tesouraria e baixa o receivable.

---

## 4. Contratos de API (novos)

- **AR:** `GET|POST /api/finance/receivables`, `POST /receivables/{id}/settle`, `GET /receivables/aging`.
- **AP:** `GET|POST /api/finance/payees`, `GET|POST /payables`, `POST /payables/{id}/approve`,
  `POST /payables/{id}/reject`, `POST /payables/{id}/schedule`, `POST /payables/{id}/pay`,
  `GET|POST /approval-tiers`.
- **Tesouraria:** `GET|POST /treasury/accounts`, `GET /treasury/balance` (unidade + consolidado),
  `POST /treasury/transfers`, `GET /treasury/cashflow?horizon=90`.
- **Caixa físico:** `POST /cash-sessions/open`, `POST /cash-sessions/{id}/close`,
  `GET /cash-sessions`.
- **Fechamento:** `POST /periods/{year}/{month}/close`, `POST /periods/{year}/{month}/reopen`.

RBAC: mutações passam pelo `FinanceWriteFilter` (Onda 1). Aprovação de AP e reabertura de período
exigem papéis específicos (a definir — ver decisões).

---

## 5. Sequência de build (incrementos ≈ 1 PR cada)

| # | Incremento | Entrega | Depende |
| --- | --- | --- | --- |
| **2.0** | **Tesouraria (D)** | Contas/caixas, saldo, transferências, consolidado | Onda 1 |
| **2.1** | **Contas a Receber (B)** | Receivables + aging + baixa na conciliação | 2.0 |
| **2.2** | **Contas a Pagar (C) — base** | Payees, payables, rateio, lançamento de despesa | 2.0 |
| **2.3** | **Alçadas (C / RF-FIN-112)** | Tiers + ApprovalService + guarda-corpos + pagamento | 2.2 |
| **2.4** | **Fluxo de caixa projetado (D)** | Projeção D+30/60/90 (recorrências + AR + AP) | 2.1–2.3 |
| **2.5** | **Caixa físico (E parcial)** | Sessões, dupla conferência, depósito | 2.0 |
| **2.6** | **Fechamento de período (I)** | Períodos + guarda de lançamento retroativo | 2.1–2.3 |

> 2.0 (tesouraria) primeiro: AR/AP referenciam contas de tesouraria ao receber/pagar.

---

## 6. Plano de testes (RNF-05)

- **AR:** promessa gera receivable; conciliação da doação vinculada baixa o receivable; aging classifica
  vencidos.
- **AP:** título nasce `awaiting_approval`; rateio soma ao total; pagamento debita tesouraria + despesa.
- **Alçadas:** faixa resolvida por valor; **autoaprovação bloqueada**; valor > R$ 5.000 exige 2
  assinaturas; título só vai a `paid` após aprovações completas.
- **Tesouraria:** saldo = abertura + movimentos; transferência não altera resultado; consolidado
  respeita visibilidade.
- **Fluxo de caixa:** projeção soma corretamente por horizonte.
- **Caixa físico:** abre/fecha com dupla conferência; depósito gera transferência.
- **Fechamento:** lançamento em período fechado é rejeitado.
- **CI:** `BILLING_ENABLED=false`.

---

## 7. Decisões resolvidas (propostas aprovadas — 2026-09-06)

1. **Contas:** ✔ **nova tabela `treasury_accounts`** dedicada (separa tesouraria da conta contábil).
2. **Aprovação de AP — papéis:** ✔ segue o **default do RF-FIN-112** (tesoureiro / +gestor / +conselho fiscal).
3. **Pagamento de AP:** ✔ **só marca `paid` + movimento de tesouraria** (execução manual do PIX);
   remessa CNAB/PIX em lote fica p/ Onda 3.
4. **Baixa de AR:** ✔ **vínculo explícito** (doação carrega o `receivable_id`); casamento heurístico depois.
5. **Fluxo de caixa projetado:** ✔ **só `approved`/`scheduled`** (compromissos firmes), não os `awaiting_approval`.
6. **Caixa físico:** ✔ dupla conferência (2º responsável) **obrigatória** no fechamento.
7. **Fechamento de período:** ✔ reabertura exige **admin** + registro em auditoria.

---

## 8. Encaixe com a Onda 3+

Conciliação por extrato (OFX/CNAB) baixará AR/AP automaticamente; orçamento (G) consumirá as
dimensões e os payables; demonstrações ITG 2002 (H) consolidarão razão + tesouraria + fundos.

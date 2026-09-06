# Onda 3 (Finance) — Plano de Implementação

> **Status:** rascunho para revisão do Product Owner · **Versão:** v0.1 — 2026-09-06
> **Escopo:** detalha a **Onda 3 — Controle** do módulo Finance: **conciliação bancária** (extrato) e
> **orçamento**. Base de requisitos: [`docs/requirements/finance.md`](../requirements/finance.md)
> (Sub-blocos E e G). Segue o padrão das Ondas 1–2 (DDL idempotente, serviços, endpoints, testes).
> Não altera comportamento até ser aprovado e desenvolvido.

---

## 1. Escopo da Onda 3

Fecha o "controle" do ciclo financeiro: reconciliar o que o banco/caixa efetivamente movimentou contra
os títulos (AR/AP) e planejar/acompanhar o orçamento por dimensão.

Entra:

- **E — Conciliação bancária (RF-FIN-130/131/133):** importar **extrato** (OFX e CNAB retorno),
  **casamento** automático (sugestão) + baixa assistida de AR/AP e tesouraria, fila de **divergências**.
- **G — Orçamento (RF-FIN-150/151/152):** orçamento anual por **centro de custo/projeto/fundo**,
  **previsto × realizado** com alertas, revisões versionadas.

**Não entra:** Open Finance (agregação automática — evolução), demonstrações ITG 2002 (Onda 4).

### 1.1 Estratégia de schema
Mantém o `SchemaProvisioner` (DDL idempotente) + espelhamento em `TenantDbContext.OnModelCreating`.

---

## 2. Modelo de dados

### 2.1 Conciliação (Sub-bloco E)

```sql
-- Extrato importado (um arquivo/importação) — RF-FIN-130
CREATE TABLE IF NOT EXISTS "{schema}".bank_statements (
    id           uuid PRIMARY KEY,
    account_id   uuid         NOT NULL,     -- treasury_account (bank)
    format       varchar(10)  NOT NULL,     -- ofx | cnab
    reference    varchar(120),              -- nome do arquivo / período
    imported_at  timestamptz  NOT NULL DEFAULT now()
);

-- Linhas do extrato — RF-FIN-131/133
CREATE TABLE IF NOT EXISTS "{schema}".bank_statement_lines (
    id            uuid PRIMARY KEY,
    statement_id  uuid          NOT NULL,
    fit_id        varchar(120),             -- id da transação no extrato (dedupe OFX)
    posted_at     date          NOT NULL,
    amount        numeric(18,2) NOT NULL,   -- + entrada / - saída
    memo          varchar(200),
    status        varchar(12)   NOT NULL DEFAULT 'unmatched', -- unmatched | matched | ignored
    matched_type  varchar(12),              -- receivable | payable | movement
    matched_id    uuid,
    created_at    timestamptz   NOT NULL DEFAULT now(),
    UNIQUE (statement_id, fit_id)
);
CREATE INDEX IF NOT EXISTS ix_stmt_lines ON "{schema}".bank_statement_lines (status, posted_at);
```

### 2.2 Orçamento (Sub-bloco G)

```sql
-- Orçamento por dimensão e período (RF-FIN-150) + revisões (RF-FIN-152)
CREATE TABLE IF NOT EXISTS "{schema}".budgets (
    id             uuid PRIMARY KEY,
    year           int          NOT NULL,
    cost_center_id uuid, project_id uuid, fund_id uuid,
    kind           varchar(10)  NOT NULL,   -- revenue | expense
    amount         numeric(18,2) NOT NULL,
    revision       int          NOT NULL DEFAULT 1,
    active         boolean      NOT NULL DEFAULT true,
    created_at     timestamptz  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_budgets_year ON "{schema}".budgets (year, kind);
```

### 2.3 Entidades EF / DbSets
`BankStatement`, `BankStatementLine`, `Budget` + mapeamentos.

---

## 3. Serviços

- **`StatementImportService`** — parse **OFX** (XML/SGML) e **CNAB retorno** (posições fixas) para
  linhas normalizadas; dedupe por `fit_id`; idempotência por importação.
- **`ReconciliationMatchService`** — sugere casamento de cada linha por **valor + data (± janela)**
  contra: recebíveis em aberto (entrada), payables aprovados/pagos e movimentos de tesouraria (saída);
  aplica a baixa (settle AR / marca AP / cria movimento) ao confirmar; fila de divergências.
- **`BudgetService`** — CRUD de orçamento por dimensão/ano; **previsto × realizado** (realizado vem de
  `transactions`/`accounting_entries` por dimensão); alertas de estouro; revisões versionadas.

> **Reuso:** a baixa de AR reaproveita `ReceivablesService`; a de AP, o fluxo de pagamento; tesouraria,
> o `TreasuryService`.

---

## 4. Contratos de API (novos)

- **Conciliação:** `POST /api/finance/statements/import` (arquivo OFX/CNAB),
  `GET /statements`, `GET /statements/{id}/lines`, `POST /statement-lines/{id}/match`
  (confirma sugestão) e `POST /statement-lines/{id}/ignore`.
- **Orçamento:** `GET|POST /api/finance/budgets`, `GET /budgets/actual?year=` (previsto × realizado por
  dimensão), `POST /budgets/{id}/revise`.

RBAC: mutações via `FinanceWriteFilter` (Onda 1). Import e conciliação exigem papel de escrita.

---

## 5. Sequência de build (incrementos ≈ 1 PR cada)

| # | Incremento | Entrega | Depende |
| --- | --- | --- | --- |
| **3.0** | **Import de extrato (OFX)** | Parser OFX + `bank_statements`/`lines` + listagem | Onda 2 |
| **3.1** | **Casamento + baixa** | Sugestão por valor/data + confirmar/ignorar + baixa AR/AP/tesouraria | 3.0 |
| **3.2** | **CNAB retorno** | Parser CNAB (posições fixas) reusando o pipeline de linhas | 3.0 |
| **3.3** | **Orçamento** | CRUD por dimensão + previsto × realizado + alertas | Onda 2 |
| **3.4** | **Revisões de orçamento** | Versionamento + histórico | 3.3 |

> 3.0 (OFX) primeiro por ser universal e sem dependência de layout bancário; CNAB (3.2) entra depois.

---

## 6. Plano de testes (RNF-05)

- **Import OFX:** parse de um extrato de exemplo → N linhas; dedupe por `fit_id` (reimport não duplica).
- **Casamento:** linha de entrada casa com recebível de mesmo valor/data → baixa; saída casa com payable
  pago/movimento; divergência fica `unmatched`.
- **CNAB:** parse de retorno de exemplo → linhas normalizadas.
- **Orçamento:** previsto × realizado soma o realizado por dimensão; alerta quando realizado > previsto;
  revisão preserva a versão anterior.
- **CI:** `BILLING_ENABLED=false`.

---

## 7. Decisões resolvidas (propostas aprovadas — 2026-09-06)

1. **Formato prioritário:** ✔ **OFX primeiro** (3.0), **CNAB** depois (3.2).
2. **Casamento:** ✔ **sugere + confirma**; auto-baixa apenas em match **exato** (valor+data+fit).
3. **Janela de data:** ✔ **± 3 dias**, configurável.
4. **Realizado do orçamento:** ✔ **competência** (`accounting_entries` por dimensão).
5. **Upload de arquivo:** ✔ na primeira entrega o core recebe o **conteúdo do arquivo como string**
   no corpo JSON (o front lê o arquivo no client e envia o texto) — flui pelo **proxy JSON genérico**
   do BFF sem endpoint dedicado. **Multipart** via BFF fica como evolução, se necessário.

---

## 8. Encaixe com a Onda 4

A conciliação fecha a base factual (caixa × títulos) e o orçamento fecha o planejamento — insumos
diretos para as **demonstrações ITG 2002** e a **prestação de contas** (Onda 4).

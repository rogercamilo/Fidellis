# Onda 4 (Finance) — Plano de Implementação

> **Status:** rascunho para revisão do Product Owner · **Versão:** v0.1 — 2026-09-06
> **Escopo:** detalha a **Onda 4 — Prestação de contas & Compliance** do módulo Finance: demonstrações
> ITG 2002, segregação de recursos com/sem restrição, trabalho voluntário, prestação de contas MROSC,
> exportação para o contador e portal de transparência. Base de requisitos:
> [`docs/requirements/finance.md`](../requirements/finance.md) (Sub-bloco H). Segue o padrão das Ondas
> 1–3 (DDL idempotente, serviços, endpoints, testes). Não altera comportamento até ser aprovado.

---

## 1. Escopo da Onda 4

Fecha o ciclo: transformar os lançamentos (razão), a tesouraria e as dimensões em **prestação de
contas** conforme a **ITG 2002 (R1)** e as exigências do terceiro setor. **Posicionamento (decisão
D2):** o sistema gera o **rascunho** das demonstrações; o **contador valida/assina** fora.

Entra (por sub-bloco de `finance.md`):

- **Demonstrações ITG 2002 (RF-FIN-160):** Balanço Patrimonial, **DRP** (superávit/déficit, não
  "lucro"), **DMPL**, **DFC** e apoio a Notas Explicativas.
- **Segregação com/sem restrição (RF-FIN-161):** demonstrações separam recursos livres e restritos
  (via `funds.restriction`).
- **Trabalho voluntário a valor justo (RF-FIN-162):** registro como se houvesse desembolso.
- **Prestação de contas MROSC (RF-FIN-163):** relatório de execução por convênio/edital.
- **Exportação para o contador (RF-FIN-165):** razão/saldos em CSV (ECD/SPED-friendly).
- **Portal de transparência (RF-FIN-164):** página pública por unidade (receitas/despesas/projetos).

**Não entra:** SPED ECD/ECF completo (exportação CSV primeiro), fundo patrimonial/endowment ativo
(RF-FIN-123, futuro), assinatura eletrônica jurídica das demonstrações (o contador assina fora).

### 1.1 Base reaproveitada
- **Razão:** `accounting_entries` + `ledger_accounts` (tipo/normal_balance) — já alimentados pela
  conciliação (receita) e pelo pagamento de AP (despesa).
- **Dimensões/fundos:** `funds.restriction` para a segregação; centros de custo/projetos para MROSC.
- **Tesouraria:** `treasury_movements` para a DFC (método direto).

---

## 2. Modelo de dados

Onda 4 é majoritariamente **leitura/agregação** sobre o que já existe. Novas tabelas mínimas:

```sql
-- Trabalho voluntário a valor justo (RF-FIN-162)
CREATE TABLE IF NOT EXISTS "{schema}".volunteer_work (
    id              uuid PRIMARY KEY,
    organization_id uuid          NOT NULL,
    description     varchar(200)  NOT NULL,
    fair_value      numeric(18,2) NOT NULL,
    performed_on    date          NOT NULL,
    cost_center_id  uuid, project_id uuid, fund_id uuid,
    transaction_id  uuid,                       -- lançamento contábil gerado
    created_at      timestamptz   NOT NULL DEFAULT now()
);

-- Snapshot de demonstração emitida (auditoria/versão) — opcional p/ histórico
CREATE TABLE IF NOT EXISTS "{schema}".financial_statements (
    id           uuid PRIMARY KEY,
    kind         varchar(20)  NOT NULL,   -- balance_sheet | income | dmpl | cashflow
    year         int          NOT NULL,
    payload      jsonb        NOT NULL,   -- demonstração calculada, congelada
    generated_by uuid,
    generated_at timestamptz  NOT NULL DEFAULT now()
);
```

> Convênios/editais MROSC reusam `receivables (source='grant'|'agreement')` + `projects` — não exigem
> tabela nova.

---

## 3. Serviços

- **`LedgerReportService`** — saldos por conta (trial balance) a partir de `accounting_entries`; base
  do BP e da DRP.
- **`StatementsService`** — monta as demonstrações ITG 2002:
  - **Balanço Patrimonial:** Ativo/Passivo/PL por saldo das contas (tipo).
  - **DRP:** Receitas − Despesas = **superávit/déficit** do período.
  - **DMPL:** PL inicial + superávit/déficit + ajustes = PL final.
  - **DFC (método direto):** entradas − saídas de `treasury_movements` no período.
  - Cada uma com recorte **com/sem restrição** (RF-FIN-161).
- **`VolunteerWorkService`** — registra o trabalho voluntário e lança a partida dobrada a valor justo
  (débito despesa "serviços voluntários" / crédito receita "trabalho voluntário") no plano de contas.
- **`MroscReportService`** — por convênio/edital: recebíveis (grant) recebidos × despesas do projeto.
- **`AccountantExportService`** — razão e saldos em CSV para o contador.

Plano de contas ganha contas de **trabalho voluntário** (receita/despesa) no seeding.

---

## 4. Contratos de API

- **Demonstrações:** `GET /api/finance/statements/balance-sheet?year=`,
  `GET /statements/income?year=` (DRP), `GET /statements/dmpl?year=`,
  `GET /statements/cashflow?year=` — cada um com `restriction=free|restricted|all`.
- **Trabalho voluntário:** `GET|POST /api/finance/volunteer-work`.
- **MROSC:** `GET /api/finance/mrosc/{projectId}` (execução).
- **Exportação:** `GET /api/finance/export/ledger?year=` (CSV), `GET /export/trial-balance?year=`.
- **Transparência (público):** `GET /api/public/{tenant}/transparency?year=` (sem dados pessoais;
  rate-limited como o resto do público).

RBAC: leituras de demonstrações liberadas a `fiscal_council`/`accountant`; escrita (voluntário) via
`FinanceWriteFilter`.

---

## 5. Sequência de build (incrementos ≈ 1 PR cada)

| # | Incremento | Entrega | Depende |
| --- | --- | --- | --- |
| **4.0** | **Balancete + BP + DRP** | `LedgerReportService` + Balanço + DRP (superávit/déficit) | Ondas 1–3 |
| **4.1** | **Segregação + DMPL** | Recorte com/sem restrição + DMPL | 4.0 |
| **4.2** | **DFC + trabalho voluntário** | DFC (direto) + registro/lançamento de voluntariado | 4.0 |
| **4.3** | **MROSC + exportação contador** | Execução por convênio + CSV razão/saldos | 4.0 |
| **4.4** | **Portal de transparência** | Página pública por unidade (receitas/despesas/projetos) | 4.0–4.3 |

> 4.0 primeiro: o balancete é a base de todas as demonstrações. 4.4 por último (consome os anteriores).

---

## 6. Plano de testes (RNF-05)

- **Balancete:** débitos = créditos; saldos por conta corretos a partir de lançamentos conhecidos.
- **DRP:** superávit = receitas − despesas do período.
- **BP:** Ativo = Passivo + PL (com o superávit no PL).
- **Segregação:** recursos restritos aparecem separados dos livres.
- **DFC:** entradas − saídas de tesouraria batem com a variação de caixa.
- **Voluntário:** registro gera partida dobrada a valor justo (receita e despesa).
- **MROSC:** execução por projeto soma recebido × gasto.
- **Exportação:** CSV do razão contém os lançamentos do ano.
- **CI:** `BILLING_ENABLED=false`.

---

## 7. Decisões pendentes (para a revisão do PO)

1. **DFC:** método **direto** (a partir de `treasury_movements`) ou **indireto** (a partir do
   resultado)? Proposta: **direto** — temos os movimentos e é mais simples/claro p/ o terceiro setor.
2. **Exportação:** **CSV** de razão/saldos agora e **SPED ECD** depois? Proposta: **CSV primeiro**.
3. **Trabalho voluntário:** entrada **manual** (o gestor lança a valor justo) ou também estimativa por
   horas × valor/hora? Proposta: **manual** (valor justo informado) na primeira entrega.
4. **Transparência:** quais números públicos e granularidade (mensal/anual; por unidade/consolidado)?
   Proposta: **anual por unidade**, receitas/despesas por rubrica + lista de projetos, **sem dados
   pessoais**.
5. **Snapshot de demonstrações:** congelar a demonstração emitida (tabela `financial_statements`) para
   histórico/versão, ou sempre calcular on-the-fly? Proposta: **on-the-fly** agora; snapshot quando
   houver assinatura/aprovação formal (RF-FIN-173).

---

## 8. Encerramento do módulo Finance

Com a Onda 4, o Finance cobre o ciclo completo do terceiro setor: **captação** (Onda 1) → **ciclo
financeiro** (Onda 2) → **controle** (Onda 3) → **prestação de contas** (Onda 4). Evoluções pós-Onda 4
ficam nas verificações PSP (card-on-file, PIX Automático), Open Finance, SPED completo e endowment.

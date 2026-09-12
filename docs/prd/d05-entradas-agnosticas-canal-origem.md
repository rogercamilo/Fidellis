# D-05 — Registro de entradas agnóstico a canal e origem

> **Status:** ✅ **implementada** (opção **conservadora**) · 2026-09-12.
> **Entregue:** `Donation.Source` (`checkout|portal|cash|manual`) + migração `EntrySource`; ledger `Caixa`
> (1.1.1) na conciliação; `ReconciliationService.PostEntryAsync` (contrapartida por origem + recibo
> condicional); `CashSessionService.CloseAsync` gera entrada de 1ª classe (débito Caixa/crédito Receita,
> sem recibo — coleta anônima); `ManualEntryService` + `POST /api/finance/entries` (RBAC lançador,
> recibo se doador); UI painel "Lançamento manual de entrada" na tela de Caixa. Testes: `ManualEntryTests`
> (3) + `CashSessionTests` (entrada de 1ª classe) → 171 verdes. **Diferido:** entidade `Entry` canônica,
> caixa discriminado, `entry_type` (D-06) e recibo-sempre.
> **Origem:** decisão **D-05** do [parecer do terceiro setor](parecer-finance-terceiro-setor.md) e a
> recomendação #1 da §7 ("caixa físico gera receita/entrada; criar lançamento manual").
> **Fluxo:** detalhar → **PO revisa/ajusta** → só então codar. Este documento é a etapa "detalhar".
> **Relação com a D-06:** o PO decidiu **antecipar `entry_type`** aqui (Q4); o eixo restante da D-06
> (ator × frequência, gating de portal, campanhas) permanece na D-06.
> **Referências de código:** `Donation`, `ReconciliationService.PostPaidAsync`, `CashSessionService.CloseAsync`,
> `ReceiptService`, `WebhookProcessor`, `Transaction`/`AccountingEntry`, `TreasuryMovement`.

---

## 1. Objetivo

Hoje **só o checkout/portal produz uma "entrada de 1ª classe"**: uma `Donation` paga dispara
`ReconciliationService.PostPaidAsync`, que cria a `Transaction` + a partida dobrada (débito Banco /
crédito Receita) + `TreasuryMovement` + recibo + régua de relacionamento. As demais origens ficam de fora:

- **Caixa físico** (`CashSessionService.CloseAsync`) só cria um `TreasuryMovement` de entrada no caixa —
  **sem receita, sem dimensão, sem tipo, sem recibo**. A maior fonte de muitas casas (oferta do culto)
  fica invisível na gestão e na contabilidade.
- **Lançamento manual** (recebimento fora do PSP — transferência, depósito avulso) **não existe**.

O objetivo da D-05 é fazer **toda entrada nascer igual** — receita + dimensão + visível — venha do
checkout (PIX/boleto/cartão), do **caixa físico**, de **lançamento manual** ou de um **portal**.

## 2. Estado atual (o que existe no código)

| Peça | Situação |
| --- | --- |
| **Entrada via PSP** | ✔ `Donation` → `PostPaidAsync`: transação + partida dobrada + tesouraria + recibo + régua. É a única "entrada de 1ª classe". |
| **Caixa físico** | 🔴 `CashSessionService.CloseAsync` só adiciona `TreasuryMovement` (inflow) no caixa. Sem receita/dimensão/tipo/recibo. `DepositAsync` faz a transferência caixa→banco. |
| **Lançamento manual** | 🔴 Inexistente. Não há como registrar um recebimento fora do PSP. |
| **`entry_type`** | 🟠 Não é de 1ª classe. O tipo é inferido de (recorrente?) × (tipo de doador). |
| **`Donation` acoplada** | 🟠 ~45 arquivos referenciam `Donation` (webhook, recibo, conciliação, relatórios, CRM, recebíveis, recorrência, tesouraria, transparência). |

## 3. Decisões do PO (2026-09-12) — opção conservadora

Após o detalhamento, o PO optou pelo caminho de **menor risco** (a alternativa ambiciosa foi considerada
e diferida — ver §11). Decisões finais:

- **Q1 — Reusar `Donation` + campo `source`.** Caixa e manual nascem como `Donation` já `paid` com
  `source` ∈ `{ checkout, portal, cash, manual }`. Como caixa/manual passam a ser `Donation`, **todos os
  leitores atuais** (relatórios, CRM, transparência, tesouraria, recebíveis) já os incluem sem refactor.
  A entidade `Entry` canônica fica como esforço futuro dedicado.
- **Q2 — Caixa agregado.** O fechamento gera **uma** entrada de receita pelo total conferido (débito
  **Caixa** / crédito Receita), com dimensões default. A quebra por tipo/finalidade fica para a D-06.
- **Q3 — Recibo condicional.** Emite recibo **quando há doador identificado** (lançamento manual
  nominal); caixa agregado (coleta anônima) **não** emite recibo — fica na contabilidade/trilha.
- **Q4 — `entry_type` fica na D-06.** A D-05 cobre **origem/canal + dimensões**; o eixo taxonômico
  (`entry_type` × ator × frequência, gating de portal) permanece na D-06.

> Efeito combinado: entrega o **coração da D-05** (caixa e lançamento manual viram receita de 1ª classe,
> visíveis na gestão e na contabilidade) com uma fração do risco, e encadeia limpo na D-06/D-08.

## 4. Modelo de dados (proposto)

Entidade base **`Entry`** (schema do tenant), com os atributos comuns de "entrada":

| Coluna | Nota |
| --- | --- |
| `id`, `organization_id`, `amount`, `status` | comuns |
| `entry_type` | `dízimo` \| `oferta` \| `doação` (Q4) |
| `source` | `pix` \| `boleto` \| `card` \| `cash` \| `manual` \| `portal` (Q1/Q4) |
| `donor_id` (nullable → ver §7) | doador/associado |
| `cost_center_id` / `project_id` / `fund_id` | dimensões (default aplicado) |
| `campaign_id` | earmark (finaliza na D-08) |
| `occurred_at` / `paid_at` | competência |
| `cash_session_id` (nullable) | vínculo com a sessão de caixa (Q2) |

**Estratégia de migração de `Donation` (o maior risco):** duas abordagens possíveis —

- **(4a) TPH/tabela renomeada:** `Donation` vira `Entry` (renomeia a tabela `donations`→`entries`,
  adiciona `source`/`entry_type`; campos de PSP/boleto/cartão passam a ser anuláveis específicos da origem
  PSP). Migração EF de schema + reescrita ampla dos ~45 pontos de uso.
- **(4b) `Entry` nova + `Donation` mantida como visão PSP:** cria `entry` genérica; a conciliação passa a
  gravar `Entry`; relatórios/transparência/CRM passam a somar `Entry`. Convivência temporária maior.

> **Recomendação técnica:** 4a (uma tabela de entrada canônica) é o alvo limpo alinhado à decisão Q1, mas
> exige uma migração cuidadosa (dados + código + testes) — ver §8. Confirmar a abordagem no review.

## 5. Caixa discriminado (Q2)

No fechamento (`CloseAsync`), além do valor conferido e da dupla conferência, o operador informa **N
linhas** `(entry_type, valor, dimensões/finalidade, doador?)`. Regra: **Σ linhas == valor conferido**
(senão erro). Cada linha gera uma `Entry` `cash` conciliada (débito **Caixa** / crédito Receita) com as
dimensões informadas. O **depósito** caixa→banco (`DepositAsync`) permanece uma **transferência de
tesouraria** (não é receita — evita dupla contagem).

## 6. Contabilidade por origem (generalizar `PostPaidAsync`)

`PostPaidAsync` hoje **fixa** débito no **Banco** + `TreasuryMovement` na conta bancária. Precisa
parametrizar a **conta de contrapartida (tesouraria + razão)** por origem:

| Origem | Débito (ativo) | Crédito | Tesouraria |
| --- | --- | --- | --- |
| PSP (pix/boleto/card/portal) | Banco | Receita | conta **bank** da unidade |
| **Caixa** | **Caixa** | Receita | conta **cash** da sessão |
| **Manual** | conta escolhida (banco/caixa) | Receita | a conta escolhida |

## 7. ⚠️ Ponto em aberto — recibo × coleta anônima (Q3)

A Q3 pede **recibo sempre** e recibo exige um **doador identificado**. Mas a **coleta do culto** é
tipicamente **anônima/agregada**. As linhas do caixa (Q2) nem sempre terão doador. Opções para o review:

- **(7a) Doador "Coleta" genérico:** entradas de caixa sem doador nominal usam um doador-agregado padrão
  (ex.: "Coleta do culto"), e o recibo sai nesse nome. Simples, mas o "recibo" perde valor fiscal nominal.
- **(7b) Recibo condicional:** emite recibo **quando houver doador**; caixa anônimo agregado fica só na
  contabilidade/trilha (era a recomendação original). Contradiz a literalidade da Q3.
- **(7c) Recibo da sessão:** um **recibo consolidado por sessão de caixa** (não por doador), como
  comprovante interno de arrecadação, além dos recibos nominais das linhas com doador.

> **Preciso da sua decisão aqui** — ela muda o modelo (`donor_id` obrigatório × opcional) e o
> `ReceiptService`.

## 8. Impacto no código (dimensão do esforço)

| Área | Mudança |
| --- | --- |
| **Modelo/migração** | `Entry` (+ `entry_type`, `source`, `cash_session_id`); migração de `donations`. Snapshots EF. |
| `ReconciliationService` | Generalizar contrapartida por origem (§6); `PostPaidAsync` passa a operar sobre `Entry`. |
| `CashSessionService.CloseAsync` | Aceitar linhas discriminadas; gerar `Entry` `cash` por linha; validar soma. |
| **Novo** lançamento manual | Serviço + endpoint `POST /api/finance/entries` (RBAC lançador — coordenador/admin, D-02 Q3). |
| `ReceiptService` | Recibo por entrada de qualquer origem (conforme §7). |
| `WebhookProcessor`, `DonationCheckoutService`, `RecurringBillingService`, `DonationExpiryService` | Passam a criar/atualizar `Entry` (origem PSP). |
| Relatórios/Reporting, Transparência, CRM/doadores, Recebíveis, Tesouraria | Somar/ler `Entry` no lugar de `Donation`. |
| Web | Tela de **lançamento manual**; fechamento de caixa com **linhas discriminadas**; rótulos de tipo/origem. |
| Testes | ~15 arquivos de teste tocam `Donation` — atualizar; novos testes de caixa discriminado, manual e contabilidade por origem. |

## 9. Segurança & RBAC

- **Lançar entrada manual e fechar caixa discriminado** = operação de lançamento → **coordenador/admin**
  (`FinanceRoles.CanLaunch`, alinhado à D-02 Q3). Caixa mantém a **dupla conferência** (2º responsável).
- Toda entrada (criação/estorno) vai ao `audit_log`. Estorno reusa `ReconciliationService.ReverseAsync`.

## 10. Riscos

1. **Refactor amplo (~45 arquivos).** A entidade `Entry` genérica (Q1) toca o núcleo de contabilidade,
   recibos, relatórios, transparência, CRM, recorrência e conciliação. Alto risco de regressão →
   **sugiro fasear** (§11) e blindar com os testes existentes.
2. **Dupla contagem** entre a receita do caixa e o depósito caixa→banco — mitigado tratando o depósito
   como transferência (§5).
3. **Recibo × anonimato** (§7) — pendente de decisão.

## 11. Faseamento sugerido (para o review)

1. **Fase A — contabilizar o caixa e o manual reusando o motor atual** (sem renomear `Donation` ainda):
   `CloseAsync` discriminado gera entradas conciliadas; novo lançamento manual. Entrega o **valor** da
   D-05 (caixa/manual viram receita de 1ª classe) com risco baixo.
2. **Fase B — introduzir `entry_type`/`source` de 1ª classe** (Q4) e ajustar relatórios/CRM.
3. **Fase C — refactor para a entidade `Entry` canônica** (Q1), migrando `Donation`.

> Alternativa registrada: se o custo/risco da Fase C não se justificar agora, manter `Donation` +
> `source` entrega Q2/Q3/Q4 com uma fração do risco. **Decisão do PO no review.**

## 12. Fora de escopo desta decisão

Eixo ator × frequência e gating de portal, campanhas como earmark (**D-06/D-08**); portal do Formattio
(**D-10/D-11**); NF/faturamento (gestão avançada, **D-09**).

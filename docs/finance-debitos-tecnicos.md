# Débitos técnicos — módulo Finance

> Inventário dos débitos técnicos acumulados nas Ondas 1–4 do módulo Finance. Mantido atualizado à
> medida que forem resolvidos. Severidade: 🔴 alto (afeta correção/uso real) · 🟠 médio · 🟡 baixo.
> Última revisão: 2026-09-07 (DT-05).

## 🔴 Alto impacto

### DT-01 — Admin não conseguia aprovar Contas a Pagar · ✅ em correção
As faixas de alçada default exigem `treasurer`/`manager`/`fiscal_council`, mas as memberships só têm
`admin`/`member` — o admin do onboarding não casava com nenhuma faixa e não aprovava nada.
**Correção (este PR):** `admin` passa a ser **aprovador coringa** (satisfaz qualquer faixa), mantendo
a segregação de funções (não aprova o próprio lançamento). **Resta (ver DT-04):** fluxo de atribuição
dos papéis financeiros a outros usuários para exercer a segregação plena.

### DT-02 — Bookkeeping simplificado: o Balanço não fechava · ✅ resolvido
A conciliação debitava "Recebível" (não "Caixa") e tesouraria/razão eram paralelos. **Correção:** a
doação recebida agora **debita Banco** (ativo) / credita Receita — o **Balanço fecha** (Ativo Banco =
Receita − Despesa = superávit) — e registra uma **entrada de tesouraria** na conta bancária da unidade
(o estorno reverte ambos). Integra razão ↔ tesouraria para as doações. **Resta (parcial):** unificação
total razão↔tesouraria para todos os fluxos (AR/AP já criam movimento; caixa físico idem).

### DT-03 — `Payable` sem `organization_id` · ✅ resolvido
Títulos a pagar não tinham unidade. **Correção:** `Payable.OrganizationId` adicionado; o **fluxo de
caixa** passa a escopar AP pelas unidades visíveis; o **pagamento** lança a despesa na **conta contábil
da unidade** do título (não mais genérica); endpoint exige `organizationId`. **Resta:** demonstrações
por unidade seguem em DT-14.

## 🟠 Médio impacto

### DT-04 — RBAC financeiro raso (RF-FIN-171) · ✅ resolvido
Não havia **atribuição** dos papéis financeiros aos membros. **Correção:** `TeamService` + endpoints
`/api/finance/team` (listar membros, `GET /roles`, `PUT /{userId}/role` — **somente admin**, com
auditoria) que definem o papel na `catalog.memberships` (alimenta o claim `role` → RBAC/alçadas). No
front, painel **"Equipe e papéis"** em Configurações (admin). Agora o RBAC e as alçadas são exercidos
de verdade (junto com o DT-01).

### DT-05 — Migrações EF versionadas (ADR-0002) · ✅ resolvido
Antes: DDL idempotente no `SchemaProvisioner`, reaplicada a todos os tenants no startup, sem
histórico/rollback. **Correção:** modelo do `TenantDbContext` agora **schemaless** + um
`TenantSearchPathInterceptor` que fixa o `search_path` no `t_<slug>` do request; uma **única base de
migrações** serve a todos os tenants, com histórico (`__ef_migrations_history`) por schema. O
`EfSchemaProvisioner` migra o `catalog` e cada tenant e **adota** schemas legados (carimba a baseline
como aplicada antes de migrar o resto). Validado em Postgres (Docker): adoção dos tenants existentes,
criação de tenant novo do zero e rollback (Down). A DDL legada segue como fallback via
`SCHEMA_STRATEGY=ddl`. **Resta (evolução):** remover o fallback DDL após validação em produção.

### DT-06 — Régua de cobrança de AR (RF-FIN-102) · ✅ resolvido
Só o **aging** havia sido entregue. **Correção:** `ReceivablesReminderService` varre títulos em
aberto/parciais com doador identificado e enfileira lembretes na **outbox** — "a vencer" (janela de
3 dias, 1 único toque) e "vencido" (no máx. 1/mês, dedupe por título + competência). Respeita
`ContactOptOut`/e-mail. Plugado no `BillingWorker` (antes do dispatch). Templates
`receivable_due_soon`/`receivable_overdue`.

### DT-07 — Competência dedicada (data contábil) · ✅ resolvido
Antes o ano/trimestre saía de `CreatedAt` ("agora"), sem lançamento retroativo. **Correção:** campo
**`AccountingDate`** (competência) em `Transaction` e `AccountingEntry`, preenchido na origem —
doação=`PaidAt`, despesa=data do pagamento, voluntariado=`PerformedOn`, estorno=data do fato.
**Demonstrações** (balancete, DRP, Balanço, DMPL, transparência), **orçamento** (realizado) e
**export do contador** passam a agregar por `AccountingDate`. DDL retrocompatível (backfill de
`created_at`). Habilita competência histórica/retroativa.

### DT-08 — Conciliação: casamento 1:1 exato; CNAB baseline · ✅ matching resolvido (CNAB fica p/ evoluir)
**Correção (matching):** a linha do extrato passa a ter `MatchedAmount` — casamento com **baixa
parcial**, **N:1** (uma linha quita vários títulos, chamando `match` em sequência) e **1:N** (vários
lançamentos quitam um título, baixa acumulada). A linha fica `partial` enquanto sobra saldo e
`matched` ao consumir |valor|. O `SuggestAsync` sugere candidatos **exatos e parciais** (campo
`Kind`), ordenando exatos primeiro. Endpoint `match` aceita `amount` opcional. DDL retrocompatível
(`matched_amount`). **Resta (evolução consciente):** parser **CNAB configurável por banco** — exige
layouts/homologação reais de cada banco; o parser atual segue o baseline Febraban (posições fixas).

### DT-09 — Import de extrato via string JSON (não multipart)
Decisão consciente (D5); arquivos grandes trafegam como string no corpo JSON. Multipart fica p/ evoluir.

## 🟡 Baixo impacto / evoluções conhecidas

### DT-10 — Snapshot/assinatura de demonstrações (RF-FIN-173) · ✅ resolvido
Antes só havia o rascunho on-the-fly. **Correção:** `StatementSnapshotService` **congela** o período
(DRP, Balanço, DFC, segregação, DMPL) num payload JSON com **hash SHA-256** e fluxo **draft → approved**.
A aprovação exige **papel de governança** (admin/conselho fiscal) e **segregação** (quem gera não
aprova); uma vez aprovado é imutável e auditado. Entidade `statement_snapshots` + endpoints
`/api/finance/reports/snapshots` (listar, obter, gerar, aprovar).

### DT-11 — Rate limiting e idempotência sem teste E2E · ✅ resolvido
O rate limiter era middleware só verificável manualmente. **Correção:** `RateLimitEndpointTests` sobe a
API in-memory (`WebApplicationFactory`) e prova de ponta a ponta que a janela fixa por IP+tenant admite
exatamente `PUBLIC_RATE_LIMIT_PERMITS` requisições e barra as excedentes com **429** (com header
`Retry-After` via `OnRejected`), sem depender de Postgres — o limiter roda antes do endpoint. A
**idempotência** do checkout já tem cobertura de integração no nível de serviço em `HardeningTests`
(mesma chave reusa a doação e não recria pedido no PSP; chaves distintas criam doações distintas); um
E2E HTTP dela exigiria provisionar Postgres, que o harness de testes (in-memory) não possui.

### DT-12 — Assinatura HMAC do webhook com header presumido
Assumido `X-Hub-Signature-256`/`X-Hub-Signature`; o mecanismo real do Pagar.me precisa ser confirmado.

### DT-13 — Testes date-sensitive · ✅ resolvido
Resolvido junto com o DT-07: com `AccountingDate` (setter público), os testes de demonstrações,
transparência, orçamento e DMPL passam a fixar **períodos históricos determinísticos** (ex.: 2025),
sem depender de "ano/trimestre corrente".

### DT-14 — Demonstrações por unidade · ✅ resolvido
Antes agregavam o tenant inteiro. **Correção:** `StatementsService` aceita `organizationId` opcional
(ausente = consolidado da rede). Recorte via `Transaction → Account.OrganizationId` (balancete, DRP,
Balanço, DMPL, segregação) e via `TreasuryAccount.OrganizationId` (DFC). Endpoints
`/api/finance/reports/*` ganham o query param `organizationId`. **Resta (evolução):** seletor de
unidade no front das demonstrações.

---

## Pendências que dependem de terceiros (não são débito de código)

- **PSP-1** — card-on-file recorrente (dízimo no cartão): confirmar tokenização recorrente no plano Pagar.me.
- **PSP-2** — PIX Automático (mandato): disponibilidade no PSP + requisitos BACEN.
- **Cartão no front** — depende da tokenização Pagar.me.js (public key + script).

---

## Ordem de ataque sugerida

1. **DT-01** (admin aprovar AP) — rápido, destrava alçadas no demo. **← em andamento.**
2. **DT-03** (`Payable.organization_id`) — desbloqueia fluxo de caixa e despesa contábil corretos.
3. **DT-02** (bookkeeping consistente) — o mais estrutural; faz as demonstrações fecharem de verdade.

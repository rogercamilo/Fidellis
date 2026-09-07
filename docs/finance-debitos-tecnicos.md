# Débitos técnicos — módulo Finance

> Inventário dos débitos técnicos acumulados nas Ondas 1–4 do módulo Finance. Mantido atualizado à
> medida que forem resolvidos. Severidade: 🔴 alto (afeta correção/uso real) · 🟠 médio · 🟡 baixo.
> Última revisão: 2026-09-06.

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

### DT-05 — Migrações EF versionadas (ADR-0002)
Ainda usamos DDL idempotente no `SchemaProvisioner`, reaplicada a todos os tenants no startup. Frágil
conforme a base cresce (sem histórico/rollback de schema).

### DT-06 — Régua de cobrança de AR (RF-FIN-102)
Só o **aging** foi entregue; faltam **lembretes** (outbox/CRM) de recebíveis a vencer/vencidos.

### DT-07 — Competência = `transaction.CreatedAt`
Sem data contábil dedicada; lançamentos datados em "agora". Ano/trimestre (orçamento, demonstrações,
transparência) dependem disso — sem lançamento retroativo nem reclassificação de competência.

### DT-08 — Conciliação: casamento só 1:1 exato; CNAB baseline
Sugere apenas match exato de valor+data (±3 dias), 1 linha ↔ 1 título; sem baixa parcial, N:1 ou
heurística. O parser CNAB é layout-base Febraban (posições fixas), sem configuração por banco.

### DT-09 — Import de extrato via string JSON (não multipart)
Decisão consciente (D5); arquivos grandes trafegam como string no corpo JSON. Multipart fica p/ evoluir.

## 🟡 Baixo impacto / evoluções conhecidas

### DT-10 — Snapshot/assinatura de demonstrações (RF-FIN-173)
Demonstrações on-the-fly; sem versão congelada nem workflow de aprovação/assinatura da prestação de contas.

### DT-11 — Rate limiting e idempotência sem teste E2E
O rate limiter é middleware, só verificável manualmente; sem teste de integração.

### DT-12 — Assinatura HMAC do webhook com header presumido
Assumido `X-Hub-Signature-256`/`X-Hub-Signature`; o mecanismo real do Pagar.me precisa ser confirmado.

### DT-13 — Testes date-sensitive
Vários usam "ano/trimestre corrente" porque `Transaction.CreatedAt` tem setter protegido; não dá para
testar períodos históricos.

### DT-14 — Demonstrações não escopadas por unidade
Agregam o tenant inteiro; sem BP/DRP por unidade da rede.

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

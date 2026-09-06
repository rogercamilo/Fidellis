# Front-end das Finanças (Ondas 1–2) — Plano de Implementação

> **Status:** rascunho para revisão do Product Owner · **Versão:** v0.1 — 2026-09-06
> **Escopo:** levar ao `apps/web` (Next.js) as funcionalidades financeiras já entregues no core/BFF nas
> Ondas 1–2 — hoje existentes apenas na API. Segue o design system atual (console enterprise:
> AppShell + Panel). Não altera comportamento até ser aprovado e desenvolvido.

---

## 1. Contexto técnico (o que já existe)

- **`apps/web`** — Next.js (app router), componentes client, sessão em `sessionStorage`
  (`fidellis.session`), token Bearer. Navegação no `AppShell` (array `NAV`). Cards via `Panel`.
- **`apps/web/app/lib/api.ts`** — wrappers `fetch` finos para o **BFF** (`NEXT_PUBLIC_BFF_URL`).
- **BFF** — tem um **proxy genérico** (`ProxyController`: `/api/*` → core, repassando o
  `Authorization`). **Consequência:** todos os endpoints novos de finanças (`/api/finance/treasury`,
  `/receivables`, `/payables`, `/cash-sessions`, `/periods`, dimensões, config) **já estão acessíveis
  pelo front** sem mudança no BFF. Só falta o cliente (`lib/api.ts`) e as telas.

### 1.1 Dependência de RBAC (BFF)
O core aplica o RBAC financeiro (`FinanceWriteFilter`) lendo o claim **`role`** do JWT. O JWT emitido
pelo BFF (`token.service.ts`) **precisa incluir `role`** (papel do usuário no tenant) para que:
(a) o core bloqueie gravações de perfis somente-leitura; (b) o front esconda ações de escrita.
É um ajuste pequeno no BFF — ver §7 (decisões).

---

## 2. Mapa das telas (core → UI)

| Área | Rota nova | Cobre (core) |
| --- | --- | --- |
| **Configurações financeiras** | `/dashboard/configuracoes` | Nomenclatura (dízimo/oferta), tipos de doador, rubricas, **dimensões** (centros de custo/fundos/projetos) — Onda 1 F/J |
| **Cobrança** (evolui a existente) | `/dashboard/cobranca` | Método pix/**boleto**/**cartão** + nomenclatura aplicada — Onda 1 A |
| **Tesouraria** | `/dashboard/tesouraria` | Contas/caixas, saldo consolidado, transferências, **fluxo de caixa projetado** — Onda 2 D |
| **Contas a Receber** | `/dashboard/receber` | Promessas/recebíveis, aging, baixa — Onda 2 B |
| **Contas a Pagar** | `/dashboard/pagar` | Credores, títulos, **alçadas** (aprovar/rejeitar/pagar) — Onda 2 C |
| **Caixa físico** | `/dashboard/caixa` | Sessões de coleta (abrir/fechar dupla conferência/depositar) — Onda 2 E |
| **Fechamento** | dentro de `/dashboard/contabilidade` | Fechar/reabrir período — Onda 2 I |

### 2.1 Navegação
O topo (`NAV`) hoje é plano com 8 itens; somar 6 lotaria a barra. **Proposta:** agrupar as áreas
financeiras. Duas opções (ver §7 decisão 1):
- **A) Sub-nav "Financeiro":** um item "Financeiro" no topo abre uma sub-navegação lateral/segmentada
  (Cobrança, Recorrência, Receber, Pagar, Tesouraria, Caixa, Configurações).
- **B) Menu suspenso** "Financeiro" no topo com os itens.

---

## 3. Camada de cliente (`lib/api.ts`)

Adicionar wrappers (mesmo padrão `authGet`/`authPost` existente), agrupados por área:

- **Dimensões:** `listCostCenters/createCostCenter`, `listFunds/createFund`, `listProjects/createProject`.
- **Config:** `getFinanceSettings/updateFinanceSettings`, `listDonorTypes/createDonorType`,
  `listCategories/createCategory`.
- **Tesouraria:** `listTreasuryAccounts/createTreasuryAccount`, `treasuryBalance`, `transfer`, `cashflow`.
- **AR:** `listReceivables`, `createReceivable`, `settleReceivable`, `receivablesAging`.
- **AP:** `listPayees/createPayee`, `listPayables/createPayable`, `approvePayable/rejectPayable/payPayable`,
  `listApprovalTiers`.
- **Caixa:** `listCashSessions`, `openCashSession`, `closeCashSession`, `depositCashSession`.
- **Períodos:** `listPeriods`, `closePeriod`, `reopenPeriod`.
- **Cobrança:** estender `CreateDonationInput` com `method` e `cardToken`.

Todos com tipos TS espelhando os DTOs do core.

---

## 4. Telas (detalhe)

### 4.1 Configurações financeiras (`/dashboard/configuracoes`)
- **Nomenclatura:** form com `recurringLabel`/`onetimeLabel` (PUT settings).
- **Dimensões:** três listas + criar (centros de custo, fundos com/sem restrição — campo `purpose`
  quando restrito, projetos).
- **Tipos de doador** e **rubricas** (revenue/expense).
- Painéis via `Panel`; ações de escrita só para papéis de escrita.

### 4.2 Tesouraria (`/dashboard/tesouraria`)
- **Cards de saldo**: consolidado da rede + por conta (lista `treasury/accounts` com saldo).
- **Nova conta** (banco/caixa) e **transferência** (form origem→destino).
- **Fluxo de caixa projetado**: gráfico/tabela D+30/60/90 (`cashflow`) — reusar Recharts (já usado em
  Relatórios). É o painel de **previsibilidade**.

### 4.3 Contas a Receber (`/dashboard/receber`)
- **Aging** em cards (a vencer / 1–30 / 31–60 / 60+).
- **Lista** de recebíveis (status, vencimento, valor, recebido) + **criar promessa** + **baixar**.

### 4.4 Contas a Pagar (`/dashboard/pagar`)
- **Credores** (lista + criar).
- **Títulos**: lista por status; **criar** (com rateio opcional); ações **aprovar / rejeitar / pagar**
  (pagar escolhe a conta de tesouraria). Mostrar a **faixa de alçada** e assinaturas necessárias.
- Botões de aprovação respeitam o papel (o core já bloqueia; o front esconde para clareza).

### 4.5 Caixa físico (`/dashboard/caixa`)
- **Abrir** sessão (escolhe o caixa + rótulo do evento).
- **Fechar** (valor conferido; o 2º responsável é quem fecha — mensagem explicando a dupla conferência).
- **Depositar** (escolhe a conta bancária).

### 4.6 Fechamento (em `/dashboard/contabilidade`)
- Lista de períodos + **fechar mês** + **reabrir** (só admin; botão escondido para os demais).

### 4.7 Cobrança (evolui `/dashboard/cobranca`)
- Seletor de **método**: PIX (atual), **boleto** (linha digitável + link do PDF), **cartão**
  (tokenização — ver §7 decisão 2). Aplicar o rótulo configurável ("Dízimo/Oferta").

---

## 5. Sequência de build (incrementos ≈ 1 PR cada)

| # | Incremento | Entrega |
| --- | --- | --- |
| **FE-0** | **Fundação** | Wrappers em `lib/api.ts` + navegação "Financeiro" + (BFF) claim `role` no JWT |
| **FE-1** | **Configurações** | Dimensões + nomenclatura + tipos de doador + rubricas |
| **FE-2** | **Tesouraria** | Contas, saldo consolidado, transferências, fluxo de caixa (gráfico) |
| **FE-3** | **Contas a Receber** | Aging + lista + criar/baixar |
| **FE-4** | **Contas a Pagar** | Credores + títulos + alçadas (aprovar/rejeitar/pagar) |
| **FE-5** | **Caixa físico + Fechamento** | Sessões + fechar/reabrir período |
| **FE-6** | **Cobrança multi-método** | Boleto + cartão + nomenclatura aplicada |

> FE-0 primeiro: a navegação e o cliente sustentam todas as telas; o claim `role` habilita o RBAC no
> front. FE-6 por último (cartão depende de decisão do PSP.js).

---

## 6. Qualidade / convenções

- Reusar `Panel`, `AppShell`, estilos de `globals.css`; **Aptos** como fonte de texto (padrão do repo).
- Estados de carregamento/erro como nas páginas atuais (try/catch + mensagem).
- Sem novas libs além de **Recharts** (já presente) para os gráficos.
- Rótulos configuráveis (dízimo/oferta) lidos de `getFinanceSettings` e aplicados nas telas de doação.
- Ações de escrita ocultadas quando `session.role` for somente-leitura (conselho fiscal/contador).

---

## 7. Decisões pendentes (para a revisão do PO)

1. **Navegação:** sub-nav "Financeiro" (opção A) ou menu suspenso (opção B)? Proposta: **A** (sub-nav),
   mais escalável conforme as áreas crescem.
2. **Cartão no checkout:** incluir a **tokenização Pagar.me.js** já no FE-6 (precisa da *public key* e
   do script do PSP), ou entregar só **PIX + boleto** no front agora e cartão depois? Proposta:
   **PIX + boleto agora**; cartão quando a integração Pagar.me.js estiver definida.
3. **RBAC no BFF:** incluir o claim **`role`** no JWT do BFF (para o core enforçar e o front esconder
   ações)? Proposta: **sim** — pequeno ajuste no `token.service.ts`, feito no FE-0.
4. **Escopo por unidade:** as telas usam a unidade ativa/visível (Rede→Unidade) como no restante do
   app? Proposta: **sim**, reaproveitando o `OrganizationPicker` existente onde fizer sentido.

---

## 8. Fora de escopo (deste plano)

- Recursos ainda não entregues no core: card-on-file recorrente (PSP-1), PIX Automático (PSP-2),
  conciliação OFX/CNAB (Onda 3), demonstrações ITG 2002 (Onda 4).
- App mobile / PWA offline para o caixa físico (poderia ser evolução útil no altar/sacristia).

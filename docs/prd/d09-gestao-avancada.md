# D-09 — "Gestão avançada" (convênios/MROSC e NF/faturamento fora do núcleo)

> **Status:** ✅ **implementada** · 2026-09-12 — decisões §4 confirmadas pelo PO (todas as recomendações).
> **Entregue:** `FinanceSettings.AdvancedManagement` (bool, default false) + migração `AdvancedManagement`;
> DTO/endpoint `/settings` estendido; toggle em `configuracoes`; `AppShell` esconde o item **Projetos &
> voluntariado** (marcado `advanced`) quando o modo está desligado (combina com o filtro por papel do
> D-03; grupos vazios somem). Contas a receber **mantida no núcleo**; NF/faturamento segue futuro (sem
> código). Core 180 testes verdes; web build/lint limpos.
> **Origem:** decisão **D-09** do [parecer](parecer-finance-terceiro-setor.md): convênios/acordos (MROSC) e
> NF/faturamento → "gestão avançada" (ativável); *provável* aposentadoria de "Contas a receber" do núcleo.
> **Fluxo:** detalhar → **PO revisa/ajusta** → só então codar.
> **Referências de código:** `FinanceSettings`, `ConfigEndpoints`, `AppShell` (NAV + D-03), `MroscReportService`,
> `VolunteerWorkService`, tela `dashboard/projetos`.

---

## 1. Objetivo

Tirar do caminho do público-base (associações/comunidades pequenas) os **pontos fora da curva** que só uma
minoria opera — **convênios/acordos (MROSC)** e **NF/faturamento** —, colocando-os atrás de um
**"Gestão avançada" ativável por tenant**. Complementa a disclosure por papel (D-03) com uma disclosure
por **funcionalidade**.

## 2. Estado atual

| Peça | Situação |
| --- | --- |
| **MROSC / convênios** | ✔ Existe: `MroscReportService` (recebido × gasto por projeto), recebíveis `source=grant/agreement`, tela `dashboard/projetos` (projetos + voluntariado). Sempre visível. |
| **NF / faturamento** | 🔴 Nunca construído — fora de escopo desde sempre. Nada a esconder; permanece futuro/avançado. |
| **Contas a receber** | ✔ Núcleo (promessas/pledges, aging, baixa). Parecer sugere *provável* colapso em "entradas previstas" — **não decidido**. |
| **Flag de modo avançado** | 🔴 Inexistente. Todos os tenants veem tudo. |

## 3. Decisões propostas (recomendação para o review)

- **Q1 — Flag por tenant + toggle em Configurações.** `FinanceSettings.AdvancedManagement` (bool, default
  **false**). Ligado/desligado na tela de Configurações (papel lançador). Simples e reversível.
- **Q2 — O que fica atrás de "Gestão avançada".** A tela **Projetos & voluntariado** (projetos, MROSC/
  convênios e trabalho voluntário a valor justo) só aparece com o modo ligado. **NF/faturamento** já é
  futuro/avançado (sem código). *(Trabalho voluntário vai junto por ser "contabilidade de projeto",
  mais avançada que o fluxo base de dízimo/oferta — ajustável.)*
- **Q3 — Contas a receber: manter no núcleo.** O parecer diz "*provável* aposentadoria" — proponho **não**
  aposentar agora (pledges/aging são úteis à base); o colapso em "entradas previstas" é um reforço maior,
  fica para depois. *(Alternativa: mover Contas a receber para o modo avançado também.)*

## 4. Impacto no código

| Área | Mudança |
| --- | --- |
| `FinanceSettings` (+migração) | `AdvancedManagement` (bool, default false). |
| `ConfigEndpoints /settings` | DTO estende com `advancedManagement`; GET/PUT. |
| `AppShell` | Busca as settings; esconde os itens avançados quando o modo está desligado (combina com o filtro por papel do D-03). |
| Web `configuracoes` | Toggle "Gestão avançada (convênios/MROSC, NF)". |
| Testes | GET/PUT do flag; (front sem teste — build/lint). |

## 5. Fora de escopo

Construir NF/faturamento (só reservado como avançado); aposentar/colapsar "Contas a receber"; mudar o
back do MROSC (só a visibilidade muda).

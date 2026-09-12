# D-08 — Campanhas (earmark de finalidade + meta × arrecadado + página pública)

> **Status:** ✅ **implementada** · 2026-09-12 — decisões §4 confirmadas pelo PO (todas as recomendações).
> **Entregue:** `Campaign` ampliada (`Description`, `StartsAt`, `EndsAt`, `FundId`, `ProjectId`) + migração
> `CampaignFields`; `CampaignService` (CRUD + progresso arrecadado×meta + janela ativa + report
> arrecadado×meta×aplicado); `CampaignEndpoints` (`/api/finance/campaigns` list/create/patch-status +
> `/{id}/report`, escrita = lançador); rotas públicas `GET /api/public/{tenant}/campaigns[/{slug}]`;
> **earmark** no `DonationCheckoutService` (doação da campanha herda o fundo/projeto vinculado, ITG 2002);
> web: tela de gestão `dashboard/campanhas` (grupo Entradas) + página pública `campanha/[tenant]/[slug]`
> com barra de progresso e doar. Testes `CampaignTests` (5) → **180 verdes**.
> **Origem:** decisão **D-08** do [parecer](parecer-finance-terceiro-setor.md). Campanha = **finalidade/
> earmark** sobre a entrada (não um 4º tipo).
> **Fluxo:** detalhar → **PO revisa/ajusta** → só então codar.
> **Referências de código:** `Campaign`, `Donation.CampaignId`, `DonationCheckoutService`, `Fund`
> (restrito/ITG 2002), `Project`, `MroscReportService` (padrão de recebido × gasto).

---

## 1. Objetivo

Transformar a campanha (hoje mínima) numa **funcionalidade dedicada**: janela finita **ou** aberta,
**meta × arrecadado**, **página pública** e vínculo opcional a **fundo restrito/projeto**. Campanha de fim
específico é recurso **com restrição** (ITG 2002) → habilita **prestação de contas por campanha**. A
campanha é um **earmark** sobre a entrada, ortogonal ao `entry_type` (D-06) — não um novo tipo.

## 2. Estado atual

| Peça | Situação |
| --- | --- |
| `Campaign` | 🟠 Mínima: `OrganizationId, Title, Slug, GoalAmount?, Status`. Sem janela, sem descrição, sem vínculo a fundo/projeto. |
| `Donation.CampaignId` | ✔ Existe e passa pelo checkout (`CheckoutCommand.CampaignId`). |
| CRUD / progresso / página | 🔴 Inexistentes. Sem endpoints de campanha, sem cálculo arrecadado × meta, sem página pública nem tela de gestão. |
| `Fund` restrito, `Project`, `MroscReportService` | ✔ Prontos — base para earmark + "aplicado". |

## 3. Modelo (proposto)

```
Campaign (ampliada)
  OrganizationId, Title, Slug, Status           (já existe)
  GoalAmount?                                    (meta — já existe)
  Description?                                   NOVO (texto público)
  StartsAt? / EndsAt?                            NOVO (janela; ambos nulos = aberta)
  FundId?  (restrito) / ProjectId?               NOVO (earmark/restrição — ITG 2002)

Progresso (derivado): arrecadado = Σ doações pagas com CampaignId; % da meta; janela ativa?
```

## 4. Decisões propostas (recomendação para o review)

- **Q1 — Earmark com restrição.** A campanha vincula-se opcionalmente a um **fundo restrito** (e/ou
  projeto). As doações da campanha **herdam esse fundo** no checkout (sobrepõe o default), segregando o
  recurso como restrito (ITG 2002). *(Alternativa: campanha só como rótulo/agrupamento, sem restrição —
  perde o vínculo contábil.)*
- **Q2 — Página pública dedicada.** `/(*)campanha/[tenant]/[slug]` com título, descrição, **barra de
  progresso (arrecadado × meta)**, janela, e botão de doar (reusa o checkout público com `campaignId`).
  *(Alternativa: sem página pública, só progresso interno.)*
- **Q3 — Prestação de contas por campanha.** Relatório **arrecadado × meta**; e, quando vinculada a
  fundo/projeto, **aplicado** (despesas na dimensão) reusando o padrão do `MroscReportService`. *(Alt.:
  só arrecadado × meta agora; aplicado depois.)*
- **Janela:** finita (`StartsAt`/`EndsAt`) **ou** aberta (nulos). Campanha fora da janela/`Status`
  encerrado não aparece como ativa no público (mas o histórico/relatório permanece).

## 5. Impacto no código

| Área | Mudança |
| --- | --- |
| `Campaign` (+migração) | `Description`, `StartsAt`, `EndsAt`, `FundId`, `ProjectId`. |
| **Novo** `CampaignService` | CRUD + progresso (arrecadado/meta/%) + report (arrecadado × meta × aplicado). |
| **Novo** `CampaignEndpoints` | `/api/finance/campaigns` (list/create/patch), `/{id}/report`. Escrita = lançador (D-02). |
| `DonationCheckoutService` | Se `CampaignId` e a campanha tem `FundId` → doação herda o fundo (earmark). |
| Público (`FinanceModule` pub) | `GET /api/public/{tenant}/campaigns` (ativas) + `/campaigns/{slug}` (detalhe+progresso); doação pública aceita `campaignId`. |
| Web | Tela de gestão de campanhas (dashboard, grupo **Entradas**); página pública `campanha/[tenant]/[slug]`. |
| Testes | Progresso (arrecadado × meta), earmark (doação herda fundo), janela ativa, report. |

## 6. Fora de escopo

Novo tipo de entrada (campanha é earmark, não `entry_type`); metas por período; imagens/upload de capa
(texto agora); doação recorrente vinculada a campanha (pode vir depois).

# D-06/D-07 — Modelo de entrada por atributos ortogonais + doação recorrente do apoiador

> **Status:** ✅ **implementada** · 2026-09-12 — decisões §3 confirmadas pelo PO (todas as recomendações).
> **Entregue:** `Donation.EntryType` + `RecurringDonation.EntryType` + rótulos por tipo em `FinanceSettings`
> (`EntryTypes` = chaves estáveis `tithe|offering|donation`); migração `EntryTypeFirstClass` com backfill
> (recorrente→tithe, caixa→offering); gating público (checkout anônimo força `donation`); dashboard/manual
> escolhem o tipo; recorrência dízimo × doação (D-07, ciclo herda o tipo); caixa **discriminado** por tipo
> no fechamento (Σ == conferido) com UI própria; rótulos editáveis em Configurações. Testes: caixa
> discriminado (×2), manual com tipo, recorrência-doação (D-07) → **175 verdes**. **Diferido:** entidade
> `Entry` canônica; autenticação de membro no portal (dízimo/oferta self-service, liga-se a D-10).
> **Origem:** decisões **D-06** e **D-07** do [parecer](parecer-finance-terceiro-setor.md) (§4.1 — taxonomia)
> e recomendação #2 da §7. Herda itens diferidos da **D-05** (caixa discriminado, entidade `Entry`).
> **Fluxo:** detalhar → **PO revisa/ajusta** → só então codar.
> **Referências de código:** `Donation`, `FinanceSettings` (`RecurringLabel`/`OnetimeLabel`), `Donor`
> (`DonorTypeId`/`ConvertedAt`), `RecurringDonation`, `RecurringBillingService`, público em `FinanceModule`.

---

## 1. Objetivo

Substituir a **inferência frágil** "recorrente ⇒ dízimo" por um **modelo explícito**: cada entrada tem um
`entry_type` de 1ª classe (`dízimo` | `oferta` | `doação`), com o eixo de negócio do §4.1 do parecer —
**tipo × ator × frequência × campanha × dimensões**. E desacoplar a recorrência do rótulo "Dízimo" para
habilitar a **doação recorrente do apoiador não-membro** (D-07).

**Taxonomia de negócio (parecer §4.1):**
- **Doação** — quem **não** é da comunidade; pública/anônima; pontual **ou** recorrente.
- **Dízimo** — **só membro**; recorrente (mensal); nome customizável.
- **Oferta** — **só membro**; pontual; valor a mais sobre o dízimo.

## 2. Estado atual (o que existe no código)

| Peça | Situação |
| --- | --- |
| **`entry_type`** | 🔴 Não existe. O tipo é implícito: `RecurringDonation` ⇒ "dízimo/oferta recorrente" (`RecurringBillingService`), rótulos vêm de `FinanceSettings.RecurringLabel="Dízimo"`/`OnetimeLabel="Oferta"`. |
| **Ator (membro × não-membro)** | 🟠 Não modelado como fato da entrada. Há `Donor.DonorTypeId` (tipo configurável "Membro/Apoiador") e `ConvertedAt`, mas nada gateia o portal. |
| **Frequência** | ✔ Derivável: `Donation.RecurringDonationId != null` ⇒ recorrente. |
| **Campanha / dimensões** | ✔ Já em `Donation` (`CampaignId`, `CostCenterId/ProjectId/FundId`). |
| **Recorrência amarrada a "Dízimo"** | 🟠 `RecurringBillingService` rotula ciclos como "Dízimo/oferta recorrente"; bloqueia rótulo próprio da doação recorrente (D-07). |
| **Caixa/manual (D-05)** | ✔ Já geram entrada de 1ª classe (`source=cash|manual`), mas **sem `entry_type`** — entram como default. |

## 3. Decisões propostas (recomendação para o review)

- **Q1 — `entry_type` de 1ª classe; frequência derivada; ator implícito pelo tipo.** Adicionar **uma**
  coluna `entry_type` (`dízimo|oferta|doação`) em `Donation`. **Frequência** segue derivada de
  `RecurringDonationId`. **Ator** é implicado pelo tipo (dízimo/oferta ⇒ membro; doação ⇒ não-membro) —
  sem coluna nova de ator. *(Alternativa: coluna `actor` explícita — mais flexível, mais peso.)*
- **Q2 — Gating conservador no portal.** O **portal/checkout público** oferece **só `doação`** (não-membro,
  pública/anônima) — sem construir autenticação de membro agora. **Dízimo/oferta** (só membro) são
  lançados pelo **dashboard** (coordenador) ou pelo caixa/manual. O portal autenticado de membro para
  dízimo/oferta fica para depois (liga-se à identidade de membro / Formattio D-10).
- **Q3 — Rótulos por tipo, customizáveis (resolve D-07 + D-02 Q6).** `FinanceSettings` passa a ter rótulo
  por tipo (`TitheLabel="Dízimo"`, `OfferingLabel="Oferta"`, `DonationLabel="Doação"`). A recorrência
  **não** força "Dízimo": o rótulo vem do `entry_type` do compromisso. Mantém-se compat lendo os antigos.
- **Q4 — Trazer o caixa discriminado (diferido da D-05); manter a entidade `Entry` diferida.** Agora que
  `entry_type` existe, o **fechamento de caixa** pode quebrar o total em linhas por `entry_type`/dimensão
  (Σ linhas == conferido). A **entidade `Entry` canônica** continua diferida (reusar `Donation` segue
  entregando o valor com menos risco).
- **D-07 — Doação recorrente do apoiador.** `RecurringDonation` ganha `entry_type` (default `dízimo`);
  ciclos herdam o tipo; o apoiador não-membro assina uma **doação recorrente** (`entry_type=doação`), com
  rótulo próprio. Reusa o mesmo motor de recorrência/dunning.
- **Migração/backfill.** `entry_type` em `donations` existentes: `RecurringDonationId != null ⇒ dízimo`;
  senão `⇒ doação` (espelha a inferência antiga sem superestimar membros). Caixa (D-05) default `oferta`
  *(confirmar)*.

## 4. Modelo (proposto)

```
Donation (entrada)
  entry_type: dízimo | oferta | doação      ← NOVO (1ª classe)
  source:     checkout|portal|cash|manual   (D-05)
  frequência: derivada de RecurringDonationId
  ator:       implícito (dízimo/oferta=membro; doação=não-membro)
  campaign_id, cost_center/project/fund      (já existem)

RecurringDonation
  entry_type: dízimo (default) | doação      ← NOVO (D-07)

FinanceSettings
  TitheLabel / OfferingLabel / DonationLabel ← NOVO (rótulos por tipo, customizáveis)
```

## 5. Impacto no código

| Área | Mudança |
| --- | --- |
| `Donation` (+migração) | Coluna `entry_type` + backfill. |
| `RecurringDonation` (+migração) | Coluna `entry_type` (default dízimo). |
| `FinanceSettings` (+migração) | Rótulos por tipo; endpoint `GET/PUT /settings` estende o DTO. |
| `RecurringBillingService` | Rótulo do ciclo vem do `entry_type` (não fixo "Dízimo"). |
| Público (`FinanceModule` pub) | Força `entry_type=doação`; dashboard/caixa/manual escolhem o tipo. |
| `CashSessionService.CloseAsync` | (Q4) linhas discriminadas por `entry_type`; Σ == conferido. |
| Web | Seleção de tipo no lançamento (dashboard, manual, caixa); rótulos por tipo em relatórios/telas. |
| Relatórios/CRM | Quebra por `entry_type` (dízimo × oferta × doação) — hoje só há método. |
| Testes | entry_type na criação/checkout/recorrência/caixa; gating do público; backfill. |

## 6. Fora de escopo

Autenticação de membro no portal (dízimo/oferta self-service) e identidade federada (**D-10/D-11**);
entidade `Entry` canônica (diferida); campanhas como página/meta dedicada (**D-08**).

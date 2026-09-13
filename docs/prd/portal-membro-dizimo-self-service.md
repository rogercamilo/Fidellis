# #75 — Portal autenticado de membro (dízimo/oferta self-service)

> **Status:** ✅ **implementada** · 2026-09-12 — decisões §4 confirmadas pelo PO (membro nativo + link mágico + dízimo/oferta pontual & recorrente).
> **Entregue:** `Donor.IsMember` (+ migração `DonorIsMember`); CRM marca/desmarca membro
> (`POST /api/crm/donors/{id}/member`) + `isMember` na lista/detalhe/`/me`; endpoints de autoatendimento
> autenticados por link mágico (`POST /api/public/{tenant}/member/{give,pledge}` — só `IsMember` libera
> dízimo/oferta; anônimo segue só doação); web: badge/toggle "membro" em Doadores + seção "Contribuir"
> no portal (`/portal/{tenant}`) com dízimo/oferta pontual (PIX) e dízimo recorrente. Reusa
> `DonationCheckoutService`/`RecurringBillingService`/`DonorMagicToken`. Testes `MemberPortalTests` (3) → 183 verdes.
> Quando o Formattio entrar (#80), a origem federada marcará o mesmo `IsMember` — sem duplicar modelo.
> **Atualização (2026-09-13) — recorrência indicada pelo membro + forma de pagamento:** o portal unificou
> os fluxos numa só tela onde o **membro escolhe**: tipo (dízimo/oferta), valor, **recorrência** (seletor
> Pontual × Mensal — só para dízimo; oferta é sempre pontual; default Mensal, pois espera-se dízimo
> recorrente, mas é **opcional**) e **forma de pagamento** (PIX/boleto). Backend: `RecurringDonation.Method`
> (+migração `RecurringDonationMethod` + DDL fallback); `CreatePledgeAsync`/`member/pledge` recebem o método;
> o ciclo (`RunBillingCycleAsync`) gera PIX **ou** boleto conforme escolhido. **Regra reforçada
> ([[fidellis-no-cobranca-dizimo-oferta]] / memória):** a instituição **não gera cobrança de dízimo/oferta** —
> a tela do operador "Cobrança" foi convertida em **"Doação avulsa"** (só `doação`; trava no
> `POST /api/finance/donations`). `RecurringBillingTests` +1 → **189 verdes**.
> **Origem:** issue **#75**, diferida da **D-06 (Q2)**. Conecta-se ao epic **#80** (Formattio) e ao **ADR-0013**.
> **Fluxo:** detalhar → **PO revisa/ajusta** → só então codar.
> **Referências de código:** `DonorMagicToken`, portal do doador (`/api/public/{tenant}/magic-link` + `/me`
> em `DonationsModule`), `/doar/{tenant}`, `Donor`, gating de `entry_type` (D-06), `RecurringBillingService`.

---

## 1. Objetivo

Hoje o **portal público** só permite **doação** (não-membro): o gating da D-06 força `entry_type=donation`
no checkout anônimo; **dízimo/oferta** (só membro) só são lançados pelo dashboard (coordenador) ou pelo
canal da integração Formattio — **bloqueado** por compliance (#80/ADR-0013). Falta um caminho de
**autoatendimento do membro**: o membro entra, é reconhecido como membro, e faz **dízimo/oferta**
(pontual e recorrente) por conta própria.

## 2. O fork central (precisa de decisão)

Para o membro dar **dízimo/oferta** self-service, o Fidellis precisa **reconhecê-lo como membro**. Como?

- **(A) Membro nativo do Fidellis** — o tenant **marca** um doador como membro (independe do Formattio).
  Não é bloqueado por compliance (não envolve dados de formação). Formattio (#80), quando vier, apenas
  **também** marca o membro (`Source=formattio`) pelo mesmo mecanismo. **Desbloqueia o #75 agora.**
- **(B) Só via Formattio** — a condição de membro vem exclusivamente da integração federada. Então o #75
  fica **bloqueado** junto com o #80 (auditoria → RIPD → ADR-0013 Aceito).

## 3. Estado atual

| Peça | Situação |
| --- | --- |
| Portal do doador | ✔ Link mágico (`DonorMagicToken`, sem senha): `magic-link` (envia link por e-mail) + `/me` (recibos/histórico). |
| Doar público | ✔ `/doar/{tenant}` → `entry_type=donation` (gating D-06). |
| Conceito de "membro" p/ doador | 🔴 Inexistente. `Donor` não tem flag de membro; `OrgMember` é para usuários de equipe, não doadores. |
| Dízimo/oferta self-service | 🔴 Não há caminho autenticado de membro. |
| Recorrência | ✔ Motor pronto (`RecurringBillingService`), hoje disparado pelo dashboard. |

## 4. Decisões propostas (recomendação)

- **Q1 — Membro nativo (A).** `Donor.IsMember` (bool) marcado pelo **coordenador/admin** (na tela de
  Doadores/CRM). Desbloqueia já; alinhado ao ADR-0013 (a origem federada, no futuro, também marca membro).
- **Q2 — Auth por link mágico (reusa `DonorMagicToken`).** Sem senha, baixo atrito, já existe. O membro
  recebe/usa o mesmo link do portal; sendo `IsMember`, o portal libera dízimo/oferta.
- **Q3 — Canal autenticado de membro libera dízimo/oferta.** Um endpoint público **com token de membro
  válido** (não anônimo) aceita `entry_type` ∈ {dízimo, oferta, doação} + recorrência; o anônimo segue só
  `doação`. É o mesmo princípio do "canal da integração" do ADR-0013, mas com token de membro nativo.
- **Q4 — Portal estende o do doador.** `/portal/{tenant}` ganha, para membro, botões de **dízimo/oferta**
  (pontual) e **dízimo recorrente** (mensal), além do histórico/recibos que já existe.

## 5. Impacto no código (se A)

| Área | Mudança |
| --- | --- |
| `Donor` (+migração) | `IsMember` (bool, default false). |
| CRM/Doadores (dashboard) | Ação "marcar como membro" + reenviar link do portal. |
| Portal público (`DonationsModule`) | Endpoint de giving **autenticado por token de membro** que permite dízimo/oferta + recorrência (valida `IsMember`); o `/me` passa a indicar `isMember`. |
| `RecurringBillingService` | Reuso p/ dízimo recorrente iniciado pelo membro. |
| BFF | Repasse das rotas públicas de membro (já há proxy `/api/public/*`). |
| Web | `/portal/{tenant}`: seção de contribuição do membro (tipo + recorrência); Doadores: marcar membro. |
| Testes | `IsMember` gating (membro dá dízimo/oferta; anônimo não), recorrência self-service. |

## 6. Segurança / LGPD

- Sem dados de formação; membro nativo é fato do tenant (não sensível por si). Auditar marcação de membro
  e contribuições. Link mágico expira (já implementado). Enumeração de e-mail: respostas neutras (já é assim).
- Quando o Formattio entrar (#80), a origem federada marca `IsMember` pelo mesmo campo — sem duplicar modelo.

## 7. Fora de escopo

Integração Formattio em si (#80, bloqueada); login com senha para membro; autocadastro de membro pelo
público (a marcação é do tenant). NF/faturamento (#79).

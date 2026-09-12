# D-03/D-04 — Navegação por papel + IA em Entradas/Saídas

> **Status:** ✅ **implementada** · 2026-09-12 — decisões confirmadas pelo PO (Q1 IA Entradas/Saídas; Q2
> filtrar menu por papel mantendo governança visível; **Q3 landing por papel — SIM**, divergindo da
> recomendação inicial de "não agora").
> **Entregue (front-only):** `AppShell` NAV reorganizada em Início/Entradas/Saídas/Contabilidade/Prestação
> de contas/Organização, com `roles` por grupo e filtro pelo papel da sessão (papel nulo/dev vê tudo);
> `landingFor(role)` + redirect no login (coordenador→Cobrança, conselheiro/moderador→A pagar,
> fiscal→Relatórios, contador→Contabilidade, admin→Início). Sem mudança de back; RBAC de escrita intacto.
> **Origem:** decisões **D-03** (progressive disclosure por papel) e **D-04** (IA em Entradas/Saídas) do
> [parecer](parecer-finance-terceiro-setor.md).
> **Fluxo:** detalhar → **PO revisa/ajusta** → só então codar. Predominantemente **front** (`AppShell`).
> **Referências de código:** `apps/web/app/components/AppShell.tsx` (NAV), sessão (`tenants[].role`),
> `FinanceRoles` (vocabulário de papéis — D-02).

---

## 1. Objetivo

- **D-04:** reorganizar a arquitetura de informação em **duas espinhas — Entradas e Saídas** — espelhando
  o modelo mental do conselho ("o que entrou, o que saiu, com que finalidade e aprovação"). Checkout e
  portais são **ferramentas/portas**, não itens de menu.
- **D-03:** cada membro vê **só o seu recorte** (progressive disclosure), reusando o RBAC pronto — corta a
  sobrecarga de ~17 itens sem esconder a governança de quem fiscaliza/aprova.

## 2. Estado atual

`AppShell.NAV` agrupa ~17 itens por **função técnica**, iguais para todos os papéis:

- **Financeiro:** Cobrança, Recorrência, Contas a receber, Contas a pagar, Tesouraria, Caixa,
  Conciliação, Orçamento, Projetos & voluntariado, Fechamento
- **Contabilidade:** Balancete & razão, Demonstrações
- **Gestão:** Doadores, Relatórios, Auditoria
- **Organização:** Equipe & conselho, Configurações

Sem filtro por papel; o RBAC só bloqueia escrita no back (`FinanceWriteFilter`) — a navegação não reflete
o papel.

## 3. IA proposta (D-04)

| Grupo | Itens | Racional |
| --- | --- | --- |
| **Início** | Início | Visão geral. |
| **Entradas** | Cobrança, Recorrência, Caixa, Contas a receber, Doadores | "O que entrou" — todas as portas de entrada + o CRM de quem doa. |
| **Saídas** | Contas a pagar, Tesouraria | "O que saiu" + a posição de caixa/bancos de onde se paga. |
| **Contabilidade** | Balancete & razão, Demonstrações, Conciliação, Fechamento | O núcleo contábil (rascunho→contador). |
| **Prestação de contas** | Orçamento, Projetos & voluntariado, Relatórios, Auditoria | Transparência e acompanhamento. |
| **Organização** | Equipe & conselho, Configurações | Governança do tenant. |

Checkout público, portal do doador e transparência permanecem **portas** (URLs públicas), fora do menu.

## 4. Decisões propostas (recomendação para o review)

- **Q1 — Adotar a IA de Entradas/Saídas acima.** Reagrupa os itens existentes pelo modelo mental do
  conselho, sem remover funcionalidade.
- **Q2 — Progressive disclosure = filtrar o menu por papel** (esconder o que o papel não usa), **mantendo
  visível a governança** para quem fiscaliza/aprova. Não é "modo simples" — é o recorte do papel. Papel
  nulo/dev vê tudo.
- **Q3 — Matriz papel → visibilidade (proposta):**

| Grupo/Item | admin | coordinator | council_officer / council_chair | fiscal_council | accountant | member |
| --- | :-: | :-: | :-: | :-: | :-: | :-: |
| Início | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| **Entradas** (Cobrança, Recorrência, Caixa, A receber, Doadores) | ✔ | ✔ | 👁️ leitura | 👁️ | – | – |
| **Saídas** (A pagar, Tesouraria) | ✔ | ✔ | ✔ (aprova) | 👁️ | – | – |
| **Contabilidade** | ✔ | ✔ | ✔ | ✔ | ✔ | – |
| **Prestação de contas** | ✔ | ✔ | ✔ | ✔ | 👁️ | – |
| **Organização** | ✔ | ✔ (Equipe) | – | – | – | – |

> ✔ = vê e opera (RBAC do back decide a escrita); 👁️ = vê (leitura); – = fora do menu. A trava real de
> escrita continua no back — o menu só **encurta a jornada**. *(Ajustável no review.)*

## 5. Impacto no código

| Área | Mudança |
| --- | --- |
| `AppShell.tsx` | NAV reestruturada (grupos Entradas/Saídas/…); cada item ganha `roles?: string[]`; filtro pelo papel da sessão. Item "Caixa" já hospeda o lançamento manual (D-05) e o fechamento discriminado (D-06). |
| (opcional) landing por papel | Redirecionar o 1º acesso do contador → Contabilidade, do fiscal → Prestação de contas. *(ver Q4 — proponho **não** agora.)* |
| Testes | Sem testes de UI hoje; validar via build/lint. |

## 6. Fora de escopo

Mudar o RBAC do back (já entregue em D-02/D-03-back); dashboards novos; landing por papel (adiado);
esconder governança de quem fiscaliza (o parecer é explícito: **não** esconder governança).

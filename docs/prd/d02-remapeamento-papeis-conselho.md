# D-02 — Remapeamento de papéis e alçadas para a governança de conselho

> **Status:** decisões do PO registradas (Q1–Q6, §9) — pronta para implementação · 2026-09-11
> **Origem:** decisão **D-02** do [parecer do terceiro setor](parecer-finance-terceiro-setor.md).
> **Fluxo:** detalhar → **PO revisa/ajusta** → só então codar. Este documento é a etapa "detalhar".
> **Referências de código:** `FinanceRoles`, `FinanceConfigSeeder`, `ApprovalService`, `FinanceWriteFilter`,
> `TeamService`/`TeamEndpoints`; requisito `RF-FIN-171`; tabela de alçadas `RF-FIN-112`.

---

## 1. Objetivo

O vocabulário de papéis atual (`treasurer/manager/fiscal_council`) é **corporativo** e não casa com a
estrutura de **governança por conselho** do público-alvo (parecer §2). Pior: o default de alçada coloca o
**conselho fiscal como aprovador** da faixa alta — quando na governança real ele **fiscaliza** (somente
leitura) e **não autoriza** pagamentos. O objetivo da D-02 é **realinhar o vocabulário e as alçadas** à
governança de conselho, **sem afrouxar** os guarda-corpos de compliance já entregues.

## 2. Estado atual (o que existe no código)

**Papéis** — `FinanceRoles.cs`:

| Chave | Escrita? | Observação |
| --- | --- | --- |
| `admin` | ✔ | Coringa: satisfaz qualquer faixa de alçada. |
| `treasurer` | ✔ | Escrita financeira. |
| `manager` | ✔ | Escrita financeira. |
| `fiscal_council` | ✖ (somente-leitura) | Mas **está listado como aprovador** na faixa >R$5.000 (conflito). |
| `accountant` | ✖ (somente-leitura) | Contador externo. |
| `member` | ✔ | Papel-base do membro. |

**Alçadas default** — `FinanceConfigSeeder.EnsureDefaultsAsync` (semeadas 1×, add-only):

| Faixa | `RolesCsv` atual | Assinaturas |
| --- | --- | --- |
| R$ 0 – R$ 500 | `treasurer` | 1 |
| R$ 500 – R$ 5.000 | `treasurer,manager` | 2 |
| ≥ R$ 5.000 | `manager,fiscal_council` | 2 |

**Guarda-corpos** — `ApprovalService`: mín. 1 assinatura; **autoaprovação bloqueada** (quem criou ≠
aprovador); teto de compliance R$ 5.000 → **2 assinaturas sempre**; o papel **deve pertencer à faixa**;
`admin` é coringa; um aprovador não assina duas vezes. `FinanceWriteFilter` barra (403) escrita de papel
somente-leitura. `TeamEndpoints`: **só `admin` atribui papéis**.

## 3. Vocabulário-alvo (mapeamento governança → chave técnica)

| Governança (parecer §2) | Chave técnica proposta | Mapeia de | Escrita | Papel na alçada |
| --- | --- | --- | --- | --- |
| **Coordenador** (equipe financeira, dia a dia) | `coordinator` | `treasurer` | ✔ | Lança; aprova faixa baixa. |
| **Conselheiro responsável** (responde ao conselho) | `council_officer` | `manager` | ✔ | Aprova faixa média. |
| **Moderador / Presidente do conselho** | `council_chair` | *(novo)* | ✔ (aprovação) | Aprova faixa alta. |
| **Conselho fiscal** | `fiscal_council` | *(mantém)* | ✖ leitura | **Não aprova — fiscaliza.** |
| **Contador** (escritório terceirizado) | `accountant` | *(mantém)* | ✖ leitura | Não aprova; rascunho→assina. |
| **Membro** | `member` | *(mantém)* | — | Sem alçada financeira. |
| **Admin** (dono/técnico do tenant) | `admin` | *(mantém)* | ✔ coringa | Coringa (ver Q4). |

Somente-leitura permanece `{ fiscal_council, accountant }`. `council_chair` **não** é somente-leitura
(precisa registrar aprovação), mas na prática só aparece na faixa alta.

## 4. Alçadas default alvo

| Faixa | Quem aprova (proposto) | `RolesCsv` | Assinaturas |
| --- | --- | --- | --- |
| Até R$ 500 | Coordenador **ou** Conselheiro responsável | `coordinator,council_officer` | 1 |
| R$ 500 – R$ 5.000 | Coordenador **+** Conselheiro responsável | `coordinator,council_officer` | 2 |
| Acima de R$ 5.000 | Conselheiro responsável **+** Moderador/Presidente | `council_officer,council_chair` | 2 |

**Mudança-chave:** o **conselho fiscal sai** da faixa alta (deixa de autorizar) e entra o
**moderador/presidente**. Os cortes de valor e o teto de compliance (R$ 5.000 → 2 assinaturas) **não
mudam**.

> Nota de segregação: como autoaprovação é bloqueada, na faixa "Até R$ 500" quem **lançou** não conta como
> o aprovador — logo é preciso uma 2ª pessoa mesmo com 1 assinatura. Isso conecta com a
> **recomendação #3 da §8 do parecer** (estado de bootstrap) e com **D-01** (onboarding cadastra a equipe).

## 5. Rótulos customizáveis por tenant

O PRD (§2) exige **vocabulário customizável por tenant**. Proposta: **chaves técnicas estáveis** (RBAC e
alçadas sempre usam a chave) + **rótulo de exibição editável** por tenant (ex.: uma casa pode chamar o
coordenador de "ecônomo", ou o dízimo de "contribuição"). Semear rótulos PT-BR default e permitir edição
na tela de Equipe. Isso desacopla a **semântica de governança** (estável) da **nomenclatura** (variável).
Local sugerido: um mapa de rótulos por papel (nova config ou extensão de `FinanceSettings`), análogo ao
`RecurringLabel` já existente. — *ver Q6.*

## 6. Impacto no código (arquivos a tocar)

| Arquivo | Mudança |
| --- | --- |
| `Security/FinanceRoles.cs` | Renomear constantes; atualizar `All`; manter `ReadOnly = {fiscal_council, accountant}`; adicionar `council_chair`. |
| `Configuration/FinanceConfigSeeder.cs` | Novos `RolesCsv` nas 3 faixas (§4); remover `fiscal_council` como aprovador. |
| `Security/TeamService.cs` | `IsValid`/`SetRoleAsync` passam a validar o novo vocabulário (automático via `All`). |
| `Security/TeamEndpoints.cs` | `GET /roles` retorna o novo `All`; expor rótulos default (§5). |
| `apps/bff/src/auth/token.service.ts` | Apenas comentário (o `role` é repassado como string livre; sem lógica fixa). |
| `apps/web/.../dashboard/pagar/page.tsx`, `.../demonstracoes/page.tsx` | Rótulos/checagens de papel na UI → novo vocabulário. |
| `docs/requirements/finance.md` (§§200-204, RF-FIN-171) | Tabela de alçadas e perfis para o vocabulário de conselho. |
| `docs/architecture/overview.md`, `docs/design/finance-onda2.md` | Atores/labels "gestor" → conselho. |
| `Services/ApprovalService.cs` | **Muda (Q4):** o coringa do `admin` passa a valer só em bootstrap, com aviso auditável; fora dele, respeita a faixa. |
| Endpoints de lançamento (título a pagar, edição) | **Novo guard (Q3):** restringir criação/edição a `{ coordinator, admin }` (operadores). |
| Testes: `TeamRoleTests`, `ApprovalTests`, `FinanceRbacTests` | Atualizar chaves de papel; cobrir bootstrap-override (Q4) e "conselheiro aprova mas não lança" (Q3). |

`FinanceWriteFilter` **não muda de lógica** (segue lendo `CanWrite`); a separação "aprova × lança" da Q3
fica na trava por endpoint, não no filtro global.

## 7. Migração de dados

1. **`catalog.memberships.role`:** `treasurer → coordinator`, `manager → council_officer`; demais
   inalterados; `council_chair` é novo (sem linhas existentes).
2. **`ApprovalTier.RolesCsv` por tenant já provisionado:** o seeder é *add-only* (só semeia se vazio),
   então **não corrige tenants existentes** — precisa de migração explícita que reescreva os `RolesCsv`
   das faixas default não customizadas.

> Como o produto está pré-produção (scaffold), o volume de dados deve ser mínimo; confirmar se há tenants
> reais antes de decidir entre migração automática vs. reset. — *ver Q1.*

## 8. Guarda-corpos preservados (não mudam)

Autoaprovação bloqueada; mín. 1 assinatura; teto de compliance R$ 5.000 → 2 assinaturas; papel deve
pertencer à faixa; um aprovador não assina duas vezes; trilha de auditoria em `member.role_changed` e nas
aprovações; somente `admin` atribui papéis.

## 9. Decisões do PO (resolvidas · 2026-09-11)

- **Q1 — Renomear as chaves técnicas, com migração.** `treasurer→coordinator`, `manager→council_officer`,
  `council_chair` novo. (Ver §7.)
- **Q2 — `admin` é papel técnico separado do Coordenador.** O dono do tenant fica `admin`; Coordenador é
  atribuído à pessoa que opera as finanças (pode ser a mesma pessoa, mas são papéis distintos).
- **Q3 — Conselheiro responsável (e Moderador/Presidente) SÓ aprovam; não lançam.** Consequência de design
  na §9.1.
- **Q4 — `admin` é aprovador-coringa SÓ no bootstrap** (equipe/conselho ainda não cadastrados), com aviso
  auditável; no regime normal respeita a alçada. Consequência de design na §9.1.
- **Q5 — Faixa "Até R$ 500" sempre exige 2 pessoas distintas** (lançador ≠ aprovador), mesmo com 1
  assinatura. Sem piso de autoaprovação.
- **Q6 — Rótulos customizáveis são só nomenclatura de exibição.** Chaves técnicas e regras de alçada
  permanecem estáveis; alçadas seguem ajustáveis pelas faixas. (Ver §5.)

### 9.1 Consequências de design das decisões

**De Q3 — "aprova mas não lança" quebra o modelo binário de permissão.** Hoje `FinanceRoles.CanWrite` é
binário (só-leitura × escreve tudo) e o `FinanceWriteFilter` barra toda requisição mutante de papel
só-leitura. Como **aprovar é uma escrita** (`PayableApproval`), `council_officer` e `council_chair`
**não** podem ser só-leitura. A separação "não lança" passa a ser feita por **trava por endpoint**:

- Manter só-leitura = `{ fiscal_council, accountant }` (inalterado).
- `council_officer` / `council_chair` = **podem escrever** (para aprovar), mas os endpoints de
  **lançamento/edição** de dados financeiros (criar título a pagar, editar, etc.) ganham um guard que
  restringe a **operadores/lançadores** = `{ coordinator, admin }`.
- Aprovação continua governada pela faixa (`RolesCsv`), como hoje.

**De Q4 — o coringa do `admin` deixa de ser incondicional.** Em `ApprovalService.ApproveAsync`, o ramo
"admin satisfaz qualquer faixa" passa a valer **apenas em estado de bootstrap** (equipe/conselho ainda
não montados) e grava **aviso auditável** (ex.: `approval.bootstrap_override`). Fora do bootstrap, o
`admin` respeita a faixa como qualquer papel. Requer uma noção de "estado de bootstrap" do tenant
(ligada a **D-01**); enquanto D-01 não existir, usar um critério simples (ex.: nº de membros com papel
de aprovação < 2).

## 10. Fora de escopo desta decisão

Alteração dos guarda-corpos de compliance; onboarding que cadastra a equipe (**D-01**, separado); estado de
bootstrap auditável (§8 do parecer); portal/RBAC do doador.

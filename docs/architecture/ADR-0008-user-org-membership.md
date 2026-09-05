# ADR-0008 — Vínculo usuário↔unidade (Rede→Unidade)

- **Status:** Aceito (2026-09-05)
- **Contexto:** o login resolvia apenas usuário↔tenant ([ADR-0004](ADR-0004-standalone-auth.md)); faltava
  associar o usuário às **unidades** (organizations) dentro do tenant.

## Contexto

`catalog.memberships` liga **usuário↔tenant** (acesso à instituição), mas as `organizations`
(hierarquia Rede→Unidade) vivem no **schema do tenant** e não tinham vínculo com o usuário. Resultado:
o usuário entrava na diocese, mas não "pertencia" a nenhuma paróquia/comunidade, e os formulários de
cobrança pediam a unidade solta.

## Decisão

**Vínculo usuário↔organização no schema do tenant**, com visibilidade em cascata pela árvore:

- Nova tabela **`org_members`** (`user_id` global do `catalog`, `organization_id`, `role`, único por
  par) no schema `t_<slug>`. Mantém o vínculo junto das `organizations` (evita FK cruzando schemas).
- **Regra de visibilidade:** o usuário vinculado a uma organização enxerga **essa organização e todas
  as filiais (descendentes por `parent_id`)** — `OrgVisibility.VisibleOrgIds` (pura/testada). Assim,
  um papel de rede (diocese) vinculado à raiz vê toda a subárvore; uma paróquia vê a si e suas filiais.
- **Contexto de usuário:** o middleware lê o claim `sub` do JWT (e `X-User` no dev) e popula
  `ICurrentUser`. Endpoints: `GET /api/organizations/mine` (subárvore visível),
  `POST /api/organizations/{id}/members`, e `POST /api/organizations` **auto-vincula o criador** como
  `admin`. Os formulários passam a usar "minhas unidades".
- **Onboarding (primeiro usuário automático):** `POST /onboarding` no BFF cria o admin (hash Argon2 no
  BFF) e delega ao core `POST /api/tenants` (com `adminUserId` + `organizationName`) a criação de
  tenant + schema + `membership` + **organização-raiz**, já **vinculando o admin a ela**; faz
  auto-login. Elimina o seed manual. Página `/signup` no web.

## Alternativas consideradas

- **`organization_id` em `catalog.memberships`:** manteria tudo no catalog, mas cruzaria schemas (as
  organizations são do tenant) e não modelaria múltiplos vínculos/subárvore — preterido.
- **Vínculo único (uma unidade por usuário):** simples demais para rede/contador multi-unidade —
  preterido em favor de N vínculos + cascata.

## Consequências

- **Positivas:** hierarquia Rede→Unidade de verdade; "minhas unidades" no login/forms; base para RBAC
  por unidade e para a consolidação da rede (Reporting).
- **Negativas / trade-offs:** `user_id` referencia o `catalog` sem FK (schemas distintos) — integridade
  garantida pela aplicação. RBAC ainda simples (papel textual); refino no roadmap.

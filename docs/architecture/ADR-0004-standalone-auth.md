# ADR-0004 — Autenticação standalone no BFF

- **Status:** Aceito (2026-09-04)

## Contexto

O Fidellis compartilha o ICP do Formattio (outro produto do mesmo autor), mas **não** haverá
integração técnica nem SSO entre os dois. O Fidellis precisa de identidade própria, com login global
por e-mail que resolva a(s) instituição(ões) do usuário — um usuário pode pertencer a vários tenants
(ex.: um contador que atende várias paróquias).

## Decisão

**Auth própria/standalone no BFF (NestJS)**, sem IdP externo:

- Credenciais em `catalog.users` com hash **Argon2** (`@node-rs/argon2`).
- `catalog.memberships` liga usuário ↔ tenant com papel (RBAC).
- Login: `POST /auth/login` valida credenciais, lista memberships e emite **JWT HS256** com claim
  `tenant` (slug). Refresh token separado (`typ: refresh`).
- Seleção de tenant: `POST /tenants/select` reemite o access token com o tenant escolhido.
- O **segredo JWT é compartilhado** com o core; o core valida a assinatura e lê o claim `tenant`.

## Alternativas consideradas

- **SSO com o Formattio:** descartado por decisão de produto — sem acoplamento entre os produtos.
- **IdP gerenciado (Auth0/Cognito/Keycloak):** reduz esforço de auth, mas adiciona dependência e
  custo, e o modelo "login global → múltiplos tenants" fica mais simples com identidade própria no
  `catalog`. Reavaliar se/quando exigirmos SAML/OIDC empresarial.

## Consequências

- **Positivas:** controle total do modelo de identidade multi-tenant; sem dependência externa no MVP;
  contrato simples BFF↔core (JWT com claim de tenant).
- **Negativas / trade-offs:** somos responsáveis por segurança de credenciais (hashing, rotação de
  segredo, rate limiting, reset de senha — roadmap). HS256 com segredo compartilhado exige rotação
  coordenada; **evoluir para chaves assimétricas/JWKS** quando houver mais consumidores do token.
- **Nota do scaffold:** o core valida o HS256 com um leitor próprio, sem pacote de auth externo; em
  produção, trocar por `Microsoft.AspNetCore.Authentication.JwtBearer` + JWKS.

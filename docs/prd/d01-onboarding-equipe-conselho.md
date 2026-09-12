# D-01 — Login por usuário + onboarding que cadastra equipe & conselho

> **Status:** ✅ **implementada** · 2026-09-11 (decisões Q1–Q6 do PO, §7).
> **Entregue:** tabela `catalog.invitations` + `tenants.onboarding_completed_at` (migração
> `TeamInvitations`); `InvitationService`/`InvitationToken` + endpoints
> `/api/finance/team/{invitations,bootstrap,onboarding/complete}` (convidar restrito a admin+coordenador);
> aceite público no BFF (`/invitations/:token[/accept]`, cria user+membership + auto-login); telas web
> `dashboard/equipe` (etapa 2 do onboarding), `convite/[token]` e banner de bootstrap no `AppShell`.
> O **override auditável** de aprovação em bootstrap (Q4) fica para a **D-02** — aqui só se define/expõe
> o estado de bootstrap que ela consome. Testes: `InvitationTests` (10).
> **Origem:** decisão **D-01** do [parecer do terceiro setor](parecer-finance-terceiro-setor.md) e as
> recomendações #1–#3 da §8 (jornada de adoção).
> **Fluxo:** detalhar → **PO revisa/ajusta** → só então codar.
> **Depende / habilita:** o **estado de bootstrap** desta spec é pré-requisito da **Q4 da
> [D-02](d02-remapeamento-papeis-conselho.md)** (coringa do `admin` só no bootstrap).
> **Referências de código:** `onboarding.service.ts`, `auth.service.ts`, `TenantModule` (`POST /api/tenants`),
> `TeamService`/`TeamEndpoints`, `catalog.users`, `catalog.memberships`.

---

## 1. Objetivo

Garantir que a instituição entre no sistema **como equipe + conselho**, e não como um login único — a
premissa que sustenta todo o veredito do parecer (§3). Hoje a porta de entrada é **unipessoal**: o
onboarding cria só o admin e **não há como a equipe entrar pelo produto**. A D-01 cobre três peças:

1. **Fluxo de convite de membro** (criar/vincular usuário + membership + papel).
2. **Onboarding em duas etapas** (instituição → equipe & conselho).
3. **Estado de bootstrap** do tenant (destrava o 1º pagamento com aviso auditável; habilita a Q4 da D-02).

O "login por usuário, sem login compartilhado" (núcleo original da D-01) **já é suportado** pelo auth
(login por e-mail → membership → claim `role`); o que falta é **o caminho de produto** para os demais
membros existirem.

## 2. Estado atual (o que existe no código)

| Peça | Situação |
| --- | --- |
| **Login por usuário** | ✔ `auth.service.ts`: valida credenciais, resolve memberships, emite claim `role` por tenant ativo. Multi-tenant por usuário pronto. |
| **Onboarding** | 🔴 `onboarding.service.ts` cria **um** usuário (admin) + chama `POST /api/tenants`, que provisiona schema, semeia contas/dimensões/config e cria membership `admin` + org-raiz. Nada convida a equipe. |
| **Atribuição de papel** | 🟠 `TeamService.SetRoleAsync` só altera o papel de quem **já** é `membership`. `TeamEndpoints`: só `admin` atribui. |
| **Convite de membro** | 🔴 **Inexistente.** Não há tabela `invitations` nem endpoint de convite/aceite; não há como criar um novo usuário+membership pela aplicação. |
| **Estado de bootstrap** | 🔴 Inexistente. Não há como saber se a equipe/conselho já foi montada. |

## 3. Escopo da D-01

### 3.1 Fluxo de convite de membro

Permitir convidar uma pessoa por **e-mail + papel**, resolvendo dois casos:

- **E-mail já existe em `catalog.users`** (a pessoa já tem login, talvez de outro tenant): criar a
  **membership** diretamente com o papel indicado e **notificar** por e-mail. Sem nova senha. *(ver Q2)*
- **E-mail novo:** criar uma **invitation** com token; enviar **link mágico**; ao aceitar, a pessoa
  define a senha → cria-se o `catalog.users` + a `membership` com o papel. 

Ações: **criar convite**, **listar pendentes**, **revogar**, **reenviar**, **aceitar** (público, via token).
O envio de e-mail reusa o **outbox/Resend** já existente (CRM).

### 3.2 Onboarding em duas etapas

- **Etapa 1 (já existe):** cria a instituição + o usuário-admin (auto-login).
- **Etapa 2 (nova):** tela **"Monte sua equipe e conselho"** — lista os papéis do modelo de conselho
  (coordenador, conselheiro responsável, moderador/presidente, conselho fiscal, contador) com campo de
  e-mail por papel; dispara os convites da §3.1. **Adiável** ("pular por agora"), mas com **banner
  persistente** de pendência até a equipe mínima existir. *(ver Q3/Q6)*

### 3.3 Estado de bootstrap

Enquanto a equipe/conselho não estiver montada, o tenant está em **bootstrap** — condição que:

- exibe o **banner** de "complete sua equipe";
- habilita o **override auditável** de aprovação da **Q4 da D-02** (o `admin` aprova qualquer faixa,
  gravando `approval.bootstrap_override`), destravando o 1º pagamento sem afrouxar o regime normal.

**Critério de saída do bootstrap:** existir a **equipe mínima de governança** — pelo menos **2 membros
distintos com papel de aprovação** (ex.: coordenador + conselheiro/moderador), de modo que a segregação
(lançador ≠ aprovador) seja possível de fato. *(ver Q4/Q6)*

## 4. Modelo de dados (proposto)

**Nova tabela `catalog.invitations`:**

| Coluna | Tipo | Nota |
| --- | --- | --- |
| `id` | uuid | PK |
| `tenant_id` | uuid | FK `catalog.tenants` |
| `email` | text | normalizado (lower/trim) |
| `role` | text | papel do vocabulário da D-02 |
| `token_hash` | text | hash do token do link (nunca o token em claro) |
| `status` | text | `pending` \| `accepted` \| `revoked` \| `expired` |
| `invited_by` | uuid | FK `catalog.users` |
| `expires_at` | timestamptz | ex.: +7 dias *(ver Q5)* |
| `created_at` / `accepted_at` | timestamptz | trilha |

**Tenant:** coluna `onboarding_completed_at timestamptz null` (ou flag `team_ready`) para dirigir o banner;
o **gate do override** (§3.3) usa a **contagem derivada** de aprovadores como fonte de verdade de compliance.
*(ver Q4)*

## 5. Impacto no código (arquivos a tocar)

| Arquivo / área | Mudança |
| --- | --- |
| `catalog` (migração) | Nova tabela `invitations`; coluna de conclusão de onboarding em `tenants`. |
| **Core — módulo de convites** | Endpoints criar/listar/revogar/reenviar/aceitar; serviço que resolve "e-mail existente × novo" e cria `membership`/`user`. |
| `Security/TeamService.cs`, `TeamEndpoints.cs` | Expor convites junto à equipe; distinguir "atribuir papel a membro existente" de "convidar". |
| `apps/bff/src/onboarding/onboarding.service.ts` | Suportar a etapa 2 (ou expor endpoint para a UI disparar convites pós-signup). |
| `apps/bff` — auth/convite | Rota pública de **aceite** (define senha → cria user+membership); reuso do hash Argon2. |
| **Outbox/Resend (CRM)** | Template de e-mail de convite + reenvio. |
| **Web** | Tela "Monte sua equipe" (etapa 2), tela de aceite de convite, banner de bootstrap. |
| `ApprovalService` (D-02, Q4) | Consome o **critério de bootstrap** definido aqui (§3.3). |
| Testes | Convite (e-mail novo × existente), aceite, revogação/expiração, saída do bootstrap. |

## 6. Segurança & LGPD

- Token do convite: aleatório forte, **guardado só como hash**, expira (Q5), uso único; aceite invalida.
- Enumeração de e-mail: respostas neutras no aceite/erro para não vazar quem é usuário.
- Convite carrega dado pessoal (e-mail + vínculo à instituição religiosa) → cobre-se pela base legal já
  usada no tenant; **não** cruza com dados de formação (isso é o elo Formattio, tratado em **D-11**).
- Toda ação (convite/aceite/revogação/mudança de papel) vai ao `audit_log`.

## 7. Decisões do PO (resolvidas · 2026-09-11)

- **Q1 — Convidam: `admin` + `coordinator`.** O coordenador monta a equipe no dia a dia; o admin também
  pode. (Alinhado à Q2 da D-02 — admin é papel técnico separado do coordenador.)
- **Q2 — E-mail já existente vira membership direto**, com o papel indicado + notificação por e-mail; sem
  novo aceite/senha.
- **Q3 — Etapa 2 do onboarding é adiável** ("pular por agora"), com banner de pendência persistente e o
  override de bootstrap (Q4/D-02) destravando o 1º pagamento até a equipe existir.
- **Q4 — Bootstrap = flag explícita + checagem derivada.** `onboarding_completed_at` dirige o banner/UX;
  a **contagem derivada de aprovadores distintos** é a fonte de verdade que libera/veta o override.
- **Q5 — Link de convite expira em 7 dias, reenviável** (o reenvio gera novo token e reinicia o prazo;
  uso único; aceite invalida).
- **Q6 — Sair do bootstrap = 2 aprovadores distintos.** Basta haver 2 membros distintos com papel de
  aprovação (sem exigir composição específica de papéis), o suficiente para a segregação lançador ≠ aprovador.

## 8. Fora de escopo desta decisão

O remapeamento de papéis/alçadas em si (**D-02**, separado); SSO/identidade federada com o Formattio
(**D-10**) e seu ADR de LGPD (**D-11**); portal/login do doador; autoatendimento de troca de senha
(fora do fluxo de convite).

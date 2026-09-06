# ADR-0012 — Portal do doador (público + link mágico) + auditoria/LGPD

- **Status:** Aceito (2026-09-06)
- **Contexto:** passo 6 (final) do PRD. Fecha o lado do doador e o compliance.

## Contexto

Faltava o **canal do doador** (doação pública, adiada no passo 1) e o **compliance** (auditoria +
LGPD). O módulo Audit era skeleton.

## Decisão

- **Doação pública** (`/doar/<tenant>`): endpoints **sem JWT** sob `/api/public/{tenant}/*`, com o
  tenant resolvido pelo **path** (`PublicTenant.TryResolveAsync`, validando `catalog.tenants`) — mesmo
  princípio do receptor de webhook. Reusa `DonationCheckoutService` (PIX). O BFF encaminha
  `/api/public/*` sem exigir token.
- **Link mágico** (sem senha): `POST /api/public/{tenant}/magic-link` acha o doador por e-mail e
  enfileira (outbox/Resend do passo 4) um link `AppBaseUrl/portal/<tenant>?token=…`; responde 200
  sempre (não vaza). `GET /api/public/{tenant}/me?token=` valida o token e devolve recibos/histórico.
  Token = HMAC HS256 assinado (`DonorMagicToken`) com o segredo da app (`AppSecret`).
- **Auditoria:** `audit_log` (schema do tenant) + `IAuditLog.RecordAsync` (ator = `ICurrentUser`),
  chamado nos pontos sensíveis (checkout público, criação de unidade/recebedor, export/anonymize/opt-out).
  `GET /api/audit/log` + página `/dashboard/auditoria`.
- **LGPD:** `GET /api/crm/donors/{id}/export` (JSON), `POST /{id}/anonymize` (erasure de PII, mantém o
  financeiro; grava `anonymized_at`) e `POST /{id}/opt-out` — a régua (`OutboxNotifier`/
  `ReactivationScanner`) passa a **pular** doadores com `contact_opt_out`.

## Alternativas consideradas

- **Portal com login/senha do doador:** melhor experiência recorrente, mas exige auth de doador
  (senha/recuperação) — preterido em favor do **link mágico** (sem senha), suficiente para consulta.
- **Anonimização apagando linhas financeiras:** violaria a prestação de contas/obrigações fiscais — por
  isso a erasure limpa só a **PII** e preserva doações/recibos.

## Consequências

- **Positivas:** doador consegue doar e consultar recibos sem login; trilha de auditoria e direitos
  LGPD (export/erasure/opt-out) atendidos; superfície pública isolada e com tenant validado.
- **Negativas / trade-offs:** endpoints públicos precisam de **rate limiting/captcha** (roadmap);
  consentimento é binário (sem granularidade marketing/transacional); portal sem gestão da própria
  recorrência ainda. WhatsApp real e migrações EF versionadas seguem no roadmap.

# ADR-0010 — CRM do doador + régua de relacionamento (outbox + Resend)

- **Status:** Aceito (2026-09-05)
- **Contexto:** passo 4 do PRD. Ativa os canais reais das notificações que existiam só como stub
  (`INotifier`/`LogNotifier`, ver [ADR-0007](ADR-0007-recurring-donations-dunning.md)).

## Contexto

O produto precisa reter e reengajar doadores (CRM 360º) e comunicar-se com eles (agradecimento,
dunning, reconquista, reativação). Faltava um canal real e um registro auditável das comunicações.

## Decisão

- **Outbox de mensagens** (`messages`, schema do tenant): toda comunicação é **enfileirada** com
  `dedupe_key` único (idempotência) e despachada depois — desacopla o gatilho do envio e dá trilha.
- **Templates** (`MessageTemplates`, puro/testável) por evento: `thank_you`, `payment_failed`,
  `past_due`, `reactivation`.
- **Canais via `IMessageSender`** (resolvido por canal pelo `MessageDispatcher`):
  - **E-mail: Resend** (API HTTP via `HttpClient`; sem `RESEND_API_KEY` → `skipped`+log, dev-safe).
  - **WhatsApp: adapter stub** (a régua já enfileira; o envio real entra com uma conta BSP).
- **Gatilhos:** `OutboxNotifier : INotifier` (substitui `LogNotifier`) enfileira dunning/past_due;
  `WebhookProcessor` chama `DonationPaidAsync` (agradecimento) ao conciliar; `ReactivationScanner`
  enfileira reativação de inativos (dedupe por doador/mês). O `BillingWorker` roda, por tenant,
  dunning → billing → reativação → **dispatch** da outbox.
- **CRM 360º** (módulo Donations, `/api/crm/donors` e `/{id}`): lista com agregados (total, nº de
  doações, última, **situação**: recorrente/ativo/inativo/novo) e perfil com histórico, recorrências e
  mensagens — escopo nas unidades visíveis (`OrgVisibility`).
- **Fronteiras (ADR-0003):** primitivos de mensageria (outbox, templates, senders, dispatcher,
  reactivation) na **Infrastructure**; Finance (gatilhos/worker) e Donations (CRM) consomem sem
  depender um do outro.

## Alternativas consideradas

- **SMTP (MailKit):** universal, mas o usuário optou por **Resend** (API simples, sem novo pacote).
- **Envio síncrono no gatilho:** simples, porém frágil (latência/erros no fluxo de pagamento) — a
  **outbox + dispatch** é mais robusta e idempotente.

## Consequências

- **Positivas:** comunicação real e auditável; régua idempotente; CRM 360º; encaixe pronto para WhatsApp.
- **Negativas / trade-offs:** envio real de e-mail depende de `RESEND_API_KEY` + domínio verificado;
  sem retry/backoff sofisticado na outbox (MVP: `sent`/`failed`/`skipped`); opt-out/LGPD de
  comunicação e editor de templates ficam no roadmap.

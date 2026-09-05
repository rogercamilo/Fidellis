# ADR-0006 — Pagamentos: cobrança PIX via Pagar.me (Order/Charge, webhook, split)

- **Status:** Aceito (2026-09-05)
- **Contexto:** passo 1 do roadmap do [PRD](../prd/product-requirements.md) — habilitar cobrança real
  de doações. Decisões de negócio em [ADR-0005](ADR-0005-zero-fee-subscription-e-psp-pagarme.md).

## Contexto

O módulo Finance precisa receber doações de verdade. As decisões de produto para este passo foram:
**PIX primeiro**, cobrança **iniciada pelo gestor autenticado**, e split **100% para a unidade**
(a plataforma não retém taxa — assinatura pura). Restava definir como modelar o pagamento, garantir
confiabilidade financeira e resolver o tenant num webhook que **não** carrega o nosso JWT.

## Decisão

**Abstração Order/Charge do Pagar.me (API Core v5)** atrás de uma interface própria
`IPaymentGateway` (`Fidellis.Infrastructure/Payments`), com implementação `PagarmePaymentGateway`
(HttpClient tipado, Basic auth com a `sk`). PIX é o método implementado; boleto/cartão entram na
mesma interface no futuro. Montagem/parse dos payloads ficam em funções puras (`PagarmePayloads`,
`PagarmeWebhook`) cobertas por testes.

**Fluxo de cobrança:** o `DonationCheckoutService` cria a doação (`pending`), chama
`CreatePixOrderAsync` (com o recebedor da unidade, se houver) e persiste `psp_order_id`,
`psp_charge_id` e o QR na doação.

**Resolução de tenant no webhook (sem JWT):** no checkout gravamos um índice global
`catalog.psp_orders (provider_order_id → tenant_slug, donation_id)`. O receptor
`POST /api/finance/webhooks/pagarme` fica **fora** da resolução por JWT: valida o Basic auth do
webhook, extrai o `order id` do payload, descobre o tenant pelo índice, define o `ITenantContext` e
processa no schema `t_<slug>`.

**Confiabilidade financeira:**
- **Idempotência:** cada evento é registrado em `payment_events` com `provider_event_id` **único**;
  reentregas viram no-op.
- **Fonte de verdade:** antes de confirmar, o `WebhookProcessor` **reconsulta** `GetChargeAsync` no
  PSP (defesa contra payloads forjados/atrasados).
- **Conciliação:** ao confirmar, marca a doação `paid` e lança **partida dobrada**
  (débito "PIX a receber" / crédito "Doações") sobre uma `transaction` da conta da unidade.

**Split (0% plataforma):** quando a unidade tem `psp_recipients.provider_recipient_id`, o pedido inclui
um split de **100% para a unidade**. A visão consolidada da rede é responsabilidade do Reporting.

## Alternativas consideradas

- **Tenant no path do webhook** (`/webhooks/pagarme/{tenant}`): exigiria registrar uma URL de webhook
  por tenant — preterido em favor de uma URL única + índice `catalog.psp_orders`.
- **Confiar apenas no payload do webhook** (sem reconsultar o PSP): mais simples, mas frágil quanto a
  spoofing/ordem de entrega — preterido.
- **Migrações EF versionadas agora:** mantido o DDL idempotente + `TenantSchemaUpgrader` (dívida do
  [ADR-0002](ADR-0002-schema-per-tenant.md)); migrar depois.

## Consequências

- **Positivas:** cobrança PIX ponta a ponta, idempotente e conciliada; base pronta para recorrência,
  recibos e dashboards; abstração de gateway isola o PSP e permite testes com um fake.
- **Negativas / trade-offs:** autenticação de webhook por Basic auth (não HMAC) — mitigada pela
  reconsulta ao PSP; onboarding/KYC de recebedores é parcial (funciona em sandbox); boleto/cartão
  ainda não implementados.
- **Operação:** em dev, o webhook exige um **túnel público** (cloudflared/ngrok) até o core; em prod,
  um ingress dedicado. `PAGARME_*` via segredos, nunca versionados.

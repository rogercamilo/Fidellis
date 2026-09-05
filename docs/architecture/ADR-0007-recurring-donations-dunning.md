# ADR-0007 — Doações recorrentes (dízimo mensal) + dunning

- **Status:** Aceito (2026-09-05)
- **Contexto:** passo 2 do roadmap do [PRD](../prd/product-requirements.md). Baseia-se no modelo de
  pagamento do [ADR-0006](ADR-0006-payments-pix-pagarme.md).

## Contexto

A captação do terceiro setor religioso é majoritariamente **recorrente** (dízimo/oferta mensal).
Precisávamos de um motor de recorrência com **dunning** (recuperação de cobranças não pagas). O
verdadeiro débito automático seria o **PIX Automático** (mandato do BCB), mas sua disponibilidade na
API do Pagar.me hoje é **incerta** — não podíamos travar o entregável nele.

## Decisão

**Motor de recorrência próprio no core + PIX por ciclo**, reusando o checkout do passo 1:

- **Modelo:** `recurring_donations` (schema do tenant) guarda o pledge (valor, `day_of_month`,
  `status`, `next_charge_at`, `attempt`). Cada ciclo é uma `Donation` ligada por
  `recurring_donation_id` — reaproveitando todo o fluxo de pagamento e conciliação existente.
- **Scheduler:** `BillingWorker` (`BackgroundService` + `PeriodicTimer`) percorre os tenants
  (schema-per-tenant, mesmo padrão de resolução do webhook) e, por tenant, roda **dunning** e depois
  **geração de ciclos**. Determinístico via `IClock` (fake nos testes). Registrado só quando
  `BILLING_ENABLED` (desligado em testes/CI).
- **Dunning:** ao expirar um ciclo pendente, incrementa `attempt` e reagenda em **D+1, D+3, D+5**
  (configurável em `BILLING_DUNNING_DAYS`); após a última tentativa marca `past_due` e pausa.
- **Confirmação:** o `WebhookProcessor` (passo 1), ao pagar um ciclo, **zera o dunning** e agenda a
  próxima cobrança mensal (`NextChargeDate`, com rollover/clamp de fim de mês).
- **Encaixe p/ PIX Automático:** a geração de cobrança fica isolada no checkout; trocar "PIX por
  ciclo" por "mandato PIX Automático" no futuro é um ponto de extensão, sem reescrever a engine.
- **Notificações:** `INotifier` com `LogNotifier` (stub); canais reais (e-mail/WhatsApp) no passo 4.

## Alternativas consideradas

- **Pagar.me Subscriptions (plans/invoices):** menos código de agendamento, mas acopla ao modelo de
  assinatura do PSP e, para PIX, ainda exige pagamento por ciclo — preterido para manter controle da
  régua e do modelo de dados.
- **PIX Automático (mandato) agora:** maior valor, mas depende de suporte não confirmado no Pagar.me —
  adiado; a arquitetura deixa o encaixe pronto.

## Consequências

- **Positivas:** recorrência + dunning ponta a ponta, testável e independente de features novas do
  PSP; reusa checkout/webhook/conciliação; determinístico nos testes via `IClock`.
- **Negativas / trade-offs:** sem mandato, o doador paga o QR a cada ciclo (a engine cuida de agenda e
  cobrança, não do débito automático). Worker de **instância única** (lock distribuído via Redis fica
  para multi-instância). Fuso: `next_charge_at` calculado em UTC no MVP (TZ por tenant no roadmap).

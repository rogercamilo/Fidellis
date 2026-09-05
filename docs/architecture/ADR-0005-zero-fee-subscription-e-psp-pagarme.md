# ADR-0005 — Monetização (0% de taxa) e PSP (Pagar.me/Stone)

- **Status:** Aceito (2026-09-04)

## Contexto

O mercado de doações para o terceiro setor religioso é liderado por players que cobram **taxa por
transação** (ex.: Doar Digital / Doare: ~2,9% + R$0,25 por doação). Para uma instituição que arrecada
dízimos/ofertas de forma recorrente, essa taxa corrói uma parte relevante da arrecadação.

## Decisão

### Monetização: assinatura pura, **0% de taxa** sobre as doações

O Fidellis cobra **assinatura** (por plano) e **não** retém taxa sobre o valor doado. O tenant fica
com **100%** da doação, pagando apenas o **custo do adquirente** (repassado direto pelo PSP). Este é
o diferencial de marketing central vs. Doar Digital/Doare.

### PSP: **Pagar.me / Stone**

Adquirência via Pagar.me/Stone, cobrindo **PIX** (inclusive PIX Automático recorrente — roadmap),
**cartão** e **boleto**, com **split** para o modelo Rede→Unidade.

## Consequências

- **Positivas:** proposta de valor clara e quantificável; alinhamento de incentivo com a instituição
  (crescemos com assinaturas, não tributando a caridade).
- **Negativas / trade-offs:** receita não escala com o volume doado — depende de aquisição/retenção de
  assinantes; precisamos de planos bem desenhados (limites por unidades/volume) para cobrir custo.
- **Escopo do scaffold:** apenas o cliente/config do PSP (chaves em `.env`, placeholders). O **fluxo
  real de cobrança** (checkout, webhooks, conciliação, dunning) é entregável futuro no módulo Finance.
- **Riscos a tratar no roadmap:** conciliação PIX/boleto, idempotência de webhook, chargeback de
  cartão, e regras de split/repasse para a hierarquia da rede.

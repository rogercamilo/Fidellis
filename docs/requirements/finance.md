# Requisitos — Módulo Finance (Núcleo Financeiro & Tesouraria)

> **Status:** rascunho para revisão do Product Owner · **Versão:** v0.3 — 2026-09-06
> **Escopo:** detalha os requisitos do bloco **Finance** antes do início do desenvolvimento.
> Complementa o [PRD](../prd/product-requirements.md) e os ADRs. Itens marcados **⚠️ Decisão
> pendente** aguardam validação; itens **✔ Decidido** já foram acordados (ver §2).

---

## 1. Visão do bloco

O Finance deixa de ser um "gateway de doações" e passa a ser o **núcleo de gestão financeira e de
tesouraria** da organização contratante — equivalente, guardadas as proporções, aos módulos FI
(Financial Accounting), CO (Controlling) e TR (Treasury) de um ERP como o SAP, porém desenhado para a
realidade do **terceiro setor religioso brasileiro** (entidades sem finalidade de lucros).

**Objetivo de produto:** substituir o controle amador (planilhas, caixa sem conciliação, decisões sem
alçada) por um núcleo com **profissionalismo, compliance e governança**, dando **clareza e
previsibilidade** financeira a organizações que hoje operam no escuro.

### 1.1 Marco normativo de referência
- **ITG 2002 (R1) — CFC:** norma-âncora da contabilidade de entidades sem finalidade de lucros.
  Exige Balanço Patrimonial, **Demonstração do Resultado do Período** (superávit/déficit, não
  "lucro"), DMPL, **DFC** e Notas Explicativas; **segregação de recursos com e sem restrição**; e
  registro de **trabalho voluntário a valor justo**.
- **MROSC — Lei 13.019/2014:** prestação de contas de parcerias com o poder público (convênios/editais).
- **Fundo patrimonial — Lei 13.800/2019:** base para endowment (evolução futura).
- **LGPD:** dados de doadores/credores tratados conforme o módulo Audit/LGPD já existente.

### 1.2 Base já implementada (não refazer)
- Checkout **PIX** (`DonationCheckoutService`) + status por reconsulta ao PSP.
- **Webhook idempotente** (`WebhookProcessor`, dedupe por `provider_event_id`) → conciliação por
  partida dobrada + recibo automático.
- **Recebedores/split** 100% p/ a unidade (`RecipientService`, `PspRecipient`).
- **Recorrência** (dízimo mensal) + **dunning** D+1/D+3/D+5 (`RecurringBillingService`, `BillingWorker`).
- **Doação pública anônima** (`/api/public/{tenant}/donations`).
- Plano de contas + partida dobrada + recibos (módulo Accounting adjacente).

### 1.3 Estados vigentes (referência)
- **Donation.Status:** `pending` | `paid` | `failed` → **passa a incluir** `expired`, `declined`,
  `refunded`, `charged_back` (ver Sub-bloco A).
- **RecurringDonation.Status:** `active` | `paused` | `past_due` | `canceled`.
- **Donation.Method:** `pix` → **passa a incluir** `boleto`, `card`.

---

## 2. Decisões acordadas (§ base para o detalhamento)

| # | Tema | Decisão | Ref. |
| --- | --- | --- | --- |
| D1 | **Alçadas de aprovação** | ✔ Parametrizável (N faixas), **default 3 faixas**, com guarda-corpos de compliance fixos | RF-FIN-112 |
| D2 | **Escopo contábil** | ✔ Exportar dados p/ contador **agora**; gerar demonstrações ITG 2002 internamente na **Onda 4** | Sub-bloco H |
| D3 | **Conciliação** | ✔ Começar por **OFX** → **CNAB retorno** → **Open Finance** (futuro) | Sub-bloco E |
| D4 | **Folha de pagamento** | ✔ **Fora** do escopo; entra apenas como título a pagar no AP | RF-FIN-116 |
| D5 | **Caixa físico** | ✔ **Entra** na Onda 2 (coleta/oferta em espécie) | Sub-bloco E |
| D6 | **Fundo patrimonial (endowment)** | ✔ **Futuro**; fundos com/sem restrição já na Onda 1 | RF-FIN-142 |
| D7 | **Moeda** | ✔ **BRL único**; campo `currency` reservado (default `BRL`), sem câmbio | RNF-07 |
| D8 | **Expiração de cobrança** | ✔ **Ambos** — webhook do PSP (primário) + job de varredura (rede de segurança) | RF-FIN-013 |
| D9 | **Parcelamento no cartão** | ✔ **Não no MVP**; se ativado no futuro, **doador absorve** o custo | RF-FIN-020 |
| D10 | **CPF no cartão** | ✔ **Obrigatório** (antifraude + recibo) | RF-FIN-020 |
| D11 | **Card-on-file (cartão recorrente)** | ✔ Assumir **sim** no design; **verificar** habilitação no plano Pagar.me antes de implementar | RF-FIN-021 |
| D12 | **Reversão de recibo em estorno** | ✔ **Cancelar** o recibo original (marca + motivo/vínculo ao evento) | RF-FIN-022 |
| D13 | **Teto máx. de compliance (1 assinatura)** | ✔ **R$ 5.000** — acima disso, 2 assinaturas sempre obrigatórias (org pode baixar, não elevar) | RF-FIN-112 |
| D14 | **Dimensões obrigatórias** | ✔ **Default configurável** (centro de custo padrão + fundo livre), não obrigatórias no lançamento | RF-FIN-143 |

---

## 3. Mapa dos sub-blocos

| Sub-bloco | Papel | Onda |
| --- | --- | --- |
| **A. Captação & Recebimentos** | Doações (PIX/boleto/cartão), recorrência, split, PIX Automático | 1 |
| **B. Contas a Receber (AR)** | Promessas de doação, convênios/editais, régua de cobrança | 2 |
| **C. Contas a Pagar (AP)** | Fornecedores, despesas, aprovação por alçada, pagamento | 2 |
| **D. Tesouraria** | Contas/caixas múltiplos, saldo consolidado, fluxo de caixa projetado | 2 |
| **E. Conciliação** | Extrato (OFX/CNAB), casamento, caixa físico | 3 |
| **F. Contabilidade gerencial** | Centros de custo, projetos, fundos com/sem restrição | 1 |
| **G. Orçamento** | Orçamento por dimensão, previsto × realizado | 3 |
| **H. Prestação de contas & Compliance** | Demonstrações ITG 2002, MROSC, transparência | 4 |
| **I. Governança & Fechamento** | Alçadas, segregação de funções, fechamento, auditoria | 1–2 |
| **J. Configurabilidade & Analytics** | Nomenclaturas próprias, tipos de doador, previsibilidade | 1 |

---

## 4. Sub-bloco A — Captação & Recebimentos

> **Ajuste transversal:** todo recebimento nasce como um título de **Contas a Receber** (integra com B)
> e carrega obrigatoriamente **centro de custo × projeto × fundo** (F).

### F1.1 — Endurecimento

#### RF-FIN-001 — Validação de assinatura do webhook · *Alta*
Validar a **assinatura HMAC** do Pagar.me sobre o corpo bruto, além do Basic auth atual.
- **RN:** assinatura inválida/ausente (com segredo configurado) → `401`; sem segredo → *fallback*
  Basic auth (dev); validação sobre o **raw body**, antes de desserializar.
- **Config:** `PAGARME_WEBHOOK_SIGNATURE_SECRET`.
- **Aceite:** assinatura inválida → 401; válida → processa; sem segredo → Basic auth.

#### RF-FIN-002 — Rate limiting nos endpoints públicos · *Alta*
Limitar `/api/public/{tenant}/*` por **IP + tenant**.
- **RN:** exceder → `429` + `Retry-After`; balde separado dos endpoints autenticados.
- **Config:** `PUBLIC_RATE_LIMIT_PERMITS` (default **10**), `PUBLIC_RATE_LIMIT_WINDOW_SECONDS`
  (default **300**).
- **Aceite:** flood acima do teto → 429.

#### RF-FIN-003 — Idempotência na criação de cobrança · *Alta*
Aceitar `Idempotency-Key` no checkout (gestor e público).
- **RN:** mesma chave (mesmo tenant) → mesma doação/cobrança, sem novo pedido no PSP; chave expira em
  24h; ausência → comportamento atual.
- **Aceite:** duas chamadas com a mesma chave → uma cobrança no PSP.

### F1.2 — Boleto

#### RF-FIN-010 — Checkout via boleto · *Alta*
- **RN:** `CreateBoletoOrderAsync` na `IPaymentGateway`, retornando linha digitável, código de barras,
  URL do PDF e vencimento; `Donation.Method = "boleto"`; split igual ao PIX; exige CPF/CNPJ.
- **Contrato:** `POST /donations` aceita `method: "pix" | "boleto"` (default `pix`).
- **Aceite:** checkout retorna linha digitável + PDF; doação `pending`.

#### RF-FIN-011 — Conciliação de boleto · *Alta*
- **RN:** `WebhookProcessor` trata boleto pago no **mesmo** fluxo (reconsulta → `paid` → partida
  dobrada → recibo → régua); boleto vencido → `expired`.
- **Aceite:** boleto pago gera lançamento + recibo idênticos ao PIX.

#### RF-FIN-013 — Estado de expiração · *Média* · **D8**
- **RN:** `Donation.Status = "expired"` quando o prazo passa sem pagamento; não reabre (gera nova
  doação); ciclo recorrente expirado → dunning.
- **Detecção (D8):** **ambos** — webhook do PSP como via primária **e** job de varredura interno
  (reaproveita o padrão do `BillingWorker`) como rede de segurança para quando o PSP não notificar.
- **Aceite:** cobrança fora do prazo → `expired`, detectada por webhook ou pela varredura.

### F1.3 — Cartão de crédito

#### RF-FIN-020 — Checkout via cartão (tokenizado) · *Alta* · **D9, D10**
- **RN:** dado do cartão **nunca** no core — front tokeniza (Pagar.me.js), core recebe `card_token`;
  `Donation.Method = "card"`; resposta **síncrona**: aprovado → concilia na hora; recusado →
  `Status = "declined"` + motivo; split igual.
- **Parcelamento (D9):** **não** no MVP (à vista); a estrutura reserva espaço para parcelamento
  futuro, quando ativado, com o **custo absorvido pelo doador** (preserva "100% p/ a unidade").
- **CPF (D10):** **obrigatório** no checkout de cartão (antifraude + recibo).
- **Aceite:** token à vista aprovado → `paid` na hora; recusa → `declined` com motivo; checkout sem
  CPF é rejeitado.

#### RF-FIN-021 — Cartão em recorrência (card-on-file) · *Alta* · **D11**
- **RN:** ciclo debita o cartão salvo no PSP (sem novo QR/boleto); recusa → dunning existente; cartão
  a vencer → notifica doador (CRM).
- **Verificação (D11):** design assume card-on-file **habilitado**; confirmar a tokenização
  recorrente no **plano Pagar.me contratado** antes de implementar. *Fallback* caso não suporte:
  dízimo no cartão via **novo checkout por ciclo**.
- **Aceite:** ciclo debita card-on-file; recusa entra no dunning.

#### RF-FIN-022 — Estorno / chargeback · *Média* · **D12**
- **RN:** evento de estorno → `refunded` | `charged_back` + **lançamento de reversão** (partida
  dobrada inversa).
- **Reversão do recibo (D12):** **cancelar** o recibo original (marca como cancelado, com **motivo** e
  vínculo ao evento de estorno); a trilha de auditoria preserva o histórico.
- **Aceite:** estorno gera lançamento inverso e o recibo original fica cancelado com motivo.

### F1.4 — PIX Automático (mandato)

#### RF-FIN-030 — Autorização de mandato · *Alta*
- **RN:** entidade `PaymentMandate` (doador, org, `RecipientId`, status, id do mandato no PSP,
  validade); doador autoriza **uma vez**; estados `pending_authorization` | `active` | `revoked` |
  `expired`; revogação (portal/LGPD) → recorrência `canceled`.
- **Sequenciamento:** F1.4 é a **última onda do Sub-bloco A**, atrás de boleto/cartão — dá tempo de
  **verificar** a disponibilidade do PIX Automático no PSP e os requisitos BACEN (aviso prévio ao
  pagador). O motor de recorrência atual (PIX-QR por ciclo) segue operando enquanto isso.
- **Aceite:** doador autoriza uma vez; próximos ciclos debitam via mandato.

#### RF-FIN-031 — Cobrança recorrente via mandato · *Alta*
- **RN:** com mandato ativo, `RecurringBillingService` debita via mandato (retrocompatível com PIX-QR
  quando não houver mandato); falha → dunning; aviso prévio ao pagador se exigido.
- **Aceite:** ciclo com mandato debita sem QR; falha → dunning.

---

## 5. Sub-bloco B — Contas a Receber (AR)

#### RF-FIN-100 — Promessas de doação (pledges) · *Alta*
- **RN:** registrar compromisso de doação futura (ex.: membro promete valor/mês) como **título a
  receber**, distinto da doação já paga; alimenta a previsibilidade (J).
- **Aceite:** promessa gera título a receber com vencimento e vínculo ao doador.

#### RF-FIN-101 — Recebíveis de convênios/editais · *Média*
- **RN:** parcelas a receber de parcerias (MROSC) e editais, vinculadas a **projeto/fundo restrito**.
- **Aceite:** convênio gera cronograma de recebíveis marcados como recurso restrito.

#### RF-FIN-102 — Régua de cobrança de AR (aging) · *Média*
- **RN:** classificação por aging (a vencer / vencido por faixa) + lembretes via **outbox** do CRM.
- **Aceite:** título vencido dispara lembrete; relatório de aging disponível.

#### RF-FIN-103 — Baixa e conciliação de AR · *Alta*
- **RN:** recebimento (Sub-bloco A ou extrato E) **baixa** o título correspondente; baixa parcial
  suportada.
- **Aceite:** pagamento recebido quita o título; saldo atualizado.

---

## 6. Sub-bloco C — Contas a Pagar (AP)

#### RF-FIN-110 — Cadastro de fornecedores/credores · *Alta*
- **RN:** dados fiscais (CPF/CNPJ), contato e **chave PIX** para pagamento; credor pode ser
  voluntário/membro (reembolso).
- **Aceite:** credor cadastrado é reutilizável em títulos a pagar.

#### RF-FIN-111 — Lançamento de despesas/obrigações · *Alta*
- **RN:** título a pagar com vencimento, **categoria/rubrica**, **centro de custo × projeto × fundo**,
  anexo de documento fiscal; nasce `awaiting_approval`.
- **Aceite:** despesa lançada exige as dimensões e entra no fluxo de aprovação.

#### RF-FIN-112 — Workflow de aprovação por alçada (parametrizável) · *Alta* · **D1**
Todo título a pagar passa por aprovação antes de poder ser agendado/pago.
- **Parametrizável (a organização configura):** número de faixas, valores de corte, papéis
  aprovadores e nº de assinaturas por faixa.
- **Default de fábrica (semeado no onboarding, editável):**

  | Faixa | Quem aprova | Assinaturas |
  | --- | --- | --- |
  | Até R$ 500 | Tesoureiro | 1 |
  | R$ 500 – R$ 5.000 | Tesoureiro + Gestor | 2 |
  | Acima de R$ 5.000 | Gestor + Conselho fiscal | 2 |

- **Guarda-corpos de compliance (fixos, não desligáveis):**
  1. **Nunca zero aprovação** — mínimo 1 aprovação sempre.
  2. **Segregação de funções** — quem lança **não** pode ser o único aprovador; quem aprova a faixa
     alta ≠ quem executa o pagamento; **autoaprovação bloqueada**.
  3. **Teto para "1 assinatura" (D13)** — acima de **R$ 5.000** (máximo de compliance do sistema),
     **2 assinaturas** passam a ser sempre obrigatórias. A organização pode **baixar** esse teto,
     nunca **elevá-lo** acima de R$ 5.000.
  4. **Trilha imutável** — cada aprovação/rejeição gravada no `audit_log` (quem, quando, valor,
     faixa); não apagável.
  5. **Recurso restrito** — pagamento que consome fundo com restrição respeita a finalidade **em
     qualquer faixa** (a alçada por valor não libera uso indevido).
  6. **Faixas contínuas** — configuração deve cobrir de zero ao infinito sem lacunas (validado).
- **Aceite:** título de R$ 3.000 exige 2 aprovações antes de `to_pay`; autoaprovação recusada;
  configuração com lacuna é rejeitada.

#### RF-FIN-113 — Agendamento e execução de pagamento · *Alta*
- **RN:** agenda de pagamentos; geração de remessa (**PIX**/CNAB); baixa na conciliação (E);
  segregação: executor ≠ aprovador da faixa alta.
- **Aceite:** título aprovado é agendado e, ao pagar, baixado.

#### RF-FIN-114 — Reembolso a voluntários/membros · *Média*
- **RN:** fluxo específico com comprovante obrigatório; passa pela mesma alçada.
- **Aceite:** reembolso exige comprovante e aprovação.

#### RF-FIN-115 — Rateio de despesa · *Média*
- **RN:** dividir um título entre múltiplos centros de custo/projetos/fundos (ex.: energia da sede
  rateada por projeto), por percentual ou valor.
- **Aceite:** despesa rateada gera lançamentos proporcionais por dimensão.

#### RF-FIN-116 — Folha como título a pagar · *Baixa* · **D4**
- **RN:** folha **fora** do escopo de cálculo; lançar apenas o **valor líquido** a pagar
  (categoria "pessoal", com centro de custo) ou importar resumo de sistema externo.
- **Aceite:** salário/pró-labore aparece como título a pagar rateável, sem cálculo de encargos.

---

## 7. Sub-bloco D — Tesouraria

#### RF-FIN-120 — Contas financeiras múltiplas · *Alta*
- **RN:** N contas bancárias + caixas físicos, cada uma com saldo próprio; tipo (banco/caixa).
- **Aceite:** organização cadastra várias contas; saldo por conta.

#### RF-FIN-121 — Saldo consolidado · *Alta*
- **RN:** saldo consolidado da unidade e **consolidado da rede** (Rede→Unidade), respeitando a
  visibilidade de unidades (`OrgVisibility`).
- **Aceite:** rede vê o consolidado das unidades visíveis.

#### RF-FIN-122 — Transferências internas · *Média*
- **RN:** transferência entre contas **não** contamina receita/despesa (só tesouraria); dupla
  perna (saída de uma conta, entrada em outra).
- **Aceite:** transferência ajusta saldos sem afetar o resultado.

#### RF-FIN-123 — Aplicações financeiras / reservas · *Baixa*
- **RN:** registrar reservas e rendimentos; base para fundo patrimonial (endowment, **futuro** — D6).
- **Aceite:** aplicação registrada com rendimento lançado.

#### RF-FIN-124 — Fluxo de caixa projetado · *Alta*
- **RN:** projeção **D+30/60/90** a partir de recorrências ativas (A), AR (B) e AP (C) agendados;
  eixo do valor de **previsibilidade** do produto.
- **Aceite:** painel mostra saldo projetado por horizonte com entradas/saídas previstas.

---

## 8. Sub-bloco E — Conciliação bancária e de caixa

#### RF-FIN-130 — Importação de extrato · *Alta* · **D3**
- **RN:** importar **OFX** (todos os bancos) e **CNAB retorno** (boletos); **Open Finance** no roadmap.
- **Aceite:** extrato importado lista os lançamentos para conciliar.

#### RF-FIN-131 — Casamento automático · *Alta*
- **RN:** sugestão de casamento por valor/data/histórico; baixa manual assistida; conciliação
  atualiza AR/AP.
- **Aceite:** lançamento do extrato casa com título e o baixa.

#### RF-FIN-132 — Caixa físico (coleta/oferta em espécie) · *Alta* · **D5**
- **RN:** **abertura/fechamento de caixa**; registro de coleta por evento (missa/culto);
  **conferência por 2 responsáveis** (segregação); depósito vira transferência interna para a conta
  bancária.
- **Aceite:** caixa aberto e fechado com conferência; coleta registrada e depositada.

#### RF-FIN-133 — Divergências · *Média*
- **RN:** fila de itens não conciliados com tratativa e nota.
- **Aceite:** item sem correspondência fica pendente até resolução.

---

## 9. Sub-bloco F — Contabilidade gerencial (dimensional) · *Onda 1*

#### RF-FIN-140 — Centros de custo · *Alta*
- **RN:** centros configuráveis (pastoral, obra social, manutenção, administrativo…).
- **Aceite:** organização cria/edita centros de custo.

#### RF-FIN-141 — Projetos · *Média*
- **RN:** projetos com orçamento e prazo próprios; vinculáveis a fundos restritos.
- **Aceite:** projeto criado recebe receitas/despesas.

#### RF-FIN-142 — Fundos com e sem restrição (ITG 2002) · *Alta* · **D6**
- **RN:** toda receita/despesa marca se o recurso é **livre** ou **restrito a uma finalidade**;
  **bloquear** uso de recurso restrito fora da finalidade; base para endowment futuro.
- **Aceite:** despesa contra fundo restrito exige aderência à finalidade; uso indevido é bloqueado.

#### RF-FIN-143 — Três dimensões com default configurável · *Alta* · **D14**
- **RN:** toda transação carrega **centro de custo × projeto × fundo** para relatórios cruzados.
  **Não são obrigatórias no lançamento** (D14): quando não informadas, o sistema aplica um **centro
  de custo padrão** e o **fundo livre** configurados por unidade, mantendo o dado sempre completo sem
  travar a usabilidade (projeto é opcional).
- **Aceite:** lançamento sem dimensão usa os defaults; relatórios cruzam as 3 dimensões.

---

## 10. Sub-bloco G — Orçamento · *Onda 3*

#### RF-FIN-150 — Orçamento anual · *Média*
- **RN:** orçamento por centro de custo/projeto/fundo, por período.
- **Aceite:** orçamento cadastrado por dimensão/ano.

#### RF-FIN-151 — Previsto × Realizado · *Média*
- **RN:** comparativo com **alertas de estouro**.
- **Aceite:** estouro de orçamento gera alerta.

#### RF-FIN-152 — Revisões orçamentárias · *Baixa*
- **RN:** revisões versionadas com histórico.
- **Aceite:** revisão preserva a versão anterior.

---

## 11. Sub-bloco H — Prestação de contas & Compliance · *Onda 4* · **D2**

#### RF-FIN-160 — Demonstrações ITG 2002 · *Alta*
- **RN:** gerar Balanço Patrimonial, **DRP** (superávit/déficit), **DMPL**, **DFC** e apoio a Notas
  Explicativas. **Posicionamento:** o sistema gera o **rascunho**; o contador valida/assina.
- **Aceite:** demonstrações geradas a partir dos lançamentos do período.

#### RF-FIN-161 — Segregação de recursos com/sem restrição · *Alta*
- **RN:** demonstrações separam recursos livres e restritos (exigência da norma).
- **Aceite:** relatório evidencia saldos por restrição.

#### RF-FIN-162 — Trabalho voluntário a valor justo · *Média*
- **RN:** registrar trabalho voluntário como se houvesse desembolso (exigência ITG 2002).
- **Aceite:** voluntariado lançado a valor justo aparece nas demonstrações.

#### RF-FIN-163 — Prestação de contas de parcerias (MROSC) · *Média*
- **RN:** relatório de execução por convênio/edital (receitas recebidas × despesas do projeto).
- **Aceite:** relatório por parceria pronto para o órgão repassador.

#### RF-FIN-164 — Portal de transparência · *Média*
- **RN:** página pública por unidade (receitas, despesas, projetos) — reforça a confiança do doador.
- **Aceite:** portal exibe números do período configurado.

#### RF-FIN-165 — Exportação para o contador · *Alta* · **D2**
- **RN:** exportação **ECD/ECF-friendly** (CSV/SPED) e fecho mensal para o contador externo.
- **Aceite:** exportação do período abre no sistema do contador.

---

## 12. Sub-bloco I — Governança & Fechamento · *Ondas 1–2*

#### RF-FIN-170 — Fechamento de período · *Alta*
- **RN:** bloquear lançamentos retroativos em período fechado; reabertura exige papel elevado + log.
- **Aceite:** lançamento em período fechado é recusado.

#### RF-FIN-171 — RBAC financeiro + segregação de funções · *Alta*
- **RN:** perfis **tesoureiro**, **gestor**, **conselho fiscal (somente leitura)**, **contador
  externo**; segregação lançar ≠ aprovar ≠ pagar.
- **Aceite:** cada perfil só executa o que lhe cabe; conselho fiscal não altera dados.

#### RF-FIN-172 — Trilha de auditoria financeira · *Alta*
- **RN:** estende o `audit_log` para lançamentos, aprovações, baixas, conciliações e fechamentos.
- **Aceite:** toda ação sensível fica rastreável e imutável.

#### RF-FIN-173 — Aprovação/assinatura de prestação de contas · *Média*
- **RN:** relatórios de prestação de contas passam por aprovação registrada antes de publicados.
- **Aceite:** relatório publicado tem aprovação rastreável.

---

## 13. Sub-bloco J — Configurabilidade & Analytics · *Onda 1*

#### RF-FIN-180 — Nomenclatura configurável da doação recorrente · *Alta*
- **RN:** cada organização nomeia à sua maneira (dízimo, contribuição, mensalidade, dádiva…) —
  **rótulo de UI/relatórios**, sem alterar a mecânica.
- **Aceite:** o rótulo configurado aparece em toda a experiência do tenant.

#### RF-FIN-181 — Nomenclatura configurável da doação pontual · *Alta*
- **RN:** rótulo próprio (oferta, apoio, doação avulsa, semeadura…).
- **Aceite:** idem RF-FIN-180 para a doação pontual.

#### RF-FIN-182 — Tipos de doador + jornada de conversão · *Alta*
- **RN:** tipos configuráveis; trilha e métricas da conversão **apoiador pontual → doador recorrente
  (membro)**.
- **Aceite:** conversão registrada e mensurável no painel.

#### RF-FIN-183 — Rubricas de receita/despesa configuráveis · *Média*
- **RN:** rubricas mapeadas ao plano de contas.
- **Aceite:** nova rubrica passa a estar disponível nos lançamentos.

#### RF-FIN-184 — Painel de previsibilidade · *Alta*
- **RN:** receita recorrente mensal projetada, inadimplência/churn de recorrentes, retenção, ticket
  médio, sazonalidade — a "clareza e previsibilidade" do briefing.
- **Aceite:** painel mostra os indicadores com série histórica.

---

## 14. Requisitos não-funcionais

- **RNF-01 Segurança PCI:** dado de cartão nunca no core (só token); segredos de PSP/webhook fora do
  versionamento.
- **RNF-02 Idempotência:** conciliação por `provider_event_id`; criação por `Idempotency-Key`.
- **RNF-03 Fonte de verdade:** confirmação sempre reconsulta o PSP antes de `paid`.
- **RNF-04 Multi-tenant:** todo dado novo no schema do tenant; índices globais só em `catalog`.
- **RNF-05 Testes:** cada método novo coberto no *fake* de `IPaymentGateway` + teste de integração;
  `BILLING_ENABLED=false` em CI.
- **RNF-06 Observabilidade:** logar transições de status com `donation_id`/`charge_id`; auditar ações
  financeiras.
- **RNF-07 Moeda:** BRL único; campo `currency` (default `BRL`) reservado, sem câmbio (**D7**).
- **RNF-08 Governança da configuração:** guarda-corpos de compliance (RF-FIN-112) não podem ser
  desligados por parametrização.

---

## 15. Ondas de entrega (dentro da Fase 1 Finance)

| Onda | Conteúdo | Sub-blocos |
| --- | --- | --- |
| **1 — Fundação transacional** | Endurecimento + boleto/cartão; **dimensões** (centro de custo/projeto/fundo); **configurabilidade** de nomenclatura/tipos de doador; RBAC financeiro base | A, F, J, I(parcial) |
| **2 — Ciclo financeiro** | **AR**, **AP** (com alçadas), **Tesouraria** (fluxo de caixa), **caixa físico** | B, C, D, E(parcial), I |
| **3 — Controle** | Conciliação (OFX/CNAB) completa + **Orçamento** | E, G |
| **4 — Compliance** | Demonstrações ITG 2002, prestação de contas MROSC, transparência, fechamento | H, I |

---

## 16. Impacto em outros módulos

| Módulo | Impacto |
| --- | --- |
| **Accounting** | Lançamentos por método/dimensão; reversão em estorno; cancelamento de recibo; demonstrações ITG 2002. |
| **CRM/Donations** | Gatilhos: recusa de cartão, cartão a vencer, aviso prévio de débito (mandato), aging de AR; nomenclaturas configuráveis. |
| **Reporting** | Breakdown por método/dimensão real; previsibilidade; consolidação da rede. |
| **Portal público** | Seleção de método no checkout; rate limiting; portal de transparência. |
| **Audit/LGPD** | Trilha estendida a lançamentos/aprovações/baixas; revogação de mandato. |

---

## 17. Fora de escopo (Fase 1)

- Cálculo de **folha de pagamento** (só o título a pagar — D4).
- **Fundo patrimonial/endowment** ativo (só fundos restritos — D6).
- **Multi-moeda**/câmbio (D7).
- Carteiras digitais (Apple/Google Pay), débito em conta, cripto.
- Antifraude além do provido pelo PSP.
- **Open Finance** (só OFX/CNAB nesta fase — D3).
- Assinatura eletrônica jurídica de demonstrações (o contador assina fora).

---

## 18. Decisões resolvidas (rodada de 2026-09-06)

Todas as pendências desta rodada foram **decididas** (ver §2, D8–D14):

1. **RF-FIN-013 (D8):** expiração detectada por **webhook + job de varredura** (ambos).
2. **RF-FIN-020 (D9/D10):** **sem parcelamento** no MVP (doador absorve se ativado); **CPF obrigatório**.
3. **RF-FIN-021 (D11):** card-on-file **assumido** no design; **verificar** habilitação no plano Pagar.me.
4. **RF-FIN-022 (D12):** estorno **cancela** o recibo original (com motivo/vínculo ao evento).
5. **RF-FIN-030:** PIX Automático é a **última onda** do Sub-bloco A; **verificar** PSP + BACEN antes.
6. **RF-FIN-112 (D13):** teto máximo de compliance = **R$ 5.000** (2 assinaturas sempre acima disso).
7. **RF-FIN-143 (D14):** dimensões **não obrigatórias** no lançamento; **default configurável** cobre.

### Itens de verificação externa (não bloqueiam o design)
- **PSP-1 (D11):** confirmar tokenização recorrente (card-on-file) no plano Pagar.me contratado.
- **PSP-2 (RF-FIN-030):** confirmar disponibilidade do PIX Automático no PSP + requisitos BACEN.

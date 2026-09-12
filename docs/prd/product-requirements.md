# PRD — Fidellis

Documento de requisitos de produto do Fidellis. Complementa a
[visão de arquitetura](../architecture/overview.md) e os ADRs.

## 1. Visão e problema

Instituições do terceiro setor religioso brasileiro (Novas Comunidades, Institutos Religiosos,
dioceses, congregações, paróquias) dependem de doações recorrentes (dízimos, ofertas) e de campanhas.
As plataformas atuais **cobram taxa por transação**, são fracas em **hierarquia de rede** e em
**prestação de contas**, e tratam o doador como um pagamento avulso, não como um relacionamento.

**Fidellis** é um SaaS multi-tenant que resolve isso com **assinatura de 0% de taxa**, hierarquia
**Rede→Unidade** de primeira classe, **CRM 360º do doador** e **prestação de contas automática**.

## 2. Público-alvo (ICP)

O público se descreve por **dois eixos ortogonais** — a hierarquia de rede (onde o dinheiro é
consolidado) e a governança interna de cada unidade (quem opera e quem aprova). Confundi-los foi o erro
da 1ª leitura; o modelo de governança e o remapeamento de papéis/alçadas são detalhados no
[parecer do terceiro setor](parecer-finance-terceiro-setor.md) (§2 e decisão **D-02**), que é a fonte de
verdade sobre o assunto.

**Eixo 1 — Hierarquia de rede (segmentos):**
- **Redes:** dioceses, institutos, congregações que consolidam várias unidades.
- **Unidades:** paróquias, casas/comunidades, obras sociais.

**Eixo 2 — Governança interna (como o público realmente se organiza):** nunca uma pessoa só — **sempre
uma equipe voluntária sob um conselho**. Os papéis (vocabulário **customizável por tenant**) são:
- **Coordenador** — está à frente da equipe financeira/contábil no dia a dia.
- **Conselheiro responsável** — responde pela equipe perante o conselho.
- **Moderador/presidente do conselho** — aprovação de alçada alta.
- **Conselho fiscal** — **fiscaliza** (somente-leitura); não autoriza pagamentos.
- **Contador** — escritório **terceirizado**; recebe o rascunho, valida e assina (somente-leitura no sistema).
- **Doador** — **membro** (dízimo/oferta) ou **não-membro/apoiador** (doação pública ou anônima).

> O vocabulário corporativo anterior (gestor de rede/unidade, secretaria) fica **superado** por este
> mapa: "gestão" corresponde a coordenador/conselheiro na governança de conselho, e a alçada de rede é
> uma dimensão da consolidação (Eixo 1), não um cargo.

## 3. Análise competitiva

| Critério                    | **Fidellis**                          | Doar Digital / Doare        | inChurch / Eklesia        |
| --------------------------- | ------------------------------------- | --------------------------- | ------------------------- |
| Modelo de cobrança          | **Assinatura, 0% sobre doações**      | ~2,9% + R$0,25 por transação| Varia / taxa + planos     |
| Hierarquia Rede→Unidade     | **Nativa** (consolidação da rede)     | Fraca                       | Parcial                   |
| CRM 360º do doador          | **Sim** (histórico, recorrência, régua)| Limitado                    | Foco em app da igreja     |
| PIX Automático + dunning    | **Roadmap central**                   | Parcial                     | Parcial                   |
| Prestação de contas/recibos | **Automática por unidade**            | Básica                      | Básica                    |
| Multi-tenant/isolamento     | **Schema-per-tenant**                 | n/d                         | n/d                       |

**Posição:** superar o **Doar Digital** eliminando a taxa por transação e entregando gestão de rede +
relacionamento com o doador que os concorrentes não priorizam.

## 4. Diferenciais de produto

1. **0% de taxa** sobre doações (só assinatura) — ver [ADR-0005](../architecture/ADR-0005-zero-fee-subscription-e-psp-pagarme.md).
2. **Hierarquia Rede→Unidade** com consolidação (diocese vê todas as paróquias).
3. **CRM 360º do doador** — histórico, recorrência, segmentação, régua de relacionamento.
4. **PIX Automático recorrente** com **dunning** (recuperação de falhas de cobrança).
5. **Recibos e prestação de contas automáticos** por unidade (transparência/LGPD).

## 5. Módulos do core

| Módulo       | Responsabilidade                                                        | Status |
| ------------ | ---------------------------------------------------------------------- | --------------- |
| **Tenant**   | Registro/provisionamento de instituições (schema `catalog` + `t_<slug>`); **onboarding em 2 etapas com convite de equipe/conselho** e estado de bootstrap (D-01) | **Funcional** — criar/listar tenant, provisiona schema; convites (`catalog.invitations`) + aceite público |
| **Donations**| Organizations, entradas, **CRM 360º do doador** + régua de relacionamento; **portal de membro** (dízimo/oferta self-service) | **Funcional** — CRM + outbox/e-mail (Resend); marca membro; link mágico |
| **Finance**  | Cobrança PIX (Pagar.me), webhook, conciliação, split, recorrência + dunning; **governança de conselho** (papéis/alçadas, D-02), **entradas de 1ª classe** (caixa/manual, D-05), **`entry_type`** dízimo/oferta/doação (D-06/D-07), **campanhas** (D-08), **gestão avançada** (D-09) | **Funcional** — entrada (`Entry`) agnóstica a canal; alçadas de conselho com override de bootstrap auditável |
| **Accounting**| Plano de contas, razão/balancete, demonstrações ITG 2002, recibos automáticos | **Funcional** — partida dobrada, balancete/razão, demonstrações, recibos (PDF/R2) |
| **Reporting**| Dashboards, série temporal, consolidação da rede                      | **Funcional** — overview, série mensal, consolidação por unidade (Recharts) |
| **Audit**    | Trilha de auditoria + LGPD (export/anonimização/opt-out)              | **Funcional** — audit_log + LGPD; portal público do doador |

## 6. Requisitos funcionais (alto nível)

- **RF-01 Identidade global:** login por e-mail resolve o(s) tenant(s) do usuário (memberships/RBAC).
- **RF-02 Provisionamento de tenant:** criar instituição cria schema isolado + tabelas.
- **RF-03 Contexto de tenant:** toda operação de dados ocorre no schema do tenant do request.
- **RF-04 Doações:** receber doação via **PIX** (checkout com QR + conciliação por webhook) e
  **dízimo recorrente mensal** com dunning; cartão/boleto e PIX Automático (mandato) seguem no roadmap.
- **RF-05 Repasse/split:** doação vai 100% para a unidade (recebedor Pagar.me por unidade);
  consolidação da rede fica no Reporting.
- **RF-06 Recibos/prestação de contas:** recibo automático por doação (número sequencial, HTML
  imprimível) + lançamento contábil (partida dobrada) contra o plano de contas; balancete/razão.
- **RF-07 Relatórios (roadmap):** dashboard por unidade e consolidado da rede; exportações.
- **RF-08 Auditoria/LGPD:** trilha de ações sensíveis (`audit_log`) + direitos do titular
  (exportação, anonimização/erasure, opt-out de comunicação) + portal público do doador.
- **RF-09 Onboarding equipe & conselho (D-01):** cadastro em 2 etapas — instituição → **convite de
  membros por e-mail + papel** (e-mail existente vira membership; novo recebe link mágico 7d); **estado de
  bootstrap** que destrava o 1º pagamento com aviso auditável até haver 2 aprovadores distintos.
- **RF-10 Governança de conselho (D-02):** vocabulário de papéis `coordinator/council_officer/
  council_chair/fiscal_council/accountant` + alçadas default de conselho; segregação **aprova ≠ lança**;
  `admin` coringa **só em bootstrap** (grava `approval.bootstrap_override`); rótulos de papéis
  **customizáveis por tenant**.
- **RF-11 Entradas agnósticas a canal (D-05/D-06/D-07):** toda entrada nasce de 1ª classe (receita +
  dimensão + tesouraria) — checkout, **caixa físico** (coleta discriminável por tipo) e **lançamento
  manual**; `entry_type` (**dízimo/oferta/doação**) de 1ª classe; **doação recorrente do apoiador**.
- **RF-12 Campanhas (D-08):** meta × arrecadado, janela, **página pública** de doação e **earmark** a
  fundo restrito/projeto (ITG 2002) → prestação de contas por campanha.
- **RF-13 Navegação por papel + IA Entradas/Saídas (D-03/D-04):** menu reorganizado (Entradas/Saídas/
  Contabilidade/Prestação de contas/Organização), filtrado pelo papel, com landing por papel.
- **RF-14 Gestão avançada (D-09):** modo ativável por tenant que oculta do público-base os pontos fora da
  curva (convênios/MROSC, projetos; NF/faturamento reservado como futuro).
- **RF-15 Portal do membro:** membro (nativo, marcado pelo tenant) autentica por link mágico e faz
  **dízimo/oferta** (pontual) e **dízimo recorrente** por autoatendimento.

## 7. Requisitos não-funcionais

- **Isolamento & LGPD:** dados por instituição isolados por schema; export/backup por tenant.
- **Segurança:** hash Argon2; JWT assinado; segredos fora do versionamento; WAF/rate limiting na borda.
- **Confiabilidade financeira:** idempotência de webhook (`payment_events`); reconsulta ao PSP como
  fonte de verdade; conciliação PIX com partida dobrada; recorrência/dunning entregues; guarda-corpos de
  alçada não-desligáveis (mín. 1 assinatura, autoaprovação bloqueada, teto de 2 assinaturas).
- **Observabilidade:** health `live`/`ready` em BFF e core; logs estruturados (roadmap: tracing).
- **Performance:** cache/fila em Redis; front na borda (Cloudflare).
- **Portabilidade:** monorepo com CI reprodutível (Node e .NET).

## 8. Roadmap (pós-scaffold)

1. ✅ **Cobrança real via PIX** (módulo Finance): checkout PIX, webhook idempotente, conciliação e
   split 100% p/ a unidade. **Entregue.** (Boleto/cartão desenhados na abstração Order/Charge.)
   Ver [ADR-0006](../architecture/ADR-0006-payments-pix-pagarme.md).
2. ✅ **Recorrência (dízimo mensal) + dunning** — motor próprio no core (scheduler multi-tenant),
   cobrança PIX por ciclo, régua D+1/D+3/D+5 → `past_due`; encaixe pronto p/ PIX Automático (mandato).
   **Entregue.** Ver [ADR-0007](../architecture/ADR-0007-recurring-donations-dunning.md).
3. ✅ **Razão contábil + recibos** (módulo Accounting) — plano de contas configurável, partida dobrada
   contra o plano, balancete/razão consolidados (Rede→Unidade) e recibo automático (HTML imprimível).
   **Entregue.** Ver [ADR-0009](../architecture/ADR-0009-accounting-receipts.md).
4. ✅ **CRM do doador + régua de relacionamento** — CRM 360º (histórico/situação), outbox idempotente,
   e-mail real (Resend), gatilhos (agradecimento/dunning/past_due) + reativação de inativo; WhatsApp
   desenhado (stub). **Entregue.** Ver [ADR-0010](../architecture/ADR-0010-crm-relationship-outbox.md).
5. ✅ **Dashboards + consolidação da rede** (módulo Reporting) — overview, série temporal mensal,
   consolidação por unidade e quebra por método (Recharts). **Entregue.**
   Ver [ADR-0011](../architecture/ADR-0011-reporting-dashboards.md).
6. ✅ **Portal do doador + auditoria/LGPD** — doação pública (`/doar/<tenant>`) + link mágico
   (`/portal/<tenant>`), trilha de auditoria e LGPD (export/anonimização/opt-out). **Entregue.**
   Ver [ADR-0012](../architecture/ADR-0012-donor-portal-audit-lgpd.md).

> **Roadmap inicial do PRD concluído (passos 1–6).**

### 8.1 Onda de governança & terceiro setor (parecer — D-01→D-09) — **entregue**

Derivada do [parecer do terceiro setor](parecer-finance-terceiro-setor.md) (specs `docs/prd/d0*.md`).
Todas implementadas e mergeadas na `main`:

- ✅ **D-01** Onboarding equipe & conselho (convites + bootstrap). `docs/prd/d01-onboarding-equipe-conselho.md`.
- ✅ **D-02** Papéis e alçadas de conselho (aprova ≠ lança; override de bootstrap). `docs/prd/d02-...md`.
- ✅ **D-03/D-04** IA Entradas/Saídas + navegação por papel + landing. `docs/prd/d03-d04-...md`.
- ✅ **D-05** Caixa físico e lançamento manual como entrada de 1ª classe. `docs/prd/d05-...md`.
- ✅ **D-06/D-07** `entry_type` de 1ª classe + doação recorrente do apoiador. `docs/prd/d06-...md`.
- ✅ **D-08** Campanhas (earmark + meta × arrecadado + página pública). `docs/prd/d08-campanhas.md`.
- ✅ **D-09** Modo "Gestão avançada" (convênios/MROSC/NF fora do núcleo). `docs/prd/d09-...md`.
- ✅ **Portal do membro** (dízimo/oferta self-service) + débitos técnicos: rótulos de papéis por tenant,
  remoção de rótulos legados, docs de vocabulário, refactor `Donation → Entry`.

### 8.2 Integração Formattio (D-10/D-11) — **planejada, bloqueada por compliance**

Adaptador **opcional de mão única** (importação) com **identidade federada**; regra confirmada
"integração estabelecida ⇒ formando é membro". **Bloqueada** até a sequência: **auditoria do cadastro
Formattio → RIPD → ADR-0013 (LGPD) aceito**. Ver [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md)
e o [plano de auditoria](d10-formattio-audit-plan.md).

**Estado (2026-09-12):** a esteira documental está **completa** — [auditoria concluída](d10-formattio-audit-worksheet.md),
[RIPD/DPIA](../architecture/ripd-formattio-fidellis.md) rascunhado, e os **rascunhos jurídicos** escritos
em `docs/legal/`: [minuta de parecer](../legal/parecer-juridico-cruzamento-sensivel.md) (base legal =
consentimento art. 11, I), [termo de consentimento](../legal/consentimento-integracao-formattio.md) e
[acordo de compartilhamento](../legal/acordo-compartilhamento-formattio-fidellis.md). ⚠️ São **modelos**,
não pareceres assinados. O bloqueio remanescente é **exclusivamente humano**: advogado(a) habilitado(a)
assina o parecer, DPO ciente, acordo assinado → **ADR-0013 → Aceito** → só então implementar
(`Donor.ExternalId`/`Source` + canal de importação + give autenticado server-to-server; contrato greenfield
no lado do Formattio).

### 8.3 Evoluções futuras

WhatsApp real, rate limiting reforçado no público, portal com login/senha do doador, NF-e/faturamento
(gestão avançada, sob demanda), exportações/agendamento de relatórios, entidade de entrada canônica com
subtipos (hoje `Entry` reusada por origem).

## 9. Fora de escopo (atual)

O produto evoluiu bem além do scaffold inicial: o roadmap do PRD (§8, passos 1–6) e a onda de governança
do parecer (§8.1, D-01→D-09) estão **entregues**. Permanecem **fora de escopo** por ora:

- **NF-e / faturamento** — reservado como recurso de "gestão avançada" (D-09), sob demanda.
- **Integração Formattio em produção** — esteira documental completa (auditoria, RIPD, rascunhos de
  parecer/consentimento/acordo em `docs/legal/`); aguarda **assinatura jurídica/DPO** → ADR-0013 Aceito (§8.2).
- **Endowment / fundo patrimonial ativo, multi-moeda, folha de pagamento** — fora do núcleo.
- **PIX Automático (mandato)** — o motor de recorrência já existe; o mandato do PSP entra quando disponível.

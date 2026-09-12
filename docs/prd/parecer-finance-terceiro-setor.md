# Parecer consolidado — Finance para o terceiro setor + decisões de produto

> **Status:** consolidado para decisão do Product Owner · **Versão:** v2 (v1 consolidou as rodadas v1→v6; v2 acrescenta a §8 — jornada de adoção, foco no público-alvo) · 2026-09-11
> **Autoria:** assessoria sênior de contabilidade do terceiro setor + análise do código implementado.
> **Objeto:** avaliar se o núcleo financeiro do Fidellis agrega valor a associações/comunidades do
> terceiro setor religioso e registrar as **decisões de produto/arquitetura** derivadas da discussão.
> Complementa [requirements/finance.md](../requirements/finance.md), o [PRD](product-requirements.md) e os ADRs.

---

## 1. Contexto e objetivo

Avaliar se a proposta do módulo Finance **agrega valor** a associações e comunidades que hoje operam
**sem metodologia nem governança** financeira/contábil, ou se **atrapalharia** mais do que ajudaria — e
transformar as conclusões em decisões acionáveis.

## 2. Premissas confirmadas sobre o público-alvo

- **Sempre uma equipe, nunca uma pessoa só.** Voluntários organizam finanças, obrigações
  tributárias/fiscais e contabilidade, com um **coordenador** à frente.
- **Governança por conselho.** A instituição é governada por um **conselho**; um **conselheiro** é o
  responsável direto pela equipe financeira/contábil e se reporta ao **moderador/presidente** do conselho.
- **A dor central é operar sem método.** Recebem (doações, dízimos/ofertas, campanhas) e pagam (contas,
  benfeitorias, obras, projetos) **sem nenhuma metodologia ou governança**.
- **A contabilidade formal é terceirizada** num escritório; o sistema gera o **rascunho** e o **contador
  valida/assina** (já é a decisão D2 de `finance.md`).
- **Não são fornecedores de produtos/serviços** (salvo exceção): **emissão de NF/faturamento é ponto
  fora da curva** e fica para gestão avançada.

## 3. Veredito

Com o contexto correto, o veredito é **favorável**. A robustez de governança do módulo (alçadas,
segregação de funções, trilha de auditoria, fechamento) **não é excesso**: é exatamente **o remédio para
a dor declarada** (operar sem método). Os riscos remanescentes são de **vocabulário, arquitetura de
informação (IA) e onboarding** — não de excesso de funcionalidade — e todos os ajustes são
**incrementais** sobre motores já entregues (recorrência/dunning, recibos, dimensões, CRM,
transparência, conciliação, snapshots de demonstrações).

> Correção de rota registrada: a primeira leitura tratou a governança como "sobre-engenharia" e temeu um
> _dead-end_ de organização unipessoal. Com a confirmação de que **há sempre equipe + conselho**, a
> governança vira o núcleo do valor; o risco real migrou para **login compartilhado** e **vocabulário de
> papéis** (ver §5 e §6).

## 4. Decisões de produto/arquitetura

| # | Decisão | Racional |
| --- | --- | --- |
| **D-01** | **Login por usuário, com perfil (papel) indicado.** Sem login compartilhado. | Habilita segregação de funções real e navegação por papel. Já suportado (`AccessClaims.role`, `TeamService`, `FinanceRoles`). |
| **D-02** | **Remapear papéis e alçadas para a governança de conselho.** Coordenador / conselheiro responsável / moderador-presidente (aprovação alta) / conselho fiscal (somente-leitura) / contador (somente-leitura). | O vocabulário atual (`treasurer/manager/fiscal_council`) é corporativo e não casa com a estrutura eclesial/associativa; alçada mal-mapeada trava o pagamento. |
| **D-03** | **Progressive disclosure por PAPEL, não por "modo simples".** Cada membro vê só o seu recorte. | Reusa o RBAC pronto e elimina a sobrecarga de ~20 itens de menu sem esconder governança. |
| **D-04** | **IA reorganizada em duas espinhas: Entradas e Saídas.** Checkout e portais são **ferramentas/portas**, não itens de menu. | Espelha o modelo mental do coordenador/conselho ("o que entrou, o que saiu, com que finalidade e aprovação"). |
| **D-05** | **Registro de entradas agnóstico a canal e a origem.** Toda entrada nasce igual (receita + dimensão + visível), venha do checkout (PIX/boleto/cartão), do **caixa físico** (espécie), de **lançamento manual** (transferência) ou de um **portal** (Fidellis ou Formattio). | Hoje só o checkout produz entrada "de primeira classe"; caixa e manual ficam de fora (ver §5). |
| **D-06** | **Modelo de entrada por atributos ortogonais:** `tipo` × `ator` × `frequência` × `campanha` × `dimensões`. | Substitui a inferência frágil "recorrente ⇒ dízimo". Detalhe na §4.1. |
| **D-07** | **Doação recorrente para o apoiador (não-membro).** Mesmo motor de recorrência/dunning; rótulo/recibo próprios. | `RF-FIN-182` (apoiador→recorrente) e `Donor.ConvertedAt` já anteciparam. |
| **D-08** | **Campanhas como funcionalidade dedicada,** mas modeladas como **finalidade/earmark** sobre a entrada (não um 4º tipo). Janela finita **ou** aberta, meta × arrecadado, página pública, vínculo opcional a **fundo restrito/projeto**. | Campanha de fim específico = recurso com restrição (ITG 2002); habilita prestação de contas por campanha. |
| **D-09** | **Convênios/acordos (MROSC) e NF/faturamento vão para "Gestão avançada"** (ativável). Provável aposentadoria de "Contas a receber" do núcleo (colapsa em "entradas previstas"). | São pontos fora da curva do público-base; só uma minoria opera. |
| **D-10** | **Integração Formattio↔Fidellis:** Fidellis **não depende** do Formattio. Integração é **adaptador opcional, de mão única (importação)**, com **identidade federada** (id estável do membro + origem). **Auditar o cadastro/estrutura de dados do Formattio é o 1º passo** do planejamento. | Reduz incerteza entre plataformas; a auditoria também melhora o modelo **nativo** de membro (útil ao Fidellis-only). |
| **D-11** | **ADR de LGPD para o elo Formattio↔Fidellis** antes de codar a integração. | Ligar dados de formação a dados de dízimo cruza categorias sensíveis (convicção religiosa + comportamento financeiro), mesmo dentro da Kairós. |

### 4.1 O modelo de entrada consolidado (D-06)

| Atributo | Valores | Regra |
| --- | --- | --- |
| **Tipo** | `dízimo` \| `oferta` \| `doação` | Primeira classe (`entry_type` na transação). Rótulo customizável por tenant. |
| **Ator** | `membro` \| `não-membro` | **Gating no portal:** dízimo/oferta só para **membro** autenticado; doação para **qualquer** pessoa (pública/anônima). |
| **Frequência** | `pontual` \| `recorrente` | **Dízimo:** recorrente (padrão mensal). **Oferta:** pontual (extra do membro). **Doação:** pontual **ou** recorrente. |
| **Campanha** | opcional | Earmark de finalidade; ligável a fundo restrito/projeto. |
| **Dimensões** | centro de custo × projeto × fundo | Já obrigatórias (default aplicado — `RF-FIN-143`). |

**Definições de negócio (fonte: PO):**
- **Doação** — feita por quem **não** é da comunidade; pública ou anônima; pontual ou recorrente.
- **Dízimo** — **só membro**; recorrente (espera-se mensal de todos); **nome customizável** por comunidade.
- **Oferta** — **só membro**; pontual; valor **a mais** sobre o dízimo.

## 5. Achados concretos no código (lacunas a corrigir)

| Achado | Onde | Impacto |
| --- | --- | --- |
| **Coleta em espécie não vira entrada gerida** — só cria `TreasuryMovement`, sem receita/dimensão/tipo. | `CashSessionService.CloseAsync` | 🔴 A maior fonte de muitas casas (oferta do culto) fica invisível na gestão. Bloqueia D-05. |
| **Não há lançamento manual de entrada** (recebimento fora do PSP). | — | 🔴 O checkout é a única porta que gera entrada de primeira classe. Bloqueia D-05. |
| **`entry_type` não é de primeira classe** — tipo inferido de (recorrente?) × (tipo de doador). | `Donation`, `FinanceSettings` | 🟠 Mistura **oferta** (membro) com **doação** (não-membro); impede quebra correta. Base do D-06. |
| **Recorrência amarrada a "Dízimo".** | `FinanceSettings.RecurringLabel` | 🟠 Impede rótulo próprio da **doação recorrente** (D-07). |
| **`Campaign` mínima** — sem janela de tempo, sem progresso × meta, sem página dedicada. | `Campaign` (`Title/Slug/GoalAmount/Status`) | 🟠 Base do D-08 a completar. |
| **`Donor` sem elo de identidade externa** (`ExternalId`/`Source`). | `Donor` | 🟠 Necessário para D-10 sem duplicar cadastro. |
| **Vocabulário de papéis corporativo** + alçada default `manager+fiscal_council`. | `FinanceRoles`, `FinanceConfigSeeder` | 🟠 Base do D-02; conselho fiscal em geral **fiscaliza**, não **autoriza**. |
| **Autoaprovação bloqueada sem estado de bootstrap** — antes de a equipe ser convidada, o admin não aprova o próprio título. | `ApprovalService.ApproveAsync` | 🟡 Mitigado por D-01 (equipe cadastrada no onboarding); prever aviso auditável na fase inicial. |

## 6. Riscos remanescentes

1. **Login compartilhado** anula a segregação e reabre o _dead-end_ de aprovação — mitigado por **D-01**
   (onboarding cadastra a equipe e o conselho individualmente).
2. **Vocabulário de papéis** desalinhado da governança real dispara guarda-corpos no lugar errado —
   mitigado por **D-02**.
3. **Dependência acidental do Formattio** — mitigado por **D-10** (adaptador opcional, Fidellis autossuficiente).
4. **LGPD do elo entre produtos** — mitigado por **D-11**.

## 7. Recomendações priorizadas (próximos passos)

1. **Corrigir o privilégio do checkout (D-05):** caixa físico passa a **gerar receita/entrada**; criar
   **lançamento manual de entrada**. _(desbloqueia a espinha de Entradas)_
2. **Tornar `entry_type` de primeira classe + eixo ator/frequência (D-06/D-07)** e separar **doação**
   (não-membro) de **oferta** (membro); desacoplar recorrência do rótulo "dízimo".
3. **Reorganizar a IA em Entradas/Saídas e a navegação por papel (D-03/D-04).**
4. **Remapear papéis e alçadas para o conselho (D-02)** + **onboarding que cadastra a equipe (D-01).**
5. **Completar Campanhas (D-08):** janela, meta × arrecadado, página pública, vínculo a fundo restrito.
6. **Mover convênios/acordos e NF para gestão avançada (D-09).**
7. **Futuro (não executar agora, mas não fechar portas):** elo de **identidade federada** no membro/doador,
   **auditoria do cadastro Formattio** e **ADR de LGPD** (D-10/D-11).

### Valor de alto impacto destravado
- **Adimplência do dízimo por membro** (quem está em dia / atrasado) reusando recorrência + dunning + CRM
  já prontos — forte para gestão e acompanhamento pastoral.
- **Prestação de contas por campanha** (arrecadado × aplicado) quando a campanha se liga a fundo restrito —
  transparência que o segmento valoriza.

## 8. Jornada de adoção — equipe voluntária + conselho

> **Foco desta rodada (a pedido do PO): o público-alvo pela ótica da adoção.** Quem cadastra, quem
> aprova, a curva de aprendizado e os pontos de abandono. O veredito do §3 é favorável *desde que a
> instituição entre no sistema já como equipe + conselho* — mas hoje **a porta de entrada é
> unipessoal**, o que reabre exatamente o _dead-end_ que a governança pressupõe não existir. A adoção,
> não a funcionalidade, é o maior risco remanescente.

### 8.1 Mapa da jornada (esperado × estado atual)

| Etapa | Quem age | O que o sistema faz hoje | Atrito |
| --- | --- | --- | --- |
| **1. Cadastro / 1º acesso** | Coordenador (ou conselheiro responsável) | `OnboardingService.signup` cria **um** usuário-admin + tenant + organização-raiz e faz auto-login. | 🔴 Entra **uma pessoa só**. Nada convida a equipe nem registra o conselho — o oposto da premissa do §2. |
| **2. Formação da equipe** | Coordenador convida tesoureiro, conselheiros, conselho fiscal, contador | **Não existe fluxo de convite.** `TeamService.SetRoleAsync` só troca o papel de quem **já** é `membership`. | 🔴 Não há caminho de produto para a equipe entrar; a governança fica no papel. Bloqueia **D-01**. |
| **3. Configurar governança** | Conselheiro responsável | `FinanceConfigSeeder` semeia alçadas default com vocabulário `treasurer/manager/fiscal_council` e põe **conselho fiscal como aprovador** na faixa alta. | 🟠 Vocabulário corporativo na 1ª tela + fiscal "autorizando" (deveria fiscalizar). Base de **D-02/D-03**. |
| **4. Primeiro recebimento** | Tesoureiro / secretaria | Só o **checkout PSP** (Pagar.me) gera entrada de 1ª classe. Caixa físico do culto vira `TreasuryMovement` sem receita. | 🟠 A "primeira vitória" (ver dinheiro entrar) depende de integrar PSP/portal; quem vive de caixa não ativa. Ligado a **D-05**. |
| **5. Primeiro pagamento** | Tesoureiro lança → conselheiro/moderador aprova | `ApprovalService`: autoaprovação bloqueada + faixa 500–5000 exige **2 assinaturas**. | 🔴 Com equipe ainda não cadastrada (etapa 2 falha), **ninguém consegue aprovar** — a espinha de Saídas trava no 1º uso. |
| **6. Rotina / prestação de contas** | Conselho fiscal (leitura), contador (rascunho→assina), moderador | Motores prontos: recorrência/dunning, recibos, dimensões, snapshots, transparência. | 🟢 Aqui o produto brilha — **se** a instituição chegou até aqui com a equipe montada. |

### 8.2 O funil de ativação e o ponto de queda

A jornada é uma corrente: **o valor do §3 só se realiza na etapa 6, mas ela morre nas etapas 2 e 5.**
A instituição que se cadastra sozinha (etapa 1) esbarra na ausência de convite (etapa 2) e, ao tentar o
primeiro pagamento (etapa 5), é bloqueada pela segregação de funções — **exatamente a proteção que é o
núcleo do valor** vira o motivo do abandono quando aplicada cedo demais, antes da equipe existir. O risco
não é "funcionalidade a menos"; é **ordem errada de eventos no onboarding**.

### 8.3 Atritos concretos no código (adoção)

| Achado | Onde | Impacto na adoção |
| --- | --- | --- |
| **Signup unipessoal** — cria só o admin; sem passo de equipe/conselho. | `onboarding.service.ts` (`signup`) | 🔴 Porta de entrada contradiz a premissa do público. Pré-requisito de **D-01**. |
| **Sem convite de membro** — só ajusta papel de `membership` existente. | `TeamService.SetRoleAsync` | 🔴 Não há como a equipe entrar pelo produto. Bloqueia **D-01**. |
| **Bootstrap trava o 1º pagamento** — autoaprovação bloqueada + 2 assinaturas na faixa média. | `ApprovalService.ApproveAsync`, `FinanceConfigSeeder` | 🔴 Sem 2ª pessoa cadastrada, nenhuma saída é aprovável. (§5 marcou 🟡 como compliance; **na jornada é 🔴 de ativação**.) |
| **Conselho fiscal como aprovador default** na faixa >R$5.000. | `FinanceConfigSeeder` (`RolesCsv`) | 🟠 Se ninguém tem o papel, títulos altos ficam impagáveis; e confunde fiscalizar × autorizar. **D-02**. |
| **Vocabulário corporativo na 1ª experiência.** | `FinanceRoles`, labels | 🟠 Curva de aprendizado + desconfiança ("não é pra nós"). **D-02/D-03**. |
| **Primeira entrada exige PSP.** | `CashSessionService.CloseAsync` (só `TreasuryMovement`) | 🟠 Sem "vitória rápida" para quem vive de caixa físico. **D-05**. |

### 8.4 Recomendações da jornada (priorizadas)

1. **Onboarding em duas etapas: instituição → equipe & conselho.** Após criar o tenant, um passo
   **obrigatório-mas-adiável** convida (por e-mail/link) tesoureiro, conselheiros, conselho fiscal e
   contador, já atribuindo papéis. Concretiza **D-01** e destrava etapas 2 e 5. _(maior alavanca de adoção)_
2. **Fluxo de convite de membro** (criar usuário + `membership` + papel de uma vez), não só troca de
   papel de quem já existe. Pré-requisito técnico da recomendação 1.
3. **Estado de bootstrap auditável:** enquanto a equipe não estiver montada, permitir que o admin conclua
   um pagamento com **aviso registrado na trilha** (autoaprovação excepcional), em vez de travar — some o
   aviso assim que o 2º aprovador entrar. Remove o beco-sem-saída do 1º pagamento sem afrouxar o compliance no regime normal.
4. **Alçadas default no vocabulário de conselho (D-02):** conselho fiscal **somente-leitura**; aprovação
   alta com moderador/presidente. Corrige etapa 3 e o default que trava títulos altos.
5. **Vitória rápida na etapa 4:** caixa físico e lançamento manual passam a **gerar entrada de 1ª classe**
   (**D-05**), para a casa "ver dinheiro entrar" sem depender de integrar o PSP no primeiro dia.
6. **Navegação por papel desde o 1º login (D-03):** cada perfil vê só o seu recorte, encurtando a curva
   de aprendizado da equipe voluntária.

> **Métrica de sucesso da adoção sugerida:** *time-to-first-approved-payment* com ≥ 2 pessoas distintas
> (lançador ≠ aprovador). Ela captura, num único número, se a instituição de fato entrou como **equipe +
> conselho** — a hipótese central de todo este parecer.

## 9. Fora de escopo (ou futuro)

NF-e/faturamento, convênios/acordos (MROSC) no núcleo, endowment/fundo patrimonial ativo, multi-moeda,
integração Formattio em produção (depende de auditoria + ADR de LGPD).

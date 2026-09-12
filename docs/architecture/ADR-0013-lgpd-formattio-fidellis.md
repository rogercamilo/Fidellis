# ADR-0013 — LGPD do elo Formattio ↔ Fidellis (identidade federada + dados sensíveis)

- **Status:** Proposto (2026-09-12) — **decisão antes de codar** (decisão **D-11** do parecer). A
  integração **D-10** fica **bloqueada** até este ADR ser Aceito e o RIPD (§ Consequências) concluído.
- **Contexto:** [parecer do terceiro setor](../prd/parecer-finance-terceiro-setor.md) (D-10/D-11) e o
  [plano de auditoria do Formattio](../prd/d10-formattio-audit-plan.md), que é o **1º passo** e alimenta este ADR.
- **Relaciona-se a:** [ADR-0004](ADR-0004-standalone-auth.md) (auth standalone),
  [ADR-0008](ADR-0008-user-org-membership.md) (identidade/membership),
  [ADR-0012](ADR-0012-donor-portal-audit-lgpd.md) (auditoria + direitos LGPD já implementados).

## Contexto

A **D-10** propõe integrar o **Formattio** (plataforma de formação da mesma casa, Kairós) ao Fidellis como
**adaptador opcional, de mão única (importação)**, com **identidade federada** do membro. O risco de
proteção de dados é o ponto central: ligar **dados de formação religiosa** a **comportamento financeiro**
(dízimo/oferta por membro — habilitado pela D-06) **cruza categorias**:

- **Convicção religiosa é dado pessoal sensível** (LGPD, art. 11) — o simples fato de ser "membro" de uma
  comunidade religiosa e participar de formação já revela convicção.
- **Histórico financeiro (dízimo/oferta)** é dado comportamental que, associado à convicção, forma um
  perfil de alta sensibilidade.

Mesmo sob a **mesma controladora**, combinar as duas bases **eleva o risco** e muda a finalidade original
da coleta (formação ≠ arrecadação). Não há base legal automática para o cruzamento. **Este ADR define as
salvaguardas obrigatórias antes de qualquer linha de código de integração.**

**Proposta refinada (2026-09-12).** A integração tem **dois fluxos**: (A) o cadastro do formando alimenta
o Fidellis (identidade federada); e (B) o **portal do formando (no Formattio)** ganha uma aba para
**dízimo/oferta**. Regra de negócio confirmada pelo PO: **existir vínculo de integração estabelecido ⇒ o
formando é membro**, e o Fidellis reconhece essa condição como verdadeira. Isso introduz um risco além do
armazenamento: ao surfacar a arrecadação **dentro do portal de formação**, o cruzamento sensível
(formação + finanças) acontece **no ponto de experiência**, mesmo com os dados fisicamente separados.

## Decisão (proposta)

1. **Finalidade específica e separada.** Dados vindos do Formattio só podem ser usados para a finalidade
   declarada da integração (identificar o membro para o gating de dízimo/oferta — D-06 — e evitar cadastro
   duplicado). **Proibido** reusar dados de formação para segmentação/marketing financeiro sem base legal
   própria e consentimento específico.
2. **Minimização por identidade federada.** Importar **o mínimo**: um **id estável do membro** + **origem**
   (`source`), e no máximo os campos estritamente necessários à correspondência (ex.: nome, e-mail/doc já
   coletados pelo Fidellis). **Não** copiar histórico/conteúdo de formação, sacramentos ou quaisquer
   categorias sensíveis para o schema do Fidellis. O elo é uma **referência** (id + origem), não uma cópia.
3. **Mão única e opcional.** Só **importação** (Formattio → Fidellis); o Fidellis **não depende** do
   Formattio (ADR alinhado à D-10). Adaptador desligável por tenant; sem ele o produto opera normalmente.
4. **Base legal explícita antes do cruzamento.** Enquanto jurídico não definir a base legal do
   **cruzamento** (convicção + finanças) — consentimento específico do titular **ou** hipótese do art. 11
   com registro fundamentado — **o cruzamento não ocorre**. O gating "membro × não-membro" (D-06) usa
   apenas o **vínculo de membership** (dado necessário à relação), não o conteúdo de formação.
5. **Segregação e não-vazamento.** Dados sensíveis do elo **não** entram em relatórios, exports contábeis,
   transparência pública nem régua de comunicação. Pseudonimização do id externo; acesso restrito por papel.
6. **Direitos do titular e retenção.** Export/erasure/opt-out seguem o já implementado no ADR-0012; a
   erasure do membro **remove o elo federado** (mantendo o financeiro por obrigação legal, como já decidido).
   Retenção do vínculo alinhada à finalidade; revisão periódica.
7. **Auditoria do elo.** Toda importação/associação/consulta que toque o elo Formattio vai ao `audit_log`
   (reusa `IAuditLog`), com ator e origem.
8. **Membership derivada da origem federada (regra "integrado ⇒ membro").** Um doador com
   `Source = formattio` **é** membro por definição — a origem é a prova, sem importar nem inferir um
   "flag de membro". Essa verdade é **declarada no nível do tenant-integração** ("formandos deste
   Formattio = membros deste Fidellis"), de forma **explícita e auditável** (config da integração, não um
   `if` escondido); cada doador federado a herda. Isso **reforça a minimização** (basta `id + origem`).
9. **Canal da integração como emissor de dízimo/oferta; "Formattio lança, não armazena".** A arrecadação
   pela aba do portal usa uma **credencial de serviço por tenant** (server-to-server) + o **id federado**
   do membro; esse canal **autenticado** é autorizado a criar `dízimo`/`oferta` (contexto de membro) — ao
   contrário do canal público anônimo, que segue restrito a `doação` (D-06). O Formattio **abre/embute** o
   fluxo do Fidellis e **não armazena** histórico financeiro do formando ao lado da formação, para o
   cruzamento sensível **não se materializar** no portal.

## Alternativas consideradas

- **Não integrar (status quo).** Mais seguro; perde a conveniência de reconhecer o membro. Aceitável como
  fallback permanente — o Fidellis é autossuficiente.
- **Integração total (copiar o cadastro de formação).** Rejeitada: cria uma base combinada de alto risco,
  amplia a superfície de vazamento e a finalidade, e provavelmente exige consentimento que não temos.
- **Identidade federada (id + origem), sem copiar sensíveis.** **Escolhida** — habilita a correspondência
  do membro com o menor volume de dados e sem materializar o cruzamento sensível.

## Consequências

- **Positivas:** integração desenhada por padrão para **minimização** e **finalidade específica**; risco
  de cruzamento sensível contido; Fidellis segue autossuficiente; trilha de auditoria do elo.
- **Negativas / trade-offs:** a integração fica **bloqueada** até (a) este ADR ser **Aceito**, (b) o
  **RIPD/DPIA** (Relatório de Impacto à Proteção de Dados) concluído — obrigatório pelo alto risco de
  tratamento de dado sensível em larga escala, (c) **parecer jurídico** definindo a base legal do
  cruzamento e o texto de consentimento (se aplicável), e (d) a **auditoria do cadastro Formattio**
  concluída (doc D-10). Nenhum código de importação deve ser mergeado antes disso.
- **Pré-condições de implementação (checklist):** RIPD assinado · base legal definida · DPO/encarregado
  ciente · minimização revisada contra o dicionário de dados (D-10) · plano de retenção/erasure do elo ·
  eventos de auditoria mapeados · **contrato de auth server-to-server (credencial por tenant) definido** ·
  regra de offboarding do vínculo definida (✔ ver abaixo — pausa/encerra com aviso).
- **Progresso (2026-09-12):** auditoria do Formattio **concluída** (esquema + código — ver
  [planilha](../prd/d10-formattio-audit-worksheet.md)) e **RIPD rascunhado**
  ([ripd-formattio-fidellis.md](ripd-formattio-fidellis.md)). Falta validação **jurídica/DPO** do RIPD +
  base legal do cruzamento → então este ADR passa a **Aceito**. Para acelerar a etapa jurídica, há
  **rascunhos/modelos** do [termo de consentimento](../legal/consentimento-integracao-formattio.md) e do
  [acordo de compartilhamento](../legal/acordo-compartilhamento-formattio-fidellis.md) — pendentes de
  revisão jurídica, não são pareceres.

## Ciclo de vida do vínculo (offboarding) — decidido

Regra do PO (2026-09-12): ao **perder o vínculo** (formando conclui/desiste), a **recorrência de dízimo
pausa/encerra, com aviso** ao membro. Implementação:

- O Formattio **sinaliza** o offboarding ao Fidellis (evento/webhook/status — a confirmar na auditoria D-10).
- Ao receber o sinal, o Fidellis **pausa** a recorrência (status `paused`) e **notifica** o membro (régua/
  outbox) — reversível se a pessoa retornar. **Encerra** (`canceled`) quando o desligamento for definitivo.
- O vínculo federado (`ExternalId`/`Source`) segue as regras de retenção/erasure do elo; a pausa não apaga
  o financeiro já realizado (obrigação de prestação de contas).

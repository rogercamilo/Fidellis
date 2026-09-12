# Minuta de Parecer Jurídico — Base legal do cruzamento sensível (Formattio ↔ Fidellis)

> ⚠️ **MINUTA / MODELO — NÃO É PARECER JURÍDICO EMITIDO POR ADVOGADO.** Redigido pela engenharia (CTO)
> para **estruturar e acelerar** a análise que hoje bloqueia o [#80], satisfazendo a pendência do
> [RIPD §7](../architecture/ripd-formattio-fidellis.md) e do [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md).
> **Deve ser revisada, complementada e ASSINADA por advogado(a) habilitado(a) e pelo Encarregado/DPO
> antes de qualquer uso.** Não substitui aconselhamento jurídico. **Versão:** 0.1 (2026-09-12).

---

## 1. Consulta

Indaga-se: **há base legal** para o Fidellis **cruzar** a condição de **membro de comunidade religiosa**
(dado que revela **convicção religiosa** — sensível) com o **comportamento financeiro** (dízimo/oferta), a
partir da integração de identidade com o Formattio? Em caso positivo, **sob quais condições** o tratamento
é lícito?

## 2. Fatos relevantes (do RIPD e da auditoria D-10)

- Integração **de mão única** Formattio → Fidellis, com **dois fluxos**: (A) identidade federada do
  membro (`ExternalId + Source=formattio + email + nome`); (B) aba de **dízimo/oferta** no portal do
  formando, que aciona o Fidellis server-to-server.
- Regra de negócio: **"integração estabelecida ⇒ o formando é membro"** — a origem federada é a prova da
  condição de membro.
- **Nada** de formação/vocação/sacramentos é copiado ao Fidellis (minimização confirmada em auditoria).
- A sensibilidade decorre da **combinação**: "ser membro (convicção)" + "quanto/quando contribui".
- Ambas as plataformas são da **Kairós**; a **instituição contratante** figura como **controladora** e as
  plataformas como **operadores** (enquadramento a confirmar neste parecer).

## 3. Questões jurídicas

1. Trata-se de **dado pessoal sensível**? (LGPD art. 5º, II; art. 11)
2. Qual **hipótese legal** autoriza o tratamento do cruzamento? As hipóteses **sem consentimento** (art.
   11, II) são suficientes, ou é necessário **consentimento específico e destacado** (art. 11, I)?
3. Requisitos de **validade** do consentimento (art. 8º) e do consentimento **para dado sensível** (art. 11, I).
4. **Finalidade** e **mudança de finalidade** (a coleta original no Formattio era para *formação*, não
   *arrecadação*) — art. 6º, I, e adequação/necessidade.
5. **Papéis** (controlador × operador) e **compartilhamento** entre operadores da mesma casa (art. 5º; art. 39).
6. **Deveres acessórios:** RIPD (art. 38), segurança (art. 46-49), direitos do titular (art. 18), incidentes.

## 4. Análise

### 4.1 Natureza do dado — sensível

A LGPD (art. 5º, II) qualifica como **sensível** o dado que revele **convicção religiosa**. A condição de
**membro/formando** de comunidade religiosa **revela** essa convicção. Logo, mesmo sem importar conteúdo de
formação, o **vínculo** tratado no elo é **dado sensível**, e seu tratamento se sujeita ao **art. 11**.

### 4.2 Escolha da hipótese legal

As hipóteses do **art. 11, II** (sem consentimento) foram examinadas e, em regra, **não amparam** o
cruzamento aqui:

- **"a" (obrigação legal/regulatória):** dizimar/ofertar é ato **voluntário**; não há obrigação legal que
  imponha o cruzamento. **Não se aplica.**
- **"d" (exercício regular de direitos):** não se trata de litígio/contrato que exija o cruzamento.
  **Não se aplica** ao cruzamento (embora a **guarda do financeiro já realizado** se ampare em obrigação
  legal de prestação de contas — ver §4.6).
- **"b", "c", "e", "f", "g"** (políticas públicas, pesquisa, vida, saúde, prevenção a fraude): **alheias**
  à finalidade de arrecadação religiosa. **Não se aplicam.**

Observa-se que **entidades religiosas** podem tratar dados sensíveis de seus membros **para as suas
próprias finalidades religiosas** — porém o presente caso envolve (i) **mudança/ampliação de finalidade**
(de *formação* para *reconhecimento do membro em plataforma de arrecadação*) e (ii) **compartilhamento
entre plataformas distintas**, surfaçando o cruzamento **no ponto de experiência** (portal). Por
prudência e por ser o cruzamento **não indispensável** a nenhuma das hipóteses do art. 11, II, a base
legal **adequada e mais segura** é o **consentimento específico e destacado** do titular
(**art. 11, I**). *(O(a) advogado(a) deve confirmar se, no caso concreto da instituição, alguma finalidade
religiosa própria autorizaria dispensa parcial — a recomendação conservadora é NÃO depender disso.)*

### 4.3 Requisitos de validade do consentimento

O consentimento deve ser (art. 8º e art. 11, I):

- **Livre** — sem condicionar o uso do Formattio ou a doação pública à autorização (o [termo](consentimento-integracao-formattio.md) já prevê recusa sem prejuízo);
- **Informado** — linguagem clara sobre dados, finalidade, compartilhamento, direitos e revogação;
- **Inequívoco** — manifestação afirmativa (opt-in), vedado consentimento tácito/pré-marcado;
- **Específico e destacado** (exigência reforçada para dado sensível) — para **finalidade determinada**,
  apartado de outros termos, com destaque à natureza sensível;
- **Comprovável** — ônus do controlador (art. 8º, §2º): registrar aceite + versão + timestamp
  (reuso do `PrivacyAcceptance`);
- **Revogável a qualquer tempo**, por procedimento simples e gratuito (art. 8º, §5º).

O termo minutado atende a esses requisitos; **requer revisão** de redação jurídica.

### 4.4 Finalidade, adequação e necessidade (art. 6º)

- **Finalidade** legítima, específica e informada: **reconhecer o membro** para habilitar dízimo/oferta
  (D-06) e evitar duplicidade. **Vedado** reuso (marketing/segmentação) sem nova base/consentimento.
- **Adequação/necessidade:** minimização confirmada (`ExternalId + origem + email + nome`); toda formação
  fica fora. A **vinculação pelo `ExternalId`** (e não por e-mail, que **não é único** no Formattio)
  reduz risco de **match incorreto** — recomenda-se **não** fazer lookup por e-mail para o give autenticado.

### 4.5 Papéis e compartilhamento

Recomenda-se enquadrar a **instituição contratante como controladora** e **Formattio/Fidellis como
operadores** (art. 5º, VI-VII), formalizando o **acordo de compartilhamento** ([minuta](acordo-compartilhamento-formattio-fidellis.md))
com escopo fechado, finalidade única, segurança e vedação de reuso. O compartilhamento **mão única** e
**mínimo** é proporcional. *(Confirmar se, no arranjo Kairós, alguma plataforma atua como controladora
conjunta — art. 5º, IX — o que exigiria ajuste do instrumento.)*

### 4.6 Deveres acessórios

- **RIPD/DPIA** (art. 38): elaborado ([rascunho](../architecture/ripd-formattio-fidellis.md)); recomenda-se
  a ANPD poder requisitá-lo — manter atualizado e assinado.
- **Segurança** (art. 46-49): server-to-server, credencial por tenant, TLS, segregação, auditoria — ver acordo §5/§8.
- **Direitos do titular** (art. 18): acesso, correção, portabilidade, eliminação, **revogação** — já
  suportados (ADR-0012 no Fidellis; tooling LGPD no Formattio). A **revogação/erasure** desfaz o elo e
  **pausa/encerra** a recorrência (offboarding).
- **Guarda do financeiro já realizado:** mantida por **obrigação legal de prestação de contas** (base
  própria, art. 7º/art. 16), **sem novo cruzamento** — a erasure remove o elo, não o histórico obrigatório.
- **Menores/incapazes:** se houver formandos menores, o consentimento observa o art. 14 (melhor interesse,
  consentimento de responsável) — **verificar** a base de formandos.

## 5. Condicionantes (para licitude)

1. **Consentimento específico e destacado** coletado **antes** de habilitar a aba de dízimo/oferta; sem
   ele, o portal opera **apenas com doação** (não-membro), sem cruzamento.
2. **Trava técnica:** o give autenticado de dízimo/oferta só é aceito com **elo + consentimento ativo**.
3. **Minimização e segregação** auditadas: dados do elo fora de relatórios/export/transparência/régua.
4. **Acordo de compartilhamento** assinado; papéis definidos; **RIPD** assinado; **DPO** ciente.
5. **Vedação de reuso**; revisão periódica de finalidade e retenção.
6. Verificação de **menores** e, se houver, adequação ao art. 14.

## 6. Conclusão (minuta)

O cruzamento é **juridicamente viável** desde que fundado em **consentimento específico e destacado
(art. 11, I)**, observados os princípios do art. 6º e as condicionantes da §5. **Não** é recomendável
apoiar o cruzamento em hipóteses do art. 11, II. Atendidas as condicionantes, o **ADR-0013** pode passar a
**Aceito** e a implementação técnica ser liberada. **Persistindo dúvida sobre a base legal, a alternativa
conservadora é não integrar** (o Fidellis é autossuficiente) **ou** operar o portal **apenas com doação**.

> **Este documento é uma MINUTA técnica.** A emissão do parecer é do(a) advogado(a) habilitado(a), a quem
> cabe confirmar hipótese legal, redação do consentimento, enquadramento de papéis e verificação de menores.

## 7. Referências

[RIPD/DPIA](../architecture/ripd-formattio-fidellis.md) · [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md)
· [Auditoria D-10](../prd/d10-formattio-audit-worksheet.md) · [Termo de consentimento](consentimento-integracao-formattio.md)
· [Acordo de compartilhamento](acordo-compartilhamento-formattio-fidellis.md) · LGPD (Lei 13.709/2018),
arts. 5º, 6º, 7º, 8º, 11, 14, 16, 18, 38, 39, 46-49.

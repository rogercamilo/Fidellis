# Termo de Consentimento — Integração Formattio ↔ Fidellis (dízimo/oferta do membro)

> ⚠️ **RASCUNHO / MODELO — NÃO É PARECER JURÍDICO.** Redigido pela engenharia (CTO) para **acelerar a
> etapa jurídica** e satisfazer a pendência do [RIPD](../architecture/ripd-formattio-fidellis.md) e do
> [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md). **Revisão obrigatória por
> jurídico/DPO antes de qualquer uso.** Campos entre `[ ]` são preenchidos na configuração da integração.
> **Versão:** 0.1 (2026-09-12).

---

## Por que este consentimento existe (contexto — não faz parte do texto ao titular)

O cruzamento **"ser membro de comunidade religiosa" (convicção — dado sensível, LGPD art. 11) × "quanto/quando
contribui" (comportamento financeiro)** não tem base legal automática, mesmo sob a mesma controladora
(Kairós). A hipótese de trabalho do RIPD é **consentimento específico e destacado (art. 11, I)**, coletado
**no portal do formando** antes de habilitar a aba de dízimo/oferta. Enquanto o titular não consentir, o
cruzamento **não ocorre** e o portal só oferece **doação** (fluxo não-membro, D-06).

---

## Texto ao titular (a exibir no portal do formando, antes de ativar a aba de dízimo/oferta)

### Autorização para vincular sua identidade de membro à sua contribuição

A **[NOME DA INSTITUIÇÃO CONTRATANTE]** utiliza duas plataformas da **Kairós**: o **Formattio** (sua
formação/acompanhamento) e o **Fidellis** (dízimos, ofertas e prestação de contas). Para que você possa
**dizimar ou ofertar diretamente pelo portal**, precisamos **vincular sua identidade de membro** (mantida
no Formattio) à sua contribuição (registrada no Fidellis).

**O que será compartilhado entre as plataformas** (o mínimo necessário):

- um **identificador interno** seu (código estável — não é seu CPF nem seu RG);
- a **origem** do vínculo (que você é membro/formando desta instituição);
- seu **e-mail** e **nome**, apenas para exibição e conferência.

**O que NÃO será compartilhado:** nada da sua **formação, vocação, sacramentos, acompanhamento,
documentos, foto** ou histórico eclesiástico. Esses dados **permanecem no Formattio** e **não** vão para o
Fidellis.

**Para que serve** (finalidade específica): reconhecer você como membro para registrar **dízimo/oferta**
(que só o membro faz) e evitar cadastro duplicado. **Não** usaremos esse vínculo para marketing,
segmentação ou qualquer outra finalidade sem um novo consentimento seu.

**Dado sensível.** O fato de você ser membro de uma comunidade religiosa revela **convicção religiosa**,
que a lei trata como **dado pessoal sensível**. Por isso pedimos sua autorização **específica e
destacada** para este vínculo.

**É voluntário.** Você **não é obrigado** a autorizar. Se não autorizar:
- continua usando o **Formattio** normalmente;
- ainda pode **doar** pelo canal público (sem o vínculo de membro);
- apenas **não** verá a aba de **dízimo/oferta** de membro no portal.

**Você pode mudar de ideia a qualquer momento.** Ao **revogar**, o vínculo entre as plataformas é
desfeito e a aba de dízimo/oferta deixa de funcionar. Contribuições **já realizadas** são mantidas pelo
prazo que a lei exige (prestação de contas), sem novo cruzamento.

**Seus direitos** (LGPD): acesso, correção, portabilidade, eliminação, informação e revogação. Para
exercê-los, fale com **[CANAL DO ENCARREGADO/DPO — e-mail/telefone]**.

**Ao marcar a caixa abaixo, você declara que leu, entendeu e autoriza**, de forma **livre, informada,
específica e destacada**, o vínculo da sua identidade de membro (Formattio) à sua contribuição (Fidellis),
para a finalidade acima.

- [ ] **Autorizo** o vínculo da minha identidade de membro para dízimo/oferta pelo portal.

| Campo | Valor |
| --- | --- |
| Titular | `[NOME]` — `[E-MAIL]` |
| Instituição (controladora) | `[NOME DA INSTITUIÇÃO CONTRATANTE]` |
| Data/hora do aceite | `[TIMESTAMP]` |
| Versão do termo | `[VERSÃO]` (ex.: 0.1) |
| Registro | armazenar aceite + versão + IP/user-agent (à semelhança de `PrivacyAcceptance` no Formattio) |

---

## Notas de implementação (para engenharia — não exibir ao titular)

- **Onde coletar:** no **portal do formando** (Formattio), antes de habilitar a aba de dízimo/oferta.
- **Onde registrar:** o Formattio já tem `PrivacyAcceptance` — reusar para gravar **aceite + versão + timestamp**.
  O Fidellis pode espelhar o *flag* de "consentimento de cruzamento" no elo (não o conteúdo), para a **trava técnica**.
- **Trava técnica (RIPD §5 / ADR-0013 dec. 4 e 9):** o give autenticado de **dízimo/oferta** só é aceito
  se houver **elo (`ExternalId`) + consentimento ativo**. Sem consentimento → portal só **doação**.
- **Revogação:** desfaz o vínculo e **pausa/encerra** a recorrência (offboarding — ADR-0013), notificando o titular.
- **Versionamento:** toda alteração material do texto **incrementa a versão** e **re-coleta** o consentimento.

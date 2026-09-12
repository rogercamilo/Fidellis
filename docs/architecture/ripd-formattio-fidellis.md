# RIPD/DPIA — Elo Formattio ↔ Fidellis (integração de identidade de membro)

> **Status:** **rascunho técnico** para revisão do **Encarregado/DPO + jurídico** · 2026-09-12. Não é
> aprovação: a integração (#80) segue **bloqueada** até este RIPD ser validado e o
> [ADR-0013](ADR-0013-lgpd-formattio-fidellis.md) passar a **Aceito**.
> **Insumos:** [plano](../prd/d10-formattio-audit-plan.md) e [planilha de auditoria](../prd/d10-formattio-audit-worksheet.md) (esquema + código do Formattio).
> **Base normativa:** LGPD (Lei 13.709/2018), em especial **art. 7º/art. 11** (dado sensível) e art. 6º (princípios).

---

## 1. Identificação

| Item | Conteúdo |
| --- | --- |
| Tratamento | Integração **de mão única** Formattio → Fidellis: identidade federada de membro + aba de dízimo/oferta no portal do formando. |
| Controlador(es) | A **instituição contratante** (tenant) é controladora dos dados de seus formandos/doadores; Fidellis e Formattio atuam como **operadores** (mesma casa, Kairós). *(confirmar enquadramento com jurídico)* |
| Encarregado (DPO) | _(a designar/confirmar)_ |
| Autoria do rascunho | Engenharia (CTO) — pendente validação jurídica/DPO. |

## 2. Descrição do tratamento

Dois fluxos (ver ADR-0013):
- **A — Identidade federada (importação):** o Fidellis passa a referenciar o formando por `ExternalId`
  (`Formando.id`) + `Source=formattio`. Carga inicial via `GET /api/export/organizacao` do Formattio.
- **B — Aba de dízimo/oferta no portal do formando:** o formando, autenticado no Formattio, aciona uma
  contribuição que o Formattio encaminha ao Fidellis (server-to-server) com o `ExternalId`.

**Dados tratados no elo (mínimo):** `ExternalId`, `Source`, **e-mail**, **nome** (exibição). Frequência
de contribuição passa a existir no Fidellis (comportamento financeiro do membro).

**Categorias especiais / sensíveis (art. 11):** a condição de **membro de comunidade religiosa** e a
**formação/vocação** (níveis, sacramentos, acompanhamento) são dado **sensível**. **O elo NÃO importa**
nada de formação/vocação — apenas a referência de identidade. A sensibilidade remanescente vem da
**combinação** "é membro (convicção) + quanto/quando contribui".

## 3. Necessidade e proporcionalidade

- **Finalidade específica:** reconhecer o membro para habilitar dízimo/oferta (D-06) e evitar cadastro
  duplicado. **Proibido** reuso para outra finalidade (ex.: marketing) sem base legal própria.
- **Minimização (confirmada na auditoria):** basta `ExternalId + origem + email(+nome)`; toda formação
  fica fora. O vínculo autoritativo é o `ExternalId` (o e-mail **não é único** no Formattio e **não** é usado como chave).
- **Base legal:** a definir pelo jurídico. Para o **cruzamento sensível** (convicção + finanças), a
  hipótese provável é **consentimento específico e destacado** (art. 11, I) do titular, coletado no
  portal; enquanto não houver, **o cruzamento não ocorre** e o portal só oferece doação (não-membro).
- **Retenção/eliminação:** o elo segue as regras de retenção/erasure do [ADR-0012](ADR-0012-donor-portal-audit-lgpd.md);
  a erasure remove o elo (mantém o financeiro por obrigação legal). Offboarding (`ativo`/`deletedAt`)
  pausa/encerra a recorrência.

## 4. Riscos aos titulares

| # | Risco | Prob. | Impacto | Nível |
| --- | --- | :-: | :-: | :-: |
| R1 | **Perfilamento sensível** (convicção religiosa × comportamento financeiro) formado pela combinação das bases | Média | Alto | **Alto** |
| R2 | **Vazamento/uso indevido** do elo ou de dados financeiros do membro | Baixa | Alto | Médio |
| R3 | **Match incorreto** (e-mail não único, sem CPF) associando contribuição à pessoa errada | Média | Médio | Médio |
| R4 | **Desvio de finalidade** (usar dados de formação/contribuição para fins não consentidos) | Média | Alto | **Alto** |
| R5 | **Falta de base legal** para o cruzamento sensível | Média | Alto | **Alto** |
| R6 | **Materialização do cruzamento no portal de formação** (financeiro exibido no contexto de formação) | Média | Médio | Médio |

## 5. Medidas de mitigação

| Risco | Medida |
| --- | --- |
| R1/R4 | **Minimização** (só `ExternalId+origem+email(+nome)`); finalidade específica declarada; **proibição** de reuso; segregação — dados sensíveis **não** entram em relatórios/export/transparência/régua. |
| R5 | **Base legal antes de qualquer cruzamento** (provável **consentimento específico** no portal); sem ela, portal só doação. Trava técnica: o give de dízimo/oferta exige o elo + consentimento. |
| R6 | **"Formattio lança, não armazena"**: a aba **abre/encaminha** o fluxo do Fidellis; o Formattio **não** guarda histórico financeiro ao lado da formação. |
| R3 | Vínculo pelo **`ExternalId`** (não por e-mail); import idempotente por `ExternalId`; reconciliação de e-mail duplicado tratada manualmente/à parte. |
| R2 | **Server-to-server** com credencial de serviço **por tenant**; TLS; token do formando com expiração (Formattio já tem `FormandoAccessToken`); **auditoria** de todo acesso ao elo (`audit_log`, reusando o existente). |
| Geral | **Acordo de compartilhamento de dados** entre Formattio e Fidellis (mesmo sob a Kairós); direitos do titular (export/erasure/opt-out — ADR-0012 no Fidellis; Formattio já tem `PrivacyAcceptance`/`DeletionRequest`/`EmailSuppression`). |

## 6. Risco residual e conclusão

Com as medidas da §5, os riscos **Altos** (R1/R4/R5) caem a um residual **aceitável** *desde que* a base
legal (consentimento específico) esteja formalizada e a minimização seja auditada. **Conclusão técnica:
prosseguir é viável**, condicionado às pendências abaixo. Caso o jurídico não valide a base legal do
cruzamento, a alternativa é **não integrar** (Fidellis é autossuficiente) ou operar o portal **apenas com
doação** (não-membro), sem cruzamento.

## 7. Pendências para aprovar (checklist)

- [ ] **Parecer jurídico** sobre a base legal do cruzamento sensível + texto de **consentimento específico**.
- [ ] **Encarregado/DPO** designado e ciente; enquadramento controlador × operador confirmado.
- [ ] **Acordo de compartilhamento de dados** Formattio ↔ Fidellis assinado.
- [ ] **ADR-0013** movido para **Aceito** com estas condições registradas.
- [ ] Só então: implementar no Fidellis `Donor.ExternalId`/`Source` + canal de importação + give autenticado.

## 8. Referências

ADR-0013 (LGPD do elo) · ADR-0012 (auditoria/LGPD já implementados) · plano e planilha de auditoria (D-10)
· D-06 (gating membro × não-membro) · D-01 (identidade/membership).

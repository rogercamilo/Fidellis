# Acordo de Compartilhamento de Dados — Formattio ↔ Fidellis

> ⚠️ **RASCUNHO / MODELO — NÃO É PARECER JURÍDICO.** Redigido pela engenharia (CTO) para **acelerar a
> etapa jurídica** e satisfazer a pendência do [RIPD](../architecture/ripd-formattio-fidellis.md) e do
> [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md). **Revisão e formalização obrigatórias
> por jurídico/DPO.** Campos entre `[ ]` são preenchidos na assinatura. **Versão:** 0.1 (2026-09-12).

---

## Preâmbulo — enquadramento a confirmar

Ambas as plataformas são produtos da **mesma casa (Kairós)**, e a **instituição contratante** (tenant) é a
**controladora** dos dados de seus formandos/doadores; **Formattio** e **Fidellis** atuam como
**operadores** (LGPD art. 5º, VI–VII). Este acordo disciplina o **compartilhamento de mão única
(Formattio → Fidellis)** do **elo de identidade de membro**. *(O jurídico deve confirmar se o instrumento
adequado é (a) um anexo de proteção de dados ao contrato controladora–operadores, e/ou (b) um acordo
operador–operador entre as duas plataformas Kairós.)*

## 1. Partes

| Papel | Identificação |
| --- | --- |
| Controladora | `[NOME DA INSTITUIÇÃO CONTRATANTE]`, `[CNPJ]` |
| Operador — origem | **Formattio** (Kairós), `[RAZÃO SOCIAL/CNPJ]` |
| Operador — destino | **Fidellis** (Kairós), `[RAZÃO SOCIAL/CNPJ]` |
| Encarregado (DPO) comum | `[NOME/CANAL]` |

## 2. Objeto e finalidade

Compartilhamento **mínimo e de mão única** da **identidade de membro** do Formattio para o Fidellis, com a
**finalidade específica** de reconhecer o membro e habilitar **dízimo/oferta** (D-06) e evitar cadastro
duplicado. **Vedado** qualquer outro uso (marketing, segmentação, enriquecimento) sem base legal e
consentimento próprios.

## 3. Dados compartilhados (escopo fechado)

| Dado | Origem | Classificação |
| --- | --- | --- |
| `ExternalId` (id estável do membro) | `Formando.id` | comum (referência) |
| `Source = formattio` (origem/vínculo) | constante | comum |
| E-mail | `Formando.email` | comum (identificação) |
| Nome (exibição) | `Formando.nome` | comum |

**Expressamente fora do escopo:** formação, vocação, sacramentos, acompanhamento, documentos
eclesiásticos, RG, CPF, foto, `passwordHash` e qualquer categoria sensível (art. 11). O elo é
**referência (id + origem)**, não cópia. A regra **"integração estabelecida ⇒ membro"** deriva da
**origem**, sem importar flag ou conteúdo de formação (ADR-0013, dec. 8).

## 4. Base legal e consentimento

O **cruzamento sensível** (convicção × finanças) depende de **base legal específica** a ser definida pelo
jurídico — hipótese de trabalho: **consentimento específico e destacado** (art. 11, I), coletado no portal
(ver [termo de consentimento](consentimento-integracao-formattio.md)). **Sem base legal/consentimento
ativo, não há compartilhamento nem cruzamento**; o portal opera apenas com **doação** (não-membro).

## 5. Obrigações dos operadores

- **Minimização & finalidade:** tratar apenas os dados da §3, só para a finalidade da §2.
- **"Formattio lança, não armazena":** o portal do formando **abre/encaminha** o fluxo do Fidellis; o
  Formattio **não** guarda histórico financeiro ao lado da formação (RIPD §5 / ADR-0013 dec. 9).
- **Segurança:** transporte **server-to-server** com **credencial de serviço por tenant**, TLS, segredos
  em cofre; tokens de portal com expiração (`FormandoAccessToken`); **princípio do menor privilégio**.
- **Segregação:** dados do elo **não** entram em relatórios, exports contábeis, transparência pública ou
  régua de comunicação (ADR-0013 dec. 5).
- **Auditoria:** registrar em `audit_log` toda importação/associação/consulta ao elo, com ator e origem.
- **Sub-operadores:** vedado subcontratar tratamento sem anuência prévia da controladora.
- **Confidencialidade:** sigilo dos dados, inclusive após o término.

## 6. Direitos dos titulares

Encaminhamento e atendimento de acesso, correção, portabilidade, eliminação, informação e **revogação**.
Formattio: `PrivacyAcceptance`/`DeletionRequest`/`EmailSuppression`/`export/meus-dados`. Fidellis:
export/erasure/opt-out do **ADR-0012**. A **revogação/erasure** desfaz o elo e **pausa/encerra** a
recorrência (offboarding — ADR-0013), preservando o financeiro por obrigação legal.

## 7. Ciclo de vida do vínculo (offboarding)

Sinal de offboarding no Formattio: **`Formando.ativo=false`** e/ou `deletedAt` (e, quando populado,
`condicaoAtual ∈ {desligado, falecido}`). Ao receber o sinal (via export/polling; webhook outbound a
construir no Formattio), o Fidellis **pausa** a recorrência (`paused`, reversível) e **notifica** o
membro; **encerra** (`canceled`) no desligamento definitivo.

## 8. Segurança da informação e incidentes

Medidas técnicas e organizacionais compatíveis com o risco (art. 46). **Notificação de incidente** entre
as partes e ao DPO **sem demora**, com apoio mútuo à comunicação à **ANPD** e aos titulares quando cabível
(art. 48).

## 9. Retenção e eliminação

Retenção do elo alinhada à finalidade (§2) e às regras do **ADR-0012/ADR-0013**; revisão periódica.
Encerrado o acordo ou a finalidade, os operadores **eliminam** o elo, salvo obrigação legal de guarda
(financeiro/prestação de contas).

## 10. Auditoria e conformidade

A controladora (ou o DPO comum) pode **auditar** o cumprimento deste acordo. Anexos de referência: **RIPD**,
**ADR-0013**, **planilha de auditoria D-10** e **termo de consentimento**.

## 11. Vigência, término e foro

Vigência a partir da assinatura, enquanto durar a integração ativa e a finalidade. Rescisão por descumprimento
material ou por decisão da controladora. Foro: `[COMARCA]`.

## 12. Assinaturas

| Parte | Nome/Cargo | Data | Assinatura |
| --- | --- | --- | --- |
| Controladora | `[ ]` | `[ ]` | `[ ]` |
| Operador — Formattio | `[ ]` | `[ ]` | `[ ]` |
| Operador — Fidellis | `[ ]` | `[ ]` | `[ ]` |
| Encarregado (DPO) | `[ ]` | `[ ]` | `[ ]` |

---

## Referências

[RIPD/DPIA](../architecture/ripd-formattio-fidellis.md) · [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md)
· [Auditoria D-10](../prd/d10-formattio-audit-worksheet.md) · [Termo de consentimento](consentimento-integracao-formattio.md)
· ADR-0012 (auditoria/LGPD já implementados no Fidellis).

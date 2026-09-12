# D-10 — Auditoria do Formattio (concluída — esquema + código)

> **Status:** ✅ **auditoria substancialmente concluída** · 2026-09-12 — esquema (Docker, metadados) +
> **revisão do código** (`App formativo/frontend`, Next.js). **Nenhuma PII consultada** (ADR-0013).
> **Pronta para o RIPD.** Única pendência não-bloqueante: taxa de match em amostra pseudonimizada.
> Alimenta o [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md). Epic **#80**.

---

## 0. Acesso / insumos

- [x] **Esquema** — Docker `appformativo-postgres-1` (Postgres 16), db `formacao_comunitaria`, ORM **Prisma**.
- [x] **Código/superfície de API** — `App formativo/frontend` (Next.js, ~149 rotas `app/api`) revisado.
- [ ] **Amostra pseudonimizada** p/ taxa de match (não-bloqueante — o vínculo é o `ExternalId`, não e-mail).

## 1. Dicionário — entidades relevantes + classificação LGPD

| Entidade | Campos-chave | Classificação | No elo? |
| --- | --- | --- | --- |
| **Formando** (o membro) | `id`, `organizacaoId`, `email`, `nome`, `ativo`, `condicaoAtual`, `deletedAt` | comum (identificação/vínculo) | **sim** (mínimo) |
| **Formando** (formação) | `nivelFormativo`, `modalidade`, `dataIngresso`, `condicaoAtual`, `paroquiaReferencia`, `estadoCivil`, `numFilhos`, `dataNascimento`, `rg`, `foto`, `passwordHash` | **SENSÍVEL** (convicção/formação religiosa, art. 11 + PII) | **NÃO** |
| **Organizacao** (tenant) | `id`, `nome`, `status`, `tipoOrganizacao` | comum | referência (mapa por config) |
| **FormandoAccessToken** | `formandoId`, `tokenHash`, `tipo`, `expiresAt`, `usedAt` | comum (auth) | — (referência p/ auth do portal) |
| **Usuario** (equipe/admin) | `id`, `organizacaoId`, `email`, `perfil`, `mfa*` | comum | não (é o lado equipe, não o membro) |
| `Acompanhamento*`, `ProcessoEclesiastico`, `DocumentoEclesiastico`, `LeituraVocacional`, `RegistroPromessa`, `RelatorioEtapa`, `ProgressoEtapa`, `Depoimento`, `Compromisso` | conteúdo de formação/vocacional | **SENSÍVEL** | **NÃO** |
| LGPD infra: `PrivacyAcceptance`, `CookieConsent`, `DeletionRequest`, `EmailSuppression` | — | — | (Formattio já tem tooling LGPD ✔) |

## 2. Identidade & correspondência

| Item | Achado |
| --- | --- |
| **`ExternalId`** (id estável do formando) | ✅ **`Formando.id`** (text/cuid, `NOT NULL`). |
| Chave de match com `Donor` do Fidellis | `Formando.email` (`NOT NULL`), mas **⚠️ NÃO é único** (índices únicos só em `id` e `tokenAssinatura`; na base de teste: **35 formandos / 27 e-mails distintos** → e-mails repetidos). **Não há CPF** (só `rg`). → **e-mail é heurística, não chave**; o vínculo autoritativo é o `ExternalId`. |
| Vínculo de org/tenant | `Formando.organizacaoId` → **exige mapa de config** `Organizacao.id (Formattio) ↔ tenant slug (Fidellis)` — **não há id/CNPJ compartilhado**. |
| Escala (base de teste) | 35 formandos, todos `ativo=true`, `condicaoAtual` **null** (não populado). |

## 3. Conjunto mínimo a importar (confirmado pelo esquema)

| Campo | Origem | Justificativa |
| --- | --- | --- |
| `ExternalId` | `Formando.id` | referência estável do membro |
| `Source = formattio` | (constante) | origem → **implica membro** (regra do PO) |
| e-mail | `Formando.email` | chave de correspondência com `Donor` |
| nome (opcional) | `Formando.nome` | exibição; já coletado pelo Fidellis no give |

> **Nada** de formação/vocação (§1 sensível) entra no Fidellis. O elo é referência (`id + origem`), não cópia.

## 4. Superfície de integração & offboarding

| Item | Achado |
| --- | --- |
| Banco | Postgres 16 (Prisma). |
| **Export** (carga inicial) | ✅ `GET /api/export/organizacao` (export por org) e `/api/export/meus-dados` (portabilidade LGPD do próprio formando). Viável para a 1ª carga de formandos. |
| **Portal do formando** | ✅ Robusto: `portal/login`, `portal/me`, `portal/perfil`, `portal/notificacoes`, `portal/presenca`, `portal/travessia`… → **home natural da aba de dízimo/oferta** (chamaria o Fidellis server-to-server). |
| **API de integração externa** | 🔴 **Inexistente** — não há API pública de terceiros nem `api-key`/service-token. O contrato de integração (Formattio→Fidellis) é **greenfield no lado do Formattio**. |
| Webhooks | Só **inbound** (`webhooks/resend`, `stripe/webhook`). **Sem webhook outbound** de mudança de status → offboarding via **export/polling** ou webhook a ser construído no Formattio. |
| Auth p/ Formattio→Fidellis | Formattio tem auth de formando (`portal/login` + `FormandoAccessToken`, `ativar`/`recuperar`) e `Usuario.mfa`. Para a aba de dízimo: **credencial de serviço por tenant** (server-to-server) + `ExternalId` — a construir (não existe hoje). |
| **Sinal de offboarding** | ✅ **`Formando.ativo=false`** e/ou `deletedAt`. O enum `condicaoAtual ∈ {…, desligado, falecido}` existe mas **não estava populado** na base de teste → confiar em `ativo`/`deletedAt` como sinal primário. Formattio tem `ProcessedWebhookEvent` (infra de webhook) → um **webhook de mudança de status** é viável; senão, polling. |
| Billing | Formattio usa **Stripe** (Organizacao.stripe*); Fidellis usa Pagar.me — sistemas separados (sem impacto no give do membro, que é lado Fidellis). |

## 5. Qualidade dos dados

- `Formando.email` **NOT NULL** ✅ (chave de match sempre presente). Unicidade por org a confirmar.
- Soft-delete (`deletedAt`) presente em `Formando`/`Usuario`. Escala (nº de formandos) a medir com amostra.

## 6. Achados & recomendação (para o RIPD/ADR-0013)

- **Viável tecnicamente.** `ExternalId` estável (`Formando.id`), match por **e-mail**, offboarding claro
  (`ativo`/`condicaoAtual`/`deletedAt`). Formattio já tem token de formando (auth) e tooling LGPD.
- **Minimização confortável:** basta `id + origem + email(+nome)`; toda a formação/vocação fica fora — o
  cruzamento sensível **não se materializa** no Fidellis (princípio "Formattio lança, não armazena").
- **Ponto de atenção 1 — e-mail NÃO é único** (sem CPF): auto-match por e-mail é **ambíguo**. Conclusão de
  desenho: o vínculo **é o `ExternalId`** (`Formando.id`); o give autenticado pela integração carrega o
  `ExternalId` — **não** faz lookup por e-mail. E-mail vira só exibição/heurística opcional de reconciliação.
- **Ponto de atenção 2 — mapa org↔tenant** por config (sem id compartilhado) → parte do setup da integração.
- **Semântica confirma a regra do PO:** o enum chama-se **`CondicaoMembro`** — o formando **é** um membro,
  reforçando "integração ⇒ membro".

## 7. Conclusão

Auditoria **substancialmente concluída** (esquema + código). Superfície definida: **carga inicial por
`export/organizacao`**; **aba de dízimo/oferta no portal do formando** (a construir no Formattio,
chamando o Fidellis por credencial de serviço/tenant); **offboarding por export/polling** (sem webhook
outbound hoje). O **contrato de integração é greenfield no lado do Formattio** — não bloqueia o desenho
do Fidellis, mas é dependência de entrega da outra equipe.

**Próximo passo:** **RIPD/DPIA** com base nestes achados → aceitar **ADR-0013** → só então codar, no
Fidellis, `Donor.ExternalId`/`Source` + canal de importação + a chamada de give autenticada. Pendência
não-bloqueante: medir taxa de match numa amostra pseudonimizada (o vínculo autoritativo é o `ExternalId`).

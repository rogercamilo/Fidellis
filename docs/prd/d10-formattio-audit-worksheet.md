# D-10 — Auditoria do Formattio (1ª rodada preenchida via esquema local)

> **Status:** auditoria **em andamento** · 2026-09-12 — **§1–§5 preenchidos via introspecção do esquema**
> (Docker local, só metadados; **nenhuma linha de PII consultada** — ADR-0013). Pendências: amostra
> pseudonimizada p/ taxa de match; superfície de **API** (app não estava no ar); confirmar **unicidade de
> e-mail por org**. Alimenta o RIPD e o [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md). Epic **#80**.

---

## 0. Acesso / insumos

- [x] **Acesso ao esquema** — Docker `appformativo-postgres-1` (Postgres 16), db `formacao_comunitaria`,
  user `formativo`; Adminer em `:8080`. ORM: **Prisma** (`_prisma_migrations`). Billing do Formattio: **Stripe**.
- [ ] **Amostra pseudonimizada** p/ medir taxa de match por e-mail (não consultada — só metadados até aqui).
- [ ] **Doc/superfície de API** (o container da app não está no ar; só Postgres + Adminer).
- [ ] Confirmar **unicidade de `Formando.email`** (por org?) e escala (nº de formandos).

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
| Banco | Postgres 16 (Prisma). Export/DB viável para 1ª carga. |
| **API** | _(a confirmar — app fora do ar; verificar rotas no repo do Formattio)_. |
| Auth p/ Formattio→Fidellis | Formattio já tem **`FormandoAccessToken`** (token/link mágico) e `Usuario.mfa`. Para a aba de dízimo: **credencial de serviço por tenant** (server-to-server) + `ExternalId` — a definir no ADR. |
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

## 7. Pendências para fechar a auditoria

1. **API do Formattio** (subir a app / conferir rotas no repo) — define API × export.
2. **Amostra pseudonimizada** → taxa de match por e-mail + unicidade + escala.
3. Consolidar → **RIPD** → aceitar **ADR-0013** → só então codar adaptador + identidade federada.

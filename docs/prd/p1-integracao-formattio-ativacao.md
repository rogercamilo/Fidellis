# P1 — Ativação da integração Formattio (provisionamento + "Conectar" self-service)

> **Status:** ✍️ **spec para revisão do PO** · 2026-09-13. Deriva das decisões do
> [estudo de disponibilização](../strategy/estudo-monetizacao-e-integracao-formattio.md) (§11): bundle +
> self-service; cenário A provisionado no onboarding; sem cobrança; **P1 completo** como próxima entrega.
> Constrói sobre o #80 já implementado ([contrato](../integrations/formattio-fidellis-contract.md), ADR-0013).
> **Objetivo:** eliminar o atrito de ativação atual (chave via console + env + sync manual) preservando
> minimização, consentimento e segredos cifrados. Fluxo: **detalhar → PO revisa → codar**.

---

## 0. Recorte e faseamento interno

O P1 tem três frentes, da mais imediata à mais dependente de terceiros:

| Frente | Escopo | Depende de | Prontidão |
| --- | --- | --- | --- |
| **P1a** | UI de **geração/rotação/status da chave** no Fidellis | nada (back já existe) | **pronta para codar** |
| **P1b** | Fluxo **"Conectar" self-service** (código de pareamento + auto-map + agendador de sync + painel) | P1a | design detalhado abaixo |
| **P1c** | **Provisionamento no onboarding** do cenário A (elo automático) | existir um onboarding conjunto Kairós | design + dependência |

Recomendo entregar **P1a primeiro** (mata o console imediatamente), depois **P1b**, e **P1c** quando o
onboarding conjunto existir (ou como uma variante "provisionar sem código" reutilizando o motor do P1b).

---

## P1a — UI de chave no Fidellis (pronta para codar)

### Objetivo
Permitir que o operador (admin/coordenador) **gere/rotacione** a credencial de serviço da integração e veja
o **status** pela tela de **Configurações**, sem console. A chave em claro aparece **uma única vez**.

### Estado atual
Backend **já existe** (#97): `POST /api/finance/integration/{source}/key` (emite/rotaciona, devolve a chave
uma vez) e `GET /api/finance/integration/{source}` (`{ configured, enabled }`). Só falta a UI + client.

### Impacto no código
| Área | Mudança |
| --- | --- |
| `apps/web/app/lib/api.ts` | `getIntegrationStatus(t, source)` (GET) e `issueIntegrationKey(t, source)` (POST). |
| `apps/web/app/dashboard/configuracoes` | Seção **"Integração Formattio"**: status (configurada/habilitada); botão **Gerar/Rotacionar chave** → exibe a chave uma vez em caixa com **copiar** + aviso "isto invalida a chave anterior"; texto explicando que a chave é colada no Formattio. |
| RBAC | Escrita já restrita pelo `FinanceWriteFilter` (papel lançador). |

### UX
Botão → confirmação ("rotacionar invalida a chave atual") → modal/caixa com a chave + **Copiar** + link para
onde colar no Formattio. Status mostra "configurada" quando já houve emissão.

### Fora de escopo / testes
Sem mudança de backend (coberto por `IntegrationCredentialTests`). Front: build/lint. Sem persistir a chave
em claro no browser (só exibir e copiar).

---

## P1b — "Conectar" self-service (design)

### Objetivo
Substituir a **cópia manual de chave** e a **config por env** por um **pareamento seguro** entre as duas
contas (sem SSO): o humano move apenas um **código curto de uso único**; a **credencial trafega
server-to-server** e é **persistida cifrada**; o **mapa organização↔tenant/unidade** é resolvido no ato.

### Fluxo
1. **No Fidellis (admin):** em *Configurações → Integração Formattio*, botão **"Gerar código de conexão"**;
   o admin escolhe a **unidade (organization)** a vincular. O Fidellis cria um **connect code** de uso único
   (curto, expira em ~10 min), guardado **hasheado**, ligado a `(tenant, organizationId)`.
2. **No Formattio (admin, gestão):** em *Configurações → Integração Fidellis*, cola o **código** e a
   **base URL** do Fidellis, e clica **Conectar**.
3. **Troca server-to-server:** o Formattio chama o Fidellis `POST /api/public/{tenant}/integration/connect { code }`.
   O Fidellis valida/consome o código e devolve **uma vez**: `{ tenantSlug, organizationId, serviceKey, pullSecret }`.
   - `serviceKey` = credencial do canal Formattio→Fidellis (give/pledge/offboard) — o Formattio guarda **cifrada**.
   - `pullSecret` = segredo compartilhado do canal Fidellis→Formattio (sync) — ambos os lados persistem
     (Fidellis para **enviar**, Formattio para **validar**), substituindo os env manuais.
4. **Auto-map:** `tenantSlug`/`organizationId` vêm da troca (o código já estava ligado à unidade) → o
   Formattio grava direto (sem digitar GUID). *(Sugestão de match por CNPJ/nome é reforço opcional na UI.)*
5. **Backfill + agendador:** ao conectar, o Fidellis registra a conexão (ver entidade abaixo) e dispara o
   **backfill inicial** (sync puxado) + agenda os ciclos seguintes.
6. **Painel (dos dois lados):** status "conectado", última sync, nº de membros, **rotacionar chave**,
   **desconectar** (desconectar ⇒ offboarding em massa com aviso + revoga credenciais).

### Impacto no código

**Fidellis (repo core):**
| Peça | Mudança |
| --- | --- |
| Entidade `IntegrationConnection` (tenant schema) | Substitui o env do puller por config persistida: `Source`, `ExternalBaseUrl` (Formattio), `ExternalOrgId` (organizacaoId), `PullSecretHash`/`enc`, `Enabled`, `LastSyncAt`. |
| `ConnectCode` (tenant schema, efêmero) | `CodeHash`, `OrganizationId`, `ExpiresAt`, `UsedAt`. |
| Endpoints | `POST /api/finance/integration/connect-code` (operador, gera o código p/ uma unidade); `POST /api/public/{tenant}/integration/connect` (troca código→credenciais, consome o código); status/rotate/disconnect. |
| Agendador | Hosted service (espelha o `BillingWorker`) que roda o `FormattioSyncService` para cada `IntegrationConnection` habilitada; intervalo por config. Substitui o gatilho manual. |
| `FormattioSyncService` | Passa a ler baseUrl/secret/orgId da `IntegrationConnection` (não do env). |

**Formattio (repo App formativo):**
| Peça | Mudança |
| --- | --- |
| `IntegracaoFidellis` (já existe) | Ganha `pullSecretEnc`; o "Conectar" preenche tudo pela troca (não manual). |
| Config UI (já existe) | Vira **"Conectar"**: campo do código + base URL + botão; troca server-to-server; some a digitação de chave/GUID. |
| Endpoint de sync (já existe) | Passa a validar o Bearer pelo `pullSecret` obtido na conexão. |

### Segurança/compliance
- O humano nunca copia a **chave**, só um **código efêmero de uso único** (hasheado, expira, consumível 1x).
- Credenciais **cifradas** em ambos os lados; **rotação** e **desconexão** explícitas; auditoria de connect/rotate/disconnect.
- Minimização e consentimento **inalterados** (o consentimento do membro segue no portal).

---

## P1c — Provisionamento no onboarding (cenário A) (design + dependência)

### Objetivo
Para a instituição que **entra na Kairós contratando os dois** (cenário A), estabelecer o elo
**automaticamente no provisionamento conjunto** — sem código, sem passo do admin.

### Dependência
Requer um **fluxo de onboarding/provisionamento conjunto Kairós** (hoje **inexistente** como fluxo único; os
produtos têm signup próprio e auth standalone). Enquanto não existir, P1c é **design**; a ativação do
cenário A usa o P1b (uma vez, no setup) como ponte.

### Desenho (quando houver provisionamento conjunto)
No ato de criar os dois tenants, um **canal de provisionamento confiável** (segredo de plataforma Kairós,
server-to-server) executa a **mesma troca do P1b sem intervenção humana**: mapeia organização↔unidade, emite
e injeta `serviceKey`+`pullSecret`, marca a integração habilitada, e agenda o backfill. O consentimento do
membro continua sendo coletado no **1º acesso ao portal** (não se antecipa consentimento).

### Impacto
Reusa **todo** o motor do P1b (mesmos endpoints/entidades), trocando o "código digitado pelo humano" por um
**token de provisionamento** emitido pela camada Kairós. Nenhuma nova regra de dados.

---

## Riscos & sequência

- **P1a** é isolada e imediata (só UI) — **fazer primeiro**.
- **P1b** é a maior (entidades + agendador + troca + UI nos dois repos) — quebrar em sub-tarefas: (1) entidades+connect no Fidellis; (2) agendador; (3) "Conectar" no Formattio; (4) painéis.
- **P1c** aguarda o onboarding conjunto — não bloquear o P1b por ela.
- Manter o **MVP manual** funcionando até o P1b entrar (fallback de piloto).

## Próximo passo
Aprovado o recorte, começo pela **P1a** (codável já) e sigo para as sub-tarefas do **P1b**. O **one-pager
comercial** do cenário A entra em paralelo, como material de go-to-market (não bloqueia o código).

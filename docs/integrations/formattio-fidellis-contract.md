# Contrato de integração Formattio → Fidellis (#80)

> **Status:** ✅ **lado Fidellis implementado e pronto** · 2026-09-13. Este documento é o **contrato que a
> equipe do Formattio segue** para consumir a integração. Base: [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md)
> (Aceito), [RIPD](../architecture/ripd-formattio-fidellis.md), [auditoria D-10](../prd/d10-formattio-audit-worksheet.md).
> **Regra de ouro:** a instituição **não cobra** dízimo/oferta — o membro contribui; o Formattio apenas
> **encaminha** o fluxo do Fidellis e **não armazena** financeiro ("Formattio lança, não armazena").

---

## 1. Visão geral

Integração **de mão única** (Formattio → Fidellis), **por tenant**, com **identidade federada** do membro:

1. **Identidade** — o formando do Formattio vira um **doador-membro** no Fidellis, referenciado por
   `ExternalId` (= `Formando.id`) + `Source = formattio`. A origem federada **implica membro**.
2. **Contribuição** — a aba de dízimo/oferta no **portal do formando** aciona o Fidellis
   **server-to-server** (o Formattio não guarda o financeiro).
3. **Offboarding** — ao desligar o formando, a recorrência **pausa/encerra** com aviso.

**Minimização (LGPD):** trafega só `externalId + origem + nome + e-mail`. **Nada** de formação, vocação,
sacramentos, documentos, foto. O vínculo autoritativo é o **`externalId`** — **não** o e-mail (que **não é
único** na base do Formattio).

## 2. Pré-requisitos de setup (uma vez por instituição)

| Item | Responsável | Como |
| --- | --- | --- |
| **Mapa `organizacaoId` (Formattio) ↔ `tenant` (slug Fidellis) + `organizationId` (unidade Fidellis)** | operação/config | não há id/CNPJ compartilhado — é config da integração. |
| **Consentimento específico** do titular (art. 11, I) coletado no portal **antes** de habilitar a aba | Formattio | ver [termo](../legal/consentimento-integracao-formattio.md). Sem consentimento ⇒ portal só doação. |
| **Credencial de serviço** por tenant | operador Fidellis emite; Formattio guarda | §3. |

## 3. Autenticação

- **Canal de contribuição/offboarding (público, server-to-server):** header **`X-Integration-Key: <chave>`**.
  A chave é **por tenant e por origem** (`formattio`), guardada só como hash no Fidellis.
- **Emissão/rotação da chave** (operador Fidellis autenticado por JWT):
  ```http
  POST /api/finance/integration/formattio/key
  → 200 { "source": "formattio", "key": "<valor em claro — exibido UMA vez>" }
  ```
  `GET /api/finance/integration/formattio → { configured, enabled }`.
- **Importação de identidade (carga inicial):** endpoint de **operador** (JWT), não usa a chave de serviço.

> `{tenant}` nos caminhos públicos é o **slug** da instituição no Fidellis.

## 4. Endpoints

### 4.1 Importar identidade (carga inicial / sincronização) — JWT do operador
Idempotente por `(source, externalId)`. Marca `IsMember`. Fonte natural: `GET /api/export/organizacao` do Formattio.
```http
POST /api/crm/donors/federated
{
  "source": "formattio",
  "members": [
    { "externalId": "<Formando.id>", "name": "João da Silva", "email": "joao@ex.com" }
  ]
}
→ 200 { "source": "formattio", "created": 1, "updated": 0 }
```

### 4.2 Contribuição pontual (dízimo/oferta) — `X-Integration-Key`
```http
POST /api/public/{tenant}/integration/give
X-Integration-Key: <chave>
{
  "externalId": "<Formando.id>",
  "organizationId": "<unidade Fidellis (GUID)>",
  "amount": 50.00,
  "entryType": "tithe",      // "tithe" (dízimo) | "offering" (oferta)
  "method": "pix"            // "pix" | "boleto"
}
→ 201 { donationId, status, qrCode, qrCodeUrl, boletoLine, boletoUrl, ... }
```

### 4.3 Dízimo recorrente (mensal) — `X-Integration-Key`
```http
POST /api/public/{tenant}/integration/pledge
X-Integration-Key: <chave>
{ "externalId": "...", "organizationId": "...", "amount": 50.00, "dayOfMonth": 5, "method": "pix" }
→ 201 { id, amount, dayOfMonth, status, method }
```

### 4.4 Offboarding — `X-Integration-Key`
Dispare quando `Formando.ativo=false`/`deletedAt` (ou `condicaoAtual ∈ {desligado, falecido}`).
```http
POST /api/public/{tenant}/integration/offboard
X-Integration-Key: <chave>
{ "externalId": "...", "permanent": false }   // false = pausa (reversível) · true = encerra
→ 200 { "action": "paused" | "canceled", "affected": <nº de recorrências> }
```

## 5. Códigos de resposta

| Código | Quando |
| --- | --- |
| `201` | contribuição/recorrência criada |
| `200` | offboarding aplicado / import ok |
| `400` | payload inválido (ex.: `amount` ≤ 0, faltou `organizationId`) |
| `401` | `X-Integration-Key` ausente/errada (ou origem desabilitada) |
| `404` | tenant não encontrado, ou **membro federado não importado** (faça a §4.1 antes) |

## 6. Mapeamento de dados

| Formattio | Fidellis |
| --- | --- |
| `Formando.id` | `externalId` (chave do vínculo) |
| `Formando.nome` | `name` |
| `Formando.email` | `email` (exibição/heurística — **não** é chave) |
| `Organizacao.id` | `{tenant}` + `organizationId` (via mapa de config — §2) |
| `Formando.ativo=false`/`deletedAt` | dispara §4.4 (offboarding) |

## 7. Segurança & LGPD (obrigatório)

- **Não** enviar dados de formação/vocação/sensíveis — só o conjunto mínimo (§1).
- **Consentimento** específico coletado antes de habilitar a aba; sem ele, a aba só oferece **doação**
  (fluxo anônimo, sem `externalId`).
- **TLS** + segredo em cofre; a chave de serviço é por tenant e **rotacionável** (§3).
- O Fidellis **audita** todo toque do elo (`donor.federated_import`, `integration.give/pledge/offboard`,
  `integration.key_issued`).
- Direitos do titular (export/erasure/opt-out) seguem o [ADR-0012](../architecture/ADR-0012-donor-portal-audit-lgpd.md);
  erasure/revogação desfazem o elo e param a recorrência.

## 8. Checklist para a equipe do Formattio

- [ ] Definir o **mapa** `organizacaoId ↔ tenant/organizationId`.
- [ ] Coletar **consentimento** no portal antes de exibir a aba.
- [ ] Obter e guardar a **chave de serviço** (§3) por instituição.
- [ ] **Sincronizar identidades** (§4.1) — carga inicial + atualização incremental.
- [ ] Aba de dízimo/oferta no portal chamando §4.2/§4.3 (**sem armazenar** financeiro).
- [ ] Emitir **offboarding** (§4.4) na mudança de status do formando.

## 9. Referências

[ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md) · [RIPD](../architecture/ripd-formattio-fidellis.md)
· [Auditoria D-10](../prd/d10-formattio-audit-worksheet.md) · [Termo de consentimento](../legal/consentimento-integracao-formattio.md)
· [Acordo de compartilhamento](../legal/acordo-compartilhamento-formattio-fidellis.md).

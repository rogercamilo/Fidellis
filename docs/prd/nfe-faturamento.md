# NF-e / Faturamento (gestão avançada) — spec de escopo (#79)

> **Status:** ✅ **IMPLEMENTADA (fase 1 — registro/vínculo)** · 2026-09-12. PO confirmou: documento
> **ambos** (nfse+nfe no cadastro), profundidade **registro/vínculo** (sem provedor fiscal), provedor
> **indefinido** → só fase 1. Feature reservada na **D-09** atrás do modo **"Gestão avançada"**.
> **Entregue:** entidade `FiscalDocument` (+migração `FiscalDocuments` + DDL fallback); `FiscalDocumentService`
> (registrar/anexar PDF no R2/cancelar/CSV, gated por `AdvancedManagement`, só sobre recebível `service|sale`);
> `FiscalDocumentEndpoints` (`/api/finance/fiscal-documents`); item **Faturamento (NF)** no `AppShell`
> (`advanced:true`, grupo Entradas) + tela `dashboard/faturamento` + origens `service|sale` em Contas a
> receber. Testes `FiscalDocumentTests` (5). **Fase 2 (emissão integrada via provedor fiscal) fica futura.**
> **Origem:** [issue #79](https://github.com/rogercamilo/Fidellis/issues/79) · [D-09](d09-gestao-avancada.md) ·
> [parecer](parecer-finance-terceiro-setor.md) (NF = ponto **fora da curva** do público-base).
> **Referências de código:** `FinanceSettings.AdvancedManagement`, `Receivable`/`ReceivablesService`,
> `Receipt`/`ReceiptPdfService` + `IObjectStorage` (R2), `IPaymentGateway`+`WebhookProcessor` (padrão de provedor externo).

---

## 1. Objetivo e recorte

Permitir que **a minoria** de instituições que **presta serviço/vende** (e portanto **emite nota fiscal**)
registre/emita o documento fiscal **vinculado à receita**, **sem** empurrar isso ao público-base. Aparece
**só** com **Gestão avançada** ligada (D-09).

**Premissa que molda tudo:** o núcleo do Fidellis é **doação/dízimo/oferta** — que geram **recibo** (já
existe: `Receipt` + PDF + R2), **não** nota fiscal. Nota fiscal só faz sentido sobre **atividade
comercial/serviço** (curso, evento pago, aluguel de espaço, bazar/livraria) — o que, no terceiro setor
religioso, é **exceção** e frequentemente sujeito a **imunidade/isenção** (CF art. 150, VI, "b"). Logo, NF
**não** se aplica a doações; aplica-se a **recebíveis de natureza comercial**.

## 2. Estado atual

| Peça | Situação |
| --- | --- |
| Flag `AdvancedManagement` | ✔ Existe (D-09); hoje gateia só a tela Projetos & voluntariado. |
| `Receivable` (a receber) | ✔ Existe: `Source`, `DonorId`, `Amount`, `DueDate`, `Status`, vínculo a `Donation`/fundo/projeto. Vínculo natural da NF. |
| `Receipt` + PDF + R2 | ✔ Recibo de doação: PDF gerado (`ReceiptPdfService`) e arquivado (`IObjectStorage`→R2). **Modelo direto** para arquivar o PDF/DANFE/DANFSE. |
| `IPaymentGateway` + `WebhookProcessor` | ✔ Padrão de **provedor externo assíncrono** (Pagar.me): interface + payloads + callback. **Modelo direto** para um provedor fiscal. |
| NF / faturamento | 🔴 **Nada** — sem entidade, endpoint, tela nem provedor. |

## 3. Landscape fiscal (por que isto precisa de decisão de negócio)

| Documento | Quando | Complexidade de integração |
| --- | --- | --- |
| **NFS-e** (serviço) | curso, evento, aluguel, consultoria, mensalidade de serviço | **Municipal** — ~5.570 municípios, cada um com layout/webservice próprio (padrão nacional ABRASF/NFS-e Nacional em adoção). Alta sem um agregador. |
| **NF-e** (produto, mod. 55) | venda de bens (livraria, artigos) | Estadual/SEFAZ, padrão nacional único. Média. |
| **NFC-e** (mod. 65) | varejo ao consumidor (bazar/loja) | Estadual, exige equipamento/contingência. Média/alta. |

→ Para o público do Fidellis, o caso realista é **NFS-e (serviço)**; NF-e produto é raro; NFC-e improvável.
A escolha do **documento** e do **provedor** define o tamanho da entrega — daí as perguntas da §5.

## 4. Decisões propostas (recomendação para o review)

- **Q1 — Documento-alvo: NFS-e (serviço) primeiro.** Cobre o caso realista (evento/curso/aluguel). NF-e
  produto e NFC-e ficam fora até haver demanda concreta de venda de bens. *(alternativas na §5)*
- **Q2 — Profundidade da 1ª entrega: começar por REGISTRO/VÍNCULO, não emissão integrada.**
  MVP honesto e alinhado a "contabilidade terceirizada, contador assina": a NF é **emitida por fora**
  (contador/emissor municipal) e o Fidellis **registra** número/série/data/valor/PDF e **vincula ao
  recebível**, **arquivando o PDF no R2** (reuso do padrão do recibo). Zero integração fiscal, entrega
  rápida, cobre a maioria. **Emissão integrada via provedor fiscal** (`IFiscalProvider`, assíncrona como o
  webhook do PSP) fica como **Fase 2**, quando houver provedor/município definidos. *(alternativa: já ir para emissão integrada)*
- **Q3 — Vínculo: `Receivable` de natureza comercial.** A NF referencia um **recebível** (serviço
  prestado/venda), **nunca** uma doação/dízimo/oferta (essas têm recibo). Marcar o recebível como
  "faturável" (ex.: `Source ∈ {service, sale}`), e a NF fica 1:1 com ele. Quando a receita cai, a `Entry`
  correspondente carrega a referência da NF para relatório/exportação ao contador.
- **Q4 — Gate e papel.** Todo o recurso atrás de `AdvancedManagement=true`; escrita restrita a
  `FinanceRoles.CanLaunch` (coordenador/admin), como os demais lançamentos. Some do menu quando o modo está desligado.

## 5. Decisões do PO (confirmadas 2026-09-12)

1. **Documento fiscal:** **ambos** — o cadastro aceita `nfse` (serviço) e `nfe` (produto). Como a fase 1 é
   só registro/vínculo, suportar ambos é barato (um campo `type`), sem custo de integração.
2. **Profundidade:** **registro/vínculo** — a NF é emitida por fora e o Fidellis registra/arquiva/vincula.
   Sem `IFiscalProvider` nesta fase.
3. **Provedor/município:** **indefinido** — reforça ficar na fase 1; emissão integrada fica para quando
   houver provedor e demanda concreta.

## 6. Impacto no código (para a recomendação Q1–Q4 — registro/vínculo de NFS-e)

| Área | Mudança |
| --- | --- |
| `FiscalDocument` (nova entidade, tenant) + migração | Tipo (nfse/nfe), número, série, chave/protocolo, emissão, valor, `ReceivableId`, `EntryId?`, `Status` (registrada), `PdfObjectKey` (R2). |
| `Receivable` | Marcar faturável por `Source ∈ {service, sale}` (sem mudança de schema — só semântica/validação); opcional `FiscalDocumentId`. |
| `FiscalDocuments/*` (novo, no módulo Finance) | Endpoints `GET/POST /api/finance/fiscal-documents` (registrar), `POST {id}/pdf` (upload/arquivar no R2 via `IObjectStorage`), atrás de `FinanceWriteFilter` + `AdvancedManagement`. |
| Export ao contador | Incluir NF vinculada no `AccountantExportService`. |
| Web | Tela/aba "Faturamento" (grupo avançado no `AppShell`, `advanced:true`): listar/registrar NF, anexar PDF, ver vínculo com recebível. |
| Testes | Registro + vínculo + gate (modo desligado → 404/oculto) + arquivamento. |

**Fase 2 (emissão integrada — só se Q2=emissão):** `IFiscalProvider` (emitir/consultar/cancelar) +
payloads + processamento assíncrono (espelha `IPaymentGateway`/`WebhookProcessor`); credenciais por tenant;
tratamento de rejeição/cancelamento; DANFE/DANFSE gerada pelo provedor arquivada no R2.

## 7. Fora de escopo

Emitir NF sobre **doação/dízimo/oferta** (usam recibo, não NF); apuração/retenção de tributos (ISS/PIS/
COFINS) — é do contador; NFC-e/equipamento de PDV; múltiplos provedores simultâneos. Sem demanda concreta
(issue #79), **priorizar só após a decisão da §5**.

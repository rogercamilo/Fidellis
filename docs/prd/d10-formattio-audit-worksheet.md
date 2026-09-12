# D-10 — Planilha de auditoria do Formattio (a preencher) + insumos necessários

> **Status:** auditoria **iniciada** · 2026-09-12 · **aguardando acesso/insumos do Formattio**.
> Operacionaliza o [plano de auditoria](d10-formattio-audit-plan.md); alimenta o RIPD e o
> [ADR-0013 (LGPD)](../architecture/ADR-0013-lgpd-formattio-fidellis.md). Rastreia o epic **#80**.
> **Regra:** só **metadados + amostra mínima pseudonimizada**; nada de PII em volume (ADR-0013).

---

## 0. Insumos necessários (bloqueiam o preenchimento) — solicitar ao responsável pelo Formattio

Sem estes, a auditoria não avança. Marque ao obter:

- [ ] **Acesso autorizado** (com base legal p/ tratamento, mesmo em teste): esquema do banco **ou** doc de API **ou** export de amostra.
- [ ] **Esquema/dicionário do Formattio** (tabelas/coleções, colunas, tipos, chaves) — sem extrair PII em volume.
- [ ] **Identificador do formando**: existe **id estável e único**? qual campo? é público/interno?
- [ ] **Chaves de correspondência** disponíveis: e-mail e/ou CPF do formando (para casar com `Donor` do Fidellis).
- [ ] **Superfície de integração**: há **API**? **export** (CSV/JSON)? acesso a banco? auth, rate limit, formato, frequência.
- [ ] **Sinal de status** do formando (ativo/concluiu/desistiu) — evento/webhook/campo? (para o offboarding — ADR-0013).
- [ ] **Governança**: controlador/operador, base legal da coleta original (formação), retenção, DPO/encarregado.

> Enquanto os itens acima não chegam, as tabelas abaixo ficam **(a preencher)**. Alternativa: responder ao
> **questionário §7** inline que eu preencho a planilha.

## 1. Dicionário de dados (a preencher)

| Entidade | Campo | Tipo | Classificação LGPD (comum × **sensível**) | Necessário ao elo? |
| --- | --- | --- | --- | --- |
| _(a preencher)_ | | | | |

> Marcar **sensível** (art. 11) tudo que revele convicção religiosa/formação. O elo importa **só** o mínimo.

## 2. Identidade & correspondência (a preencher)

| Item | Resposta |
| --- | --- |
| Campo do **`ExternalId`** (id estável do formando) | _(a preencher)_ |
| Chave(s) de match com `Donor` (e-mail/CPF) | _(a preencher)_ |
| Taxa de match esperada (amostra pseudonimizada) | _(a preencher)_ |
| Duplicidade/órfãos observados | _(a preencher)_ |

## 3. Conjunto mínimo a importar (proposta)

> Alvo por padrão (ADR-0013): **`ExternalId` + `Source=formattio`** e nada de conteúdo de formação. Confirmar
> se algum campo adicional é **estritamente** necessário à correspondência.

| Campo importado | Justificativa (mínimo necessário) |
| --- | --- |
| `ExternalId` | referência estável do membro |
| `Source=formattio` | origem → **implica membro** (regra do PO) |
| _(outro?)_ | _(só se indispensável)_ |

## 4. Superfície de integração (a preencher)

| Item | Resposta |
| --- | --- |
| API × export × banco | _(a preencher)_ |
| Auth / rate limit | _(a preencher)_ |
| Formato + cadência de atualização | _(a preencher)_ |
| Superfície do **portal do formando** (onde caberia a aba de dízimo/oferta) | _(a preencher)_ |
| **Mecanismo de auth** p/ o Formattio chamar o Fidellis (credencial por tenant × token) | _(a preencher)_ |
| **Sinal de offboarding** (evento/webhook/status) | _(a preencher)_ |

## 5. Qualidade dos dados (a preencher)

Completude/consistência de e-mail/CPF; duplicidade; registros órfãos. _(a preencher)_

## 6. Saída → RIPD / ADR-0013

Ao concluir §1–§5, consolidar: dicionário + classificação, mapa de correspondência, conjunto mínimo, lista
de sensíveis, recomendação de superfície, contrato de auth e mecanismo de offboarding → **RIPD** e
pré-condições do **ADR-0013**. Só então desenhar o adaptador de importação.

> **Possível conclusão de "não integrar":** se não houver id estável nem chave de match confiável, a
> integração pode ser inviável/adiada — o Fidellis é autossuficiente (fallback permanente aceitável).

## 7. Questionário rápido (atalho, se não houver acesso direto ao esquema)

Se preferir, responda a estas perguntas que eu preencho §1–§4:
1. Existe um **id único e estável** por formando? Qual o nome/tipo do campo?
2. O Formattio tem **e-mail** e/ou **CPF** confiáveis do formando?
3. A integração seria por **API**, **export** ou **acesso a banco**? Há documentação?
4. O portal do formando **existe** hoje? Onde caberia uma aba de "dízimo/oferta"?
5. Como o Formattio autenticaria uma chamada ao Fidellis (por tenant)? Há credencial de serviço/OAuth?
6. Há **evento/campo** que sinalize quando o formando conclui/desiste (para pausar a recorrência)?
7. Quais campos do cadastro são **sensíveis** (convicção/formação/saúde)?

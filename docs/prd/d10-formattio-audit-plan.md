# D-10 — Plano de auditoria do cadastro/estrutura do Formattio (1º passo da integração)

> **Status:** plano (a executar) · 2026-09-12. É o **1º passo** da decisão **D-10** do
> [parecer](parecer-finance-terceiro-setor.md): *"auditar o cadastro/estrutura de dados do Formattio é o
> 1º passo do planejamento"*. **Não é implementação** — é levantamento que **alimenta o
> [ADR-0013 (LGPD)](../architecture/ADR-0013-lgpd-formattio-fidellis.md)** e habilita o desenho do adaptador.
> **Pré-condição:** acesso autorizado à instância/schema do Formattio (só metadados + amostra mínima).

---

## 1. Objetivo

Antes de desenhar qualquer integração, **entender o modelo de dados do Formattio** para responder: existe
um **id estável de membro**? Quais dados são **sensíveis** (LGPD art. 11)? Qual a **superfície de
exportação** (API, export, banco)? E, pela proposta refinada, qual a **superfície do portal do formando** e
o **mecanismo de auth** para a aba de dízimo/oferta? O resultado é o **insumo do RIPD** e do adaptador de
**importação de mão única** + **canal de arrecadação** (D-10).

**Regra de negócio confirmada (2026-09-12):** *existir vínculo de integração estabelecido ⇒ o formando é
membro*, e o Fidellis reconhece como verdadeiro. Consequências para a auditoria: não é preciso levantar/
importar um "flag de membro" — **o `id estável do formando` vira o `ExternalId`** e a origem (`Source=formattio`)
**já implica membro**. A membership é declarada no nível do **tenant-integração** (config), não inferida.
Isso dispensa qualquer dado de formação no Fidellis (só `id + origem`).

## 2. Princípios da auditoria

- **Só metadados + amostra mínima.** A auditoria mapeia **estrutura** (esquema, dicionário), não copia
  dados pessoais em volume. Qualquer amostra é pseudonimizada e descartada ao fim.
- **Mão única / autossuficiência.** O objetivo é um **adaptador opcional de importação**; o Fidellis não
  passa a depender do Formattio (D-10).
- **Minimização desde o desenho.** A meta é o **menor conjunto** que permita correspondência de membro —
  identidade federada (id + origem), não cópia de formação (ver ADR-0013).

## 3. Escopo — o que mapear

| Frente | O que levantar |
| --- | --- |
| **Entidades & relações** | Membro/pessoa, vínculos com comunidade/unidade, formação/turmas, sacramentos, papéis. Cardinalidades e chaves. |
| **Identidade do membro** | Existe **id estável e único** do formando? É reaproveitável como `ExternalId`? Há e-mail/CPF confiáveis para correspondência/dedupe? |
| **Portal do formando** | Como a aba de dízimo/oferta seria embutida/lançada (deep-link, iframe, redirect)? O que o portal exibe hoje e onde caberia a aba. |
| **Mecanismo de auth** | Como o Formattio autentica a chamada ao Fidellis em nome do formando: credencial de serviço **por tenant**? token assinado? Auth/rate limit da API do Formattio. |
| **Sinal de offboarding** | O Formattio consegue **sinalizar** quando um formando conclui/desiste (perde o vínculo)? Evento/webhook? campo de status? frequência? |
| **Classificação LGPD** | Marcar cada campo: comum × **sensível** (convicção religiosa, saúde, etc.). Identificar o mínimo necessário. |
| **Correspondência Fidellis** | Como casar o membro do Formattio com `Donor`/membership do Fidellis (por e-mail? doc? id?). Taxa de match esperada, duplicatas. |
| **Superfície técnica** | Há **API**? Export (CSV/JSON)? Acesso a banco? Autenticação, rate limits, formato, frequência de atualização. |
| **Qualidade** | Completude/consistência dos campos-chave (e-mail/doc), duplicidade, registros órfãos. |
| **Governança** | Controlador/operador, base legal da coleta original (formação), retenção, encarregado (DPO). |

## 4. Perguntas a responder (saída objetiva)

1. Qual campo será o **`ExternalId`** estável do formando no elo federado?
2. Qual o **conjunto mínimo** de campos (espera-se **só `id + origem`**, já que a origem implica membro)?
3. Qual a **melhor superfície** de importação (API × export) e sua cadência?
4. Que campos são **sensíveis** e, portanto, **não** devem ser materializados no Fidellis?
5. Como o Formattio **autentica** a chamada de arrecadação ao Fidellis (credencial por tenant × token)?
6. Como o Formattio **sinaliza o offboarding** (formando que perde o vínculo) para o Fidellis reagir à recorrência?

## 5. Método

1. Obter acesso autorizado + documentação/esquema do Formattio.
2. Levantar o **dicionário de dados** (tabelas/coleções, tipos, chaves) — sem extrair PII em volume.
3. **Classificar** cada campo (comum × sensível) e marcar o mínimo necessário.
4. Testar **correspondência** de membro numa **amostra pseudonimizada** (medir taxa de match e duplicatas).
5. Avaliar a **superfície de importação** (API/export, auth, formato, frequência).
6. Consolidar os **entregáveis** (§6).

## 6. Entregáveis

- **Dicionário de dados** do Formattio (estrutura + classificação LGPD por campo).
- **Mapa de correspondência** Formattio → Fidellis (`ExternalId`, chaves de match, taxa esperada).
- **Conjunto mínimo** proposto para o elo federado (esperado: `id + origem`).
- **Lista de dados sensíveis** que **não** entram no Fidellis.
- **Recomendação de superfície** (API × export) + cadência.
- **Contrato de auth** proposto para o canal de arrecadação (credencial por tenant × token assinado).
- **Mecanismo de offboarding** disponível (evento/webhook/status) para dirigir a recorrência.
- **Insumo para o RIPD** e para as pré-condições do [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md).

## 7. Riscos & pré-condições

- Sem **acesso autorizado**, a auditoria não começa. Amostras exigem base para tratamento (mesmo em teste).
- **Não** copiar dados sensíveis durante a auditoria; só estrutura + amostra mínima pseudonimizada.
- Se **não houver id estável** nem chave de match confiável, a integração pode ser **inviável/adiada** — a
  auditoria pode concluir por **não integrar** (fallback permanente aceitável — Fidellis é autossuficiente).

## 8. Próximos passos (depois da auditoria)

1. Concluir o **RIPD** e o **ADR-0013 (LGPD)** com base nos achados.
2. Só então desenhar: (a) a **identidade federada** (`Donor.ExternalId`/`Source` — hoje inexistentes),
   com `Source=formattio` **implicando membro** e a membership declarada por tenant; (b) o **canal de
   arrecadação** (server-to-server, credencial por tenant) autorizado a criar `dízimo`/`oferta`; (c) a
   **aba no portal do formando** que **lança** o fluxo do Fidellis (não armazena financeiro no Formattio).
3. Implementar a **regra de offboarding** (decidida — PO 2026-09-12): ao sinal de perda de vínculo, a
   recorrência **pausa** (`paused`, com aviso ao membro) e **encerra** (`canceled`) se definitivo. Depende
   do Formattio **sinalizar** a mudança de status (ver §3 "Sinal de offboarding").
4. Implementar com as salvaguardas do ADR-0013 (minimização, segregação, auditoria do elo).

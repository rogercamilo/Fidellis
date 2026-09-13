# Estudo (CTO) — Disponibilização da integração Fidellis ↔ Formattio

> **Status:** 📌 **estudo de apoio à decisão** · 2026-09-13 (rev. 2). Foco: **como disponibilizar** a
> integração (não precificar agora). Precificação fica **parqueada** — só princípios, para não travar a
> decisão. Consolida parecer, ADR-0013, RIPD, contrato de integração e a implementação já concluída (#80).
> **Fontes:** [parecer](../prd/parecer-finance-terceiro-setor.md) · [ADR-0013](../architecture/ADR-0013-lgpd-formattio-fidellis.md)
> · [contrato](../integrations/formattio-fidellis-contract.md).

---

## 1. Sumário executivo

- A pergunta central **não é preço**, é **como e para quem a integração fica disponível** e **como é ativada**.
- Existem **4 cenários de adoção** — e o que eu tinha deixado de fora é o mais importante: **a instituição
  nova que já entra na Kairós contratando Formattio + Fidellis juntos** (bundle desde o dia 1). Ele muda o
  desenho: nesse caso a integração deve ser **provisionada no onboarding conjunto**, sem "conectar duas
  contas" depois.
- **Recomendação de disponibilização:** integração **habilitável por instituição, sem cobrança por
  transação** (0% sobre doação intocável), **ligada por padrão no bundle Kairós** e **opcional/self-service**
  para quem já tem um dos produtos.
- **Dois modos de ativação**, conforme o cenário: **(a) provisionada no onboarding** (cliente novo dos dois)
  e **(b) "Conectar" self-service** (quem já tem um e adiciona o outro). Ambos preservam minimização,
  consentimento e segredos cifrados.

## 2. O que já temos (ativos)

Integração **#80 concluída nos dois lados** (identidade federada, give autenticado, offboarding, contrato) +
núcleo Fidellis (PIX 0%, dízimo recorrente/dunning, taxonomia, portal do membro, governança de conselho,
contabilidade/transparência, consolidação de rede). Falta só o **operacional de ativação** e a **decisão de
disponibilização** (este estudo). Precificação da plataforma: **ainda não decidida — e não é o foco agora**.

## 3. Cenários de adoção (núcleo da decisão)

| # | Cenário | Situação de dados | Ativação recomendada |
| --- | --- | --- | --- |
| **A** | **Novo cliente contrata Formattio + Fidellis juntos** (jornada Kairós desde o dia 1) | greenfield nos dois | **Provisionar no onboarding conjunto**: cria os dois tenants, **estabelece o elo automaticamente** (mapeamento e credencial já no setup), consentimento do membro no 1º acesso. **Zero passo de "conectar".** |
| **B** | Já tem **Formattio**, adiciona o Fidellis | membros existem no Formattio | **"Conectar" self-service** + **backfill inicial** dos membros (sync puxado). |
| **C** | Já tem **Fidellis**, adiciona o Formattio | doadores/membros existem no Fidellis | **"Conectar" self-service**; a partir daí o Formattio passa a ser origem federada; reconciliação por e-mail é heurística (vínculo é o `ExternalId`). |
| **D** | Tem os dois **separados** e decide integrar | dados dos dois lados | **"Conectar" self-service** + backfill + tratamento de duplicidade (relatório de conflitos). |

> **Cenário A é o mais estratégico e o de melhor experiência**: como o cadastro nasce junto, o mapeamento
> `organização ↔ tenant/unidade` é conhecido, a credencial é emitida no próprio provisionamento e o cliente
> **não vê complexidade**. É também o mais seguro em compliance (consentimento e minimização embutidos no
> desenho, não "colados" depois). **Deve ser o caminho dourado** que o marketing/onboarding empurra.

## 4. Formas de disponibilizar (opções de decisão — sem preço agora)

| Modelo | Como funciona | Prós | Contras |
| --- | --- | --- | --- |
| **Incluída (bundle Kairós)** | quem tem os dois, tem a integração ligada por padrão | máxima adoção, retenção, CX; reforça o bundle | não isola receita da integração |
| **Recurso opcional habilitável** | disponível a quem tem os dois, ligado por escolha do admin | controle/opt-in; bom p/ compliance (consentimento explícito) | 1 passo a mais |
| **Add-on cobrável** | cobra à parte pela integração | isola receita | atrito, contradiz "fácil e simples"; risco de esvaziar o bundle |
| **Gatilho de cross-sell** | quem tem só um vê a integração como motivo para adotar o outro | expansão de base | depende de ter os dois |

**Recomendação:** **Incluída no bundle (A)** + **opcional/self-service (B/C/D)**, **sem add-on cobrável por
transação**. A integração vale mais como **motor de adoção/retenção** do que como linha de receita isolada.

## 5. Monetização — princípios (parqueado; decidir depois)

Quando formos precificar, os princípios que **não** devem mudar:
- **0% sobre doação é intocável** (diferencial de marketing). Receita = **assinatura**, não transação.
- Precificar por **porte/funcionalidade**, **nunca por volume de doação** (não taxar generosidade; e não
  criar incentivo a vigiar arrecadação — coerente com o ADR-0013).
- A integração entra como **valor do bundle**, não como taxa. *(Faixas/valores: estudo próprio, no futuro.)*

## 6. Apelo comercial da integração

- **Dízimo recorrente do membro com alta adimplência** — assinado no portal onde o membro já vive (Formattio),
  cobrado/dunning no Fidellis: ataca a dor nº 1 (previsibilidade de caixa) **sem taxa sobre a doação**.
- **Cadastro único** (identidade federada) e **prestação de contas/transparência** herdadas — sem retrabalho.
- **Uma jornada só** para o membro → menos atrito para doar.
- Para o cenário A, isso vira **proposta de valor de entrada**: "comece a jornada Kairós com formação e
  finanças já conversando".

## 7. Processo funcional recomendado

### 7.1 Modo A — provisionado no onboarding (cliente novo dos dois)
No provisionamento conjunto: cria os dois tenants → **estabelece o elo automaticamente** (mapeia
organização↔unidade, emite e injeta a credencial server-to-server) → habilita a aba de contribuição
(consentimento do membro no 1º acesso). **O cliente não executa nenhum passo técnico.**

### 7.2 Modo B/C/D — "Conectar" self-service
Handshake sem SSO (mantém auth standalone): botão **Conectar** → **código de pareamento de uso único**
(expira em minutos) troca por credencial server-to-server (**a chave nunca aparece em tela/console** — corrige
o atrito do MVP atual) → **mapeamento automático por CNPJ/nome** com confirmação → **backfill** + **sync
automático** → **painel** de status/rotacionar/desconectar (desconectar = offboarding em massa com aviso).

### 7.3 Jornadas (CX)
- **Admin (A):** nada a fazer — já vem conectado.
- **Admin (B/C/D):** Conectar → confirmar mapeamento → pronto (~2 min). Sem env, sem console.
- **Membro:** portal → **Contribuir** → autoriza (1ª vez) → dízimo/valor/recorrência/forma de pagamento.
- **Offboarding:** inativar/excluir formando → recorrência pausa/encerra com aviso (automático).

## 8. Compliance & segurança (não negociável)

Minimização (só id+origem+nome+e-mail), **consentimento específico** antes do cruzamento, "Formattio lança,
não armazena", auditoria de todo toque do elo, **segredos cifrados + rotação + por tenant**, least-privilege.
Tudo ancorado em ADR-0013 + RIPD; termo de consentimento e acordo de compartilhamento já rascunhados. No
**cenário A**, consentimento e minimização ficam **embutidos no desenho do onboarding** (melhor postura).

## 9. Riscos & mitigações

| Risco | Mitigação |
| --- | --- |
| Ativação técnica travar adoção (B/C/D) | Fluxo "Conectar" self-service; e no A, provisionamento automático |
| Duplicidade de pessoas ao conectar bases existentes (D) | Vínculo por `ExternalId` (não e-mail) + relatório de conflitos |
| Percepção de "vigilância" do dízimo | Minimização + não-monetização por volume + consentimento transparente |
| Add-on cobrável esvaziar o bundle | Não cobrar por transação; integração como valor do bundle |
| Dependência de dois produtos | Integração desligável; Fidellis autossuficiente; contrato estável documentado |

## 10. Roadmap

- **P0 — feito:** integração completa (código) + ativação manual (piloto).
- **P1 — recomendado:** (a) **provisionamento no onboarding** para o cenário A; (b) **"Conectar" self-service**
  (código de pareamento, auto-map, agendador, painel) para B/C/D; UI de chave/rotação no Fidellis.
- **P2:** identidade unificada Kairós, sync por evento (webhooks), entitlements por plano quando houver preço.

## 11. Decisões tomadas (PO, 2026-09-13)

1. **Disponibilização:** **incluída no bundle Kairós + opcional/self-service** para quem tem só um produto. ✅
2. **Cenário A (novo cliente dos dois):** **caminho dourado — provisionar a integração no onboarding conjunto**
   (elo automático, sem passo técnico). ✅
3. **Modelo comercial:** **sem cobrança por transação / sem add-on** agora — 0% sobre doação intocável;
   precificação da plataforma parqueada para estudo próprio. ✅
4. **Próxima entrega:** **P1 completo** — provisionamento no onboarding (A) + fluxo "Conectar" self-service
   (B/C/D) + UI de geração/rotação de chave no Fidellis. ✅

> **Próximo passo:** detalhar o **P1 em specs implementáveis** (provisionamento no onboarding conjunto;
> connect code de uso único; auto-map por CNPJ/nome; agendador de sync; painel status/rotacionar/desconectar;
> UI de chave no Fidellis) + **one-pager comercial** do cenário A. Fluxo do projeto: detalhar → PO revisa → codar.

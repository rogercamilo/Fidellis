# PRD — Fidellis

Documento de requisitos de produto do Fidellis. Complementa a
[visão de arquitetura](../architecture/overview.md) e os ADRs.

## 1. Visão e problema

Instituições do terceiro setor religioso brasileiro (Novas Comunidades, Institutos Religiosos,
dioceses, congregações, paróquias) dependem de doações recorrentes (dízimos, ofertas) e de campanhas.
As plataformas atuais **cobram taxa por transação**, são fracas em **hierarquia de rede** e em
**prestação de contas**, e tratam o doador como um pagamento avulso, não como um relacionamento.

**Fidellis** é um SaaS multi-tenant que resolve isso com **assinatura de 0% de taxa**, hierarquia
**Rede→Unidade** de primeira classe, **CRM 360º do doador** e **prestação de contas automática**.

## 2. Público-alvo (ICP)

- **Redes:** dioceses, institutos, congregações que consolidam várias unidades.
- **Unidades:** paróquias, casas/comunidades, obras sociais.
- **Papéis:** gestor de rede, gestor de unidade, tesoureiro/contador, secretaria, e o **doador**.

## 3. Análise competitiva

| Critério                    | **Fidellis**                          | Doar Digital / Doare        | inChurch / Eklesia        |
| --------------------------- | ------------------------------------- | --------------------------- | ------------------------- |
| Modelo de cobrança          | **Assinatura, 0% sobre doações**      | ~2,9% + R$0,25 por transação| Varia / taxa + planos     |
| Hierarquia Rede→Unidade     | **Nativa** (consolidação da rede)     | Fraca                       | Parcial                   |
| CRM 360º do doador          | **Sim** (histórico, recorrência, régua)| Limitado                    | Foco em app da igreja     |
| PIX Automático + dunning    | **Roadmap central**                   | Parcial                     | Parcial                   |
| Prestação de contas/recibos | **Automática por unidade**            | Básica                      | Básica                    |
| Multi-tenant/isolamento     | **Schema-per-tenant**                 | n/d                         | n/d                       |

**Posição:** superar o **Doar Digital** eliminando a taxa por transação e entregando gestão de rede +
relacionamento com o doador que os concorrentes não priorizam.

## 4. Diferenciais de produto

1. **0% de taxa** sobre doações (só assinatura) — ver [ADR-0005](../architecture/ADR-0005-zero-fee-subscription-e-psp-pagarme.md).
2. **Hierarquia Rede→Unidade** com consolidação (diocese vê todas as paróquias).
3. **CRM 360º do doador** — histórico, recorrência, segmentação, régua de relacionamento.
4. **PIX Automático recorrente** com **dunning** (recuperação de falhas de cobrança).
5. **Recibos e prestação de contas automáticos** por unidade (transparência/LGPD).

## 5. Módulos do core

| Módulo       | Responsabilidade                                                        | Status scaffold |
| ------------ | ---------------------------------------------------------------------- | --------------- |
| **Tenant**   | Registro/provisionamento de instituições (schema `catalog` + `t_<slug>`)| **Funcional** (criar/listar tenant, provisiona schema) |
| **Donations**| Organizations, doações, **CRM 360º do doador** + régua de relacionamento | **Funcional** — CRM (histórico/situação) + outbox/e-mail (Resend) |
| **Finance**  | Cobrança PIX (Pagar.me), webhook idempotente, conciliação, split, **recorrência + dunning** | **Funcional (PIX)** — checkout, webhook, partida dobrada, dízimo mensal + dunning |
| **Accounting**| Plano de contas, razão/balancete, recibos automáticos                 | **Funcional** — plano de contas, partida dobrada, balancete consolidado, recibos |
| **Reporting**| Dashboards, série temporal, consolidação da rede                      | **Funcional** — overview, série mensal, consolidação por unidade (Recharts) |
| **Audit**    | Trilha de auditoria + LGPD (export/anonimização/opt-out)              | **Funcional** — audit_log + LGPD; portal público do doador |

## 6. Requisitos funcionais (alto nível)

- **RF-01 Identidade global:** login por e-mail resolve o(s) tenant(s) do usuário (memberships/RBAC).
- **RF-02 Provisionamento de tenant:** criar instituição cria schema isolado + tabelas.
- **RF-03 Contexto de tenant:** toda operação de dados ocorre no schema do tenant do request.
- **RF-04 Doações:** receber doação via **PIX** (checkout com QR + conciliação por webhook) e
  **dízimo recorrente mensal** com dunning; cartão/boleto e PIX Automático (mandato) seguem no roadmap.
- **RF-05 Repasse/split:** doação vai 100% para a unidade (recebedor Pagar.me por unidade);
  consolidação da rede fica no Reporting.
- **RF-06 Recibos/prestação de contas:** recibo automático por doação (número sequencial, HTML
  imprimível) + lançamento contábil (partida dobrada) contra o plano de contas; balancete/razão.
- **RF-07 Relatórios (roadmap):** dashboard por unidade e consolidado da rede; exportações.
- **RF-08 Auditoria/LGPD:** trilha de ações sensíveis (`audit_log`) + direitos do titular
  (exportação, anonimização/erasure, opt-out de comunicação) + portal público do doador.

## 7. Requisitos não-funcionais

- **Isolamento & LGPD:** dados por instituição isolados por schema; export/backup por tenant.
- **Segurança:** hash Argon2; JWT assinado; segredos fora do versionamento; WAF/rate limiting na borda.
- **Confiabilidade financeira:** idempotência de webhook (`payment_events`); reconsulta ao PSP como
  fonte de verdade; conciliação PIX com partida dobrada. Recorrência/dunning no roadmap.
- **Observabilidade:** health `live`/`ready` em BFF e core; logs estruturados (roadmap: tracing).
- **Performance:** cache/fila em Redis; front na borda (Cloudflare).
- **Portabilidade:** monorepo com CI reprodutível (Node e .NET).

## 8. Roadmap (pós-scaffold)

1. ✅ **Cobrança real via PIX** (módulo Finance): checkout PIX, webhook idempotente, conciliação e
   split 100% p/ a unidade. **Entregue.** (Boleto/cartão desenhados na abstração Order/Charge.)
   Ver [ADR-0006](../architecture/ADR-0006-payments-pix-pagarme.md).
2. ✅ **Recorrência (dízimo mensal) + dunning** — motor próprio no core (scheduler multi-tenant),
   cobrança PIX por ciclo, régua D+1/D+3/D+5 → `past_due`; encaixe pronto p/ PIX Automático (mandato).
   **Entregue.** Ver [ADR-0007](../architecture/ADR-0007-recurring-donations-dunning.md).
3. ✅ **Razão contábil + recibos** (módulo Accounting) — plano de contas configurável, partida dobrada
   contra o plano, balancete/razão consolidados (Rede→Unidade) e recibo automático (HTML imprimível).
   **Entregue.** Ver [ADR-0009](../architecture/ADR-0009-accounting-receipts.md).
4. ✅ **CRM do doador + régua de relacionamento** — CRM 360º (histórico/situação), outbox idempotente,
   e-mail real (Resend), gatilhos (agradecimento/dunning/past_due) + reativação de inativo; WhatsApp
   desenhado (stub). **Entregue.** Ver [ADR-0010](../architecture/ADR-0010-crm-relationship-outbox.md).
5. ✅ **Dashboards + consolidação da rede** (módulo Reporting) — overview, série temporal mensal,
   consolidação por unidade e quebra por método (Recharts). **Entregue.**
   Ver [ADR-0011](../architecture/ADR-0011-reporting-dashboards.md).
6. ✅ **Portal do doador + auditoria/LGPD** — doação pública (`/doar/<tenant>`) + link mágico
   (`/portal/<tenant>`), trilha de auditoria e LGPD (export/anonimização/opt-out). **Entregue.**
   Ver [ADR-0012](../architecture/ADR-0012-donor-portal-audit-lgpd.md).

> **Roadmap do PRD concluído (passos 1–6).** Evoluções futuras: WhatsApp real, PDF/R2 de recibos,
> rate limiting no público, portal com login do doador, migrações EF versionadas (ADR-0002),
> exportações/agendamento de relatórios.

## 9. Fora de escopo do primeiro entregável

Toda a lógica de negócio profunda dos módulos acima. O primeiro entregável é o **scaffold rodável +
arquitetura documentada** (multi-tenant, auth, CI, ADRs).

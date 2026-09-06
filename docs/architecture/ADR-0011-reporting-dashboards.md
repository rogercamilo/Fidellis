# ADR-0011 — Reporting: dashboards + consolidação da rede

- **Status:** Aceito (2026-09-05)
- **Contexto:** passo 5 do PRD. Reusa a árvore Rede→Unidade (`OrgVisibility`) e as doações pagas.

## Contexto

Faltava a visão gerencial: quanto se arrecada, por período e **por unidade** (consolidação da rede).
O Painel calculava KPIs somando listas no cliente — impreciso e não escalável.

## Decisão

- **Módulo Reporting** com endpoints agregados (base em doações `paid`, escopo nas unidades visíveis):
  `GET /api/reporting/overview` (arrecadado, nº, ticket médio, doadores ativos, recorrências ativas,
  quebra por método), `GET /timeseries?months=12` (série mensal) e `GET /by-unit` (consolidação).
- **`ReportingCalc.MonthlySeries`** — cálculo puro/testável da série mensal com **zero-fill**.
- **Gráficos com Recharts** no web; página **/dashboard/relatorios** (KPIs + barra mensal + pizza por
  método + tabela de consolidação). O **Painel** passa a consumir `overview` (KPIs reais).
- **Consolidação da rede:** agrega por `organization_id` sobre a subárvore visível (`OrgVisibility`),
  reaproveitando a regra Rede→Unidade do [ADR-0008](ADR-0008-user-org-membership.md).

## Alternativas consideradas

- **Gráfico SVG próprio (sem dep):** mais leve, mas o usuário optou por **Recharts** (mais recursos).
- **Materializar agregados (tabelas de rollup):** melhor em escala, porém prematuro — as consultas
  sobre `donations` atendem o volume atual; cache/rollup fica no roadmap.

## Consequências

- **Positivas:** dashboards reais, consolidação por unidade e KPIs consistentes; cálculo de série
  testável; base para exportações e comparativos.
- **Negativas / trade-offs:** agregação on-the-fly (sem cache) pode custar em bases grandes;
  Recharts adiciona ~100 kB à rota de relatórios; exportação (CSV/PDF), drill-down e comparativos de
  período ficam no roadmap.

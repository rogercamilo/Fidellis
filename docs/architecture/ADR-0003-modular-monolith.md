# ADR-0003 — Core como monólito modular (.NET 10)

- **Status:** Aceito (2026-09-04)

## Contexto

O domínio do Fidellis tem módulos com forte coesão transacional (uma doação gera transação e
lançamentos contábeis, com recibo e auditoria). Microsserviços desde o início trariam sobrecarga de
consistência distribuída e operação, sem benefício claro no MVP.

## Decisão

Implementar o core como **monólito modular** em .NET 10, com fronteiras explícitas por módulo:

- **Tenant** — registro/provisionamento de instituições (opera no `catalog`).
- **Donations** — campanhas, doações, doadores.
- **Finance** — orquestração de pagamento/repasse (integra o PSP).
- **Accounting** — razão contábil, recibos, prestação de contas.
- **Reporting** — dashboards, exportações, consolidação da rede.
- **Audit** — trilha de auditoria/LGPD.

Projetos de suporte: `SharedKernel` (primitivos: `ITenantContext`, `Result`, `Entity`) e
`Infrastructure` (EF Core + Npgsql, resolução de schema, provisionador, Redis). Cada módulo se
registra por DI e expõe seus endpoints; a composição fica no host `Fidellis.Api`.

## Consequências

- **Positivas:** transações locais (sem saga) no MVP; deploy único simples; fronteiras de módulo
  preparam uma eventual extração para serviços; testabilidade por módulo.
- **Negativas / trade-offs:** disciplina necessária para não vazar dependências entre módulos;
  escala é por processo inteiro (não por módulo).
- **Regras:** módulos dependem de `SharedKernel` e `Infrastructure`, **não** uns dos outros; o host é
  o único ponto de composição. Extração para serviço só quando houver necessidade real de escala/time.

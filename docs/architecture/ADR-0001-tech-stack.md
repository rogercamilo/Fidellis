# ADR-0001 — Stack tecnológica

- **Status:** Aceito (2026-09-04)
- **Contexto:** primeiro entregável do Fidellis (scaffold + arquitetura).

## Contexto

Precisamos de uma stack que (a) suporte multi-tenancy financeiro com forte consistência, (b) permita
um front moderno na borda, (c) reaproveite o conhecimento "padrão da casa" e (d) escale de um MVP a
uma operação com dioceses/redes consolidando várias unidades.

## Decisão

Adotar a stack em camadas:

| Camada        | Tecnologia                              | Papel                                             |
| ------------- | --------------------------------------- | ------------------------------------------------- |
| Borda         | **Cloudflare** (DNS + CDN + WAF)        | TLS, cache, proteção, roteamento por subdomínio   |
| Front         | **Next.js** (React/TypeScript)          | Landing, painel, portal do doador (roadmap)       |
| BFF           | **NestJS**                              | Auth standalone, contexto de tenant, proxy        |
| Core          | **.NET 10** (monólito modular)          | Domínio: doações, finanças, contabilidade         |
| Dados         | **PostgreSQL** + **Redis**              | Persistência schema-per-tenant + cache/fila       |
| Storage       | **Cloudflare R2 / S3**                  | Recibos, anexos, assets                           |
| Pagamentos    | **Pagar.me / Stone**                    | PIX, cartão, boleto, split (ver [ADR-0005])       |

## Consequências

- **Positivas:** camadas com responsabilidades claras; front na borda com baixa latência; core .NET
  robusto para regras financeiras/contábeis; Postgres oferece isolamento por schema (ver
  [ADR-0002](ADR-0002-schema-per-tenant.md)).
- **Negativas / trade-offs:** três runtimes (Node no web+bff, .NET no core) aumentam a superfície de
  build/CI; exige disciplina de contrato entre BFF e core (JWT compartilhado, DTOs).
- **Mitigações:** monorepo (pnpm workspaces + Turborepo) e dois pipelines de CI; segredo JWT único
  entre BFF e core.

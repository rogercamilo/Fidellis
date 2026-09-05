# Cloudflare — Fidellis

Camada de borda do Fidellis: **DNS + CDN + WAF** na frente do `web` (Next.js) e do `bff` (NestJS).
O core `.NET` fica em rede privada, acessível apenas pelo BFF.

## Componentes

| Recurso              | Uso                                                                 |
| -------------------- | ------------------------------------------------------------------- |
| DNS                  | Zona `fidellis.com.br` + subdomínios de tenant `*.fidellis.com.br`  |
| CDN                  | Cache de assets estáticos do `web`                                  |
| WAF                  | Regras OWASP + rate limiting nos endpoints de auth/doação           |
| Pages / Workers      | Deploy do `web` (Next.js via OpenNext) e opcionalmente do `bff`     |
| R2                   | Storage de assets/recibos (bucket `fidellis-assets`)                |

## Notas de deploy (a detalhar em entregável futuro)

- `web`: build Next.js → `@opennextjs/cloudflare` → Workers/Pages. Ver `apps/web/wrangler.jsonc`.
- Multi-tenant por subdomínio: `<tenant>.fidellis.com.br` resolve para o mesmo `web`; o slug do
  tenant é lido do host e propagado ao BFF.
- Segredos (`JWT_SECRET`, `PAGARME_API_KEY`, credenciais R2) via `wrangler secret put` / Secrets
  Store — nunca versionados.

# ADR-0009 — Contabilidade (plano de contas + razão) + recibos automáticos

- **Status:** Aceito (2026-09-05)
- **Contexto:** passo 3 do PRD. Formaliza a partida dobrada já criada na conciliação
  ([ADR-0006](ADR-0006-payments-pix-pagarme.md)) e emite recibos de doação.

## Contexto

A conciliação (passo 1) já lançava débito/crédito, mas com contas em **texto livre** e sem recibo. O
terceiro setor precisa de **prestação de contas**: plano de contas, razão/balancete e **recibos**.

## Decisão

- **Plano de contas configurável** (`ledger_accounts`, schema do tenant): `code, name, type, normal_balance,
  postable, parent_id`. Semeado por tenant no onboarding (`ChartOfAccountsSeeder.EnsureDefaultAsync`,
  idempotente) e editável por endpoints. Os lançamentos passam a referenciar `ledger_account_id`
  (mantendo `ledger` como rótulo). Códigos bem-conhecidos da conciliação: **`1.1.3` PIX a receber** e
  **`4.1.1` Dízimos e ofertas** (`ChartOfAccounts`).
- **Recibo automático** (`receipts`, schema do tenant) emitido na conciliação do webhook
  (`ReceiptService`, idempotente por doação), com **número sequencial por organização/ano**
  (`{ano}/{seq:000000}`). Entrega em **HTML imprimível** (`/recibo/{id}` no web; o navegador gera o PDF).
- **Relatórios:** `GET /api/accounting/trial-balance` (balancete) e `/ledger` (razão por conta), com
  **consolidação da subárvore Rede→Unidade** (reusa `OrgVisibility`, movido para
  `Fidellis.Infrastructure.Organizations` para ser compartilhado entre módulos sem violar o ADR-0003).
- **Fronteiras (ADR-0003):** os primitivos contábeis (`ChartOfAccounts`/`Seeder`, `ReceiptService`,
  entidades) vivem na **Infrastructure**; Finance (conciliação) e Accounting (relatórios) os consomem
  sem depender um do outro.

## Alternativas consideradas

- **Manter razão em texto livre + só relatórios:** mais simples, mas sem plano configurável nem base
  para DRE/Balanço — preterido (o usuário optou pelo plano configurável).
- **PDF no servidor + R2/S3:** melhor arquivamento, porém adiciona lib de PDF + storage — adiado; o
  HTML imprimível cobre a necessidade imediata.

## Consequências

- **Positivas:** prestação de contas real (razão, balancete, recibos numerados); plano evolutivo por
  tenant; consolidação por unidade/rede; recibo idempotente (não duplica em reentrega do webhook).
- **Negativas / trade-offs:** numeração de recibo por contagem+ano (com unicidade em
  `(organization_id, number)`) pode exigir retry sob concorrência alta — aceitável no volume atual;
  DRE/Balanço formais, fechamento de exercício e PDF ficam no roadmap.

using System.Text;
using Fidellis.Infrastructure;
using Fidellis.Infrastructure.Audit;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Security;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Banking;
using Fidellis.Modules.Finance.Budgeting;
using Fidellis.Modules.Finance.Campaigns;
using Fidellis.Modules.Finance.CashSessions;
using Fidellis.Modules.Finance.Configuration;
using Fidellis.Modules.Finance.Dimensions;
using Fidellis.Modules.Finance.Entries;
using Fidellis.Modules.Finance.Invoicing;
using Fidellis.Modules.Finance.Payables;
using Fidellis.Modules.Finance.Periods;
using Fidellis.Modules.Finance.Reports;
using Fidellis.Modules.Finance.Receivables;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.Modules.Finance.Treasury;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Finance;

/// <summary>
/// Módulo Finance — cobrança real de doações via PIX (Pagar.me): checkout, consulta de status,
/// recebedores (split Rede→Unidade) e o receptor de webhook idempotente que faz a conciliação.
/// </summary>
public static class FinanceModule
{
    public static IServiceCollection AddFinanceModule(this IServiceCollection services)
    {
        services.AddScoped<ReconciliationService>();
        services.AddScoped<DonationCheckoutService>();
        services.AddScoped<WebhookProcessor>();
        services.AddScoped<RecipientService>();
        services.AddScoped<RecurringBillingService>();
        services.AddScoped<DonationExpiryService>();
        services.AddScoped<TreasuryService>();
        services.AddScoped<CashFlowService>();
        services.AddScoped<ReceivablesService>();
        services.AddScoped<ReceivablesReminderService>();
        services.AddScoped<PayablesService>();
        services.AddScoped<ApprovalService>();
        services.AddScoped<CashSessionService>();
        services.AddScoped<ManualEntryService>();
        services.AddScoped<CampaignService>();
        services.AddScoped<PeriodService>();
        services.AddScoped<StatementImportService>();
        services.AddScoped<ReconciliationMatchService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<StatementsService>();
        services.AddScoped<StatementSnapshotService>();
        services.AddScoped<VolunteerWorkService>();
        services.AddScoped<MroscReportService>();
        services.AddScoped<AccountantExportService>();
        services.AddScoped<FiscalDocumentService>();
        services.AddScoped<IntegrationCredentialService>();
        services.AddScoped<IntegrationOffboardService>();
        services.AddScoped<Security.TeamService>();
        services.AddScoped<Security.InvitationService>();
        services.AddScoped<Notifications.INotifier, Notifications.OutboxNotifier>();
        return services;
    }

    public static IEndpointRouteBuilder MapFinanceModule(this IEndpointRouteBuilder app)
    {
        // RBAC financeiro (RF-FIN-171): bloqueia gravações de perfis somente-leitura.
        var group = app.MapGroup("/api/finance").WithTags("Finance").AddEndpointFilter<FinanceWriteFilter>();

        group.MapGet("/ping", (ITenantContext tenant) =>
            Results.Ok(new { module = "Finance", tenant = tenant.TenantId, schema = tenant.SchemaName }));

        // Cria uma cobrança PIX (gestor autenticado; tenant vem do JWT).
        group.MapPost("/donations", async (
            CreateDonationRequest req,
            HttpRequest request,
            DonationCheckoutService checkout,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            if (!tenant.HasTenant)
                return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (req.Amount <= 0)
                return Results.BadRequest(new { error = "amount deve ser positivo." });
            if (req.Donor is null || string.IsNullOrWhiteSpace(req.Donor.Name) || string.IsNullOrWhiteSpace(req.Donor.Document))
                return Results.BadRequest(new { error = "donor.name e donor.document são obrigatórios (PIX/boleto/cartão exigem CPF/CNPJ)." });
            var method = (req.Method ?? "pix").Trim().ToLowerInvariant();
            if (method is not ("pix" or "boleto" or "card"))
                return Results.BadRequest(new { error = "method deve ser 'pix', 'boleto' ou 'card'." });
            if (method == "card" && string.IsNullOrWhiteSpace(req.CardToken))
                return Results.BadRequest(new { error = "cardToken é obrigatório para pagamento com cartão." });

            // Regra de negócio: a instituição só gera cobrança de DOAÇÃO. Dízimo/oferta são do membro
            // (portal/Formattio, por iniciativa dele) ou registrados como recebidos (caixa/manual) — nunca
            // "cobrados" pelo operador. Por isso o tipo é forçado a `donation` aqui.
            var result = await checkout.CreateAsync(new CheckoutCommand(
                req.OrganizationId, req.Amount, req.Donor.Name, req.Donor.Email ?? "", req.Donor.Document,
                req.CampaignId, req.Description, IdempotencyKey: request.Headers["Idempotency-Key"].FirstOrDefault(),
                Method: method, CardToken: req.CardToken, EntryType: EntryTypes.Donation), ct);

            return Results.Created($"/api/finance/donations/{result.DonationId}", result);
        });

        // Status de uma doação (reconsulta o PSP p/ exibição quando ainda pendente).
        group.MapGet("/donations/{id:guid}", async (
            Guid id,
            TenantDbContext db,
            IPaymentGateway gateway,
            CancellationToken ct) =>
        {
            var d = await db.Entries.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null) return Results.NotFound();

            string? pspStatus = null;
            if (d.Status == "pending" && d.PspChargeId is { Length: > 0 } chargeId)
            {
                try { pspStatus = (await gateway.GetChargeAsync(chargeId, ct)).Status; }
                catch { /* exibição best-effort; webhook é a fonte de verdade */ }
            }

            return Results.Ok(new
            {
                id = d.Id,
                status = d.Status,
                pspStatus,
                amount = d.Amount,
                qrCode = d.PixQrCode,
                qrCodeUrl = d.PixQrCodeUrl,
                expiresAt = d.ExpiresAt,
                paidAt = d.PaidAt,
            });
        });

        // Cadastra o recebedor (destino do split) de uma unidade.
        group.MapPost("/recipients", async (
            CreateRecipientHttpRequest req,
            RecipientService recipients,
            ITenantContext tenant,
            IAuditLog audit,
            CancellationToken ct) =>
        {
            if (!tenant.HasTenant)
                return Results.BadRequest(new { error = "Nenhum tenant no request." });

            var result = await recipients.CreateAsync(
                req.OrganizationId, req.Name, req.Email, req.Document, req.PixKey, ct);
            await audit.RecordAsync("recipient.created", "psp_recipient", result.Id.ToString());
            return Results.Created($"/api/finance/recipients/{result.Id}", result);
        });

        // ---- Integração federada (#80 / ADR-0013 dec.9): credencial de serviço por tenant ----

        // Emite/rotaciona a chave da origem (ex.: formattio). O valor em claro é retornado UMA vez.
        group.MapPost("/integration/{source}/key", async (
            string source, IntegrationCredentialService creds, ITenantContext tenant, IAuditLog audit, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var key = await creds.IssueAsync(source, ct);
            await audit.RecordAsync("integration.key_issued", "integration_credential", source.Trim().ToLowerInvariant());
            return Results.Ok(new { source = source.Trim().ToLowerInvariant(), key });
        });

        group.MapGet("/integration/{source}", async (string source, IntegrationCredentialService creds, CancellationToken ct) =>
        {
            var (configured, enabled) = await creds.StatusAsync(source, ct);
            return Results.Ok(new { source = source.Trim().ToLowerInvariant(), configured, enabled });
        });

        // ---- Doação recorrente do apoiador (não-membro) + dunning ----
        // Recorrência de DÍZIMO é indicada pelo próprio membro no portal (member/pledge); a instituição
        // só monta doação recorrente de apoiador aqui (regra: operador não cobra dízimo/oferta).

        group.MapPost("/recurring-donations", async (
            CreateRecurringRequest req,
            TenantDbContext db,
            RecurringBillingService billing,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            if (!tenant.HasTenant)
                return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (req.Amount <= 0)
                return Results.BadRequest(new { error = "amount deve ser positivo." });
            if (req.Donor is null || string.IsNullOrWhiteSpace(req.Donor.Name))
                return Results.BadRequest(new { error = "donor.name é obrigatório." });

            var donor = await db.Donors.FirstOrDefaultAsync(d => d.Email != null && d.Email == req.Donor.Email, ct);
            if (donor is null)
            {
                donor = new Donor { Name = req.Donor.Name, Email = req.Donor.Email, Document = req.Donor.Document };
                db.Donors.Add(donor);
                await db.SaveChangesAsync(ct);
            }

            // Força doação: o operador só monta doação recorrente do apoiador; dízimo é do membro.
            var r = await billing.CreatePledgeAsync(
                req.OrganizationId, donor.Id, req.Amount, req.DayOfMonth, req.ChargeToday ?? true,
                entryType: EntryTypes.Donation, ct: ct);
            return Results.Created($"/api/finance/recurring-donations/{r.Id}", ToRecurringDto(r));
        });

        group.MapGet("/recurring-donations", async (TenantDbContext db, CancellationToken ct) =>
        {
            var list = await db.RecurringDonations
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RecurringDto(r.Id, r.OrganizationId, r.Amount, r.DayOfMonth, r.Status, r.NextChargeAt, r.Attempt))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        group.MapPost("/recurring-donations/{id:guid}/pause", async (Guid id, RecurringBillingService billing, CancellationToken ct) =>
            await billing.PauseAsync(id, ct) is { } r ? Results.Ok(ToRecurringDto(r)) : Results.NotFound());

        group.MapPost("/recurring-donations/{id:guid}/resume", async (Guid id, RecurringBillingService billing, CancellationToken ct) =>
            await billing.ResumeAsync(id, ct) is { } r ? Results.Ok(ToRecurringDto(r)) : Results.NotFound());

        group.MapPost("/recurring-donations/{id:guid}/cancel", async (Guid id, RecurringBillingService billing, CancellationToken ct) =>
            await billing.CancelAsync(id, ct) is { } r ? Results.Ok(ToRecurringDto(r)) : Results.NotFound());

        // ---- Público (doador anônimo; tenant pelo path) ----
        // Rate limiting por IP+tenant (RF-FIN-002) aplicado a todo o grupo público.
        var pub = app.MapGroup("/api/public/{tenant}").WithTags("Public").RequireRateLimiting("public");

        pub.MapPost("/donations", async (
            string tenant, CreateDonationRequest req, HttpRequest request,
            CatalogDbContext catalog, ITenantContext tc, DonationCheckoutService checkout, IAuditLog audit,
            CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound(new { error = "Instituição não encontrada." });
            if (req.Amount <= 0)
                return Results.BadRequest(new { error = "amount deve ser positivo." });
            if (req.Donor is null || string.IsNullOrWhiteSpace(req.Donor.Name) || string.IsNullOrWhiteSpace(req.Donor.Document))
                return Results.BadRequest(new { error = "donor.name e donor.document são obrigatórios." });
            var method = (req.Method ?? "pix").Trim().ToLowerInvariant();
            if (method is not ("pix" or "boleto" or "card"))
                return Results.BadRequest(new { error = "method deve ser 'pix', 'boleto' ou 'card'." });
            if (method == "card" && string.IsNullOrWhiteSpace(req.CardToken))
                return Results.BadRequest(new { error = "cardToken é obrigatório para pagamento com cartão." });

            // Gating por ator (D-06): o portal público só aceita DOAÇÃO (não-membro). Dízimo/oferta
            // (só membro) são lançados pelo dashboard/caixa/manual — não pelo checkout anônimo.
            var result = await checkout.CreateAsync(new CheckoutCommand(
                req.OrganizationId, req.Amount, req.Donor.Name, req.Donor.Email ?? "", req.Donor.Document,
                req.CampaignId, req.Description, IdempotencyKey: request.Headers["Idempotency-Key"].FirstOrDefault(),
                Method: method, CardToken: req.CardToken, EntryType: EntryTypes.Donation), ct);
            await audit.RecordAsync("donation.public_checkout", "donation", result.DonationId.ToString());
            return Results.Created($"/api/public/{tenant}/donations/{result.DonationId}", result);
        });

        pub.MapGet("/donations/{id:guid}", async (
            string tenant, Guid id, CatalogDbContext catalog, ITenantContext tc, TenantDbContext db, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound();
            var d = await db.Entries.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null) return Results.NotFound();
            return Results.Ok(new { id = d.Id, status = d.Status, qrCode = d.PixQrCode, qrCodeUrl = d.PixQrCodeUrl, expiresAt = d.ExpiresAt, amount = d.Amount });
        });

        // Portal de transparência (Onda 4 inc.4.4): resumo público consolidado trimestral, sem dados pessoais.
        pub.MapGet("/transparency", async (
            string tenant, int? year, int? quarter,
            CatalogDbContext catalog, ITenantContext tc, Services.StatementsService statements, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound(new { error = "Instituição não encontrada." });
            var y = year ?? DateTimeOffset.UtcNow.Year;
            var q = quarter is >= 1 and <= 4 ? quarter : null;
            return Results.Ok(await statements.TransparencyAsync(y, q, ct));
        });

        // Campanhas públicas (D-08): lista das ativas + detalhe com progresso (arrecadado × meta).
        pub.MapGet("/campaigns", async (
            string tenant, CatalogDbContext catalog, ITenantContext tc, Services.CampaignService campaigns, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound(new { error = "Instituição não encontrada." });
            return Results.Ok(await campaigns.ListAsync(activeOnly: true, ct));
        });

        pub.MapGet("/campaigns/{slug}", async (
            string tenant, string slug, CatalogDbContext catalog, ITenantContext tc, Services.CampaignService campaigns, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound(new { error = "Instituição não encontrada." });
            var c = await campaigns.GetBySlugAsync(slug, ct);
            return c is { Active: true } ? Results.Ok(c) : Results.NotFound(new { error = "Campanha não encontrada." });
        });

        // ---- Autoatendimento do MEMBRO (#75): autenticado por link mágico do doador (DonorMagicToken).
        // Sendo membro (Donor.IsMember), libera dízimo/oferta (pontual) e dízimo recorrente — ao contrário
        // do checkout anônimo, restrito a doação (gating D-06).
        pub.MapPost("/member/give", async (
            string tenant, MemberGiveRequest req, HttpRequest request,
            CatalogDbContext catalog, ITenantContext tc, TenantDbContext db,
            DonationCheckoutService checkout, InfrastructureOptions options, IAuditLog audit, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound(new { error = "Instituição não encontrada." });
            var member = await ResolveMemberAsync(db, req.Token, tenant, options, ct);
            if (member is null)
                return Results.Json(new { error = "Sessão de membro inválida ou expirada." }, statusCode: StatusCodes.Status401Unauthorized);
            if (!member.IsMember)
                return Results.Json(new { error = "Apenas membros dão dízimo/oferta." }, statusCode: StatusCodes.Status403Forbidden);
            if (req.Amount <= 0)
                return Results.BadRequest(new { error = "amount deve ser positivo." });

            var entryType = EntryTypes.IsValid(req.EntryType) ? req.EntryType : EntryTypes.Offering;
            var method = (req.Method ?? "pix").Trim().ToLowerInvariant();
            if (method is not ("pix" or "boleto")) method = "pix"; // cartão exige tokenização no front
            var result = await checkout.CreateAsync(new CheckoutCommand(
                req.OrganizationId, req.Amount, member.Name, member.Email ?? "", member.Document ?? "",
                IdempotencyKey: request.Headers["Idempotency-Key"].FirstOrDefault(), Method: method, EntryType: entryType), ct);
            await audit.RecordAsync("member.give", "donation", result.DonationId.ToString());
            return Results.Created($"/api/public/{tenant}/donations/{result.DonationId}", result);
        });

        pub.MapPost("/member/pledge", async (
            string tenant, MemberPledgeRequest req,
            CatalogDbContext catalog, ITenantContext tc, TenantDbContext db,
            RecurringBillingService billing, InfrastructureOptions options, IAuditLog audit, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound(new { error = "Instituição não encontrada." });
            var member = await ResolveMemberAsync(db, req.Token, tenant, options, ct);
            if (member is null)
                return Results.Json(new { error = "Sessão de membro inválida ou expirada." }, statusCode: StatusCodes.Status401Unauthorized);
            if (!member.IsMember)
                return Results.Json(new { error = "Apenas membros assinam dízimo recorrente." }, statusCode: StatusCodes.Status403Forbidden);
            if (req.Amount <= 0)
                return Results.BadRequest(new { error = "amount deve ser positivo." });

            // O membro indica a forma de pagamento da recorrência (PIX ou boleto; cartão exige tokenização por ciclo).
            var pledgeMethod = (req.Method ?? "pix").Trim().ToLowerInvariant() is "boleto" ? "boleto" : "pix";
            var r = await billing.CreatePledgeAsync(
                req.OrganizationId, member.Id, req.Amount, req.DayOfMonth, chargeToday: true,
                entryType: EntryTypes.Tithe, method: pledgeMethod, ct: ct);
            await audit.RecordAsync("member.pledge", "recurring_donation", r.Id.ToString());
            return Results.Created($"/api/public/{tenant}/member/pledge/{r.Id}",
                new { id = r.Id, amount = r.Amount, dayOfMonth = r.DayOfMonth, status = r.Status, nextChargeAt = r.NextChargeAt, method = r.Method });
        });

        // ---- Canal da integração federada (#80 / ADR-0013 dec.9): server-to-server, autenticado por
        // credencial de serviço por tenant (header X-Integration-Key). Autoriza dízimo/oferta de MEMBRO
        // federado (resolvido por ExternalId) — "Formattio lança, não armazena". O membro é reconhecido
        // pela origem (Source=formattio ⇒ membro); o canal anônimo público segue restrito a doação.
        pub.MapPost("/integration/give", async (
            string tenant, IntegrationGiveRequest req, HttpRequest request,
            CatalogDbContext catalog, ITenantContext tc, TenantDbContext db,
            IntegrationCredentialService creds, DonationCheckoutService checkout, IAuditLog audit, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound(new { error = "Instituição não encontrada." });
            if (!await creds.ValidateAsync("formattio", request.Headers["X-Integration-Key"].FirstOrDefault(), ct))
                return Results.Json(new { error = "Credencial de integração inválida." }, statusCode: StatusCodes.Status401Unauthorized);
            var member = await ResolveFederatedMemberAsync(db, req.ExternalId, ct);
            if (member is null)
                return Results.NotFound(new { error = "Membro federado não encontrado (importe a identidade primeiro)." });
            if (req.Amount <= 0 || req.OrganizationId == Guid.Empty)
                return Results.BadRequest(new { error = "organizationId e amount (>0) são obrigatórios." });

            var entryType = req.EntryType is EntryTypes.Tithe or EntryTypes.Offering ? req.EntryType : EntryTypes.Tithe;
            var method = (req.Method ?? "pix").Trim().ToLowerInvariant() is "boleto" ? "boleto" : "pix";
            var result = await checkout.CreateAsync(new CheckoutCommand(
                req.OrganizationId, req.Amount, member.Name, member.Email ?? "", member.Document ?? "",
                Method: method, EntryType: entryType), ct);
            await audit.RecordAsync("integration.give", "donation", result.DonationId.ToString());
            return Results.Created($"/api/public/{tenant}/donations/{result.DonationId}", result);
        });

        pub.MapPost("/integration/pledge", async (
            string tenant, IntegrationPledgeRequest req, HttpRequest request,
            CatalogDbContext catalog, ITenantContext tc, TenantDbContext db,
            IntegrationCredentialService creds, RecurringBillingService billing, IAuditLog audit, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound(new { error = "Instituição não encontrada." });
            if (!await creds.ValidateAsync("formattio", request.Headers["X-Integration-Key"].FirstOrDefault(), ct))
                return Results.Json(new { error = "Credencial de integração inválida." }, statusCode: StatusCodes.Status401Unauthorized);
            var member = await ResolveFederatedMemberAsync(db, req.ExternalId, ct);
            if (member is null)
                return Results.NotFound(new { error = "Membro federado não encontrado (importe a identidade primeiro)." });
            if (req.Amount <= 0 || req.OrganizationId == Guid.Empty)
                return Results.BadRequest(new { error = "organizationId e amount (>0) são obrigatórios." });

            var method = (req.Method ?? "pix").Trim().ToLowerInvariant() is "boleto" ? "boleto" : "pix";
            var r = await billing.CreatePledgeAsync(
                req.OrganizationId, member.Id, req.Amount, req.DayOfMonth, chargeToday: true,
                entryType: EntryTypes.Tithe, method: method, ct: ct);
            await audit.RecordAsync("integration.pledge", "recurring_donation", r.Id.ToString());
            return Results.Created($"/api/public/{tenant}/integration/pledge/{r.Id}",
                new { id = r.Id, amount = r.Amount, dayOfMonth = r.DayOfMonth, status = r.Status, method = r.Method });
        });

        // Offboarding do vínculo (ADR-0013): ao perder o vínculo no Formattio, pausa (reversível) ou
        // encerra (permanent=true) a recorrência de dízimo do membro, com aviso.
        pub.MapPost("/integration/offboard", async (
            string tenant, IntegrationOffboardRequest req, HttpRequest request,
            CatalogDbContext catalog, ITenantContext tc,
            IntegrationCredentialService creds, IntegrationOffboardService offboard, IAuditLog audit, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound(new { error = "Instituição não encontrada." });
            if (!await creds.ValidateAsync("formattio", request.Headers["X-Integration-Key"].FirstOrDefault(), ct))
                return Results.Json(new { error = "Credencial de integração inválida." }, statusCode: StatusCodes.Status401Unauthorized);

            var result = await offboard.OffboardAsync(req.ExternalId, req.Permanent, ct);
            if (result is null)
                return Results.NotFound(new { error = "Membro federado não encontrado." });
            await audit.RecordAsync("integration.offboard", "donor", $"{req.ExternalId}:{result.Value.Action}:{result.Value.Affected}");
            return Results.Ok(new { action = result.Value.Action, affected = result.Value.Affected });
        });

        // Receptor de webhook do Pagar.me — FORA da resolução de tenant por JWT.
        group.MapPost("/webhooks/pagarme", async (
            HttpRequest request,
            CatalogDbContext catalog,
            ITenantContext tenant,
            WebhookProcessor processor,
            InfrastructureOptions options,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var raw = await reader.ReadToEndAsync(ct);

            if (!WebhookAuthOk(request, raw, options))
                return Results.Unauthorized();

            PagarmeWebhookEvent evt;
            try { evt = PagarmeWebhook.Parse(raw); }
            catch { return Results.BadRequest(new { error = "payload inválido." }); }

            if (string.IsNullOrEmpty(evt.OrderId))
                return Results.Ok(new { ignored = "sem order id" });

            var slug = await catalog.PspOrders
                .Where(o => o.ProviderOrderId == evt.OrderId)
                .Select(o => o.TenantSlug)
                .FirstOrDefaultAsync(ct);

            if (slug is null)
                return Results.Ok(new { ignored = "order desconhecido" });

            tenant.SetTenant(slug);
            var processed = await processor.ProcessAsync(evt, raw, ct);
            return Results.Ok(new { processed });
        });

        // Configuração das dimensões gerenciais (centros de custo/fundos/projetos).
        app.MapDimensions();

        // Configurabilidade financeira (nomenclatura, tipos de doador, rubricas).
        app.MapFinanceConfig();

        // Tesouraria (contas/caixas, saldo consolidado, transferências).
        app.MapTreasury();

        // Contas a Receber (promessas/recebíveis, aging, baixa).
        app.MapReceivables();

        // Contas a Pagar (credores, títulos com rateio).
        app.MapPayables();

        // Caixa físico (sessões de coleta em espécie com dupla conferência).
        app.MapCashSessions();

        // Lançamento manual de entrada (recebimento fora do PSP — D-05).
        app.MapEntries();

        // Campanhas (earmark + meta × arrecadado + prestação de contas — D-08).
        app.MapCampaigns();

        // Faturamento (#79 — NF/faturamento, gestão avançada): registro/vínculo de NF.
        app.MapFiscalDocuments();

        // Fechamento de período (bloqueio de lançamentos retroativos).
        app.MapPeriods();

        // Conciliação: import de extrato bancário (OFX).
        app.MapStatements();

        // Orçamento (previsto × realizado por dimensão).
        app.MapBudgets();

        // Demonstrações contábeis ITG 2002 (balancete, DRP, Balanço Patrimonial).
        app.MapReports();

        // Snapshots/assinatura das demonstrações (prestação de contas congelada — DT-10).
        app.MapReportSnapshots();

        // Equipe/papéis financeiros (RBAC — DT-04).
        app.MapFinanceTeam();

        return app;
    }

    private static RecurringDto ToRecurringDto(RecurringDonation r)
        => new(r.Id, r.OrganizationId, r.Amount, r.DayOfMonth, r.Status, r.NextChargeAt, r.Attempt);

    /// <summary>Resolve o doador a partir do link mágico (#75); null se o token é inválido ou de outro tenant.</summary>
    private static async Task<Donor?> ResolveMemberAsync(
        TenantDbContext db, string? token, string tenant, InfrastructureOptions options, CancellationToken ct)
    {
        var valid = DonorMagicToken.Validate(token ?? "", options.AppSecret, DateTimeOffset.UtcNow);
        if (valid is null || valid.Value.Tenant != tenant.Trim().ToLowerInvariant()) return null;
        return await db.Donors.FirstOrDefaultAsync(d => d.Id == valid.Value.DonorId, ct);
    }

    /// <summary>Resolve o membro pela identidade federada (#80): vínculo por (source=formattio, externalId).</summary>
    private static async Task<Donor?> ResolveFederatedMemberAsync(TenantDbContext db, string? externalId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalId)) return null;
        var extId = externalId.Trim();
        return await db.Donors.FirstOrDefaultAsync(d => d.Source == "formattio" && d.ExternalId == extId, ct);
    }

    private static bool WebhookAuthOk(HttpRequest request, string raw, InfrastructureOptions options)
    {
        // 1) Assinatura HMAC-SHA256 sobre o corpo bruto (RF-FIN-001): precede o Basic auth.
        if (!string.IsNullOrEmpty(options.PagarmeWebhookSignatureSecret))
        {
            var sig = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            if (string.IsNullOrEmpty(sig))
                sig = request.Headers["X-Hub-Signature"].FirstOrDefault();
            return WebhookSignature.IsValid(options.PagarmeWebhookSignatureSecret, raw, sig);
        }

        // 2) Basic auth (dev/legado): sem credenciais configuradas, não exige auth.
        if (string.IsNullOrEmpty(options.PagarmeWebhookUser))
            return true;

        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            var sep = decoded.IndexOf(':');
            if (sep < 0) return false;
            return decoded[..sep] == options.PagarmeWebhookUser
                && decoded[(sep + 1)..] == options.PagarmeWebhookPassword;
        }
        catch
        {
            return false;
        }
    }
}

public sealed record DonorInput(string Name, string? Email, string Document);

public sealed record CreateDonationRequest(
    Guid OrganizationId,
    decimal Amount,
    DonorInput Donor,
    Guid? CampaignId = null,
    string? Description = null,
    string Method = "pix",
    string? CardToken = null,
    string EntryType = EntryTypes.Donation);

public sealed record CreateRecipientHttpRequest(
    Guid OrganizationId,
    string Name,
    string Email,
    string Document,
    string? PixKey = null);

public sealed record CreateRecurringRequest(
    Guid OrganizationId,
    decimal Amount,
    int DayOfMonth,
    DonorInput Donor,
    bool? ChargeToday = null,
    string EntryType = EntryTypes.Tithe);

// Autoatendimento do membro (#75): autenticado pelo Token (link mágico do doador).
public sealed record MemberGiveRequest(
    string Token, Guid OrganizationId, decimal Amount, string EntryType = EntryTypes.Offering, string Method = "pix");

public sealed record MemberPledgeRequest(
    string Token, Guid OrganizationId, decimal Amount, int DayOfMonth, string Method = "pix");

// Canal da integração federada (#80): autenticado por credencial de serviço (header X-Integration-Key),
// identifica o membro pelo ExternalId (não por token de doador).
public sealed record IntegrationGiveRequest(
    string ExternalId, Guid OrganizationId, decimal Amount, string EntryType = EntryTypes.Tithe, string Method = "pix");
public sealed record IntegrationPledgeRequest(
    string ExternalId, Guid OrganizationId, decimal Amount, int DayOfMonth, string Method = "pix");
public sealed record IntegrationOffboardRequest(string ExternalId, bool Permanent = false);

public sealed record RecurringDto(
    Guid Id,
    Guid OrganizationId,
    decimal Amount,
    int DayOfMonth,
    string Status,
    DateTimeOffset NextChargeAt,
    int Attempt);

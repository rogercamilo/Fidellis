using System.Text;
using Fidellis.Infrastructure;
using Fidellis.Infrastructure.Audit;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Configuration;
using Fidellis.Modules.Finance.Dimensions;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
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

            var result = await checkout.CreateAsync(new CheckoutCommand(
                req.OrganizationId, req.Amount, req.Donor.Name, req.Donor.Email ?? "", req.Donor.Document,
                req.CampaignId, req.Description, IdempotencyKey: request.Headers["Idempotency-Key"].FirstOrDefault(),
                Method: method, CardToken: req.CardToken), ct);

            return Results.Created($"/api/finance/donations/{result.DonationId}", result);
        });

        // Status de uma doação (reconsulta o PSP p/ exibição quando ainda pendente).
        group.MapGet("/donations/{id:guid}", async (
            Guid id,
            TenantDbContext db,
            IPaymentGateway gateway,
            CancellationToken ct) =>
        {
            var d = await db.Donations.FirstOrDefaultAsync(x => x.Id == id, ct);
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

        // ---- Recorrência (dízimo mensal) + dunning ----

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

            var r = await billing.CreatePledgeAsync(
                req.OrganizationId, donor.Id, req.Amount, req.DayOfMonth, req.ChargeToday ?? true, ct);
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

            var result = await checkout.CreateAsync(new CheckoutCommand(
                req.OrganizationId, req.Amount, req.Donor.Name, req.Donor.Email ?? "", req.Donor.Document,
                req.CampaignId, req.Description, IdempotencyKey: request.Headers["Idempotency-Key"].FirstOrDefault(),
                Method: method, CardToken: req.CardToken), ct);
            await audit.RecordAsync("donation.public_checkout", "donation", result.DonationId.ToString());
            return Results.Created($"/api/public/{tenant}/donations/{result.DonationId}", result);
        });

        pub.MapGet("/donations/{id:guid}", async (
            string tenant, Guid id, CatalogDbContext catalog, ITenantContext tc, TenantDbContext db, CancellationToken ct) =>
        {
            if (!await PublicTenant.TryResolveAsync(catalog, tc, tenant, ct))
                return Results.NotFound();
            var d = await db.Donations.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null) return Results.NotFound();
            return Results.Ok(new { id = d.Id, status = d.Status, qrCode = d.PixQrCode, qrCodeUrl = d.PixQrCodeUrl, expiresAt = d.ExpiresAt, amount = d.Amount });
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

        return app;
    }

    private static RecurringDto ToRecurringDto(RecurringDonation r)
        => new(r.Id, r.OrganizationId, r.Amount, r.DayOfMonth, r.Status, r.NextChargeAt, r.Attempt);

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
    string? CardToken = null);

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
    bool? ChargeToday = null);

public sealed record RecurringDto(
    Guid Id,
    Guid OrganizationId,
    decimal Amount,
    int DayOfMonth,
    string Status,
    DateTimeOffset NextChargeAt,
    int Attempt);

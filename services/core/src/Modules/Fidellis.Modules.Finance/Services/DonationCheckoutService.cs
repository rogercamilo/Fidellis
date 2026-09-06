using Fidellis.Infrastructure.Catalog;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Cria a doação (pendente) e o pedido PIX no PSP, persistindo os ids do provedor e o índice
/// global <c>catalog.psp_orders</c> (usado depois pelo webhook para resolver o tenant).
/// O helper <see cref="CreatePixChargeAsync"/> é reusado pela cobrança recorrente (passo 2).
/// </summary>
public sealed class DonationCheckoutService(
    TenantDbContext tenantDb,
    CatalogDbContext catalogDb,
    IPaymentGateway gateway,
    ITenantContext tenant)
{
    public async Task<CheckoutResult> CreateAsync(CheckoutCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Amount <= 0)
            throw new ArgumentException("O valor da doação deve ser positivo.");
        if (!tenant.HasTenant)
            throw new InvalidOperationException("Nenhum tenant resolvido para o request.");

        // Idempotência (RF-FIN-003): mesma chave (não expirada) devolve a doação já criada.
        if (!string.IsNullOrWhiteSpace(cmd.IdempotencyKey))
        {
            var existingKey = await tenantDb.IdempotencyKeys
                .FirstOrDefaultAsync(k => k.Key == cmd.IdempotencyKey && k.ExpiresAt > DateTimeOffset.UtcNow, ct);
            if (existingKey is not null)
            {
                var prior = await tenantDb.Donations.FirstAsync(d => d.Id == existingKey.DonationId, ct);
                return ToResult(prior);
            }
        }

        // Doador: reusa por e-mail se já existir; senão cria.
        var donor = await tenantDb.Donors.FirstOrDefaultAsync(
            d => d.Email != null && d.Email == cmd.DonorEmail, ct);
        if (donor is null)
        {
            donor = new Donor { Name = cmd.DonorName, Email = cmd.DonorEmail, Document = cmd.DonorDocument };
            tenantDb.Donors.Add(donor);
        }

        var method = string.IsNullOrWhiteSpace(cmd.Method) ? "pix" : cmd.Method.Trim().ToLowerInvariant();

        var donation = new Donation
        {
            OrganizationId = cmd.OrganizationId,
            Amount = cmd.Amount,
            Method = method,
            Status = "pending",
            DonorName = cmd.DonorName,
            DonorId = donor.Id,
            CampaignId = cmd.CampaignId,
        };
        tenantDb.Donations.Add(donation);

        // Dimensões: aplica os defaults do tenant quando não informadas (RF-FIN-143).
        donation.CostCenterId ??= await tenantDb.CostCenters
            .Where(c => c.IsDefault).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        donation.FundId ??= await tenantDb.Funds
            .Where(f => f.IsDefault).Select(f => (Guid?)f.Id).FirstOrDefaultAsync(ct);

        if (method == "boleto")
            await CreateBoletoChargeAsync(donation, donor, cmd.Description, ct);
        else
            await CreatePixChargeAsync(donation, donor, cmd.Description, ct);

        // Registra a chave de idempotência (validade 24h) apontando para a nova doação.
        if (!string.IsNullOrWhiteSpace(cmd.IdempotencyKey))
            tenantDb.IdempotencyKeys.Add(new IdempotencyKey
            {
                Key = cmd.IdempotencyKey,
                DonationId = donation.Id,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            });

        await tenantDb.SaveChangesAsync(ct);
        await catalogDb.SaveChangesAsync(ct);

        return ToResult(donation);
    }

    private static CheckoutResult ToResult(Donation d) => new(
        d.Id, d.Status, d.Method,
        QrCode: d.PixQrCode, QrCodeUrl: d.PixQrCodeUrl, ExpiresAt: d.ExpiresAt,
        BoletoLine: d.BoletoLine, BoletoUrl: d.BoletoUrl, DueDate: d.DueDate);

    /// <summary>
    /// Gera o pedido PIX para uma doação já criada (avulsa ou de ciclo recorrente): aplica o split
    /// 100% para a unidade (se houver recebedor), grava os ids do PSP na doação e adiciona o índice
    /// <c>catalog.psp_orders</c>. Não faz <c>SaveChanges</c> — quem chama persiste.
    /// </summary>
    public async Task<PixOrderResult> CreatePixChargeAsync(
        Donation donation, Donor donor, string? description = null, CancellationToken ct = default)
    {
        var recipient = await tenantDb.PspRecipients
            .Where(r => r.OrganizationId == donation.OrganizationId && r.Status == "active")
            .Select(r => r.ProviderRecipientId)
            .FirstOrDefaultAsync(ct);

        var order = await gateway.CreatePixOrderAsync(new CreatePixOrderRequest(
            Amount: donation.Amount,
            DonorName: donor.Name,
            DonorEmail: donor.Email ?? "",
            DonorDocument: donor.Document ?? "",
            RecipientId: recipient,
            Description: description ?? "Doação"), ct);

        donation.PspOrderId = order.OrderId;
        donation.PspChargeId = order.ChargeId;
        donation.PixQrCode = order.QrCode;
        donation.PixQrCodeUrl = order.QrCodeUrl;
        donation.ExpiresAt = order.ExpiresAt;
        donation.Status = order.Status is { Length: > 0 } s ? s : "pending";

        if (!string.IsNullOrEmpty(order.OrderId))
        {
            catalogDb.PspOrders.Add(new PspOrder
            {
                ProviderOrderId = order.OrderId,
                TenantSlug = tenant.TenantId!,
                DonationId = donation.Id,
            });
        }

        return order;
    }

    /// <summary>
    /// Gera o pedido boleto para uma doação já criada: aplica o split (se houver recebedor), grava os
    /// ids do PSP e os dados do boleto (linha/código/PDF/vencimento) na doação e adiciona o índice
    /// <c>catalog.psp_orders</c>. Deriva também <c>ExpiresAt</c> do vencimento (p/ a varredura de
    /// expiração). Não faz <c>SaveChanges</c> — quem chama persiste.
    /// </summary>
    public async Task<BoletoOrderResult> CreateBoletoChargeAsync(
        Donation donation, Donor donor, string? description = null, CancellationToken ct = default)
    {
        var recipient = await tenantDb.PspRecipients
            .Where(r => r.OrganizationId == donation.OrganizationId && r.Status == "active")
            .Select(r => r.ProviderRecipientId)
            .FirstOrDefaultAsync(ct);

        var order = await gateway.CreateBoletoOrderAsync(new CreateBoletoOrderRequest(
            Amount: donation.Amount,
            DonorName: donor.Name,
            DonorEmail: donor.Email ?? "",
            DonorDocument: donor.Document ?? "",
            RecipientId: recipient,
            Description: description ?? "Doação"), ct);

        donation.PspOrderId = order.OrderId;
        donation.PspChargeId = order.ChargeId;
        donation.BoletoLine = order.Line;
        donation.BoletoBarcode = order.Barcode;
        donation.BoletoUrl = order.BoletoUrl;
        donation.DueDate = order.DueDate;
        donation.ExpiresAt = order.DueDate is { } due
            ? new DateTimeOffset(due.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
            : null;
        donation.Status = order.Status is { Length: > 0 } s ? s : "pending";

        if (!string.IsNullOrEmpty(order.OrderId))
        {
            catalogDb.PspOrders.Add(new PspOrder
            {
                ProviderOrderId = order.OrderId,
                TenantSlug = tenant.TenantId!,
                DonationId = donation.Id,
            });
        }

        return order;
    }
}

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

        // Doador: reusa por e-mail se já existir; senão cria.
        var donor = await tenantDb.Donors.FirstOrDefaultAsync(
            d => d.Email != null && d.Email == cmd.DonorEmail, ct);
        if (donor is null)
        {
            donor = new Donor { Name = cmd.DonorName, Email = cmd.DonorEmail, Document = cmd.DonorDocument };
            tenantDb.Donors.Add(donor);
        }

        var donation = new Donation
        {
            OrganizationId = cmd.OrganizationId,
            Amount = cmd.Amount,
            Method = "pix",
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

        var order = await CreatePixChargeAsync(donation, donor, cmd.Description, ct);

        await tenantDb.SaveChangesAsync(ct);
        await catalogDb.SaveChangesAsync(ct);

        return new CheckoutResult(donation.Id, donation.Status, order.QrCode, order.QrCodeUrl, order.ExpiresAt);
    }

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
}

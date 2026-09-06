using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Infrastructure.Accounting;

/// <summary>Emite recibos de doação com número sequencial por organização/ano. Idempotente por doação.</summary>
public sealed class ReceiptService(TenantDbContext db, IClock clock)
{
    public async Task<Receipt> GenerateForDonationAsync(
        Donation donation, string donorName, string? donorDocument, CancellationToken ct = default)
    {
        var existing = await db.Receipts.FirstOrDefaultAsync(r => r.DonationId == donation.Id, ct);
        if (existing is not null) return existing;

        var now = clock.UtcNow;
        var prefix = $"{now.Year}/";
        var seq = await db.Receipts.CountAsync(
            r => r.OrganizationId == donation.OrganizationId && r.Number.StartsWith(prefix), ct) + 1;

        var receipt = new Receipt
        {
            Number = $"{now.Year}/{seq:000000}",
            OrganizationId = donation.OrganizationId,
            DonationId = donation.Id,
            DonorName = donorName,
            DonorDocument = donorDocument,
            Amount = donation.Amount,
            IssuedAt = now,
        };
        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);
        return receipt;
    }
}

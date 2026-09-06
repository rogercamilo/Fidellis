using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Varredura de expiração de doações avulsas (RF-FIN-013 / D8): marca como <c>expired</c> as doações
/// <c>pending</c> não-recorrentes cujo PIX/boleto já passou do prazo. É a rede de segurança do estado
/// de expiração — o webhook do PSP é a via primária. Os ciclos recorrentes têm expiração própria no
/// dunning (<see cref="RecurringBillingService.RunDunningAsync"/>), por isso são excluídos aqui.
/// </summary>
public sealed class DonationExpiryService(TenantDbContext db, IClock clock)
{
    public async Task<int> ExpireOverdueAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var overdue = await db.Donations
            .Where(d => d.Status == "pending" && d.RecurringDonationId == null
                        && ((d.ExpiresAt != null && d.ExpiresAt < now)
                            || (d.DueDate != null && d.DueDate < today)))
            .ToListAsync(ct);

        foreach (var d in overdue)
            d.Status = "expired";

        if (overdue.Count > 0)
            await db.SaveChangesAsync(ct);

        return overdue.Count;
    }
}

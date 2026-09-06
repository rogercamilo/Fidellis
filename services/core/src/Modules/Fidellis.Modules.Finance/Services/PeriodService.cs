using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Security;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Fechamento de período (RF-FIN-170 / decisão D7): fecha um mês (bloqueando lançamentos retroativos)
/// e reabre — <b>apenas admin</b>. A checagem <see cref="IsClosedForDateAsync"/> é usada pelos
/// serviços de lançamento para rejeitar postagens em período fechado. Roda no schema do tenant.
/// </summary>
public sealed class PeriodService(TenantDbContext db, IClock clock)
{
    public async Task<AccountingPeriod> CloseAsync(int year, int month, Guid closedBy, CancellationToken ct = default)
    {
        var period = await db.AccountingPeriods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month, ct);
        if (period is null)
        {
            period = new AccountingPeriod { Year = year, Month = month };
            db.AccountingPeriods.Add(period);
        }
        period.Status = "closed";
        period.ClosedBy = closedBy;
        period.ClosedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return period;
    }

    /// <summary>Reabre um período fechado. Somente papel <c>admin</c> (D7).</summary>
    public async Task<AccountingPeriod?> ReopenAsync(int year, int month, string role, CancellationToken ct = default)
    {
        if (!string.Equals(role, FinanceRoles.Admin, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Somente admin pode reabrir um período fechado.");

        var period = await db.AccountingPeriods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month, ct);
        if (period is null) return null;
        period.Status = "open";
        period.ClosedBy = null;
        period.ClosedAt = null;
        await db.SaveChangesAsync(ct);
        return period;
    }

    public async Task<bool> IsClosedForDateAsync(DateOnly date, CancellationToken ct = default)
        => await db.AccountingPeriods.AnyAsync(p => p.Year == date.Year && p.Month == date.Month && p.Status == "closed", ct);

    /// <summary>Lança se o período do <paramref name="date"/> estiver fechado (guarda de lançamento).</summary>
    public async Task EnsureOpenAsync(DateOnly date, CancellationToken ct = default)
    {
        if (await IsClosedForDateAsync(date, ct))
            throw new InvalidOperationException($"Período {date.Month:00}/{date.Year} está fechado; lançamento retroativo bloqueado.");
    }
}

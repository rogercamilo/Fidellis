using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Motor de doações recorrentes + dunning. Lógica determinística (recebe o tempo via
/// <see cref="IClock"/>): gera uma cobrança PIX por ciclo (reusando o checkout do passo 1) e
/// reagenda/suspende conforme a política de dunning. Roda com o <c>ITenantContext</c> resolvido.
/// </summary>
public sealed class RecurringBillingService(
    TenantDbContext db,
    CatalogDbContext catalogDb,
    DonationCheckoutService checkout,
    INotifier notifier,
    BillingOptions options,
    IClock clock,
    ILogger<RecurringBillingService> logger)
{
    public async Task<RecurringDonation> CreatePledgeAsync(
        Guid organizationId, Guid donorId, decimal amount, int dayOfMonth, bool chargeToday = true, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentException("O valor deve ser positivo.");
        var now = clock.UtcNow;

        var recurring = new RecurringDonation
        {
            OrganizationId = organizationId,
            DonorId = donorId,
            Amount = amount,
            DayOfMonth = Math.Clamp(dayOfMonth, 1, 31),
            Status = "active",
            NextChargeAt = chargeToday ? now : NextChargeDate(dayOfMonth, now),
        };
        db.RecurringDonations.Add(recurring);
        await db.SaveChangesAsync(ct);
        return recurring;
    }

    /// <summary>Gera as cobranças dos ciclos vencidos (sem ciclo em aberto). Retorna quantas criou.</summary>
    public async Task<int> RunBillingCycleAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var due = await db.RecurringDonations
            .Where(r => r.Status == "active" && r.NextChargeAt <= now)
            .ToListAsync(ct);

        var created = 0;
        foreach (var r in due)
        {
            var hasOpen = await db.Donations.AnyAsync(
                d => d.RecurringDonationId == r.Id && d.Status == "pending", ct);
            if (hasOpen) continue;

            var donor = await db.Donors.FirstOrDefaultAsync(d => d.Id == r.DonorId, ct);
            if (donor is null)
            {
                logger.LogWarning("Recorrência {Id} sem doador {DonorId}; pulando.", r.Id, r.DonorId);
                continue;
            }

            var cycle = new Donation
            {
                OrganizationId = r.OrganizationId,
                Amount = r.Amount,
                Method = "pix",
                Status = "pending",
                DonorId = r.DonorId,
                DonorName = donor.Name,
                RecurringDonationId = r.Id,
                Attempt = r.Attempt,
                DueAt = now.AddSeconds(options.CycleExpirySeconds),
            };
            db.Donations.Add(cycle);

            await checkout.CreatePixChargeAsync(cycle, donor, "Dízimo/oferta recorrente", ct);
            cycle.ExpiresAt ??= cycle.DueAt; // se o PSP não devolveu expiração, usa a nossa

            r.LastDonationId = cycle.Id;
            created++;
            await notifier.ChargeCreatedAsync(r, cycle, ct);
        }

        await db.SaveChangesAsync(ct);
        await catalogDb.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>Aplica o dunning nos ciclos pendentes expirados. Retorna quantos afetou.</summary>
    public async Task<int> RunDunningAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var expired = await db.Donations
            .Where(d => d.RecurringDonationId != null && d.Status == "pending"
                        && d.ExpiresAt != null && d.ExpiresAt < now)
            .ToListAsync(ct);

        var affected = 0;
        foreach (var cycle in expired)
        {
            cycle.Status = "expired";

            var r = await db.RecurringDonations.FirstOrDefaultAsync(x => x.Id == cycle.RecurringDonationId, ct);
            if (r is null || r.Status != "active") continue;

            r.Attempt += 1;
            if (r.Attempt <= options.DunningDays.Length)
            {
                r.NextChargeAt = now.AddDays(options.DunningDays[r.Attempt - 1]);
                await notifier.PaymentFailedAsync(r, r.Attempt, ct);
            }
            else
            {
                r.Status = "past_due";
                await notifier.PastDueAsync(r, ct);
            }
            affected++;
        }

        await db.SaveChangesAsync(ct);
        return affected;
    }

    public Task<RecurringDonation?> PauseAsync(Guid id, CancellationToken ct = default)
        => SetStatusAsync(id, r => r.Status = "paused", ct);

    public Task<RecurringDonation?> ResumeAsync(Guid id, CancellationToken ct = default)
        => SetStatusAsync(id, r =>
        {
            r.Status = "active";
            r.Attempt = 0;
            r.NextChargeAt = clock.UtcNow;
        }, ct);

    public Task<RecurringDonation?> CancelAsync(Guid id, CancellationToken ct = default)
        => SetStatusAsync(id, r =>
        {
            r.Status = "canceled";
            r.CanceledAt = clock.UtcNow;
        }, ct);

    private async Task<RecurringDonation?> SetStatusAsync(Guid id, Action<RecurringDonation> mutate, CancellationToken ct)
    {
        var r = await db.RecurringDonations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;
        mutate(r);
        await db.SaveChangesAsync(ct);
        return r;
    }

    /// <summary>Próxima data mensal no/depois de <paramref name="from"/> para o dia informado (clamp p/ fim de mês).</summary>
    public static DateTimeOffset NextChargeDate(int dayOfMonth, DateTimeOffset from)
    {
        dayOfMonth = Math.Clamp(dayOfMonth, 1, 31);
        var candidate = BuildDate(from.Year, from.Month, dayOfMonth, from.Offset);
        if (candidate <= from)
        {
            var y = from.Month == 12 ? from.Year + 1 : from.Year;
            var m = from.Month == 12 ? 1 : from.Month + 1;
            candidate = BuildDate(y, m, dayOfMonth, from.Offset);
        }
        return candidate;
    }

    private static DateTimeOffset BuildDate(int year, int month, int day, TimeSpan offset)
        => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)), 0, 0, 0, offset);
}

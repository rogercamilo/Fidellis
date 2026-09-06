using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Fluxo de caixa projetado (RF-FIN-124): a partir do saldo consolidado de tesouraria, projeta o saldo
/// em D+30/60/90 somando as entradas previstas (recorrências ativas + Contas a Receber em aberto) e
/// subtraindo as saídas firmes (Contas a Pagar aprovadas/agendadas — decisão D5). É o eixo de
/// <b>previsibilidade</b> do produto. Roda no schema do tenant resolvido.
/// </summary>
public sealed class CashFlowService(TenantDbContext db, TreasuryService treasury, IClock clock)
{
    private static readonly int[] Horizons = [30, 60, 90];
    private const int CycleDays = 30;

    public async Task<IReadOnlyList<CashFlowProjection>> ProjectAsync(
        IReadOnlyCollection<Guid> organizationIds, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var opening = await treasury.ConsolidatedBalanceAsync(organizationIds, ct);

        var recurrings = await db.RecurringDonations
            .Where(r => r.Status == "active" && organizationIds.Contains(r.OrganizationId))
            .Select(r => new { r.Amount, r.NextChargeAt })
            .ToListAsync(ct);

        var receivables = await db.Receivables
            .Where(r => (r.Status == "open" || r.Status == "partial") && organizationIds.Contains(r.OrganizationId))
            .Select(r => new { Outstanding = r.Amount - r.ReceivedAmount, r.DueDate })
            .ToListAsync(ct);

        // Payables não têm organização no modelo atual: projeção considera o tenant inteiro.
        var payables = await db.Payables
            .Where(p => p.Status == "approved" || p.Status == "scheduled")
            .Select(p => new { p.Amount, p.DueDate })
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var result = new List<CashFlowProjection>(Horizons.Length);

        foreach (var horizon in Horizons)
        {
            var end = now.AddDays(horizon);
            var endDate = today.AddDays(horizon);

            decimal inflow = 0m;
            foreach (var r in recurrings)
                if (r.NextChargeAt <= end)
                {
                    var cycles = (int)((end - r.NextChargeAt).TotalDays / CycleDays) + 1;
                    inflow += r.Amount * cycles;
                }
            foreach (var r in receivables)
                if (r.DueDate <= endDate && r.Outstanding > 0)
                    inflow += r.Outstanding;

            decimal outflow = 0m;
            foreach (var p in payables)
                if (p.DueDate <= endDate)
                    outflow += p.Amount;

            result.Add(new CashFlowProjection(horizon, opening, inflow, outflow, opening + inflow - outflow));
        }

        return result;
    }
}

/// <summary>Projeção de caixa para um horizonte (dias).</summary>
public sealed record CashFlowProjection(int HorizonDays, decimal Opening, decimal ExpectedInflows, decimal ExpectedOutflows, decimal Projected);

using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Contas a Receber (Onda 2 inc.2.1): promessas de doação/recebíveis, baixa (manual ou por vínculo com
/// a doação conciliada) e aging. Roda no schema do tenant resolvido.
/// </summary>
public sealed class ReceivablesService(TenantDbContext db, IClock clock)
{
    public async Task<Receivable> CreateAsync(
        Guid organizationId, decimal amount, DateOnly dueDate, string source, Guid? donorId,
        string? description, Guid? costCenterId, Guid? projectId, Guid? fundId, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentException("O valor deve ser positivo.");

        var receivable = new Receivable
        {
            OrganizationId = organizationId,
            Amount = amount,
            DueDate = dueDate,
            Source = source is "grant" or "agreement" ? source : "pledge",
            DonorId = donorId,
            Description = description,
            CostCenterId = costCenterId,
            ProjectId = projectId,
            FundId = fundId,
        };
        db.Receivables.Add(receivable);
        await db.SaveChangesAsync(ct);
        return receivable;
    }

    /// <summary>Baixa (parcial ou total) um título. Aplica o valor recebido e atualiza o status.</summary>
    public async Task<Receivable?> SettleAsync(Guid id, decimal amount, Guid? donationId, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentException("O valor da baixa deve ser positivo.");
        var receivable = await db.Receivables.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (receivable is null) return null;

        Apply(receivable, amount, donationId);
        await db.SaveChangesAsync(ct);
        return receivable;
    }

    /// <summary>Aplica a baixa a uma entidade já carregada (usado pela conciliação). Não persiste.</summary>
    public static void Apply(Receivable receivable, decimal amount, Guid? donationId)
    {
        if (receivable.Status is "canceled" or "received") return;
        receivable.ReceivedAmount += amount;
        if (donationId is { } d) receivable.DonationId = d;
        receivable.Status = receivable.ReceivedAmount >= receivable.Amount ? "received" : "partial";
    }

    /// <summary>Classifica os títulos em aberto por faixa de vencimento (a vencer / vencido).</summary>
    public async Task<AgingReport> AgingAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var open = await db.Receivables
            .Where(r => r.Status == "open" || r.Status == "partial")
            .Select(r => new { r.Amount, r.ReceivedAmount, r.DueDate })
            .ToListAsync(ct);

        var report = new AgingReport();
        foreach (var r in open)
        {
            var outstanding = r.Amount - r.ReceivedAmount;
            if (outstanding <= 0) continue;
            var days = r.DueDate.DayNumber - today.DayNumber;
            if (days >= 0) report.NotDue += outstanding;
            else if (days >= -30) report.Overdue1To30 += outstanding;
            else if (days >= -60) report.Overdue31To60 += outstanding;
            else report.Overdue60Plus += outstanding;
        }
        return report;
    }
}

public sealed class AgingReport
{
    public decimal NotDue { get; set; }
    public decimal Overdue1To30 { get; set; }
    public decimal Overdue31To60 { get; set; }
    public decimal Overdue60Plus { get; set; }
    public decimal TotalOutstanding => NotDue + Overdue1To30 + Overdue31To60 + Overdue60Plus;
}

using Fidellis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>Execução de uma parceria/edital (MROSC): recebido × gasto do projeto.</summary>
public sealed record MroscReport(Guid ProjectId, string ProjectName, decimal Expected, decimal Received, decimal Spent, decimal Balance);

/// <summary>
/// Prestação de contas de parcerias MROSC (Onda 4 inc.4.3 / RF-FIN-163): por projeto, soma os
/// recebíveis de convênio/edital (grant/agreement) recebidos e as despesas do projeto. Roda no schema
/// do tenant.
/// </summary>
public sealed class MroscReportService(TenantDbContext db)
{
    public async Task<MroscReport?> ReportAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return null;

        var grants = await db.Receivables
            .Where(r => r.ProjectId == projectId && (r.Source == "grant" || r.Source == "agreement"))
            .Select(r => new { r.Amount, r.ReceivedAmount })
            .ToListAsync(ct);
        var expected = grants.Sum(g => g.Amount);
        var received = grants.Sum(g => g.ReceivedAmount);

        var spent = await db.Transactions
            .Where(t => t.ProjectId == projectId && t.Kind == "debit")
            .SumAsync(t => t.Amount, ct);

        return new MroscReport(projectId, project.Name, expected, received, spent, received - spent);
    }
}

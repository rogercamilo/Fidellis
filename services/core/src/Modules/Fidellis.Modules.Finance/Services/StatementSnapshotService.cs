using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Snapshot/assinatura das demonstrações (RF-FIN-173 / DT-10): congela o rascunho on-the-fly do
/// <see cref="StatementsService"/> num payload JSON com hash SHA-256, e gerencia o fluxo de
/// aprovação (draft → approved). Uma vez aprovado, o snapshot é imutável (fonte da prestação de
/// contas histórica). Roda no schema do tenant.
/// </summary>
public sealed class StatementSnapshotService(TenantDbContext db, StatementsService statements, IClock clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Gera (congela) um snapshot draft do período: DRP, Balanço, DFC e segregação/DMPL.</summary>
    public async Task<StatementSnapshot> GenerateAsync(int year, string? generatedBy, CancellationToken ct = default)
    {
        var content = new
        {
            year,
            generatedAt = clock.UtcNow,
            income = await statements.IncomeAsync(year, ct: ct),
            balanceSheet = await statements.BalanceSheetAsync(year, ct: ct),
            cashFlow = await statements.CashFlowAsync(year, ct: ct),
            segregated = await statements.IncomeSegregatedAsync(year, ct: ct),
            dmpl = await statements.DmplAsync(year, ct: ct),
        };
        var payload = JsonSerializer.Serialize(content, Json);

        var snapshot = new StatementSnapshot
        {
            Year = year,
            Payload = payload,
            Hash = Sha256(payload),
            Status = "draft",
            GeneratedBy = generatedBy,
        };
        db.StatementSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);
        return snapshot;
    }

    /// <summary>Aprova/assina um snapshot draft (RF-FIN-173). No-op idempotente se já aprovado.</summary>
    public async Task<StatementSnapshot?> ApproveAsync(Guid id, string approvedBy, CancellationToken ct = default)
    {
        var snapshot = await db.StatementSnapshots.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (snapshot is null) return null;
        if (snapshot.Status == "approved") return snapshot;

        // Segregação: quem aprova não pode ser quem gerou (assinatura por outra pessoa).
        if (!string.IsNullOrWhiteSpace(snapshot.GeneratedBy) &&
            string.Equals(snapshot.GeneratedBy, approvedBy, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Quem gerou o snapshot não pode aprová-lo (segregação de funções).");

        snapshot.Status = "approved";
        snapshot.ApprovedBy = approvedBy;
        snapshot.ApprovedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return snapshot;
    }

    public Task<List<StatementSnapshot>> ListAsync(int? year, CancellationToken ct = default)
    {
        var q = db.StatementSnapshots.AsQueryable();
        if (year is { } y) q = q.Where(s => s.Year == y);
        return q.OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
    }

    public Task<StatementSnapshot?> GetAsync(Guid id, CancellationToken ct = default)
        => db.StatementSnapshots.FirstOrDefaultAsync(s => s.Id == id, ct);

    private static string Sha256(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}

using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Contas a Pagar — base (Onda 2 inc.2.2): credores/fornecedores, títulos a pagar (nascem
/// <c>awaiting_approval</c>) e rateio por dimensão. Aprovação (alçadas) e pagamento entram no inc.2.3.
/// Roda no schema do tenant resolvido.
/// </summary>
public sealed class PayablesService(TenantDbContext db)
{
    public async Task<Payee> CreatePayeeAsync(string name, string? document, string? pixKey, string kind, CancellationToken ct = default)
    {
        var payee = new Payee
        {
            Name = name,
            Document = document,
            PixKey = pixKey,
            Kind = kind is "volunteer" or "staff" ? kind : "supplier",
        };
        db.Payees.Add(payee);
        await db.SaveChangesAsync(ct);
        return payee;
    }

    public Task<List<Payee>> ListPayeesAsync(CancellationToken ct = default)
        => db.Payees.OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<Payable> CreatePayableAsync(
        Guid payeeId, decimal amount, DateOnly dueDate, string description, Guid? categoryId, string? documentUrl,
        Guid? costCenterId, Guid? projectId, Guid? fundId,
        IReadOnlyList<PayableAllocationInput>? allocations, Guid? createdBy, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentException("O valor deve ser positivo.");
        if (!await db.Payees.AnyAsync(p => p.Id == payeeId, ct))
            throw new ArgumentException("Credor (payeeId) inexistente.");

        if (allocations is { Count: > 0 })
        {
            var sum = allocations.Sum(a => a.Amount);
            if (allocations.Any(a => a.Amount <= 0))
                throw new ArgumentException("Cada rateio deve ter valor positivo.");
            if (sum != amount)
                throw new ArgumentException($"A soma do rateio ({sum:0.00}) deve igualar o valor do título ({amount:0.00}).");
        }

        var payable = new Payable
        {
            PayeeId = payeeId,
            Amount = amount,
            DueDate = dueDate,
            Description = description,
            CategoryId = categoryId,
            DocumentUrl = documentUrl,
            CostCenterId = costCenterId,
            ProjectId = projectId,
            FundId = fundId,
            CreatedBy = createdBy,
        };
        db.Payables.Add(payable);

        if (allocations is { Count: > 0 })
            foreach (var a in allocations)
                db.PayableAllocations.Add(new PayableAllocation
                {
                    PayableId = payable.Id,
                    CostCenterId = a.CostCenterId,
                    ProjectId = a.ProjectId,
                    FundId = a.FundId,
                    Amount = a.Amount,
                });

        await db.SaveChangesAsync(ct);
        return payable;
    }

    public Task<List<Payable>> ListPayablesAsync(string? status, CancellationToken ct = default)
    {
        var q = db.Payables.AsQueryable();
        if (status is { Length: > 0 }) q = q.Where(p => p.Status == status);
        return q.OrderBy(p => p.DueDate).ToListAsync(ct);
    }

    public async Task<Payable?> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var payable = await db.Payables.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payable is null) return null;
        if (payable.Status == "paid") throw new InvalidOperationException("Título pago não pode ser cancelado.");
        payable.Status = "canceled";
        await db.SaveChangesAsync(ct);
        return payable;
    }
}

public sealed record PayableAllocationInput(decimal Amount, Guid? CostCenterId = null, Guid? ProjectId = null, Guid? FundId = null);

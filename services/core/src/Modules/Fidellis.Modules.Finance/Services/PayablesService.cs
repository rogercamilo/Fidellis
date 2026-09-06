using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Contas a Pagar (Onda 2 inc.2.2/2.3): credores/fornecedores, títulos (nascem <c>awaiting_approval</c>),
/// rateio por dimensão e <b>pagamento</b> de título aprovado (movimento de tesouraria + despesa
/// contábil). Roda no schema do tenant resolvido.
/// </summary>
public sealed class PayablesService(TenantDbContext db, IClock? clock = null, ChartOfAccountsSeeder? chartSeeder = null)
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

    /// <summary>
    /// Paga um título <b>aprovado</b> (RF-FIN-112/113): marca <c>paid</c>, registra a saída na conta de
    /// tesouraria e lança a despesa (partida dobrada: débito Despesa / crédito Banco).
    /// </summary>
    public async Task<Payable?> PayAsync(Guid id, Guid treasuryAccountId, CancellationToken ct = default)
    {
        var payable = await db.Payables.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payable is null) return null;
        if (payable.Status != "approved")
            throw new InvalidOperationException($"Só títulos aprovados podem ser pagos (status: {payable.Status}).");
        if (!await db.TreasuryAccounts.AnyAsync(a => a.Id == treasuryAccountId, ct))
            throw new InvalidOperationException("Conta de tesouraria inexistente.");

        var now = clock?.UtcNow ?? DateTimeOffset.UtcNow;
        payable.Status = "paid";
        payable.PaidAt = now;
        payable.AccountId = treasuryAccountId;

        // Saída de tesouraria (liquidez).
        db.TreasuryMovements.Add(new TreasuryMovement
        {
            AccountId = treasuryAccountId,
            Kind = "outflow",
            Amount = payable.Amount,
            Description = $"Pagamento {payable.Description}",
            PayableId = payable.Id,
        });

        // Despesa contábil (partida dobrada): débito Despesa, crédito Banco.
        var seeder = chartSeeder ?? new ChartOfAccountsSeeder(db);
        await seeder.EnsureDefaultAsync(ct);
        var accounts = await db.LedgerAccounts
            .Where(a => a.Code == ChartOfAccounts.Expense || a.Code == ChartOfAccounts.Bank)
            .ToDictionaryAsync(a => a.Code, a => a, ct);
        var expense = accounts[ChartOfAccounts.Expense];
        var bank = accounts[ChartOfAccounts.Bank];

        var account = await db.Accounts.FirstOrDefaultAsync(ct);
        if (account is null)
        {
            account = new Account { OrganizationId = Guid.Empty, Name = "Conta de despesas" };
            db.Accounts.Add(account);
        }
        var transaction = new Transaction
        {
            AccountId = account.Id,
            Amount = payable.Amount,
            Kind = "debit",
            Description = $"Despesa {payable.Description}",
            CostCenterId = payable.CostCenterId,
            ProjectId = payable.ProjectId,
            FundId = payable.FundId,
        };
        db.Transactions.Add(transaction);
        db.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = expense.Id, Ledger = expense.Name, Debit = payable.Amount, Credit = 0 },
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = bank.Id, Ledger = bank.Name, Debit = 0, Credit = payable.Amount });

        await db.SaveChangesAsync(ct);
        return payable;
    }
}

public sealed record PayableAllocationInput(decimal Amount, Guid? CostCenterId = null, Guid? ProjectId = null, Guid? FundId = null);

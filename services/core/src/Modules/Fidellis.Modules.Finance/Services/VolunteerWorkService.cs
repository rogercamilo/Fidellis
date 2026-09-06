using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Trabalho voluntário a valor justo (Onda 4 inc.4.2 / RF-FIN-162): registra o serviço e lança a
/// partida dobrada — débito <b>despesa</b> (serviços voluntários aplicados) / crédito <b>receita</b>
/// (serviços voluntários recebidos) — reconhecendo-o "como se houvesse desembolso" (ITG 2002). O
/// resultado líquido é zero, mas ambos aparecem nas demonstrações. Roda no schema do tenant.
/// </summary>
public sealed class VolunteerWorkService(TenantDbContext db, ChartOfAccountsSeeder chartSeeder)
{
    public Task<List<VolunteerWork>> ListAsync(CancellationToken ct = default)
        => db.VolunteerWork.OrderByDescending(v => v.PerformedOn).ToListAsync(ct);

    public async Task<VolunteerWork> RecordAsync(
        Guid organizationId, string description, decimal fairValue, DateOnly performedOn,
        Guid? costCenterId, Guid? projectId, Guid? fundId, CancellationToken ct = default)
    {
        if (fairValue <= 0) throw new ArgumentException("O valor justo deve ser positivo.");

        var work = new VolunteerWork
        {
            OrganizationId = organizationId,
            Description = description,
            FairValue = fairValue,
            PerformedOn = performedOn,
            CostCenterId = costCenterId,
            ProjectId = projectId,
            FundId = fundId,
        };
        db.VolunteerWork.Add(work);

        await chartSeeder.EnsureDefaultAsync(ct);
        var accounts = await db.LedgerAccounts
            .Where(a => a.Code == ChartOfAccounts.VolunteerExpense || a.Code == ChartOfAccounts.VolunteerRevenue)
            .ToDictionaryAsync(a => a.Code, a => a, ct);
        var expense = accounts[ChartOfAccounts.VolunteerExpense];
        var revenue = accounts[ChartOfAccounts.VolunteerRevenue];

        var account = await db.Accounts.FirstOrDefaultAsync(ct);
        if (account is null)
        {
            account = new Account { OrganizationId = organizationId, Name = "Conta principal" };
            db.Accounts.Add(account);
        }

        var transaction = new Transaction
        {
            AccountId = account.Id,
            Amount = fairValue,
            Kind = "voluntary",
            Description = $"Trabalho voluntário: {description}",
            CostCenterId = costCenterId,
            ProjectId = projectId,
            FundId = fundId,
        };
        db.Transactions.Add(transaction);
        db.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = expense.Id, Ledger = expense.Name, Debit = fairValue, Credit = 0 },
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = revenue.Id, Ledger = revenue.Name, Debit = 0, Credit = fairValue });

        work.TransactionId = transaction.Id;
        await db.SaveChangesAsync(ct);
        return work;
    }
}

using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Tesouraria (Onda 2 inc.2.0): contas (banco/caixa), saldo (abertura + movimentos), transferências
/// internas (dupla perna, sem afetar o resultado) e saldo consolidado por conjunto de unidades.
/// Roda no schema do tenant resolvido.
/// </summary>
public sealed class TreasuryService(TenantDbContext db)
{
    public async Task<TreasuryAccount> CreateAccountAsync(
        Guid organizationId, string name, string kind, decimal openingBalance, CancellationToken ct = default)
    {
        var account = new TreasuryAccount
        {
            OrganizationId = organizationId,
            Name = name,
            Kind = kind is "cash" ? "cash" : "bank",
            OpeningBalance = openingBalance,
        };
        db.TreasuryAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }

    public Task<List<TreasuryAccount>> ListAccountsAsync(CancellationToken ct = default)
        => db.TreasuryAccounts.OrderBy(a => a.Name).ToListAsync(ct);

    public async Task<decimal> AccountBalanceAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await db.TreasuryAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null) return 0m;
        return account.OpeningBalance + await MovementsDeltaAsync([accountId], ct);
    }

    /// <summary>Saldo consolidado das contas das unidades informadas (Rede→Unidade).</summary>
    public async Task<decimal> ConsolidatedBalanceAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken ct = default)
    {
        var accounts = await db.TreasuryAccounts
            .Where(a => organizationIds.Contains(a.OrganizationId))
            .ToListAsync(ct);
        if (accounts.Count == 0) return 0m;

        var opening = accounts.Sum(a => a.OpeningBalance);
        var delta = await MovementsDeltaAsync(accounts.Select(a => a.Id).ToList(), ct);
        return opening + delta;
    }

    /// <summary>Transferência interna: saída de uma conta e entrada em outra (não afeta o resultado).</summary>
    public async Task<(TreasuryMovement Outflow, TreasuryMovement Inflow)> TransferAsync(
        Guid fromAccountId, Guid toAccountId, decimal amount, string? description, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentException("O valor da transferência deve ser positivo.");
        if (fromAccountId == toAccountId) throw new ArgumentException("Conta de origem e destino não podem ser iguais.");

        var outflow = new TreasuryMovement { AccountId = fromAccountId, Kind = "transfer_out", Amount = amount, Description = description, CounterpartId = toAccountId };
        var inflow = new TreasuryMovement { AccountId = toAccountId, Kind = "transfer_in", Amount = amount, Description = description, CounterpartId = fromAccountId };
        db.TreasuryMovements.AddRange(outflow, inflow);
        await db.SaveChangesAsync(ct);
        return (outflow, inflow);
    }

    private async Task<decimal> MovementsDeltaAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
    {
        var movements = await db.TreasuryMovements
            .Where(m => accountIds.Contains(m.AccountId))
            .Select(m => new { m.Kind, m.Amount })
            .ToListAsync(ct);

        decimal delta = 0m;
        foreach (var m in movements)
            delta += m.Kind is "inflow" or "transfer_in" ? m.Amount : -m.Amount;
        return delta;
    }
}

using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Infrastructure.Accounting;

/// <summary>Semeia o plano de contas padrão no schema do tenant, se ainda não existir (idempotente por código).</summary>
public sealed class ChartOfAccountsSeeder(TenantDbContext db)
{
    public async Task EnsureDefaultAsync(CancellationToken ct = default)
    {
        var byCode = await db.LedgerAccounts.ToDictionaryAsync(a => a.Code, a => a.Id, ct);
        var added = new Dictionary<string, Guid>();

        foreach (var def in ChartOfAccounts.Default)
        {
            if (byCode.ContainsKey(def.Code)) continue;

            Guid? parentId = null;
            if (def.ParentCode is { } pc)
                parentId = byCode.TryGetValue(pc, out var pid) ? pid : added.GetValueOrDefault(pc);

            var account = new LedgerAccount
            {
                Code = def.Code,
                Name = def.Name,
                Type = def.Type,
                NormalBalance = def.NormalBalance,
                Postable = def.Postable,
                ParentId = parentId,
            };
            db.LedgerAccounts.Add(account);
            added[def.Code] = account.Id;
        }

        if (added.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}

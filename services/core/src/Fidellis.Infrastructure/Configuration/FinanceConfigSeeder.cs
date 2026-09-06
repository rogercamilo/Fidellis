using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Infrastructure.Configuration;

/// <summary>
/// Semeia a configuração financeira default do tenant (idempotente): a linha única de
/// <see cref="FinanceSettings"/> (nomenclaturas Dízimo/Oferta) e os tipos de doador iniciais
/// (Membro = recorrente-default, Apoiador). RF-FIN-180/181/182.
/// </summary>
public sealed class FinanceConfigSeeder(TenantDbContext db)
{
    public const string RecurringDonorTypeName = "Membro";
    public const string OneTimeDonorTypeName = "Apoiador";

    public async Task EnsureDefaultsAsync(CancellationToken ct = default)
    {
        var changed = false;

        if (!await db.FinanceSettings.AnyAsync(ct))
        {
            db.FinanceSettings.Add(new FinanceSettings());
            changed = true;
        }

        if (!await db.DonorTypes.AnyAsync(ct))
        {
            db.DonorTypes.Add(new DonorType { Name = RecurringDonorTypeName, IsRecurringDefault = true });
            db.DonorTypes.Add(new DonorType { Name = OneTimeDonorTypeName });
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}

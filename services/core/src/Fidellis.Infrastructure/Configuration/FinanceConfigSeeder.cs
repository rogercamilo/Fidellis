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

        // Faixas de alçada default (RF-FIN-112) no vocabulário de conselho (D-02 §4): o conselho fiscal
        // NÃO autoriza (fiscaliza); a faixa alta é do conselheiro + moderador/presidente.
        if (!await db.ApprovalTiers.AnyAsync(ct))
        {
            db.ApprovalTiers.AddRange(
                new ApprovalTier { MinAmount = 0m, MaxAmount = 500m, Signatures = 1, RolesCsv = "coordinator,council_officer" },
                new ApprovalTier { MinAmount = 500m, MaxAmount = 5000m, Signatures = 2, RolesCsv = "coordinator,council_officer" },
                new ApprovalTier { MinAmount = 5000m, MaxAmount = null, Signatures = 2, RolesCsv = "council_officer,council_chair" });
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}

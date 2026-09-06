using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Infrastructure.Dimensions;

/// <summary>
/// Semeia as dimensões default do tenant (idempotente por código): um centro de custo "Geral" e um
/// fundo "Recursos livres" (sem restrição), ambos marcados como <c>is_default</c>. São os valores
/// aplicados a lançamentos sem dimensão informada (RF-FIN-143 / D14).
/// </summary>
public sealed class DimensionsSeeder(TenantDbContext db)
{
    public const string DefaultCostCenterCode = "GERAL";
    public const string DefaultFundCode = "LIVRE";

    public async Task EnsureDefaultsAsync(CancellationToken ct = default)
    {
        var changed = false;

        if (!await db.CostCenters.AnyAsync(c => c.Code == DefaultCostCenterCode, ct))
        {
            db.CostCenters.Add(new CostCenter { Code = DefaultCostCenterCode, Name = "Geral", IsDefault = true });
            changed = true;
        }

        if (!await db.Funds.AnyAsync(f => f.Code == DefaultFundCode, ct))
        {
            db.Funds.Add(new Fund
            {
                Code = DefaultFundCode,
                Name = "Recursos livres",
                Restriction = "free",
                IsDefault = true,
            });
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}

using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

public sealed record ManualEntryCommand(
    Guid TreasuryAccountId, decimal Amount,
    string? DonorName, string? DonorEmail, string? DonorDocument,
    Guid? CostCenterId, Guid? ProjectId, Guid? FundId, DateTimeOffset? OccurredAt);

/// <summary>
/// Lançamento manual de entrada (D-05): recebimento fora do PSP (transferência recebida, depósito
/// avulso). Cria uma <see cref="Donation"/> já <c>paid</c> com origem <c>manual</c>, contabiliza contra a
/// conta de tesouraria escolhida (Caixa ou Banco conforme o tipo) e — via
/// <see cref="ReconciliationService.PostEntryAsync"/> — emite recibo quando há doador identificado
/// (recibo condicional, Q3). Roda no schema do tenant.
/// </summary>
public sealed class ManualEntryService(TenantDbContext db, ReconciliationService reconciliation, IClock clock)
{
    public async Task<Donation> CreateAsync(ManualEntryCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Amount <= 0)
            throw new ArgumentException("O valor da entrada deve ser positivo.");
        var account = await db.TreasuryAccounts.FirstOrDefaultAsync(a => a.Id == cmd.TreasuryAccountId, ct)
            ?? throw new ArgumentException("Conta de tesouraria inexistente.");

        // Doador (opcional): reusa por e-mail se houver; senão cria. Sem doador → sem recibo.
        Guid? donorId = null;
        var donorName = cmd.DonorName?.Trim();
        if (!string.IsNullOrWhiteSpace(donorName))
        {
            var donor = !string.IsNullOrWhiteSpace(cmd.DonorEmail)
                ? await db.Donors.FirstOrDefaultAsync(d => d.Email == cmd.DonorEmail, ct)
                : null;
            if (donor is null)
            {
                donor = new Donor { Name = donorName, Email = cmd.DonorEmail, Document = cmd.DonorDocument };
                db.Donors.Add(donor);
            }
            donorId = donor.Id;
        }

        var entry = new Donation
        {
            OrganizationId = account.OrganizationId,
            Amount = cmd.Amount,
            Method = "manual",
            Source = "manual",
            Status = "paid",
            PaidAt = cmd.OccurredAt ?? clock.UtcNow,
            DonorName = donorName,
            DonorId = donorId,
            // Dimensões: usa as informadas ou aplica os defaults do tenant (RF-FIN-143).
            CostCenterId = cmd.CostCenterId ?? await db.CostCenters.Where(c => c.IsDefault).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct),
            ProjectId = cmd.ProjectId,
            FundId = cmd.FundId ?? await db.Funds.Where(f => f.IsDefault).Select(f => (Guid?)f.Id).FirstOrDefaultAsync(ct),
        };
        db.Donations.Add(entry);

        var ledger = account.Kind == "cash" ? ChartOfAccounts.Cash : ChartOfAccounts.Bank;
        await reconciliation.PostEntryAsync(entry, account, ledger, ct);
        await db.SaveChangesAsync(ct);
        return entry;
    }
}

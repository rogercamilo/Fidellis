using Fidellis.Infrastructure.Audit;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fidellis.Modules.Finance.Entries;

/// <summary>
/// Entradas manuais (D-05): recebimento fora do PSP. Lançar é operação de lançador (coordenador/admin —
/// D-02 Q3); a entrada nasce de 1ª classe (receita + dimensão + tesouraria) como o checkout.
/// </summary>
public static class EntryEndpoints
{
    public static IEndpointRouteBuilder MapEntries(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/entries").WithTags("Finance/Entries").AddEndpointFilter<FinanceWriteFilter>();

        g.MapPost("/", async (
            ManualEntryRequest req, ManualEntryService entries, ITenantContext tenant, ICurrentUser user, IAuditLog audit, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (!FinanceRoles.CanLaunch(user.Role))
                return Results.Json(new { error = "Somente coordenador ou admin lançam entradas." },
                    statusCode: StatusCodes.Status403Forbidden);
            try
            {
                var e = await entries.CreateAsync(new ManualEntryCommand(
                    req.TreasuryAccountId, req.Amount, req.DonorName, req.DonorEmail, req.DonorDocument,
                    req.CostCenterId, req.ProjectId, req.FundId, req.OccurredAt, req.EntryType), ct);
                await audit.RecordAsync("entry.manual_created", "donation", e.Id.ToString());
                return Results.Created($"/api/finance/entries/{e.Id}",
                    new { id = e.Id, amount = e.Amount, source = e.Source, status = e.Status });
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        return app;
    }
}

public sealed record ManualEntryRequest(
    Guid TreasuryAccountId, decimal Amount, string? DonorName = null, string? DonorEmail = null,
    string? DonorDocument = null, Guid? CostCenterId = null, Guid? ProjectId = null, Guid? FundId = null,
    DateTimeOffset? OccurredAt = null, string EntryType = EntryTypes.Donation);

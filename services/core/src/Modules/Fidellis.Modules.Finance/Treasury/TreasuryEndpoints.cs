using Fidellis.Infrastructure.Organizations;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Treasury;

/// <summary>
/// Endpoints de tesouraria (Onda 2 inc.2.0): contas, saldo (por conta/unidade e consolidado da rede),
/// e transferências internas. Mutações passam pelo <see cref="FinanceWriteFilter"/>.
/// </summary>
public static class TreasuryEndpoints
{
    public static IEndpointRouteBuilder MapTreasury(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/treasury").WithTags("Finance/Treasury").AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/accounts", async (TreasuryService treasury, CancellationToken ct) =>
        {
            var accounts = await treasury.ListAccountsAsync(ct);
            var dtos = new List<TreasuryAccountDto>(accounts.Count);
            foreach (var a in accounts)
                dtos.Add(new TreasuryAccountDto(a.Id, a.OrganizationId, a.Name, a.Kind, a.OpeningBalance,
                    await treasury.AccountBalanceAsync(a.Id, ct), a.Active));
            return Results.Ok(dtos);
        });

        g.MapPost("/accounts", async (
            CreateTreasuryAccountRequest req, TreasuryService treasury, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (req.OrganizationId == Guid.Empty || string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "organizationId e name são obrigatórios." });

            var a = await treasury.CreateAccountAsync(req.OrganizationId, req.Name.Trim(), req.Kind ?? "bank", req.OpeningBalance ?? 0m, ct);
            return Results.Created($"/api/finance/treasury/accounts/{a.Id}",
                new TreasuryAccountDto(a.Id, a.OrganizationId, a.Name, a.Kind, a.OpeningBalance, a.OpeningBalance, a.Active));
        });

        // Saldo: por unidade (?organizationId=) ou consolidado das unidades visíveis (Rede→Unidade).
        g.MapGet("/balance", async (
            Guid? organizationId, TreasuryService treasury, TenantDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            IReadOnlyCollection<Guid> orgIds = organizationId is { } org
                ? [org]
                : await VisibleOrgIdsAsync(db, user, ct);

            var balance = await treasury.ConsolidatedBalanceAsync(orgIds, ct);
            return Results.Ok(new { organizationIds = orgIds, balance });
        });

        g.MapPost("/transfers", async (
            TransferRequest req, TreasuryService treasury, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            try
            {
                var (outflow, inflow) = await treasury.TransferAsync(req.FromAccountId, req.ToAccountId, req.Amount, req.Description, ct);
                return Results.Ok(new { outflowId = outflow.Id, inflowId = inflow.Id, req.Amount });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Fluxo de caixa projetado D+30/60/90 (RF-FIN-124): consolidado das unidades visíveis.
        g.MapGet("/cashflow", async (
            Guid? organizationId, CashFlowService cashflow, TenantDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            IReadOnlyCollection<Guid> orgIds = organizationId is { } org
                ? [org]
                : await VisibleOrgIdsAsync(db, user, ct);
            var projection = await cashflow.ProjectAsync(orgIds, ct);
            return Results.Ok(projection);
        });

        return app;
    }

    private static async Task<IReadOnlyCollection<Guid>> VisibleOrgIdsAsync(TenantDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var allOrgs = await db.Organizations.Select(o => new { o.Id, o.ParentId }).ToListAsync(ct);
        if (user.UserId is not { } userId)
            return allOrgs.Select(o => o.Id).ToList(); // dev/sem usuário: tudo

        var memberOrgIds = await db.OrgMembers.Where(m => m.UserId == userId).Select(m => m.OrganizationId).ToListAsync(ct);
        return OrgVisibility.VisibleOrgIds(memberOrgIds, allOrgs.Select(o => (o.Id, o.ParentId)).ToList());
    }
}

public sealed record TreasuryAccountDto(Guid Id, Guid OrganizationId, string Name, string Kind, decimal OpeningBalance, decimal Balance, bool Active);
public sealed record CreateTreasuryAccountRequest(Guid OrganizationId, string Name, string? Kind = null, decimal? OpeningBalance = null);
public sealed record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, string? Description = null);

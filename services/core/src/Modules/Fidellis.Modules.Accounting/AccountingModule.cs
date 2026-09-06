using Fidellis.Infrastructure.Organizations;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Accounting;

/// <summary>
/// Módulo Accounting — plano de contas, razão (extrato por conta), balancete consolidado
/// (subárvore Rede→Unidade via <see cref="OrgVisibility"/>) e recibos.
/// </summary>
public static class AccountingModule
{
    public static IServiceCollection AddAccountingModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapAccountingModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting").WithTags("Accounting");

        group.MapGet("/ping", (ITenantContext tenant) =>
            Results.Ok(new { module = "Accounting", tenant = tenant.TenantId, schema = tenant.SchemaName }));

        // Plano de contas
        group.MapGet("/accounts", async (ITenantContext tenant, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var list = await db.LedgerAccounts
                .OrderBy(a => a.Code)
                .Select(a => new { a.Id, a.Code, a.Name, a.Type, normalBalance = a.NormalBalance, a.Postable, a.ParentId })
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        group.MapPost("/accounts", async (CreateAccountRequest req, ITenantContext tenant, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "code e name são obrigatórios." });
            if (await db.LedgerAccounts.AnyAsync(a => a.Code == req.Code, ct))
                return Results.Conflict(new { error = $"Conta '{req.Code}' já existe." });

            var acc = new LedgerAccount
            {
                Code = req.Code.Trim(),
                Name = req.Name.Trim(),
                Type = req.Type ?? "asset",
                NormalBalance = req.NormalBalance ?? "debit",
                Postable = req.Postable ?? true,
                ParentId = req.ParentId,
            };
            db.LedgerAccounts.Add(acc);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/accounting/accounts/{acc.Id}", new { acc.Id, acc.Code, acc.Name });
        });

        // Balancete (trial balance) consolidado nas unidades visíveis
        group.MapGet("/trial-balance", async (
            ITenantContext tenant, ICurrentUser user, TenantDbContext db,
            DateTimeOffset? fromDate, DateTimeOffset? toDate, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var visible = await VisibleOrgsAsync(user, db, ct);

            var rows = await (
                from e in db.AccountingEntries
                join t in db.Transactions on e.TransactionId equals t.Id
                join a in db.Accounts on t.AccountId equals a.Id
                where visible.Contains(a.OrganizationId)
                      && (fromDate == null || e.CreatedAt >= fromDate)
                      && (toDate == null || e.CreatedAt <= toDate)
                group e by new { e.LedgerAccountId, e.Ledger } into g
                select new { g.Key.LedgerAccountId, g.Key.Ledger, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
                .ToListAsync(ct);

            var codes = await db.LedgerAccounts.ToDictionaryAsync(a => a.Id, a => a.Code, ct);
            var result = rows
                .Select(r => new
                {
                    ledgerAccountId = r.LedgerAccountId,
                    code = r.LedgerAccountId is { } id && codes.TryGetValue(id, out var c) ? c : null,
                    name = r.Ledger,
                    debit = r.Debit,
                    credit = r.Credit,
                    balance = r.Debit - r.Credit,
                })
                .OrderBy(r => r.code)
                .ToList();

            return Results.Ok(new
            {
                from = fromDate,
                to = toDate,
                totalDebit = result.Sum(r => r.debit),
                totalCredit = result.Sum(r => r.credit),
                accounts = result,
            });
        });

        // Razão (extrato) de uma conta, com saldo acumulado
        group.MapGet("/ledger", async (
            ITenantContext tenant, ICurrentUser user, TenantDbContext db,
            Guid accountId, DateTimeOffset? fromDate, DateTimeOffset? toDate, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var visible = await VisibleOrgsAsync(user, db, ct);

            var entries = await (
                from e in db.AccountingEntries
                join t in db.Transactions on e.TransactionId equals t.Id
                join a in db.Accounts on t.AccountId equals a.Id
                where e.LedgerAccountId == accountId
                      && visible.Contains(a.OrganizationId)
                      && (fromDate == null || e.CreatedAt >= fromDate)
                      && (toDate == null || e.CreatedAt <= toDate)
                orderby e.CreatedAt
                select new { e.CreatedAt, e.Debit, e.Credit, t.Description })
                .ToListAsync(ct);

            decimal running = 0;
            var lines = entries.Select(e =>
            {
                running += e.Debit - e.Credit;
                return new { date = e.CreatedAt, debit = e.Debit, credit = e.Credit, description = e.Description, balance = running };
            }).ToList();

            return Results.Ok(new { accountId, balance = running, lines });
        });

        // Recibos
        group.MapGet("/receipts", async (ITenantContext tenant, ICurrentUser user, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var visible = await VisibleOrgsAsync(user, db, ct);
            var list = await db.Receipts
                .Where(r => visible.Contains(r.OrganizationId))
                .OrderByDescending(r => r.IssuedAt)
                .Select(r => new { r.Id, r.Number, r.OrganizationId, r.DonorName, r.Amount, r.IssuedAt })
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        group.MapGet("/receipts/{id:guid}", async (Guid id, ITenantContext tenant, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var r = await db.Receipts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (r is null) return Results.NotFound();

            var orgName = await db.Organizations.Where(o => o.Id == r.OrganizationId).Select(o => o.Name).FirstOrDefaultAsync(ct);
            return Results.Ok(new
            {
                r.Id, r.Number, r.OrganizationId, organizationName = orgName,
                r.DonorName, donorDocument = r.DonorDocument, r.Amount, r.IssuedAt,
            });
        });

        return app;
    }

    private static async Task<HashSet<Guid>> VisibleOrgsAsync(ICurrentUser user, TenantDbContext db, CancellationToken ct)
    {
        if (!user.HasUser) return [];
        var memberIds = await db.OrgMembers.Where(m => m.UserId == user.UserId).Select(m => m.OrganizationId).ToListAsync(ct);
        var all = await db.Organizations.Select(o => new { o.Id, o.ParentId }).ToListAsync(ct);
        return OrgVisibility.VisibleOrgIds(memberIds, all.Select(o => (o.Id, o.ParentId)).ToList());
    }
}

public sealed record CreateAccountRequest(
    string Code,
    string Name,
    string? Type = null,
    string? NormalBalance = null,
    bool? Postable = null,
    Guid? ParentId = null);

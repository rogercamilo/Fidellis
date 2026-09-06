using Fidellis.Infrastructure.Audit;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Periods;

/// <summary>
/// Endpoints de fechamento de período (Onda 2 inc.2.6 / RF-FIN-170): fechar, reabrir (admin) e listar.
/// Mutações passam pelo <see cref="FinanceWriteFilter"/>; a reabertura é registrada em auditoria.
/// </summary>
public static class PeriodEndpoints
{
    public static IEndpointRouteBuilder MapPeriods(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/periods").WithTags("Finance/Periods").AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/", async (TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AccountingPeriods.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Select(p => new PeriodDto(p.Year, p.Month, p.Status, p.ClosedAt)).ToListAsync(ct)));

        g.MapPost("/{year:int}/{month:int}/close", async (
            int year, int month, PeriodService periods, ICurrentUser user, IAuditLog audit, CancellationToken ct) =>
        {
            if (user.UserId is not { } uid) return Results.BadRequest(new { error = "Usuário do request não identificado." });
            if (month is < 1 or > 12) return Results.BadRequest(new { error = "month inválido." });

            var p = await periods.CloseAsync(year, month, uid, ct);
            await audit.RecordAsync("period.closed", "accounting_period", $"{year}-{month:00}");
            return Results.Ok(new PeriodDto(p.Year, p.Month, p.Status, p.ClosedAt));
        });

        g.MapPost("/{year:int}/{month:int}/reopen", async (
            int year, int month, PeriodService periods, ICurrentUser user, IAuditLog audit, CancellationToken ct) =>
        {
            try
            {
                var p = await periods.ReopenAsync(year, month, user.Role ?? "", ct);
                if (p is null) return Results.NotFound();
                await audit.RecordAsync("period.reopened", "accounting_period", $"{year}-{month:00}");
                return Results.Ok(new PeriodDto(p.Year, p.Month, p.Status, p.ClosedAt));
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        return app;
    }
}

public sealed record PeriodDto(int Year, int Month, string Status, DateTimeOffset? ClosedAt);

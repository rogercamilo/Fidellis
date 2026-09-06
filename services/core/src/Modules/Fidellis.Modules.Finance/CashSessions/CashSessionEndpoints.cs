using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.CashSessions;

/// <summary>
/// Endpoints de caixa físico (Onda 2 inc.2.5): abrir/fechar sessão (dupla conferência) e depositar.
/// O usuário do request é quem abre (opened_by) e quem confere no fechamento (confirmed_by).
/// Mutações passam pelo <see cref="FinanceWriteFilter"/>.
/// </summary>
public static class CashSessionEndpoints
{
    public static IEndpointRouteBuilder MapCashSessions(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/cash-sessions").WithTags("Finance/CashSessions").AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/", async (string? status, TenantDbContext db, CancellationToken ct) =>
        {
            var q = db.CashSessions.AsQueryable();
            if (status is { Length: > 0 }) q = q.Where(s => s.Status == status);
            var list = await q.OrderByDescending(s => s.OpenedAt)
                .Select(s => new CashSessionDto(s.Id, s.AccountId, s.EventLabel, s.Status, s.CountedAmount, s.OpenedBy, s.ConfirmedBy, s.ClosedAt, s.DepositedMovementId))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        g.MapPost("/open", async (
            OpenCashSessionRequest req, CashSessionService sessions, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } uid) return Results.BadRequest(new { error = "Usuário do request não identificado." });
            try
            {
                var s = await sessions.OpenAsync(req.AccountId, uid, req.EventLabel, ct);
                return Results.Created($"/api/finance/cash-sessions/{s.Id}",
                    new CashSessionDto(s.Id, s.AccountId, s.EventLabel, s.Status, s.CountedAmount, s.OpenedBy, s.ConfirmedBy, s.ClosedAt, s.DepositedMovementId));
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        g.MapPost("/{id:guid}/close", async (
            Guid id, CloseCashSessionRequest req, CashSessionService sessions, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } uid) return Results.BadRequest(new { error = "Usuário do request não identificado." });
            try
            {
                var s = await sessions.CloseAsync(id, req.CountedAmount, uid, ct);
                return Results.Ok(new CashSessionDto(s.Id, s.AccountId, s.EventLabel, s.Status, s.CountedAmount, s.OpenedBy, s.ConfirmedBy, s.ClosedAt, s.DepositedMovementId));
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        g.MapPost("/{id:guid}/deposit", async (
            Guid id, DepositCashSessionRequest req, CashSessionService sessions, CancellationToken ct) =>
        {
            try
            {
                var s = await sessions.DepositAsync(id, req.BankAccountId, ct);
                return Results.Ok(new { id = s.Id, depositedMovementId = s.DepositedMovementId });
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        return app;
    }
}

public sealed record CashSessionDto(
    Guid Id, Guid AccountId, string? EventLabel, string Status, decimal? CountedAmount,
    Guid OpenedBy, Guid? ConfirmedBy, DateTimeOffset? ClosedAt, Guid? DepositedMovementId);

public sealed record OpenCashSessionRequest(Guid AccountId, string? EventLabel = null);
public sealed record CloseCashSessionRequest(decimal CountedAmount);
public sealed record DepositCashSessionRequest(Guid BankAccountId);

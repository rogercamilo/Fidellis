using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Offboarding do vínculo federado (#80 / ADR-0013): ao perder o vínculo no Formattio
/// (<c>Formando.ativo=false</c>/<c>deletedAt</c>), a recorrência de dízimo do membro <b>pausa</b>
/// (reversível) ou <b>encerra</b> (definitivo), <b>com aviso</b> ao membro. O financeiro já realizado é
/// preservado (prestação de contas). Roda no schema do tenant.
/// </summary>
public sealed class IntegrationOffboardService(TenantDbContext db, RecurringBillingService billing, MessageOutbox outbox)
{
    /// <summary>
    /// Pausa (ou encerra, se <paramref name="permanent"/>) as recorrências ativas/pausadas do membro
    /// federado e enfileira o aviso. Retorna <c>null</c> se o membro não existe.
    /// </summary>
    public async Task<(int Affected, string Action)?> OffboardAsync(
        string externalId, bool permanent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalId)) return null;
        var extId = externalId.Trim();
        var member = await db.Donors.FirstOrDefaultAsync(d => d.Source == "formattio" && d.ExternalId == extId, ct);
        if (member is null) return null;

        var recurringIds = await db.RecurringDonations
            .Where(r => r.DonorId == member.Id && (r.Status == "active" || r.Status == "paused"))
            .Select(r => r.Id).ToListAsync(ct);

        foreach (var id in recurringIds)
        {
            if (permanent) await billing.CancelAsync(id, ct);
            else await billing.PauseAsync(id, ct);
        }

        // Aviso ao membro (best-effort; respeita opt-out e exige e-mail). Só quando havia o que pausar/encerrar.
        if (recurringIds.Count > 0 && !member.ContactOptOut && member.Email is { Length: > 0 } email)
        {
            var subject = permanent ? "Sua contribuição recorrente foi encerrada" : "Sua contribuição recorrente foi pausada";
            var body = permanent
                ? $"Olá, {member.Name}. Como seu vínculo foi encerrado, suas contribuições recorrentes de dízimo foram encerradas. As contribuições já realizadas permanecem registradas."
                : $"Olá, {member.Name}. Suas contribuições recorrentes de dízimo foram pausadas e podem ser retomadas quando você retornar.";
            await outbox.EnqueueAsync(new EnqueueRequest(
                "membership_ended", email, subject, body,
                DonorId: member.Id, DedupeKey: $"offboard:{member.Id}:{(permanent ? "end" : "pause")}"), ct);
        }

        return (recurringIds.Count, permanent ? "canceled" : "paused");
    }
}

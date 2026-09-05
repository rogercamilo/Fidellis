using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Cria/registra o recebedor do PSP (destino do split PIX) de uma unidade. Onboarding/KYC
/// completo é entregável futuro — funciona em sandbox com dados de teste.
/// </summary>
public sealed class RecipientService(TenantDbContext db, IPaymentGateway gateway)
{
    public async Task<RecipientResult> CreateAsync(
        Guid organizationId, string name, string email, string document, string? pixKey, CancellationToken ct = default)
    {
        var created = await gateway.CreateRecipientAsync(new CreateRecipientRequest(name, email, document, pixKey), ct);

        var recipient = new PspRecipient
        {
            OrganizationId = organizationId,
            ProviderRecipientId = created.RecipientId,
            Status = created.Status is { Length: > 0 } s ? s : "active",
        };
        db.PspRecipients.Add(recipient);
        await db.SaveChangesAsync(ct);

        return new RecipientResult(recipient.Id, recipient.ProviderRecipientId, recipient.Status);
    }
}

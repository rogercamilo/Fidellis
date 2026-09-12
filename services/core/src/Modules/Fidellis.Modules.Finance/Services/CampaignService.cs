using System.Text;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>Campanha + progresso (arrecadado × meta) e se está ativa (status + janela).</summary>
public sealed record CampaignProgress(
    Guid Id, Guid OrganizationId, string Title, string Slug, string? Description,
    decimal? GoalAmount, decimal Raised, decimal Percent, bool Active,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, Guid? FundId, Guid? ProjectId, string Status);

/// <summary>Prestação de contas da campanha: arrecadado × meta × aplicado (despesas na dimensão vinculada).</summary>
public sealed record CampaignReport(Guid Id, string Title, decimal? GoalAmount, decimal Raised, decimal Applied, decimal Balance);

/// <summary>
/// Campanhas (D-08): earmark de finalidade sobre a entrada, com meta, janela e vínculo opcional a fundo
/// restrito/projeto. Progresso e prestação de contas são derivados das doações pagas e das despesas na
/// dimensão vinculada. Roda no schema do tenant.
/// </summary>
public sealed class CampaignService(TenantDbContext db, IClock clock)
{
    public async Task<Campaign> CreateAsync(
        Guid organizationId, string title, string? slug, decimal? goalAmount, string? description,
        DateTimeOffset? startsAt, DateTimeOffset? endsAt, Guid? fundId, Guid? projectId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Título é obrigatório.");
        if (endsAt is { } e && startsAt is { } s && e < s)
            throw new ArgumentException("A data final não pode ser anterior à inicial.");

        var normalized = Slugify(!string.IsNullOrWhiteSpace(slug) ? slug! : title);
        if (await db.Campaigns.AnyAsync(c => c.Slug == normalized, ct))
            throw new ArgumentException($"Já existe uma campanha com o identificador '{normalized}'.");

        var campaign = new Campaign
        {
            OrganizationId = organizationId,
            Title = title.Trim(),
            Slug = normalized,
            GoalAmount = goalAmount,
            Description = description?.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt,
            FundId = fundId,
            ProjectId = projectId,
        };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync(ct);
        return campaign;
    }

    public async Task<List<CampaignProgress>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var campaigns = await db.Campaigns.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        var result = new List<CampaignProgress>();
        foreach (var c in campaigns)
        {
            var p = await ProgressAsync(c, ct);
            if (!activeOnly || p.Active) result.Add(p);
        }
        return result;
    }

    public async Task<CampaignProgress?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var c = await db.Campaigns.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        return c is null ? null : await ProgressAsync(c, ct);
    }

    public async Task<Campaign?> SetStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        var c = await db.Campaigns.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        c.Status = status == "closed" ? "closed" : "active";
        await db.SaveChangesAsync(ct);
        return c;
    }

    public async Task<CampaignProgress> ProgressAsync(Campaign c, CancellationToken ct = default)
    {
        var raised = await db.Donations
            .Where(d => d.CampaignId == c.Id && d.Status == "paid").SumAsync(d => d.Amount, ct);
        var percent = c.GoalAmount is { } g && g > 0 ? Math.Round(raised / g * 100m, 1) : 0m;
        return new CampaignProgress(
            c.Id, c.OrganizationId, c.Title, c.Slug, c.Description, c.GoalAmount, raised, percent,
            IsActive(c), c.StartsAt, c.EndsAt, c.FundId, c.ProjectId, c.Status);
    }

    public async Task<CampaignReport?> ReportAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.Campaigns.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;

        var raised = await db.Donations
            .Where(d => d.CampaignId == c.Id && d.Status == "paid").SumAsync(d => d.Amount, ct);

        // Aplicado: despesas (débito) na dimensão vinculada — fundo restrito ou projeto.
        decimal applied = 0m;
        if (c.FundId is { } fundId)
            applied = await db.Transactions.Where(t => t.FundId == fundId && t.Kind == "debit").SumAsync(t => t.Amount, ct);
        else if (c.ProjectId is { } projectId)
            applied = await db.Transactions.Where(t => t.ProjectId == projectId && t.Kind == "debit").SumAsync(t => t.Amount, ct);

        return new CampaignReport(c.Id, c.Title, c.GoalAmount, raised, applied, raised - applied);
    }

    private bool IsActive(Campaign c)
    {
        if (c.Status != "active") return false;
        var now = clock.UtcNow;
        if (c.StartsAt is { } s && now < s) return false;
        if (c.EndsAt is { } e && now > e) return false;
        return true;
    }

    /// <summary>Slug ASCII: minúsculas, não-alfanumérico vira '-', colapsa/apara. Colisão tratada no Create.</summary>
    private static string Slugify(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
            sb.Append(ch is >= 'a' and <= 'z' or >= '0' and <= '9' ? ch : '-');
        var slug = sb.ToString().Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Length == 0 ? Guid.NewGuid().ToString("N")[..8] : slug;
    }
}

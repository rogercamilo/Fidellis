namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Tipo de entrada de 1ª classe (D-06), como <b>chaves técnicas estáveis</b> — o rótulo de exibição é
/// customizável por tenant (<c>FinanceSettings</c>). Taxonomia do parecer §4.1:
/// <list type="bullet">
/// <item><b>tithe</b> (dízimo) — só membro; recorrente/mensal.</item>
/// <item><b>offering</b> (oferta) — só membro; pontual; valor a mais.</item>
/// <item><b>donation</b> (doação) — não-membro; pública/anônima; pontual ou recorrente.</item>
/// </list>
/// O <b>ator</b> (membro × não-membro) é implicado pelo tipo; a <b>frequência</b> é derivada da
/// recorrência (<c>Entry.RecurringDonationId</c>).
/// </summary>
public static class EntryTypes
{
    public const string Tithe = "tithe";
    public const string Offering = "offering";
    public const string Donation = "donation";

    public static readonly IReadOnlyList<string> All = [Tithe, Offering, Donation];

    public static bool IsValid(string? type) => type is not null && All.Contains(type);
}

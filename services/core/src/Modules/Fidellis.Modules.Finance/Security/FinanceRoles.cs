namespace Fidellis.Modules.Finance.Security;

/// <summary>
/// Vocabulário de papéis financeiros (RF-FIN-171), alinhado à governança de conselho (D-02). Perfis
/// <b>somente-leitura</b> (conselho fiscal, contador) não alteram dados financeiros; os demais podem.
/// A separação "aprova × lança" da Q3 não vive aqui (é uma trava por endpoint via <see cref="CanLaunch"/>):
/// conselheiro/moderador <b>podem escrever</b> (para aprovar), mas não lançam títulos. Papel
/// desconhecido/nulo é permitido (compatibilidade com dev/testes e com o fluxo público sem usuário) —
/// a segregação real depende do BFF emitir o claim <c>role</c>.
/// </summary>
public static class FinanceRoles
{
    public const string Admin = "admin";

    /// <summary>Coordenador da equipe financeira (dia a dia). Lança; aprova a faixa baixa. (Era <c>treasurer</c>.)</summary>
    public const string Coordinator = "coordinator";

    /// <summary>Conselheiro responsável pelas finanças. Aprova faixa média; não lança. (Era <c>manager</c>.)</summary>
    public const string CouncilOfficer = "council_officer";

    /// <summary>Moderador / presidente do conselho. Aprova faixa alta; não lança.</summary>
    public const string CouncilChair = "council_chair";

    /// <summary>Conselho fiscal — fiscaliza (somente-leitura); não autoriza pagamentos.</summary>
    public const string FiscalCouncil = "fiscal_council";

    /// <summary>Contador (escritório terceirizado) — somente-leitura; rascunho → assina.</summary>
    public const string Accountant = "accountant";

    public const string Member = "member";

    private static readonly HashSet<string> ReadOnly =
        new(StringComparer.OrdinalIgnoreCase) { FiscalCouncil, Accountant };

    /// <summary>
    /// Operadores/lançadores (Q3): quem cria/edita dados financeiros (títulos a pagar etc.). Conselheiro
    /// e moderador aprovam, mas <b>não</b> lançam — a separação é feita por trava de endpoint, não pelo
    /// <see cref="FinanceWriteFilter"/> (que segue binário só-leitura × escreve).
    /// </summary>
    private static readonly HashSet<string> Launchers =
        new(StringComparer.OrdinalIgnoreCase) { Admin, Coordinator };

    /// <summary>Papéis que podem convidar/gerenciar a equipe (D-01, Q1): admin + coordenador.</summary>
    private static readonly HashSet<string> Inviters =
        new(StringComparer.OrdinalIgnoreCase) { Admin, Coordinator };

    /// <summary>Vocabulário completo de papéis atribuíveis a um membro.</summary>
    public static readonly IReadOnlyList<string> All =
        [Admin, Coordinator, CouncilOfficer, CouncilChair, FiscalCouncil, Accountant, Member];

    /// <summary>Pode alterar dados financeiros? Falso apenas para papéis explicitamente somente-leitura.</summary>
    public static bool CanWrite(string? role) => role is null || !ReadOnly.Contains(role);

    /// <summary>É um papel conhecido (atribuível)?</summary>
    public static bool IsValid(string? role) => role is not null && All.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>Pode convidar membros e montar a equipe (D-01)? Admin + coordenador.</summary>
    public static bool CanInvite(string? role) => role is not null && Inviters.Contains(role);

    /// <summary>
    /// Pode lançar/editar dados financeiros (Q3)? Admin + coordenador. Nulo passa (dev/testes/público);
    /// conselheiro/moderador aprovam mas não lançam.
    /// </summary>
    public static bool CanLaunch(string? role) => role is null || Launchers.Contains(role);
}

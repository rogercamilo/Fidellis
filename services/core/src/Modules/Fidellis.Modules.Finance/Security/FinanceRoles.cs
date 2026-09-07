namespace Fidellis.Modules.Finance.Security;

/// <summary>
/// Vocabulário de papéis financeiros (RF-FIN-171) e a política de escrita. Perfis <b>somente-leitura</b>
/// (conselho fiscal, contador) não alteram dados financeiros; os demais podem. Papel desconhecido/nulo
/// é permitido (compatibilidade com dev/testes e com o fluxo público sem usuário) — a segregação real
/// depende do BFF emitir o claim <c>role</c>.
/// </summary>
public static class FinanceRoles
{
    public const string Admin = "admin";
    public const string Treasurer = "treasurer";
    public const string Manager = "manager";
    public const string FiscalCouncil = "fiscal_council";
    public const string Accountant = "accountant";
    public const string Member = "member";

    private static readonly HashSet<string> ReadOnly =
        new(StringComparer.OrdinalIgnoreCase) { FiscalCouncil, Accountant };

    /// <summary>Vocabulário completo de papéis atribuíveis a um membro.</summary>
    public static readonly IReadOnlyList<string> All = [Admin, Treasurer, Manager, FiscalCouncil, Accountant, Member];

    /// <summary>Pode alterar dados financeiros? Falso apenas para papéis explicitamente somente-leitura.</summary>
    public static bool CanWrite(string? role) => role is null || !ReadOnly.Contains(role);

    /// <summary>É um papel conhecido (atribuível)?</summary>
    public static bool IsValid(string? role) => role is not null && All.Contains(role, StringComparer.OrdinalIgnoreCase);
}

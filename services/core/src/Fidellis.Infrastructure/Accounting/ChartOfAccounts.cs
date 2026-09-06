namespace Fidellis.Infrastructure.Accounting;

/// <summary>
/// Plano de contas padrão (semeado por tenant) e os códigos bem-conhecidos usados pela conciliação
/// automática das doações.
/// </summary>
public static class ChartOfAccounts
{
    /// <summary>Ativo: PIX a receber (débito).</summary>
    public const string Receivable = "1.1.3";

    /// <summary>Receita: dízimos e ofertas (crédito).</summary>
    public const string Revenue = "4.1.1";

    /// <summary>Banco (crédito no pagamento de despesas).</summary>
    public const string Bank = "1.1.2";

    /// <summary>Despesa: despesas gerais (débito no pagamento de Contas a Pagar).</summary>
    public const string Expense = "5.1.1";

    public sealed record AccountDef(string Code, string Name, string Type, string NormalBalance, bool Postable, string? ParentCode);

    /// <summary>Ordenado do pai para o filho (resolve <c>parent_id</c> na semeadura).</summary>
    public static readonly IReadOnlyList<AccountDef> Default =
    [
        new("1", "Ativo", "asset", "debit", false, null),
        new("1.1", "Disponível", "asset", "debit", false, "1"),
        new("1.1.1", "Caixa", "asset", "debit", true, "1.1"),
        new("1.1.2", "Bancos", "asset", "debit", true, "1.1"),
        new(Receivable, "PIX a receber", "asset", "debit", true, "1.1"),
        new("2", "Passivo", "liability", "credit", false, null),
        new("3", "Patrimônio Líquido", "equity", "credit", false, null),
        new("4", "Receitas", "revenue", "credit", false, null),
        new("4.1", "Doações", "revenue", "credit", false, "4"),
        new(Revenue, "Dízimos e ofertas", "revenue", "credit", true, "4.1"),
        new("5", "Despesas", "expense", "debit", false, null),
        new("5.1", "Despesas operacionais", "expense", "debit", false, "5"),
        new(Expense, "Despesas gerais", "expense", "debit", true, "5.1"),
    ];
}

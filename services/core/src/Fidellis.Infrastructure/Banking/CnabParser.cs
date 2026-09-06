using System.Globalization;

namespace Fidellis.Infrastructure.Banking;

/// <summary>
/// Parser (puro/testável) de retorno bancário <b>CNAB 400</b> de cobrança (layout base Febraban). Lê
/// os registros de detalhe (tipo <c>1</c> na primeira posição) e extrai a data de ocorrência, o valor
/// e o "nosso número" como identificador. Posições podem variar por banco — este é um baseline; ajustes
/// por layout entram por configuração no futuro. Não faz I/O.
/// </summary>
public static class CnabParser
{
    // Posições 0-based do registro de detalhe (tipo 1) no CNAB 400 cobrança (base Febraban).
    private const int NossoNumeroStart = 62, NossoNumeroLen = 12;
    private const int OccurrenceDateStart = 110, OccurrenceDateLen = 6; // DDMMAA
    private const int AmountStart = 152, AmountLen = 13;                 // 2 casas decimais implícitas

    public static IReadOnlyList<StatementTransaction> Parse(string content)
    {
        var result = new List<StatementTransaction>();
        if (string.IsNullOrWhiteSpace(content)) return result;

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < AmountStart + AmountLen) continue; // linha curta demais p/ detalhe
            if (line[0] != '1') continue;                        // só registros de detalhe

            if (!TryDate(Slice(line, OccurrenceDateStart, OccurrenceDateLen), out var posted)) continue;
            if (!TryCents(Slice(line, AmountStart, AmountLen), out var amount)) continue;

            var fit = Slice(line, NossoNumeroStart, NossoNumeroLen).Trim().TrimStart('0');
            result.Add(new StatementTransaction(
                string.IsNullOrEmpty(fit) ? null : fit,
                posted,
                amount, // retorno de cobrança = entrada (positivo)
                "Retorno CNAB"));
        }

        return result;
    }

    private static string Slice(string s, int start, int len)
        => start + len <= s.Length ? s.Substring(start, len) : string.Empty;

    private static bool TryDate(string ddmmaa, out DateOnly date)
    {
        date = default;
        if (ddmmaa.Length != 6 || !ddmmaa.All(char.IsDigit)) return false;
        int d = int.Parse(ddmmaa[..2]), mo = int.Parse(ddmmaa.Substring(2, 2)), yy = int.Parse(ddmmaa.Substring(4, 2));
        if (mo is < 1 or > 12 || d is < 1 or > 31) return false;
        date = new DateOnly(2000 + yy, mo, d);
        return true;
    }

    private static bool TryCents(string digits, out decimal amount)
    {
        amount = 0m;
        var trimmed = digits.Trim();
        if (trimmed.Length == 0 || !trimmed.All(char.IsDigit)) return false;
        if (!long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var cents)) return false;
        amount = cents / 100m;
        return true;
    }
}

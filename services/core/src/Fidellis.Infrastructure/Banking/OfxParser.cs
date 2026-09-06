using System.Globalization;
using System.Text.RegularExpressions;

namespace Fidellis.Infrastructure.Banking;

/// <summary>Transação normalizada de um extrato OFX.</summary>
public sealed record OfxTransaction(string? FitId, DateOnly PostedAt, decimal Amount, string? Memo);

/// <summary>
/// Parser (puro/testável) de extrato OFX. Tolerante ao SGML do OFX 1.x (tags nem sempre fechadas):
/// extrai cada bloco <c>STMTTRN</c> e seus campos por regex. Não faz I/O.
/// </summary>
public static partial class OfxParser
{
    public static IReadOnlyList<OfxTransaction> Parse(string content)
    {
        var result = new List<OfxTransaction>();
        if (string.IsNullOrWhiteSpace(content)) return result;

        foreach (Match block in StmtTrnRegex().Matches(content))
        {
            var body = block.Groups[1].Value;
            var amountRaw = Field(body, "TRNAMT");
            var dateRaw = Field(body, "DTPOSTED");
            if (amountRaw is null || dateRaw is null) continue;

            if (!decimal.TryParse(amountRaw.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                continue;
            if (!TryParseOfxDate(dateRaw, out var posted))
                continue;

            result.Add(new OfxTransaction(
                Field(body, "FITID"),
                posted,
                amount,
                Field(body, "MEMO") ?? Field(body, "NAME")));
        }

        return result;
    }

    private static string? Field(string body, string tag)
    {
        var m = Regex.Match(body, $@"<{tag}>\s*([^<\r\n]+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static bool TryParseOfxDate(string raw, out DateOnly date)
    {
        // DTPOSTED ex.: 20260515120000[-3:GMT] — usamos os 8 primeiros dígitos (yyyyMMdd).
        var digits = new string(raw.TrimStart().TakeWhile(char.IsDigit).ToArray());
        if (digits.Length >= 8 &&
            int.TryParse(digits[..4], out var y) &&
            int.TryParse(digits.Substring(4, 2), out var mo) &&
            int.TryParse(digits.Substring(6, 2), out var d) &&
            mo is >= 1 and <= 12 && d is >= 1 and <= 31)
        {
            date = new DateOnly(y, mo, d);
            return true;
        }
        date = default;
        return false;
    }

    [GeneratedRegex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StmtTrnRegex();
}

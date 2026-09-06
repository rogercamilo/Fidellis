namespace Fidellis.Modules.Reporting;

/// <summary>Cálculos puros/testáveis de reporting (série mensal com zero-fill).</summary>
public static class ReportingCalc
{
    public sealed record MonthPoint(string Month, decimal Total, int Count);

    /// <summary>
    /// Agrupa <paramref name="rows"/> (data, valor) nos últimos <paramref name="months"/> meses,
    /// do mais antigo ao mais novo, preenchendo com zero os meses sem doação.
    /// </summary>
    public static IReadOnlyList<MonthPoint> MonthlySeries(
        IEnumerable<(DateTimeOffset Date, decimal Amount)> rows, int months, DateTimeOffset now)
    {
        months = Math.Clamp(months, 1, 60);
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-(months - 1));

        var buckets = new Dictionary<string, (decimal Total, int Count)>();
        for (var i = 0; i < months; i++)
            buckets[Key(start.AddMonths(i))] = (0m, 0);

        foreach (var (date, amount) in rows)
        {
            var key = Key(date);
            if (buckets.TryGetValue(key, out var b))
                buckets[key] = (b.Total + amount, b.Count + 1);
        }

        return Enumerable.Range(0, months)
            .Select(i =>
            {
                var key = Key(start.AddMonths(i));
                var b = buckets[key];
                return new MonthPoint(key, b.Total, b.Count);
            })
            .ToList();
    }

    private static string Key(DateTimeOffset d) => $"{d.Year:D4}-{d.Month:D2}";
}

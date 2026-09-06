using Fidellis.Modules.Reporting;
using Xunit;

namespace Fidellis.IntegrationTests;

public class ReportingCalcTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MonthlySeries_returns_ordered_months_with_zero_fill()
    {
        var rows = new (DateTimeOffset, decimal)[]
        {
            (new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), 30m),
            (new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero), 100m),
            (new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero), 20m),
            (new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 999m), // fora da janela
        };

        var series = ReportingCalc.MonthlySeries(rows, 3, Now);

        Assert.Equal(3, series.Count);
        Assert.Equal(["2026-04", "2026-05", "2026-06"], series.Select(s => s.Month));
        Assert.Equal(30m, series[0].Total);
        Assert.Equal(1, series[0].Count);
        Assert.Equal(0m, series[1].Total); // maio sem doação
        Assert.Equal(0, series[1].Count);
        Assert.Equal(120m, series[2].Total); // junho: 100 + 20
        Assert.Equal(2, series[2].Count);
    }

    [Fact]
    public void MonthlySeries_last_bucket_is_current_month()
    {
        var series = ReportingCalc.MonthlySeries([], 12, Now);
        Assert.Equal(12, series.Count);
        Assert.Equal("2026-06", series[^1].Month);
        Assert.All(series, p => Assert.Equal(0m, p.Total));
    }
}

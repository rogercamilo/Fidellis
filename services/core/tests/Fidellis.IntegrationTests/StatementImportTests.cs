using Fidellis.Infrastructure.Banking;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Conciliação — import de extrato OFX (Onda 3 inc.3.0): parser + dedupe.</summary>
public class StatementImportTests
{
    private const string Ofx = """
        OFXHEADER:100
        <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>
        <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260515120000[-3:GMT]<TRNAMT>300.00<FITID>A1<MEMO>Doacao PIX</STMTTRN>
        <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260516000000<TRNAMT>-120.50<FITID>A2<NAME>Energia</STMTTRN>
        </BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
        """;

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public void Parser_extracts_transactions()
    {
        var txs = OfxParser.Parse(Ofx);
        Assert.Equal(2, txs.Count);
        Assert.Equal("A1", txs[0].FitId);
        Assert.Equal(new DateOnly(2026, 5, 15), txs[0].PostedAt);
        Assert.Equal(300.00m, txs[0].Amount);
        Assert.Equal("Doacao PIX", txs[0].Memo);
        Assert.Equal(-120.50m, txs[1].Amount);
        Assert.Equal("Energia", txs[1].Memo); // NAME quando não há MEMO
    }

    [Fact]
    public async Task Import_persists_lines_and_dedupes_on_reimport()
    {
        var tdb = TDb($"ofx_{Guid.NewGuid()}");
        var treasury = new TreasuryService(tdb);
        var acc = await treasury.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        var svc = new StatementImportService(tdb);

        var first = await svc.ImportAsync(acc.Id, "ofx", "mai/2026", Ofx);
        Assert.Equal(2, first.Imported);
        Assert.Equal(0, first.Skipped);
        Assert.Equal(2, await tdb.BankStatementLines.CountAsync());

        var again = await svc.ImportAsync(acc.Id, "ofx", "mai/2026 (reimport)", Ofx);
        Assert.Equal(0, again.Imported);
        Assert.Equal(2, again.Skipped);                       // dedupe por fit_id
        Assert.Equal(2, await tdb.BankStatementLines.CountAsync()); // não duplicou
    }

    [Fact]
    public async Task Import_rejects_unknown_account()
    {
        var tdb = TDb($"ofx_{Guid.NewGuid()}");
        var svc = new StatementImportService(tdb);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ImportAsync(Guid.NewGuid(), "ofx", null, Ofx));
    }
}

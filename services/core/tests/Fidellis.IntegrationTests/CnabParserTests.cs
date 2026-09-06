using Fidellis.Infrastructure.Banking;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Conciliação — retorno CNAB 400 (Onda 3 inc.3.2): parser + import.</summary>
public class CnabParserTests
{
    /// <summary>Monta uma linha de detalhe CNAB 400 com os campos nas posições esperadas.</summary>
    private static string DetailLine(string nossoNumero, string ddmmaa, string amountCents13)
    {
        var line = new string(' ', 400).ToCharArray();
        line[0] = '1';                                   // registro de detalhe
        nossoNumero.CopyTo(0, line, 62, nossoNumero.Length);
        ddmmaa.CopyTo(0, line, 110, 6);
        amountCents13.CopyTo(0, line, 152, 13);
        return new string(line);
    }

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public void Parser_reads_detail_records()
    {
        var content = DetailLine("000000012345", "200526", "0000000030000") + "\n" +
                      "0HEADER (ignorado, não começa com 1)".PadRight(400);
        var txs = CnabParser.Parse(content);

        Assert.Single(txs);
        Assert.Equal("12345", txs[0].FitId);
        Assert.Equal(new DateOnly(2026, 5, 20), txs[0].PostedAt);
        Assert.Equal(300.00m, txs[0].Amount);
    }

    [Fact]
    public async Task Import_cnab_persists_lines()
    {
        var tdb = TDb($"cnab_{Guid.NewGuid()}");
        var treasury = new TreasuryService(tdb);
        var acc = await treasury.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        var svc = new StatementImportService(tdb);

        var content = DetailLine("000000012345", "200526", "0000000030000") + "\n" +
                      DetailLine("000000067890", "210526", "0000000012550");
        var (_, imported, _) = await svc.ImportAsync(acc.Id, "cnab", "retorno.ret", content);

        Assert.Equal(2, imported);
        Assert.Equal(2, await tdb.BankStatementLines.CountAsync());
    }
}

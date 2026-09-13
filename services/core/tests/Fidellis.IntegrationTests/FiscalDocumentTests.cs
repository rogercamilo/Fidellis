using System.Collections.Concurrent;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Storage;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Faturamento (#79): registro/vínculo de NF, gated por "Gestão avançada" (D-09), só sobre recebível comercial.</summary>
public class FiscalDocumentTests
{
    /// <summary>Storage em memória para exercitar anexar/baixar PDF.</summary>
    private sealed class MemoryStorage : IObjectStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> _store = new();
        public bool Enabled => true;
        public Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
        { _store[key] = content; return Task.CompletedTask; }
        public Task<byte[]?> GetAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);
    }

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static async Task<Receivable> SeedReceivableAsync(TenantDbContext db, Guid org, string source)
    {
        var r = new Receivable { OrganizationId = org, Amount = 500m, DueDate = new DateOnly(2026, 6, 1), Source = source };
        db.Receivables.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    private static async Task SetAdvancedAsync(TenantDbContext db, bool on)
    {
        db.FinanceSettings.Add(new FinanceSettings { AdvancedManagement = on });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Registers_nf_linked_to_commercial_receivable()
    {
        var tdb = TDb($"fd_{Guid.NewGuid()}");
        var svc = new FiscalDocumentService(tdb, new MemoryStorage());
        var org = Guid.NewGuid();
        await SetAdvancedAsync(tdb, true);
        var recv = await SeedReceivableAsync(tdb, org, FiscalReceivableSources.Service);

        var doc = await svc.RegisterAsync(org, "NFSE", "2026/123", "A", "chave-x", 500m, null, recv.Id, "Curso", default);

        Assert.Equal(FiscalDocumentTypes.Nfse, doc.Type); // normalizado p/ minúsculo
        Assert.Equal("registered", doc.Status);
        Assert.Equal(recv.Id, doc.ReceivableId);
        Assert.Single(await tdb.FiscalDocuments.ToListAsync());
    }

    [Fact]
    public async Task Rejects_when_advanced_management_off()
    {
        var tdb = TDb($"fd_{Guid.NewGuid()}");
        var svc = new FiscalDocumentService(tdb, new MemoryStorage());
        var org = Guid.NewGuid();
        await SetAdvancedAsync(tdb, false);
        var recv = await SeedReceivableAsync(tdb, org, FiscalReceivableSources.Service);

        await Assert.ThrowsAsync<FiscalDocumentException>(() =>
            svc.RegisterAsync(org, "nfse", "1", null, null, 500m, null, recv.Id, null, default));
    }

    [Fact]
    public async Task Rejects_nf_over_donation_receivable()
    {
        var tdb = TDb($"fd_{Guid.NewGuid()}");
        var svc = new FiscalDocumentService(tdb, new MemoryStorage());
        var org = Guid.NewGuid();
        await SetAdvancedAsync(tdb, true);
        var pledge = await SeedReceivableAsync(tdb, org, "pledge"); // doação/promessa — usa recibo, não NF

        var ex = await Assert.ThrowsAsync<FiscalDocumentException>(() =>
            svc.RegisterAsync(org, "nfse", "1", null, null, 500m, null, pledge.Id, null, default));
        Assert.Contains("comercial", ex.Message);
    }

    [Fact]
    public async Task Rejects_invalid_type_and_nonpositive_amount()
    {
        var tdb = TDb($"fd_{Guid.NewGuid()}");
        var svc = new FiscalDocumentService(tdb, new MemoryStorage());
        var org = Guid.NewGuid();
        await SetAdvancedAsync(tdb, true);
        var recv = await SeedReceivableAsync(tdb, org, FiscalReceivableSources.Sale);

        await Assert.ThrowsAsync<FiscalDocumentException>(() =>
            svc.RegisterAsync(org, "recibo", "1", null, null, 500m, null, recv.Id, null, default));
        await Assert.ThrowsAsync<FiscalDocumentException>(() =>
            svc.RegisterAsync(org, "nfe", "1", null, null, 0m, null, recv.Id, null, default));
    }

    [Fact]
    public async Task Attaches_and_reads_pdf()
    {
        var tdb = TDb($"fd_{Guid.NewGuid()}");
        var svc = new FiscalDocumentService(tdb, new MemoryStorage());
        var org = Guid.NewGuid();
        await SetAdvancedAsync(tdb, true);
        var recv = await SeedReceivableAsync(tdb, org, FiscalReceivableSources.Service);
        var doc = await svc.RegisterAsync(org, "nfse", "1", null, null, 500m, null, recv.Id, null, default);

        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        var updated = await svc.AttachPdfAsync(doc.Id, pdf, default);
        Assert.NotNull(updated!.PdfObjectKey);
        Assert.Equal(pdf, await svc.GetPdfAsync(doc.Id, default));
    }
}

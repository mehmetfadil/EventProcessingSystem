using EventProcessing.Core.Enums;
using EventProcessing.Core.Models;
using EventProcessing.Infrastructure.Data;
using EventProcessing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EventProcessing.UnitTests;

public class SummaryRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SummaryRepository _repository;

    public SummaryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UnitTestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AppDbContext(options);
        var logger = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .CreateLogger<SummaryRepository>();
        _repository = new SummaryRepository(_db, logger);
    }

    public void Dispose() => _db.Dispose();

    // =========================================================================
    // BAŞARILI SENARYOLAR — Yeni event işleme
    // =========================================================================

    [Fact]
    public async Task ProcessEvent_NewCredit_CreatesSummary()
    {
        var evt = CreateEvent(Guid.NewGuid(), "cust-1", TransactionType.Credit, 200m, "TRY");

        await _repository.ProcessEventTransactionallyAsync(evt);

        var summary = await _repository.GetSummaryAsync("cust-1", evt.OccurredAt.Date, "TRY");
        Assert.NotNull(summary);
        Assert.Equal(200m, summary!.TotalCredit);
        Assert.Equal(0m, summary.TotalDebit);
        Assert.Equal(200m, summary.NetAmount);
        Assert.Equal(1, summary.UniqueEventCount);
    }

    [Fact]
    public async Task ProcessEvent_NewDebit_CreatesSummary()
    {
        var evt = CreateEvent(Guid.NewGuid(), "cust-2", TransactionType.Debit, 75m, "USD");

        await _repository.ProcessEventTransactionallyAsync(evt);

        var summary = await _repository.GetSummaryAsync("cust-2", evt.OccurredAt.Date, "USD");
        Assert.NotNull(summary);
        Assert.Equal(0m, summary!.TotalCredit);
        Assert.Equal(75m, summary.TotalDebit);
        Assert.Equal(-75m, summary.NetAmount);
    }

    // =========================================================================
    // DUPLICATE KONTROLÜ — Idempotency
    // =========================================================================

    [Fact]
    public async Task ProcessEvent_Duplicate_IsSkipped()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateEvent(eventId, "cust-3", TransactionType.Credit, 50m, "EUR");

        await _repository.ProcessEventTransactionallyAsync(evt);
        await _repository.ProcessEventTransactionallyAsync(evt); // duplicate

        var summary = await _repository.GetSummaryAsync("cust-3", evt.OccurredAt.Date, "EUR");
        Assert.Equal(1, summary!.UniqueEventCount);
        Assert.Equal(50m, summary.TotalCredit);
    }

    // =========================================================================
    // (Aggregation — Aynı müşteri/gün/para birimi
    // =========================================================================

    [Fact]
    public async Task ProcessEvent_MultipleEvents_AggregatesCorrectly()
    {
        var date = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var e1 = CreateEvent(Guid.NewGuid(), "cust-4", TransactionType.Credit, 100m, "TRY", date);
        var e2 = CreateEvent(Guid.NewGuid(), "cust-4", TransactionType.Debit, 30m, "TRY", date);
        var e3 = CreateEvent(Guid.NewGuid(), "cust-4", TransactionType.Credit, 50m, "TRY", date);

        await _repository.ProcessEventTransactionallyAsync(e1);
        await _repository.ProcessEventTransactionallyAsync(e2);
        await _repository.ProcessEventTransactionallyAsync(e3);

        var summary = await _repository.GetSummaryAsync("cust-4", date.Date, "TRY");
        Assert.Equal(150m, summary!.TotalCredit);
        Assert.Equal(30m, summary.TotalDebit);
        Assert.Equal(120m, summary.NetAmount); // 150 - 30
        Assert.Equal(3, summary.UniqueEventCount);
    }

    // =========================================================================
    // İZOLASYON — Farklı müşteriler, farklı tarihler
    // =========================================================================

    [Fact]
    public async Task ProcessEvent_DifferentCustomers_IndependentSummaries()
    {
        var e1 = CreateEvent(Guid.NewGuid(), "cust-a", TransactionType.Credit, 100m, "TRY");
        var e2 = CreateEvent(Guid.NewGuid(), "cust-b", TransactionType.Credit, 200m, "TRY");

        await _repository.ProcessEventTransactionallyAsync(e1);
        await _repository.ProcessEventTransactionallyAsync(e2);

        var sA = await _repository.GetSummaryAsync("cust-a", e1.OccurredAt.Date, "TRY");
        var sB = await _repository.GetSummaryAsync("cust-b", e1.OccurredAt.Date, "TRY");
        Assert.Equal(100m, sA!.TotalCredit);
        Assert.Equal(200m, sB!.TotalCredit);
        Assert.NotEqual(sA.Id, sB.Id);
    }

    [Fact]
    public async Task ProcessEvent_SameCustomerDifferentDate_SeparateSummaries()
    {
        var e1 = CreateEvent(Guid.NewGuid(), "cust-5", TransactionType.Credit, 100m, "TRY",
            new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));
        var e2 = CreateEvent(Guid.NewGuid(), "cust-5", TransactionType.Credit, 200m, "TRY",
            new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc));

        await _repository.ProcessEventTransactionallyAsync(e1);
        await _repository.ProcessEventTransactionallyAsync(e2);

        var s1 = await _repository.GetSummaryAsync("cust-5", new DateTime(2026, 8, 7), "TRY");
        var s2 = await _repository.GetSummaryAsync("cust-5", new DateTime(2026, 8, 8), "TRY");
        Assert.Equal(100m, s1!.TotalCredit);
        Assert.Equal(200m, s2!.TotalCredit);
    }

    // =========================================================================
    // SINIR DURUMLAR — Var olmayan kayıt
    // =========================================================================

    [Fact]
    public async Task GetSummary_NonExistent_ReturnsNull()
    {
        var result = await _repository.GetSummaryAsync("no-one", DateTime.UtcNow.Date, "XXX");
        Assert.Null(result);
    }

    // =========================================================================
    // VERİ BÜTÜNLÜĞÜ — ProcessedEvents kaydı
    // =========================================================================

    [Fact]
    public async Task ProcessEvent_AddsProcessedEventRecord()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateEvent(eventId, "cust-6", TransactionType.Credit, 50m, "TRY");

        await _repository.ProcessEventTransactionallyAsync(evt);

        var exists = await _db.ProcessedEvents.AnyAsync(e => e.EventId == eventId);
        Assert.True(exists);
    }

    private static TransactionEvent CreateEvent(
        Guid eventId, string customerId, TransactionType type,
        decimal amount, string currency, DateTime? occurredAt = null)
    {
        return new TransactionEvent
        {
            EventId = eventId,
            CustomerId = customerId,
            Type = type,
            Amount = amount,
            Currency = currency,
            OccurredAt = occurredAt ?? new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc)
        };
    }
}

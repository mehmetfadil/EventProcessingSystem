using EventProcessing.Core.Enums;
using EventProcessing.Core.Interfaces;
using EventProcessing.Core.Models;
using EventProcessing.Infrastructure.Data;
using EventProcessing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventProcessing.IntegrationTests;

/// <summary>
/// Docker konteynerlarına (SQL Server + RabbitMQ) karşı çalışan entegrasyon testleri.
/// testlerin çalışması için `docker compose up -d` ile altyapının ayağa kalkmış olması gerekir.
/// </summary>
public class EventProcessingIntegrationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SummaryRepository _repository;

    private static readonly string TestCustomerId = $"integration-test-{Guid.NewGuid():N}";
    private static readonly DateTime TestDate = new(2026, 8, 8);

    public EventProcessingIntegrationTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
            ?? "Server=127.0.0.1;Database=EventProcessingIntegrationTestDb;User Id=sa;Password=YourStrong!Pass123;TrustServerCertificate=True;";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", connectionString }
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(config.GetConnectionString("DefaultConnection"))
            .Options;

        _db = new AppDbContext(options);

        // Test veritabanını sıfırdan oluştur
        _db.Database.EnsureDeleted();
        _db.Database.Migrate();

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        _repository = new SummaryRepository(_db, loggerFactory.CreateLogger<SummaryRepository>());
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // =========================================================================
    // TEST 1: Yeni bir event işlenince DailySummary oluşuyor mu?
    // =========================================================================
    [Fact]
    public async Task ProcessEvent_NewEvent_CreatesDailySummary()
    {
        var evt = new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            CustomerId = TestCustomerId,
            Type = TransactionType.Credit,
            Amount = 100m,
            Currency = "TRY",
            OccurredAt = TestDate.AddHours(10)
        };

        await _repository.ProcessEventTransactionallyAsync(evt);

        var summary = await _repository.GetSummaryAsync(TestCustomerId, TestDate, "TRY");
        Assert.NotNull(summary);
        Assert.Equal(100m, summary!.TotalCredit);
        Assert.Equal(0m, summary.TotalDebit);
        Assert.Equal(100m, summary.NetAmount);
        Assert.Equal(1, summary.UniqueEventCount);
    }

    // =========================================================================
    // TEST 2: Aynı event iki kere gönderilirse duplicate atlanıyor mu?
    // =========================================================================
    [Fact]
    public async Task ProcessEvent_DuplicateEvent_SkipsDuplicate()
    {
        var eventId = Guid.NewGuid();
        var evt = new TransactionEvent
        {
            EventId = eventId,
            CustomerId = TestCustomerId,
            Type = TransactionType.Credit,
            Amount = 50m,
            Currency = "USD",
            OccurredAt = TestDate
        };

        await _repository.ProcessEventTransactionallyAsync(evt);
        await _repository.ProcessEventTransactionallyAsync(evt); // duplicate

        var summary = await _repository.GetSummaryAsync(TestCustomerId, TestDate, "USD");
        Assert.NotNull(summary);
        Assert.Equal(50m, summary!.TotalCredit); // hâlâ 50, çift sayılmadı
        Assert.Equal(1, summary.UniqueEventCount); // duplicate atlandı
    }

    // =========================================================================
    // TEST 3: Credit + Debit aynı müşteri/gün/para biriminde doğru toplanıyor mu?
    // =========================================================================
    [Fact]
    public async Task ProcessEvent_CreditAndDebit_AggregatesCorrectly()
    {
        var credit = new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            CustomerId = TestCustomerId,
            Type = TransactionType.Credit,
            Amount = 200m,
            Currency = "TRY",
            OccurredAt = TestDate.AddHours(9)
        };
        var debit = new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            CustomerId = TestCustomerId,
            Type = TransactionType.Debit,
            Amount = 80m,
            Currency = "TRY",
            OccurredAt = TestDate.AddHours(14)
        };

        await _repository.ProcessEventTransactionallyAsync(credit);
        await _repository.ProcessEventTransactionallyAsync(debit);

        var summary = await _repository.GetSummaryAsync(TestCustomerId, TestDate, "TRY");
        Assert.NotNull(summary);
        Assert.Equal(200m, summary!.TotalCredit);
        Assert.Equal(80m, summary.TotalDebit);
        Assert.Equal(120m, summary.NetAmount); // 200 - 80
        Assert.Equal(2, summary.UniqueEventCount);
    }

    // =========================================================================
    // TEST 4: Var olmayan özet sorgulanınca null dönüyor mu?
    // =========================================================================
    [Fact]
    public async Task GetSummary_NonExistent_ReturnsNull()
    {
        var summary = await _repository.GetSummaryAsync("nonexistent-customer", TestDate, "XYZ");
        Assert.Null(summary);
    }

    // =========================================================================
    // TEST 5: Aynı müşteri, aynı gün, farklı para birimleri ayrı özetleniyor mu?
    // =========================================================================
    [Fact]
    public async Task ProcessEvent_DifferentCurrencies_SeparateSummaries()
    {
        var evtTry = new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            CustomerId = TestCustomerId,
            Type = TransactionType.Credit,
            Amount = 100m,
            Currency = "TRY",
            OccurredAt = TestDate
        };
        var evtUsd = new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            CustomerId = TestCustomerId,
            Type = TransactionType.Credit,
            Amount = 100m,
            Currency = "USD",
            OccurredAt = TestDate
        };

        await _repository.ProcessEventTransactionallyAsync(evtTry);
        await _repository.ProcessEventTransactionallyAsync(evtUsd);

        var summaryTry = await _repository.GetSummaryAsync(TestCustomerId, TestDate, "TRY");
        var summaryUsd = await _repository.GetSummaryAsync(TestCustomerId, TestDate, "USD");

        Assert.NotNull(summaryTry);
        Assert.NotNull(summaryUsd);
        Assert.Equal(100m, summaryTry!.TotalCredit);
        Assert.Equal(100m, summaryUsd!.TotalCredit);
        Assert.NotEqual(summaryTry.Id, summaryUsd.Id); // farklı kayıtlar
    }

    // =========================================================================
    // TEST 6: İşlenmiş event ProcessedEvents tablosuna ekleniyor mu?
    // =========================================================================
    [Fact]
    public async Task ProcessEvent_AddsToProcessedEvents()
    {
        var eventId = Guid.NewGuid();
        var evt = new TransactionEvent
        {
            EventId = eventId,
            CustomerId = TestCustomerId,
            Type = TransactionType.Debit,
            Amount = 75m,
            Currency = "EUR",
            OccurredAt = TestDate
        };

        await _repository.ProcessEventTransactionallyAsync(evt);

        var isProcessed = await _db.ProcessedEvents.AnyAsync(e => e.EventId == eventId);
        Assert.True(isProcessed);
    }
}

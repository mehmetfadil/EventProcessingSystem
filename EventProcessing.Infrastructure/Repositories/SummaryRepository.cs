using EventProcessing.Core.Enums;
using EventProcessing.Core.Interfaces;
using EventProcessing.Core.Models;
using EventProcessing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventProcessing.Infrastructure.Repositories
{
    public class SummaryRepository : ISummaryRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SummaryRepository> _logger;

        public SummaryRepository(AppDbContext context, ILogger<SummaryRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DailySummary?> GetSummaryAsync(string customerId, DateTime date, string currency, CancellationToken cancellationToken = default)
        {
            return await _context.DailySummaries
                .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.Date == date && s.Currency == currency, cancellationToken);
        }

        public async Task ProcessEventTransactionallyAsync(TransactionEvent transactionEvent, CancellationToken cancellationToken = default)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);

            try
            {
                // Idempotency Kontrolü
                bool isProcessed = await _context.ProcessedEvents
                    .AnyAsync(e => e.EventId == transactionEvent.EventId, cancellationToken);

                if (isProcessed)
                {
                    _logger.LogWarning("Event {EventId} zaten işlenmiş. Duplicate atlanıyor.", transactionEvent.EventId);
                    return; 
                }

                // Daha sonra tekrar işlenmemesi için ID'yi kaydediyoruz
                _context.ProcessedEvents.Add(new ProcessedEvent
                {
                    EventId = transactionEvent.EventId,
                    ProcessedAt = DateTime.UtcNow
                });

                var date = transactionEvent.OccurredAt.Date;
                var summary = await GetSummaryAsync(transactionEvent.CustomerId, date, transactionEvent.Currency, cancellationToken);

                if (summary == null)
                {
                    summary = new DailySummary
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = transactionEvent.CustomerId,
                        Date = date,
                        Currency = transactionEvent.Currency,
                        TotalCredit = 0,
                        TotalDebit = 0,
                        NetAmount = 0,
                        UniqueEventCount = 0,
                        LastUpdatedAt = DateTime.UtcNow
                    };
                    _context.DailySummaries.Add(summary);
                }

                // finansal hesaplamalar
                if (transactionEvent.Type == TransactionType.Credit)
                {
                    summary.TotalCredit += transactionEvent.Amount;
                    summary.NetAmount += transactionEvent.Amount;
                }
                else if (transactionEvent.Type == TransactionType.Debit)
                {
                    summary.TotalDebit += transactionEvent.Amount;
                    summary.NetAmount -= transactionEvent.Amount;
                }

                summary.UniqueEventCount += 1;
                summary.LastUpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // İki işlem aynı anda hatası
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(ex, "Event {EventId} için eşzamanlı çakışma önlendi.", transactionEvent.EventId);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Event {EventId} işlenirken kritik hata.", transactionEvent.EventId);
                throw;
            }
        }
    }
}
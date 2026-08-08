using EventProcessing.Core.Models;

namespace EventProcessing.Core.Interfaces
{
    public interface ISummaryRepository
    {
        Task<DailySummary?> GetSummaryAsync(string customerId, DateTime date, string currency, CancellationToken cancellationToken = default);
        Task ProcessEventTransactionallyAsync(TransactionEvent transactionEvent, CancellationToken cancellationToken = default);
    }
}
using EventProcessing.Core.Models;

namespace EventProcessing.Core.Interfaces
{
    public interface IEventPublisher
    {
        Task PublishBatchAsync(IEnumerable<TransactionEvent> events, CancellationToken cancellationToken = default);
    }
}
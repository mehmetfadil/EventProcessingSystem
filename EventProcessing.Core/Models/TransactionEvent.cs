using EventProcessing.Core.Enums;

namespace EventProcessing.Core.Models
{
    public class TransactionEvent
    {
        public Guid EventId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
    }
}
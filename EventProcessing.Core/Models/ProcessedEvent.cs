namespace EventProcessing.Core.Models
{
    public class ProcessedEvent
    {
        public Guid EventId { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}

namespace EventProcessing.IngestionApi.DTOs
{
    public class BatchResponse
    {
        public Guid BatchId { get; set; }
        public int AcceptedCount { get; set; }
    }
}
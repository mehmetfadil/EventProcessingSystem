namespace EventProcessing.Core.Models
{
    public class DailySummary
    {
        public Guid Id { get; set; }

        public string CustomerId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Currency { get; set; } = string.Empty;

        public decimal TotalCredit { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal NetAmount { get; set; } // Toplam Credit - Toplam Debit
        public int UniqueEventCount { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
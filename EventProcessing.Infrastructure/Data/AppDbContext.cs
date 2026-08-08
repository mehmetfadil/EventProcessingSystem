using EventProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EventProcessing.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<DailySummary> DailySummaries { get; set; }
        public DbSet<ProcessedEvent> ProcessedEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DailySummary>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.CustomerId, e.Date, e.Currency }).IsUnique();

                entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.TotalCredit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalDebit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NetAmount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<ProcessedEvent>(entity =>
            {
                entity.HasKey(e => e.EventId);
            });
        }
    }
}
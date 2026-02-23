using Microsoft.EntityFrameworkCore;
using SentimentAnalyzer.API.Data.Entities;

namespace SentimentAnalyzer.API.Data;

public class InsuranceAnalysisDbContext : DbContext
{
    public InsuranceAnalysisDbContext(DbContextOptions<InsuranceAnalysisDbContext> options)
        : base(options)
    {
    }

    public DbSet<AnalysisRecord> AnalysisRecords => Set<AnalysisRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisRecord>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Sentiment);
            entity.HasIndex(e => e.CustomerPersona);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.InteractionType);
        });
    }
}

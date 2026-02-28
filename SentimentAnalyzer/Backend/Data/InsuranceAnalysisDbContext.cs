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
    public DbSet<ClaimRecord> Claims => Set<ClaimRecord>();
    public DbSet<ClaimEvidenceRecord> ClaimEvidence => Set<ClaimEvidenceRecord>();
    public DbSet<ClaimActionRecord> ClaimActions => Set<ClaimActionRecord>();
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
    public DbSet<DocumentChunkRecord> DocumentChunks => Set<DocumentChunkRecord>();
    public DbSet<FraudCorrelationRecord> FraudCorrelations => Set<FraudCorrelationRecord>();
    public DbSet<CxInteractionRecord> CxInteractions => Set<CxInteractionRecord>();
    public DbSet<CxConversationRecord> CxConversations => Set<CxConversationRecord>();
    public DbSet<DocumentQAPairRecord> DocumentQAPairs => Set<DocumentQAPairRecord>();

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

        modelBuilder.Entity<ClaimRecord>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.FraudScore);
            entity.HasMany(e => e.Evidence).WithOne(e => e.Claim).HasForeignKey(e => e.ClaimId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Actions).WithOne(e => e.Claim).HasForeignKey(e => e.ClaimId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClaimEvidenceRecord>(entity =>
        {
            entity.HasIndex(e => e.ClaimId);
        });

        modelBuilder.Entity<ClaimActionRecord>(entity =>
        {
            entity.HasIndex(e => e.ClaimId);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<FraudCorrelationRecord>(entity =>
        {
            entity.HasIndex(e => e.SourceClaimId);
            entity.HasIndex(e => e.CorrelatedClaimId);
            entity.HasIndex(e => e.CorrelationScore);
            entity.HasIndex(e => e.DetectedAt);
            // No FK constraints — claims can be deleted independently without cascading to correlations
            entity.HasOne(e => e.SourceClaim).WithMany().HasForeignKey(e => e.SourceClaimId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.CorrelatedClaim).WithMany().HasForeignKey(e => e.CorrelatedClaimId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CxInteractionRecord>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.EscalationRecommended);
        });

        modelBuilder.Entity<CxConversationRecord>(entity =>
        {
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.HasIndex(e => e.LastActivityUtc);
        });

        modelBuilder.Entity<DocumentRecord>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Status);
            entity.HasMany(e => e.Chunks).WithOne(e => e.Document).HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentChunkRecord>(entity =>
        {
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.SectionName);
            entity.HasIndex(e => e.IsSafe);
        });

        modelBuilder.Entity<DocumentQAPairRecord>(entity =>
        {
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.ChunkId);
            entity.HasIndex(e => e.Category);
            entity.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Chunk).WithMany().HasForeignKey(e => e.ChunkId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

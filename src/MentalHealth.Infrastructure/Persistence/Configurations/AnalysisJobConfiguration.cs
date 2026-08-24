using MentalHealth.Domain.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class AnalysisJobConfiguration : IEntityTypeConfiguration<AnalysisJob>
{
    public void Configure(EntityTypeBuilder<AnalysisJob> builder)
    {
        builder.ToTable("analysis_jobs");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(job => job.SessionId).HasColumnName("session_id");
        builder.Property(job => job.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(job => job.TranscriptRevision).HasColumnName("transcript_revision");
        builder.Property(job => job.Attempts).HasColumnName("attempts");
        builder.Property(job => job.FailureCode)
            .HasColumnName("failure_code")
            .HasMaxLength(128);
        builder.Property(job => job.CreatedAt).HasColumnName("created_at");
        builder.Property(job => job.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(job => job.SessionId).IsUnique();
        builder.HasIndex(job => new { job.Status, job.UpdatedAt });
    }
}

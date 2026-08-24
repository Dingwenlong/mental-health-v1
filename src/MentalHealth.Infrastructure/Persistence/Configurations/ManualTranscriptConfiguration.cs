using MentalHealth.Domain.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class ManualTranscriptConfiguration : IEntityTypeConfiguration<ManualTranscript>
{
    public void Configure(EntityTypeBuilder<ManualTranscript> builder)
    {
        builder.ToTable("manual_transcripts");
        builder.HasKey(document => new { document.SessionId, document.Revision });
        builder.Property(document => document.SessionId).HasColumnName("session_id");
        builder.Property(document => document.Revision).HasColumnName("revision");
        builder.Property(document => document.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(document => document.Text)
            .HasColumnName("text")
            .HasMaxLength(200_000);
        builder.Property(document => document.Sha256)
            .HasColumnName("sha256")
            .HasMaxLength(64);
        builder.Property(document => document.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(document => new { document.SessionId, document.CreatedAt });
    }
}

using MentalHealth.Domain.Consents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class ConsentRecordConfiguration
    : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("consent_records");
        builder.HasKey(consent => consent.Id);
        builder.Property(consent => consent.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(consent => consent.SubjectId).HasColumnName("subject_id");
        builder.Property(consent => consent.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(consent => consent.TextVersion)
            .HasColumnName("text_version")
            .HasMaxLength(64);
        builder.Property(consent => consent.GrantedByUserId)
            .HasColumnName("granted_by_user_id");
        builder.Property(consent => consent.GrantedAt).HasColumnName("granted_at");
        builder.Property(consent => consent.WithdrawnByUserId)
            .HasColumnName("withdrawn_by_user_id");
        builder.Property(consent => consent.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Ignore(consent => consent.Active);
        builder.HasIndex(consent => new { consent.SubjectId, consent.Kind })
            .IsUnique()
            .HasFilter("withdrawn_at IS NULL");
    }
}

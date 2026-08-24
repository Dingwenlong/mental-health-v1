using MentalHealth.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class DemoDataDeletionConfiguration
    : IEntityTypeConfiguration<DemoDataDeletion>
{
    public void Configure(EntityTypeBuilder<DemoDataDeletion> builder)
    {
        builder.ToTable("demo_data_deletions");
        builder.HasKey(deletion => deletion.SubjectId);
        builder.Property(deletion => deletion.SubjectId)
            .HasColumnName("subject_id")
            .ValueGeneratedNever();
        builder.Property(deletion => deletion.RequestedByUserId)
            .HasColumnName("requested_by_user_id");
        builder.Property(deletion => deletion.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(deletion => deletion.RequestedAt)
            .HasColumnName("requested_at");
        builder.Property(deletion => deletion.LastAttemptAt)
            .HasColumnName("last_attempt_at");
        builder.Property(deletion => deletion.DeletedAt)
            .HasColumnName("deleted_at");
        builder.HasIndex(deletion => new
        {
            deletion.Status,
            deletion.LastAttemptAt
        });
    }
}

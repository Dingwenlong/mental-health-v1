using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class ConsultationSessionConfiguration
    : IEntityTypeConfiguration<ConsultationSession>
{
    public void Configure(EntityTypeBuilder<ConsultationSession> builder)
    {
        builder.ToTable("consultation_sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(session => session.SubjectId)
            .HasColumnName("subject_id");
        builder.Property(session => session.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(session => session.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(session => session.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(session => session.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(session => session.StartedAt).HasColumnName("started_at");
        builder.Property(session => session.CompletedAt).HasColumnName("completed_at");
        builder.Property(session => session.CancelledAt).HasColumnName("cancelled_at");
        builder.Ignore(session => session.DomainEvents);
        builder.HasIndex(session => new { session.SubjectId, session.Status });
    }
}

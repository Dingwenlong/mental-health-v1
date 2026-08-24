using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.FollowUps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class ObservationCaseConfiguration
    : IEntityTypeConfiguration<ObservationCase>
{
    public void Configure(EntityTypeBuilder<ObservationCase> builder)
    {
        builder.ToTable("observation_cases");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.AssessmentId).HasColumnName("assessment_id");
        builder.Property(item => item.SessionId).HasColumnName("session_id");
        builder.Property(item => item.SubjectId).HasColumnName("subject_id");
        builder.Property(item => item.ConsultationKind)
            .HasColumnName("consultation_kind")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(item => item.OriginalLevel)
            .HasColumnName("original_level")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(item => item.CurrentLevel)
            .HasColumnName("current_level")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(item => item.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(item => item.LatestReviewId).HasColumnName("latest_review_id");
        builder.Property(item => item.FollowUpTaskId).HasColumnName("follow_up_task_id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<RiskAssessment>()
            .WithMany()
            .HasForeignKey(item => item.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConsultationSession>()
            .WithMany()
            .HasForeignKey(item => item.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FollowUpTask>()
            .WithMany()
            .HasForeignKey(item => item.FollowUpTaskId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.AssessmentId).IsUnique();
        builder.HasIndex(item => new { item.Status, item.CurrentLevel, item.CreatedAt });
        builder.HasIndex(item => new { item.SubjectId, item.Status });
    }
}

using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class ClinicalReviewConfiguration
    : IEntityTypeConfiguration<ClinicalReview>
{
    public void Configure(EntityTypeBuilder<ClinicalReview> builder)
    {
        builder.ToTable("clinical_reviews");
        builder.HasKey(review => review.Id);
        builder.Property(review => review.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(review => review.ObservationCaseId)
            .HasColumnName("observation_case_id");
        builder.Property(review => review.AssessmentId).HasColumnName("assessment_id");
        builder.Property(review => review.ReviewerId).HasColumnName("reviewer_id");
        builder.Property(review => review.ReviewedLevel)
            .HasColumnName("reviewed_level")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(review => review.Reason)
            .HasColumnName("reason")
            .HasMaxLength(1000);
        builder.Property(review => review.ReviewedAt).HasColumnName("reviewed_at");
        builder.HasOne<ObservationCase>()
            .WithMany()
            .HasForeignKey(review => review.ObservationCaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RiskAssessment>()
            .WithMany()
            .HasForeignKey(review => review.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Practitioner>()
            .WithMany()
            .HasForeignKey(review => review.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(review => new
        {
            review.ObservationCaseId,
            review.ReviewedAt,
            review.Id
        });
    }
}

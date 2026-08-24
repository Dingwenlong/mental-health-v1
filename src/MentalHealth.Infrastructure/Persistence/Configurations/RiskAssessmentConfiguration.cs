using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class RiskAssessmentConfiguration
    : IEntityTypeConfiguration<RiskAssessment>
{
    public void Configure(EntityTypeBuilder<RiskAssessment> builder)
    {
        builder.ToTable("risk_assessments");
        builder.HasKey(assessment => assessment.Id);
        builder.Property(assessment => assessment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(assessment => assessment.SessionId).HasColumnName("session_id");
        builder.Property(assessment => assessment.SubjectId).HasColumnName("subject_id");
        builder.Property(assessment => assessment.TranscriptRevision)
            .HasColumnName("transcript_revision");
        builder.Property(assessment => assessment.RuleSetVersion)
            .HasColumnName("rule_set_version")
            .HasMaxLength(64);
        builder.Property(assessment => assessment.Score)
            .HasColumnName("score")
            .HasPrecision(10, 6);
        builder.Property(assessment => assessment.AvailableWeight)
            .HasColumnName("available_weight")
            .HasPrecision(8, 6);
        builder.Property(assessment => assessment.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(8, 6);
        builder.Property(assessment => assessment.Level)
            .HasColumnName("level")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(assessment => assessment.IsCrisis).HasColumnName("is_crisis");
        builder.Property(assessment => assessment.CrisisRuleId)
            .HasColumnName("crisis_rule_id")
            .HasMaxLength(128);
        builder.Property(assessment => assessment.MissingMask).HasColumnName("missing_mask");
        builder.Property(assessment => assessment.CreatedAt).HasColumnName("created_at");
        builder.Ignore(assessment => assessment.Missing);
        builder.HasOne<ConsultationSession>()
            .WithMany()
            .HasForeignKey(assessment => assessment.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RiskRuleSet>()
            .WithMany()
            .HasForeignKey(assessment => assessment.RuleSetVersion)
            .HasPrincipalKey(rule => rule.Version)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(assessment => new
        {
            assessment.SessionId,
            assessment.CreatedAt
        });
        builder.HasIndex(assessment => new
        {
            assessment.SessionId,
            assessment.RuleSetVersion,
            assessment.TranscriptRevision
        }).IsUnique();

        builder.OwnsMany(assessment => assessment.Evidence, evidence =>
        {
            evidence.ToTable("risk_evidence");
            evidence.WithOwner().HasForeignKey("assessment_id");
            evidence.Property<int>("id");
            evidence.HasKey("assessment_id", "id");
            evidence.Property(item => item.Code)
                .HasColumnName("code")
                .HasMaxLength(128);
            evidence.Property(item => item.Modality)
                .HasColumnName("modality")
                .HasMaxLength(32);
            evidence.Property(item => item.Contribution)
                .HasColumnName("contribution")
                .HasPrecision(10, 6);
            evidence.Property(item => item.SourceRange)
                .HasColumnName("source_range")
                .HasMaxLength(256);
            evidence.Property(item => item.Quality)
                .HasColumnName("quality")
                .HasPrecision(8, 6);
        });
    }
}

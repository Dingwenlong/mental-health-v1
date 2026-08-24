using MentalHealth.Domain.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class RiskRuleSetConfiguration : IEntityTypeConfiguration<RiskRuleSet>
{
    public void Configure(EntityTypeBuilder<RiskRuleSet> builder)
    {
        builder.ToTable("risk_rule_sets");
        builder.HasKey(rule => rule.Id);
        builder.HasAlternateKey(rule => rule.Version);
        builder.Property(rule => rule.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(rule => rule.Version)
            .HasColumnName("version")
            .HasMaxLength(64);
        builder.Property(rule => rule.ScaleWeight)
            .HasColumnName("scale_weight")
            .HasPrecision(8, 6);
        builder.Property(rule => rule.TextWeight)
            .HasColumnName("text_weight")
            .HasPrecision(8, 6);
        builder.Property(rule => rule.AudioWeight)
            .HasColumnName("audio_weight")
            .HasPrecision(8, 6);
        builder.Property(rule => rule.VideoWeight)
            .HasColumnName("video_weight")
            .HasPrecision(8, 6);
        builder.Property(rule => rule.TrendWeight)
            .HasColumnName("trend_weight")
            .HasPrecision(8, 6);
        builder.Property(rule => rule.L1Threshold)
            .HasColumnName("l1_threshold")
            .HasPrecision(8, 4);
        builder.Property(rule => rule.L2Threshold)
            .HasColumnName("l2_threshold")
            .HasPrecision(8, 4);
        builder.Property(rule => rule.L3Threshold)
            .HasColumnName("l3_threshold")
            .HasPrecision(8, 4);
        builder.Property(rule => rule.CrisisRulesEnabled)
            .HasColumnName("crisis_rules_enabled");
        builder.Property(rule => rule.Active).HasColumnName("active");
        builder.Property(rule => rule.CreatedAt).HasColumnName("created_at");
        builder.Property(rule => rule.ActivatedAt).HasColumnName("activated_at");
        builder.Ignore(rule => rule.Weights);
        builder.Ignore(rule => rule.Thresholds);
        builder.HasIndex(rule => rule.Active)
            .IsUnique()
            .HasFilter("\"active\" = TRUE");

        var v1 = RiskRuleSet.V1;
        builder.HasData(new
        {
            v1.Id,
            v1.Version,
            v1.ScaleWeight,
            v1.TextWeight,
            v1.AudioWeight,
            v1.VideoWeight,
            v1.TrendWeight,
            v1.L1Threshold,
            v1.L2Threshold,
            v1.L3Threshold,
            v1.CrisisRulesEnabled,
            v1.Active,
            v1.CreatedAt,
            v1.ActivatedAt
        });
    }
}

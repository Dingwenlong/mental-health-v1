using MentalHealth.Domain.Care;
using MentalHealth.Domain.FollowUps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class CareConfiguration : IEntityTypeConfiguration<DailyCheckIn>, IEntityTypeConfiguration<ExerciseCompletion>,
    IEntityTypeConfiguration<SharingGrant>, IEntityTypeConfiguration<CarePlan>, IEntityTypeConfiguration<CarePlanTask>
{
    public void Configure(EntityTypeBuilder<DailyCheckIn> builder)
    {
        builder.ToTable("daily_check_ins");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.SleepHours).HasPrecision(3, 1);
        builder.Property(item => item.Note).HasMaxLength(500);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasIndex(item => new { item.SubjectId, item.Date }).IsUnique();
    }
    public void Configure(EntityTypeBuilder<ExerciseCompletion> builder)
    {
        builder.ToTable("exercise_completions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.ExerciseId).HasMaxLength(40);
        builder.HasIndex(item => new { item.SubjectId, item.CompletedAt });
    }
    public void Configure(EntityTypeBuilder<SharingGrant> builder)
    {
        builder.ToTable("daily_sharing_grants");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<FollowUpTask>().WithMany().HasForeignKey(item => item.FollowUpId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.FollowUpId, item.AssignmentVersion }).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        builder.HasIndex(item => new { item.SubjectId, item.DoctorId });
    }
    public void Configure(EntityTypeBuilder<CarePlan> builder)
    {
        builder.ToTable("care_plans");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Title).HasMaxLength(120);
        builder.Property(item => item.CreationKey).HasMaxLength(100);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasOne<FollowUpTask>().WithMany().HasForeignKey(item => item.FollowUpId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Tasks).WithOne().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(item => new { item.AuthorId, item.CreationKey }).IsUnique();
        builder.HasIndex(item => item.FollowUpId).IsUnique().HasFilter("\"Status\" IN ('Draft', 'Active')");
        builder.HasIndex(item => new { item.SubjectId, item.CreatedAt });
    }
    public void Configure(EntityTypeBuilder<CarePlanTask> builder)
    {
        builder.ToTable("care_plan_tasks");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasMaxLength(20);
        builder.Property(item => item.ExerciseId).HasMaxLength(40);
        builder.Property(item => item.Status).HasMaxLength(20);
        builder.Property(item => item.Feedback).HasMaxLength(500);
    }
}

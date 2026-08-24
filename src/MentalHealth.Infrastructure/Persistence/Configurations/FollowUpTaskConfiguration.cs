using MentalHealth.Domain.FollowUps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class FollowUpTaskConfiguration : IEntityTypeConfiguration<FollowUpTask>
{
    public void Configure(EntityTypeBuilder<FollowUpTask> builder)
    {
        builder.ToTable("follow_up_tasks");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(task => task.SubjectId).HasColumnName("subject_id");
        builder.Property(task => task.AssessmentId).HasColumnName("assessment_id");
        builder.Property(task => task.AssigneeId).HasColumnName("assignee_id");
        builder.Property(task => task.AvailabilitySlotId)
            .HasColumnName("availability_slot_id");
        builder.Property(task => task.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(task => task.ProposedAt).HasColumnName("proposed_at");
        builder.Property(task => task.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(task => task.DueAt).HasColumnName("due_at");
        builder.Property(task => task.Deadline).HasColumnName("deadline");
        builder.Property(task => task.ConflictCode)
            .HasColumnName("conflict_code")
            .HasMaxLength(128);
        builder.Property(task => task.BecameDueAt).HasColumnName("became_due_at");
        builder.Property(task => task.CompletedAt).HasColumnName("completed_at");
        builder.Property(task => task.OverdueAt).HasColumnName("overdue_at");
        builder.Property(task => task.CancelledAt).HasColumnName("cancelled_at");
        builder.Ignore(task => task.DomainEvents);
        builder.HasOne<MentalHealth.Domain.Consultations.AvailabilitySlot>()
            .WithMany()
            .HasForeignKey(task => task.AvailabilitySlotId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(task => task.AssessmentId).IsUnique();
        builder.HasIndex(task => new { task.AssigneeId, task.Status, task.DueAt });
        builder.HasIndex(task => new { task.SubjectId, task.Status });
    }
}

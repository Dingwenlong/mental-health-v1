using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class AvailabilitySlotConfiguration
    : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable("availability_slots");
        builder.HasKey(slot => slot.Id);
        builder.Property(slot => slot.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(slot => slot.PractitionerId)
            .HasColumnName("practitioner_id");
        builder.Property(slot => slot.StartAt).HasColumnName("start_at");
        builder.Property(slot => slot.EndAt).HasColumnName("end_at");
        builder.Property(slot => slot.Active).HasColumnName("active");
        builder.Property(slot => slot.CreatedAt).HasColumnName("created_at");
        builder.HasOne<Practitioner>()
            .WithMany()
            .HasForeignKey(slot => slot.PractitionerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(slot => new
        {
            slot.PractitionerId,
            slot.Active,
            slot.StartAt,
            slot.EndAt
        });
    }
}

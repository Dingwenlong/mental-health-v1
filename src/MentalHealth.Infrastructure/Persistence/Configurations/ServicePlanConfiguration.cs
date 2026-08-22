using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class ServicePlanConfiguration
    : IEntityTypeConfiguration<ServicePlan>
{
    public void Configure(EntityTypeBuilder<ServicePlan> builder)
    {
        builder.ToTable("service_plans");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(plan => plan.Name)
            .HasColumnName("name")
            .HasMaxLength(100);
        builder.Property(plan => plan.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(plan => plan.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(plan => plan.PaymentMode)
            .HasColumnName("payment_mode")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(plan => plan.PriceInMinorUnits)
            .HasColumnName("price_in_minor_units");
        builder.Property(plan => plan.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3);
        builder.Property(plan => plan.DurationMinutes)
            .HasColumnName("duration_minutes");
        builder.Property(plan => plan.Active).HasColumnName("active");
        builder.Property(plan => plan.CreatedAt).HasColumnName("created_at");
        builder.Property(plan => plan.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(plan => new
        {
            plan.Active,
            plan.Kind,
            plan.Channel,
            plan.PaymentMode
        });
    }
}

using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class DemoOrderConfiguration
    : IEntityTypeConfiguration<DemoOrder>
{
    public void Configure(EntityTypeBuilder<DemoOrder> builder)
    {
        builder.ToTable("demo_orders");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(order => order.SubjectId).HasColumnName("subject_id");
        builder.Property(order => order.PlanId).HasColumnName("plan_id");
        builder.Property(order => order.AmountInMinorUnits)
            .HasColumnName("amount_in_minor_units");
        builder.Property(order => order.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3);
        builder.Property(order => order.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(100);
        builder.Property(order => order.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(order => order.PaymentReference)
            .HasColumnName("payment_reference")
            .HasMaxLength(128);
        builder.Property(order => order.CreatedAt).HasColumnName("created_at");
        builder.Property(order => order.ConfirmedAt).HasColumnName("confirmed_at");
        builder.HasOne<ServicePlan>()
            .WithMany()
            .HasForeignKey(order => order.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(order => new { order.SubjectId, order.IdempotencyKey })
            .IsUnique();
        builder.HasIndex(order => new { order.SubjectId, order.Status, order.CreatedAt });
    }
}

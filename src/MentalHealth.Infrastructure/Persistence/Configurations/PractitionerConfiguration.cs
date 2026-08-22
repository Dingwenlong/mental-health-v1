using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class PractitionerConfiguration
    : IEntityTypeConfiguration<Practitioner>
{
    public void Configure(EntityTypeBuilder<Practitioner> builder)
    {
        builder.ToTable("practitioners");
        builder.HasKey(practitioner => practitioner.Id);
        builder.Property(practitioner => practitioner.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(practitioner => practitioner.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(80);
        builder.Property(practitioner => practitioner.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(practitioner => practitioner.Active)
            .HasColumnName("active");
        builder.Property(practitioner => practitioner.CreatedAt)
            .HasColumnName("created_at");
        builder.Property(practitioner => practitioner.UpdatedAt)
            .HasColumnName("updated_at");
        builder.HasIndex(practitioner => new
        {
            practitioner.Active,
            practitioner.Role,
            practitioner.DisplayName
        });
    }
}

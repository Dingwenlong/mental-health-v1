using MentalHealth.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(audit => audit.ActorUserId)
            .HasColumnName("actor_user_id");
        builder.Property(audit => audit.Action)
            .HasColumnName("action")
            .HasMaxLength(64);
        builder.Property(audit => audit.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(64);
        builder.Property(audit => audit.ResourceId).HasColumnName("resource_id");
        builder.Property(audit => audit.OccurredAt).HasColumnName("occurred_at");
        builder.HasIndex(audit => new
        {
            audit.ResourceType,
            audit.ResourceId,
            audit.OccurredAt
        });
        builder.HasIndex(audit => new { audit.ActorUserId, audit.OccurredAt });
    }
}

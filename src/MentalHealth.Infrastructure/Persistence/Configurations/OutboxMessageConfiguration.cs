using MentalHealth.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(message => message.AggregateId)
            .HasColumnName("aggregate_id");
        builder.Property(message => message.Type)
            .HasColumnName("type")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(message => message.OccurredAt).HasColumnName("occurred_at");
        builder.Property(message => message.CreatedAt).HasColumnName("created_at");
        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(message => message.ProcessedAt).HasColumnName("processed_at");
        builder.Property(message => message.Attempts).HasColumnName("attempts");
        builder.Property(message => message.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(1024);
        builder.Property(message => message.LockedBy)
            .HasColumnName("locked_by")
            .HasMaxLength(128);
        builder.Property(message => message.LockedUntil).HasColumnName("locked_until");
        builder.HasIndex(message => new { message.ProcessedAt, message.OccurredAt });
        builder.HasIndex(message => new { message.ProcessedAt, message.LockedUntil, message.OccurredAt });
        builder.HasIndex(message => new { message.AggregateId, message.Type });
    }
}

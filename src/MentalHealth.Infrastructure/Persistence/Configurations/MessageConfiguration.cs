using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(message => message.SessionId)
            .HasColumnName("session_id");
        builder.Property(message => message.SenderUserId)
            .HasColumnName("sender_user_id");
        builder.Property(message => message.SenderKind)
            .HasColumnName("sender_kind")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(message => message.Text)
            .HasColumnName("text")
            .HasMaxLength(4000)
            .IsRequired();
        builder.Property(message => message.ClientMessageId)
            .HasColumnName("client_message_id")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(message => message.Sequence)
            .HasColumnName("sequence");
        builder.Property(message => message.SentAt)
            .HasColumnName("sent_at");
        builder.HasIndex(message => new
        {
            message.SessionId,
            message.ClientMessageId
        })
            .IsUnique();
        builder.HasIndex(message => new
        {
            message.SessionId,
            message.Sequence
        })
            .IsUnique();
        builder.HasOne<ConsultationSession>()
            .WithMany()
            .HasForeignKey(message => message.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

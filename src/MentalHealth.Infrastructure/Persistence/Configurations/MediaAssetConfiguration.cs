using MentalHealth.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentalHealth.Infrastructure.Persistence.Configurations;

public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(asset => asset.SessionId).HasColumnName("session_id");
        builder.Property(asset => asset.SubjectId).HasColumnName("subject_id");
        builder.Property(asset => asset.CreatedByUserId)
            .HasColumnName("created_by_user_id");
        builder.Property(asset => asset.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100);
        builder.Property(asset => asset.ExpectedChunks)
            .HasColumnName("expected_chunks");
        builder.Property(asset => asset.CreationIdempotencyKey)
            .HasColumnName("creation_idempotency_key")
            .HasMaxLength(100);
        builder.Property(asset => asset.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(24);
        builder.Property(asset => asset.ObjectKey)
            .HasColumnName("object_key")
            .HasMaxLength(500);
        builder.Property(asset => asset.Sha256)
            .HasColumnName("sha256")
            .HasMaxLength(64);
        builder.Property(asset => asset.Length).HasColumnName("length");
        builder.Property(asset => asset.CapturedAt).HasColumnName("captured_at");
        builder.Property(asset => asset.UploadExpiresAt)
            .HasColumnName("upload_expires_at");
        builder.Property(asset => asset.CompletedAt).HasColumnName("completed_at");
        builder.Property(asset => asset.ChunksDeletedAt)
            .HasColumnName("chunks_deleted_at");
        builder.Property(asset => asset.CompletionIdempotencyKey)
            .HasColumnName("completion_idempotency_key")
            .HasMaxLength(100);
        builder.Property(asset => asset.IsDemo).HasColumnName("is_demo");
        builder.HasIndex(asset => new
        {
            asset.SessionId,
            asset.CreationIdempotencyKey
        })
            .IsUnique();
        builder.HasIndex(asset => new { asset.SubjectId, asset.CapturedAt });
        builder.HasIndex(asset => new { asset.Status, asset.CapturedAt });
        builder.HasOne<ConsultationSession>()
            .WithMany()
            .HasForeignKey(asset => asset.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

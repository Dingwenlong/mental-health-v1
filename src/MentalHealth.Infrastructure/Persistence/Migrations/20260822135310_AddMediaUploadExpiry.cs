using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentalHealth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaUploadExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "chunks_deleted_at",
                table: "media_assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "upload_expires_at",
                table: "media_assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE media_assets " +
                "SET upload_expires_at = captured_at + INTERVAL '24 hours' " +
                "WHERE upload_expires_at IS NULL");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "upload_expires_at",
                table: "media_assets",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chunks_deleted_at",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "upload_expires_at",
                table: "media_assets");
        }
    }
}

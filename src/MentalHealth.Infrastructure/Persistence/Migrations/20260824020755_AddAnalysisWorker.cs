using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentalHealth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisWorker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "locked_by",
                table: "outbox_messages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "analysis_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    transcript_revision = table.Column<int>(type: "integer", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    failure_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "manual_transcripts",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    text = table.Column<string>(type: "character varying(200000)", maxLength: 200000, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_transcripts", x => new { x.session_id, x.revision });
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_processed_at_locked_until_occurred_at",
                table: "outbox_messages",
                columns: new[] { "processed_at", "locked_until", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_jobs_session_id",
                table: "analysis_jobs",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_analysis_jobs_status_updated_at",
                table: "analysis_jobs",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_transcripts_session_id_created_at",
                table: "manual_transcripts",
                columns: new[] { "session_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_jobs");

            migrationBuilder.DropTable(
                name: "manual_transcripts");

            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_processed_at_locked_until_occurred_at",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "locked_by",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "outbox_messages");
        }
    }
}

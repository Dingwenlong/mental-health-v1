using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentalHealth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    text_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    withdrawn_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_actor_user_id_occurred_at",
                table: "audit_events",
                columns: new[] { "actor_user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_resource_type_resource_id_occurred_at",
                table: "audit_events",
                columns: new[] { "resource_type", "resource_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_consent_records_subject_id_kind",
                table: "consent_records",
                columns: new[] { "subject_id", "kind" },
                unique: true,
                filter: "withdrawn_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "consent_records");
        }
    }
}

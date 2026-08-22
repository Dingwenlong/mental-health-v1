using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentalHealth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "aggregate_id",
                table: "outbox_messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                "UPDATE outbox_messages SET aggregate_id = id " +
                "WHERE aggregate_id = '00000000-0000-0000-0000-000000000000'");
            migrationBuilder.Sql(
                "ALTER TABLE outbox_messages " +
                "ALTER COLUMN aggregate_id DROP DEFAULT");

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_practitioner_id",
                table: "consultation_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "completion_idempotency_key",
                table: "consultation_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "creation_idempotency_key",
                table: "consultation_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "order_id",
                table: "consultation_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    client_message_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_consultation_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "consultation_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_aggregate_id_type",
                table: "outbox_messages",
                columns: new[] { "aggregate_id", "type" });

            migrationBuilder.CreateIndex(
                name: "IX_consultation_sessions_order_id",
                table: "consultation_sessions",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consultation_sessions_subject_id_creation_idempotency_key",
                table: "consultation_sessions",
                columns: new[] { "subject_id", "creation_idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_session_id_client_message_id",
                table: "messages",
                columns: new[] { "session_id", "client_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_session_id_sequence",
                table: "messages",
                columns: new[] { "session_id", "sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_aggregate_id_type",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_consultation_sessions_order_id",
                table: "consultation_sessions");

            migrationBuilder.DropIndex(
                name: "IX_consultation_sessions_subject_id_creation_idempotency_key",
                table: "consultation_sessions");

            migrationBuilder.DropColumn(
                name: "aggregate_id",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "assigned_practitioner_id",
                table: "consultation_sessions");

            migrationBuilder.DropColumn(
                name: "completion_idempotency_key",
                table: "consultation_sessions");

            migrationBuilder.DropColumn(
                name: "creation_idempotency_key",
                table: "consultation_sessions");

            migrationBuilder.DropColumn(
                name: "order_id",
                table: "consultation_sessions");
        }
    }
}

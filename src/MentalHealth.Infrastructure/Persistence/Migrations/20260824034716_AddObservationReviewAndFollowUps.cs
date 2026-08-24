using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentalHealth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddObservationReviewAndFollowUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "availability_slot_id",
                table: "follow_up_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "follow_up_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "conflict_code",
                table: "follow_up_tasks",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deadline",
                table: "follow_up_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason",
                table: "audit_events",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "observation_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consultation_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    original_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    current_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    latest_review_id = table.Column<Guid>(type: "uuid", nullable: true),
                    follow_up_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation_cases", x => x.id);
                    table.ForeignKey(
                        name: "FK_observation_cases_consultation_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "consultation_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_observation_cases_follow_up_tasks_follow_up_task_id",
                        column: x => x.follow_up_task_id,
                        principalTable: "follow_up_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_observation_cases_risk_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "risk_assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clinical_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    observation_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinical_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_clinical_reviews_observation_cases_observation_case_id",
                        column: x => x.observation_case_id,
                        principalTable: "observation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clinical_reviews_practitioners_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "practitioners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clinical_reviews_risk_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "risk_assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_follow_up_tasks_assessment_id",
                table: "follow_up_tasks",
                column: "assessment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_follow_up_tasks_availability_slot_id",
                table: "follow_up_tasks",
                column: "availability_slot_id");

            migrationBuilder.CreateIndex(
                name: "IX_clinical_reviews_assessment_id",
                table: "clinical_reviews",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "IX_clinical_reviews_observation_case_id_reviewed_at_id",
                table: "clinical_reviews",
                columns: new[] { "observation_case_id", "reviewed_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_clinical_reviews_reviewer_id",
                table: "clinical_reviews",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "IX_observation_cases_assessment_id",
                table: "observation_cases",
                column: "assessment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_observation_cases_follow_up_task_id",
                table: "observation_cases",
                column: "follow_up_task_id");

            migrationBuilder.CreateIndex(
                name: "IX_observation_cases_session_id",
                table: "observation_cases",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_observation_cases_status_current_level_created_at",
                table: "observation_cases",
                columns: new[] { "status", "current_level", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_observation_cases_subject_id_status",
                table: "observation_cases",
                columns: new[] { "subject_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "FK_follow_up_tasks_availability_slots_availability_slot_id",
                table: "follow_up_tasks",
                column: "availability_slot_id",
                principalTable: "availability_slots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_follow_up_tasks_availability_slots_availability_slot_id",
                table: "follow_up_tasks");

            migrationBuilder.DropTable(
                name: "clinical_reviews");

            migrationBuilder.DropTable(
                name: "observation_cases");

            migrationBuilder.DropIndex(
                name: "IX_follow_up_tasks_assessment_id",
                table: "follow_up_tasks");

            migrationBuilder.DropIndex(
                name: "IX_follow_up_tasks_availability_slot_id",
                table: "follow_up_tasks");

            migrationBuilder.DropColumn(
                name: "availability_slot_id",
                table: "follow_up_tasks");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "follow_up_tasks");

            migrationBuilder.DropColumn(
                name: "conflict_code",
                table: "follow_up_tasks");

            migrationBuilder.DropColumn(
                name: "deadline",
                table: "follow_up_tasks");

            migrationBuilder.DropColumn(
                name: "reason",
                table: "audit_events");
        }
    }
}

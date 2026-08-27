using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentalHealth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareContinuity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignmentVersion",
                table: "follow_up_tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "care_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowUpId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreationKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_care_plans_follow_up_tasks_FollowUpId",
                        column: x => x.FollowUpId,
                        principalTable: "follow_up_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_check_ins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Mood = table.Column<int>(type: "integer", nullable: false),
                    SleepHours = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_check_ins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "daily_sharing_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowUpId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentVersion = table.Column<int>(type: "integer", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsentVersion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_sharing_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_daily_sharing_grants_follow_up_tasks_FollowUpId",
                        column: x => x.FollowUpId,
                        principalTable: "follow_up_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exercise_completions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_completions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "care_plan_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExerciseId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Feedback = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_plan_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_care_plan_tasks_care_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "care_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_care_plan_tasks_PlanId",
                table: "care_plan_tasks",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_care_plans_AuthorId_CreationKey",
                table: "care_plans",
                columns: new[] { "AuthorId", "CreationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_care_plans_FollowUpId",
                table: "care_plans",
                column: "FollowUpId",
                unique: true,
                filter: "\"Status\" IN ('Draft', 'Active')");

            migrationBuilder.CreateIndex(
                name: "IX_care_plans_SubjectId_CreatedAt",
                table: "care_plans",
                columns: new[] { "SubjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_check_ins_SubjectId_Date",
                table: "daily_check_ins",
                columns: new[] { "SubjectId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_sharing_grants_FollowUpId_AssignmentVersion",
                table: "daily_sharing_grants",
                columns: new[] { "FollowUpId", "AssignmentVersion" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_daily_sharing_grants_SubjectId_DoctorId",
                table: "daily_sharing_grants",
                columns: new[] { "SubjectId", "DoctorId" });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_SubjectId_CompletedAt",
                table: "exercise_completions",
                columns: new[] { "SubjectId", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "care_plan_tasks");

            migrationBuilder.DropTable(
                name: "daily_check_ins");

            migrationBuilder.DropTable(
                name: "daily_sharing_grants");

            migrationBuilder.DropTable(
                name: "exercise_completions");

            migrationBuilder.DropTable(
                name: "care_plans");

            migrationBuilder.DropColumn(
                name: "AssignmentVersion",
                table: "follow_up_tasks");
        }
    }
}

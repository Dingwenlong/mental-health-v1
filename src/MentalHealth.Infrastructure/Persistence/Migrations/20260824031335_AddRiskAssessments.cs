using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MentalHealth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "assessment_id",
                table: "analysis_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                table: "analysis_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "risk_rule_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scale_weight = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    text_weight = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    audio_weight = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    video_weight = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    trend_weight = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    l1_threshold = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    l2_threshold = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    l3_threshold = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    crisis_rules_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_rule_sets", x => x.id);
                    table.UniqueConstraint("AK_risk_rule_sets_version", x => x.version);
                });

            migrationBuilder.CreateTable(
                name: "risk_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transcript_revision = table.Column<int>(type: "integer", nullable: true),
                    rule_set_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    score = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    available_weight = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_crisis = table.Column<bool>(type: "boolean", nullable: false),
                    crisis_rule_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    missing_mask = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_assessments", x => x.id);
                    table.ForeignKey(
                        name: "FK_risk_assessments_consultation_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "consultation_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_risk_assessments_risk_rule_sets_rule_set_version",
                        column: x => x.rule_set_version,
                        principalTable: "risk_rule_sets",
                        principalColumn: "version",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_evidence",
                columns: table => new
                {
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    modality = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    contribution = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    source_range = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    quality = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_evidence", x => new { x.assessment_id, x.id });
                    table.ForeignKey(
                        name: "FK_risk_evidence_risk_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "risk_assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "risk_rule_sets",
                columns: new[] { "id", "activated_at", "active", "audio_weight", "created_at", "crisis_rules_enabled", "l1_threshold", "l2_threshold", "l3_threshold", "scale_weight", "text_weight", "trend_weight", "version", "video_weight" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, 0.15m, new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, 25m, 50m, 75m, 0.45m, 0.25m, 0.10m, "risk-v1", 0.05m });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_jobs_assessment_id",
                table: "analysis_jobs",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_rule_set_version",
                table: "risk_assessments",
                column: "rule_set_version");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_session_id_created_at",
                table: "risk_assessments",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_session_id_rule_set_version_transcript_rev~",
                table: "risk_assessments",
                columns: new[] { "session_id", "rule_set_version", "transcript_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_rule_sets_active",
                table: "risk_rule_sets",
                column: "active",
                unique: true,
                filter: "\"active\" = TRUE");

            migrationBuilder.AddForeignKey(
                name: "FK_analysis_jobs_risk_assessments_assessment_id",
                table: "analysis_jobs",
                column: "assessment_id",
                principalTable: "risk_assessments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_analysis_jobs_risk_assessments_assessment_id",
                table: "analysis_jobs");

            migrationBuilder.DropTable(
                name: "risk_evidence");

            migrationBuilder.DropTable(
                name: "risk_assessments");

            migrationBuilder.DropTable(
                name: "risk_rule_sets");

            migrationBuilder.DropIndex(
                name: "IX_analysis_jobs_assessment_id",
                table: "analysis_jobs");

            migrationBuilder.DropColumn(
                name: "assessment_id",
                table: "analysis_jobs");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "analysis_jobs");
        }
    }
}

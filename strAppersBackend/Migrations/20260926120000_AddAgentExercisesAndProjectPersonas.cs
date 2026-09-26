using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <summary>
    /// InstituteProjects.MainAIPersonaId (project-level persona override), side personas
    /// (InstituteProjectPersonas + PersonaChatHistory) and agent-exercise grading
    /// (AgentExercises, AgentRequirements, AgentWorlds, AgentScenarioSets, AgentScenarios,
    /// ProjectAgentScenarioSets, AgentGradingReports).
    ///
    /// Hand-written, like the other recent migrations here: the model snapshot is well behind the live
    /// schema, so `dotnet ef migrations add` would re-add existing columns. The equivalent guarded SQL
    /// ships as Migrations/agent_exercise_add.sql for manual application. Two FK names are 63 characters,
    /// matching what Postgres truncated them to when that SQL ran.
    /// </summary>
    public partial class AddAgentExercisesAndProjectPersonas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "MainAIPersonaId", table: "InstituteProjects", type: "integer", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_InstituteProjects_MainAIPersonaId", table: "InstituteProjects", column: "MainAIPersonaId");
            migrationBuilder.AddForeignKey(
                name: "FK_InstituteProjects_AIPersonas_MainAIPersonaId", table: "InstituteProjects", column: "MainAIPersonaId",
                principalTable: "AIPersonas", principalColumn: "Id", onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateTable(
                name: "InstituteProjectPersonas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteProjectId = table.Column<int>(type: "integer", nullable: false),
                    PersonaId = table.Column<int>(type: "integer", nullable: false),
                    ContextText = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteProjectPersonas", x => x.Id);
                    table.ForeignKey("FK_InstituteProjectPersonas_InstituteProjects_InstituteProjectI", x => x.InstituteProjectId,
                        "InstituteProjects", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_InstituteProjectPersonas_AIPersonas_PersonaId", x => x.PersonaId,
                        "AIPersonas", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex("IX_InstituteProjectPersonas_InstituteProjectId_PersonaId", "InstituteProjectPersonas",
                new[] { "InstituteProjectId", "PersonaId" }, unique: true);
            migrationBuilder.CreateIndex("IX_InstituteProjectPersonas_PersonaId", "InstituteProjectPersonas", "PersonaId");

            migrationBuilder.CreateTable(
                name: "PersonaChatHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SprintId = table.Column<int>(type: "integer", nullable: false),
                    PersonaId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    AIModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonaChatHistory", x => x.Id);
                    table.ForeignKey("FK_PersonaChatHistory_AIPersonas_PersonaId", x => x.PersonaId,
                        "AIPersonas", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex("IX_PersonaChatHistory_StudentId_PersonaId_SprintId", "PersonaChatHistory",
                new[] { "StudentId", "PersonaId", "SprintId" });

            migrationBuilder.CreateTable(
                name: "AgentExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EndpointPath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LimitsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table => table.PrimaryKey("PK_AgentExercises", x => x.Id));
            migrationBuilder.CreateIndex("IX_AgentExercises_Key", "AgentExercises", "Key", unique: true);

            migrationBuilder.CreateTable(
                name: "AgentRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExerciseId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    PersonaId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRequirements", x => x.Id);
                    table.ForeignKey("FK_AgentRequirements_AgentExercises_ExerciseId", x => x.ExerciseId,
                        "AgentExercises", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_AgentRequirements_AIPersonas_PersonaId", x => x.PersonaId,
                        "AIPersonas", "Id", onDelete: ReferentialAction.SetNull);
                });
            migrationBuilder.CreateIndex("IX_AgentRequirements_ExerciseId_Code", "AgentRequirements", new[] { "ExerciseId", "Code" }, unique: true);
            migrationBuilder.CreateIndex("IX_AgentRequirements_PersonaId", "AgentRequirements", "PersonaId");

            migrationBuilder.CreateTable(
                name: "AgentWorlds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table => table.PrimaryKey("PK_AgentWorlds", x => x.Id));
            migrationBuilder.CreateIndex("IX_AgentWorlds_Key", "AgentWorlds", "Key", unique: true);

            migrationBuilder.CreateTable(
                name: "AgentScenarioSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExerciseId = table.Column<int>(type: "integer", nullable: false),
                    WorldId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentScenarioSets", x => x.Id);
                    table.CheckConstraint("CK_AgentScenarioSets_Status", "\"Status\" IN ('draft', 'published', 'archived')");
                    table.ForeignKey("FK_AgentScenarioSets_AgentExercises_ExerciseId", x => x.ExerciseId,
                        "AgentExercises", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_AgentScenarioSets_AgentWorlds_WorldId", x => x.WorldId,
                        "AgentWorlds", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex("IX_AgentScenarioSets_ExerciseId_Name_Version", "AgentScenarioSets",
                new[] { "ExerciseId", "Name", "Version" }, unique: true);
            migrationBuilder.CreateIndex("IX_AgentScenarioSets_WorldId", "AgentScenarioSets", "WorldId");

            migrationBuilder.CreateTable(
                name: "AgentScenarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScenarioSetId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Request = table.Column<string>(type: "text", nullable: false),
                    PositionLat = table.Column<double>(type: "double precision", nullable: true),
                    PositionLng = table.Column<double>(type: "double precision", nullable: true),
                    ExpectationsJson = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentScenarios", x => x.Id);
                    table.ForeignKey("FK_AgentScenarios_AgentScenarioSets_ScenarioSetId", x => x.ScenarioSetId,
                        "AgentScenarioSets", "Id", onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex("IX_AgentScenarios_ScenarioSetId_Key", "AgentScenarios", new[] { "ScenarioSetId", "Key" }, unique: true);

            migrationBuilder.CreateTable(
                name: "ProjectAgentScenarioSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteProjectId = table.Column<int>(type: "integer", nullable: false),
                    ScenarioSetId = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgentScenarioSets", x => x.Id);
                    table.CheckConstraint("CK_ProjectAgentScenarioSets_Purpose", "\"Purpose\" IN ('practice', 'graded')");
                    table.ForeignKey("FK_ProjectAgentScenarioSets_InstituteProjects_InstituteProjectI", x => x.InstituteProjectId,
                        "InstituteProjects", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_ProjectAgentScenarioSets_AgentScenarioSets_ScenarioSetId", x => x.ScenarioSetId,
                        "AgentScenarioSets", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex("IX_ProjectAgentScenarioSets_InstituteProjectId_Purpose", "ProjectAgentScenarioSets",
                new[] { "InstituteProjectId", "Purpose" }, unique: true);
            migrationBuilder.CreateIndex("IX_ProjectAgentScenarioSets_ScenarioSetId", "ProjectAgentScenarioSets", "ScenarioSetId");

            migrationBuilder.CreateTable(
                name: "AgentGradingReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GradingId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ScenarioSetId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    CriticalFailure = table.Column<bool>(type: "boolean", nullable: true),
                    ReportJson = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentGradingReports", x => x.Id);
                    table.ForeignKey("FK_AgentGradingReports_AgentScenarioSets_ScenarioSetId", x => x.ScenarioSetId,
                        "AgentScenarioSets", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex("IX_AgentGradingReports_GradingId", "AgentGradingReports", "GradingId", unique: true);
            migrationBuilder.CreateIndex("IX_AgentGradingReports_BoardId", "AgentGradingReports", "BoardId");
            migrationBuilder.CreateIndex("IX_AgentGradingReports_ScenarioSetId", "AgentGradingReports", "ScenarioSetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AgentGradingReports");
            migrationBuilder.DropTable(name: "ProjectAgentScenarioSets");
            migrationBuilder.DropTable(name: "AgentScenarios");
            migrationBuilder.DropTable(name: "AgentScenarioSets");
            migrationBuilder.DropTable(name: "AgentWorlds");
            migrationBuilder.DropTable(name: "AgentRequirements");
            migrationBuilder.DropTable(name: "AgentExercises");
            migrationBuilder.DropTable(name: "PersonaChatHistory");
            migrationBuilder.DropTable(name: "InstituteProjectPersonas");
            migrationBuilder.DropForeignKey(name: "FK_InstituteProjects_AIPersonas_MainAIPersonaId", table: "InstituteProjects");
            migrationBuilder.DropIndex(name: "IX_InstituteProjects_MainAIPersonaId", table: "InstituteProjects");
            migrationBuilder.DropColumn(name: "MainAIPersonaId", table: "InstituteProjects");
        }
    }
}

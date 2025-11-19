using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordHashToStudentsAndOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "GithubUser",
                table: "Students",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Students",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Photo",
                table: "Students",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgrammingLanguageId",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectPriority1",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectPriority2",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectPriority3",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectPriority4",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartPendingAt",
                table: "Students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Roles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DataSchema",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Kickoff",
                table: "Projects",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SystemDesignFormatted",
                table: "Projects",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "completed_chunks",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "deployment_manifest",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ide_generation_status",
                table: "Projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "mock_records_count",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_chunks",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GithubUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupChat",
                table: "ProjectBoards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextMeetingTime",
                table: "ProjectBoards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextMeetingUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Logo",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Organizations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TermsAccepted",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TermsAcceptedAt",
                table: "Organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsUse",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Figma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FigmaAccessToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FigmaRefreshToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FigmaTokenExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FigmaUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FigmaFileUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FigmaFileKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FigmaLastSync = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Figma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Figma_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgrammingLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReleaseYear = table.Column<int>(type: "integer", nullable: true),
                    Creator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgrammingLanguages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectsIDE",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<int>(type: "integer", nullable: false),
                    chunk_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    chunk_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    chunk_description = table.Column<string>(type: "text", nullable: true),
                    generation_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    files_json = table.Column<string>(type: "jsonb", nullable: true),
                    files_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    dependencies = table.Column<string[]>(type: "text[]", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    tokens_used = table.Column<int>(type: "integer", nullable: true),
                    generation_time_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectsIDE", x => x.id);
                    table.ForeignKey(
                        name: "FK_ProjectsIDE_Projects_project_id",
                        column: x => x.project_id,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    ModuleType = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModules_ModuleTypes_ModuleType",
                        column: x => x.ModuleType,
                        principalTable: "ModuleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectModules_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8172));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8184));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8187));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8190));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8193));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8195));

            migrationBuilder.InsertData(
                table: "ModuleTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Frontend" },
                    { 2, "Backend" },
                    { 3, "Database" },
                    { 4, "Authentication" },
                    { 5, "API" },
                    { 6, "Mobile" },
                    { 7, "DevOps" },
                    { 8, "Testing" }
                });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Logo", "PasswordHash", "TermsAcceptedAt", "TermsUse" },
                values: new object[] { new DateTime(2025, 9, 20, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(2596), null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "Logo", "PasswordHash", "TermsAcceptedAt", "TermsUse" },
                values: new object[] { new DateTime(2025, 9, 25, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(2603), null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "Logo", "PasswordHash", "TermsAcceptedAt", "TermsUse" },
                values: new object[] { new DateTime(2025, 9, 30, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(2607), null, null, null, null });

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(3952));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(3957));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(3961));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(3964));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(4029));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(4033));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "completed_chunks", "CreatedAt", "DataSchema", "deployment_manifest", "ide_generation_status", "Kickoff", "mock_records_count", "SystemDesignFormatted", "total_chunks" },
                values: new object[] { 0, new DateTime(2025, 10, 20, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3288), null, null, "not_started", false, 10, null, 0 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "completed_chunks", "CreatedAt", "DataSchema", "deployment_manifest", "ide_generation_status", "Kickoff", "mock_records_count", "SystemDesignFormatted", "total_chunks" },
                values: new object[] { 0, new DateTime(2025, 10, 30, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3296), null, null, "not_started", false, 10, null, 0 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "completed_chunks", "CreatedAt", "DataSchema", "deployment_manifest", "ide_generation_status", "Kickoff", "mock_records_count", "SystemDesignFormatted", "total_chunks" },
                values: new object[] { 0, new DateTime(2025, 8, 21, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3299), null, null, "not_started", false, 10, null, 0 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "completed_chunks", "CreatedAt", "DataSchema", "deployment_manifest", "ide_generation_status", "Kickoff", "mock_records_count", "SystemDesignFormatted", "total_chunks" },
                values: new object[] { 0, new DateTime(2025, 11, 4, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3303), null, null, "not_started", false, 10, null, 0 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "completed_chunks", "CreatedAt", "DataSchema", "deployment_manifest", "ide_generation_status", "Kickoff", "mock_records_count", "SystemDesignFormatted", "total_chunks" },
                values: new object[] { 0, new DateTime(2025, 11, 9, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3306), null, null, "not_started", false, 10, null, 0 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "completed_chunks", "CreatedAt", "DataSchema", "deployment_manifest", "ide_generation_status", "Kickoff", "mock_records_count", "SystemDesignFormatted", "total_chunks" },
                values: new object[] { 0, new DateTime(2025, 11, 14, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3309), null, null, "not_started", false, 10, null, 0 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "completed_chunks", "CreatedAt", "DataSchema", "deployment_manifest", "ide_generation_status", "Kickoff", "mock_records_count", "SystemDesignFormatted", "total_chunks" },
                values: new object[] { 0, new DateTime(2025, 11, 16, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3312), null, null, "not_started", false, 10, null, 0 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "completed_chunks", "CreatedAt", "DataSchema", "deployment_manifest", "ide_generation_status", "Kickoff", "mock_records_count", "SystemDesignFormatted", "total_chunks" },
                values: new object[] { 0, new DateTime(2025, 11, 18, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3315), null, null, "not_started", false, 10, null, 0 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "completed_chunks", "CreatedAt", "DataSchema", "deployment_manifest", "ide_generation_status", "Kickoff", "mock_records_count", "SystemDesignFormatted", "total_chunks" },
                values: new object[] { 0, new DateTime(2025, 11, 17, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3410), null, null, "not_started", false, 10, null, 0 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Type" },
                values: new object[] { new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5190), 4 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "Type" },
                values: new object[] { new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5196), 1 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "Type" },
                values: new object[] { new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5199), 1 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "Type" },
                values: new object[] { new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5201), 3 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "Type" },
                values: new object[] { new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5204), 2 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "Type" },
                values: new object[] { new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5207), 4 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "Type" },
                values: new object[] { new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5209), 2 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "Type" },
                values: new object[] { new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5212), 2 });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 20, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9468));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 25, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9475));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 30, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9478));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 4, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9480));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 9, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9482));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 15, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9484));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 1, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9486));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 7, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9488));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "GithubUser", "PasswordHash", "Photo", "ProgrammingLanguageId", "ProjectPriority1", "ProjectPriority2", "ProjectPriority3", "ProjectPriority4", "StartPendingAt", "Status" },
                values: new object[] { new DateTime(2025, 10, 5, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5482), "", null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "GithubUser", "PasswordHash", "Photo", "ProgrammingLanguageId", "ProjectPriority1", "ProjectPriority2", "ProjectPriority3", "ProjectPriority4", "StartPendingAt", "Status" },
                values: new object[] { new DateTime(2025, 10, 10, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5491), "", null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "GithubUser", "PasswordHash", "Photo", "ProgrammingLanguageId", "ProjectPriority1", "ProjectPriority2", "ProjectPriority3", "ProjectPriority4", "StartPendingAt", "Status" },
                values: new object[] { new DateTime(2025, 10, 15, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5495), "", null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "GithubUser", "PasswordHash", "Photo", "ProgrammingLanguageId", "ProjectPriority1", "ProjectPriority2", "ProjectPriority3", "ProjectPriority4", "StartPendingAt", "Status" },
                values: new object[] { new DateTime(2025, 10, 20, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5497), "", null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "GithubUser", "PasswordHash", "Photo", "ProgrammingLanguageId", "ProjectPriority1", "ProjectPriority2", "ProjectPriority3", "ProjectPriority4", "StartPendingAt", "Status" },
                values: new object[] { new DateTime(2025, 10, 25, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5538), "", null, null, null, null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9763));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9771));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9774));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9776));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9779));

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProgrammingLanguageId",
                table: "Students",
                column: "ProgrammingLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectPriority1",
                table: "Students",
                column: "ProjectPriority1");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectPriority2",
                table: "Students",
                column: "ProjectPriority2");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectPriority3",
                table: "Students",
                column: "ProjectPriority3");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectPriority4",
                table: "Students",
                column: "ProjectPriority4");

            migrationBuilder.CreateIndex(
                name: "IX_Figma_BoardId",
                table: "Figma",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_Figma_FigmaFileKey",
                table: "Figma",
                column: "FigmaFileKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammingLanguages_IsActive",
                table: "ProgrammingLanguages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammingLanguages_Name",
                table: "ProgrammingLanguages",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_ModuleType",
                table: "ProjectModules",
                column: "ModuleType");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_ProjectId",
                table: "ProjectModules",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_Sequence",
                table: "ProjectModules",
                column: "Sequence");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectsIDE_generation_order",
                table: "ProjectsIDE",
                column: "generation_order");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectsIDE_project_id",
                table: "ProjectsIDE",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectsIDE_project_id_chunk_id",
                table: "ProjectsIDE",
                columns: new[] { "project_id", "chunk_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectsIDE_status",
                table: "ProjectsIDE",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_ProgrammingLanguages_ProgrammingLanguageId",
                table: "Students",
                column: "ProgrammingLanguageId",
                principalTable: "ProgrammingLanguages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Projects_ProjectPriority1",
                table: "Students",
                column: "ProjectPriority1",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Projects_ProjectPriority2",
                table: "Students",
                column: "ProjectPriority2",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Projects_ProjectPriority3",
                table: "Students",
                column: "ProjectPriority3",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Projects_ProjectPriority4",
                table: "Students",
                column: "ProjectPriority4",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_ProgrammingLanguages_ProgrammingLanguageId",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Projects_ProjectPriority1",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Projects_ProjectPriority2",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Projects_ProjectPriority3",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Projects_ProjectPriority4",
                table: "Students");

            migrationBuilder.DropTable(
                name: "Figma");

            migrationBuilder.DropTable(
                name: "ProgrammingLanguages");

            migrationBuilder.DropTable(
                name: "ProjectModules");

            migrationBuilder.DropTable(
                name: "ProjectsIDE");

            migrationBuilder.DropTable(
                name: "ModuleTypes");

            migrationBuilder.DropIndex(
                name: "IX_Students_ProgrammingLanguageId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_ProjectPriority1",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_ProjectPriority2",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_ProjectPriority3",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_ProjectPriority4",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Photo",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ProgrammingLanguageId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ProjectPriority1",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ProjectPriority2",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ProjectPriority3",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ProjectPriority4",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StartPendingAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "DataSchema",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Kickoff",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SystemDesignFormatted",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "completed_chunks",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "deployment_manifest",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ide_generation_status",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "mock_records_count",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "total_chunks",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "GithubUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "GroupChat",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "NextMeetingTime",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "NextMeetingUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "Logo",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TermsAccepted",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "TermsUse",
                table: "Organizations");

            migrationBuilder.AlterColumn<string>(
                name: "GithubUser",
                table: "Students",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(10));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(24));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(26));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(29));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(31));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(33));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(3942));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 12, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(3948));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 17, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(3951));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(5477));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(5482));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(5485));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(5487));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(5490));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(5492));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 6, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(5187));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 16, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(5198));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 8, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(5201));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 21, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(5204));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(5207));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 1, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(5213));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 3, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(5215));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 5, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(5218));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 4, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(5220));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(6717));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(6723));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(6726));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(6728));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(6731));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(6733));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(6735));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 27, 20, 6, 52, 781, DateTimeKind.Utc).AddTicks(6737));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 6, 20, 6, 52, 782, DateTimeKind.Utc).AddTicks(1722));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 11, 20, 6, 52, 782, DateTimeKind.Utc).AddTicks(1729));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 16, 20, 6, 52, 782, DateTimeKind.Utc).AddTicks(1732));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 21, 20, 6, 52, 782, DateTimeKind.Utc).AddTicks(1734));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 26, 20, 6, 52, 782, DateTimeKind.Utc).AddTicks(1736));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 1, 20, 6, 52, 782, DateTimeKind.Utc).AddTicks(1738));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 18, 20, 6, 52, 782, DateTimeKind.Utc).AddTicks(1740));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 24, 20, 6, 52, 782, DateTimeKind.Utc).AddTicks(1742));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "GithubUser" },
                values: new object[] { new DateTime(2025, 8, 22, 20, 6, 52, 780, DateTimeKind.Utc).AddTicks(9829), null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "GithubUser" },
                values: new object[] { new DateTime(2025, 8, 27, 20, 6, 52, 780, DateTimeKind.Utc).AddTicks(9842), null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "GithubUser" },
                values: new object[] { new DateTime(2025, 9, 1, 20, 6, 52, 780, DateTimeKind.Utc).AddTicks(9846), null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "GithubUser" },
                values: new object[] { new DateTime(2025, 9, 6, 20, 6, 52, 780, DateTimeKind.Utc).AddTicks(9852), null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "GithubUser" },
                values: new object[] { new DateTime(2025, 9, 11, 20, 6, 52, 780, DateTimeKind.Utc).AddTicks(9857), null });

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(1466));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(1471));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(1473));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(1476));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 7, 20, 6, 52, 773, DateTimeKind.Utc).AddTicks(1478));
        }
    }
}

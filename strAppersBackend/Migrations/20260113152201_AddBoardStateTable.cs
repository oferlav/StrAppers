using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardStateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NeonProjectId",
                table: "ProjectBoards",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BoardStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Webhook = table.Column<bool>(type: "boolean", nullable: true),
                    ServiceName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    File = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Line = table.Column<int>(type: "integer", nullable: true),
                    StackTrace = table.Column<string>(type: "text", nullable: true),
                    RequestUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastBuildStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastBuildOutput = table.Column<string>(type: "text", nullable: true),
                    LatestErrorSummary = table.Column<string>(type: "text", nullable: true),
                    SprintNumber = table.Column<int>(type: "integer", nullable: true),
                    BranchName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BranchUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LatestCommitId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LatestCommitDescription = table.Column<string>(type: "text", nullable: true),
                    LatestCommitDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMergeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LatestEvent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PRStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BranchStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardStates_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 13, 15, 22, 0, 68, DateTimeKind.Utc).AddTicks(4023));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 13, 15, 22, 0, 68, DateTimeKind.Utc).AddTicks(4029));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 46, DateTimeKind.Utc).AddTicks(8372));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 46, DateTimeKind.Utc).AddTicks(8389));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 46, DateTimeKind.Utc).AddTicks(8392));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 46, DateTimeKind.Utc).AddTicks(8395));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 46, DateTimeKind.Utc).AddTicks(8397));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 46, DateTimeKind.Utc).AddTicks(8399));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(3023));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(3030));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(3034));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(4540));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(4549));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(4552));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(4554));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(4557));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(4559));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 14, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(6873));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 24, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(6883));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 15, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(6886));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 29, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(6889));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(6948));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 8, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(6969));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 10, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(6972));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 12, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(6976));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(6980));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 14, 15, 22, 0, 59, DateTimeKind.Utc).AddTicks(4357));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 19, 15, 22, 0, 59, DateTimeKind.Utc).AddTicks(4369));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 24, 15, 22, 0, 59, DateTimeKind.Utc).AddTicks(4371));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 29, 15, 22, 0, 59, DateTimeKind.Utc).AddTicks(4374));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 3, 15, 22, 0, 59, DateTimeKind.Utc).AddTicks(4376));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 9, 15, 22, 0, 59, DateTimeKind.Utc).AddTicks(4379));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 26, 15, 22, 0, 59, DateTimeKind.Utc).AddTicks(4381));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 1, 15, 22, 0, 59, DateTimeKind.Utc).AddTicks(4384));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 29, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(357));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(371));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 9, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(375));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 14, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(378));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 19, 15, 22, 0, 58, DateTimeKind.Utc).AddTicks(381));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 13, 15, 22, 0, 64, DateTimeKind.Utc).AddTicks(1679));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 13, 15, 22, 0, 64, DateTimeKind.Utc).AddTicks(1683));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 13, 15, 22, 0, 64, DateTimeKind.Utc).AddTicks(1685));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 13, 15, 22, 0, 64, DateTimeKind.Utc).AddTicks(1688));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(66));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(73));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(76));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(81));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 15, 22, 0, 47, DateTimeKind.Utc).AddTicks(83));

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BoardId",
                table: "BoardStates",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BoardId_Source",
                table: "BoardStates",
                columns: new[] { "BoardId", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BranchName",
                table: "BoardStates",
                column: "BranchName");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BranchStatus",
                table: "BoardStates",
                column: "BranchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_CreatedAt",
                table: "BoardStates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_LastBuildStatus",
                table: "BoardStates",
                column: "LastBuildStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_LatestCommitId",
                table: "BoardStates",
                column: "LatestCommitId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_PRStatus",
                table: "BoardStates",
                column: "PRStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_Source",
                table: "BoardStates",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_UpdatedAt",
                table: "BoardStates",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardStates");

            migrationBuilder.DropColumn(
                name: "NeonProjectId",
                table: "ProjectBoards");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 8, 7, 29, 23, 270, DateTimeKind.Utc).AddTicks(5352));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 8, 7, 29, 23, 270, DateTimeKind.Utc).AddTicks(5357));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(3778));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(3793));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(3796));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(3798));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(3800));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(3802));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(8968));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(8976));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(8979));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 29, 7, 29, 23, 245, DateTimeKind.Utc).AddTicks(481));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 29, 7, 29, 23, 245, DateTimeKind.Utc).AddTicks(488));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 29, 7, 29, 23, 245, DateTimeKind.Utc).AddTicks(491));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 29, 7, 29, 23, 245, DateTimeKind.Utc).AddTicks(494));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 29, 7, 29, 23, 245, DateTimeKind.Utc).AddTicks(496));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 29, 7, 29, 23, 245, DateTimeKind.Utc).AddTicks(499));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 9, 7, 29, 23, 258, DateTimeKind.Utc).AddTicks(7252));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 19, 7, 29, 23, 258, DateTimeKind.Utc).AddTicks(7273));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 7, 29, 23, 258, DateTimeKind.Utc).AddTicks(7277));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 24, 7, 29, 23, 258, DateTimeKind.Utc).AddTicks(7282));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 29, 7, 29, 23, 258, DateTimeKind.Utc).AddTicks(7360));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 7, 29, 23, 258, DateTimeKind.Utc).AddTicks(7381));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 5, 7, 29, 23, 258, DateTimeKind.Utc).AddTicks(7384));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 7, 7, 29, 23, 258, DateTimeKind.Utc).AddTicks(7388));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 6, 7, 29, 23, 258, DateTimeKind.Utc).AddTicks(7391));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 9, 7, 29, 23, 259, DateTimeKind.Utc).AddTicks(5674));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 14, 7, 29, 23, 259, DateTimeKind.Utc).AddTicks(5688));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 19, 7, 29, 23, 259, DateTimeKind.Utc).AddTicks(5691));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 24, 7, 29, 23, 259, DateTimeKind.Utc).AddTicks(5693));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 29, 7, 29, 23, 259, DateTimeKind.Utc).AddTicks(5695));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 4, 7, 29, 23, 259, DateTimeKind.Utc).AddTicks(5698));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 21, 7, 29, 23, 259, DateTimeKind.Utc).AddTicks(5700));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 27, 7, 29, 23, 259, DateTimeKind.Utc).AddTicks(5702));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 7, 29, 23, 256, DateTimeKind.Utc).AddTicks(577));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 29, 7, 29, 23, 256, DateTimeKind.Utc).AddTicks(593));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 7, 29, 23, 256, DateTimeKind.Utc).AddTicks(599));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 9, 7, 29, 23, 256, DateTimeKind.Utc).AddTicks(602));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 14, 7, 29, 23, 256, DateTimeKind.Utc).AddTicks(605));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 8, 7, 29, 23, 265, DateTimeKind.Utc).AddTicks(4225));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 8, 7, 29, 23, 265, DateTimeKind.Utc).AddTicks(4232));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 8, 7, 29, 23, 265, DateTimeKind.Utc).AddTicks(4235));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 8, 7, 29, 23, 265, DateTimeKind.Utc).AddTicks(4237));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(5779));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(5788));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(5791));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(5794));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 7, 29, 23, 244, DateTimeKind.Utc).AddTicks(5796));
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddGithubBranchToBoardStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoardStates_BoardId_Source",
                table: "BoardStates");

            migrationBuilder.AddColumn<string>(
                name: "GithubBranch",
                table: "BoardStates",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 19, 52, 51, 304, DateTimeKind.Utc).AddTicks(1826));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 19, 52, 51, 304, DateTimeKind.Utc).AddTicks(1831));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(6345));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(6364));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(6368));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(6371));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(6373));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(6376));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 282, DateTimeKind.Utc).AddTicks(950));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 52, 51, 282, DateTimeKind.Utc).AddTicks(956));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 28, 19, 52, 51, 282, DateTimeKind.Utc).AddTicks(960));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 19, 52, 51, 282, DateTimeKind.Utc).AddTicks(2241));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 19, 52, 51, 282, DateTimeKind.Utc).AddTicks(2248));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 19, 52, 51, 282, DateTimeKind.Utc).AddTicks(2251));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 19, 52, 51, 282, DateTimeKind.Utc).AddTicks(2253));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 19, 52, 51, 282, DateTimeKind.Utc).AddTicks(2256));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 19, 52, 51, 282, DateTimeKind.Utc).AddTicks(2258));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 18, 19, 52, 51, 293, DateTimeKind.Utc).AddTicks(6484));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 19, 52, 51, 293, DateTimeKind.Utc).AddTicks(6502));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 19, 52, 51, 293, DateTimeKind.Utc).AddTicks(6506));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 19, 52, 51, 293, DateTimeKind.Utc).AddTicks(6509));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 7, 19, 52, 51, 293, DateTimeKind.Utc).AddTicks(6512));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 12, 19, 52, 51, 293, DateTimeKind.Utc).AddTicks(6531));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 14, 19, 52, 51, 293, DateTimeKind.Utc).AddTicks(6534));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 16, 19, 52, 51, 293, DateTimeKind.Utc).AddTicks(6537));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 15, 19, 52, 51, 293, DateTimeKind.Utc).AddTicks(6540));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 18, 19, 52, 51, 294, DateTimeKind.Utc).AddTicks(4021));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 23, 19, 52, 51, 294, DateTimeKind.Utc).AddTicks(4031));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 28, 19, 52, 51, 294, DateTimeKind.Utc).AddTicks(4033));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 2, 19, 52, 51, 294, DateTimeKind.Utc).AddTicks(4035));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 7, 19, 52, 51, 294, DateTimeKind.Utc).AddTicks(4038));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 13, 19, 52, 51, 294, DateTimeKind.Utc).AddTicks(4040));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 30, 19, 52, 51, 294, DateTimeKind.Utc).AddTicks(4042));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 5, 19, 52, 51, 294, DateTimeKind.Utc).AddTicks(4044));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 3, 19, 52, 51, 292, DateTimeKind.Utc).AddTicks(9886));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 19, 52, 51, 292, DateTimeKind.Utc).AddTicks(9905));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 52, 51, 292, DateTimeKind.Utc).AddTicks(9908));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 18, 19, 52, 51, 292, DateTimeKind.Utc).AddTicks(9911));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 23, 19, 52, 51, 292, DateTimeKind.Utc).AddTicks(9914));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 19, 52, 51, 299, DateTimeKind.Utc).AddTicks(5030));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 19, 52, 51, 299, DateTimeKind.Utc).AddTicks(5034));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 19, 52, 51, 299, DateTimeKind.Utc).AddTicks(5036));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 19, 52, 51, 299, DateTimeKind.Utc).AddTicks(5038));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(7910));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(7916));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(7918));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(7921));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 19, 52, 51, 281, DateTimeKind.Utc).AddTicks(7923));

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BoardId_Source",
                table: "BoardStates",
                columns: new[] { "BoardId", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoardStates_BoardId_Source",
                table: "BoardStates");

            migrationBuilder.DropColumn(
                name: "GithubBranch",
                table: "BoardStates");

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
                name: "IX_BoardStates_BoardId_Source",
                table: "BoardStates",
                columns: new[] { "BoardId", "Source" });
        }
    }
}

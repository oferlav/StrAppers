using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddGithubBranchToBoardStatesUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoardStates_BoardId_Source_Webhook",
                table: "BoardStates");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 15, 42, 934, DateTimeKind.Utc).AddTicks(1048));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 15, 42, 934, DateTimeKind.Utc).AddTicks(1054));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(4678));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(4693));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(4696));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(4698));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(4700));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(4703));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(8866));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 28, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(8872));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 3, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(8875));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 15, 42, 914, DateTimeKind.Utc).AddTicks(71));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 15, 42, 914, DateTimeKind.Utc).AddTicks(78));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 15, 42, 914, DateTimeKind.Utc).AddTicks(81));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 15, 42, 914, DateTimeKind.Utc).AddTicks(83));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 15, 42, 914, DateTimeKind.Utc).AddTicks(86));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 15, 42, 914, DateTimeKind.Utc).AddTicks(89));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 23, 19, 15, 42, 924, DateTimeKind.Utc).AddTicks(4813));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 19, 15, 42, 924, DateTimeKind.Utc).AddTicks(4825));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 24, 19, 15, 42, 924, DateTimeKind.Utc).AddTicks(4828));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 7, 19, 15, 42, 924, DateTimeKind.Utc).AddTicks(4831));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 12, 19, 15, 42, 924, DateTimeKind.Utc).AddTicks(4834));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 19, 15, 42, 924, DateTimeKind.Utc).AddTicks(4837));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 19, 19, 15, 42, 924, DateTimeKind.Utc).AddTicks(4840));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 21, 19, 15, 42, 924, DateTimeKind.Utc).AddTicks(4843));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 20, 19, 15, 42, 924, DateTimeKind.Utc).AddTicks(4846));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 23, 19, 15, 42, 925, DateTimeKind.Utc).AddTicks(3003));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 28, 19, 15, 42, 925, DateTimeKind.Utc).AddTicks(3010));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 2, 19, 15, 42, 925, DateTimeKind.Utc).AddTicks(3012));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 7, 19, 15, 42, 925, DateTimeKind.Utc).AddTicks(3015));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 12, 19, 15, 42, 925, DateTimeKind.Utc).AddTicks(3017));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 18, 19, 15, 42, 925, DateTimeKind.Utc).AddTicks(3019));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 4, 19, 15, 42, 925, DateTimeKind.Utc).AddTicks(3021));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 10, 19, 15, 42, 925, DateTimeKind.Utc).AddTicks(3023));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 19, 15, 42, 923, DateTimeKind.Utc).AddTicks(9300));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 15, 42, 923, DateTimeKind.Utc).AddTicks(9315));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 18, 19, 15, 42, 923, DateTimeKind.Utc).AddTicks(9318));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 23, 19, 15, 42, 923, DateTimeKind.Utc).AddTicks(9321));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 19, 15, 42, 923, DateTimeKind.Utc).AddTicks(9324));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 15, 42, 929, DateTimeKind.Utc).AddTicks(9250));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 15, 42, 929, DateTimeKind.Utc).AddTicks(9255));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 15, 42, 929, DateTimeKind.Utc).AddTicks(9305));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 15, 42, 929, DateTimeKind.Utc).AddTicks(9307));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(6112));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(6120));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(6122));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(6125));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 15, 42, 913, DateTimeKind.Utc).AddTicks(6127));

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BoardId_Source_Webhook_GithubBranch",
                table: "BoardStates",
                columns: new[] { "BoardId", "Source", "Webhook", "GithubBranch" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoardStates_BoardId_Source_Webhook_GithubBranch",
                table: "BoardStates");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 3, 31, 596, DateTimeKind.Utc).AddTicks(585));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 3, 31, 596, DateTimeKind.Utc).AddTicks(594));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 568, DateTimeKind.Utc).AddTicks(3957));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(1303));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(1308));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(1314));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(1317));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(1323));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(7721));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 28, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(7728));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 3, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(7731));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(9148));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(9155));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(9158));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(9161));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(9163));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(9166));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 23, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(6271));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(6288));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 24, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(6292));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 7, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(6296));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 12, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(6299));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(6316));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 19, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(6319));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 21, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(6323));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 20, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(6326));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 23, 19, 3, 31, 584, DateTimeKind.Utc).AddTicks(4337));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 28, 19, 3, 31, 584, DateTimeKind.Utc).AddTicks(4349));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 2, 19, 3, 31, 584, DateTimeKind.Utc).AddTicks(4352));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 7, 19, 3, 31, 584, DateTimeKind.Utc).AddTicks(4354));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 12, 19, 3, 31, 584, DateTimeKind.Utc).AddTicks(4357));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 18, 19, 3, 31, 584, DateTimeKind.Utc).AddTicks(4359));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 4, 19, 3, 31, 584, DateTimeKind.Utc).AddTicks(4362));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 10, 19, 3, 31, 584, DateTimeKind.Utc).AddTicks(4364));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(77));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(91));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 18, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(94));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 23, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(98));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 19, 3, 31, 583, DateTimeKind.Utc).AddTicks(102));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 3, 31, 591, DateTimeKind.Utc).AddTicks(987));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 3, 31, 591, DateTimeKind.Utc).AddTicks(994));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 3, 31, 591, DateTimeKind.Utc).AddTicks(996));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 19, 3, 31, 591, DateTimeKind.Utc).AddTicks(998));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(4607));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(4614));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(4617));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(4620));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 19, 3, 31, 569, DateTimeKind.Utc).AddTicks(4622));

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BoardId_Source_Webhook",
                table: "BoardStates",
                columns: new[] { "BoardId", "Source", "Webhook" },
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class FixBoardStatesUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoardStates_BoardId_Source",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoardStates_BoardId_Source_Webhook",
                table: "BoardStates");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 18, 37, 47, 330, DateTimeKind.Utc).AddTicks(9614));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 18, 37, 47, 330, DateTimeKind.Utc).AddTicks(9620));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(1296));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(1311));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(1314));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(1316));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(1319));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(1322));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(6152));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 28, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(6160));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 3, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(6164));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(7611));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(7622));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(7625));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(7628));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(7630));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(7633));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 23, 18, 37, 47, 318, DateTimeKind.Utc).AddTicks(5765));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 18, 37, 47, 318, DateTimeKind.Utc).AddTicks(5776));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 24, 18, 37, 47, 318, DateTimeKind.Utc).AddTicks(5781));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 7, 18, 37, 47, 318, DateTimeKind.Utc).AddTicks(5786));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 12, 18, 37, 47, 318, DateTimeKind.Utc).AddTicks(5789));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 17, 18, 37, 47, 318, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 19, 18, 37, 47, 318, DateTimeKind.Utc).AddTicks(5810));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 21, 18, 37, 47, 318, DateTimeKind.Utc).AddTicks(5816));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 20, 18, 37, 47, 318, DateTimeKind.Utc).AddTicks(5820));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 23, 18, 37, 47, 319, DateTimeKind.Utc).AddTicks(4656));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 28, 18, 37, 47, 319, DateTimeKind.Utc).AddTicks(4668));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 2, 18, 37, 47, 319, DateTimeKind.Utc).AddTicks(4671));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 7, 18, 37, 47, 319, DateTimeKind.Utc).AddTicks(4673));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 12, 18, 37, 47, 319, DateTimeKind.Utc).AddTicks(4676));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 18, 18, 37, 47, 319, DateTimeKind.Utc).AddTicks(4678));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 4, 18, 37, 47, 319, DateTimeKind.Utc).AddTicks(4681));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 1, 10, 18, 37, 47, 319, DateTimeKind.Utc).AddTicks(4683));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 18, 37, 47, 317, DateTimeKind.Utc).AddTicks(9602));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 18, 37, 47, 317, DateTimeKind.Utc).AddTicks(9623));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 18, 18, 37, 47, 317, DateTimeKind.Utc).AddTicks(9628));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 23, 18, 37, 47, 317, DateTimeKind.Utc).AddTicks(9632));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 18, 37, 47, 317, DateTimeKind.Utc).AddTicks(9636));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 18, 37, 47, 324, DateTimeKind.Utc).AddTicks(9121));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 18, 37, 47, 324, DateTimeKind.Utc).AddTicks(9127));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 18, 37, 47, 324, DateTimeKind.Utc).AddTicks(9130));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 22, 18, 37, 47, 324, DateTimeKind.Utc).AddTicks(9191));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(3030));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(3039));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(3042));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(3045));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 18, 37, 47, 301, DateTimeKind.Utc).AddTicks(3047));

            migrationBuilder.CreateIndex(
                name: "IX_BoardStates_BoardId_Source",
                table: "BoardStates",
                columns: new[] { "BoardId", "Source" },
                unique: true);
        }
    }
}

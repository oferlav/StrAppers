using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaUrlsToProjectBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresentationUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YoutubeUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 9, 49, 39, 186, DateTimeKind.Utc).AddTicks(2273));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 9, 49, 39, 186, DateTimeKind.Utc).AddTicks(2278));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(3532));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(3544));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(3547));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(3551));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(3555));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(3559));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(9116));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(9127));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(9131));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 9, 49, 39, 159, DateTimeKind.Utc).AddTicks(763));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 9, 49, 39, 159, DateTimeKind.Utc).AddTicks(770));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 9, 49, 39, 159, DateTimeKind.Utc).AddTicks(773));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 9, 49, 39, 159, DateTimeKind.Utc).AddTicks(776));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 9, 49, 39, 159, DateTimeKind.Utc).AddTicks(778));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 9, 49, 39, 159, DateTimeKind.Utc).AddTicks(781));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(6937));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 14, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(6944));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 5, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(6948));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 19, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(6952));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 24, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(6955));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 29, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(6970));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 31, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(6973));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(6976));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(6979));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 4, 9, 49, 39, 173, DateTimeKind.Utc).AddTicks(4483));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 9, 9, 49, 39, 173, DateTimeKind.Utc).AddTicks(4492));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 14, 9, 49, 39, 173, DateTimeKind.Utc).AddTicks(4494));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 19, 9, 49, 39, 173, DateTimeKind.Utc).AddTicks(4497));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 24, 9, 49, 39, 173, DateTimeKind.Utc).AddTicks(4499));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 29, 9, 49, 39, 173, DateTimeKind.Utc).AddTicks(4501));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 16, 9, 49, 39, 173, DateTimeKind.Utc).AddTicks(4503));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 22, 9, 49, 39, 173, DateTimeKind.Utc).AddTicks(4508));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(939));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(955));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 29, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(958));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 4, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(961));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 9, 9, 49, 39, 172, DateTimeKind.Utc).AddTicks(965));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 9, 49, 39, 180, DateTimeKind.Utc).AddTicks(8522));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 9, 49, 39, 180, DateTimeKind.Utc).AddTicks(8526));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 9, 49, 39, 180, DateTimeKind.Utc).AddTicks(8528));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 3, 9, 49, 39, 180, DateTimeKind.Utc).AddTicks(8530));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(5235));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(5244));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(5247));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(5250));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 9, 49, 39, 158, DateTimeKind.Utc).AddTicks(5252));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "PresentationUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "YoutubeUrl",
                table: "ProjectBoards");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 17, 3, 54, 650, DateTimeKind.Utc).AddTicks(1783));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 17, 3, 54, 650, DateTimeKind.Utc).AddTicks(1790));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(5659));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(5675));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(5678));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(5680));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(5682));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(5685));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 622, DateTimeKind.Utc).AddTicks(839));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 17, 3, 54, 622, DateTimeKind.Utc).AddTicks(847));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 17, 3, 54, 622, DateTimeKind.Utc).AddTicks(851));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 17, 3, 54, 622, DateTimeKind.Utc).AddTicks(2332));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 17, 3, 54, 622, DateTimeKind.Utc).AddTicks(2338));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 17, 3, 54, 622, DateTimeKind.Utc).AddTicks(2340));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 17, 3, 54, 622, DateTimeKind.Utc).AddTicks(2343));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 17, 3, 54, 622, DateTimeKind.Utc).AddTicks(2345));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 17, 3, 54, 622, DateTimeKind.Utc).AddTicks(2348));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 3, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(369));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(376));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 4, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(379));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 18, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(382));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 23, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(386));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 28, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(410));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 30, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(413));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(416));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 31, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(420));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 3, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(8081));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 8, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(8092));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 13, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(8094));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 18, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(8096));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 23, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(8099));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 28, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(8101));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 15, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(8103));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 21, 17, 3, 54, 640, DateTimeKind.Utc).AddTicks(8105));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 3, 54, 639, DateTimeKind.Utc).AddTicks(4226));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 17, 3, 54, 639, DateTimeKind.Utc).AddTicks(4244));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 28, 17, 3, 54, 639, DateTimeKind.Utc).AddTicks(4248));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 3, 17, 3, 54, 639, DateTimeKind.Utc).AddTicks(4251));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 17, 3, 54, 639, DateTimeKind.Utc).AddTicks(4254));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 17, 3, 54, 645, DateTimeKind.Utc).AddTicks(7044));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 17, 3, 54, 645, DateTimeKind.Utc).AddTicks(7049));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 17, 3, 54, 645, DateTimeKind.Utc).AddTicks(7051));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 2, 17, 3, 54, 645, DateTimeKind.Utc).AddTicks(7053));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(7450));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(7456));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(7458));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(7461));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 3, 17, 3, 54, 621, DateTimeKind.Utc).AddTicks(7463));
        }
    }
}

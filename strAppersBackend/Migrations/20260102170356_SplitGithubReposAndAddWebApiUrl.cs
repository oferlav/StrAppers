using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class SplitGithubReposAndAddWebApiUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add GithubFrontendUrl column
            migrationBuilder.AddColumn<string>(
                name: "GithubFrontendUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Step 2: Add GithubBackendUrl column
            migrationBuilder.AddColumn<string>(
                name: "GithubBackendUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Step 3: Copy data from GithubUrl to GithubBackendUrl
            migrationBuilder.Sql(@"
                UPDATE ""ProjectBoards""
                SET ""GithubBackendUrl"" = ""GithubUrl""
                WHERE ""GithubBackendUrl"" IS NULL AND ""GithubUrl"" IS NOT NULL;
            ");

            // Step 4: Add WebApiUrl column
            migrationBuilder.AddColumn<string>(
                name: "WebApiUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Step 5: Drop the old GithubUrl column
            migrationBuilder.DropColumn(
                name: "GithubUrl",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Re-add GithubUrl column
            migrationBuilder.AddColumn<string>(
                name: "GithubUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // Step 2: Copy data back from GithubBackendUrl to GithubUrl
            migrationBuilder.Sql(@"
                UPDATE ""ProjectBoards""
                SET ""GithubUrl"" = ""GithubBackendUrl""
                WHERE ""GithubUrl"" IS NULL AND ""GithubBackendUrl"" IS NOT NULL;
            ");

            // Step 3: Drop the new columns
            migrationBuilder.DropColumn(
                name: "WebApiUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "GithubBackendUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "GithubFrontendUrl",
                table: "ProjectBoards");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 20, 13, 39, 49, 734, DateTimeKind.Utc).AddTicks(871));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 20, 13, 39, 49, 734, DateTimeKind.Utc).AddTicks(876));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(6039));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(6051));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(6053));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(6056));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(6058));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(6060));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 711, DateTimeKind.Utc).AddTicks(145));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 26, 13, 39, 49, 711, DateTimeKind.Utc).AddTicks(179));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 31, 13, 39, 49, 711, DateTimeKind.Utc).AddTicks(182));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 10, 13, 39, 49, 711, DateTimeKind.Utc).AddTicks(1419));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 10, 13, 39, 49, 711, DateTimeKind.Utc).AddTicks(1424));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 10, 13, 39, 49, 711, DateTimeKind.Utc).AddTicks(1426));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 10, 13, 39, 49, 711, DateTimeKind.Utc).AddTicks(1428));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 10, 13, 39, 49, 711, DateTimeKind.Utc).AddTicks(1431));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 10, 13, 39, 49, 711, DateTimeKind.Utc).AddTicks(1433));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 20, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(7286));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 30, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(7295));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 21, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(7299));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 5, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(7302));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 10, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(7306));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 15, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(7324));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 17, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(7327));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 19, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(7330));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 18, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(7333));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 20, 13, 39, 49, 724, DateTimeKind.Utc).AddTicks(4628));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 25, 13, 39, 49, 724, DateTimeKind.Utc).AddTicks(4639));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 30, 13, 39, 49, 724, DateTimeKind.Utc).AddTicks(4641));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 5, 13, 39, 49, 724, DateTimeKind.Utc).AddTicks(4644));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 10, 13, 39, 49, 724, DateTimeKind.Utc).AddTicks(4649));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 15, 13, 39, 49, 724, DateTimeKind.Utc).AddTicks(4651));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 2, 13, 39, 49, 724, DateTimeKind.Utc).AddTicks(4653));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 8, 13, 39, 49, 724, DateTimeKind.Utc).AddTicks(4656));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 5, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(1383));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 10, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(1400));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 15, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(1404));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 20, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(1407));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 25, 13, 39, 49, 723, DateTimeKind.Utc).AddTicks(1410));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 20, 13, 39, 49, 729, DateTimeKind.Utc).AddTicks(3864));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 20, 13, 39, 49, 729, DateTimeKind.Utc).AddTicks(3869));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 20, 13, 39, 49, 729, DateTimeKind.Utc).AddTicks(3871));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 20, 13, 39, 49, 729, DateTimeKind.Utc).AddTicks(3873));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(7404));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(7407));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(7410));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(7412));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 13, 39, 49, 710, DateTimeKind.Utc).AddTicks(7414));
        }
    }
}

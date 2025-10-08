using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStudentIdLengthAndAddProjectBoardUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBoards_ProjectStatuses_ProjectStatusId",
                table: "ProjectBoards");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBoards_ProjectStatuses_StatusId",
                table: "ProjectBoards");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectStatuses_StatusId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Organizations_OrganizationId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_OrganizationId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Projects_StatusId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "StudentRoles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "StudentRoles");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "HasAdmin",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "HasAdmin",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Organizations");

            migrationBuilder.RenameColumn(
                name: "IsAvailable",
                table: "Projects",
                newName: "isAvailable");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ProjectBoards",
                newName: "BoardId");

            migrationBuilder.RenameColumn(
                name: "ProjectStatusId",
                table: "ProjectBoards",
                newName: "AdminId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectBoards_ProjectStatusId",
                table: "ProjectBoards",
                newName: "IX_ProjectBoards_AdminId");

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BoardURL",
                table: "ProjectBoards",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MovieUrl",
                table: "ProjectBoards",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishUrl",
                table: "ProjectBoards",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SprintPlan",
                table: "ProjectBoards",
                type: "jsonb",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(5782));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(5796));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(5798));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(5801));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(5803));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(8878));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 11, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(8886));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 16, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(8890));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 425, DateTimeKind.Utc).AddTicks(185));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 425, DateTimeKind.Utc).AddTicks(190));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 425, DateTimeKind.Utc).AddTicks(193));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 425, DateTimeKind.Utc).AddTicks(195));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 425, DateTimeKind.Utc).AddTicks(197));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 425, DateTimeKind.Utc).AddTicks(200));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 5, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(3852));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 15, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(3860));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 7, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(3863));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(3866));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 25, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(3868));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 30, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(3871));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 2, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(3874));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 4, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(3876));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 3, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(3878));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(5259));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(5264));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(5267));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(5270));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(5272));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(5274));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(5277));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(5279));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 5, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(9811));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 10, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(9821));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 15, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(9824));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 20, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(9876));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 25, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(9879));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 31, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(9881));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 17, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(9883));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 23, 20, 24, 1, 430, DateTimeKind.Utc).AddTicks(9886));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "IsAvailable", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 21, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8953), true, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "IsAvailable", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 26, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8965), true, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "IsAvailable", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 31, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8969), true, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "IsAvailable" },
                values: new object[] { new DateTime(2025, 9, 5, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8972), true });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "IsAvailable" },
                values: new object[] { new DateTime(2025, 9, 10, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8975), true });

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(7233));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(7239));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(7241));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(7244));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 20, 24, 1, 424, DateTimeKind.Utc).AddTicks(7246));

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBoards_ProjectStatuses_StatusId",
                table: "ProjectBoards",
                column: "StatusId",
                principalTable: "ProjectStatuses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBoards_Students_AdminId",
                table: "ProjectBoards",
                column: "AdminId",
                principalTable: "Students",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBoards_ProjectStatuses_StatusId",
                table: "ProjectBoards");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBoards_Students_AdminId",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "BoardURL",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "MovieUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "PublishUrl",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "SprintPlan",
                table: "ProjectBoards");

            migrationBuilder.RenameColumn(
                name: "isAvailable",
                table: "Projects",
                newName: "IsAvailable");

            migrationBuilder.RenameColumn(
                name: "BoardId",
                table: "ProjectBoards",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "AdminId",
                table: "ProjectBoards",
                newName: "ProjectStatusId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectBoards_AdminId",
                table: "ProjectBoards",
                newName: "IX_ProjectBoards_ProjectStatusId");

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "StudentRoles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "StudentRoles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAdmin",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasAdmin",
                table: "ProjectBoards",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2863));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2880));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2883));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2885));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2888));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2890));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ContactPhone", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(6145) });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ContactPhone", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 7, 30, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(6153) });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ContactPhone", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 8, 4, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(6156) });

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7398));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7403));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7406));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7409));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7412));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7415));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "EndDate", "HasAdmin", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 8, 24, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9164), new DateTime(2025, 10, 23, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9159), null, true, new DateTime(2025, 8, 24, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9147), 3 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "EndDate", "HasAdmin", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 9, 3, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9172), new DateTime(2025, 11, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9170), null, false, new DateTime(2025, 9, 3, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9169), 2 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "DueDate", "EndDate", "HasAdmin", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 6, 25, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9177), null, new DateTime(2025, 9, 13, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9175), true, new DateTime(2025, 6, 25, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9174), 5 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "EndDate", "HasAdmin", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 9, 8, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9181), new DateTime(2025, 11, 7, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9180), null, false, new DateTime(2025, 9, 8, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9179), 3 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "DueDate", "EndDate", "HasAdmin", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 9, 13, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9185), null, null, false, new DateTime(2025, 9, 13, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9184), 4 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "EndDate", "HasAdmin", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 9, 18, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9190), new DateTime(2025, 11, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9189), null, false, new DateTime(2025, 9, 18, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9187), 1 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "EndDate", "HasAdmin", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 9, 20, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9195), new DateTime(2025, 12, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9193), null, false, new DateTime(2025, 9, 20, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9193), 1 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "DueDate", "EndDate", "HasAdmin", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 9, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9200), new DateTime(2025, 12, 7, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9198), null, false, new DateTime(2025, 9, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9197), 1 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DueDate", "EndDate", "HasAdmin", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 9, 21, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9204), new DateTime(2026, 1, 21, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9203), null, false, new DateTime(2025, 9, 21, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9202), 1 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(811));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(816));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(819));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(821));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(824));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(826));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(828));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(830));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AssignedAt", "AssignedDate", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6034), new DateTime(2025, 8, 24, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6040), null });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AssignedAt", "AssignedDate", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6045), new DateTime(2025, 8, 29, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6047), null });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AssignedAt", "AssignedDate", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6048), new DateTime(2025, 9, 3, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6049), null });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "AssignedAt", "AssignedDate", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6051), new DateTime(2025, 9, 8, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6052), null });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "AssignedAt", "AssignedDate", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6053), new DateTime(2025, 9, 13, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6054), null });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "AssignedAt", "AssignedDate", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6056), new DateTime(2025, 8, 19, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6057), null });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "AssignedAt", "AssignedDate", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6058), new DateTime(2025, 9, 5, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6060), null });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "AssignedAt", "AssignedDate", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6061), new DateTime(2025, 9, 11, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6062), null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "OrganizationId", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 9, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9973), 1, 1 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "OrganizationId", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 14, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9985), 1, 2 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "OrganizationId", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 19, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9989), 1, 3 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "OrganizationId" },
                values: new object[] { new DateTime(2025, 8, 24, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9992), 1 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "OrganizationId" },
                values: new object[] { new DateTime(2025, 8, 29, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9995), 1 });

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4430));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4435));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4438));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4441));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4443));

            migrationBuilder.CreateIndex(
                name: "IX_Students_OrganizationId",
                table: "Students",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_StatusId",
                table: "Projects",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBoards_ProjectStatuses_ProjectStatusId",
                table: "ProjectBoards",
                column: "ProjectStatusId",
                principalTable: "ProjectStatuses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBoards_ProjectStatuses_StatusId",
                table: "ProjectBoards",
                column: "StatusId",
                principalTable: "ProjectStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ProjectStatuses_StatusId",
                table: "Projects",
                column: "StatusId",
                principalTable: "ProjectStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Organizations_OrganizationId",
                table: "Students",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

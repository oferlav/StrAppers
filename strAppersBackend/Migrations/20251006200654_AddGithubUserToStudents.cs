using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddGithubUserToStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StudentId",
                table: "Students",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GithubUser",
                table: "Students",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GithubUser",
                table: "Students");

            migrationBuilder.AlterColumn<string>(
                name: "StudentId",
                table: "Students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

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
                column: "CreatedAt",
                value: new DateTime(2025, 8, 21, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8953));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 26, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8965));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 31, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8969));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 5, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8972));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 10, 20, 24, 1, 429, DateTimeKind.Utc).AddTicks(8975));

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
        }
    }
}

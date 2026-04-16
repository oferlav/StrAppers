using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentNextMeetingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextMeetingTime",
                table: "Students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextMeetingUrl",
                table: "Students",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 16, 40, 39, 613, DateTimeKind.Utc).AddTicks(9231));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 16, 40, 39, 613, DateTimeKind.Utc).AddTicks(9240));

            migrationBuilder.UpdateData(
                table: "Institutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(1221));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 577, DateTimeKind.Utc).AddTicks(7270));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 577, DateTimeKind.Utc).AddTicks(7282));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 577, DateTimeKind.Utc).AddTicks(7285));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 577, DateTimeKind.Utc).AddTicks(7288));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 577, DateTimeKind.Utc).AddTicks(7290));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 577, DateTimeKind.Utc).AddTicks(7297));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(5597));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(5604));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 23, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(5617));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(6872));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(6878));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(6881));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(6928));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(6930));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(6933));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 16, 40, 39, 595, DateTimeKind.Utc).AddTicks(4846));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 16, 40, 39, 595, DateTimeKind.Utc).AddTicks(4855));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 14, 16, 40, 39, 595, DateTimeKind.Utc).AddTicks(4859));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 30, 16, 40, 39, 595, DateTimeKind.Utc).AddTicks(4862));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 4, 16, 40, 39, 595, DateTimeKind.Utc).AddTicks(5070));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 16, 40, 39, 595, DateTimeKind.Utc).AddTicks(5074));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 16, 40, 39, 595, DateTimeKind.Utc).AddTicks(5077));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 13, 16, 40, 39, 595, DateTimeKind.Utc).AddTicks(5080));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 16, 40, 39, 595, DateTimeKind.Utc).AddTicks(5083));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 15, 16, 40, 39, 596, DateTimeKind.Utc).AddTicks(5612));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 20, 16, 40, 39, 596, DateTimeKind.Utc).AddTicks(5620));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 25, 16, 40, 39, 596, DateTimeKind.Utc).AddTicks(5623));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 30, 16, 40, 39, 596, DateTimeKind.Utc).AddTicks(5625));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 4, 16, 40, 39, 596, DateTimeKind.Utc).AddTicks(5630));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 10, 16, 40, 39, 596, DateTimeKind.Utc).AddTicks(5633));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 27, 16, 40, 39, 596, DateTimeKind.Utc).AddTicks(5635));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 2, 16, 40, 39, 596, DateTimeKind.Utc).AddTicks(5637));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "NextMeetingTime", "NextMeetingUrl" },
                values: new object[] { new DateTime(2026, 2, 28, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6586), null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "NextMeetingTime", "NextMeetingUrl" },
                values: new object[] { new DateTime(2026, 3, 5, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6604), null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "NextMeetingTime", "NextMeetingUrl" },
                values: new object[] { new DateTime(2026, 3, 10, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6608), null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "NextMeetingTime", "NextMeetingUrl" },
                values: new object[] { new DateTime(2026, 3, 15, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6611), null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "NextMeetingTime", "NextMeetingUrl" },
                values: new object[] { new DateTime(2026, 3, 20, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6615), null, null });

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 16, 40, 39, 608, DateTimeKind.Utc).AddTicks(4320));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 16, 40, 39, 608, DateTimeKind.Utc).AddTicks(4324));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 16, 40, 39, 608, DateTimeKind.Utc).AddTicks(4326));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 16, 40, 39, 608, DateTimeKind.Utc).AddTicks(4328));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(2435));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(2440));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(2443));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(2445));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 16, 40, 39, 578, DateTimeKind.Utc).AddTicks(2447));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextMeetingTime",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "NextMeetingUrl",
                table: "Students");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 15, 6, 45, 173, DateTimeKind.Utc).AddTicks(6986));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 15, 6, 45, 173, DateTimeKind.Utc).AddTicks(6992));

            migrationBuilder.UpdateData(
                table: "Institutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(2223));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 141, DateTimeKind.Utc).AddTicks(8364));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 141, DateTimeKind.Utc).AddTicks(8379));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 141, DateTimeKind.Utc).AddTicks(8382));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 141, DateTimeKind.Utc).AddTicks(8385));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 141, DateTimeKind.Utc).AddTicks(8387));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 141, DateTimeKind.Utc).AddTicks(8390));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(6205));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(6212));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 23, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(6225));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(7463));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(7468));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(7471));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(7474));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(7478));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(7480));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 15, 6, 45, 157, DateTimeKind.Utc).AddTicks(4557));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 15, 6, 45, 157, DateTimeKind.Utc).AddTicks(4570));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 14, 15, 6, 45, 157, DateTimeKind.Utc).AddTicks(4574));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 30, 15, 6, 45, 157, DateTimeKind.Utc).AddTicks(4577));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 4, 15, 6, 45, 157, DateTimeKind.Utc).AddTicks(4611));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 15, 6, 45, 157, DateTimeKind.Utc).AddTicks(4614));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 15, 6, 45, 157, DateTimeKind.Utc).AddTicks(4617));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 13, 15, 6, 45, 157, DateTimeKind.Utc).AddTicks(4621));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 15, 6, 45, 157, DateTimeKind.Utc).AddTicks(4624));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 15, 15, 6, 45, 158, DateTimeKind.Utc).AddTicks(4445));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 20, 15, 6, 45, 158, DateTimeKind.Utc).AddTicks(4455));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 25, 15, 6, 45, 158, DateTimeKind.Utc).AddTicks(4457));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 30, 15, 6, 45, 158, DateTimeKind.Utc).AddTicks(4460));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 4, 15, 6, 45, 158, DateTimeKind.Utc).AddTicks(4462));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 10, 15, 6, 45, 158, DateTimeKind.Utc).AddTicks(4465));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 27, 15, 6, 45, 158, DateTimeKind.Utc).AddTicks(4467));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 2, 15, 6, 45, 158, DateTimeKind.Utc).AddTicks(4469));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 28, 15, 6, 45, 156, DateTimeKind.Utc).AddTicks(7884));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 15, 6, 45, 156, DateTimeKind.Utc).AddTicks(7897));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 10, 15, 6, 45, 156, DateTimeKind.Utc).AddTicks(7901));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 15, 6, 45, 156, DateTimeKind.Utc).AddTicks(7907));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 20, 15, 6, 45, 156, DateTimeKind.Utc).AddTicks(7910));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 15, 6, 45, 169, DateTimeKind.Utc).AddTicks(234));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 15, 6, 45, 169, DateTimeKind.Utc).AddTicks(238));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 15, 6, 45, 169, DateTimeKind.Utc).AddTicks(240));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 15, 6, 45, 169, DateTimeKind.Utc).AddTicks(242));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(3314));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(3320));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(3326));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(3328));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 15, 6, 45, 142, DateTimeKind.Utc).AddTicks(3331));
        }
    }
}

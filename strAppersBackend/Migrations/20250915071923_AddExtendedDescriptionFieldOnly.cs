using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedDescriptionFieldOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(106));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(120));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(123));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(125));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(127));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(130));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(2736));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 22, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(2742));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 27, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(2746));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(3759));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(3765));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(3768));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(3771));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(3773));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(3776));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 16, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9977), new DateTime(2025, 10, 15, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9973), new DateTime(2025, 8, 16, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9961) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 26, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9983), new DateTime(2025, 11, 14, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9982), new DateTime(2025, 8, 26, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9981) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 17, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9988), new DateTime(2025, 9, 5, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9987), new DateTime(2025, 6, 17, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9986) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 31, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9993), new DateTime(2025, 10, 30, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9992), new DateTime(2025, 8, 31, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9991) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 5, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9997), new DateTime(2025, 9, 5, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9996) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(2), new DateTime(2025, 11, 14, 7, 19, 22, 263, DateTimeKind.Utc), new DateTime(2025, 9, 10, 7, 19, 22, 263, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 12, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(7), new DateTime(2025, 12, 14, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5), new DateTime(2025, 9, 12, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(4) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 14, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(11), new DateTime(2025, 11, 29, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(10), new DateTime(2025, 9, 14, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(9) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 13, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(15), new DateTime(2026, 1, 13, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(14), new DateTime(2025, 9, 13, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(13) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(1272));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(1278));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(1281));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(1283));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(1286));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(1288));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(1290));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(1293));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 16, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5178));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 21, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5186));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 26, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5189));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 31, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5192));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 5, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5194));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 11, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5196));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 28, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5199));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 3, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5201));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 1, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(3949));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(3960));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 11, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(3964));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 16, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(3967));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 21, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(3971));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(1326));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(1331));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(1334));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(1336));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 19, 22, 261, DateTimeKind.Utc).AddTicks(1338));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(4566));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(4582));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(4585));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(4587));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(4589));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(4648));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(8152));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 22, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(8162));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 27, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(8165));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(9488));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(9498));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(9501));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(9508));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(9511));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(9515));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 16, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(341), new DateTime(2025, 10, 15, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(337), new DateTime(2025, 8, 16, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(326) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 26, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(347), new DateTime(2025, 11, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(345), new DateTime(2025, 8, 26, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(344) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 17, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(352), new DateTime(2025, 9, 5, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(351), new DateTime(2025, 6, 17, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(350) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 31, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(357), new DateTime(2025, 10, 30, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(355), new DateTime(2025, 8, 31, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(354) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 5, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(360), new DateTime(2025, 9, 5, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(359) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(365), new DateTime(2025, 11, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(363), new DateTime(2025, 9, 10, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(362) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 12, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(370), new DateTime(2025, 12, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(368), new DateTime(2025, 9, 12, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(367) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(377), new DateTime(2025, 11, 29, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(373), new DateTime(2025, 9, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(372) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 13, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(382), new DateTime(2026, 1, 13, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(380), new DateTime(2025, 9, 13, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(380) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(2066));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(2073));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(2076));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(2078));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(2081));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(2083));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(2085));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(2089));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 16, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(7917));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 21, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(7928));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 26, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(7931));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 31, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(7933));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 5, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(7935));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 11, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(7937));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 28, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(7940));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 3, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(7942));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 1, 7, 10, 23, 511, DateTimeKind.Utc).AddTicks(3067));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 10, 23, 511, DateTimeKind.Utc).AddTicks(3079));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 11, 7, 10, 23, 511, DateTimeKind.Utc).AddTicks(3083));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 16, 7, 10, 23, 511, DateTimeKind.Utc).AddTicks(3087));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 21, 7, 10, 23, 511, DateTimeKind.Utc).AddTicks(3090));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(6153));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(6158));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(6161));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(6163));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 10, 23, 509, DateTimeKind.Utc).AddTicks(6166));
        }
    }
}

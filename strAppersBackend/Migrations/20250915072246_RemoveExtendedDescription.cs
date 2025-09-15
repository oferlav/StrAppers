using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveExtendedDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtendedDescription",
                table: "Projects");

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(3061));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(3074));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(3077));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(3079));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(3081));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(3084));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(5669));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 22, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(5675));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 27, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(5718));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(6807));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(6812));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(6814));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(6817));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(6819));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(6822));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 16, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1338), new DateTime(2025, 10, 15, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1332), new DateTime(2025, 8, 16, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1319) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 26, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1345), new DateTime(2025, 11, 14, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1344), new DateTime(2025, 8, 26, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1343) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 17, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1350), new DateTime(2025, 9, 5, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1349), new DateTime(2025, 6, 17, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1348) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 31, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1355), new DateTime(2025, 10, 30, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1354), new DateTime(2025, 8, 31, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1353) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 5, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1358), new DateTime(2025, 9, 5, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1357) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1363), new DateTime(2025, 11, 14, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1362), new DateTime(2025, 9, 10, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1361) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 12, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1368), new DateTime(2025, 12, 14, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1367), new DateTime(2025, 9, 12, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1365) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 14, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1372), new DateTime(2025, 11, 29, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1371), new DateTime(2025, 9, 14, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1370) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 13, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1377), new DateTime(2026, 1, 13, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1375), new DateTime(2025, 9, 13, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(1375) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(2627));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(2633));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(2635));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(2638));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(2640));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(2642));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(2644));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(2648));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 16, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(6528));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 21, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(6539));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 26, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(6542));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 31, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(6544));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 5, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(6546));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 11, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(6549));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 28, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(6551));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 3, 7, 22, 45, 512, DateTimeKind.Utc).AddTicks(6554));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 1, 7, 22, 45, 511, DateTimeKind.Utc).AddTicks(6602));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 6, 7, 22, 45, 511, DateTimeKind.Utc).AddTicks(6614));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 11, 7, 22, 45, 511, DateTimeKind.Utc).AddTicks(6618));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 16, 7, 22, 45, 511, DateTimeKind.Utc).AddTicks(6625));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 21, 7, 22, 45, 511, DateTimeKind.Utc).AddTicks(6628));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(4347));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(4351));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(4354));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(4356));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 17, 7, 22, 45, 510, DateTimeKind.Utc).AddTicks(4358));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtendedDescription",
                table: "Projects",
                type: "TEXT",
                nullable: true);

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
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 16, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9977), new DateTime(2025, 10, 15, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9973), null, new DateTime(2025, 8, 16, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9961) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 26, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9983), new DateTime(2025, 11, 14, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9982), null, new DateTime(2025, 8, 26, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9981) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 17, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9988), new DateTime(2025, 9, 5, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9987), null, new DateTime(2025, 6, 17, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9986) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 31, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9993), new DateTime(2025, 10, 30, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9992), null, new DateTime(2025, 8, 31, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9991) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 5, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9997), null, new DateTime(2025, 9, 5, 7, 19, 22, 262, DateTimeKind.Utc).AddTicks(9996) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(2), new DateTime(2025, 11, 14, 7, 19, 22, 263, DateTimeKind.Utc), null, new DateTime(2025, 9, 10, 7, 19, 22, 263, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 12, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(7), new DateTime(2025, 12, 14, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(5), null, new DateTime(2025, 9, 12, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(4) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 14, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(11), new DateTime(2025, 11, 29, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(10), null, new DateTime(2025, 9, 14, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(9) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 13, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(15), new DateTime(2026, 1, 13, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(14), null, new DateTime(2025, 9, 13, 7, 19, 22, 263, DateTimeKind.Utc).AddTicks(13) });

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
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class InstituteTemplates_CourseName_Projects_CourseName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "InstituteTemplates",
                newName: "CourseName");

            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "Projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 4, 5, 32, 17, 20, DateTimeKind.Utc).AddTicks(8246));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 4, 5, 32, 17, 20, DateTimeKind.Utc).AddTicks(8251));

            migrationBuilder.UpdateData(
                table: "Institutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 4, 5, 32, 16, 973, DateTimeKind.Utc).AddTicks(8316));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 973, DateTimeKind.Utc).AddTicks(4058));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 973, DateTimeKind.Utc).AddTicks(4072));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 973, DateTimeKind.Utc).AddTicks(4075));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 973, DateTimeKind.Utc).AddTicks(4077));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 973, DateTimeKind.Utc).AddTicks(4080));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 973, DateTimeKind.Utc).AddTicks(4082));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 977, DateTimeKind.Utc).AddTicks(2038));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 10, 5, 32, 16, 977, DateTimeKind.Utc).AddTicks(2044));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 5, 32, 16, 977, DateTimeKind.Utc).AddTicks(2058));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 5, 32, 16, 977, DateTimeKind.Utc).AddTicks(3322));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 5, 32, 16, 977, DateTimeKind.Utc).AddTicks(3331));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 5, 32, 16, 977, DateTimeKind.Utc).AddTicks(3334));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 5, 32, 16, 977, DateTimeKind.Utc).AddTicks(3337));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 5, 32, 16, 977, DateTimeKind.Utc).AddTicks(3340));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 5, 32, 16, 977, DateTimeKind.Utc).AddTicks(3343));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CourseName", "CreatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 4, 5, 32, 16, 992, DateTimeKind.Utc).AddTicks(7342) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CourseName", "CreatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 14, 5, 32, 16, 992, DateTimeKind.Utc).AddTicks(7355) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CourseName", "CreatedAt" },
                values: new object[] { null, new DateTime(2026, 2, 3, 5, 32, 16, 992, DateTimeKind.Utc).AddTicks(7359) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CourseName", "CreatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 19, 5, 32, 16, 992, DateTimeKind.Utc).AddTicks(7363) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CourseName", "CreatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 24, 5, 32, 16, 992, DateTimeKind.Utc).AddTicks(7394) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CourseName", "CreatedAt" },
                values: new object[] { null, new DateTime(2026, 4, 29, 5, 32, 16, 992, DateTimeKind.Utc).AddTicks(7399) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CourseName", "CreatedAt" },
                values: new object[] { null, new DateTime(2026, 5, 1, 5, 32, 16, 992, DateTimeKind.Utc).AddTicks(7402) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CourseName", "CreatedAt" },
                values: new object[] { null, new DateTime(2026, 5, 3, 5, 32, 16, 992, DateTimeKind.Utc).AddTicks(7405) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CourseName", "CreatedAt" },
                values: new object[] { null, new DateTime(2026, 5, 2, 5, 32, 16, 992, DateTimeKind.Utc).AddTicks(7408) });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 4, 5, 32, 17, 1, DateTimeKind.Utc).AddTicks(1520));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 9, 5, 32, 17, 1, DateTimeKind.Utc).AddTicks(1532));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 14, 5, 32, 17, 1, DateTimeKind.Utc).AddTicks(1535));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 19, 5, 32, 17, 1, DateTimeKind.Utc).AddTicks(1538));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 24, 5, 32, 17, 1, DateTimeKind.Utc).AddTicks(1540));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 30, 5, 32, 17, 1, DateTimeKind.Utc).AddTicks(1543));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 16, 5, 32, 17, 1, DateTimeKind.Utc).AddTicks(1545));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 22, 5, 32, 17, 1, DateTimeKind.Utc).AddTicks(1547));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 20, 5, 32, 16, 991, DateTimeKind.Utc).AddTicks(7886));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 5, 32, 16, 991, DateTimeKind.Utc).AddTicks(7901));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 30, 5, 32, 16, 991, DateTimeKind.Utc).AddTicks(7905));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 4, 5, 32, 16, 991, DateTimeKind.Utc).AddTicks(7909));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 5, 32, 16, 991, DateTimeKind.Utc).AddTicks(7912));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 4, 5, 32, 17, 13, DateTimeKind.Utc).AddTicks(8766));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 4, 5, 32, 17, 13, DateTimeKind.Utc).AddTicks(8770));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 4, 5, 32, 17, 13, DateTimeKind.Utc).AddTicks(8773));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 4, 5, 32, 17, 13, DateTimeKind.Utc).AddTicks(8774));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 976, DateTimeKind.Utc).AddTicks(9330));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 976, DateTimeKind.Utc).AddTicks(9339));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 976, DateTimeKind.Utc).AddTicks(9342));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 976, DateTimeKind.Utc).AddTicks(9344));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 5, 32, 16, 976, DateTimeKind.Utc).AddTicks(9369));

            // Runs after seed UpdateData so Projects.CourseName is populated (seeds set null on add).
            migrationBuilder.Sql(
                """
                UPDATE "Projects"
                SET "CourseName" = LEFT("Title", 100)
                WHERE "CourseName" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourseName",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "CourseName",
                table: "InstituteTemplates",
                newName: "Name");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 3, 19, 1, 47, 815, DateTimeKind.Utc).AddTicks(1648));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 3, 19, 1, 47, 815, DateTimeKind.Utc).AddTicks(1656));

            migrationBuilder.UpdateData(
                table: "Institutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 19, 1, 47, 774, DateTimeKind.Utc).AddTicks(9049));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 774, DateTimeKind.Utc).AddTicks(5034));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 774, DateTimeKind.Utc).AddTicks(5046));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 774, DateTimeKind.Utc).AddTicks(5049));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 774, DateTimeKind.Utc).AddTicks(5052));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 774, DateTimeKind.Utc).AddTicks(5054));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 774, DateTimeKind.Utc).AddTicks(5057));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 778, DateTimeKind.Utc).AddTicks(322));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 9, 19, 1, 47, 778, DateTimeKind.Utc).AddTicks(329));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 14, 19, 1, 47, 778, DateTimeKind.Utc).AddTicks(340));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 19, 1, 47, 778, DateTimeKind.Utc).AddTicks(1626));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 19, 1, 47, 778, DateTimeKind.Utc).AddTicks(1635));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 19, 1, 47, 778, DateTimeKind.Utc).AddTicks(1638));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 19, 1, 47, 778, DateTimeKind.Utc).AddTicks(1641));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 19, 1, 47, 778, DateTimeKind.Utc).AddTicks(1643));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 19, 1, 47, 778, DateTimeKind.Utc).AddTicks(1646));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 19, 1, 47, 792, DateTimeKind.Utc).AddTicks(5811));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 13, 19, 1, 47, 792, DateTimeKind.Utc).AddTicks(5822));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 2, 19, 1, 47, 792, DateTimeKind.Utc).AddTicks(5826));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 18, 19, 1, 47, 792, DateTimeKind.Utc).AddTicks(5829));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 19, 1, 47, 792, DateTimeKind.Utc).AddTicks(5852));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 28, 19, 1, 47, 792, DateTimeKind.Utc).AddTicks(5856));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 30, 19, 1, 47, 792, DateTimeKind.Utc).AddTicks(5859));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 19, 1, 47, 792, DateTimeKind.Utc).AddTicks(5863));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 1, 19, 1, 47, 792, DateTimeKind.Utc).AddTicks(5866));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 3, 19, 1, 47, 799, DateTimeKind.Utc).AddTicks(7545));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 8, 19, 1, 47, 799, DateTimeKind.Utc).AddTicks(7555));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 13, 19, 1, 47, 799, DateTimeKind.Utc).AddTicks(7558));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 18, 19, 1, 47, 799, DateTimeKind.Utc).AddTicks(7561));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 23, 19, 1, 47, 799, DateTimeKind.Utc).AddTicks(7563));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 29, 19, 1, 47, 799, DateTimeKind.Utc).AddTicks(7565));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 15, 19, 1, 47, 799, DateTimeKind.Utc).AddTicks(7568));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 21, 19, 1, 47, 799, DateTimeKind.Utc).AddTicks(7570));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 19, 19, 1, 47, 791, DateTimeKind.Utc).AddTicks(7110));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 24, 19, 1, 47, 791, DateTimeKind.Utc).AddTicks(7123));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 29, 19, 1, 47, 791, DateTimeKind.Utc).AddTicks(7127));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 3, 19, 1, 47, 791, DateTimeKind.Utc).AddTicks(7130));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 8, 19, 1, 47, 791, DateTimeKind.Utc).AddTicks(7133));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 3, 19, 1, 47, 810, DateTimeKind.Utc).AddTicks(3907));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 3, 19, 1, 47, 810, DateTimeKind.Utc).AddTicks(3913));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 3, 19, 1, 47, 810, DateTimeKind.Utc).AddTicks(3916));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 3, 19, 1, 47, 810, DateTimeKind.Utc).AddTicks(3918));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 777, DateTimeKind.Utc).AddTicks(7662));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 777, DateTimeKind.Utc).AddTicks(7671));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 777, DateTimeKind.Utc).AddTicks(7674));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 777, DateTimeKind.Utc).AddTicks(7676));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 4, 19, 1, 47, 777, DateTimeKind.Utc).AddTicks(7678));
        }
    }
}

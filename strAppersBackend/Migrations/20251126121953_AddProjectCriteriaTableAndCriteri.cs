using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCriteriaTableAndCriteri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CriteriaIds",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectCriterias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCriterias", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(4034));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(4047));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(4050));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(4052));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(4055));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(4057));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(8556));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 2, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(8566));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 7, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(8569));

            migrationBuilder.InsertData(
                table: "ProjectCriterias",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Popular Projects" },
                    { 2, "UI/UX Designer Needed" },
                    { 3, "Backend Developer Needed" },
                    { 4, "Frontend Developer Needed" },
                    { 5, "Product manager Needed" },
                    { 6, "Marketing Needed" },
                    { 7, "New Projects" }
                });

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(9831));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(9835));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(9838));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(9841));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(9843));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(9845));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "CriteriaIds" },
                values: new object[] { new DateTime(2025, 10, 27, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4118), null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "CriteriaIds" },
                values: new object[] { new DateTime(2025, 11, 6, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4129), null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "CriteriaIds" },
                values: new object[] { new DateTime(2025, 8, 28, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4134), null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "CriteriaIds" },
                values: new object[] { new DateTime(2025, 11, 11, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4138), null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "CriteriaIds" },
                values: new object[] { new DateTime(2025, 11, 16, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4141), null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "CriteriaIds" },
                values: new object[] { new DateTime(2025, 11, 21, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4144), null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "CriteriaIds" },
                values: new object[] { new DateTime(2025, 11, 23, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4147), null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "CriteriaIds" },
                values: new object[] { new DateTime(2025, 11, 25, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4151), null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "CriteriaIds" },
                values: new object[] { new DateTime(2025, 11, 24, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4155), null });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(6131));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(6141));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(6143));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(6146));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(6148));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(6151));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(6153));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(6156));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 27, 12, 19, 52, 170, DateTimeKind.Utc).AddTicks(1413));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 1, 12, 19, 52, 170, DateTimeKind.Utc).AddTicks(1419));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 6, 12, 19, 52, 170, DateTimeKind.Utc).AddTicks(1425));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 11, 12, 19, 52, 170, DateTimeKind.Utc).AddTicks(1428));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 16, 12, 19, 52, 170, DateTimeKind.Utc).AddTicks(1430));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 22, 12, 19, 52, 170, DateTimeKind.Utc).AddTicks(1432));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 8, 12, 19, 52, 170, DateTimeKind.Utc).AddTicks(1434));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 14, 12, 19, 52, 170, DateTimeKind.Utc).AddTicks(1436));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 12, 12, 19, 52, 168, DateTimeKind.Utc).AddTicks(8232));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 17, 12, 19, 52, 168, DateTimeKind.Utc).AddTicks(8242));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 12, 19, 52, 168, DateTimeKind.Utc).AddTicks(8245));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 27, 12, 19, 52, 168, DateTimeKind.Utc).AddTicks(8249));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 1, 12, 19, 52, 168, DateTimeKind.Utc).AddTicks(8252));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(5526));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(5532));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(5593));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(5596));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 27, 12, 19, 52, 159, DateTimeKind.Utc).AddTicks(5598));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectCriterias");

            migrationBuilder.DropColumn(
                name: "CriteriaIds",
                table: "Projects");

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8172));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8184));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8187));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8190));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8193));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(8195));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(2596));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 25, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(2603));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 30, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(2607));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(3952));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(3957));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(3961));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(3964));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(4029));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 412, DateTimeKind.Utc).AddTicks(4033));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3288));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3296));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 21, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3299));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3303));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3306));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 14, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3309));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3312));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3315));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(3410));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5190));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5196));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5199));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5201));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5204));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5207));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5209));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(5212));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 20, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9468));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 25, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9475));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 30, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9478));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 4, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9480));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 9, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9482));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 10, 15, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9484));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 1, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9486));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 7, 18, 41, 12, 421, DateTimeKind.Utc).AddTicks(9488));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 5, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5482));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5491));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 15, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5495));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5497));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 25, 18, 41, 12, 420, DateTimeKind.Utc).AddTicks(5538));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9763));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9771));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9774));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9776));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 20, 18, 41, 12, 411, DateTimeKind.Utc).AddTicks(9779));
        }
    }
}

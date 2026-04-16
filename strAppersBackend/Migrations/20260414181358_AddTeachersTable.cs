using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTeachersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teachers_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 18, 13, 57, 522, DateTimeKind.Utc).AddTicks(2682));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 18, 13, 57, 522, DateTimeKind.Utc).AddTicks(2688));

            migrationBuilder.UpdateData(
                table: "Institutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 18, 13, 57, 480, DateTimeKind.Utc).AddTicks(8406));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 480, DateTimeKind.Utc).AddTicks(4484));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 480, DateTimeKind.Utc).AddTicks(4500));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 480, DateTimeKind.Utc).AddTicks(4502));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 480, DateTimeKind.Utc).AddTicks(4505));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 480, DateTimeKind.Utc).AddTicks(4511));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 480, DateTimeKind.Utc).AddTicks(4513));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(8630));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(8637));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 23, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(8645));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(9762));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(9766));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(9769));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(9771));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(9774));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(9776));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 18, 13, 57, 497, DateTimeKind.Utc).AddTicks(1840));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 18, 13, 57, 497, DateTimeKind.Utc).AddTicks(1850));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 14, 18, 13, 57, 497, DateTimeKind.Utc).AddTicks(1853));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 30, 18, 13, 57, 497, DateTimeKind.Utc).AddTicks(1856));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 4, 18, 13, 57, 497, DateTimeKind.Utc).AddTicks(1881));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 18, 13, 57, 497, DateTimeKind.Utc).AddTicks(1884));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 18, 13, 57, 497, DateTimeKind.Utc).AddTicks(1887));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 13, 18, 13, 57, 497, DateTimeKind.Utc).AddTicks(1890));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 18, 13, 57, 497, DateTimeKind.Utc).AddTicks(1893));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 15, 18, 13, 57, 498, DateTimeKind.Utc).AddTicks(2518));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 20, 18, 13, 57, 498, DateTimeKind.Utc).AddTicks(2526));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 25, 18, 13, 57, 498, DateTimeKind.Utc).AddTicks(2529));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 30, 18, 13, 57, 498, DateTimeKind.Utc).AddTicks(2531));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 4, 18, 13, 57, 498, DateTimeKind.Utc).AddTicks(2585));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 10, 18, 13, 57, 498, DateTimeKind.Utc).AddTicks(2588));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 27, 18, 13, 57, 498, DateTimeKind.Utc).AddTicks(2590));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 2, 18, 13, 57, 498, DateTimeKind.Utc).AddTicks(2592));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 28, 18, 13, 57, 496, DateTimeKind.Utc).AddTicks(5665));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 18, 13, 57, 496, DateTimeKind.Utc).AddTicks(5675));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 10, 18, 13, 57, 496, DateTimeKind.Utc).AddTicks(5679));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 18, 13, 57, 496, DateTimeKind.Utc).AddTicks(5682));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 20, 18, 13, 57, 496, DateTimeKind.Utc).AddTicks(5686));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 18, 13, 57, 514, DateTimeKind.Utc).AddTicks(1835));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 18, 13, 57, 514, DateTimeKind.Utc).AddTicks(1839));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 18, 13, 57, 514, DateTimeKind.Utc).AddTicks(1841));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 18, 13, 57, 514, DateTimeKind.Utc).AddTicks(1843));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(6055));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(6067));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(6070));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(6072));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 18, 13, 57, 481, DateTimeKind.Utc).AddTicks(6074));

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_Email",
                table: "Teachers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_InstituteId",
                table: "Teachers",
                column: "InstituteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Teachers");

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
                column: "CreatedAt",
                value: new DateTime(2026, 2, 28, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6586));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6604));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 10, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6608));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6611));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 20, 16, 40, 39, 594, DateTimeKind.Utc).AddTicks(6615));

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
    }
}

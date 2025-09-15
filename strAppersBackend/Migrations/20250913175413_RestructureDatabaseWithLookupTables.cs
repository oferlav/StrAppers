using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class RestructureDatabaseWithLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropColumn(
                name: "Major",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Students");

            migrationBuilder.AddColumn<int>(
                name: "MajorId",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "YearId",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Majors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Department = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Majors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Years",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Years", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Majors",
                columns: new[] { "Id", "CreatedAt", "Department", "Description", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5489), "Computer Science", "Study of computational systems and design", true, "Computer Science", null },
                    { 2, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5501), "Computer Science", "Engineering approach to software development", true, "Software Engineering", null },
                    { 3, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5504), "Computer Science", "Extracting insights from data", true, "Data Science", null },
                    { 4, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5506), "Computer Science", "Protecting digital systems and data", true, "Cybersecurity", null },
                    { 5, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5509), "Information Systems", "Management and use of technology", true, "Information Technology", null },
                    { 6, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5511), "Business", "General business management", true, "Business Administration", null }
                });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(8090));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 20, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(8099));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(8102));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(9081));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(9086));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(9089));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(9092));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(9095));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(9098));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 14, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3257), new DateTime(2025, 10, 13, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3252), new DateTime(2025, 8, 14, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3244) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 24, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3264), new DateTime(2025, 11, 12, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3262), new DateTime(2025, 8, 24, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3261) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 15, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3270), new DateTime(2025, 9, 3, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3268), new DateTime(2025, 6, 15, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3267) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 29, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3275), new DateTime(2025, 10, 28, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3273), new DateTime(2025, 8, 29, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3272) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 3, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3278), new DateTime(2025, 9, 3, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3277) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 8, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3283), new DateTime(2025, 11, 12, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3282), new DateTime(2025, 9, 8, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3281) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3288), new DateTime(2025, 12, 12, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3286), new DateTime(2025, 9, 10, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3285) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 12, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3292), new DateTime(2025, 11, 27, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3291), new DateTime(2025, 9, 12, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3290) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 11, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3297), new DateTime(2026, 1, 11, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3296), new DateTime(2025, 9, 11, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(3295) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(4450));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(4455));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(4458));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(4460));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(4463));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(4465));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(4467));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(4470));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 14, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(8239));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 19, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(8247));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 24, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(8250));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 29, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(8252));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 3, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(8255));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 9, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(8257));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 26, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(8259));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 1, 17, 54, 12, 981, DateTimeKind.Utc).AddTicks(8262));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "MajorId", "YearId" },
                values: new object[] { new DateTime(2025, 7, 30, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8885), 1, 3 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "MajorId", "YearId" },
                values: new object[] { new DateTime(2025, 8, 4, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8895), 2, 4 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "MajorId", "YearId" },
                values: new object[] { new DateTime(2025, 8, 9, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8900), 3, 5 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "MajorId", "YearId" },
                values: new object[] { new DateTime(2025, 8, 14, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8905), 4, 2 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "MajorId", "YearId" },
                values: new object[] { new DateTime(2025, 8, 19, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8908), 1, 1 });

            migrationBuilder.InsertData(
                table: "Years",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6734), "First year of study", true, "Freshman", 1, null },
                    { 2, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6739), "Second year of study", true, "Sophomore", 2, null },
                    { 3, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6742), "Third year of study", true, "Junior", 3, null },
                    { 4, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6744), "Fourth year of study", true, "Senior", 4, null },
                    { 5, new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6746), "Graduate level study", true, "Graduate", 5, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_MajorId",
                table: "Students",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_YearId",
                table: "Students",
                column: "YearId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Majors_MajorId",
                table: "Students",
                column: "MajorId",
                principalTable: "Majors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Years_YearId",
                table: "Students",
                column: "YearId",
                principalTable: "Years",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Majors_MajorId",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Years_YearId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "Majors");

            migrationBuilder.DropTable(
                name: "Years");

            migrationBuilder.DropIndex(
                name: "IX_Students_MajorId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_YearId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MajorId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "YearId",
                table: "Students");

            migrationBuilder.AddColumn<string>(
                name: "Major",
                table: "Students",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Year",
                table: "Students",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(8645));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 20, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(8652));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(8656));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(9690));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(9696));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(9699));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(9701));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(9704));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(9706));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 14, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(423), new DateTime(2025, 10, 13, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(415), new DateTime(2025, 8, 14, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(408) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 24, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(433), new DateTime(2025, 11, 12, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(432), new DateTime(2025, 8, 24, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(431) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 15, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(439), new DateTime(2025, 9, 3, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(437), new DateTime(2025, 6, 15, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(436) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 29, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(444), new DateTime(2025, 10, 28, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(442), new DateTime(2025, 8, 29, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(442) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 3, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(447), new DateTime(2025, 9, 3, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(446) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 8, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(452), new DateTime(2025, 11, 12, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(451), new DateTime(2025, 9, 8, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(450) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(457), new DateTime(2025, 12, 12, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(455), new DateTime(2025, 9, 10, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(454) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 12, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(461), new DateTime(2025, 11, 27, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(460), new DateTime(2025, 9, 12, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(459) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 11, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(466), new DateTime(2026, 1, 11, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(464), new DateTime(2025, 9, 11, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(463) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(1609));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(1615));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(1617));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(1620));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(1622));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(1624));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(1627));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(1629));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 14, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(5338));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 19, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(5345));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 24, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(5347));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 29, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(5350));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 3, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(5353));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 9, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(5355));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 26, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(5357));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 1, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(5360));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Major", "Year" },
                values: new object[] { new DateTime(2025, 7, 30, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6040), "Computer Science", "Junior" });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "Major", "Year" },
                values: new object[] { new DateTime(2025, 8, 4, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6050), "Software Engineering", "Senior" });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "Major", "Year" },
                values: new object[] { new DateTime(2025, 8, 9, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6054), "Data Science", "Graduate" });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "Major", "Year" },
                values: new object[] { new DateTime(2025, 8, 14, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6057), "Cybersecurity", "Sophomore" });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "Major", "Year" },
                values: new object[] { new DateTime(2025, 8, 19, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6060), "Computer Science", "Freshman" });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 8, 14, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6067), "john.doe@example.com", "John Doe" },
                    { 2, new DateTime(2025, 8, 19, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6082), "jane.smith@example.com", "Jane Smith" },
                    { 3, new DateTime(2025, 8, 24, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6084), "bob.johnson@example.com", "Bob Johnson" },
                    { 4, new DateTime(2025, 8, 29, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6086), "alice.brown@example.com", "Alice Brown" },
                    { 5, new DateTime(2025, 9, 3, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6194), "charlie.wilson@example.com", "Charlie Wilson" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }
    }
}

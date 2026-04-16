using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutesAndStudentInstituteId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstituteId",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Institutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Website = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TermsUse = table.Column<string>(type: "text", nullable: true),
                    TermsAccepted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TermsAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutes", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 10, 8, 369, DateTimeKind.Utc).AddTicks(443));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 10, 8, 369, DateTimeKind.Utc).AddTicks(449));

            migrationBuilder.InsertData(
                table: "Institutes",
                columns: new[] { "Id", "Address", "ContactEmail", "Country", "CreatedAt", "Description", "IsActive", "Logo", "Name", "PasswordHash", "Phone", "State", "TermsAcceptedAt", "TermsUse", "Type", "UpdatedAt", "Website" },
                values: new object[] { 1, "42 Innovation Way, Suite 100", "contact@strappers-academy.example.org", "Israel", new DateTime(2026, 3, 15, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(3940), "Technology-focused institute and applied learning campus.", true, null, "StrAppers Academy of Technology", null, "555-2100", "Tel Aviv District", null, null, "Institute", null, "https://academy.strappers.example.org" });

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(198));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(216));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(219));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(222));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(224));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(226));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(8126));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 18, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(8139));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 23, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(8143));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(9373));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(9379));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(9382));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(9384));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(9387));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 5, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(9390));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 15, 8, 10, 8, 347, DateTimeKind.Utc).AddTicks(2581));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 25, 8, 10, 8, 347, DateTimeKind.Utc).AddTicks(2597));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 14, 8, 10, 8, 347, DateTimeKind.Utc).AddTicks(2601));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 30, 8, 10, 8, 347, DateTimeKind.Utc).AddTicks(2604));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 4, 8, 10, 8, 347, DateTimeKind.Utc).AddTicks(2631));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 8, 10, 8, 347, DateTimeKind.Utc).AddTicks(2634));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 8, 10, 8, 347, DateTimeKind.Utc).AddTicks(2637));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 13, 8, 10, 8, 347, DateTimeKind.Utc).AddTicks(2640));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 8, 10, 8, 347, DateTimeKind.Utc).AddTicks(2643));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 15, 8, 10, 8, 348, DateTimeKind.Utc).AddTicks(3316));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 20, 8, 10, 8, 348, DateTimeKind.Utc).AddTicks(3330));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 25, 8, 10, 8, 348, DateTimeKind.Utc).AddTicks(3333));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 30, 8, 10, 8, 348, DateTimeKind.Utc).AddTicks(3335));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 4, 8, 10, 8, 348, DateTimeKind.Utc).AddTicks(3337));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 10, 8, 10, 8, 348, DateTimeKind.Utc).AddTicks(3339));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 27, 8, 10, 8, 348, DateTimeKind.Utc).AddTicks(3342));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 2, 8, 10, 8, 348, DateTimeKind.Utc).AddTicks(3344));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "InstituteId" },
                values: new object[] { new DateTime(2026, 2, 28, 8, 10, 8, 346, DateTimeKind.Utc).AddTicks(6058), null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "InstituteId" },
                values: new object[] { new DateTime(2026, 3, 5, 8, 10, 8, 346, DateTimeKind.Utc).AddTicks(6081), null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "InstituteId" },
                values: new object[] { new DateTime(2026, 3, 10, 8, 10, 8, 346, DateTimeKind.Utc).AddTicks(6086), null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "InstituteId" },
                values: new object[] { new DateTime(2026, 3, 15, 8, 10, 8, 346, DateTimeKind.Utc).AddTicks(6091), null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "InstituteId" },
                values: new object[] { new DateTime(2026, 3, 20, 8, 10, 8, 346, DateTimeKind.Utc).AddTicks(6095), null });

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 10, 8, 361, DateTimeKind.Utc).AddTicks(4122));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 10, 8, 361, DateTimeKind.Utc).AddTicks(4127));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 10, 8, 361, DateTimeKind.Utc).AddTicks(4129));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 14, 8, 10, 8, 361, DateTimeKind.Utc).AddTicks(4131));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(5282));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(5288));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(5291));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(5294));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 13, 8, 10, 8, 331, DateTimeKind.Utc).AddTicks(5296));

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstituteId",
                table: "Students",
                column: "InstituteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Institutes_InstituteId",
                table: "Students",
                column: "InstituteId",
                principalTable: "Institutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Institutes_InstituteId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "Institutes");

            migrationBuilder.DropIndex(
                name: "IX_Students_InstituteId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InstituteId",
                table: "Students");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 15, 27, 21, 322, DateTimeKind.Utc).AddTicks(5350));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 15, 27, 21, 322, DateTimeKind.Utc).AddTicks(5357));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 289, DateTimeKind.Utc).AddTicks(9821));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 289, DateTimeKind.Utc).AddTicks(9834));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 289, DateTimeKind.Utc).AddTicks(9837));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 289, DateTimeKind.Utc).AddTicks(9839));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 289, DateTimeKind.Utc).AddTicks(9842));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 289, DateTimeKind.Utc).AddTicks(9844));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(4067));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(4074));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 21, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(4078));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(5381));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(5387));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(5390));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(5393));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(5395));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(5398));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 15, 27, 21, 304, DateTimeKind.Utc).AddTicks(4859));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 23, 15, 27, 21, 304, DateTimeKind.Utc).AddTicks(4871));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 12, 15, 27, 21, 304, DateTimeKind.Utc).AddTicks(4875));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 15, 27, 21, 304, DateTimeKind.Utc).AddTicks(4878));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 15, 27, 21, 304, DateTimeKind.Utc).AddTicks(4881));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 15, 27, 21, 304, DateTimeKind.Utc).AddTicks(4902));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 15, 27, 21, 304, DateTimeKind.Utc).AddTicks(4906));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 15, 27, 21, 304, DateTimeKind.Utc).AddTicks(4909));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 10, 15, 27, 21, 304, DateTimeKind.Utc).AddTicks(4912));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 13, 15, 27, 21, 305, DateTimeKind.Utc).AddTicks(5109));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 18, 15, 27, 21, 305, DateTimeKind.Utc).AddTicks(5117));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 23, 15, 27, 21, 305, DateTimeKind.Utc).AddTicks(5123));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 28, 15, 27, 21, 305, DateTimeKind.Utc).AddTicks(5126));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 2, 15, 27, 21, 305, DateTimeKind.Utc).AddTicks(5128));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 8, 15, 27, 21, 305, DateTimeKind.Utc).AddTicks(5131));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 25, 15, 27, 21, 305, DateTimeKind.Utc).AddTicks(5133));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 31, 15, 27, 21, 305, DateTimeKind.Utc).AddTicks(5135));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 15, 27, 21, 303, DateTimeKind.Utc).AddTicks(8323));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 15, 27, 21, 303, DateTimeKind.Utc).AddTicks(8337));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 8, 15, 27, 21, 303, DateTimeKind.Utc).AddTicks(8342));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 15, 27, 21, 303, DateTimeKind.Utc).AddTicks(8345));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 18, 15, 27, 21, 303, DateTimeKind.Utc).AddTicks(8350));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 15, 27, 21, 317, DateTimeKind.Utc).AddTicks(4947));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 15, 27, 21, 317, DateTimeKind.Utc).AddTicks(4953));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 15, 27, 21, 317, DateTimeKind.Utc).AddTicks(4957));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 12, 15, 27, 21, 317, DateTimeKind.Utc).AddTicks(4959));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(1288));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(1292));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(1295));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(1298));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 15, 27, 21, 290, DateTimeKind.Utc).AddTicks(1300));
        }
    }
}

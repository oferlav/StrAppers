using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectStatusTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Projects");

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProjectStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStatuses", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(3100));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 20, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(3105));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(3109));

            migrationBuilder.InsertData(
                table: "ProjectStatuses",
                columns: new[] { "Id", "Color", "CreatedAt", "Description", "IsActive", "Name", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "#10B981", new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4073), "Newly created project", true, "New", 1, null },
                    { 2, "#3B82F6", new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4079), "Project in planning phase", true, "Planning", 2, null },
                    { 3, "#F59E0B", new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4081), "Project currently being worked on", true, "In Progress", 3, null },
                    { 4, "#EF4444", new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4084), "Project temporarily paused", true, "On Hold", 4, null },
                    { 5, "#059669", new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4086), "Project successfully completed", true, "Completed", 5, null },
                    { 6, "#6B7280", new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4089), "Project cancelled or abandoned", true, "Cancelled", 6, null }
                });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 8, 14, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4238), new DateTime(2025, 10, 13, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4236), new DateTime(2025, 8, 14, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4222), 3 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 8, 24, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4245), new DateTime(2025, 11, 12, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4244), new DateTime(2025, 8, 24, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4243), 2 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 6, 15, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4251), new DateTime(2025, 9, 3, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4249), new DateTime(2025, 6, 15, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4248), 5 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 8, 29, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4299), new DateTime(2025, 10, 28, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4298), new DateTime(2025, 8, 29, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4297), 3 });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "StartDate", "StatusId" },
                values: new object[] { new DateTime(2025, 9, 3, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4303), new DateTime(2025, 9, 3, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4302), 4 });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(5431));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(5435));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(5438));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(5441));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(5443));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(5445));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(5447));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(5450));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 14, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(9273));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 19, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(9279));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 24, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(9282));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 29, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(9284));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 3, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(9286));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 9, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(9289));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 26, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(9291));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 1, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(9294));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 30, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(9768));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(9778));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 9, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(9781));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(9784));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 19, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(9787));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(1543));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 19, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(1552));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 24, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(1554));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 29, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(1556));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 3, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(1558));

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "CreatedAt", "Description", "DueDate", "EndDate", "OrganizationId", "Priority", "StartDate", "StatusId", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 6, new DateTime(2025, 9, 8, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4307), "Mobile application for students to discover and register for campus events", new DateTime(2025, 11, 12, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4306), null, 1, "Medium", new DateTime(2025, 9, 8, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4305), 1, "Mobile App for Campus Events", null },
                    { 7, new DateTime(2025, 9, 10, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4312), "VR environment for immersive learning experiences", new DateTime(2025, 12, 12, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4311), null, 2, "High", new DateTime(2025, 9, 10, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4310), 1, "Virtual Reality Learning Lab", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_StatusId",
                table: "Projects",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ProjectStatuses_StatusId",
                table: "Projects",
                column: "StatusId",
                principalTable: "ProjectStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectStatuses_StatusId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectStatuses");

            migrationBuilder.DropIndex(
                name: "IX_Projects_StatusId",
                table: "Projects");

            migrationBuilder.DeleteData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(8547));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 20, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(8557));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(8560));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "StartDate", "Status" },
                values: new object[] { new DateTime(2025, 8, 14, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9214), new DateTime(2025, 10, 13, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9208), new DateTime(2025, 8, 14, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9201), "In Progress" });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "StartDate", "Status" },
                values: new object[] { new DateTime(2025, 8, 24, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9221), new DateTime(2025, 11, 12, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9219), new DateTime(2025, 8, 24, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9218), "Planning" });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "StartDate", "Status" },
                values: new object[] { new DateTime(2025, 6, 15, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9225), new DateTime(2025, 9, 3, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9224), new DateTime(2025, 6, 15, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9223), "Completed" });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "StartDate", "Status" },
                values: new object[] { new DateTime(2025, 8, 29, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9280), new DateTime(2025, 10, 28, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9278), new DateTime(2025, 8, 29, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9277), "In Progress" });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "StartDate", "Status" },
                values: new object[] { new DateTime(2025, 9, 3, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9284), new DateTime(2025, 9, 3, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9283), "On Hold" });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(620));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(624));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(627));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(629));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(631));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(634));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(636));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(638));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 14, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6496));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 19, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6504));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 24, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6507));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 29, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6509));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 3, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6511));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 9, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6513));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 26, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6516));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 1, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6518));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 30, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5406));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5414));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 9, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5417));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5420));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 19, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5423));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6555));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 19, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6567));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 24, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6569));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 29, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6571));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 3, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6573));
        }
    }
}

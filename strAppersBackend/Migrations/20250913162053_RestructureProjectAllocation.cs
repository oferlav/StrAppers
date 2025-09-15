using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class RestructureProjectAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentProjects");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAdmin",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(5038));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 20, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(5089));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(5093));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(7701));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(7710));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(7714));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(7716));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(7720));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(7726));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "HasAdmin", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 14, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9474), new DateTime(2025, 10, 13, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9471), true, new DateTime(2025, 8, 14, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9464) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "HasAdmin", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 24, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9482), new DateTime(2025, 11, 12, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9480), false, new DateTime(2025, 8, 24, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9479) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "HasAdmin", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 15, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9487), new DateTime(2025, 9, 3, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9485), true, new DateTime(2025, 6, 15, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9484) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "HasAdmin", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 29, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9492), new DateTime(2025, 10, 28, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9491), false, new DateTime(2025, 8, 29, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9490) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "HasAdmin", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 3, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9496), false, new DateTime(2025, 9, 3, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9495) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "HasAdmin", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 8, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9501), new DateTime(2025, 11, 12, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9499), false, new DateTime(2025, 9, 8, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9498) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "HasAdmin", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9505), new DateTime(2025, 12, 12, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9504), false, new DateTime(2025, 9, 10, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9503) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(576));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(581));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(584));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(586));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(589));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(592));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(594));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(596));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 14, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(4538));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 19, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(4548));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 24, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(4551));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 29, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(4605));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 3, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(4608));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 9, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(4610));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 8, 26, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(4612));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 9, 1, 16, 20, 53, 31, DateTimeKind.Utc).AddTicks(4615));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "IsAdmin", "ProjectId" },
                values: new object[] { new DateTime(2025, 7, 30, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5121), true, 1 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "IsAdmin", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 4, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5129), false, 2 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "IsAdmin", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 9, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5133), true, 3 });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "IsAdmin", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 14, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5136), false, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "IsAdmin", "ProjectId" },
                values: new object[] { new DateTime(2025, 8, 19, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5140), false, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(3491));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 19, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(3501));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 24, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(3504));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 29, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(3506));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 3, 16, 20, 53, 29, DateTimeKind.Utc).AddTicks(3508));

            migrationBuilder.CreateIndex(
                name: "IX_Students_ProjectId",
                table: "Students",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Projects_ProjectId",
                table: "Students",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Projects_ProjectId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_ProjectId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "HasAdmin",
                table: "Projects");

            migrationBuilder.CreateTable(
                name: "StudentProjects",
                columns: table => new
                {
                    ProjectsId = table.Column<int>(type: "integer", nullable: false),
                    StudentsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProjects", x => new { x.ProjectsId, x.StudentsId });
                    table.ForeignKey(
                        name: "FK_StudentProjects_Projects_ProjectsId",
                        column: x => x.ProjectsId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentProjects_Students_StudentsId",
                        column: x => x.StudentsId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4073));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4079));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4081));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4084));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4086));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 15, 51, 17, 762, DateTimeKind.Utc).AddTicks(4089));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 14, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4238), new DateTime(2025, 10, 13, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4236), new DateTime(2025, 8, 14, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4222) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 24, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4245), new DateTime(2025, 11, 12, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4244), new DateTime(2025, 8, 24, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4243) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 15, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4251), new DateTime(2025, 9, 3, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4249), new DateTime(2025, 6, 15, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4248) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 29, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4299), new DateTime(2025, 10, 28, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4298), new DateTime(2025, 8, 29, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4297) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 3, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4303), new DateTime(2025, 9, 3, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4302) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 8, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4307), new DateTime(2025, 11, 12, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4306), new DateTime(2025, 9, 8, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4305) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4312), new DateTime(2025, 12, 12, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4311), new DateTime(2025, 9, 10, 15, 51, 17, 763, DateTimeKind.Utc).AddTicks(4310) });

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

            migrationBuilder.CreateIndex(
                name: "IX_StudentProjects_StudentsId",
                table: "StudentProjects",
                column: "StudentsId");
        }
    }
}

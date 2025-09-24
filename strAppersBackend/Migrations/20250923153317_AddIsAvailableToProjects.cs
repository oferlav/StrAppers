using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAvailableToProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EndDate",
                table: "StudentRoles",
                newName: "UpdatedAt");

            migrationBuilder.AddColumn<string>(
                name: "BoardId",
                table: "Students",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "StudentRoles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "ProjectStatuses",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7);

            migrationBuilder.AddColumn<string>(
                name: "ExtendedDescription",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemDesign",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SystemDesignDoc",
                table: "Projects",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DesignVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    DesignDocument = table.Column<string>(type: "TEXT", nullable: false),
                    DesignDocumentPdf = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignVersions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectBoards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    StatusId = table.Column<int>(type: "integer", nullable: true),
                    HasAdmin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ProjectStatusId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBoards_ProjectStatuses_ProjectStatusId",
                        column: x => x.ProjectStatusId,
                        principalTable: "ProjectStatuses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectBoards_ProjectStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "ProjectStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectBoards_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2863));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2880));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2883));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2885));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2888));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(2890));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ContactPhone", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(6145) });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ContactPhone", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 7, 30, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(6153) });

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "ContactPhone", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 8, 4, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(6156) });

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7398));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7403));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7406));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7409));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7412));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(7415));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "IsAvailable", "StartDate", "SystemDesign", "SystemDesignDoc" },
                values: new object[] { new DateTime(2025, 8, 24, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9164), new DateTime(2025, 10, 23, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9159), null, true, new DateTime(2025, 8, 24, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9147), null, null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "IsAvailable", "StartDate", "SystemDesign", "SystemDesignDoc" },
                values: new object[] { new DateTime(2025, 9, 3, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9172), new DateTime(2025, 11, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9170), null, true, new DateTime(2025, 9, 3, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9169), null, null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "ExtendedDescription", "IsAvailable", "StartDate", "SystemDesign", "SystemDesignDoc" },
                values: new object[] { new DateTime(2025, 6, 25, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9177), new DateTime(2025, 9, 13, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9175), null, true, new DateTime(2025, 6, 25, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9174), null, null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "IsAvailable", "StartDate", "SystemDesign", "SystemDesignDoc" },
                values: new object[] { new DateTime(2025, 9, 8, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9181), new DateTime(2025, 11, 7, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9180), null, true, new DateTime(2025, 9, 8, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9179), null, null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "ExtendedDescription", "IsAvailable", "StartDate", "SystemDesign", "SystemDesignDoc" },
                values: new object[] { new DateTime(2025, 9, 13, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9185), null, true, new DateTime(2025, 9, 13, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9184), null, null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "IsAvailable", "StartDate", "SystemDesign", "SystemDesignDoc" },
                values: new object[] { new DateTime(2025, 9, 18, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9190), new DateTime(2025, 11, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9189), null, true, new DateTime(2025, 9, 18, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9187), null, null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "IsAvailable", "StartDate", "SystemDesign", "SystemDesignDoc" },
                values: new object[] { new DateTime(2025, 9, 20, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9195), new DateTime(2025, 12, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9193), null, true, new DateTime(2025, 9, 20, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9193), null, null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "IsAvailable", "StartDate", "SystemDesign", "SystemDesignDoc" },
                values: new object[] { new DateTime(2025, 9, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9200), new DateTime(2025, 12, 7, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9198), null, true, new DateTime(2025, 9, 22, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9197), null, null });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "IsAvailable", "StartDate", "SystemDesign", "SystemDesignDoc" },
                values: new object[] { new DateTime(2025, 9, 21, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9204), new DateTime(2026, 1, 21, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9203), null, true, new DateTime(2025, 9, 21, 15, 33, 16, 137, DateTimeKind.Utc).AddTicks(9202), null, null });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(811));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(816));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(819));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(821));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(824));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(826));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(828));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(830));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AssignedAt", "AssignedDate" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6034), new DateTime(2025, 8, 24, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6040) });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AssignedAt", "AssignedDate" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6045), new DateTime(2025, 8, 29, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6047) });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AssignedAt", "AssignedDate" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6048), new DateTime(2025, 9, 3, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6049) });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "AssignedAt", "AssignedDate" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6051), new DateTime(2025, 9, 8, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6052) });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "AssignedAt", "AssignedDate" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6053), new DateTime(2025, 9, 13, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6054) });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "AssignedAt", "AssignedDate" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6056), new DateTime(2025, 8, 19, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6057) });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "AssignedAt", "AssignedDate" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6058), new DateTime(2025, 9, 5, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6060) });

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "AssignedAt", "AssignedDate" },
                values: new object[] { new DateTime(2025, 9, 23, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6061), new DateTime(2025, 9, 11, 15, 33, 16, 138, DateTimeKind.Utc).AddTicks(6062) });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BoardId", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 8, 9, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9973) });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "BoardId", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 8, 14, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9985) });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "BoardId", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 8, 19, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9989) });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "BoardId", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 8, 24, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9992) });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "BoardId", "CreatedAt" },
                values: new object[] { null, new DateTime(2025, 8, 29, 15, 33, 16, 136, DateTimeKind.Utc).AddTicks(9995) });

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4430));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4435));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4438));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4441));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 25, 15, 33, 16, 134, DateTimeKind.Utc).AddTicks(4443));

            migrationBuilder.CreateIndex(
                name: "IX_Students_BoardId",
                table: "Students",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignVersions_IsActive",
                table: "DesignVersions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DesignVersions_ProjectId",
                table: "DesignVersions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignVersions_VersionNumber",
                table: "DesignVersions",
                column: "VersionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_CreatedAt",
                table: "ProjectBoards",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_ProjectId",
                table: "ProjectBoards",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_ProjectStatusId",
                table: "ProjectBoards",
                column: "ProjectStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_StatusId",
                table: "ProjectBoards",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_ProjectBoards_BoardId",
                table: "Students",
                column: "BoardId",
                principalTable: "ProjectBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_ProjectBoards_BoardId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "DesignVersions");

            migrationBuilder.DropTable(
                name: "ProjectBoards");

            migrationBuilder.DropIndex(
                name: "IX_Students_BoardId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "StudentRoles");

            migrationBuilder.DropColumn(
                name: "ExtendedDescription",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SystemDesign",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SystemDesignDoc",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Organizations");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "StudentRoles",
                newName: "EndDate");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "ProjectStatuses",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7,
                oldNullable: true);

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
    }
}

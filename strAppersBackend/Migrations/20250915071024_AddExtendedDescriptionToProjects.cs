using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedDescriptionToProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtendedDescription",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JoinRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    StudentEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StudentFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StudentLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    JoinDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Added = table.Column<bool>(type: "boolean", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 16, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(341), new DateTime(2025, 10, 15, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(337), null, new DateTime(2025, 8, 16, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(326) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 26, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(347), new DateTime(2025, 11, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(345), null, new DateTime(2025, 8, 26, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(344) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 17, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(352), new DateTime(2025, 9, 5, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(351), null, new DateTime(2025, 6, 17, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(350) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 31, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(357), new DateTime(2025, 10, 30, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(355), null, new DateTime(2025, 8, 31, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(354) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 5, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(360), null, new DateTime(2025, 9, 5, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(359) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(365), new DateTime(2025, 11, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(363), null, new DateTime(2025, 9, 10, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(362) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 12, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(370), new DateTime(2025, 12, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(368), null, new DateTime(2025, 9, 12, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(367) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(377), new DateTime(2025, 11, 29, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(373), null, new DateTime(2025, 9, 14, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(372) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DueDate", "ExtendedDescription", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 13, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(382), new DateTime(2026, 1, 13, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(380), null, new DateTime(2025, 9, 13, 7, 10, 23, 512, DateTimeKind.Utc).AddTicks(380) });

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

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_Added",
                table: "JoinRequests",
                column: "Added");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_ChannelId",
                table: "JoinRequests",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_JoinDate",
                table: "JoinRequests",
                column: "JoinDate");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_ProjectId",
                table: "JoinRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_StudentEmail",
                table: "JoinRequests",
                column: "StudentEmail");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_StudentId",
                table: "JoinRequests",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JoinRequests");

            migrationBuilder.DropColumn(
                name: "ExtendedDescription",
                table: "Projects");

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5489));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5501));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5504));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5506));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5509));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(5511));

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
                column: "CreatedAt",
                value: new DateTime(2025, 7, 30, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8885));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8895));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 9, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8900));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8905));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 19, 17, 54, 12, 980, DateTimeKind.Utc).AddTicks(8908));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6734));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6739));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6742));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6744));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 7, 15, 17, 54, 12, 979, DateTimeKind.Utc).AddTicks(6746));
        }
    }
}

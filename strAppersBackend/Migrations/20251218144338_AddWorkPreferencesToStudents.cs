using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPreferencesToStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CV",
                table: "Students",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FreelanceWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FullTimeWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HomeWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HybridWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinutesToWork",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MultilingualWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NightShiftWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PartTimeWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RelocationWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StudentWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionTypeId",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TravelWork",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "Observed",
                table: "ProjectBoards",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.CreateTable(
                name: "BoardMeetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MeetingTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StudentEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CustomMeetingUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ActualMeetingUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Attended = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    JoinTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardMeetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardMeetings_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubscriptionTypeId = table.Column<int>(type: "integer", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employers_Subscriptions_SubscriptionTypeId",
                        column: x => x.SubscriptionTypeId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployerAdds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployerId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    JobDescription = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerAdds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployerAdds_Employers_EmployerId",
                        column: x => x.EmployerId,
                        principalTable: "Employers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployerAdds_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployerBoards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployerId = table.Column<int>(type: "integer", nullable: false),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Observed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MeetRequest = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployerBoards_Employers_EmployerId",
                        column: x => x.EmployerId,
                        principalTable: "Employers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployerBoards_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployerCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployerId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployerCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployerCandidates_Employers_EmployerId",
                        column: x => x.EmployerId,
                        principalTable: "Employers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployerCandidates_Students_StudentId",
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
                value: new DateTime(2025, 10, 19, 14, 43, 35, 656, DateTimeKind.Utc).AddTicks(9101));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 656, DateTimeKind.Utc).AddTicks(9124));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 656, DateTimeKind.Utc).AddTicks(9127));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 656, DateTimeKind.Utc).AddTicks(9129));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 656, DateTimeKind.Utc).AddTicks(9131));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 656, DateTimeKind.Utc).AddTicks(9134));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(3757));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 24, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(3767));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 29, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(3770));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(5057));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(5065));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(5068));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(5071));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(5073));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 8, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(5076));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 14, 43, 35, 673, DateTimeKind.Utc).AddTicks(5739));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 28, 14, 43, 35, 673, DateTimeKind.Utc).AddTicks(5750));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 19, 14, 43, 35, 673, DateTimeKind.Utc).AddTicks(5754));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 3, 14, 43, 35, 673, DateTimeKind.Utc).AddTicks(5757));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 8, 14, 43, 35, 673, DateTimeKind.Utc).AddTicks(5761));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 13, 14, 43, 35, 673, DateTimeKind.Utc).AddTicks(5780));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 15, 14, 43, 35, 673, DateTimeKind.Utc).AddTicks(5783));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 17, 14, 43, 35, 673, DateTimeKind.Utc).AddTicks(5787));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 12, 16, 14, 43, 35, 673, DateTimeKind.Utc).AddTicks(5790));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 18, 14, 43, 35, 674, DateTimeKind.Utc).AddTicks(2793));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 23, 14, 43, 35, 674, DateTimeKind.Utc).AddTicks(2802));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 28, 14, 43, 35, 674, DateTimeKind.Utc).AddTicks(2808));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 3, 14, 43, 35, 674, DateTimeKind.Utc).AddTicks(2810));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 8, 14, 43, 35, 674, DateTimeKind.Utc).AddTicks(2812));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 13, 14, 43, 35, 674, DateTimeKind.Utc).AddTicks(2815));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2025, 11, 30, 14, 43, 35, 674, DateTimeKind.Utc).AddTicks(2817));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2025, 12, 6, 14, 43, 35, 674, DateTimeKind.Utc).AddTicks(2820));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CV", "CreatedAt", "MinutesToWork", "SubscriptionTypeId" },
                values: new object[] { null, new DateTime(2025, 11, 3, 14, 43, 35, 672, DateTimeKind.Utc).AddTicks(7906), null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CV", "CreatedAt", "MinutesToWork", "SubscriptionTypeId" },
                values: new object[] { null, new DateTime(2025, 11, 8, 14, 43, 35, 672, DateTimeKind.Utc).AddTicks(7926), null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CV", "CreatedAt", "MinutesToWork", "SubscriptionTypeId" },
                values: new object[] { null, new DateTime(2025, 11, 13, 14, 43, 35, 672, DateTimeKind.Utc).AddTicks(7930), null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CV", "CreatedAt", "MinutesToWork", "SubscriptionTypeId" },
                values: new object[] { null, new DateTime(2025, 11, 18, 14, 43, 35, 672, DateTimeKind.Utc).AddTicks(7933), null, null });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CV", "CreatedAt", "MinutesToWork", "SubscriptionTypeId" },
                values: new object[] { null, new DateTime(2025, 11, 23, 14, 43, 35, 672, DateTimeKind.Utc).AddTicks(7936), null, null });

            migrationBuilder.InsertData(
                table: "Subscriptions",
                columns: new[] { "Id", "CreatedAt", "Description", "Price", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 12, 18, 14, 43, 35, 679, DateTimeKind.Utc).AddTicks(4154), "Junior", 0m, null },
                    { 2, new DateTime(2025, 12, 18, 14, 43, 35, 679, DateTimeKind.Utc).AddTicks(4163), "Product", 0m, null },
                    { 3, new DateTime(2025, 12, 18, 14, 43, 35, 679, DateTimeKind.Utc).AddTicks(4166), "Enterprise A", 0m, null },
                    { 4, new DateTime(2025, 12, 18, 14, 43, 35, 679, DateTimeKind.Utc).AddTicks(4168), "Enterprise B", 0m, null }
                });

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(729));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(736));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(738));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(740));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 19, 14, 43, 35, 657, DateTimeKind.Utc).AddTicks(743));

            migrationBuilder.CreateIndex(
                name: "IX_Students_SubscriptionTypeId",
                table: "Students",
                column: "SubscriptionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_Attended",
                table: "BoardMeetings",
                column: "Attended");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_BoardId",
                table: "BoardMeetings",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_MeetingTime",
                table: "BoardMeetings",
                column: "MeetingTime");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_StudentEmail",
                table: "BoardMeetings",
                column: "StudentEmail");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerAdds_EmployerId",
                table: "EmployerAdds",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerAdds_RoleId",
                table: "EmployerAdds",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerBoards_BoardId",
                table: "EmployerBoards",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerBoards_EmployerId",
                table: "EmployerBoards",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerBoards_EmployerId_BoardId",
                table: "EmployerBoards",
                columns: new[] { "EmployerId", "BoardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployerCandidates_CreatedAt",
                table: "EmployerCandidates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerCandidates_EmployerId",
                table: "EmployerCandidates",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployerCandidates_EmployerId_StudentId",
                table: "EmployerCandidates",
                columns: new[] { "EmployerId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployerCandidates_StudentId",
                table: "EmployerCandidates",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employers_ContactEmail",
                table: "Employers",
                column: "ContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Employers_SubscriptionTypeId",
                table: "Employers",
                column: "SubscriptionTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Subscriptions_SubscriptionTypeId",
                table: "Students",
                column: "SubscriptionTypeId",
                principalTable: "Subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Subscriptions_SubscriptionTypeId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "BoardMeetings");

            migrationBuilder.DropTable(
                name: "EmployerAdds");

            migrationBuilder.DropTable(
                name: "EmployerBoards");

            migrationBuilder.DropTable(
                name: "EmployerCandidates");

            migrationBuilder.DropTable(
                name: "Employers");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Students_SubscriptionTypeId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "CV",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FreelanceWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FullTimeWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "HomeWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "HybridWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MinutesToWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MultilingualWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "NightShiftWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PartTimeWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "RelocationWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudentWork",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "SubscriptionTypeId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "TravelWork",
                table: "Students");

            migrationBuilder.AlterColumn<bool>(
                name: "Observed",
                table: "ProjectBoards",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

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
                column: "CreatedAt",
                value: new DateTime(2025, 10, 27, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4118));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 6, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4129));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 28, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4134));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 11, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4138));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4141));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 21, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4144));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 23, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4147));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 25, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4151));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 24, 12, 19, 52, 169, DateTimeKind.Utc).AddTicks(4155));

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
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddFigmaOAuthPendingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FigmaOAuthPending",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FigmaAccessToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FigmaRefreshToken = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FigmaTokenExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FigmaOAuthPending", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 36, 20, 358, DateTimeKind.Utc).AddTicks(8856));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 36, 20, 358, DateTimeKind.Utc).AddTicks(8862));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(4353));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(4370));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(4373));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(4376));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(4378));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(4381));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(8772));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(8778));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(8781));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 36, 20, 319, DateTimeKind.Utc).AddTicks(106));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 36, 20, 319, DateTimeKind.Utc).AddTicks(114));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 36, 20, 319, DateTimeKind.Utc).AddTicks(117));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 36, 20, 319, DateTimeKind.Utc).AddTicks(120));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 36, 20, 319, DateTimeKind.Utc).AddTicks(123));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 36, 20, 319, DateTimeKind.Utc).AddTicks(126));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 8, 8, 36, 20, 339, DateTimeKind.Utc).AddTicks(1012));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 18, 8, 36, 20, 339, DateTimeKind.Utc).AddTicks(1026));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 7, 8, 36, 20, 339, DateTimeKind.Utc).AddTicks(1030));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 23, 8, 36, 20, 339, DateTimeKind.Utc).AddTicks(1033));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 8, 36, 20, 339, DateTimeKind.Utc).AddTicks(1038));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 8, 36, 20, 339, DateTimeKind.Utc).AddTicks(1053));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 4, 8, 36, 20, 339, DateTimeKind.Utc).AddTicks(1057));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 6, 8, 36, 20, 339, DateTimeKind.Utc).AddTicks(1061));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 5, 8, 36, 20, 339, DateTimeKind.Utc).AddTicks(1117));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 8, 8, 36, 20, 340, DateTimeKind.Utc).AddTicks(3210));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 13, 8, 36, 20, 340, DateTimeKind.Utc).AddTicks(3219));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 18, 8, 36, 20, 340, DateTimeKind.Utc).AddTicks(3222));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 23, 8, 36, 20, 340, DateTimeKind.Utc).AddTicks(3226));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 28, 8, 36, 20, 340, DateTimeKind.Utc).AddTicks(3228));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 3, 8, 36, 20, 340, DateTimeKind.Utc).AddTicks(3231));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 20, 8, 36, 20, 340, DateTimeKind.Utc).AddTicks(3234));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 26, 8, 36, 20, 340, DateTimeKind.Utc).AddTicks(3236));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 21, 8, 36, 20, 338, DateTimeKind.Utc).AddTicks(4381));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 36, 20, 338, DateTimeKind.Utc).AddTicks(4440));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 8, 36, 20, 338, DateTimeKind.Utc).AddTicks(4444));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 8, 8, 36, 20, 338, DateTimeKind.Utc).AddTicks(4448));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 8, 36, 20, 338, DateTimeKind.Utc).AddTicks(4451));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 36, 20, 353, DateTimeKind.Utc).AddTicks(7709));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 36, 20, 353, DateTimeKind.Utc).AddTicks(7714));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 36, 20, 353, DateTimeKind.Utc).AddTicks(7717));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 36, 20, 353, DateTimeKind.Utc).AddTicks(7720));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(5807));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(5814));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(5817));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(5819));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 36, 20, 318, DateTimeKind.Utc).AddTicks(5822));

            migrationBuilder.CreateIndex(
                name: "IX_FigmaOAuthPending_Email",
                table: "FigmaOAuthPending",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FigmaOAuthPending");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 33, 34, 267, DateTimeKind.Utc).AddTicks(4818));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 33, 34, 267, DateTimeKind.Utc).AddTicks(4824));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 237, DateTimeKind.Utc).AddTicks(9810));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 237, DateTimeKind.Utc).AddTicks(9822));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 237, DateTimeKind.Utc).AddTicks(9825));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 237, DateTimeKind.Utc).AddTicks(9828));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 237, DateTimeKind.Utc).AddTicks(9831));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 237, DateTimeKind.Utc).AddTicks(9833));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(8273));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 11, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(8282));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 16, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(8285));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(9648));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(9653));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(9656));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(9659));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(9661));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(9664));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 8, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(8059));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 18, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(8067));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 7, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(8071));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 23, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(8074));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 28, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(8077));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 2, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(8101));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 4, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(8104));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 6, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(8107));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 5, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(8110));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 8, 8, 33, 34, 253, DateTimeKind.Utc).AddTicks(7366));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 13, 8, 33, 34, 253, DateTimeKind.Utc).AddTicks(7374));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 18, 8, 33, 34, 253, DateTimeKind.Utc).AddTicks(7377));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 23, 8, 33, 34, 253, DateTimeKind.Utc).AddTicks(7379));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 28, 8, 33, 34, 253, DateTimeKind.Utc).AddTicks(7382));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 3, 8, 33, 34, 253, DateTimeKind.Utc).AddTicks(7384));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 20, 8, 33, 34, 253, DateTimeKind.Utc).AddTicks(7386));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 26, 8, 33, 34, 253, DateTimeKind.Utc).AddTicks(7389));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 21, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(1867));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 26, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(1884));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 3, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(1888));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 8, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(1892));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 13, 8, 33, 34, 252, DateTimeKind.Utc).AddTicks(1896));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 33, 34, 262, DateTimeKind.Utc).AddTicks(5420));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 33, 34, 262, DateTimeKind.Utc).AddTicks(5425));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 33, 34, 262, DateTimeKind.Utc).AddTicks(5427));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 7, 8, 33, 34, 262, DateTimeKind.Utc).AddTicks(5428));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(1278));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(1284));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(1287));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(1289));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 6, 8, 33, 34, 238, DateTimeKind.Utc).AddTicks(1291));
        }
    }
}

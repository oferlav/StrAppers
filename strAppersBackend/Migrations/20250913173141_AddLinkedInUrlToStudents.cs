using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedInUrlToStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "CreatedAt", "Description", "DueDate", "EndDate", "HasAdmin", "OrganizationId", "Priority", "StartDate", "StatusId", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 8, new DateTime(2025, 9, 12, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(461), "Secure voting system using blockchain technology", new DateTime(2025, 11, 27, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(460), null, false, 1, "High", new DateTime(2025, 9, 12, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(459), 1, "Blockchain Voting System", null },
                    { 9, new DateTime(2025, 9, 11, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(466), "Internet of Things system for campus management", new DateTime(2026, 1, 11, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(464), null, false, 2, "Medium", new DateTime(2025, 9, 11, 17, 31, 41, 120, DateTimeKind.Utc).AddTicks(463), 1, "IoT Smart Campus", null }
                });

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
                columns: new[] { "CreatedAt", "LinkedInUrl" },
                values: new object[] { new DateTime(2025, 7, 30, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6040), "https://linkedin.com/in/alexjohnson" });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "LinkedInUrl" },
                values: new object[] { new DateTime(2025, 8, 4, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6050), "https://linkedin.com/in/sarahwilliams" });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "LinkedInUrl" },
                values: new object[] { new DateTime(2025, 8, 9, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6054), "https://linkedin.com/in/michaelbrown" });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "LinkedInUrl" },
                values: new object[] { new DateTime(2025, 8, 14, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6057), "https://linkedin.com/in/emilydavis" });

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "LinkedInUrl" },
                values: new object[] { new DateTime(2025, 8, 19, 17, 31, 41, 119, DateTimeKind.Utc).AddTicks(6060), "https://linkedin.com/in/davidmiller" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6067));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 19, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6082));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 24, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6084));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 29, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6086));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 3, 17, 31, 41, 118, DateTimeKind.Utc).AddTicks(6194));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "Students");

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
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 14, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9474), new DateTime(2025, 10, 13, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9471), new DateTime(2025, 8, 14, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9464) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 24, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9482), new DateTime(2025, 11, 12, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9480), new DateTime(2025, 8, 24, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9479) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "EndDate", "StartDate" },
                values: new object[] { new DateTime(2025, 6, 15, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9487), new DateTime(2025, 9, 3, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9485), new DateTime(2025, 6, 15, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9484) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 8, 29, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9492), new DateTime(2025, 10, 28, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9491), new DateTime(2025, 8, 29, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9490) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "CreatedAt", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 3, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9496), new DateTime(2025, 9, 3, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9495) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 8, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9501), new DateTime(2025, 11, 12, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9499), new DateTime(2025, 9, 8, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9498) });

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "CreatedAt", "DueDate", "StartDate" },
                values: new object[] { new DateTime(2025, 9, 10, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9505), new DateTime(2025, 12, 12, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9504), new DateTime(2025, 9, 10, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(9503) });

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
                column: "CreatedAt",
                value: new DateTime(2025, 7, 30, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5121));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 4, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5129));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 9, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5133));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 14, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5136));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2025, 8, 19, 16, 20, 53, 30, DateTimeKind.Utc).AddTicks(5140));

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
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Website = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StudentId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Major = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Year = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateTable(
                name: "StudentRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentRoles_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Organizations",
                columns: new[] { "Id", "Address", "ContactEmail", "CreatedAt", "Description", "IsActive", "Name", "Phone", "Type", "UpdatedAt", "Website" },
                values: new object[,]
                {
                    { 1, "123 Tech Street, Tech City", "info@techuniversity.edu", new DateTime(2025, 7, 15, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(8547), "Leading technology university", true, "Tech University", "555-0101", "University", null, "https://techuniversity.edu" },
                    { 2, "456 Innovation Ave, Tech City", "contact@innovationlabs.com", new DateTime(2025, 7, 20, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(8557), "Research and development company", true, "Innovation Labs", "555-0102", "Company", null, "https://innovationlabs.com" },
                    { 3, "789 Good Street, Tech City", "hello@codeforgood.org", new DateTime(2025, 7, 25, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(8560), "Non-profit organization promoting tech for social good", true, "Code for Good", "555-0103", "Non-profit", null, "https://codeforgood.org" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Leadership", new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(620), "Leads project planning and execution", true, "Project Manager", null },
                    { 2, "Technical", new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(624), "Develops user interface and user experience", true, "Frontend Developer", null },
                    { 3, "Technical", new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(627), "Develops server-side logic and database integration", true, "Backend Developer", null },
                    { 4, "Technical", new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(629), "Designs user interface and user experience", true, "UI/UX Designer", null },
                    { 5, "Technical", new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(631), "Tests software and ensures quality standards", true, "Quality Assurance", null },
                    { 6, "Leadership", new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(634), "Provides guidance and mentorship to team members", true, "Team Lead", null },
                    { 7, "Academic", new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(636), "Conducts research and data analysis", true, "Research Assistant", null },
                    { 8, "Administrative", new DateTime(2025, 8, 4, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(638), "Creates and maintains project documentation", true, "Documentation Specialist", null }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 8, 14, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6555), "john.doe@example.com", "John Doe" },
                    { 2, new DateTime(2025, 8, 19, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6567), "jane.smith@example.com", "Jane Smith" },
                    { 3, new DateTime(2025, 8, 24, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6569), "bob.johnson@example.com", "Bob Johnson" },
                    { 4, new DateTime(2025, 8, 29, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6571), "alice.brown@example.com", "Alice Brown" },
                    { 5, new DateTime(2025, 9, 3, 15, 29, 0, 210, DateTimeKind.Utc).AddTicks(6573), "charlie.wilson@example.com", "Charlie Wilson" }
                });

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "CreatedAt", "Description", "DueDate", "EndDate", "OrganizationId", "Priority", "StartDate", "Status", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 8, 14, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9214), "Web application for managing student records and academic progress", new DateTime(2025, 10, 13, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9208), null, 1, "High", new DateTime(2025, 8, 14, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9201), "In Progress", "Student Management System", null },
                    { 2, new DateTime(2025, 8, 24, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9221), "Machine learning platform for academic research", new DateTime(2025, 11, 12, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9219), null, 2, "Medium", new DateTime(2025, 8, 24, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9218), "Planning", "AI Research Platform", null },
                    { 3, new DateTime(2025, 6, 15, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9225), "Mobile app connecting volunteers with local community needs", null, new DateTime(2025, 9, 3, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9224), 3, "High", new DateTime(2025, 6, 15, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9223), "Completed", "Community Outreach App", null },
                    { 4, new DateTime(2025, 8, 29, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9280), "E-learning platform with video streaming and assessments", new DateTime(2025, 10, 28, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9278), null, 1, "Critical", new DateTime(2025, 8, 29, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9277), "In Progress", "Online Learning Platform", null },
                    { 5, new DateTime(2025, 9, 3, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9284), "Real-time dashboard for analyzing student performance metrics", null, null, 1, "Medium", new DateTime(2025, 9, 3, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(9283), "On Hold", "Data Analytics Dashboard", null }
                });

            migrationBuilder.InsertData(
                table: "Students",
                columns: new[] { "Id", "CreatedAt", "Email", "FirstName", "LastName", "Major", "OrganizationId", "StudentId", "UpdatedAt", "Year" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 7, 30, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5406), "alex.johnson@techuniversity.edu", "Alex", "Johnson", "Computer Science", 1, "TU001", null, "Junior" },
                    { 2, new DateTime(2025, 8, 4, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5414), "sarah.williams@techuniversity.edu", "Sarah", "Williams", "Software Engineering", 1, "TU002", null, "Senior" },
                    { 3, new DateTime(2025, 8, 9, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5417), "michael.brown@techuniversity.edu", "Michael", "Brown", "Data Science", 1, "TU003", null, "Graduate" },
                    { 4, new DateTime(2025, 8, 14, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5420), "emily.davis@techuniversity.edu", "Emily", "Davis", "Cybersecurity", 1, "TU004", null, "Sophomore" },
                    { 5, new DateTime(2025, 8, 19, 15, 29, 0, 211, DateTimeKind.Utc).AddTicks(5423), "david.miller@techuniversity.edu", "David", "Miller", "Computer Science", 1, "TU005", null, "Freshman" }
                });

            migrationBuilder.InsertData(
                table: "StudentRoles",
                columns: new[] { "Id", "AssignedDate", "EndDate", "IsActive", "Notes", "RoleId", "StudentId" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 8, 14, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6496), null, true, "Leading the Student Management System project", 1, 1 },
                    { 2, new DateTime(2025, 8, 19, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6504), null, true, "Frontend development for multiple projects", 2, 2 },
                    { 3, new DateTime(2025, 8, 24, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6507), null, true, "Backend development and database design", 3, 3 },
                    { 4, new DateTime(2025, 8, 29, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6509), null, true, "UI/UX design for community outreach app", 4, 4 },
                    { 5, new DateTime(2025, 9, 3, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6511), null, true, "QA testing for online learning platform", 5, 5 },
                    { 6, new DateTime(2025, 8, 9, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6513), null, true, "Team lead for junior developers", 6, 1 },
                    { 7, new DateTime(2025, 8, 26, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6516), null, true, "Research on AI and machine learning", 7, 3 },
                    { 8, new DateTime(2025, 9, 1, 15, 29, 0, 212, DateTimeKind.Utc).AddTicks(6518), null, true, "Documentation for frontend components", 8, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId",
                table: "Projects",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProjects_StudentsId",
                table: "StudentProjects",
                column: "StudentsId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRoles_RoleId",
                table: "StudentRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRoles_StudentId",
                table: "StudentRoles",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_Email",
                table: "Students",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_OrganizationId",
                table: "Students",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentId",
                table: "Students",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentProjects");

            migrationBuilder.DropTable(
                name: "StudentRoles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseTypeRoleCountAndRoleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CourseType",
                table: "InstituteTemplates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Squad");

            migrationBuilder.AddColumn<int>(
                name: "RoleCount",
                table: "InstituteTemplates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoleIndex",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourseType",
                table: "InstituteTemplates");

            migrationBuilder.DropColumn(
                name: "RoleCount",
                table: "InstituteTemplates");

            migrationBuilder.DropColumn(
                name: "RoleIndex",
                table: "Students");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using strAppersBackend.Data;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260422193000_AddProjectInstituteIdAndInstituteTemplateIsActive")]
    public partial class AddProjectInstituteIdAndInstituteTemplateIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstituteId",
                table: "Projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "InstituteTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_InstituteId",
                table: "Projects",
                column: "InstituteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Institutes_InstituteId",
                table: "Projects",
                column: "InstituteId",
                principalTable: "Institutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Institutes_InstituteId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_InstituteId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "InstituteId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "InstituteTemplates");
        }
    }
}

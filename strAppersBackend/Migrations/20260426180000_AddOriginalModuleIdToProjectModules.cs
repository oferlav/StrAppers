using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using strAppersBackend.Data;

#nullable disable

namespace strAppersBackend.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260426180000_AddOriginalModuleIdToProjectModules")]
    public partial class AddOriginalModuleIdToProjectModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalModuleId",
                table: "ProjectModules",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_ProjectId_OriginalModuleId",
                table: "ProjectModules",
                columns: new[] { "ProjectId", "OriginalModuleId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectModules_ProjectId_OriginalModuleId",
                table: "ProjectModules");

            migrationBuilder.DropColumn(
                name: "OriginalModuleId",
                table: "ProjectModules");
        }
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using strAppersBackend.Data;

#nullable disable

namespace strAppersBackend.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260425130000_AddInUseToProjects")]
    public partial class AddInUseToProjects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InUse",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InUse",
                table: "Projects");
        }
    }
}

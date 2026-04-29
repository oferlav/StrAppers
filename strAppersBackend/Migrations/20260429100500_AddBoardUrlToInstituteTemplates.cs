using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    public partial class AddBoardUrlToInstituteTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoardURL",
                table: "InstituteTemplates",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoardURL",
                table: "InstituteTemplates");
        }
    }
}

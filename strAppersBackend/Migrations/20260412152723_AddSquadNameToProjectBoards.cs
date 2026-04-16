using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSquadNameToProjectBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SquadName",
                table: "ProjectBoards",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_SquadName",
                table: "ProjectBoards",
                column: "SquadName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectBoards_SquadName",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "SquadName",
                table: "ProjectBoards");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStoryBoardToProjectBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserStoryBoardId",
                table: "ProjectBoards",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserStoryBoardUrl",
                table: "ProjectBoards",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserStoryBoardId",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "UserStoryBoardUrl",
                table: "ProjectBoards");
        }
    }
}

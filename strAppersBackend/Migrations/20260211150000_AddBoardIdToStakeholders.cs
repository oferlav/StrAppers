using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardIdToStakeholders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoardId",
                table: "Stakeholders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stakeholders_BoardId",
                table: "Stakeholders",
                column: "BoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stakeholders_ProjectBoards_BoardId",
                table: "Stakeholders",
                column: "BoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stakeholders_ProjectBoards_BoardId",
                table: "Stakeholders");

            migrationBuilder.DropIndex(
                name: "IX_Stakeholders_BoardId",
                table: "Stakeholders");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Stakeholders");
        }
    }
}

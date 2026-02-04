using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemBoardIdToStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemBoardId",
                table: "Students",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_SystemBoardId",
                table: "Students",
                column: "SystemBoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_ProjectBoards_SystemBoardId",
                table: "Students",
                column: "SystemBoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_ProjectBoards_SystemBoardId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_SystemBoardId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "SystemBoardId",
                table: "Students");
        }
    }
}

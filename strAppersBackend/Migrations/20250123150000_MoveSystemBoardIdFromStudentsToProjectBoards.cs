using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class MoveSystemBoardIdFromStudentsToProjectBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add SystemBoardId column to ProjectBoards table
            migrationBuilder.AddColumn<string>(
                name: "SystemBoardId",
                table: "ProjectBoards",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Create index on SystemBoardId for better performance
            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_SystemBoardId",
                table: "ProjectBoards",
                column: "SystemBoardId");

            // Add foreign key constraint (self-referencing)
            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBoards_ProjectBoards_SystemBoardId",
                table: "ProjectBoards",
                column: "SystemBoardId",
                principalTable: "ProjectBoards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.SetNull);

            // Note: SystemBoardId column removal from Students table should be done manually
            // as per user's request
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove foreign key constraint
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBoards_ProjectBoards_SystemBoardId",
                table: "ProjectBoards");

            // Remove index
            migrationBuilder.DropIndex(
                name: "IX_ProjectBoards_SystemBoardId",
                table: "ProjectBoards");

            // Remove SystemBoardId column from ProjectBoards
            migrationBuilder.DropColumn(
                name: "SystemBoardId",
                table: "ProjectBoards");

            // Note: SystemBoardId column addition back to Students table should be done manually if needed
        }
    }
}

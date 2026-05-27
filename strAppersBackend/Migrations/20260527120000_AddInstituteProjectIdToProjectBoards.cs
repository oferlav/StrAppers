using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddInstituteProjectIdToProjectBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstituteProjectId",
                table: "ProjectBoards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_InstituteProjectId",
                table: "ProjectBoards",
                column: "InstituteProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBoards_InstituteProjects_InstituteProjectId",
                table: "ProjectBoards",
                column: "InstituteProjectId",
                principalTable: "InstituteProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBoards_InstituteProjects_InstituteProjectId",
                table: "ProjectBoards");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBoards_InstituteProjectId",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "InstituteProjectId",
                table: "ProjectBoards");
        }
    }
}

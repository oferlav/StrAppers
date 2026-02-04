using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectBoardSprintMergeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectBoardSprintMerge",
                columns: table => new
                {
                    ProjectBoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SprintNumber = table.Column<int>(type: "integer", nullable: false),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ListId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBoardSprintMerge", x => new { x.ProjectBoardId, x.SprintNumber });
                    table.ForeignKey(
                        name: "FK_ProjectBoardSprintMerge_ProjectBoards_ProjectBoardId",
                        column: x => x.ProjectBoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoardSprintMerge_ProjectBoardId",
                table: "ProjectBoardSprintMerge",
                column: "ProjectBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoardSprintMerge_SprintNumber",
                table: "ProjectBoardSprintMerge",
                column: "SprintNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectBoardSprintMerge");
        }
    }
}

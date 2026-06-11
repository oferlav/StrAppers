using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestBoardsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestBoards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PublishUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GithubFrontendUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GithubBackendUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WebApiUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DBPassword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NeonBranchId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NeonProjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestBoards_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestBoards_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestBoards_BoardId",
                table: "QuestBoards",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestBoards_StudentId",
                table: "QuestBoards",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestBoards_StudentId_BoardId",
                table: "QuestBoards",
                columns: new[] { "StudentId", "BoardId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "QuestBoards");
        }
    }
}

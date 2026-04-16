using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheReviewTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CacheReview",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SprintNumber = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewContent = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheReview", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CacheReview_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CacheReview_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CacheReview_BoardId",
                table: "CacheReview",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_CacheReview_BoardId_StudentId_SprintNumber_SequenceNumber",
                table: "CacheReview",
                columns: new[] { "BoardId", "StudentId", "SprintNumber", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CacheReview_StudentId",
                table: "CacheReview",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CacheReview");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardMeetingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardMeetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MeetingTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardMeetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardMeetings_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_BoardId",
                table: "BoardMeetings",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_MeetingTime",
                table: "BoardMeetings",
                column: "MeetingTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardMeetings");
        }
    }
}


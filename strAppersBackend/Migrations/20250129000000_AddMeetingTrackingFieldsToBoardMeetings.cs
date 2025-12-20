using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingTrackingFieldsToBoardMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StudentEmail",
                table: "BoardMeetings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomMeetingUrl",
                table: "BoardMeetings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActualMeetingUrl",
                table: "BoardMeetings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Attended",
                table: "BoardMeetings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinTime",
                table: "BoardMeetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_StudentEmail",
                table: "BoardMeetings",
                column: "StudentEmail");

            migrationBuilder.CreateIndex(
                name: "IX_BoardMeetings_Attended",
                table: "BoardMeetings",
                column: "Attended");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoardMeetings_Attended",
                table: "BoardMeetings");

            migrationBuilder.DropIndex(
                name: "IX_BoardMeetings_StudentEmail",
                table: "BoardMeetings");

            migrationBuilder.DropColumn(
                name: "StudentEmail",
                table: "BoardMeetings");

            migrationBuilder.DropColumn(
                name: "CustomMeetingUrl",
                table: "BoardMeetings");

            migrationBuilder.DropColumn(
                name: "ActualMeetingUrl",
                table: "BoardMeetings");

            migrationBuilder.DropColumn(
                name: "Attended",
                table: "BoardMeetings");

            migrationBuilder.DropColumn(
                name: "JoinTime",
                table: "BoardMeetings");
        }
    }
}





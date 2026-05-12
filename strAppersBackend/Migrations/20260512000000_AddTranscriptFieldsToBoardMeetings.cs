using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscriptFieldsToBoardMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TranscriptId",
                table: "BoardMeetings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TranscriptFetchedAt",
                table: "BoardMeetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptVtt",
                table: "BoardMeetings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TranscriptId",
                table: "BoardMeetings");

            migrationBuilder.DropColumn(
                name: "TranscriptFetchedAt",
                table: "BoardMeetings");

            migrationBuilder.DropColumn(
                name: "TranscriptVtt",
                table: "BoardMeetings");
        }
    }
}

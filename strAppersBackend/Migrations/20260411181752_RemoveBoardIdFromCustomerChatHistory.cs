using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBoardIdFromCustomerChatHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerChatHistory_BoardId_StudentId_SprintId",
                table: "CustomerChatHistory");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "CustomerChatHistory");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerChatHistory_StudentId_SprintId",
                table: "CustomerChatHistory",
                columns: new[] { "StudentId", "SprintId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerChatHistory_StudentId_SprintId",
                table: "CustomerChatHistory");

            migrationBuilder.AddColumn<string>(
                name: "BoardId",
                table: "CustomerChatHistory",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "CustomerChatHistory" AS c
                SET "BoardId" = s."BoardId"
                FROM "Students" AS s
                WHERE s."Id" = c."StudentId" AND c."BoardId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerChatHistory_BoardId_StudentId_SprintId",
                table: "CustomerChatHistory",
                columns: new[] { "BoardId", "StudentId", "SprintId" });
        }
    }
}

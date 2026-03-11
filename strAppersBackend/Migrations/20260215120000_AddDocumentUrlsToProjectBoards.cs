using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentUrlsToProjectBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollectionJourneyUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DatabaseSchemaUrl",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Document1Url",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Document2Url",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Document3Url",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Document4Url",
                table: "ProjectBoards",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Document1Name",
                table: "ProjectBoards",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Document2Name",
                table: "ProjectBoards",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Document3Name",
                table: "ProjectBoards",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Document4Name",
                table: "ProjectBoards",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CollectionJourneyUrl", table: "ProjectBoards");
            migrationBuilder.DropColumn(name: "DatabaseSchemaUrl", table: "ProjectBoards");
            migrationBuilder.DropColumn(name: "Document1Url", table: "ProjectBoards");
            migrationBuilder.DropColumn(name: "Document2Url", table: "ProjectBoards");
            migrationBuilder.DropColumn(name: "Document3Url", table: "ProjectBoards");
            migrationBuilder.DropColumn(name: "Document4Url", table: "ProjectBoards");
            migrationBuilder.DropColumn(name: "Document1Name", table: "ProjectBoards");
            migrationBuilder.DropColumn(name: "Document2Name", table: "ProjectBoards");
            migrationBuilder.DropColumn(name: "Document3Name", table: "ProjectBoards");
            migrationBuilder.DropColumn(name: "Document4Name", table: "ProjectBoards");
        }
    }
}

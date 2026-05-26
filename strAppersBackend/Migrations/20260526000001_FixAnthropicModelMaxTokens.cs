using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class FixAnthropicModelMaxTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "MaxTokens", "Name" },
                values: new object[] { "Anthropic Claude Sonnet 4.6 model - powerful for complex tasks", 64000, "claude-sonnet-4-6" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "MaxTokens", "Name" },
                values: new object[] { "Anthropic Claude Sonnet 4.5 model - powerful for complex tasks", 200000, "claude-sonnet-4-5-20250929" });
        }
    }
}

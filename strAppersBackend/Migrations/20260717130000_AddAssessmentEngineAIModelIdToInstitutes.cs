using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentEngineAIModelIdToInstitutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssessmentEngineAIModelId",
                table: "Institutes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Institutes_AssessmentEngineAIModelId",
                table: "Institutes",
                column: "AssessmentEngineAIModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Institutes_AIModels_AssessmentEngineAIModelId",
                table: "Institutes",
                column: "AssessmentEngineAIModelId",
                principalTable: "AIModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Institutes_AIModels_AssessmentEngineAIModelId",
                table: "Institutes");

            migrationBuilder.DropIndex(
                name: "IX_Institutes_AssessmentEngineAIModelId",
                table: "Institutes");

            migrationBuilder.DropColumn(name: "AssessmentEngineAIModelId", table: "Institutes");
        }
    }
}

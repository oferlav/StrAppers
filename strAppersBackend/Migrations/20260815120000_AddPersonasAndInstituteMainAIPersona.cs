using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <summary>
    /// Personas catalog (student-facing AI chat personas) + Institutes.MainAIPersonaId.
    ///
    /// Hand-written, like the other recent migrations here: the model snapshot in this repo is well
    /// behind the live schema (columns added over time through manual SQL were never scaffolded), so
    /// `dotnet ef migrations add` emits a migration that tries to re-add existing columns. The
    /// equivalent raw SQL ships as Migrations/personas_add.sql for manual application.
    /// </summary>
    public partial class AddPersonasAndInstituteMainAIPersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIPersonas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIPersonas", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "MainAIPersonaId",
                table: "Institutes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Institutes_MainAIPersonaId",
                table: "Institutes",
                column: "MainAIPersonaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Institutes_AIPersonas_MainAIPersonaId",
                table: "Institutes",
                column: "MainAIPersonaId",
                principalTable: "AIPersonas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Institutes_AIPersonas_MainAIPersonaId",
                table: "Institutes");

            migrationBuilder.DropIndex(
                name: "IX_Institutes_MainAIPersonaId",
                table: "Institutes");

            migrationBuilder.DropColumn(name: "MainAIPersonaId", table: "Institutes");

            migrationBuilder.DropTable(name: "AIPersonas");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    public partial class AddInstituteSquadsAndTemplateSquadLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SquadId",
                table: "InstituteTemplates",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstituteSquads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteSquads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteSquads_Institutes_InstituteId",
                        column: x => x.InstituteId,
                        principalTable: "Institutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstituteSquadRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SquadId = table.Column<int>(type: "integer", nullable: false),
                    BaseInstituteRoleId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Competencies = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
                    CustomerEngagement = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsTechnical = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteSquadRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteSquadRoles_InstituteRoles_BaseInstituteRoleId",
                        column: x => x.BaseInstituteRoleId,
                        principalTable: "InstituteRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InstituteSquadRoles_InstituteSquads_SquadId",
                        column: x => x.SquadId,
                        principalTable: "InstituteSquads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstituteSquadRoles_RoleTypes_Type",
                        column: x => x.Type,
                        principalTable: "RoleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstituteSquadRoles_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstituteTemplates_SquadId",
                table: "InstituteTemplates",
                column: "SquadId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_BaseInstituteRoleId",
                table: "InstituteSquadRoles",
                column: "BaseInstituteRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_SkillId",
                table: "InstituteSquadRoles",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_SquadId",
                table: "InstituteSquadRoles",
                column: "SquadId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_SquadId_Name",
                table: "InstituteSquadRoles",
                columns: new[] { "SquadId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquadRoles_Type",
                table: "InstituteSquadRoles",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquads_InstituteId",
                table: "InstituteSquads",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteSquads_InstituteId_Name",
                table: "InstituteSquads",
                columns: new[] { "InstituteId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_InstituteTemplates_InstituteSquads_SquadId",
                table: "InstituteTemplates",
                column: "SquadId",
                principalTable: "InstituteSquads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstituteTemplates_InstituteSquads_SquadId",
                table: "InstituteTemplates");

            migrationBuilder.DropTable(
                name: "InstituteSquadRoles");

            migrationBuilder.DropTable(
                name: "InstituteSquads");

            migrationBuilder.DropIndex(
                name: "IX_InstituteTemplates_SquadId",
                table: "InstituteTemplates");

            migrationBuilder.DropColumn(
                name: "SquadId",
                table: "InstituteTemplates");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddInstituteIdToProjectBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstituteId",
                table: "ProjectBoards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBoards_InstituteId",
                table: "ProjectBoards",
                column: "InstituteId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBoards_Institutes_InstituteId",
                table: "ProjectBoards",
                column: "InstituteId",
                principalTable: "Institutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBoards_Institutes_InstituteId",
                table: "ProjectBoards");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBoards_InstituteId",
                table: "ProjectBoards");

            migrationBuilder.DropColumn(
                name: "InstituteId",
                table: "ProjectBoards");
        }
    }
}

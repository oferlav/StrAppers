using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class InstituteAssistantChatHistoryInstituteProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IACH_InstituteId_TeacherId_ProjectId_Source_CreatedAt",
                table: "InstituteAssistantChatHistory");

            migrationBuilder.AlterColumn<int>(
                name: "ProjectId",
                table: "InstituteAssistantChatHistory",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "InstituteProjectId",
                table: "InstituteAssistantChatHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IACH_InstituteId_TeacherId_Scope_Source_CreatedAt",
                table: "InstituteAssistantChatHistory",
                columns: new[] { "InstituteId", "TeacherId", "ProjectId", "InstituteProjectId", "Source", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InstituteAssistantChatHistory_InstituteProjectId",
                table: "InstituteAssistantChatHistory",
                column: "InstituteProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_InstituteAssistantChatHistory_InstituteProjects_InstitutePr~",
                table: "InstituteAssistantChatHistory",
                column: "InstituteProjectId",
                principalTable: "InstituteProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstituteAssistantChatHistory_InstituteProjects_InstitutePr~",
                table: "InstituteAssistantChatHistory");

            migrationBuilder.DropIndex(
                name: "IX_IACH_InstituteId_TeacherId_Scope_Source_CreatedAt",
                table: "InstituteAssistantChatHistory");

            migrationBuilder.DropIndex(
                name: "IX_InstituteAssistantChatHistory_InstituteProjectId",
                table: "InstituteAssistantChatHistory");

            migrationBuilder.DropColumn(
                name: "InstituteProjectId",
                table: "InstituteAssistantChatHistory");

            migrationBuilder.AlterColumn<int>(
                name: "ProjectId",
                table: "InstituteAssistantChatHistory",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IACH_InstituteId_TeacherId_ProjectId_Source_CreatedAt",
                table: "InstituteAssistantChatHistory",
                columns: new[] { "InstituteId", "TeacherId", "ProjectId", "Source", "CreatedAt" });
        }
    }
}

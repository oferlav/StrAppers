using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheMetricsGraph2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Graph2",
                table: "CacheMetrics",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 14, 12, 56, 403, DateTimeKind.Utc).AddTicks(2499));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 14, 12, 56, 403, DateTimeKind.Utc).AddTicks(2505));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(5296));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(5306));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(5309));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(5312));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(5314));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(5317));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(9728));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(9736));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(9740));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 14, 12, 56, 371, DateTimeKind.Utc).AddTicks(972));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 14, 12, 56, 371, DateTimeKind.Utc).AddTicks(978));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 14, 12, 56, 371, DateTimeKind.Utc).AddTicks(981));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 14, 12, 56, 371, DateTimeKind.Utc).AddTicks(984));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 14, 12, 56, 371, DateTimeKind.Utc).AddTicks(986));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 14, 12, 56, 371, DateTimeKind.Utc).AddTicks(989));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 12, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(7020));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 22, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(7034));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(7038));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(7041));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(7112));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 6, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(7135));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 8, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(7139));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 10, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(7142));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(7145));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 12, 14, 12, 56, 387, DateTimeKind.Utc).AddTicks(7148));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 17, 14, 12, 56, 387, DateTimeKind.Utc).AddTicks(7158));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 22, 14, 12, 56, 387, DateTimeKind.Utc).AddTicks(7161));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 27, 14, 12, 56, 387, DateTimeKind.Utc).AddTicks(7163));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 1, 14, 12, 56, 387, DateTimeKind.Utc).AddTicks(7165));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 7, 14, 12, 56, 387, DateTimeKind.Utc).AddTicks(7168));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 24, 14, 12, 56, 387, DateTimeKind.Utc).AddTicks(7170));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 30, 14, 12, 56, 387, DateTimeKind.Utc).AddTicks(7173));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 25, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(305));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(317));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 7, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(321));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 12, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(325));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 17, 14, 12, 56, 386, DateTimeKind.Utc).AddTicks(329));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 14, 12, 56, 398, DateTimeKind.Utc).AddTicks(5134));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 14, 12, 56, 398, DateTimeKind.Utc).AddTicks(5140));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 14, 12, 56, 398, DateTimeKind.Utc).AddTicks(5142));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 14, 12, 56, 398, DateTimeKind.Utc).AddTicks(5144));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(6767));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(6772));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(6775));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(6777));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 14, 12, 56, 370, DateTimeKind.Utc).AddTicks(6779));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Graph2",
                table: "CacheMetrics");

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 9, 13, 27, 420, DateTimeKind.Utc).AddTicks(1244));

            migrationBuilder.UpdateData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 9, 13, 27, 420, DateTimeKind.Utc).AddTicks(1249));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(7897));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(7911));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(7914));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(7916));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(7919));

            migrationBuilder.UpdateData(
                table: "Majors",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(7921));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 389, DateTimeKind.Utc).AddTicks(2220));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 9, 13, 27, 389, DateTimeKind.Utc).AddTicks(2229));

            migrationBuilder.UpdateData(
                table: "Organizations",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 20, 9, 13, 27, 389, DateTimeKind.Utc).AddTicks(2233));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 9, 13, 27, 389, DateTimeKind.Utc).AddTicks(3450));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 9, 13, 27, 389, DateTimeKind.Utc).AddTicks(3455));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 9, 13, 27, 389, DateTimeKind.Utc).AddTicks(3460));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 9, 13, 27, 389, DateTimeKind.Utc).AddTicks(3464));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 9, 13, 27, 389, DateTimeKind.Utc).AddTicks(3468));

            migrationBuilder.UpdateData(
                table: "ProjectStatuses",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 9, 13, 27, 389, DateTimeKind.Utc).AddTicks(3471));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 12, 9, 13, 27, 403, DateTimeKind.Utc).AddTicks(702));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 22, 9, 13, 27, 403, DateTimeKind.Utc).AddTicks(711));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 1, 11, 9, 13, 27, 403, DateTimeKind.Utc).AddTicks(715));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 27, 9, 13, 27, 403, DateTimeKind.Utc).AddTicks(718));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 1, 9, 13, 27, 403, DateTimeKind.Utc).AddTicks(721));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 6, 9, 13, 27, 403, DateTimeKind.Utc).AddTicks(735));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 8, 9, 13, 27, 403, DateTimeKind.Utc).AddTicks(738));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 10, 9, 13, 27, 403, DateTimeKind.Utc).AddTicks(741));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 9, 9, 13, 27, 403, DateTimeKind.Utc).AddTicks(744));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 12, 9, 13, 27, 404, DateTimeKind.Utc).AddTicks(3739));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 17, 9, 13, 27, 404, DateTimeKind.Utc).AddTicks(3749));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 3,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 22, 9, 13, 27, 404, DateTimeKind.Utc).AddTicks(3751));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 4,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 27, 9, 13, 27, 404, DateTimeKind.Utc).AddTicks(3754));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 5,
                column: "AssignedDate",
                value: new DateTime(2026, 4, 1, 9, 13, 27, 404, DateTimeKind.Utc).AddTicks(3756));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 6,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 7, 9, 13, 27, 404, DateTimeKind.Utc).AddTicks(3759));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 7,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 24, 9, 13, 27, 404, DateTimeKind.Utc).AddTicks(3761));

            migrationBuilder.UpdateData(
                table: "StudentRoles",
                keyColumn: "Id",
                keyValue: 8,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 30, 9, 13, 27, 404, DateTimeKind.Utc).AddTicks(3763));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 25, 9, 13, 27, 402, DateTimeKind.Utc).AddTicks(4047));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 2, 9, 13, 27, 402, DateTimeKind.Utc).AddTicks(4058));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 7, 9, 13, 27, 402, DateTimeKind.Utc).AddTicks(4062));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 12, 9, 13, 27, 402, DateTimeKind.Utc).AddTicks(4066));

            migrationBuilder.UpdateData(
                table: "Students",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 3, 17, 9, 13, 27, 402, DateTimeKind.Utc).AddTicks(4070));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 9, 13, 27, 415, DateTimeKind.Utc).AddTicks(3927));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 9, 13, 27, 415, DateTimeKind.Utc).AddTicks(3931));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 9, 13, 27, 415, DateTimeKind.Utc).AddTicks(3933));

            migrationBuilder.UpdateData(
                table: "Subscriptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 11, 9, 13, 27, 415, DateTimeKind.Utc).AddTicks(3936));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(9273));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(9279));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(9282));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(9284));

            migrationBuilder.UpdateData(
                table: "Years",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 2, 10, 9, 13, 27, 388, DateTimeKind.Utc).AddTicks(9286));
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponToInstitutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Coupon",
                table: "Institutes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Coupon",
                table: "Institutes");
        }
    }
}

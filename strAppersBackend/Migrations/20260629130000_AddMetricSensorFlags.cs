using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricSensorFlags : Migration
    {
        private static readonly string[] SensorColumns =
        [
            "UseCustomerChat", "UseMentorChat", "UseCodebaseQuality", "UseResources",
            "UseStakeholders", "UseProjectModule", "UseMeetingTranscripts", "UseGroupChat",
            "UsePrivateChat", "UseTrelloTasks", "UseTrelloUserStory", "UseFigmaDesign",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var col in SensorColumns)
            {
                migrationBuilder.AddColumn<bool>(
                    name: col,
                    table: "Metrics",
                    type: "boolean",
                    nullable: false,
                    defaultValue: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var col in SensorColumns)
                migrationBuilder.DropColumn(name: col, table: "Metrics");
        }
    }
}

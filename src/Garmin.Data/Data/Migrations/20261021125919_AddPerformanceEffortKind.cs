using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmin.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceEffortKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EffortKind",
                table: "ActivityPerformanceEfforts",
                type: "TEXT",
                maxLength: 24,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffortKind",
                table: "ActivityPerformanceEfforts");
        }
    }
}

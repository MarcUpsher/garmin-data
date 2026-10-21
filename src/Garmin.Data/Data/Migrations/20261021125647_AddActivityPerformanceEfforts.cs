using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmin.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityPerformanceEfforts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityPerformanceEfforts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<long>(type: "INTEGER", nullable: false),
                    DistanceMeters = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    StartElapsedSeconds = table.Column<double>(type: "REAL", nullable: false),
                    EndElapsedSeconds = table.Column<double>(type: "REAL", nullable: false),
                    ObservationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AlgorithmVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityPerformanceEfforts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityPerformanceEfforts_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityPerformanceEfforts_ActivityId_DistanceMeters",
                table: "ActivityPerformanceEfforts",
                columns: new[] { "ActivityId", "DistanceMeters" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityPerformanceEfforts");
        }
    }
}

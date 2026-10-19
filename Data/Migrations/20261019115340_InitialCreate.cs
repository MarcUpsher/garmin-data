using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmin.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FitSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sport = table.Column<int>(type: "INTEGER", nullable: true),
                    SubSport = table.Column<int>(type: "INTEGER", nullable: true),
                    ElapsedTimeSeconds = table.Column<double>(type: "REAL", nullable: true),
                    TimerTimeSeconds = table.Column<double>(type: "REAL", nullable: true),
                    DistanceMeters = table.Column<double>(type: "REAL", nullable: true),
                    Calories = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageHeartRateBpm = table.Column<int>(type: "INTEGER", nullable: true),
                    MaximumHeartRateBpm = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageSpeedMetersPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    MaximumSpeedMetersPerSecond = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActivityExtraFields",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<long>(type: "INTEGER", nullable: false),
                    GlobalMessageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DeveloperDataIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseType = table.Column<int>(type: "INTEGER", nullable: true),
                    RawValue = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityExtraFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityExtraFields_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityLaps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<long>(type: "INTEGER", nullable: false),
                    LapNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ElapsedTimeSeconds = table.Column<double>(type: "REAL", nullable: true),
                    TimerTimeSeconds = table.Column<double>(type: "REAL", nullable: true),
                    DistanceMeters = table.Column<double>(type: "REAL", nullable: true),
                    Calories = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageHeartRateBpm = table.Column<int>(type: "INTEGER", nullable: true),
                    MaximumHeartRateBpm = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageSpeedMetersPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    MaximumSpeedMetersPerSecond = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLaps_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivityRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<long>(type: "INTEGER", nullable: false),
                    RecordNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LatitudeDegrees = table.Column<double>(type: "REAL", nullable: true),
                    LongitudeDegrees = table.Column<double>(type: "REAL", nullable: true),
                    AltitudeMeters = table.Column<double>(type: "REAL", nullable: true),
                    HeartRateBpm = table.Column<int>(type: "INTEGER", nullable: true),
                    CadenceRpm = table.Column<int>(type: "INTEGER", nullable: true),
                    DistanceMeters = table.Column<double>(type: "REAL", nullable: true),
                    SpeedMetersPerSecond = table.Column<double>(type: "REAL", nullable: true),
                    PowerWatts = table.Column<int>(type: "INTEGER", nullable: true),
                    TemperatureCelsius = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityRecords_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySourceFiles",
                columns: table => new
                {
                    ActivityId = table.Column<long>(type: "INTEGER", nullable: false),
                    FitBytes = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySourceFiles", x => x.ActivityId);
                    table.ForeignKey(
                        name: "FK_ActivitySourceFiles_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_FitSha256",
                table: "Activities",
                column: "FitSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_StartedAtUtc",
                table: "Activities",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityExtraFields_ActivityId_GlobalMessageNumber_MessageNumber_FieldNumber_DeveloperDataIndex",
                table: "ActivityExtraFields",
                columns: new[] { "ActivityId", "GlobalMessageNumber", "MessageNumber", "FieldNumber", "DeveloperDataIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLaps_ActivityId_LapNumber",
                table: "ActivityLaps",
                columns: new[] { "ActivityId", "LapNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_ActivityId_RecordNumber",
                table: "ActivityRecords",
                columns: new[] { "ActivityId", "RecordNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRecords_ActivityId_TimestampUtc",
                table: "ActivityRecords",
                columns: new[] { "ActivityId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityExtraFields");

            migrationBuilder.DropTable(
                name: "ActivityLaps");

            migrationBuilder.DropTable(
                name: "ActivityRecords");

            migrationBuilder.DropTable(
                name: "ActivitySourceFiles");

            migrationBuilder.DropTable(
                name: "Activities");
        }
    }
}

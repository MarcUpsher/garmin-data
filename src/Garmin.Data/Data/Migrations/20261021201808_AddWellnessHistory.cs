using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmin.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWellnessHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyWellnessRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceRecordHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CalendarDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    TotalSteps = table.Column<int>(type: "INTEGER", nullable: true),
                    DailyStepGoal = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalDistanceMeters = table.Column<double>(type: "REAL", nullable: true),
                    TotalKilocalories = table.Column<int>(type: "INTEGER", nullable: true),
                    ActiveKilocalories = table.Column<int>(type: "INTEGER", nullable: true),
                    RestingHeartRateBpm = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentDayRestingHeartRateBpm = table.Column<int>(type: "INTEGER", nullable: true),
                    AllDayStress = table.Column<int>(type: "INTEGER", nullable: true),
                    BodyBattery = table.Column<int>(type: "INTEGER", nullable: true),
                    ModerateIntensityMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    VigorousIntensityMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    FloorsAscended = table.Column<int>(type: "INTEGER", nullable: true),
                    FloorsDescended = table.Column<int>(type: "INTEGER", nullable: true),
                    Respiration = table.Column<double>(type: "REAL", nullable: true),
                    HydrationMillilitres = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyWellnessRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SleepRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceRecordHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CalendarDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SleepStartUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SleepEndUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeepSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    LightSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    RemSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    AwakeSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    UnmeasurableSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageRespiration = table.Column<double>(type: "REAL", nullable: true),
                    LowestRespiration = table.Column<double>(type: "REAL", nullable: true),
                    HighestRespiration = table.Column<double>(type: "REAL", nullable: true),
                    WindowConfirmationType = table.Column<string>(type: "TEXT", nullable: true),
                    IsRetroactive = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SleepRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vo2MaxRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceRecordHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CalendarDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Sport = table.Column<string>(type: "TEXT", nullable: true),
                    SubSport = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    Vo2MaxValue = table.Column<double>(type: "REAL", nullable: true),
                    MaxMet = table.Column<double>(type: "REAL", nullable: true),
                    MaxMetCategory = table.Column<string>(type: "TEXT", nullable: true),
                    IsCalibrated = table.Column<bool>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vo2MaxRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyWellnessRecords_CalendarDate",
                table: "DailyWellnessRecords",
                column: "CalendarDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailyWellnessRecords_SourceRecordHash",
                table: "DailyWellnessRecords",
                column: "SourceRecordHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SleepRecords_CalendarDate",
                table: "SleepRecords",
                column: "CalendarDate");

            migrationBuilder.CreateIndex(
                name: "IX_SleepRecords_SourceRecordHash",
                table: "SleepRecords",
                column: "SourceRecordHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vo2MaxRecords_CalendarDate_Sport",
                table: "Vo2MaxRecords",
                columns: new[] { "CalendarDate", "Sport" });

            migrationBuilder.CreateIndex(
                name: "IX_Vo2MaxRecords_SourceRecordHash",
                table: "Vo2MaxRecords",
                column: "SourceRecordHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyWellnessRecords");

            migrationBuilder.DropTable(
                name: "SleepRecords");

            migrationBuilder.DropTable(
                name: "Vo2MaxRecords");
        }
    }
}

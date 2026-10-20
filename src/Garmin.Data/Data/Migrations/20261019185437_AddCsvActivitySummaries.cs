using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmin.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCsvActivitySummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FitBytes",
                table: "ActivitySourceFiles",
                newName: "SourceBytes");

            migrationBuilder.AddColumn<string>(
                name: "SourceFormat",
                table: "ActivitySourceFiles",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "FitSha256",
                table: "Activities",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<string>(
                name: "ActivityType",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AerobicTrainingEffect",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageCadenceRpm",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AveragePaceSecondsPerKilometer",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageStrideLengthMeters",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BestLapTimeSeconds",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BestPaceSecondsPerKilometer",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LapCount",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaximumCadenceRpm",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaximumElevationMeters",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinimumElevationMeters",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MovingTimeSeconds",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKind",
                table: "Activities",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceSha256",
                table: "Activities",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Activities SET SourceSha256 = FitSha256, SourceKind = 'FIT' WHERE FitSha256 IS NOT NULL;");
            migrationBuilder.Sql("UPDATE ActivitySourceFiles SET SourceFormat = 'FIT';");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtLocal",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Steps",
                table: "Activities",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TotalAscentMeters",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TotalDescentMeters",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TrainingStressScore",
                table: "Activities",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_SourceSha256",
                table: "Activities",
                column: "SourceSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_StartedAtLocal",
                table: "Activities",
                column: "StartedAtLocal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_SourceSha256",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_StartedAtLocal",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SourceFormat",
                table: "ActivitySourceFiles");

            migrationBuilder.DropColumn(
                name: "ActivityType",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "AerobicTrainingEffect",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "AverageCadenceRpm",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "AveragePaceSecondsPerKilometer",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "AverageStrideLengthMeters",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "BestLapTimeSeconds",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "BestPaceSecondsPerKilometer",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "LapCount",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "MaximumCadenceRpm",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "MaximumElevationMeters",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "MinimumElevationMeters",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "MovingTimeSeconds",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SourceKind",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SourceSha256",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "StartedAtLocal",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Steps",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TotalAscentMeters",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TotalDescentMeters",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "TrainingStressScore",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "SourceBytes",
                table: "ActivitySourceFiles",
                newName: "FitBytes");

            migrationBuilder.AlterColumn<string>(
                name: "FitSha256",
                table: "Activities",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}

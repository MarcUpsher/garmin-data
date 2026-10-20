using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmin.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGarminExportActivitySources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalActivityId",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivitySourceEvidence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActivityId = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceFormat = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SourceSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceBytes = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySourceEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivitySourceEvidence_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ExternalActivityId",
                table: "Activities",
                column: "ExternalActivityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySourceEvidence_ActivityId",
                table: "ActivitySourceEvidence",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySourceEvidence_SourceSha256",
                table: "ActivitySourceEvidence",
                column: "SourceSha256",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivitySourceEvidence");

            migrationBuilder.DropIndex(
                name: "IX_Activities_ExternalActivityId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ExternalActivityId",
                table: "Activities");
        }
    }
}

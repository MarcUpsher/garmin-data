using Garmin.Api.Contracts;
using Garmin.Data;
using Garmin.Data.Analysis;
using Garmin.Data.Importing;
using Microsoft.EntityFrameworkCore;
using static Garmin.Data.Analysis.ActivityMetrics;

namespace Garmin.Api.Endpoints;

internal static class WellnessEndpoints
{
    public static void MapWellnessEndpoints(this IEndpointRouteBuilder app, SemaphoreSlim syncLock)
    {
        app.MapPost("/api/wellness/import", async (
            WellnessImportRequest request, GarminDbContext db, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.DirectoryPath)) return Results.BadRequest("Enter the DI_CONNECT directory from your Garmin export.");
            if (!await syncLock.WaitAsync(0, cancellationToken)) return Results.Conflict(new { message = "Another import or sync is already running." });
            try
            {
                var result = await GarminWellnessExportImporter.ImportAsync(request.DirectoryPath, db, cancellationToken);
                return Results.Ok(result);
            }
            catch (DirectoryNotFoundException) { return Results.Problem("Select the DI_CONNECT folder containing the Garmin export subfolders.", statusCode: 400); }
            catch (System.Text.Json.JsonException) { return Results.Problem("An export file contains invalid JSON. The import was not saved.", statusCode: 400); }
            catch (UnauthorizedAccessException) { return Results.Problem("The export folder cannot be read. Check its file permissions.", statusCode: 400); }
            catch (IOException ex) { return Results.Problem($"The export could not be read: {ex.Message}", statusCode: 400); }
            catch (ArgumentException) { return Results.Problem("Enter a valid DI_CONNECT folder path.", statusCode: 400); }
            finally { syncLock.Release(); }
        });

        app.MapGet("/api/wellness/status", async (GarminDbContext db, CancellationToken cancellationToken) =>
        {
            var sleepCount = await db.SleepRecords.CountAsync(cancellationToken);
            var wellnessCount = await db.DailyWellnessRecords.CountAsync(cancellationToken);
            var vo2Count = await db.Vo2MaxRecords.CountAsync(cancellationToken);
            return Results.Ok(new
            {
                sleepRecords = sleepCount,
                sleepFirstDate = sleepCount == 0 ? (DateOnly?)null : await db.SleepRecords.MinAsync(x => x.CalendarDate, cancellationToken),
                sleepLastDate = sleepCount == 0 ? (DateOnly?)null : await db.SleepRecords.MaxAsync(x => x.CalendarDate, cancellationToken),
                dailyWellnessRecords = wellnessCount,
                dailyWellnessNeedsUpdate = await db.DailyWellnessRecords.CountAsync(x => x.ImportVersion < GarminWellnessExportImporter.CurrentWellnessImportVersion, cancellationToken),
                wellnessFirstDate = wellnessCount == 0 ? (DateOnly?)null : await db.DailyWellnessRecords.MinAsync(x => x.CalendarDate, cancellationToken),
                wellnessLastDate = wellnessCount == 0 ? (DateOnly?)null : await db.DailyWellnessRecords.MaxAsync(x => x.CalendarDate, cancellationToken),
                vo2MaxRecords = vo2Count,
                vo2FirstDate = vo2Count == 0 ? (DateOnly?)null : await db.Vo2MaxRecords.MinAsync(x => x.CalendarDate, cancellationToken),
                vo2LastDate = vo2Count == 0 ? (DateOnly?)null : await db.Vo2MaxRecords.MaxAsync(x => x.CalendarDate, cancellationToken)
            });
        });
    }
}

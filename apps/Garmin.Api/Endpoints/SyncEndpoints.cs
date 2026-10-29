using Garmin.Api.Contracts;
using Garmin.Data;
using Garmin.Data.Analysis;
using Garmin.Data.Importing;
using Microsoft.EntityFrameworkCore;
using static Garmin.Data.Analysis.ActivityMetrics;

namespace Garmin.Api.Endpoints;

internal static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app, SemaphoreSlim syncLock)
    {
        app.MapPost("/api/sync", async (GarminDbContext db, CancellationToken cancellationToken) =>
        {
            if (!await syncLock.WaitAsync(0, cancellationToken))
                return Results.Conflict(new { message = "A watch import is already running." });

            try
            {
                var activityDirectory = GarminWatchDiscovery.FindActivityDirectory(
                    Environment.GetEnvironmentVariable("GARMIN_WATCH_ROOTS"));
                if (activityDirectory is null)
                    return Results.Conflict(new { message = "No mounted Garmin watch found. Plug it in, choose File Transfer on the watch if prompted, and wait for GARMIN/Activity to appear in the file manager." });

                var result = await FitDirectoryImporter.ImportAsync(activityDirectory, db, cancellationToken);
                return Results.Ok(new
                {
                    result.Imported,
                    result.Skipped,
                    result.Failed,
                    result.RecordCount,
                    result.LapCount,
                    result.FilesScanned,
                    result.FilesHashed,
                    result.Errors,
                    Mount = Path.GetDirectoryName(Path.GetDirectoryName(activityDirectory))
                });
            }
            catch (OperationCanceledException)
            {
                return Results.Problem("The watch import was cancelled.", statusCode: 499);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Watch import failed: {ex.Message}", statusCode: 500);
            }
            finally
            {
                syncLock.Release();
            }
        });
    }
}

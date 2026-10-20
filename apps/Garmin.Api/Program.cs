using Garmin.Data;
using Garmin.Data.Importing;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5080");
var defaultDataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "garmin-data");
var databasePath = Path.GetFullPath(Environment.GetEnvironmentVariable("GARMIN_DB_PATH")
    ?? Path.Combine(defaultDataDirectory, "activities.db"));
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

builder.Services.AddDbContext<GarminDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddCors(options => options.AddPolicy("local-angular", policy => policy
    .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();
app.UseCors("local-angular");
var syncLock = new SemaphoreSlim(1, 1);

app.MapGet("/api/activities/{id:long}", async (
    long id,
    GarminDbContext db,
    CancellationToken cancellationToken) =>
{
    var activity = await db.Activities.AsNoTracking()
        .Where(item => item.Id == id)
        .Select(item => new ActivityDetails(
            item.Id,
            item.StartedAtUtc ?? item.StartedAtLocal,
            item.ActivityType,
            item.ActivityTitle,
            item.DistanceMeters,
            item.ElapsedTimeSeconds,
            item.MovingTimeSeconds,
            item.AverageHeartRateBpm,
            item.MaximumHeartRateBpm,
            item.AveragePaceSecondsPerKilometer,
            item.BestPaceSecondsPerKilometer,
            item.AverageSpeedMetersPerSecond,
            item.MaximumSpeedMetersPerSecond,
            item.Calories,
            item.LapCount ?? item.Laps.Count,
            item.TotalAscentMeters,
            item.SourceKind,
            item.Records.Any()))
        .SingleOrDefaultAsync(cancellationToken);

    if (activity is null) return Results.NotFound();

    var laps = await db.ActivityLaps.AsNoTracking()
        .Where(lap => lap.ActivityId == id)
        .OrderBy(lap => lap.LapNumber)
        .Select(lap => new ActivityLapItem(
            lap.LapNumber,
            lap.StartedAtUtc,
            lap.ElapsedTimeSeconds,
            lap.TimerTimeSeconds,
            lap.DistanceMeters,
            lap.AverageHeartRateBpm,
            lap.MaximumHeartRateBpm,
            lap.AverageSpeedMetersPerSecond,
            lap.MaximumSpeedMetersPerSecond))
        .ToListAsync(cancellationToken);

    var records = await db.ActivityRecords.AsNoTracking()
        .Where(record => record.ActivityId == id)
        .OrderBy(record => record.RecordNumber)
        .Select(record => new ActivitySample(
            record.TimestampUtc,
            record.DistanceMeters,
            record.SpeedMetersPerSecond,
            record.HeartRateBpm,
            record.AltitudeMeters))
        .ToListAsync(cancellationToken);

    const int maximumChartSamples = 1200;
    if (records.Count > maximumChartSamples)
    {
        var lastIndex = records.Count - 1;
        records = Enumerable.Range(0, maximumChartSamples)
            .Select(index => records[(int)Math.Round(index * (double)lastIndex / (maximumChartSamples - 1))])
            .ToList();
    }

    return Results.Ok(new ActivityDetailsResponse(activity, laps, records));
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GarminDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/api/activities", async (
    int? page,
    int? pageSize,
    string? type,
    GarminDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentPage = page ?? 1;
    var currentPageSize = pageSize ?? 25;
    if (currentPage < 1 || currentPageSize < 1 || currentPageSize > 100)
        return Results.BadRequest("page must be positive and pageSize must be between 1 and 100.");

    var query = db.Activities.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(type))
        query = query.Where(activity => activity.ActivityType == type);

    var totalCount = await query.CountAsync(cancellationToken);
    var items = await query
        .OrderByDescending(activity => activity.StartedAtUtc ?? activity.StartedAtLocal)
        .ThenByDescending(activity => activity.Id)
        .Skip((currentPage - 1) * currentPageSize)
        .Take(currentPageSize)
        .Select(activity => new ActivityListItem(
            activity.Id,
            activity.StartedAtUtc ?? activity.StartedAtLocal,
            activity.ActivityType,
            activity.ActivityTitle,
            activity.DistanceMeters,
            activity.ElapsedTimeSeconds,
            activity.MovingTimeSeconds,
            activity.AverageHeartRateBpm,
            activity.MaximumHeartRateBpm,
            activity.AveragePaceSecondsPerKilometer,
            activity.Calories,
            activity.LapCount ?? activity.Laps.Count,
            activity.SourceKind,
            activity.Records.Any()))
        .ToListAsync(cancellationToken);

    return Results.Ok(new ActivityPage(items, totalCount, currentPage, currentPageSize));
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/sync", async (GarminDbContext db, CancellationToken cancellationToken) =>
{
    if (!await syncLock.WaitAsync(0, cancellationToken))
        return Results.Conflict(new { message = "A watch import is already running." });

    try
    {
        var activityDirectory = FindMountedGarminActivityDirectory();
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

app.Run();

static string? FindMountedGarminActivityDirectory()
{
    var user = Environment.UserName;
    var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
    var roots = new[]
        {
            $"/run/media/{user}", $"/media/{user}", "/media",
            runtimeDirectory is null ? null : Path.Combine(runtimeDirectory, "gvfs")
        }
        .Where(path => path is not null)
        .Select(path => path!)
        .Where(Directory.Exists)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    foreach (var root in roots)
    {
        var candidates = new List<string> { root };
        try
        {
            var pending = new Queue<(string Path, int Depth)>();
            pending.Enqueue((root, 0));
            while (pending.TryDequeue(out var current) && current.Depth < 5)
            {
                foreach (var child in Directory.EnumerateDirectories(current.Path))
                {
                    candidates.Add(child);
                    pending.Enqueue((child, current.Depth + 1));
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        foreach (var candidate in candidates)
        {
            var activityDirectory = Path.Combine(candidate, "GARMIN", "Activity");
            if (Directory.Exists(activityDirectory)) return activityDirectory;
            if (Path.GetFileName(candidate).Equals("GARMIN", StringComparison.OrdinalIgnoreCase))
            {
                activityDirectory = Path.Combine(candidate, "Activity");
                if (Directory.Exists(activityDirectory)) return activityDirectory;
            }
        }
    }

    return null;
}

internal sealed record ActivityListItem(
    long Id,
    DateTime? StartedAt,
    string? ActivityType,
    string? Title,
    double? DistanceMeters,
    double? ElapsedTimeSeconds,
    double? MovingTimeSeconds,
    int? AverageHeartRateBpm,
    int? MaximumHeartRateBpm,
    double? AveragePaceSecondsPerKilometer,
    int? Calories,
    int LapCount,
    string Source,
    bool HasRecordSamples);

internal sealed record ActivityPage(
    IReadOnlyList<ActivityListItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

internal sealed record ActivityDetails(
    long Id,
    DateTime? StartedAt,
    string? ActivityType,
    string? Title,
    double? DistanceMeters,
    double? ElapsedTimeSeconds,
    double? MovingTimeSeconds,
    int? AverageHeartRateBpm,
    int? MaximumHeartRateBpm,
    double? AveragePaceSecondsPerKilometer,
    double? BestPaceSecondsPerKilometer,
    double? AverageSpeedMetersPerSecond,
    double? MaximumSpeedMetersPerSecond,
    int? Calories,
    int LapCount,
    double? TotalAscentMeters,
    string Source,
    bool HasRecordSamples);

internal sealed record ActivityLapItem(
    int LapNumber,
    DateTime? StartedAtUtc,
    double? ElapsedTimeSeconds,
    double? TimerTimeSeconds,
    double? DistanceMeters,
    int? AverageHeartRateBpm,
    int? MaximumHeartRateBpm,
    double? AverageSpeedMetersPerSecond,
    double? MaximumSpeedMetersPerSecond);

internal sealed record ActivitySample(
    DateTime? TimestampUtc,
    double? DistanceMeters,
    double? SpeedMetersPerSecond,
    int? HeartRateBpm,
    double? AltitudeMeters);

internal sealed record ActivityDetailsResponse(
    ActivityDetails Activity,
    IReadOnlyList<ActivityLapItem> Laps,
    IReadOnlyList<ActivitySample> Records);

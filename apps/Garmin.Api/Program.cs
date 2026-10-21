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
    if (activity.AveragePaceSecondsPerKilometer is > 3600)
        activity = activity with
        {
            AveragePaceSecondsPerKilometer = ResolvePace(
                activity.AveragePaceSecondsPerKilometer,
                activity.AverageSpeedMetersPerSecond,
                activity.DistanceMeters,
                activity.ElapsedTimeSeconds)
        };

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
            record.RecordNumber,
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

app.MapGet("/api/activities/{id:long}/segment-summary", async (
    long id,
    int startRecord,
    int endRecord,
    GarminDbContext db,
    CancellationToken cancellationToken) =>
{
    if (startRecord < 0 || endRecord <= startRecord)
        return Results.BadRequest("Select a valid record range.");

    var records = await db.ActivityRecords.AsNoTracking()
        .Where(record => record.ActivityId == id
            && record.RecordNumber >= startRecord
            && record.RecordNumber <= endRecord)
        .OrderBy(record => record.RecordNumber)
        .Select(record => new SegmentRecord(
            record.RecordNumber,
            record.TimestampUtc,
            record.DistanceMeters,
            record.HeartRateBpm,
            record.AltitudeMeters))
        .ToListAsync(cancellationToken);

    if (records.Count < 2) return Results.NotFound();

    var activityRecords = db.ActivityRecords.AsNoTracking().Where(record => record.ActivityId == id);
    var activityStartTime = await activityRecords.Where(record => record.TimestampUtc != null)
        .OrderBy(record => record.RecordNumber)
        .Select(record => record.TimestampUtc)
        .FirstOrDefaultAsync(cancellationToken);
    var activityStartDistance = await activityRecords.Where(record => record.DistanceMeters != null)
        .OrderBy(record => record.RecordNumber)
        .Select(record => record.DistanceMeters)
        .FirstOrDefaultAsync(cancellationToken);

    var firstDistance = records.FirstOrDefault(record => record.DistanceMeters is not null)?.DistanceMeters;
    var lastDistance = records.LastOrDefault(record => record.DistanceMeters is not null)?.DistanceMeters;
    double? distance = firstDistance is not null && lastDistance is not null
        ? Math.Max(0, lastDistance.Value - firstDistance.Value)
        : null;
    var firstTime = records.FirstOrDefault(record => record.TimestampUtc is not null)?.TimestampUtc;
    var lastTime = records.LastOrDefault(record => record.TimestampUtc is not null)?.TimestampUtc;
    double? duration = firstTime is not null && lastTime is not null
        ? Math.Max(0, (lastTime.Value - firstTime.Value).TotalSeconds)
        : null;
    double? pace = distance is > 0 && duration is not null
        ? duration.Value * 1000.0 / distance.Value
        : null;
    var startTime = records.FirstOrDefault(record => record.TimestampUtc is not null)?.TimestampUtc;
    var endTime = records.LastOrDefault(record => record.TimestampUtc is not null)?.TimestampUtc;
    double? startElapsed = startTime is not null && activityStartTime is not null
        ? Math.Max(0, (startTime.Value - activityStartTime.Value).TotalSeconds)
        : null;
    double? endElapsed = endTime is not null && activityStartTime is not null
        ? Math.Max(0, (endTime.Value - activityStartTime.Value).TotalSeconds)
        : null;
    double? startPositionDistance = firstDistance is not null && activityStartDistance is not null
        ? Math.Max(0, firstDistance.Value - activityStartDistance.Value)
        : null;
    double? endPositionDistance = lastDistance is not null && activityStartDistance is not null
        ? Math.Max(0, lastDistance.Value - activityStartDistance.Value)
        : null;
    var heartRates = records.Where(record => record.HeartRateBpm is not null)
        .Select(record => record.HeartRateBpm!.Value).ToArray();
    double? averageHeartRate = heartRates.Length > 0 ? heartRates.Average() : null;
    var heartRateRecords = records.Where(record => record.HeartRateBpm is not null).ToArray();
    int? startHeartRate = heartRateRecords.FirstOrDefault()?.HeartRateBpm;
    int? endHeartRate = heartRateRecords.LastOrDefault()?.HeartRateBpm;
    int? peakHeartRate = heartRates.Length > 0 ? heartRates.Max() : null;
    var elevationChange = EstimateElevationChange(records);

    return Results.Ok(new ActivitySegmentSummary(
        startElapsed,
        endElapsed,
        startPositionDistance,
        endPositionDistance,
        distance,
        elevationChange.Gain,
        elevationChange.Loss,
        elevationChange.Net,
        pace,
        startHeartRate,
        endHeartRate,
        peakHeartRate,
        averageHeartRate,
        duration));
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
    int? dayOfWeek,
    bool? hidden,
    GarminDbContext db,
    CancellationToken cancellationToken) =>
{
    var currentPage = page ?? 1;
    var currentPageSize = pageSize ?? 25;
    if (currentPage < 1 || currentPageSize < 1 || currentPageSize > 100)
        return Results.BadRequest("page must be positive and pageSize must be between 1 and 100.");
    if (dayOfWeek is < 0 or > 6)
        return Results.BadRequest("dayOfWeek must be between 0 (Sunday) and 6 (Saturday).");

    var query = db.Activities.AsNoTracking();
    query = query.Where(activity => activity.IsHidden == (hidden ?? false));
    if (!string.IsNullOrWhiteSpace(type))
        query = query.Where(activity => activity.ActivityType == type);
    if (dayOfWeek is { } day)
        query = query.Where(activity => (activity.StartedAtLocal ?? activity.StartedAtUtc)!.Value.DayOfWeek == (DayOfWeek)day);

    var totalCount = await query.CountAsync(cancellationToken);
    var rows = await query
        .OrderByDescending(activity => activity.StartedAtUtc ?? activity.StartedAtLocal)
        .ThenByDescending(activity => activity.Id)
        .Skip((currentPage - 1) * currentPageSize)
        .Take(currentPageSize)
        .Select(activity => new
        {
            activity.Id,
            StartedAt = activity.StartedAtUtc ?? activity.StartedAtLocal,
            activity.ActivityType,
            activity.ActivityTitle,
            activity.DistanceMeters,
            activity.ElapsedTimeSeconds,
            activity.MovingTimeSeconds,
            activity.AverageHeartRateBpm,
            activity.MaximumHeartRateBpm,
            activity.AveragePaceSecondsPerKilometer,
            activity.AverageSpeedMetersPerSecond,
            activity.Calories,
            LapCount = activity.LapCount ?? activity.Laps.Count,
            activity.SourceKind,
            HasRecordSamples = activity.Records.Any()
        })
        .ToListAsync(cancellationToken);

    var items = rows.Select(activity => new ActivityListItem(
        activity.Id,
        activity.StartedAt,
        activity.ActivityType,
        activity.ActivityTitle,
        activity.DistanceMeters,
        activity.ElapsedTimeSeconds,
        activity.MovingTimeSeconds,
        activity.AverageHeartRateBpm,
        activity.MaximumHeartRateBpm,
        ResolvePace(activity.AveragePaceSecondsPerKilometer, activity.AverageSpeedMetersPerSecond,
            activity.DistanceMeters, activity.ElapsedTimeSeconds),
        activity.Calories,
        activity.LapCount,
        activity.SourceKind,
        activity.HasRecordSamples)).ToList();

    return Results.Ok(new ActivityPage(items, totalCount, currentPage, currentPageSize));
});

app.MapGet("/api/trends/consistency", async (
    string? period,
    string? bucket,
    GarminDbContext db,
    CancellationToken cancellationToken) =>
{
    var selectedPeriod = string.IsNullOrWhiteSpace(period) ? "6m" : period.ToLowerInvariant();
    var selectedBucket = string.IsNullOrWhiteSpace(bucket) ? "month" : bucket.ToLowerInvariant();
    if (selectedPeriod is not ("6m" or "1y" or "all"))
        return Results.BadRequest("period must be 6m, 1y, or all.");
    if (selectedBucket is not ("month" or "week"))
        return Results.BadRequest("bucket must be month or week.");
    if (selectedBucket == "week" && selectedPeriod != "6m")
        return Results.BadRequest("Weekly buckets are available for the 6-month range.");

    var activities = await db.Activities.AsNoTracking()
        .Where(activity => activity.ActivityType == "running" && !activity.IsHidden)
        .OrderBy(activity => activity.StartedAtLocal ?? activity.StartedAtUtc)
        .Select(activity => new RunningTrendActivity(
            activity.Id,
            activity.StartedAtLocal,
            activity.StartedAtUtc,
            activity.ActivityTitle,
            activity.DistanceMeters,
            activity.ElapsedTimeSeconds,
            activity.TimerTimeSeconds,
            activity.MovingTimeSeconds,
            activity.AverageHeartRateBpm,
            activity.MaximumHeartRateBpm,
            activity.AveragePaceSecondsPerKilometer,
            activity.AverageSpeedMetersPerSecond,
            activity.Calories,
            activity.LapCount ?? activity.Laps.Count,
            activity.SourceKind,
            activity.Records.Any()))
        .ToListAsync(cancellationToken);

    var today = DateOnly.FromDateTime(DateTime.Now);
    var endExclusive = today.AddDays(1);
    var earliest = activities.Where(activity => ActivityDate(activity.StartedAtLocal, activity.StartedAtUtc) is not null)
        .Select(activity => ActivityDate(activity.StartedAtLocal, activity.StartedAtUtc)!.Value)
        .DefaultIfEmpty(today)
        .Min();
    var rangeStart = selectedPeriod switch
    {
        "6m" => today.AddMonths(-6),
        "1y" => today.AddYears(-1),
        _ => earliest
    };

    var bins = new List<RunningConsistencyBucket>();
    var cursor = selectedBucket == "month"
        ? new DateOnly(rangeStart.Year, rangeStart.Month, 1)
        : rangeStart.AddDays(-(((int)rangeStart.DayOfWeek + 6) % 7));
    while (cursor < endExclusive)
    {
        var naturalEnd = selectedBucket == "month" ? cursor.AddMonths(1) : cursor.AddDays(7);
        var visibleStart = cursor < rangeStart ? rangeStart : cursor;
        var visibleEnd = naturalEnd > endExclusive ? endExclusive : naturalEnd;
        var inBucket = activities.Where(activity => ActivityDate(activity.StartedAtLocal, activity.StartedAtUtc) is { } date
            && date >= visibleStart && date < visibleEnd).ToArray();
        var label = selectedBucket == "month"
            ? cursor.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)
            : $"Week of {cursor:dd MMM yyyy}";
        bins.Add(new RunningConsistencyBucket(
            visibleStart,
            visibleEnd,
            label,
            inBucket.Where(activity => activity.DistanceMeters is not null).Sum(activity => activity.DistanceMeters!.Value) / 1000.0,
            inBucket.Length,
            inBucket.Sum(activity => activity.TimerTimeSeconds ?? activity.ElapsedTimeSeconds ?? 0)));
        cursor = naturalEnd;
    }

    return Results.Ok(new RunningConsistencyResponse(selectedPeriod, selectedBucket, rangeStart, endExclusive, bins));
});

app.MapGet("/api/trends/consistency/activities", async (
    DateOnly startDate,
    DateOnly endDate,
    GarminDbContext db,
    CancellationToken cancellationToken) =>
{
    if (endDate <= startDate) return Results.BadRequest("endDate must be after startDate.");
    var candidates = await db.Activities.AsNoTracking()
        .Where(activity => activity.ActivityType == "running" && !activity.IsHidden)
        .Select(activity => new RunningTrendActivity(
            activity.Id,
            activity.StartedAtLocal,
            activity.StartedAtUtc,
            activity.ActivityTitle,
            activity.DistanceMeters,
            activity.ElapsedTimeSeconds,
            activity.TimerTimeSeconds,
            activity.MovingTimeSeconds,
            activity.AverageHeartRateBpm,
            activity.MaximumHeartRateBpm,
            activity.AveragePaceSecondsPerKilometer,
            activity.AverageSpeedMetersPerSecond,
            activity.Calories,
            activity.LapCount ?? activity.Laps.Count,
            activity.SourceKind,
            activity.Records.Any()))
        .ToListAsync(cancellationToken);

    var items = candidates
        .Where(activity => ActivityDate(activity.StartedAtLocal, activity.StartedAtUtc) is { } date
            && date >= startDate && date < endDate)
        .OrderByDescending(activity => activity.StartedAtLocal ?? activity.StartedAtUtc)
        .Select(activity => new ActivityListItem(
            activity.Id,
            activity.StartedAtLocal ?? activity.StartedAtUtc,
            "running",
            activity.ActivityTitle,
            activity.DistanceMeters,
            activity.ElapsedTimeSeconds,
            activity.MovingTimeSeconds,
            activity.AverageHeartRateBpm,
            activity.MaximumHeartRateBpm,
            ResolvePace(activity.AveragePaceSecondsPerKilometer, activity.AverageSpeedMetersPerSecond,
                activity.DistanceMeters, activity.ElapsedTimeSeconds),
            activity.Calories,
            activity.LapCount,
            activity.SourceKind,
            activity.HasRecordSamples))
        .ToList();
    return Results.Ok(items);
});

app.MapGet("/api/trends/performance", async (
    int? distanceMeters,
    string? period,
    GarminDbContext db,
    CancellationToken cancellationToken) =>
{
    var selectedDistance = distanceMeters ?? 5000;
    if (!PerformanceEffortCalculator.DistancesMeters.Contains(selectedDistance))
        return Results.BadRequest("distanceMeters must be one of 1000, 2000, 5000, 6000, or 10000.");
    var selectedPeriod = string.IsNullOrWhiteSpace(period) ? "all" : period.ToLowerInvariant();
    if (selectedPeriod is not ("6m" or "1y" or "all"))
        return Results.BadRequest("period must be 6m, 1y, or all.");

    var today = DateOnly.FromDateTime(DateTime.Now);
    var endExclusive = today.AddDays(1);
    var query = from effort in db.ActivityPerformanceEfforts.AsNoTracking()
                join activity in db.Activities.AsNoTracking() on effort.ActivityId equals activity.Id
                where effort.DistanceMeters == selectedDistance
                    && effort.AlgorithmVersion == PerformanceEffortCalculator.CurrentAlgorithmVersion
                    && !activity.IsHidden
                select new PerformanceEffortPoint(
                    activity.Id,
                    activity.StartedAtLocal ?? activity.StartedAtUtc,
                    activity.ActivityTitle,
                    effort.DistanceMeters,
                    effort.EffortKind,
                    effort.DurationSeconds,
                    effort.StartElapsedSeconds,
                    effort.EndElapsedSeconds,
                    effort.ObservationCount,
                    effort.AlgorithmVersion);
    // SQLite cannot translate ordering through this positional record projection.
    // This result is already scoped to one distance, so sort the modest result set in memory.
    var allPoints = (await query.ToListAsync(cancellationToken))
        .OrderBy(point => point.StartedAt)
        .ThenBy(point => point.ActivityId)
        .ToList();
    var dated = allPoints.Where(point => point.StartedAt is not null).ToArray();
    var earliest = dated.Length == 0 ? today : DateOnly.FromDateTime(dated[0].StartedAt!.Value);
    var rangeStart = selectedPeriod switch { "6m" => today.AddMonths(-6), "1y" => today.AddYears(-1), _ => earliest };

    double? bestSoFar = null;
    var withBestFlags = dated.Select(point =>
    {
        var isBest = bestSoFar is null || point.DurationSeconds < bestSoFar.Value;
        if (isBest) bestSoFar = point.DurationSeconds;
        return point with { IsPersonalBest = isBest };
    }).Where(point => DateOnly.FromDateTime(point.StartedAt!.Value) >= rangeStart
        && DateOnly.FromDateTime(point.StartedAt.Value) < endExclusive).ToArray();
    return Results.Ok(new RunningPerformanceResponse(selectedPeriod, selectedDistance, rangeStart, endExclusive,
        withBestFlags, withBestFlags.Count(point => point.IsPersonalBest)));
});

app.MapPost("/api/trends/performance/rebuild", async (
    GarminDbContext db,
    CancellationToken cancellationToken) =>
{
    if (!await syncLock.WaitAsync(0, cancellationToken))
        return Results.Conflict(new { message = "Another import or trends rebuild is already running." });
    try
    {
        var activityIds = await db.Activities.AsNoTracking()
            .Where(activity => (activity.ActivityType == "running" || activity.Sport == 1)
                && activity.Records.Any())
            .Select(activity => activity.Id).ToListAsync(cancellationToken);
        var effortCount = 0;
        foreach (var activityId in activityIds)
        {
            var activity = await db.Activities.Include(item => item.Records)
                .SingleAsync(item => item.Id == activityId, cancellationToken);
            db.ActivityPerformanceEfforts.RemoveRange(db.ActivityPerformanceEfforts.Where(item => item.ActivityId == activityId));
            var efforts = PerformanceEffortCalculator.Calculate(activity);
            db.ActivityPerformanceEfforts.AddRange(efforts);
            effortCount += efforts.Count;
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
        return Results.Ok(new { activitiesProcessed = activityIds.Count, effortsUpdated = effortCount,
            algorithmVersion = PerformanceEffortCalculator.CurrentAlgorithmVersion });
    }
    finally { syncLock.Release(); }
});

app.MapPut("/api/activities/{id:long}/hidden", async (
    long id,
    ActivityVisibilityRequest request,
    GarminDbContext db,
    CancellationToken cancellationToken) =>
{
    var activity = await db.Activities.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (activity is null) return Results.NotFound();

    activity.IsHidden = request.IsHidden;
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
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

static double? ResolvePace(double? storedPace, double? averageSpeed, double? distance, double? elapsedTime)
{
    if (storedPace is not > 3600) return storedPace;
    if (averageSpeed is >= 0.5 and <= 15) return 1000.0 / averageSpeed.Value;
    if (distance is > 0 && elapsedTime is > 0) return elapsedTime.Value * 1000.0 / distance.Value;
    return storedPace;
}

static DateOnly? ActivityDate(DateTime? localStart, DateTime? utcStart)
{
    var start = localStart ?? utcStart;
    return start is null ? null : DateOnly.FromDateTime(start.Value);
}

static (double? Gain, double? Loss, double? Net) EstimateElevationChange(IReadOnlyList<SegmentRecord> records)
{
    var altitudes = records.Select(record => record.AltitudeMeters).ToArray();
    var smoothed = new double?[altitudes.Length];
    for (var index = 0; index < altitudes.Length; index++)
    {
        var start = Math.Max(0, index - 2);
        var window = altitudes.Skip(start)
            .Take(Math.Min(altitudes.Length, index + 3) - start)
            .Where(value => value is not null).Select(value => value!.Value).Order().ToArray();
        if (window.Length > 0) smoothed[index] = window[window.Length / 2];
    }

    var valid = smoothed.Where(value => value is not null).Select(value => value!.Value).ToArray();
    if (valid.Length < 2) return (null, null, null);
    var gain = 0.0;
    var loss = 0.0;
    double? anchor = null;
    foreach (var altitude in smoothed)
    {
        if (altitude is not { } current)
        {
            anchor = null;
            continue;
        }
        if (anchor is null)
        {
            anchor = current;
            continue;
        }

        var change = current - anchor.Value;
        if (change >= 2.0)
        {
            gain += change;
            anchor = current;
        }
        else if (change <= -2.0)
        {
            loss += -change;
            anchor = current;
        }
    }
    return (gain, loss, valid[^1] - valid[0]);
}

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

internal sealed record ActivityVisibilityRequest(bool IsHidden);

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
    int RecordNumber,
    DateTime? TimestampUtc,
    double? DistanceMeters,
    double? SpeedMetersPerSecond,
    int? HeartRateBpm,
    double? AltitudeMeters);

internal sealed record SegmentRecord(
    int RecordNumber,
    DateTime? TimestampUtc,
    double? DistanceMeters,
    int? HeartRateBpm,
    double? AltitudeMeters);

internal sealed record ActivitySegmentSummary(
    double? StartElapsedSeconds,
    double? EndElapsedSeconds,
    double? StartDistanceMeters,
    double? EndDistanceMeters,
    double? DistanceMeters,
    double? ElevationGainMeters,
    double? ElevationLossMeters,
    double? NetElevationChangeMeters,
    double? AveragePaceSecondsPerKilometer,
    int? StartHeartRateBpm,
    int? EndHeartRateBpm,
    int? PeakHeartRateBpm,
    double? AverageHeartRateBpm,
    double? DurationSeconds);

internal sealed record RunningTrendActivity(
    long Id,
    DateTime? StartedAtLocal,
    DateTime? StartedAtUtc,
    string? ActivityTitle,
    double? DistanceMeters,
    double? ElapsedTimeSeconds,
    double? TimerTimeSeconds,
    double? MovingTimeSeconds,
    int? AverageHeartRateBpm,
    int? MaximumHeartRateBpm,
    double? AveragePaceSecondsPerKilometer,
    double? AverageSpeedMetersPerSecond,
    int? Calories,
    int LapCount,
    string SourceKind,
    bool HasRecordSamples);

internal sealed record RunningConsistencyBucket(
    DateOnly StartDate,
    DateOnly EndDate,
    string Label,
    double DistanceKm,
    int RunCount,
    double TotalTimerTimeSeconds);

internal sealed record RunningConsistencyResponse(
    string Period,
    string Bucket,
    DateOnly RangeStart,
    DateOnly RangeEnd,
    IReadOnlyList<RunningConsistencyBucket> Buckets);

internal sealed record PerformanceEffortPoint(
    long ActivityId,
    DateTime? StartedAt,
    string? Title,
    int DistanceMeters,
    string EffortKind,
    double DurationSeconds,
    double StartElapsedSeconds,
    double EndElapsedSeconds,
    int ObservationCount,
    int AlgorithmVersion,
    bool IsPersonalBest = false);

internal sealed record RunningPerformanceResponse(
    string Period,
    int DistanceMeters,
    DateOnly RangeStart,
    DateOnly RangeEnd,
    IReadOnlyList<PerformanceEffortPoint> Points,
    int PersonalBestCount);

internal sealed record ActivityDetailsResponse(
    ActivityDetails Activity,
    IReadOnlyList<ActivityLapItem> Laps,
    IReadOnlyList<ActivitySample> Records);

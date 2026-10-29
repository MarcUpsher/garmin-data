using Garmin.Api.Contracts;
using Garmin.Data;
using Garmin.Data.Analysis;
using Garmin.Data.Importing;
using Microsoft.EntityFrameworkCore;
using static Garmin.Data.Analysis.ActivityMetrics;

namespace Garmin.Api.Endpoints;

internal static class TrendsEndpoints
{
    public static void MapTrendsEndpoints(this IEndpointRouteBuilder app, SemaphoreSlim syncLock)
    {
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
                    .Where(activity => activity.ActivityType == "running" || activity.Sport == 1)
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

        app.MapGet("/api/trends/sleep", async (string? period, GarminDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TrendDateRange.IsValidPeriod(period)) return Results.BadRequest("period must be 6m, 1y, or all.");
            var earliest = await db.SleepRecords.OrderBy(x => x.CalendarDate)
                .Select(x => (DateOnly?)x.CalendarDate).FirstOrDefaultAsync(cancellationToken);
            var range = TrendDateRange.Create(period, DateOnly.FromDateTime(DateTime.Today), earliest);
            var records = await db.SleepRecords.AsNoTracking()
                .Where(x => x.CalendarDate >= range.RangeStart && x.CalendarDate < range.RangeEnd)
                .OrderBy(x => x.CalendarDate).ToListAsync(cancellationToken);
            var points = SleepTrendCalculator.Calculate(records);
            return Results.Ok(new { points, recordCount = records.Count, range.RangeStart, range.RangeEnd });
        });

        app.MapGet("/api/trends/vo2-max", async (string? period, string? sport, GarminDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TrendDateRange.IsValidPeriod(period)) return Results.BadRequest("period must be 6m, 1y, or all.");
            var query = db.Vo2MaxRecords.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(sport)) query = query.Where(x => x.Sport == sport);
            var earliest = await query.OrderBy(x => x.CalendarDate)
                .Select(x => (DateOnly?)x.CalendarDate).FirstOrDefaultAsync(cancellationToken);
            var range = TrendDateRange.Create(period, DateOnly.FromDateTime(DateTime.Today), earliest);
            var rows = await query.Where(x => x.CalendarDate >= range.RangeStart && x.CalendarDate < range.RangeEnd)
                .OrderBy(x => x.CalendarDate).ThenBy(x => x.Sport).ThenBy(x => x.Id).ToListAsync(cancellationToken);
            var points = rows.Select(x => new Vo2TrendPoint(
                x.Id, x.CalendarDate.ToString("yyyy-MM-dd"), x.Vo2MaxValue, x.Sport, x.SubSport, x.DeviceId, x.MaxMet, x.IsCalibrated)).ToArray();
            return Results.Ok(new { points, recordCount = points.Length, range.RangeStart, range.RangeEnd });
        });
    }

    private sealed record RunningTrendActivity(
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
}

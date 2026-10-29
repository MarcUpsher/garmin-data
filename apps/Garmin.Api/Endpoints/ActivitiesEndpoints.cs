using Garmin.Api.Contracts;
using Garmin.Data;
using Garmin.Data.Analysis;
using Garmin.Data.Importing;
using Microsoft.EntityFrameworkCore;
using static Garmin.Data.Analysis.ActivityMetrics;

namespace Garmin.Api.Endpoints;

internal static class ActivitiesEndpoints
{
    public static void MapActivitiesEndpoints(this IEndpointRouteBuilder app)
    {
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
            var elevationChange = EstimateElevationChange(records.Select(record => record.AltitudeMeters));

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
    }

    private sealed record SegmentRecord(
        int RecordNumber,
        DateTime? TimestampUtc,
        double? DistanceMeters,
        int? HeartRateBpm,
        double? AltitudeMeters);
}

namespace Garmin.Api.Contracts;

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

internal sealed record ActivityDetailsResponse(
    ActivityDetails Activity,
    IReadOnlyList<ActivityLapItem> Laps,
    IReadOnlyList<ActivitySample> Records);

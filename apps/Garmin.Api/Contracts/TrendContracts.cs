namespace Garmin.Api.Contracts;

internal sealed record Vo2TrendPoint(long Id, string Date, double? Value, string? Sport, string? SubSport, string? DeviceId, double? MaxMet, bool? IsCalibrated);

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

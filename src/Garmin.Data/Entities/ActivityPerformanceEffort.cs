namespace Garmin.Data.Entities;

/// <summary>A cached best rolling-distance effort derived from timestamped FIT samples.</summary>
public sealed class ActivityPerformanceEffort
{
    public long Id { get; set; }
    public long ActivityId { get; set; }
    public int DistanceMeters { get; set; }
    /// <summary>Rolling segment from records or an activity whose full distance matches the target.</summary>
    public required string EffortKind { get; set; }
    public double DurationSeconds { get; set; }
    public double StartElapsedSeconds { get; set; }
    public double EndElapsedSeconds { get; set; }
    public int ObservationCount { get; set; }
    public int AlgorithmVersion { get; set; }
    public Activity Activity { get; set; } = null!;
}

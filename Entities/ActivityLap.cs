namespace Garmin.Data.Entities;

/// <summary>An optional FIT lap/split summary. Activities with no lap messages have no rows here.</summary>
public sealed class ActivityLap
{
    public long Id { get; set; }
    public long ActivityId { get; set; }
    public int LapNumber { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public double? ElapsedTimeSeconds { get; set; }
    public double? TimerTimeSeconds { get; set; }
    public double? DistanceMeters { get; set; }
    public int? Calories { get; set; }
    public int? AverageHeartRateBpm { get; set; }
    public int? MaximumHeartRateBpm { get; set; }
    public double? AverageSpeedMetersPerSecond { get; set; }
    public double? MaximumSpeedMetersPerSecond { get; set; }

    public Activity Activity { get; set; } = null!;
}

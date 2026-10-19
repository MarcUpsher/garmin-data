namespace Garmin.Data.Entities;

/// <summary>A time-stamped sample from a FIT Record message.</summary>
public sealed class ActivityRecord
{
    public long Id { get; set; }
    public long ActivityId { get; set; }
    public int RecordNumber { get; set; }
    public DateTime? TimestampUtc { get; set; }
    public double? LatitudeDegrees { get; set; }
    public double? LongitudeDegrees { get; set; }
    public double? AltitudeMeters { get; set; }
    public int? HeartRateBpm { get; set; }
    public int? CadenceRpm { get; set; }
    public double? DistanceMeters { get; set; }
    public double? SpeedMetersPerSecond { get; set; }
    public int? PowerWatts { get; set; }
    public int? TemperatureCelsius { get; set; }

    public Activity Activity { get; set; } = null!;
}

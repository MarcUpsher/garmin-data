namespace Garmin.Data.Entities;

/// <summary>A decoded activity and the original FIT file it came from.</summary>
public sealed class Activity
{
    public long Id { get; set; }

    /// <summary>Import source format, such as FIT or GarminConnectCsv.</summary>
    public required string SourceKind { get; set; }
    /// <summary>Lowercase SHA-256 hex digest used as the idempotency key for this source record.</summary>
    public required string SourceSha256 { get; set; }
    /// <summary>FIT content digest. Null for summary-only sources such as CSV.</summary>
    public string? FitSha256 { get; set; }
    public required string OriginalFileName { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime ImportedAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    /// <summary>Local wall-clock start time from exports that do not include a timezone.</summary>
    public DateTime? StartedAtLocal { get; set; }
    public string? ActivityType { get; set; }
    public string? ActivityTitle { get; set; }
    public int? Sport { get; set; }
    public int? SubSport { get; set; }
    public double? ElapsedTimeSeconds { get; set; }
    public double? TimerTimeSeconds { get; set; }
    public double? DistanceMeters { get; set; }
    public int? Calories { get; set; }
    public int? AverageHeartRateBpm { get; set; }
    public int? MaximumHeartRateBpm { get; set; }
    public double? AverageSpeedMetersPerSecond { get; set; }
    public double? MaximumSpeedMetersPerSecond { get; set; }
    public double? AverageCadenceRpm { get; set; }
    public double? MaximumCadenceRpm { get; set; }
    public double? AveragePaceSecondsPerKilometer { get; set; }
    public double? BestPaceSecondsPerKilometer { get; set; }
    public double? TotalAscentMeters { get; set; }
    public double? TotalDescentMeters { get; set; }
    public double? AverageStrideLengthMeters { get; set; }
    public double? AerobicTrainingEffect { get; set; }
    public int? Steps { get; set; }
    public int? LapCount { get; set; }
    public double? BestLapTimeSeconds { get; set; }
    public double? MovingTimeSeconds { get; set; }
    public double? MinimumElevationMeters { get; set; }
    public double? MaximumElevationMeters { get; set; }
    public double? TrainingStressScore { get; set; }
    public bool? IsFavorite { get; set; }

    public ICollection<ActivityLap> Laps { get; set; } = new List<ActivityLap>();
    public ICollection<ActivityRecord> Records { get; set; } = new List<ActivityRecord>();
    public ICollection<ActivityExtraField> ExtraFields { get; set; } = new List<ActivityExtraField>();
    public ActivitySourceFile? SourceFile { get; set; }
}

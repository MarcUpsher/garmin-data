namespace Garmin.Data.Entities;

public sealed class DailyWellnessRecord
{
    public long Id { get; set; }
    public required string SourceRecordHash { get; set; }
    public required string SourceFileName { get; set; }
    public int ImportVersion { get; set; }
    public DateOnly CalendarDate { get; set; }
    public string? Source { get; set; }
    public int? TotalSteps { get; set; }
    public int? DailyStepGoal { get; set; }
    public double? TotalDistanceMeters { get; set; }
    public int? TotalKilocalories { get; set; }
    public int? ActiveKilocalories { get; set; }
    public int? RestingHeartRateBpm { get; set; }
    public int? CurrentDayRestingHeartRateBpm { get; set; }
    public int? AllDayStress { get; set; }
    /// <summary>Garmin's MOSTRECENT value for this day's export; distinct from ENDOFDAY.</summary>
    public int? BodyBattery { get; set; }
    public int? BodyBatteryCharged { get; set; }
    public int? BodyBatteryDrained { get; set; }
    public int? BodyBatteryHigh { get; set; }
    public int? BodyBatteryLow { get; set; }
    public int? BodyBatteryEndOfDay { get; set; }
    public int? ModerateIntensityMinutes { get; set; }
    public int? VigorousIntensityMinutes { get; set; }
    public double? FloorsAscendedMeters { get; set; }
    public double? FloorsDescendedMeters { get; set; }
    /// <summary>Average waking respiration in breaths per minute.</summary>
    public double? Respiration { get; set; }
    public double? HydrationMillilitres { get; set; }
}

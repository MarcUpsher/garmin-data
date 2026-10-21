namespace Garmin.Data.Entities;

public sealed class DailyWellnessRecord
{
    public long Id { get; set; }
    public required string SourceRecordHash { get; set; }
    public required string SourceFileName { get; set; }
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
    public int? BodyBattery { get; set; }
    public int? ModerateIntensityMinutes { get; set; }
    public int? VigorousIntensityMinutes { get; set; }
    public int? FloorsAscended { get; set; }
    public int? FloorsDescended { get; set; }
    public double? Respiration { get; set; }
    public double? HydrationMillilitres { get; set; }
}

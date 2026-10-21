namespace Garmin.Data.Entities;

public sealed class SleepRecord
{
    public long Id { get; set; }
    public required string SourceRecordHash { get; set; }
    public required string SourceFileName { get; set; }
    public DateOnly CalendarDate { get; set; }
    public DateTime? SleepStartUtc { get; set; }
    public DateTime? SleepEndUtc { get; set; }
    public int? DeepSeconds { get; set; }
    public int? LightSeconds { get; set; }
    public int? RemSeconds { get; set; }
    public int? AwakeSeconds { get; set; }
    public int? UnmeasurableSeconds { get; set; }
    public double? AverageRespiration { get; set; }
    public double? LowestRespiration { get; set; }
    public double? HighestRespiration { get; set; }
    public string? WindowConfirmationType { get; set; }
    public bool? IsRetroactive { get; set; }
}

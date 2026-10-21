namespace Garmin.Data.Entities;

public sealed class Vo2MaxRecord
{
    public long Id { get; set; }
    public required string SourceRecordHash { get; set; }
    public required string SourceFileName { get; set; }
    public DateOnly CalendarDate { get; set; }
    public string? Sport { get; set; }
    public string? SubSport { get; set; }
    public string? DeviceId { get; set; }
    public double? Vo2MaxValue { get; set; }
    public double? MaxMet { get; set; }
    public string? MaxMetCategory { get; set; }
    public bool? IsCalibrated { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

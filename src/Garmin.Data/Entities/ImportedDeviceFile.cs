namespace Garmin.Data.Entities;

/// <summary>Metadata checkpoint allowing unchanged files on a connected device to be skipped.</summary>
public sealed class ImportedDeviceFile
{
    public long Id { get; set; }
    /// <summary>Canonical path to the mounted device's GARMIN directory.</summary>
    public required string DeviceKey { get; set; }
    /// <summary>Path within the GARMIN directory, such as Activity/example.fit.</summary>
    public required string RelativePath { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime? LastModifiedUtc { get; set; }
    public DateTime LastVerifiedAtUtc { get; set; }
    public long ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;
}

namespace Garmin.Data.Entities;

/// <summary>An additional raw source file linked to a canonical activity, such as its FIT file.</summary>
public sealed class ActivitySourceEvidence
{
    public long Id { get; set; }
    public long ActivityId { get; set; }
    public required string SourceFormat { get; set; }
    public required string SourceSha256 { get; set; }
    public required string OriginalFileName { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime ImportedAtUtc { get; set; }
    public required byte[] SourceBytes { get; set; }

    public Activity Activity { get; set; } = null!;
}

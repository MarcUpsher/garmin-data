namespace Garmin.Data.Entities;

/// <summary>Original source payload, split out so normal activity queries do not load the BLOB.</summary>
public sealed class ActivitySourceFile
{
    public long ActivityId { get; set; }
    public required string SourceFormat { get; set; }
    public required byte[] SourceBytes { get; set; }

    public Activity Activity { get; set; } = null!;
}

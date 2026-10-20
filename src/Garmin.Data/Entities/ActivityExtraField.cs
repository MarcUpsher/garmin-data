namespace Garmin.Data.Entities;

/// <summary>
/// Raw bytes for FIT fields not yet mapped to first-class columns. Keeping message and field
/// identifiers lets a later importer/profile upgrade decode these without losing source data.
/// </summary>
public sealed class ActivityExtraField
{
    public long Id { get; set; }
    public long ActivityId { get; set; }
    public int GlobalMessageNumber { get; set; }
    public int MessageNumber { get; set; }
    public int FieldNumber { get; set; }
    /// <summary>-1 for standard fields; otherwise the FIT developer-data index.</summary>
    public int DeveloperDataIndex { get; set; } = -1;
    public int? BaseType { get; set; }
    public required byte[] RawValue { get; set; }

    public Activity Activity { get; set; } = null!;
}

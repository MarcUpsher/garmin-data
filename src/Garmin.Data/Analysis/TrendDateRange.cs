namespace Garmin.Data.Analysis;

/// <summary>Calendar dates with an exclusive end, shared by wellness trend queries.</summary>
public sealed record TrendDateRange(DateOnly RangeStart, DateOnly RangeEnd)
{
    public static bool IsValidPeriod(string? period) => period is null or "6m" or "1y" or "all";

    public static TrendDateRange Create(string? period, DateOnly today, DateOnly? earliest)
    {
        var start = period switch
        {
            null or "6m" => today.AddMonths(-6),
            "1y" => today.AddYears(-1),
            "all" => earliest is { } date && date < today ? date : today,
            _ => throw new ArgumentException("period must be 6m, 1y, or all.", nameof(period))
        };
        return new(start, today.AddDays(1));
    }
}

using Garmin.Data.Entities;

namespace Garmin.Data.Analysis;

public sealed record SleepMonthlySummary(string Month, double? AverageSleepHours, int NightCount,
    double? AverageDeepSeconds, double? AverageLightSeconds, double? AverageRemSeconds, double? AverageRespiration,
    int ExcludedNightCount);

public static class SleepTrendCalculator
{
    public static IReadOnlyList<SleepMonthlySummary> Calculate(IEnumerable<SleepRecord> records) => records
        .GroupBy(x => new { x.CalendarDate.Year, x.CalendarDate.Month })
        .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month).Select(group =>
        {
            // Older devices do not record REM separately. Deep and light must both
            // be present; absent or zero-duration nights are missing measurements.
            var measured = group.Where(x => x.DeepSeconds is >= 0 && x.LightSeconds is >= 0
                && x.RemSeconds is not < 0
                && (long)x.DeepSeconds.Value + x.LightSeconds.Value + (x.RemSeconds ?? 0) > 0).ToArray();
            double? Average(Func<SleepRecord, double?> selector) => measured.Select(selector).Average();
            return new SleepMonthlySummary($"{group.Key.Year:D4}-{group.Key.Month:D2}-01",
                Average(x => ((double)x.DeepSeconds!.Value + x.LightSeconds!.Value + (x.RemSeconds ?? 0)) / 3600),
                measured.Length, Average(x => x.DeepSeconds), Average(x => x.LightSeconds), Average(x => x.RemSeconds),
                Average(x => x.AverageRespiration is > 0 ? x.AverageRespiration : null), group.Count() - measured.Length);
        }).ToArray();
}

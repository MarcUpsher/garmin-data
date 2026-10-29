namespace Garmin.Data.Analysis;

public static class ActivityMetrics
{
    public static double? ResolvePace(double? storedPace, double? averageSpeed, double? distance, double? elapsedTime)
    {
        if (storedPace is not > 3600) return storedPace;
        if (averageSpeed is >= 0.5 and <= 15) return 1000.0 / averageSpeed.Value;
        if (distance is > 0 && elapsedTime is > 0) return elapsedTime.Value * 1000.0 / distance.Value;
        return storedPace;
    }

    public static DateOnly? ActivityDate(DateTime? localStart, DateTime? utcStart)
    {
        var start = localStart ?? utcStart;
        return start is null ? null : DateOnly.FromDateTime(start.Value);
    }

    public static (double? Gain, double? Loss, double? Net) EstimateElevationChange(IEnumerable<double?> altitudeSamples)
    {
        var altitudes = altitudeSamples.ToArray();
        var smoothed = new double?[altitudes.Length];
        for (var index = 0; index < altitudes.Length; index++)
        {
            var start = Math.Max(0, index - 2);
            var window = altitudes.Skip(start)
                .Take(Math.Min(altitudes.Length, index + 3) - start)
                .Where(value => value is not null).Select(value => value!.Value).Order().ToArray();
            if (window.Length > 0) smoothed[index] = window[window.Length / 2];
        }

        var valid = smoothed.Where(value => value is not null).Select(value => value!.Value).ToArray();
        if (valid.Length < 2) return (null, null, null);
        var gain = 0.0;
        var loss = 0.0;
        double? anchor = null;
        foreach (var altitude in smoothed)
        {
            if (altitude is not { } current)
            {
                anchor = null;
                continue;
            }
            if (anchor is null)
            {
                anchor = current;
                continue;
            }

            var change = current - anchor.Value;
            if (change >= 2.0)
            {
                gain += change;
                anchor = current;
            }
            else if (change <= -2.0)
            {
                loss += -change;
                anchor = current;
            }
        }
        return (gain, loss, valid[^1] - valid[0]);
    }
}

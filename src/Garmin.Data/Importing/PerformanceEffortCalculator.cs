using Garmin.Data.Entities;

namespace Garmin.Data.Importing;

/// <summary>Deterministic rolling-distance effort extraction for timestamped running records.</summary>
public static class PerformanceEffortCalculator
{
    public const int CurrentAlgorithmVersion = 3;
    public static readonly int[] DistancesMeters = [1000, 2000, 5000, 6000, 10000];

    private const double MinSpeedMetersPerSecond = 0.8;
    private const double MaxSpeedMetersPerSecond = 8.5;
    private const int MinObservations = 5;
    // Forerunner 55 FIT record timestamps commonly vary between 1 and 8 seconds.
    // Ten seconds tolerates that cadence while still splitting real recording gaps.
    private static readonly TimeSpan MaxSampleGap = TimeSpan.FromSeconds(10);

    public static IReadOnlyList<ActivityPerformanceEffort> Calculate(Activity activity)
    {
        if (activity.ActivityType != "running" && activity.Sport != 1) return [];
        var records = activity.Records
            .Where(x => x.TimestampUtc is not null && x.DistanceMeters is >= 0)
            .OrderBy(x => x.RecordNumber)
            .ToArray();
        var chunks = new List<List<ActivityRecord>>();
        if (records.Length >= MinObservations)
        {
            var chunk = new List<ActivityRecord> { records[0] };
            for (var i = 1; i < records.Length; i++)
            {
                var previous = records[i - 1];
                var current = records[i];
                var seconds = (current.TimestampUtc!.Value - previous.TimestampUtc!.Value).TotalSeconds;
                var distance = current.DistanceMeters!.Value - previous.DistanceMeters!.Value;
                var speed = seconds > 0 ? distance / seconds : 0;
                if (seconds > 0 && seconds <= MaxSampleGap.TotalSeconds && distance > 0
                    && speed >= MinSpeedMetersPerSecond && speed <= MaxSpeedMetersPerSecond)
                {
                    chunk.Add(current);
                }
                else
                {
                    if (chunk.Count >= MinObservations) chunks.Add(chunk);
                    chunk = [current];
                }
            }
            if (chunk.Count >= MinObservations) chunks.Add(chunk);
        }

        var activityStart = activity.StartedAtUtc ?? (records.Length > 0
            ? records[0].TimestampUtc!.Value
            : DateTime.MinValue);
        var results = new List<ActivityPerformanceEffort>();
        foreach (var target in DistancesMeters)
        {
            Candidate? best = null;
            foreach (var samples in chunks)
            {
                var endIndex = 1;
                for (var startIndex = 0; startIndex < samples.Count - MinObservations + 1; startIndex++)
                {
                    endIndex = Math.Max(endIndex, startIndex + 1);
                    var startDistance = samples[startIndex].DistanceMeters!.Value;
                    var targetDistance = startDistance + target;
                    while (endIndex < samples.Count && samples[endIndex].DistanceMeters!.Value < targetDistance)
                        endIndex++;
                    if (endIndex >= samples.Count) break;
                    if (endIndex - startIndex + 1 < MinObservations) continue;

                    var before = samples[endIndex - 1];
                    var after = samples[endIndex];
                    var span = after.DistanceMeters!.Value - before.DistanceMeters!.Value;
                    if (span <= 0) continue;
                    var fraction = (targetDistance - before.DistanceMeters.Value) / span;
                    var endTime = before.TimestampUtc!.Value
                        + TimeSpan.FromTicks((long)((after.TimestampUtc!.Value - before.TimestampUtc.Value).Ticks * fraction));
                    var startTime = samples[startIndex].TimestampUtc!.Value;
                    var duration = (endTime - startTime).TotalSeconds;
                    if (duration <= 0 || duration > target / MinSpeedMetersPerSecond) continue;
                    if (best is null || duration < best.DurationSeconds)
                        best = new Candidate(duration, (startTime - activityStart).TotalSeconds,
                            (endTime - activityStart).TotalSeconds, endIndex - startIndex + 1);
                }
            }

            if (best is not null)
                results.Add(new ActivityPerformanceEffort
                {
                    Activity = activity,
                    DistanceMeters = target,
                    EffortKind = "RollingSegment",
                    DurationSeconds = best.DurationSeconds,
                    StartElapsedSeconds = best.StartElapsedSeconds,
                    EndElapsedSeconds = best.EndElapsedSeconds,
                    ObservationCount = best.ObservationCount,
                    AlgorithmVersion = CurrentAlgorithmVersion
                });
            else if (activity.DistanceMeters is { } fullDistance
                && Math.Abs(fullDistance - target) <= Math.Max(5, target * .001)
                && (activity.ElapsedTimeSeconds ?? activity.TimerTimeSeconds) is > 0)
            {
                // Rolling efforts use timestamp elapsed time. Prefer the same clock
                // for whole activities; timer time is a fallback for sparse exports.
                var duration = activity.ElapsedTimeSeconds ?? activity.TimerTimeSeconds!.Value;
                if (fullDistance / duration is >= MinSpeedMetersPerSecond and <= MaxSpeedMetersPerSecond)
                    results.Add(new ActivityPerformanceEffort
                    {
                        Activity = activity,
                        DistanceMeters = target,
                        EffortKind = "RecordedActivity",
                        DurationSeconds = duration,
                        StartElapsedSeconds = 0,
                        EndElapsedSeconds = duration,
                        ObservationCount = 0,
                        AlgorithmVersion = CurrentAlgorithmVersion
                    });
            }
        }
        return results;
    }

    private sealed record Candidate(double DurationSeconds, double StartElapsedSeconds,
        double EndElapsedSeconds, int ObservationCount);
}

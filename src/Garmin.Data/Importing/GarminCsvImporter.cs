using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garmin.Data;
using Garmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garmin.Data.Importing;

public static class GarminCsvImporter
{
    private static readonly string[] RequiredColumns =
    [
        "Activity Type", "Date", "Title", "Distance", "Calories", "Time", "Avg HR", "Max HR",
        "Aerobic TE", "Avg Run Cadence", "Max Run Cadence", "Avg Pace", "Best Pace", "Total Ascent",
        "Total Descent", "Avg Stride Length", "Training Stress Score®", "Steps", "Best Lap Time",
        "Number of Laps", "Moving Time", "Elapsed Time", "Min Elevation", "Max Elevation", "Favorite"
    ];

    public static async Task<(int Imported, int Skipped, int Failed, int RecordCount)> ImportAsync(
        string path, GarminDbContext db)
    {
        var content = await File.ReadAllTextAsync(path, Encoding.UTF8);
        var rows = ParseCsv(content).ToArray();
        if (rows.Length < 2) throw new InvalidDataException("CSV has no activity rows.");

        var headers = rows[0];
        var indexes = headers.Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);
        var missing = RequiredColumns.Where(x => !indexes.ContainsKey(x)).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"CSV is missing required columns: {string.Join(", ", missing)}");

        var existing = await db.Activities.Select(x => x.SourceSha256).ToHashSetAsync();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var imported = 0;
        var skipped = 0;
        var failed = 0;
        var sourceName = Path.GetFileName(path);

        for (var rowIndex = 1; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            try
            {
                if (row.Length != headers.Length)
                    throw new InvalidDataException($"Expected {headers.Length} cells, found {row.Length}.");

                var values = headers.Select((header, index) => (header, value: row[index]))
                    .ToDictionary(x => x.header, x => x.value, StringComparer.Ordinal);
                var rawRow = JsonSerializer.SerializeToUtf8Bytes(values);
                var rowKey = Convert.ToHexStringLower(SHA256.HashData(rawRow));
                occurrences.TryGetValue(rowKey, out var occurrence);
                occurrences[rowKey] = occurrence + 1;
                var sourceHashInput = Encoding.UTF8.GetBytes($"{rowKey}:{occurrence}");
                var sourceHash = Convert.ToHexStringLower(SHA256.HashData(sourceHashInput));
                if (existing.Contains(sourceHash))
                {
                    skipped++;
                    continue;
                }

                var localStart = ParseLocalDate(values["Date"]);
                var distanceKm = ParseNumber(values["Distance"]);
                var elapsed = ParseDuration(values["Elapsed Time"]);
                var moving = ParseDuration(values["Moving Time"]);
                var avgPace = ParsePace(values["Avg Pace"]);
                var activity = new Activity
                {
                    SourceKind = "GarminConnectCsv",
                    SourceSha256 = sourceHash,
                    OriginalFileName = sourceName,
                    FileSizeBytes = rawRow.LongLength,
                    ImportedAtUtc = DateTime.UtcNow,
                    StartedAtLocal = localStart,
                    ActivityType = Value(values, "Activity Type"),
                    ActivityTitle = Value(values, "Title"),
                    ElapsedTimeSeconds = elapsed,
                    TimerTimeSeconds = ParseDuration(values["Time"]),
                    MovingTimeSeconds = moving,
                    DistanceMeters = distanceKm is null ? null : distanceKm * 1000.0,
                    Calories = ParseInteger(values["Calories"]),
                    AverageHeartRateBpm = ParseInteger(values["Avg HR"]),
                    MaximumHeartRateBpm = ParseInteger(values["Max HR"]),
                    AverageSpeedMetersPerSecond = distanceKm is > 0 && moving is > 0
                        ? distanceKm.Value * 1000.0 / moving.Value
                        : null,
                    AverageCadenceRpm = ParseNumber(values["Avg Run Cadence"]),
                    MaximumCadenceRpm = ParseNumber(values["Max Run Cadence"]),
                    AveragePaceSecondsPerKilometer = avgPace,
                    BestPaceSecondsPerKilometer = ParsePace(values["Best Pace"]),
                    TotalAscentMeters = ParseNumber(values["Total Ascent"]),
                    TotalDescentMeters = ParseNumber(values["Total Descent"]),
                    AverageStrideLengthMeters = ParseNumber(values["Avg Stride Length"]),
                    AerobicTrainingEffect = ParseNumber(values["Aerobic TE"]),
                    TrainingStressScore = ParseNumber(values["Training Stress Score®"]),
                    Steps = ParseInteger(values["Steps"]),
                    LapCount = ParseInteger(values["Number of Laps"]),
                    BestLapTimeSeconds = ParseDuration(values["Best Lap Time"]),
                    MinimumElevationMeters = ParseNumber(values["Min Elevation"]),
                    MaximumElevationMeters = ParseNumber(values["Max Elevation"]),
                    IsFavorite = bool.TryParse(Value(values, "Favorite"), out var favorite) ? favorite : null,
                    SourceFile = new ActivitySourceFile { SourceFormat = "GarminConnectCsvRowJson", SourceBytes = rawRow }
                };

                db.Activities.Add(activity);
                existing.Add(sourceHash);
                imported++;
            }
            catch (Exception ex) when (ex is InvalidDataException or FormatException or ArgumentException)
            {
                failed++;
                Console.Error.WriteLine($"Skipped CSV row {rowIndex + 1}: {ex.Message}");
            }
        }

        await db.SaveChangesAsync();
        return (imported, skipped, failed, imported);
    }

    private static string? Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) && value != "--" ? value : null;

    private static double? ParseNumber(string value)
    {
        var normalized = value.Replace(",", "", StringComparison.Ordinal).Trim();
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && double.IsFinite(result) ? result : null;
    }

    private static int? ParseInteger(string value)
    {
        var normalized = value.Replace(",", "", StringComparison.Ordinal).Trim();
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static DateTime? ParseLocalDate(string value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var result)
            ? DateTime.SpecifyKind(result, DateTimeKind.Unspecified)
            : null;

    private static double? ParseDuration(string value) =>
        TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var result) && result >= TimeSpan.Zero
            ? result.TotalSeconds
            : null;

    private static double? ParsePace(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('-') || value == "--") return null;
        var parts = value.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || !double.TryParse(parts[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)
            || minutes < 0 || seconds < 0 || seconds >= 60)
            return null;
        return minutes * 60.0 + seconds;
    }

    private static IEnumerable<string[]> ParseCsv(string content)
    {
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < content.Length && content[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                row.Add(cell.ToString());
                cell.Clear();
                yield return row.ToArray();
                row.Clear();
            }
            else cell.Append(c);
        }

        if (quoted) throw new InvalidDataException("CSV ends inside a quoted cell.");
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            yield return row.ToArray();
        }
    }
}

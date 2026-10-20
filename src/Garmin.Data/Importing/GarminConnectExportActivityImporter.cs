using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garmin.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Garmin.Data.Importing;

public sealed record GarminExportActivityImportResult(
    int SummariesImported,
    int FitFilesLinked,
    int FitOnlyActivitiesImported,
    int SupplementalFitFilesLinked,
    int FitFilesSkipped,
    int Failed,
    long RecordCount,
    long LapCount,
    IReadOnlyList<string> Errors);

/// <summary>Imports Garmin Connect activity summaries and links matching FIT files to them.</summary>
public static class GarminConnectExportActivityImporter
{
    // Confirmed by exact-time comparison of export rows with the existing CSV/FIT imports.
    private const double ExportDistanceUnitsPerMeter = 100.0;
    private const double ExportSpeedUnitsPerMetersPerSecond = 10.0;
    private const double ExportDurationUnitsPerSecond = 1000.0;
    private const double ExportEnergyUnitsPerKilocalorie = 4.19002;
    private const double ExportRunCadenceUnitsPerStepPerMinute = 0.5;
    private const double ExportStrideUnitsPerMeter = 100.0;

    public static async Task<GarminExportActivityImportResult> ImportAsync(
        string diConnectDirectory,
        string catalogPath,
        GarminDbContext db,
        string? supplementDatabasePath = null,
        CancellationToken cancellationToken = default)
    {
        var fitnessDirectory = Path.Combine(diConnectDirectory, "DI-Connect-Fitness");
        var archivesDirectory = Path.Combine(diConnectDirectory, "DI-Connect-Uploaded-Files");
        if (!Directory.Exists(fitnessDirectory)) throw new DirectoryNotFoundException(fitnessDirectory);
        if (!Directory.Exists(archivesDirectory)) throw new DirectoryNotFoundException(archivesDirectory);
        if (!File.Exists(catalogPath)) throw new FileNotFoundException("FIT catalog not found.", catalogPath);

        if (await db.Activities.AnyAsync(cancellationToken))
            throw new InvalidOperationException("Export activity import requires an empty Activities table. Use a new staging database.");

        var byStartTime = new Dictionary<DateTime, long>();
        var summariesImported = 0;
        foreach (var path in Directory.EnumerateFiles(fitnessDirectory, "*_summarizedActivities.json")
                     .OrderBy(x => x, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            using var document = JsonDocument.Parse(bytes);
            foreach (var outer in document.RootElement.EnumerateArray())
            {
                if (!outer.TryGetProperty("summarizedActivitiesExport", out var export)
                    || export.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var summary in export.EnumerateArray())
                {
                    var sourceBytes = Encoding.UTF8.GetBytes(summary.GetRawText());
                    var sourceHash = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
                    var startUtc = EpochMilliseconds(summary, "startTimeGmt");
                    var activityId = StringValue(summary, "activityId");
                    var averageSpeed = ScaledValue(summary, "avgSpeed", ExportSpeedUnitsPerMetersPerSecond);
                    var distance = ScaledValue(summary, "distance", ExportDistanceUnitsPerMeter);
                    var localTime = EpochMilliseconds(summary, "startTimeLocal");

                    var activity = new Activity
                    {
                        SourceKind = "GarminConnectExport",
                        SourceSha256 = sourceHash,
                        ExternalActivityId = activityId,
                        OriginalFileName = Path.GetFileName(path),
                        FileSizeBytes = sourceBytes.LongLength,
                        ImportedAtUtc = DateTime.UtcNow,
                        StartedAtUtc = startUtc,
                        StartedAtLocal = localTime is null
                            ? null
                            : DateTime.SpecifyKind(localTime.Value, DateTimeKind.Unspecified),
                        ActivityType = StringValue(summary, "activityType"),
                        ActivityTitle = StringValue(summary, "name"),
                        ElapsedTimeSeconds = ScaledValue(summary, "elapsedDuration", ExportDurationUnitsPerSecond),
                        TimerTimeSeconds = ScaledValue(summary, "duration", ExportDurationUnitsPerSecond),
                        MovingTimeSeconds = ScaledValue(summary, "movingDuration", ExportDurationUnitsPerSecond),
                        DistanceMeters = distance,
                        Calories = ScaledIntegerValue(summary, "calories", ExportEnergyUnitsPerKilocalorie),
                        AverageHeartRateBpm = IntegerValue(summary, "avgHr"),
                        MaximumHeartRateBpm = IntegerValue(summary, "maxHr"),
                        AverageSpeedMetersPerSecond = averageSpeed,
                        MaximumSpeedMetersPerSecond = ScaledValue(summary, "maxSpeed", ExportSpeedUnitsPerMetersPerSecond),
                        AverageCadenceRpm = ScaledValue(summary, "avgRunCadence", ExportRunCadenceUnitsPerStepPerMinute),
                        MaximumCadenceRpm = ScaledValue(summary, "maxRunCadence", ExportRunCadenceUnitsPerStepPerMinute),
                        AveragePaceSecondsPerKilometer = averageSpeed is > 0 ? 1000.0 / averageSpeed : null,
                        TotalAscentMeters = ScaledValue(summary, "elevationGain", ExportDistanceUnitsPerMeter),
                        TotalDescentMeters = ScaledValue(summary, "elevationLoss", ExportDistanceUnitsPerMeter),
                        AverageStrideLengthMeters = ScaledValue(summary, "avgStrideLength", ExportStrideUnitsPerMeter),
                        AerobicTrainingEffect = NumberValue(summary, "aerobicTrainingEffect"),
                        Steps = IntegerValue(summary, "steps"),
                        LapCount = IntegerValue(summary, "lapCount"),
                        MinimumElevationMeters = ScaledValue(summary, "minElevation", ExportDistanceUnitsPerMeter),
                        MaximumElevationMeters = ScaledValue(summary, "maxElevation", ExportDistanceUnitsPerMeter),
                        IsFavorite = BooleanValue(summary, "favorite"),
                        SourceFile = new ActivitySourceFile
                        {
                            SourceFormat = "GarminConnectActivitySummaryJson",
                            SourceBytes = sourceBytes
                        }
                    };
                    db.Activities.Add(activity);
                    summariesImported++;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var activity in await db.Activities.AsNoTracking()
                     .Where(x => x.ExternalActivityId != null && x.StartedAtUtc != null)
                     .Select(x => new { x.Id, x.StartedAtUtc })
                     .ToListAsync(cancellationToken))
        {
            if (activity.StartedAtUtc is { } start)
                byStartTime.TryAdd(DateTime.SpecifyKind(start, DateTimeKind.Utc), activity.Id);
        }
        db.ChangeTracker.Clear();

        var catalogRows = await ReadActivityCatalogRowsAsync(catalogPath, cancellationToken);
        var archives = Directory.EnumerateFiles(archivesDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .ToDictionary(path => Path.GetFileName(path)!, StringComparer.Ordinal);
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        var archiveHashes = catalogRows.Where(x => x.ParseStatus == "ok")
            .Select(x => x.Sha256).ToHashSet(StringComparer.Ordinal);
        var linked = 0;
        var fitOnly = 0;
        var skipped = 0;
        var failed = 0;
        long records = 0;
        long laps = 0;
        var errors = new List<string>();

        foreach (var grouping in catalogRows.GroupBy(x => x.Archive, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!archives.TryGetValue(grouping.Key, out var archivePath))
            {
                failed += grouping.Count();
                errors.Add($"Archive not found: {grouping.Key}");
                continue;
            }

            var wanted = grouping.ToDictionary(x => x.Entry, StringComparer.Ordinal);
            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!wanted.TryGetValue(entry.FullName, out var catalog)) continue;
                if (catalog.ParseStatus != "ok")
                {
                    skipped++;
                    errors.Add($"{catalog.Entry}: excluded by catalog ({catalog.Error})");
                    continue;
                }
                if (!hashes.Add(catalog.Sha256))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    await using var entryStream = entry.Open();
                    using var memory = new MemoryStream(entry.Length > int.MaxValue ? 0 : (int)entry.Length);
                    await entryStream.CopyToAsync(memory, cancellationToken);
                    var sourceBytes = memory.ToArray();
                    var actualHash = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
                    if (!string.Equals(actualHash, catalog.Sha256, StringComparison.Ordinal))
                        throw new InvalidDataException("FIT contents no longer match the catalog hash.");

                    var decoded = FitActivityDecoder.Decode(entry.FullName, sourceBytes, actualHash);
                    if (decoded.StartedAtUtc is { } fitStart
                        && byStartTime.TryGetValue(DateTime.SpecifyKind(fitStart, DateTimeKind.Utc), out var existingId))
                    {
                        var canonical = new Activity { Id = existingId, SourceKind = "", SourceSha256 = "", OriginalFileName = "" };
                        db.Attach(canonical);
                        canonical.FitSha256 = actualHash;
                        CopyWhenMissing(decoded, canonical);

                        foreach (var lap in decoded.Laps)
                        {
                            lap.ActivityId = existingId;
                            lap.Activity = canonical;
                            db.ActivityLaps.Add(lap);
                        }
                        foreach (var record in decoded.Records)
                        {
                            record.ActivityId = existingId;
                            record.Activity = canonical;
                            db.ActivityRecords.Add(record);
                        }
                        db.ActivitySourceEvidence.Add(new ActivitySourceEvidence
                        {
                            ActivityId = existingId,
                            SourceFormat = "FIT",
                            SourceSha256 = actualHash,
                            OriginalFileName = entry.FullName,
                            FileSizeBytes = sourceBytes.LongLength,
                            ImportedAtUtc = DateTime.UtcNow,
                            SourceBytes = sourceBytes
                        });
                        linked++;
                    }
                    else
                    {
                        decoded.SourceKind = "FITArchive";
                        decoded.SourceSha256 = actualHash;
                        decoded.FitSha256 = actualHash;
                        decoded.ExternalActivityId = null;
                        decoded.SourceFile ??= new ActivitySourceFile
                        {
                            SourceFormat = "FIT",
                            SourceBytes = sourceBytes
                        };
                        db.Activities.Add(decoded);
                        fitOnly++;
                    }

                    await db.SaveChangesAsync(cancellationToken);
                    records += decoded.Records.Count;
                    laps += decoded.Laps.Count;
                    db.ChangeTracker.Clear();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    errors.Add($"{catalog.Entry}: {ex.GetType().Name}: {ex.Message}");
                    db.ChangeTracker.Clear();
                }
            }
        }

        var supplemental = supplementDatabasePath is null
            ? new SupplementalImportResult(0, 0, 0)
            : await ImportSupplementalFitsAsync(supplementDatabasePath, archiveHashes, byStartTime, db,
                errors, cancellationToken);
        return new GarminExportActivityImportResult(summariesImported, linked, fitOnly, supplemental.Linked, skipped,
            failed, records + supplemental.Records, laps + supplemental.Laps, errors);
    }

    private static async Task<SupplementalImportResult> ImportSupplementalFitsAsync(
        string databasePath,
        IReadOnlySet<string> archiveHashes,
        IReadOnlyDictionary<DateTime, long> activityIdsByStart,
        GarminDbContext db,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(databasePath)) throw new FileNotFoundException("Supplement database not found.", databasePath);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.FitSha256, a.OriginalFileName, a.StartedAtUtc, source.SourceBytes
            FROM Activities a
            JOIN ActivitySourceFiles source ON source.ActivityId = a.Id
            WHERE a.SourceKind = 'FIT' AND a.FitSha256 IS NOT NULL
            """;

        var linked = 0;
        long records = 0;
        long laps = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expectedHash = reader.GetString(0);
            if (archiveHashes.Contains(expectedHash)) continue;

            var filename = reader.GetString(1);
            var startValue = reader.IsDBNull(2) ? null : reader.GetString(2);
            var bytes = (byte[])reader[3];
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
            {
                errors.Add($"{filename}: existing database FIT hash does not match its source payload.");
                continue;
            }
            if (!DateTime.TryParse(startValue, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start)
                || !activityIdsByStart.TryGetValue(DateTime.SpecifyKind(start, DateTimeKind.Utc), out var activityId))
            {
                errors.Add($"{filename}: no exact Garmin export summary start-time match; source was not added.");
                continue;
            }

            try
            {
                var decoded = FitActivityDecoder.Decode(filename, bytes, actualHash);
                var canonical = new Activity
                {
                    Id = activityId, SourceKind = "", SourceSha256 = "", OriginalFileName = ""
                };
                db.Attach(canonical);
                canonical.FitSha256 = actualHash;
                CopyWhenMissing(decoded, canonical);
                foreach (var lap in decoded.Laps)
                {
                    lap.ActivityId = activityId;
                    lap.Activity = canonical;
                    db.ActivityLaps.Add(lap);
                }
                foreach (var record in decoded.Records)
                {
                    record.ActivityId = activityId;
                    record.Activity = canonical;
                    db.ActivityRecords.Add(record);
                }
                db.ActivitySourceEvidence.Add(new ActivitySourceEvidence
                {
                    ActivityId = activityId,
                    SourceFormat = "FIT",
                    SourceSha256 = actualHash,
                    OriginalFileName = filename,
                    FileSizeBytes = bytes.LongLength,
                    ImportedAtUtc = DateTime.UtcNow,
                    SourceBytes = bytes
                });
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
                linked++;
                records += decoded.Records.Count;
                laps += decoded.Laps.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{filename}: supplemental FIT import failed: {ex.Message}");
                db.ChangeTracker.Clear();
            }
        }

        return new SupplementalImportResult(linked, records, laps);
    }

    private static async Task<List<CatalogRow>> ReadActivityCatalogRowsAsync(
        string path, CancellationToken cancellationToken)
    {
        var rows = new List<CatalogRow>();
        using var reader = new StreamReader(path);
        _ = await reader.ReadLineAsync(cancellationToken);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var fields = ParseCsvLine(line);
            if (fields.Count < 12 || fields[4] != "Activity") continue;
            rows.Add(new CatalogRow(fields[0], fields[1], fields[3], fields[10], fields[11]));
        }
        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted && c == '"' && i + 1 < line.Length && line[i + 1] == '"')
            {
                value.Append('"');
                i++;
            }
            else if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted)
            {
                fields.Add(value.ToString());
                value.Clear();
            }
            else value.Append(c);
        }
        fields.Add(value.ToString());
        return fields;
    }

    private static void CopyWhenMissing(Activity from, Activity to)
    {
        to.DistanceMeters ??= from.DistanceMeters;
        to.ElapsedTimeSeconds ??= from.ElapsedTimeSeconds;
        to.TimerTimeSeconds ??= from.TimerTimeSeconds;
        to.Calories ??= from.Calories;
        to.AverageHeartRateBpm ??= from.AverageHeartRateBpm;
        to.MaximumHeartRateBpm ??= from.MaximumHeartRateBpm;
        to.AverageSpeedMetersPerSecond ??= from.AverageSpeedMetersPerSecond;
        to.MaximumSpeedMetersPerSecond ??= from.MaximumSpeedMetersPerSecond;
        to.Sport ??= from.Sport;
        to.SubSport ??= from.SubSport;
    }

    private static string? StringValue(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Number
                ? value.ToString()
                : null;

    private static double? NumberValue(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;

    private static double? ScaledValue(JsonElement element, string name, double scale) =>
        NumberValue(element, name) is { } number
            ? number / scale
            : null;

    private static int? ScaledIntegerValue(JsonElement element, string name, double scale) =>
        NumberValue(element, name) is { } number && number / scale >= int.MinValue && number / scale <= int.MaxValue
            ? (int)Math.Round(number / scale)
            : null;

    private static int? IntegerValue(JsonElement element, string name) =>
        NumberValue(element, name) is { } number && number >= int.MinValue && number <= int.MaxValue
            ? (int)Math.Round(number)
            : null;

    private static bool? BooleanValue(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static DateTime? EpochMilliseconds(JsonElement element, string name)
    {
        var value = NumberValue(element, name);
        if (value is null || value < -62135596800000d || value > 253402300799999d) return null;
        return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(value.Value))
            .UtcDateTime;
    }

    private sealed record CatalogRow(string Archive, string Entry, string Sha256, string ParseStatus, string Error);
    private sealed record SupplementalImportResult(int Linked, long Records, long Laps);
}

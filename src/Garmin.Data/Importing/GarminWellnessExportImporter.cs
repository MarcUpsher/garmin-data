using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Garmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garmin.Data.Importing;

public sealed record WellnessImportResult(
    int SleepAdded, int DailyWellnessAdded, int Vo2MaxAdded, int DailyWellnessUpdated, int Skipped, int Invalid,
    string? FirstDate, string? LastDate);

/// <summary>Imports dated sleep, daily wellness and VO2 metrics from a Garmin Connect export.</summary>
public static class GarminWellnessExportImporter
{
    public const int CurrentWellnessImportVersion = 2;
    public static async Task<WellnessImportResult> ImportAsync(
        string diConnectDirectory, GarminDbContext db, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(diConnectDirectory)) throw new DirectoryNotFoundException(diConnectDirectory);
        foreach (var folder in new[] { "DI-Connect-Wellness", "DI-Connect-Aggregator", "DI-Connect-Metrics" })
            if (!Directory.Exists(Path.Combine(diConnectDirectory, folder)))
                throw new DirectoryNotFoundException($"The export is missing {folder}.");
        var staleWellness = await db.DailyWellnessRecords
            .Where(x => x.ImportVersion < CurrentWellnessImportVersion)
            .ToDictionaryAsync(x => x.SourceRecordHash, cancellationToken);
        var sleepHashes = (await db.SleepRecords.Select(x => x.SourceRecordHash).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var wellnessHashes = (await db.DailyWellnessRecords.Select(x => x.SourceRecordHash).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var vo2Hashes = (await db.Vo2MaxRecords.Select(x => x.SourceRecordHash).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var addedSleep = 0; var addedWellness = 0; var addedVo2 = 0; var updatedWellness = 0; var skipped = 0; var invalid = 0;
        var dates = new List<DateOnly>();

        foreach (var path in Directory.EnumerateFiles(Path.Combine(diConnectDirectory, "DI-Connect-Wellness"), "*_sleepData.json").OrderBy(x => x, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var doc = JsonDocument.Parse(await File.ReadAllBytesAsync(path, cancellationToken));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) { invalid++; continue; }
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!TryDate(item, "calendarDate", out var date)) { invalid++; continue; }
                var hash = Hash(item);
                if (!sleepHashes.Add(hash)) { skipped++; continue; }
                db.SleepRecords.Add(new SleepRecord
                {
                    SourceRecordHash = hash, SourceFileName = Path.GetFileName(path), CalendarDate = date,
                    SleepStartUtc = ReadDateTime(item, "sleepStartTimestampGMT"), SleepEndUtc = ReadDateTime(item, "sleepEndTimestampGMT"),
                    DeepSeconds = ReadInt(item, "deepSleepSeconds"), LightSeconds = ReadInt(item, "lightSleepSeconds"),
                    RemSeconds = ReadInt(item, "remSleepSeconds"), AwakeSeconds = ReadInt(item, "awakeSleepSeconds"),
                    UnmeasurableSeconds = ReadInt(item, "unmeasurableSeconds"), AverageRespiration = ReadDouble(item, "averageRespiration"),
                    LowestRespiration = ReadDouble(item, "lowestRespiration"), HighestRespiration = ReadDouble(item, "highestRespiration"),
                    WindowConfirmationType = ReadString(item, "sleepWindowConfirmationType"), IsRetroactive = ReadBool(item, "retro")
                });
                dates.Add(date); addedSleep++;
            }
        }

        foreach (var path in Directory.EnumerateFiles(Path.Combine(diConnectDirectory, "DI-Connect-Aggregator"), "UDSFile_*.json").OrderBy(x => x, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var doc = JsonDocument.Parse(await File.ReadAllBytesAsync(path, cancellationToken));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) { invalid++; continue; }
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!TryDate(item, "calendarDate", out var date)) { invalid++; continue; }
                var hash = Hash(item);
                staleWellness.Remove(hash, out var existing);
                if (!wellnessHashes.Add(hash) && existing is null) { skipped++; continue; }
                var stress = Object(item, "allDayStress");
                var totalStress = Array(stress, "aggregatorList")
                    .FirstOrDefault(x => ReadString(x, "type") == "TOTAL");
                var battery = Object(item, "bodyBattery");
                int? BatteryStat(string type) => ReadScore(Array(battery, "bodyBatteryStatList")
                    .FirstOrDefault(x => ReadString(x, "bodyBatteryStatType") == type), "statsValue");
                var record = new DailyWellnessRecord
                {
                    ImportVersion = CurrentWellnessImportVersion,
                    SourceRecordHash = hash, SourceFileName = Path.GetFileName(path), CalendarDate = date,
                    Source = ReadString(item, "source"), TotalSteps = ReadInt(item, "totalSteps"), DailyStepGoal = ReadInt(item, "dailyStepGoal"),
                    TotalDistanceMeters = ReadDouble(item, "totalDistanceMeters"), TotalKilocalories = ReadInt(item, "totalKilocalories"),
                    ActiveKilocalories = ReadInt(item, "activeKilocalories"), RestingHeartRateBpm = ReadInt(item, "restingHeartRate"),
                    CurrentDayRestingHeartRateBpm = ReadInt(item, "currentDayRestingHeartRate"), AllDayStress = ReadScore(totalStress, "averageStressLevel"),
                    BodyBattery = BatteryStat("MOSTRECENT"), BodyBatteryCharged = ReadInt(battery, "chargedValue"),
                    BodyBatteryDrained = ReadInt(battery, "drainedValue"), BodyBatteryHigh = BatteryStat("HIGHEST"),
                    BodyBatteryLow = BatteryStat("LOWEST"), BodyBatteryEndOfDay = BatteryStat("ENDOFDAY"), ModerateIntensityMinutes = ReadInt(item, "moderateIntensityMinutes"),
                    VigorousIntensityMinutes = ReadInt(item, "vigorousIntensityMinutes"), FloorsAscendedMeters = ReadDouble(item, "floorsAscendedInMeters"),
                    FloorsDescendedMeters = ReadDouble(item, "floorsDescendedInMeters"),
                    Respiration = ReadDouble(Object(item, "respiration"), "avgWakingRespirationValue"),
                    HydrationMillilitres = ReadDouble(Object(item, "hydration"), "valueInML")
                };
                if (existing is null) { db.DailyWellnessRecords.Add(record); addedWellness++; }
                else
                {
                    record.Id = existing.Id;
                    db.Entry(existing).CurrentValues.SetValues(record);
                    updatedWellness++;
                }
                dates.Add(date);
            }
        }

        var metricsDirectory = Path.Combine(diConnectDirectory, "DI-Connect-Metrics");
        foreach (var path in Directory.EnumerateFiles(metricsDirectory, "*MetricsMaxMetData*.json").OrderBy(x => x, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var doc = JsonDocument.Parse(await File.ReadAllBytesAsync(path, cancellationToken));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) { invalid++; continue; }
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!TryDate(item, "calendarDate", out var date)) { invalid++; continue; }
                var hash = Hash(item);
                if (!vo2Hashes.Add(hash)) { skipped++; continue; }
                db.Vo2MaxRecords.Add(new Vo2MaxRecord
                {
                    SourceRecordHash = hash, SourceFileName = Path.GetFileName(path), CalendarDate = date,
                    Sport = ReadString(item, "sport"), SubSport = ReadString(item, "subSport"), DeviceId = ReadString(item, "deviceId"),
                    Vo2MaxValue = ReadDouble(item, "vo2MaxValue"), MaxMet = ReadDouble(item, "maxMet"),
                    MaxMetCategory = ReadString(item, "maxMetCategory"), IsCalibrated = ReadBool(item, "calibratedData"),
                    UpdatedAtUtc = ReadDateTime(item, "updateTimestamp")
                });
                dates.Add(date); addedVo2++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new(addedSleep, addedWellness, addedVo2, updatedWellness, skipped, invalid,
            dates.Count == 0 ? null : dates.Min().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            dates.Count == 0 ? null : dates.Max().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private static JsonElement Object(JsonElement item, string key) => item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;
    private static IEnumerable<JsonElement> Array(JsonElement item, string key) => item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    private static string Hash(JsonElement item) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(item.GetRawText())));
    private static bool TryDate(JsonElement item, string key, out DateOnly date)
    {
        var text = ReadString(item, key);
        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
    private static string? ReadString(JsonElement item, string key) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var p) && p.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? p.ToString() : null;
    private static int? ReadInt(JsonElement item, string key) => double.TryParse(ReadString(item, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) && n >= int.MinValue && n <= int.MaxValue ? (int)Math.Round(n) : null;
    private static int? ReadScore(JsonElement item, string key) => ReadInt(item, key) is { } n && n is >= 0 and <= 100 ? n : null;
    private static double? ReadDouble(JsonElement item, string key) => double.TryParse(ReadString(item, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
    private static bool? ReadBool(JsonElement item, string key) => bool.TryParse(ReadString(item, key), out var value) ? value : null;
    private static DateTime? ReadDateTime(JsonElement item, string key) => DateTimeOffset.TryParse(ReadString(item, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value.UtcDateTime : null;
}

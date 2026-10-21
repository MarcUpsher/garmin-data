using System.Security.Cryptography;
using System.Text;
using Garmin.Data;
using Garmin.Data.Analysis;
using Garmin.Data.Entities;
using Garmin.Data.Importing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var temp = Path.Combine(Path.GetTempPath(), $"garmin-checks-{Guid.NewGuid():N}");
Directory.CreateDirectory(temp);
try
{
    var options = new DbContextOptionsBuilder<GarminDbContext>()
        .UseSqlite($"Data Source={Path.Combine(temp, "checks.db")}").Options;
    await using var db = new GarminDbContext(options);
    await db.Database.MigrateAsync();
    var root = Path.Combine(temp, "DI_CONNECT");
    foreach (var folder in new[] { "DI-Connect-Wellness", "DI-Connect-Aggregator", "DI-Connect-Metrics" })
        Directory.CreateDirectory(Path.Combine(root, folder));
    await File.WriteAllTextAsync(Path.Combine(root, "DI-Connect-Wellness", "fixture_sleepData.json"),
        """[{"calendarDate":"2026-01-01","deepSleepSeconds":3600,"lightSleepSeconds":21600}]""");
    await File.WriteAllTextAsync(Path.Combine(root, "DI-Connect-Aggregator", "UDSFile_fixture.json"), """
        [{"calendarDate":"2026-01-01","totalSteps":5000,"floorsAscendedInMeters":3.75,
          "allDayStress":{"aggregatorList":[{"type":"AWAKE","averageStressLevel":80},{"type":"TOTAL","averageStressLevel":31}]},
          "bodyBattery":{"chargedValue":40,"drainedValue":50,"bodyBatteryStatList":[
            {"bodyBatteryStatType":"MOSTRECENT","statsValue":20},{"bodyBatteryStatType":"ENDOFDAY","statsValue":18},
            {"bodyBatteryStatType":"HIGHEST","statsValue":90},{"bodyBatteryStatType":"LOWEST","statsValue":12}]},
          "respiration":{"avgWakingRespirationValue":14.2},"hydration":{"valueInML":1800}}]
        """);
    var first = await GarminWellnessExportImporter.ImportAsync(root, db);
    Check(first.SleepAdded == 1 && first.DailyWellnessAdded == 1, "Initial import stores source records");
    var row = await db.DailyWellnessRecords.SingleAsync();
    Check(row.AllDayStress == 31 && row.BodyBattery == 20 && row.BodyBatteryEndOfDay == 18
        && row.BodyBatteryHigh == 90 && row.BodyBatteryLow == 12 && row.BodyBatteryCharged == 40
        && row.BodyBatteryDrained == 50 && row.Respiration == 14.2 && row.HydrationMillilitres == 1800
        && row.FloorsAscendedMeters == 3.75, "Nested wellness values and metre precision are preserved");
    var originalId = row.Id;
    row.ImportVersion = 0; row.AllDayStress = null; row.BodyBattery = null;
    await db.SaveChangesAsync();
    var repaired = await GarminWellnessExportImporter.ImportAsync(root, db);
    Check(repaired.DailyWellnessUpdated == 1 && repaired.DailyWellnessAdded == 0
        && (await db.DailyWellnessRecords.SingleAsync()).Id == originalId && row.AllDayStress == 31,
        "Reimport repairs old parser results in place");
    var repeated = await GarminWellnessExportImporter.ImportAsync(root, db);
    Check(repeated.DailyWellnessUpdated == 0 && repeated.Skipped == 2, "Repeated import is idempotent");
    await File.WriteAllTextAsync(Path.Combine(root, "DI-Connect-Aggregator", "UDSFile_missing.json"),
        """[{"calendarDate":"2026-01-02","allDayStress":{"aggregatorList":[{"type":"TOTAL","averageStressLevel":-1}]}}]""");
    await GarminWellnessExportImporter.ImportAsync(root, db);
    var missing = await db.DailyWellnessRecords.SingleAsync(x => x.CalendarDate == new DateOnly(2026, 1, 2));
    Check(missing.AllDayStress is null && missing.BodyBattery is null && missing.Respiration is null,
        "Missing objects and negative stress sentinels remain unavailable");
    var extraSleep = Path.Combine(root, "DI-Connect-Wellness", "extra_sleepData.json");
    var malformed = Path.Combine(root, "DI-Connect-Metrics", "bad_MetricsMaxMetData.json");
    await File.WriteAllTextAsync(extraSleep, """[{"calendarDate":"2026-01-05","deepSleepSeconds":3600,"lightSleepSeconds":21600}]""");
    await File.WriteAllTextAsync(malformed, "invalid JSON");
    var rejected = false;
    await using (var failedImport = new GarminDbContext(options))
    {
        try { await GarminWellnessExportImporter.ImportAsync(root, failedImport); }
        catch (System.Text.Json.JsonException) { rejected = true; }
    }
    Check(rejected && await db.SleepRecords.CountAsync() == 1, "Malformed files cannot leave a partially saved import");
    File.Delete(extraSleep); File.Delete(malformed);

    var month = SleepTrendCalculator.Calculate([
        new() { SourceRecordHash = "a", SourceFileName = "fixture", CalendarDate = new(2026, 1, 1), DeepSeconds = 3600, LightSeconds = 21600 },
        new() { SourceRecordHash = "b", SourceFileName = "fixture", CalendarDate = new(2026, 1, 2), DeepSeconds = 0, LightSeconds = 0, RemSeconds = 0 },
        new() { SourceRecordHash = "c", SourceFileName = "fixture", CalendarDate = new(2026, 1, 3) },
        new() { SourceRecordHash = "d", SourceFileName = "fixture", CalendarDate = new(2026, 1, 4), DeepSeconds = -1, LightSeconds = 20000 }
    ]).Single();
    Check(month.AverageSleepHours == 7 && month.NightCount == 1 && month.ExcludedNightCount == 3
        && month.AverageRemSeconds is null, "Unmeasured sleep does not become zero sleep; old REM absence stays missing");
    var empty = SleepTrendCalculator.Calculate([
        new() { SourceRecordHash = "empty", SourceFileName = "fixture", CalendarDate = new(2026, 2, 1) }
    ]).Single();
    Check(empty.AverageSleepHours is null && empty.NightCount == 0, "Entirely missing months have no estimate");
    var range = TrendDateRange.Create("6m", new(2026, 10, 21), new(2020, 1, 1));
    Check(range.RangeStart == new DateOnly(2026, 4, 21) && range.RangeEnd == new DateOnly(2026, 10, 22)
        && !TrendDateRange.IsValidPeriod("nonsense"), "Trend ranges use calendar months and include the last day");

    // Known FIT hashes must be skipped before decoding, even if a JSON summary is
    // the primary source. The dummy bytes deliberately cannot decode as FIT.
    var watch = Path.Combine(temp, "watch"); Directory.CreateDirectory(watch);
    var bytes = Encoding.UTF8.GetBytes("already stored FIT evidence");
    var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
    await File.WriteAllBytesAsync(Path.Combine(watch, "known.fit"), bytes);
    var activity = new Activity { SourceKind = "GarminConnectExport", SourceSha256 = "summary-hash", FitSha256 = hash,
        OriginalFileName = "summary.json", ActivityType = "running", DistanceMeters = 5000, ElapsedTimeSeconds = 1500 };
    db.Activities.Add(activity); await db.SaveChangesAsync();
    var sync = await FitDirectoryImporter.ImportAsync(watch, db);
    Check(sync.Skipped == 1 && sync.Failed == 0 && await db.Activities.CountAsync() == 1,
        "USB sync recognizes FITs linked to export summaries");
    var extra = Encoding.UTF8.GetBytes("supplemental FIT evidence");
    await File.WriteAllBytesAsync(Path.Combine(watch, "supplement.fit"), extra);
    db.ActivitySourceEvidence.Add(new() { Activity = activity, SourceFormat = "FIT", SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(extra)), OriginalFileName = "supplement.fit", SourceBytes = extra });
    await db.SaveChangesAsync();
    sync = await FitDirectoryImporter.ImportAsync(watch, db);
    Check(sync.Skipped == 2 && sync.Failed == 0, "USB sync also recognizes supplemental FIT evidence");
    Check(PerformanceEffortCalculator.Calculate(activity).Single(x => x.DistanceMeters == 5000).EffortKind == "RecordedActivity",
        "Summary-only runs support recorded-distance fallback");
    var pausedRun = new Activity { SourceKind = "fixture", SourceSha256 = "paused-run", OriginalFileName = "paused.fit",
        ActivityType = "running", DistanceMeters = 5000, ElapsedTimeSeconds = 1507, TimerTimeSeconds = 1500 };
    var recordedEffort = PerformanceEffortCalculator.Calculate(pausedRun).Single(x => x.DistanceMeters == 5000);
    Check(recordedEffort.DurationSeconds == 1507 && recordedEffort.EndElapsedSeconds == 1507
        && recordedEffort.AlgorithmVersion == 3,
        "Recorded efforts use elapsed time, including pauses");

    // Both files are known FIT evidence. Force the first index write to fail;
    // the second should still index successfully in the same import call.
    var failureWatch = Path.Combine(temp, "failure-watch"); Directory.CreateDirectory(failureWatch);
    await File.WriteAllBytesAsync(Path.Combine(failureWatch, "a.fit"), bytes);
    await File.WriteAllBytesAsync(Path.Combine(failureWatch, "b.fit"), bytes);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TRIGGER reject_first_index BEFORE INSERT ON ImportedDeviceFiles
        WHEN NEW.RelativePath = 'a.fit'
        BEGIN SELECT RAISE(ABORT, 'fixture index rejection'); END
        """);
    var isolated = await FitDirectoryImporter.ImportAsync(failureWatch, db);
    Check(isolated.Failed == 1 && isolated.Skipped == 1
        && await db.ImportedDeviceFiles.AnyAsync(x => x.DeviceKey == failureWatch && x.RelativePath == "b.fit")
        && !await db.ImportedDeviceFiles.AnyAsync(x => x.DeviceKey == failureWatch && x.RelativePath == "a.fit"),
        "Failed USB index write does not poison the next file");
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_first_index");

    if (args.Length == 2)
    {
        var copy = Path.Combine(temp, "snapshot.db");
        await using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = args[1], Mode = SqliteOpenMode.ReadOnly }.ToString()))
        await using (var destination = new SqliteConnection($"Data Source={copy}"))
        {
            await source.OpenAsync(); await destination.OpenAsync(); source.BackupDatabase(destination);
        }
        await using var snapshot = new GarminDbContext(new DbContextOptionsBuilder<GarminDbContext>().UseSqlite($"Data Source={copy}").Options);
        await snapshot.Database.MigrateAsync();
        var before = await snapshot.DailyWellnessRecords.Select(x => x.Id).OrderBy(x => x).ToArrayAsync();
        var result = await GarminWellnessExportImporter.ImportAsync(args[0], snapshot);
        var after = await snapshot.DailyWellnessRecords.Select(x => x.Id).OrderBy(x => x).ToArrayAsync();
        Check(before.SequenceEqual(after), "Migration and repair preserve existing daily IDs");
        var again = await GarminWellnessExportImporter.ImportAsync(args[0], snapshot);
        Check(again.SleepAdded + again.DailyWellnessAdded + again.Vo2MaxAdded + again.DailyWellnessUpdated == 0,
            "Real export remains idempotent after repair");
        Console.WriteLine($"Snapshot repair: {result.DailyWellnessUpdated} daily rows updated; stress={await snapshot.DailyWellnessRecords.CountAsync(x => x.AllDayStress != null)}, battery={await snapshot.DailyWellnessRecords.CountAsync(x => x.BodyBattery != null)}, respiration={await snapshot.DailyWellnessRecords.CountAsync(x => x.Respiration != null)}, hydration={await snapshot.DailyWellnessRecords.CountAsync(x => x.HydrationMillilitres != null)}.");
    }
    Console.WriteLine("All data checks passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    Directory.Delete(temp, recursive: true);
}

static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine($"PASS {description}");
}

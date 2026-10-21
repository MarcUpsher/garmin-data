using System.Security.Cryptography;
using Garmin.Data;
using Garmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garmin.Data.Importing;

public sealed record FitImportResult(int Imported, int Skipped, int Failed, long RecordCount, long LapCount,
    int FilesScanned, int FilesHashed, IReadOnlyList<string> Errors);

public static class FitDirectoryImporter
{
    private static readonly TimeSpan FingerprintTrustPeriod = TimeSpan.FromDays(30);

    public static async Task<FitImportResult> ImportAsync(string inputDirectory, GarminDbContext db,
        CancellationToken cancellationToken = default)
    {
        var activityDirectory = ResolveActivityDirectory(inputDirectory);
        if (!Directory.Exists(activityDirectory))
            throw new DirectoryNotFoundException($"Garmin activity folder not found: {activityDirectory}");

        var deviceRoot = ResolveDeviceRoot(activityDirectory);
        var deviceKey = deviceRoot;
        var indexedFiles = await db.ImportedDeviceFiles
            .Where(x => x.DeviceKey == deviceKey)
            .ToDictionaryAsync(x => x.RelativePath, StringComparer.Ordinal, cancellationToken);

        var files = Directory.EnumerateFiles(activityDirectory, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), ".fit", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var imported = 0;
        var skipped = 0;
        var failed = 0;
        long records = 0;
        long laps = 0;
        var filesHashed = 0;
        var errors = new List<string>();
        Dictionary<string, long>? activityIdsByHash = null;
        var now = DateTime.UtcNow;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var relativePath = Path.GetRelativePath(deviceRoot, file);
                var fileInfo = new FileInfo(file);
                fileInfo.Refresh();
                var fileSize = fileInfo.Length;
                var lastModified = fileInfo.LastWriteTimeUtc;

                if (indexedFiles.TryGetValue(relativePath, out var indexed)
                    && indexed.FileSizeBytes == fileSize
                    && indexed.LastModifiedUtc == lastModified
                    && now - indexed.LastVerifiedAtUtc < FingerprintTrustPeriod)
                {
                    skipped++;
                    continue;
                }

                var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                filesHashed++;

                if (activityIdsByHash is null)
                {
                    // Connect imports use a JSON summary as the primary source.
                    // Their FIT bytes, including supplemental files, are separate evidence.
                    activityIdsByHash = new Dictionary<string, long>(StringComparer.Ordinal);
                    var sources = await db.Activities.AsNoTracking()
                        .Select(x => new { x.SourceSha256, x.FitSha256, x.Id }).ToListAsync(cancellationToken);
                    foreach (var source in sources)
                    {
                        activityIdsByHash.TryAdd(source.SourceSha256, source.Id);
                        if (source.FitSha256 is { } fitHash) activityIdsByHash.TryAdd(fitHash, source.Id);
                    }
                    var evidence = await db.ActivitySourceEvidence.AsNoTracking()
                        .Select(x => new { x.SourceSha256, x.ActivityId }).ToListAsync(cancellationToken);
                    foreach (var source in evidence) activityIdsByHash.TryAdd(source.SourceSha256, source.ActivityId);
                }

                if (activityIdsByHash.TryGetValue(hash, out var existingActivityId))
                {
                    UpsertFileIndex(db, indexed, indexedFiles, deviceKey, relativePath, fileSize,
                        lastModified, existingActivityId);
                    await db.SaveChangesAsync(cancellationToken);
                    skipped++;
                    continue;
                }

                var activity = FitActivityDecoder.Decode(file, bytes, hash);
                foreach (var effort in PerformanceEffortCalculator.Calculate(activity))
                    activity.PerformanceEfforts.Add(effort);
                db.Activities.Add(activity);
                var fileIndex = UpsertFileIndex(db, indexed, indexedFiles, deviceKey, relativePath,
                    fileSize, lastModified, 0, activity);
                await db.SaveChangesAsync(cancellationToken);
                fileIndex.ActivityId = activity.Id;
                activityIdsByHash.Add(hash, activity.Id);
                imported++;
                records += activity.Records.Count;
                laps += activity.Laps.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                // A failed SaveChanges leaves added entities and modified index rows
                // tracked. Reload the index so the next file starts from persisted state.
                db.ChangeTracker.Clear();
                indexedFiles = await db.ImportedDeviceFiles
                    .Where(x => x.DeviceKey == deviceKey)
                    .ToDictionaryAsync(x => x.RelativePath, StringComparer.Ordinal, cancellationToken);
            }
        }

        return new FitImportResult(imported, skipped, failed, records, laps, files.Length, filesHashed, errors);
    }

    private static ImportedDeviceFile UpsertFileIndex(GarminDbContext db, ImportedDeviceFile? indexed,
        IDictionary<string, ImportedDeviceFile> indexedFiles, string deviceKey, string relativePath,
        long fileSize, DateTime lastModified, long activityId, Activity? newActivity = null)
    {
        var entry = indexed ?? new ImportedDeviceFile
        {
            DeviceKey = deviceKey,
            RelativePath = relativePath
        };
        entry.FileSizeBytes = fileSize;
        entry.LastModifiedUtc = lastModified;
        entry.LastVerifiedAtUtc = DateTime.UtcNow;
        if (newActivity is not null) entry.Activity = newActivity;
        else entry.ActivityId = activityId;
        if (indexed is null)
        {
            indexedFiles.Add(relativePath, entry);
            db.ImportedDeviceFiles.Add(entry);
        }

        return entry;
    }

    public static string ResolveActivityDirectory(string input)
    {
        var path = Path.GetFullPath(input);
        var nestedActivity = Path.Combine(path, "GARMIN", "Activity");
        if (Directory.Exists(nestedActivity)) return nestedActivity;

        if (Path.GetFileName(path).Equals("GARMIN", StringComparison.OrdinalIgnoreCase))
        {
            var activity = Path.Combine(path, "Activity");
            if (Directory.Exists(activity)) return activity;
        }

        return path;
    }

    private static string ResolveDeviceRoot(string activityDirectory)
    {
        var activity = new DirectoryInfo(activityDirectory);
        if (activity.Name.Equals("Activity", StringComparison.OrdinalIgnoreCase)
            && activity.Parent?.Name.Equals("GARMIN", StringComparison.OrdinalIgnoreCase) == true)
            return activity.Parent.FullName;

        return activity.FullName;
    }
}

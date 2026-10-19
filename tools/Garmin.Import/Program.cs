using Garmin.Data;
using Garmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: Garmin.Import <FIT-directory-or-watch-mount|Garmin-CSV-file> [--database <path>]");
    Console.WriteLine("Reads FIT files from GARMIN/Activity or imports Garmin Connect activity summaries from CSV.");
    return args.Length == 0 ? 2 : 0;
}

var inputPath = Path.GetFullPath(args[0]);
var isCsv = Path.GetExtension(inputPath).Equals(".csv", StringComparison.OrdinalIgnoreCase);
var sourceDirectory = isCsv ? null : ResolveActivityDirectory(args[0]);
if (isCsv ? !File.Exists(inputPath) : !Directory.Exists(sourceDirectory))
{
    Console.Error.WriteLine(isCsv ? $"CSV file not found: {inputPath}" : $"Activity directory not found: {sourceDirectory}");
    return 2;
}

var databasePath = GetOption(args, "--database") ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "garmin-data", "activities.db");
databasePath = Path.GetFullPath(databasePath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

var options = new DbContextOptionsBuilder<GarminDbContext>()
    .UseSqlite($"Data Source={databasePath}")
    .Options;

await using var db = new GarminDbContext(options);
await db.Database.MigrateAsync();

Console.WriteLine($"SQLite database: {databasePath}");

if (isCsv)
{
    var result = await GarminCsvImporter.ImportAsync(inputPath, db);
    Console.WriteLine($"CSV import: {result.Imported} imported, {result.Skipped} duplicates skipped, {result.Failed} failed.");
    Console.WriteLine($"Stored {result.RecordCount} summary rows. CSV does not include per-sample records or individual lap splits.");
    return result.Failed == 0 ? 0 : 1;
}

var files = Directory.EnumerateFiles(sourceDirectory!, "*", SearchOption.AllDirectories)
    .Where(x => string.Equals(Path.GetExtension(x), ".fit", StringComparison.OrdinalIgnoreCase))
    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
    .ToArray();

var imported = 0;
var skipped = 0;
var failed = 0;
long recordCount = 0;
long lapCount = 0;
Console.WriteLine($"Found {files.Length} FIT files in {sourceDirectory}");

foreach (var file in files)
{
    try
    {
        var bytes = await File.ReadAllBytesAsync(file);
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        if (await db.Activities.AnyAsync(x => x.SourceSha256 == hash))
        {
            skipped++;
            continue;
        }

        var activity = FitActivityDecoder.Decode(file, bytes, hash);
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        imported++;
        recordCount += activity.Records.Count;
        lapCount += activity.Laps.Count;
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"Failed {Path.GetFileName(file)}: {ex.Message}");
    }
}

Console.WriteLine($"Finished: {imported} imported, {skipped} duplicates skipped, {failed} failed.");
Console.WriteLine($"Stored {recordCount} record samples and {lapCount} lap summaries.");
return failed == 0 ? 0 : 1;

static string ResolveActivityDirectory(string input)
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

static string? GetOption(string[] values, string name)
{
    var index = Array.IndexOf(values, name);
    if (index < 0) return null;
    if (index + 1 >= values.Length || values[index + 1].StartsWith("--", StringComparison.Ordinal))
        throw new ArgumentException($"Expected a value after {name}.");
    return values[index + 1];
}

using Garmin.Data;
using Garmin.Data.Importing;
using Microsoft.EntityFrameworkCore;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: Garmin.Import <FIT-directory-or-watch-mount|Garmin-CSV-file> [--database <path>]");
    Console.WriteLine("Reads FIT files from GARMIN/Activity or imports Garmin Connect activity summaries from CSV.");
    Console.WriteLine("       Garmin.Import catalog-fit <FIT-archive.zip-or-directory> [--output <catalog.csv>]");
    Console.WriteLine("Catalogs FIT files in ZIP archives without importing them or extracting the archives.");
    Console.WriteLine("       Garmin.Import import-garmin-export <DI_CONNECT-directory> [--catalog <catalog.csv>] [--database <path>] [--supplement-database <old.db>]");
    Console.WriteLine("Rebuilds canonical activities by linking Garmin Connect summaries with matching FIT files.");
    return args.Length == 0 ? 2 : 0;
}

if (args[0].Equals("catalog-fit", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: Garmin.Import catalog-fit <FIT-archive.zip-or-directory> [--output <catalog.csv>]");
        return 2;
    }

    var catalogInput = Path.GetFullPath(args[1]);
    var archives = File.Exists(catalogInput)
        ? new[] { catalogInput }
        : Directory.Exists(catalogInput)
            ? Directory.EnumerateFiles(catalogInput, "*.zip", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    if (archives.Length == 0 || archives.Any(path => !Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)))
    {
        Console.Error.WriteLine($"No ZIP archives found at: {catalogInput}");
        return 2;
    }

    var outputPath = Path.GetFullPath(GetOption(args, "--output")
        ?? Path.Combine(File.Exists(catalogInput) ? Path.GetDirectoryName(catalogInput)! : catalogInput,
            "garmin-fit-catalog.csv"));
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await using var catalogOutput = new StreamWriter(outputPath, append: false, new System.Text.UTF8Encoding(false));
    var catalog = await FitArchiveCataloger.CatalogAsync(archives, catalogOutput);

    Console.WriteLine($"Catalog: {outputPath}");
    Console.WriteLine($"Scanned {catalog.FilesScanned} FIT files in {catalog.ArchivesScanned} archive(s); {catalog.FilesParsed} parsed, {catalog.FilesFailed} had errors, {catalog.DuplicateContents} duplicate content hashes.");
    foreach (var (type, count) in catalog.FileTypes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        Console.WriteLine($"  {type}: {count}");
    return catalog.FilesFailed == 0 ? 0 : 1;
}

if (args[0].Equals("import-garmin-export", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: Garmin.Import import-garmin-export <DI_CONNECT-directory> [--catalog <catalog.csv>] [--database <path>] [--supplement-database <old.db>]");
        return 2;
    }

    var exportDirectory = Path.GetFullPath(args[1]);
    var catalogPath = Path.GetFullPath(GetOption(args, "--catalog")
        ?? Path.Combine(exportDirectory, "garmin-fit-catalog.csv"));
    var exportDatabasePath = Path.GetFullPath(GetOption(args, "--database") ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "garmin-data", "activities.db"));
    Directory.CreateDirectory(Path.GetDirectoryName(exportDatabasePath)!);
    var exportOptions = new DbContextOptionsBuilder<GarminDbContext>()
        .UseSqlite($"Data Source={exportDatabasePath}")
        .Options;
    await using var exportDb = new GarminDbContext(exportOptions);
    await exportDb.Database.MigrateAsync();
    Console.WriteLine($"SQLite database: {exportDatabasePath}");

    var exportResult = await GarminConnectExportActivityImporter.ImportAsync(
        exportDirectory, catalogPath, exportDb, GetOption(args, "--supplement-database"));
    Console.WriteLine($"Summaries imported: {exportResult.SummariesImported}");
    Console.WriteLine($"FIT files linked: {exportResult.FitFilesLinked}; FIT-only activities: {exportResult.FitOnlyActivitiesImported}; supplemental FITs linked: {exportResult.SupplementalFitFilesLinked}");
    Console.WriteLine($"FIT files skipped: {exportResult.FitFilesSkipped}; failed: {exportResult.Failed}");
    Console.WriteLine($"Stored {exportResult.RecordCount} record samples and {exportResult.LapCount} lap summaries.");
    foreach (var error in exportResult.Errors) Console.Error.WriteLine(error);
    return exportResult.Failed == 0 ? 0 : 1;
}

var inputPath = Path.GetFullPath(args[0]);
var isCsv = Path.GetExtension(inputPath).Equals(".csv", StringComparison.OrdinalIgnoreCase);
var sourceDirectory = isCsv ? null : FitDirectoryImporter.ResolveActivityDirectory(args[0]);
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

var fitResult = await FitDirectoryImporter.ImportAsync(sourceDirectory!, db);
Console.WriteLine($"Finished: {fitResult.Imported} imported, {fitResult.Skipped} duplicates skipped, {fitResult.Failed} failed.");
Console.WriteLine($"Stored {fitResult.RecordCount} record samples and {fitResult.LapCount} lap summaries.");
Console.WriteLine($"Scanned {fitResult.FilesScanned} files and read {fitResult.FilesHashed} file contents to verify hashes.");
foreach (var error in fitResult.Errors) Console.Error.WriteLine(error);
return fitResult.Failed == 0 ? 0 : 1;

static string? GetOption(string[] values, string name)
{
    var index = Array.IndexOf(values, name);
    if (index < 0) return null;
    if (index + 1 >= values.Length || values[index + 1].StartsWith("--", StringComparison.Ordinal))
        throw new ArgumentException($"Expected a value after {name}.");
    return values[index + 1];
}

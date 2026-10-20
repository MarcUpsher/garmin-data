using System.IO.Compression;
using System.Security.Cryptography;
using Dynastream.Fit;
using File = System.IO.File;

namespace Garmin.Data.Importing;

public sealed record FitArchiveCatalogResult(
    int ArchivesScanned,
    int FilesScanned,
    int FilesParsed,
    int FilesFailed,
    int DuplicateContents,
    IReadOnlyDictionary<string, int> FileTypes);

/// <summary>Inspects FIT files in ZIP archives one at a time without extracting or importing them.</summary>
public static class FitArchiveCataloger
{
    public static async Task<FitArchiveCatalogResult> CatalogAsync(
        IEnumerable<string> archivePaths,
        TextWriter csv,
        CancellationToken cancellationToken = default)
    {
        var archivesScanned = 0;
        var filesScanned = 0;
        var filesParsed = 0;
        var filesFailed = 0;
        var duplicateContents = 0;
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        var fileTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await csv.WriteLineAsync("Archive,Entry,SizeBytes,Sha256,FitFileType,FileTypeCode,SessionMessages,LapMessages,RecordMessages,SessionStartsUtc,ParseStatus,Error");

        foreach (var archivePath in archivePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
            archivesScanned++;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!entry.FullName.EndsWith(".fit", StringComparison.OrdinalIgnoreCase)) continue;

                filesScanned++;
                string? hash = null;
                string? fileType = null;
                int? fileTypeCode = null;
                var sessionCount = 0;
                var lapCount = 0;
                var recordCount = 0;
                var sessionStartsUtc = new List<System.DateTime>(1);
                var fileIds = new List<FileIdMesg>(1);
                var error = "";
                var status = "ok";
                var tempPath = Path.Combine(Path.GetTempPath(), $"garmin-fit-catalog-{Guid.NewGuid():N}.tmp");

                try
                {
                    await using (var input = entry.Open())
                    await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                     81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        await input.CopyToAsync(output, cancellationToken);
                    }

                    await using (var input = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                                     81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(input, cancellationToken));
                    }

                    if (!hashes.Add(hash)) duplicateContents++;

                    var decoder = new Decode();
                    var broadcaster = new MesgBroadcaster();
                    decoder.MesgEvent += broadcaster.OnMesg;
                    decoder.MesgDefinitionEvent += broadcaster.OnMesgDefinition;
                    broadcaster.FileIdMesgEvent += (_, e) =>
                    {
                        var fileId = (FileIdMesg)e.mesg;
                        fileIds.Add(fileId);
                        if (fileType is null)
                        {
                            var fitFileType = fileId.GetType();
                            fileType = fitFileType.ToString();
                            fileTypeCode = Convert.ToInt32(fitFileType);
                        }
                    };
                    broadcaster.SessionMesgEvent += (_, e) =>
                    {
                        sessionCount++;
                        var start = AsUtc(((SessionMesg)e.mesg).GetStartTime());
                        if (start.HasValue) sessionStartsUtc.Add(start.Value);
                    };
                    broadcaster.LapMesgEvent += (_, _) => lapCount++;
                    broadcaster.RecordMesgEvent += (_, _) => recordCount++;

                    await using (var input = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                                     81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        decoder.Read(input);
                    }

                    if (fileIds.Count == 0)
                    {
                        status = "no-file-id";
                        error = "No FIT FileId message was found.";
                    }
                    else
                    {
                        if (fileIds.Count > 1)
                        {
                            status = "multiple-file-ids";
                            error = $"Found {fileIds.Count} FileId messages.";
                        }
                    }

                    if (status == "ok") filesParsed++;
                    else filesFailed++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    status = "error";
                    error = $"{ex.GetType().Name}: {ex.Message}";
                    filesFailed++;
                }
                finally
                {
                    try { File.Delete(tempPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }

                if (fileType is not null)
                    fileTypes[fileType] = fileTypes.GetValueOrDefault(fileType) + 1;

                var fields = new object?[]
                {
                    Path.GetFileName(archivePath), entry.FullName, entry.Length, hash, fileType, fileTypeCode,
                    sessionCount, lapCount, recordCount,
                    string.Join(';', sessionStartsUtc.Select(start => start.ToString("O"))), status, error
                };
                await csv.WriteLineAsync(string.Join(',', fields.Select(CsvField)));
                if (filesScanned % 250 == 0) await csv.FlushAsync(cancellationToken);
            }
        }

        await csv.FlushAsync(cancellationToken);
        return new FitArchiveCatalogResult(archivesScanned, filesScanned, filesParsed, filesFailed,
            duplicateContents, fileTypes);
    }

    private static string CsvField(object? value)
    {
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static System.DateTime? AsUtc(Dynastream.Fit.DateTime? value) => value is null
        ? null
        : System.DateTime.SpecifyKind(value.GetDateTime(), System.DateTimeKind.Utc);
}

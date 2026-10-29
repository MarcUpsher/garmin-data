namespace Garmin.Data.Importing;

/// <summary>Finds activity storage in mounted filesystems; never mounts or writes to a device.</summary>
public static class GarminWatchDiscovery
{
    // Explicit roots replace the desktop defaults. Container usernames do not
    // need to match host usernames; discovery runs again for every sync request.
    public static string? FindActivityDirectory(string? configuredRoots = null)
    {
        var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var roots = string.IsNullOrWhiteSpace(configuredRoots)
            ? new[] {
                $"/run/media/{Environment.UserName}", $"/media/{Environment.UserName}", "/media",
                runtimeDirectory is null ? null : Path.Combine(runtimeDirectory, "gvfs")
            }.OfType<string>()
            : configuredRoots.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var root in roots.Distinct(StringComparer.Ordinal))
        {
            var pending = new Queue<(string Path, int Depth)>();
            pending.Enqueue((root, 0));
            while (pending.TryDequeue(out var current))
            {
                if (!Directory.Exists(current.Path)) continue;
                var directory = new DirectoryInfo(current.Path);
                if (directory.Name.Equals("Activity", StringComparison.OrdinalIgnoreCase)
                    && directory.Parent?.Name.Equals("GARMIN", StringComparison.OrdinalIgnoreCase) == true)
                    return directory.FullName;
                var activity = Path.Combine(current.Path, "GARMIN", "Activity");
                if (Directory.Exists(activity)) return activity;
                if (directory.Name.Equals("GARMIN", StringComparison.OrdinalIgnoreCase))
                {
                    activity = Path.Combine(current.Path, "Activity");
                    if (Directory.Exists(activity)) return activity;
                }
                if (current.Depth >= 5) continue;
                try
                {
                    foreach (var child in Directory.EnumerateDirectories(current.Path))
                        pending.Enqueue((child, current.Depth + 1));
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
        return null;
    }
}

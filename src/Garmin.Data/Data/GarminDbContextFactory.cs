using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Garmin.Data;

/// <summary>Provides the SQLite provider configuration to EF migration tooling.</summary>
public sealed class GarminDbContextFactory : IDesignTimeDbContextFactory<GarminDbContext>
{
    public GarminDbContext CreateDbContext(string[] args)
    {
        var defaultDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "garmin-data");
        var databasePath = Path.GetFullPath(Environment.GetEnvironmentVariable("GARMIN_DB_PATH")
            ?? Path.Combine(defaultDataDirectory, "activities.db"));

        var options = new DbContextOptionsBuilder<GarminDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new GarminDbContext(options);
    }
}

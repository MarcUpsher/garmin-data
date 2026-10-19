using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Garmin.Data;

/// <summary>Provides the SQLite provider configuration to EF migration tooling.</summary>
public sealed class GarminDbContextFactory : IDesignTimeDbContextFactory<GarminDbContext>
{
    public GarminDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GarminDbContext>()
            .UseSqlite("Data Source=garmin.db")
            .Options;

        return new GarminDbContext(options);
    }
}

using Garmin.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garmin.Data;

public sealed class GarminDbContext(DbContextOptions<GarminDbContext> options) : DbContext(options)
{
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityLap> ActivityLaps => Set<ActivityLap>();
    public DbSet<ActivityRecord> ActivityRecords => Set<ActivityRecord>();
    public DbSet<ActivityExtraField> ActivityExtraFields => Set<ActivityExtraField>();
    public DbSet<ActivitySourceFile> ActivitySourceFiles => Set<ActivitySourceFile>();
    public DbSet<ImportedDeviceFile> ImportedDeviceFiles => Set<ImportedDeviceFile>();
    public DbSet<ActivitySourceEvidence> ActivitySourceEvidence => Set<ActivitySourceEvidence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.SourceSha256).IsUnique();
            entity.HasIndex(x => x.FitSha256).IsUnique();
            entity.HasIndex(x => x.StartedAtUtc);
            entity.HasIndex(x => x.StartedAtLocal);
            entity.HasIndex(x => x.ExternalActivityId).IsUnique();
            entity.Property(x => x.SourceSha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SourceKind).HasMaxLength(40).IsRequired();
            entity.Property(x => x.FitSha256).HasMaxLength(64);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        });

        modelBuilder.Entity<ActivitySourceFile>(entity =>
        {
            entity.HasKey(x => x.ActivityId);
            entity.Property(x => x.SourceFormat).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceBytes).IsRequired();
            entity.HasOne(x => x.Activity).WithOne(x => x.SourceFile)
                .HasForeignKey<ActivitySourceFile>(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportedDeviceFile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DeviceKey, x.RelativePath }).IsUnique();
            entity.Property(x => x.DeviceKey).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.RelativePath).HasMaxLength(1024).IsRequired();
            entity.HasOne(x => x.Activity).WithMany(x => x.ImportedDeviceFiles)
                .HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivitySourceEvidence>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.SourceSha256).IsUnique();
            entity.HasIndex(x => x.ActivityId);
            entity.Property(x => x.SourceFormat).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceSha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.OriginalFileName).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.SourceBytes).IsRequired();
            entity.HasOne(x => x.Activity).WithMany(x => x.SourceEvidence)
                .HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityLap>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ActivityId, x.LapNumber }).IsUnique();
            entity.HasOne(x => x.Activity).WithMany(x => x.Laps)
                .HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ActivityId, x.RecordNumber }).IsUnique();
            entity.HasIndex(x => new { x.ActivityId, x.TimestampUtc });
            entity.HasOne(x => x.Activity).WithMany(x => x.Records)
                .HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityExtraField>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new
            {
                x.ActivityId, x.GlobalMessageNumber, x.MessageNumber,
                x.FieldNumber, x.DeveloperDataIndex
            }).IsUnique();
            entity.HasOne(x => x.Activity).WithMany(x => x.ExtraFields)
                .HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

# Garmin activity importer (first pass)

This is a provisional .NET 10 / EF Core code-first model for the Garmin activity importer.
The undecided product name is intentionally not baked into a user-facing app yet.

## Schema shape

- `Activity` stores import metadata. SHA-256 is unique so repeated USB/Connect imports can be
  made idempotent. The original bytes live in the one-to-one `ActivitySourceFile` table, keeping
  them out of ordinary activity queries. Sport and subsport remain numeric so unknown Garmin
  enum values are not discarded.
- `ActivityLap` is an optional child collection. No lap rows means the FIT had no lap messages;
  it does not make the activity incomplete.
- `ActivityRecord` stores the common time-series signals in query-friendly columns.
- `ActivityExtraField` retains raw values for fields that are not yet mapped to columns.

The importer in `tools/Garmin.Import` uses Garmin's FIT SDK to populate these entities. It accepts
either a directory of FIT files, a `GARMIN/Activity` directory, or the root of a mounted watch;
it recursively scans for `.fit` files. This means the watch can be the source directly when Linux
mounts it as a filesystem. It does not yet mount or detect the device automatically. Example:

```sh
dotnet run --project tools/Garmin.Import -- /run/media/$USER/GARMIN
```

By default, data goes to `~/.local/share/garmin-data/activities.db`. Supply
`--database /path/to/file.db` to select another database. The importer runs EF migrations before
reading files, skips content already stored by SHA-256, and keeps each original FIT file in the
database. Run `dotnet tool restore` and `dotnet tool run dotnet-ef database update` to create/update
the SQLite schema separately. Add later schema changes with
`dotnet tool run dotnet-ef migrations add <Name>` and commit the generated migration.

Garmin Connect activity summary CSVs can be passed as a file:

```sh
dotnet run --project tools/Garmin.Import -- ~/Downloads/Activities.csv
```

CSV starts are stored as local wall-clock times because the export does not include a timezone.
Known values are normalized to metric units while each row's original values are retained as JSON
in `ActivitySourceFiles`. Missing markers and malformed durations are stored as unknown. CSV rows
provide summaries only; they do not create record samples or individual lap rows. CSV and FIT
imports are labeled separately and are not automatically reconciled as the same activity.

Keep timestamps normalized to UTC. FIT units must be converted by the importer using the FIT
profile (for example, FIT semicircle coordinates to degrees and scaled distances/speeds to SI).

The FIT importer was tested against the copied archive in `~/Downloads/Garmin Activities` using a
temporary SQLite database: all 163 files decoded and imported (103,965 record samples and 509 lap
summaries), and a second run skipped all 163 as duplicates. The CSV importer was tested against
the Garmin Connect export: all 753 rows imported, including 753 local start times, and a second run
skipped all 753. These temporary databases were removed.

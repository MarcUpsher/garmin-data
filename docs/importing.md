# Imports and recovery

For mounted paths and existing database setup in Docker, see [Docker setup](docker.md).
Command-line import tools run directly on the host.

## Sync a watch over USB

Connect the watch, choose file-transfer mode if prompted, and wait for Linux to mount it. Then click
**Sync watch** in the application. The API looks for `GARMIN/Activity` in common removable-media and
GVFS mount locations, imports new FIT files, and skips unchanged files using the local device file
index and content hashes. The importer does not mount USB devices itself; the operating system must
make the watch files available first.

## Update cached performance efforts

Open **Trends → Performance** and click **Update efforts** to calculate or rebuild cached running
efforts for existing activities. New FIT files receive calculated efforts during import. The
algorithm version is stored with the results so they can be rebuilt when the calculation changes.
After upgrading to algorithm version 3, click **Update efforts** once to refresh existing results.
Recorded full-distance efforts prefer elapsed time so pauses are counted consistently with rolling
efforts; timer time is used only when the export has no elapsed duration.

## Restore data from a Garmin Connect export

Open **Data & Imports** to idempotently import sleep, daily wellness and VO₂ Max history. For a
full activity rebuild, catalog the archive FIT files and create a new database first. The activity
importer expects an empty activity table; review its output before switching `GARMIN_DB_PATH` to
the rebuilt database. Then run the wellness import against that database as well. The full-export
workflow and options are documented in the command examples below.

The wellness importer reads stress from Garmin's `TOTAL` daily aggregate, respiration from the
average waking value, hydration in millilitres, and Body Battery from its named daily statistics.
Ascending/descending floor measurements are stored in metres. Imported daily records carry a parser
version; importing the same export repairs records from an older parser without changing their IDs.
Data & Imports indicates when this is needed.

Sleep trends use measured deep/light sleep plus REM where recorded. Missing, invalid and zero-duration
nights are excluded from averages and counted separately. Earlier devices with no REM measurement
still contribute their measured deep/light sleep; missing REM is displayed as unavailable.

## Import data from the command line

All commands below run from the repository root. Imports use the same default SQLite database as
the API unless `--database` is supplied.

Import a Garmin Connect activity CSV (summary values only; no record samples or lap splits):

```sh
dotnet run --project tools/Garmin.Import -- /path/to/Activities.csv
```

Import FIT files from a directory or mounted watch:

```sh
dotnet run --project tools/Garmin.Import -- /path/to/GARMIN
```

Catalog FIT files in Garmin Connect ZIP archives without extracting all of them to disk:

```sh
dotnet run --project tools/Garmin.Import -- catalog-fit \
  /path/to/DI_CONNECT/DI-Connect-Uploaded-Files \
  --output /path/to/garmin-fit-catalog.csv
```

Rebuild activities from a full Garmin Connect export. Use a new database path first, inspect the
reported counts, and only then decide whether to replace an existing database:

```sh
dotnet run --project tools/Garmin.Import -- import-garmin-export /path/to/DI_CONNECT \
  --database /path/to/activities.rebuilt.db \
  --supplement-database /path/to/existing/activities.db
```

The export importer creates one activity per Garmin summary and links matching FIT files by exact
UTC start time. Summary JSON and FIT payloads are retained as source evidence. The optional
supplement database preserves existing FIT payloads that are absent from the Connect export when an
exact summary match exists.

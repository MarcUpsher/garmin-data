# Garmin Data

A local-first activity library and training-history explorer for Garmin data. The project is being
built around a simple idea: keep your own activity data on your computer, make it easy to inspect,
and make long-term trends more useful than the summaries offered by Garmin Connect. The product
name is still open.

## What it does today

- Imports FIT activities from a Garmin watch mounted over USB, Garmin Connect export archives, or a
  Garmin Connect activity CSV.
- Stores activity summaries, laps, timestamped FIT samples and import source evidence in SQLite.
- Browses, filters and hides activities; individual activities open in a side panel with laps and
  a synchronized heart-rate, pace and elevation chart.
- Shows running consistency over monthly or weekly periods, including zero-activity periods.
- Calculates cached best rolling 1, 2, 5, 6 and 10 km running efforts from FIT samples, with links
  back to the activity. These are observed training efforts, not assumed race results or
  standardized course performances.

The direct Garmin Bluetooth transport is still research work and is not part of the application
sync flow. USB sync currently reads FIT files after Linux mounts the watch as storage. Other Garmin
export families such as sleep, daily wellness and historical VO₂ max are not imported yet.

## Project layout

This is a small monorepo. The frontend and backend are separate applications in one Git repository;
Nx is not currently used.

| Path | Purpose |
| --- | --- |
| `apps/web` | Angular and Angular Material user interface |
| `apps/Garmin.Api` | Local ASP.NET Core API and watch-sync endpoint |
| `src/Garmin.Data` | SQLite entities, EF Core context, migrations and import logic |
| `tools/Garmin.Import` | Command-line tools for FIT, CSV and Garmin Connect exports |
| `Garmin.sln` | .NET solution |

## Run locally on Linux

### Requirements

- .NET 10 SDK
- Node.js 22.22.3 or later in the 22.x line, or 24.15.0 or later in the 24.x line, and npm

Install the frontend dependencies once:

```sh
npm ci --prefix apps/web
```

Run the API and frontend in separate terminals from the repository root:

```sh
dotnet run --project apps/Garmin.Api
```

```sh
npm start --prefix apps/web
```

Open <http://localhost:4200>. The Angular development server forwards `/api` requests to the API at
`http://127.0.0.1:5080`. On startup, the API applies pending EF Core migrations and uses
`~/.local/share/garmin-data/activities.db` by default. Set `GARMIN_DB_PATH` to use another database:

```sh
GARMIN_DB_PATH=/path/to/activities.db dotnet run --project apps/Garmin.Api
```

The database is local application data and is not part of the repository. Treat Garmin export
archives, catalog files and database backups as private personal data.

### Sync a watch over USB

Connect the watch, choose file-transfer mode if prompted, and wait for Linux to mount it. Then click
**Sync watch** in the application. The API looks for `GARMIN/Activity` in common removable-media and
GVFS mount locations, imports new FIT files, and skips unchanged files using the local device file
index and content hashes. The importer does not mount USB devices itself; the operating system must
make the watch files available first.

### Update cached performance efforts

Open **Trends → Performance** and click **Update efforts** to calculate or rebuild cached running
efforts for existing activities. New FIT files receive calculated efforts during import. The
algorithm version is stored with the results so they can be rebuilt when the calculation changes.

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

## Build and test

```sh
dotnet build apps/Garmin.Api/Garmin.Api.csproj
dotnet build tools/Garmin.Import/Garmin.Import.csproj
npm run build --prefix apps/web
npm test --prefix apps/web
```

EF Core migrations are in `src/Garmin.Data/Data/Migrations`. The pinned `dotnet-ef` tool can be
restored with `dotnet tool restore`. Create a migration from the repository root with:

```sh
dotnet tool run dotnet-ef migrations add <Name> \
  --project src/Garmin.Data/Garmin.Data.csproj \
  --startup-project apps/Garmin.Api/Garmin.Api.csproj
```

## Packaging and Docker

The two-process development setup above is the current runnable form, not the intended final Linux
desktop experience. A later packaging step could bundle the Angular UI and launch the local API from
a desktop shell, or provide a simple native launcher. That would keep database access and mounted
watch discovery close to Linux and avoid requiring users to manage terminals.

Docker may still be useful as an optional reproducible development or headless/server deployment.
It is not implemented yet, and it is unlikely to be the best default for the desktop app: USB sync
needs access to the host's mounted watch path and file permissions, while future Bluetooth work
would need access to the host Bluetooth service and adapter. Containerizing those integrations is
possible, but adds host-device and mount configuration. The practical direction is to keep the
application runnable directly on Linux, then add Docker only if a concrete development or remote
deployment use case warrants it.

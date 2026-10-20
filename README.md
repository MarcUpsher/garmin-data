# Garmin activity library

A local-first activity browser and importer for Garmin data. The product name is still open.

## Repository layout

- `apps/Garmin.Api` — local, read-only HTTP API
- `apps/web` — Angular activity browser
- `src/Garmin.Data` — EF Core model, migrations, and SQLite access
- `tools/Garmin.Import` — FIT and Garmin Connect CSV import CLI
- `Garmin.sln` — .NET solution

This is a lightweight monorepo using Angular CLI for the frontend and .NET solution tooling for
the backend. Nx is not included yet.

## Run the first vertical slice

Start the API in one terminal:

```sh
dotnet run --project apps/Garmin.Api
```

Start the Angular app in another:

```sh
npm start --prefix apps/web
```

Open `http://localhost:4200`. The API listens on `127.0.0.1:5080`, reads
`~/.local/share/garmin-data/activities.db`, and applies EF migrations on startup. Angular proxies
`/api` requests to the local API. `GET /api/activities` returns a paged summary list and accepts
`page`, `pageSize` (1–100), and an exact `type` filter. `GET /api/health` is a local health check.

With the watch connected over USB and its storage mounted, use **Sync watch** in the top bar. The
API searches standard Linux removable-media locations (including the user GVFS mount) for
`GARMIN/Activity`, imports new FIT files into the same database, skips files already imported by
content hash, and refreshes the activity list. If the watch does not appear as a mounted file
system, open it in the file manager and choose file transfer mode if prompted; USB device access
and mounting are handled by Linux, not by the app. The app records each imported device path,
file size, and modification time so later syncs can skip unchanged FIT contents. New or changed
files are still hashed and checked against existing activities, and unchanged files are rehashed
after 30 days as a periodic integrity check.

## Catalog FIT files in a Garmin export

To inspect the FIT archive contents without importing them or extracting the archives:

~~~sh
dotnet run --project tools/Garmin.Import -- catalog-fit "/path/to/DI-Connect-Uploaded-Files" --output "/path/to/garmin-fit-catalog.csv"
~~~

The cataloger processes one entry at a time, writes each entry to CSV with a SHA-256 digest, embedded FIT file type, and session/lap/record message counts, and deletes its temporary working file. It does not open or modify the application database. The CSV can contain private archive entry names; keep it with the local export.

## Import activities

By default, imports use `~/.local/share/garmin-data/activities.db`, the same database the API reads.
To rebuild canonical activities from a full Garmin Connect export, first generate the FIT catalog and
then import into a fresh database:

~~~sh
dotnet run --project tools/Garmin.Import -- catalog-fit "/path/to/DI_CONNECT/DI-Connect-Uploaded-Files"
dotnet run --project tools/Garmin.Import -- import-garmin-export "/path/to/DI_CONNECT" \
  --database "/path/to/activities.rebuilt.db" \
  --supplement-database "/path/to/existing/activities.db"
~~~

The export importer creates one activity per Garmin summary, links matching FIT data by exact UTC
session start time, and keeps both summary JSON and FIT payloads as source evidence. It imports
Activity FITs only; monitoring, sleep, daily wellness, VO2 Max, training history, and other export
families still need their own data models and importers. Use a new database path for a rebuild and
review the reported counts before replacing an existing database. The optional supplement database
preserves existing FIT payloads absent from the Connect ZIP export when an exact summary match exists.

To import a Garmin Connect activity CSV:

```sh
dotnet run --project tools/Garmin.Import -- ~/Downloads/Activities.csv
```

To import FIT files from a directory or mounted watch:

```sh
dotnet run --project tools/Garmin.Import -- /run/media/$USER/GARMIN
```

The importer finds `.fit` files recursively and supports `GARMIN/Activity`. Linux must mount the
watch as a filesystem; automatic device detection/mounting is not implemented. Both import paths
run EF migrations and skip already imported source records by SHA-256. CSV rows are labeled
`GarminConnectCsv`, store their timezone-unspecified dates as local wall-clock values, and retain
the row values as JSON in `ActivitySourceFiles`. Standalone CSV and watch-folder imports remain separate source records; the full-export command above links summaries and FIT data by exact UTC
start time. CSVs provide summaries, not time-series samples or individual lap splits.

Use `--database /path/to/file.db` to select another database for an import or for the API via the
`GARMIN_DB_PATH` environment variable.

## EF Core migrations

The local `dotnet-ef` tool version is pinned in `dotnet-tools.json`. From the repository root:

```sh
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name> \
  --project src/Garmin.Data/Garmin.Data.csproj \
  --startup-project src/Garmin.Data/Garmin.Data.csproj
```

The importer and API apply migrations on startup. Timestamps from FIT are normalized to UTC. CSV
dates have no timezone metadata and remain separate local wall-clock values. The schema includes
optional laps, common record samples, summary metrics, and retained source payloads.

## Build and verify

```sh
dotnet build apps/Garmin.Api/Garmin.Api.csproj
dotnet build tools/Garmin.Import/Garmin.Import.csproj
npm run build --prefix apps/web
npm test --prefix apps/web
```

The importer was verified against the sample archive (163 FIT files, 103,965 record samples, 509
lap summaries) and the Garmin Connect CSV export (753 summary rows). The local SQLite database and
all activity exports are excluded from Git.

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
- Imports dated sleep, daily wellness and VO₂ Max records from a Garmin Connect export and charts
  sleep and VO₂ Max history in Trends.

The direct Garmin Bluetooth transport is still research work and is not part of the application
sync flow. USB sync currently reads FIT files after Linux mounts the watch as storage. Use **Data &
Imports** in the application for historical sleep, daily wellness and VO₂ Max records. The wider
Garmin export also contains other datasets that are not imported yet.

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
After upgrading to algorithm version 3, click **Update efforts** once to refresh existing results.
Recorded full-distance efforts prefer elapsed time so pauses are counted consistently with rolling
efforts; timer time is used only when the export has no elapsed duration.

### Restore data from a Garmin Connect export

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

## Build and test

```sh
dotnet build apps/Garmin.Api/Garmin.Api.csproj
dotnet build tools/Garmin.Import/Garmin.Import.csproj
npm run build --prefix apps/web
npm test --prefix apps/web
dotnet run --project tests/Garmin.Data.Checks
```

The data checks use synthetic records and a temporary SQLite database, with no additional test-runner
packages. To also validate migrations and reimporting against a consistent copy of an existing
database, supply the export folder and database path:

```sh
dotnet run --project tests/Garmin.Data.Checks -- /path/to/DI_CONNECT /path/to/activities.db
```

This optional check opens the source database read-only, backs it up to a temporary database,
and migrates and repairs only the copy. Both temporary databases are removed when the checks finish.

EF Core migrations are in `src/Garmin.Data/Data/Migrations`. The pinned `dotnet-ef` tool can be
restored with `dotnet tool restore`. Create a migration from the repository root with:

```sh
dotnet tool run dotnet-ef migrations add <Name> \
  --project src/Garmin.Data/Garmin.Data.csproj \
  --startup-project apps/Garmin.Api/Garmin.Api.csproj
```

## Packaging and Docker

Docker packaging is being added in stages. The first stage builds the Angular production UI and
.NET API into one image. The API serves both at the same address, including direct links to activity
and trends pages. Docker Engine and the Compose plugin are required; the host does not need Node.js
or the .NET SDK to build or run the image.

From the repository root:

```sh
cp .env.example .env
docker compose up --build -d
```

Open <http://localhost:5080>. Set `GARMIN_PORT` in `.env` to use a different port, and set `TZ` to
your local IANA timezone. `.env` stays outside Git. The published port is bound to loopback, so the
application is available only on this computer. The container runs as the .NET image's unprivileged
application user.

The initial Compose setup creates a **new, empty database** in the `app-data` Docker volume, mounted
at `/data`. It does not select the database used by the development API. The whole directory is
persistent, including SQLite's journal files, and EF migrations run when the API starts. Database
files and Garmin exports are excluded from both Git and the Docker build context.

```sh
docker compose logs --tail 100 -f
docker compose stop
docker compose start
```

`docker compose down` removes the container but retains its data volume. **Adding `--volumes`
deletes that database.** The restart policy is `unless-stopped`; automatic startup after a reboot
also requires the Docker daemon to start on the host. Rebuild with `docker compose up --build -d`
after changing the application code.

### Native Linux: USB watch and existing data

`compose.linux.yaml` adds USB storage and Garmin export access. Linux still mounts the watch;
Docker reads its mounted files. This setup uses native Docker Engine, not Docker Desktop. It
does not require a privileged container or access to raw USB devices.

Before switching, stop the development API so subsequent imports go to one database. Create a
consistent backup and a **separate working copy** of the current database (Python 3 is required):

```sh
python3 scripts/prepare-docker-data.py \
  "${XDG_DATA_HOME:-$HOME/.local/share}/garmin-data/activities.db" ./local-data
```

The script uses SQLite's online backup API, including committed WAL changes, checks the snapshot,
and copies it to `local-data/activities.db`. The backup is in `local-data/backups`. It refuses to
overwrite an existing target database. All activities, hidden flags, wellness data and source
evidence are preserved. The original database remains available for rollback. Once you switch,
new imports and edits go to the container copy; switching back later requires a fresh backup of
that copy to retain those changes.

Add these settings to the ignored `.env` file, using the values reported by `id -u` and `id -g`:

```dotenv
COMPOSE_FILE=compose.yaml:compose.linux.yaml
GARMIN_UID=1000
GARMIN_GID=1000
GARMIN_DATA_DIRECTORY=./local-data
GARMIN_WATCH_MOUNT_ROOT=/run/media
GARMIN_IMPORT_DIRECTORY=./local-data/imports
```

Run `docker compose up --build -d`. The Linux file replaces the database volume with the configured
host directory. Running as your numeric user ID allows access to desktop-mounted files and keeps
database files owned by you. If you use another data directory, ensure it exists and is writable
by that user. Never run the container and a development API against the same database concurrently.

Set `GARMIN_WATCH_MOUNT_ROOT` to the **parent** of the mounted watches, not an individual watch's
volume. It is exposed at `/watch` with `rslave` propagation, so newly attached mounts become visible
without restarting the app. The host's containing mount must support propagation
(`findmnt -T /run/media -o TARGET,PROPAGATION` should report `shared`). Docker Desktop and some
rootless Docker setups cannot provide this behaviour. Test sync, unplugging and reconnecting before
relying on it. The host desktop must mount the watch again after reconnecting.

The parent bind and USB mounts present at container creation are read-only. New USB submounts
propagated after startup inherit the host mount flags and can be writable inside the container.
The importer itself only reads watch files; the read-only bind is not a guarantee against writes
to newly propagated devices. Host-enforced read-only device mounts are needed for that guarantee.

For Garmin Connect history, set `GARMIN_IMPORT_DIRECTORY` to the extracted `DI_CONNECT` folder,
quoting values that contain spaces. Restart with `docker compose up -d` after changing settings,
then enter **`/imports`** on the application's Data & Imports page. The container sees this folder
read-only; paths previously saved in the browser from running directly on the host may need updating.

For direct Linux runs, `GARMIN_WATCH_ROOTS` optionally overrides desktop discovery with roots
separated by the platform path separator (`:` on Linux). The Linux Compose file sets it to `/watch`.
Discovery runs on each sync request, so an absent watch is reported without stopping the API.

To run automatically after boot, enable the host's Docker service once with
`sudo systemctl enable --now docker.service`. Compose's `unless-stopped` policy then restarts the
container with Docker. A manually stopped container stays stopped until you run `docker compose
start`. Database backups can use the same Python script with the container's current database as
the source and a new destination directory.

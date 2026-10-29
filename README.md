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

## Run the app

Docker Engine and the Compose plugin are required. On a fresh checkout, copy `.env.example` to
`.env` and review its settings, then run from the repository root:

```sh
docker compose up --build -d
```

Open <http://localhost:5080>. The base configuration starts with an empty persistent database.
For USB watch access and moving an existing database, follow [Docker setup](docs/docker.md).
Machine-specific settings stay in the ignored `.env` file; databases and exports stay outside Git.

## Project layout

The frontend and backend are separate applications in one repository. The shared .NET library holds
recorded data, derived calculations and import logic; the API and command-line tool both use it.

| Path | Purpose |
| --- | --- |
| `apps/Garmin.Api/Program.cs` | API startup and endpoint registration |
| `apps/Garmin.Api/Endpoints` | Activity, trend, wellness and sync endpoints |
| `apps/Garmin.Api/Contracts` | API request and response types |
| `apps/web/src/app/shell` | Application layout, navigation and theme scope |
| `apps/web/src/app/activities` | Activity list, detail view and hide dialog |
| `apps/web/src/app/trends` | Historical charts and activity drill-down |
| `apps/web/src/app/data-management` | Imports page |
| `apps/web/src/app/shared` | API client, data types and shared preferences/helpers |
| `src/Garmin.Data/Entities` | Recorded activity and wellness entities |
| `src/Garmin.Data/Data` | EF Core context and migrations |
| `src/Garmin.Data/Analysis` | Derived metrics and calculation rules |
| `src/Garmin.Data/Importing` | FIT, CSV and Garmin Connect import logic |
| `tools/Garmin.Import` | Command-line import entry point |
| `tests/Garmin.Data.Checks` | Backend checks using disposable databases |
| `scripts` | Database backup and setup utilities |
| `docs` | Setup, development and recovery instructions |
| `Dockerfile`, `compose*.yaml` | Container build and runtime configuration |
| `Garmin.sln` | .NET solution |

## Further reading

- [Docker setup, USB access and database storage](docs/docker.md)
- [Local development, builds, checks and migrations](docs/development.md)
- [Imports, performance rebuilds and recovery](docs/importing.md)
- [Frontend structure and routes](apps/web/README.md)

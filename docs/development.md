# Development

All commands run from the repository root. For everyday use, see [Docker setup](docker.md).

## Run without Docker

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

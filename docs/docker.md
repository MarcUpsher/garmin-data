# Docker setup

The Docker build packages the Angular production UI and .NET API into one image. The API serves both at the same address, including direct links to activity
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

The base Compose setup creates a **new, empty database** in the `app-data` Docker volume, mounted
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

## Native Linux: USB watch and existing data

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

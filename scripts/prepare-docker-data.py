#!/usr/bin/env python3
"""Make a consistent SQLite backup and a separate working copy for Docker."""
import argparse
from contextlib import closing
from datetime import datetime, timezone
from pathlib import Path
import shutil
import sqlite3
import sys


def prepare(source: Path, destination: Path) -> None:
    source = source.expanduser().resolve(strict=True)
    destination = destination.expanduser().resolve()
    target = destination / "activities.db"
    if target.exists():
        raise FileExistsError(f"Refusing to overwrite {target}")
    destination.mkdir(parents=True, exist_ok=True, mode=0o700)
    backups = destination / "backups"
    backups.mkdir(exist_ok=True, mode=0o700)
    (destination / "imports").mkdir(exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    backup = backups / f"activities-before-docker-{stamp}.db"
    backup.touch(mode=0o600, exist_ok=False)
    # SQLite's backup API includes committed WAL data while another process is
    # using the source. Copying only the original .db file would not guarantee that.
    with closing(sqlite3.connect(source.as_uri() + "?mode=ro", uri=True)) as original:
        with closing(sqlite3.connect(backup)) as snapshot:
            original.backup(snapshot)
            if snapshot.execute("PRAGMA quick_check").fetchone()[0] != "ok":
                raise RuntimeError("Database backup failed its integrity check")
            counts = {table: snapshot.execute(f'SELECT count(*) FROM "{table}"').fetchone()[0]
                      for table in ("Activities", "SleepRecords", "DailyWellnessRecords", "Vo2MaxRecords")}
    # Exclusive creation protects an existing destination, even if it appears
    # after the initial check. This new directory belongs to the invoking user.
    with target.open("xb") as output, backup.open("rb") as input_file:
        target.chmod(0o600)
        shutil.copyfileobj(input_file, output)
    print(f"Backup: {backup}")
    print(f"Container database: {target}")
    print("Copied records: " + ", ".join(f"{table}={count}" for table, count in counts.items()))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Existing SQLite database")
    parser.add_argument("destination", type=Path, help="New container data directory")
    args = parser.parse_args()
    try:
        prepare(args.source, args.destination)
    except (OSError, sqlite3.Error, RuntimeError) as error:
        sys.exit(str(error))

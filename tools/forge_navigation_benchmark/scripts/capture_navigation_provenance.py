"""Capture an immutable laboratory source archive and exact SHA-256 inventory."""

import datetime
import hashlib
import json
from pathlib import Path
import sys
import zipfile


lab = Path(sys.argv[1]).resolve(strict=True)
destination = Path(sys.argv[2]).resolve()
archive = destination.with_suffix(".zip")
if destination.exists() or archive.exists():
    raise SystemExit("Refusing to overwrite provenance or source archive")

inventory = []
with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED) as bundle:
    for source in sorted(lab.rglob("*")):
        relative = source.relative_to(lab)
        if not source.is_file() or any(part in {"target", ".git", "__pycache__"} for part in relative.parts):
            continue
        payload = source.read_bytes()
        inventory.append({"path": relative.as_posix(), "bytes": len(payload),
                          "sha256": hashlib.sha256(payload).hexdigest()})
        bundle.writestr(relative.as_posix(), payload)

report = {
    "schema": "forge.navigation.source-provenance",
    "version": 1,
    "captured_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "source_root": str(lab),
    "source_archive": str(archive),
    "source_archive_sha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
    "inventory": inventory,
}
shared_inventory = []
for crate_name in ("forge_core", "forge_project", "forge_world"):
    crate_root = lab.parents[1] / "crates" / crate_name
    for source in sorted(crate_root.rglob("*")):
        if source.is_file() and source.suffix in {".rs", ".toml"}:
            payload = source.read_bytes()
            shared_inventory.append({"path": str(source), "bytes": len(payload),
                                     "sha256": hashlib.sha256(payload).hexdigest()})
report["shared_path_dependency_inventory"] = shared_inventory
with destination.open("x", encoding="utf-8") as output:
    json.dump(report, output, indent=2)
    output.write("\n")
print(json.dumps({"files": len(inventory), "provenance": str(destination),
                  "archive_sha256": report["source_archive_sha256"]}))

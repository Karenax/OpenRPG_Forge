"""Add a pinned source ground-bounce export to an isolated scene bundle."""
import hashlib
import json
import os
import sys
from pathlib import Path

if len(sys.argv) != 4:
    raise SystemExit("Usage: prepare-ground-bounce-bundle.py SOURCE_BUNDLE EXPORT NEW_BUNDLE")
source, acquired, target = (Path(value).resolve() for value in sys.argv[1:])
if not source.is_dir() or not acquired.is_dir() or target.exists():
    raise SystemExit("Existing source/export and new target required")
if target.is_relative_to(source) or target.is_relative_to(acquired):
    raise SystemExit("Target must not be inside an input")
if source.drive != target.drive:
    raise SystemExit("Hard-linked bundle requires the same drive")

hash_bytes = lambda data: hashlib.sha256(data).hexdigest()
old_fixture_path = source / "scene" / "fixture.json"
old_index_path = source / "preview-scene.json"
old_fixture_bytes = old_fixture_path.read_bytes()
old_index_bytes = old_index_path.read_bytes()
fixture = json.loads(old_fixture_bytes)
index = json.loads(old_index_bytes)
metadata = json.loads((acquired / "ground-bounce.json").read_bytes())
if (fixture["source"]["commit"] != metadata["source_commit"]
        or index["source_commit"] != metadata["source_commit"]
        or metadata["schema"] != "forge.tidewater.ground-bounce-probe"
        or metadata["version"] != 1):
    raise SystemExit("Pinned source mismatch")

new_files = {}
for name in ("ground-bounce.json", "ground-bounce-raw.rgba16.bin", "ground-bounce.rgba16.bin"):
    data = (acquired / name).read_bytes()
    if name.endswith(".bin") and hash_bytes(data) != metadata["files"][name]:
        raise SystemExit(f"Export hash mismatch: {name}")
    new_files[name] = data

# Existing files share storage; both JSON manifests are written as new files.
for path in source.rglob("*"):
    relative = path.relative_to(source)
    destination = target / relative
    if path.is_dir():
        destination.mkdir(parents=True, exist_ok=True)
    elif relative.as_posix() not in {"scene/fixture.json", "preview-scene.json"}:
        if not path.is_file() or path.is_symlink():
            raise SystemExit(f"Unexpected source item: {relative}")
        destination.parent.mkdir(parents=True, exist_ok=True)
        os.link(path, destination)

for name, data in new_files.items():
    (target / "scene" / name).write_bytes(data)
    fixture["files"][name] = hash_bytes(data)
    index["files"][f"scene/{name}"] = {"bytes": len(data), "sha256": hash_bytes(data)}
fixture.setdefault("limitations", []).append("ground_bounce_fixed_sun_not_dynamic_bake")
fixture_bytes = (json.dumps(fixture, indent=2, ensure_ascii=False) + "\n").encode("utf-8")
(target / "scene" / "fixture.json").write_bytes(fixture_bytes)
index["files"]["scene/fixture.json"] = {
    "bytes": len(fixture_bytes), "sha256": hash_bytes(fixture_bytes)
}
(target / "preview-scene.json").write_text(
    json.dumps(index, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
)
if old_fixture_path.read_bytes() != old_fixture_bytes or old_index_path.read_bytes() != old_index_bytes:
    raise SystemExit("Input manifest changed")
for name, data in new_files.items():
    if (target / "scene" / name).read_bytes() != data:
        raise SystemExit(f"Output mismatch: {name}")
print(json.dumps({"bundle": str(target), "source_fixture_sha256": hash_bytes(old_fixture_bytes),
                  "ground_bounce_sha256": metadata["files"]["ground-bounce.rgba16.bin"],
                  "file_count": sum(path.is_file() for path in target.rglob("*"))}))

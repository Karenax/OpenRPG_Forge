"""Prepare a camera-only proof snapshot; never overwrite authored inputs or re-export assets."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def save(path, value):
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2, allow_nan=False)
        stream.write("\n")


def extend_cameras(world, additions):
    if not additions or len(additions) > 8:
        raise ValueError("require 1..8 additional proof cameras")
    candidate = copy.deepcopy(world)
    existing = {camera["id"] for camera in world["validation_cameras"]}
    for camera in additions:
        identifier = camera.get("id", "")
        if (not identifier or identifier in existing
                or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789_-" for c in identifier)):
            raise ValueError("unsafe or repeated proof camera ID")
        existing.add(identifier)
        candidate["validation_cameras"].append(copy.deepcopy(camera))
    return candidate


def check_geometry(base, candidate):
    # Compare full output documents, not merely counts. Lineage-only caches have different hashes.
    for name in ("terrain.v1.json", "terrain-carved.v2.json", "vegetation.v1.json"):
        if read(base / name) != read(candidate / name):
            raise ValueError("camera-only snapshot changed " + name)
    for name in ("semantic-masks.v1.json", "spline-carving.v1.json",
                 "traversal-audit.v2.json", "bridge-placements.v1.json"):
        before, after = read(base / name), read(candidate / name)
        for directory, document in ((base, before), (candidate, after)):
            report = read(directory / "world-sketch.compilation.v4.json")
            lineage = {"source_world_sketch_sha256": "source_world_sketch_sha256"}
            if name == "bridge-placements.v1.json":
                lineage["source_traversal_audit_sha256"] = "traversal_audit_sha256"
            for field, report_field in lineage.items():
                if document.pop(field) != report[report_field]:
                    raise ValueError("invalid derived lineage: " + name + ":" + field)
        if before != after:
            raise ValueError("camera-only snapshot changed derived content: " + name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("cli", "world-sketch", "preview", "additional-cameras", "source-root", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    paths = {name: getattr(args, name).resolve(strict=True)
             for name in ("cli", "world_sketch", "preview", "additional_cameras")}
    pins = {name: digest(path) for name, path in paths.items()}
    source = read(paths["world_sketch"])
    candidate = extend_cameras(source, read(paths["additional_cameras"]))
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    save(output / "world-sketch.v1.json", candidate)
    compilations = []
    for label, document in (("base", paths["world_sketch"]), ("camera-set", output / "world-sketch.v1.json")):
        destination = output / label
        command = [str(paths["cli"]), "compile-world-sketch", str(document), str(destination),
                   "--source-root", str(args.source_root.resolve(strict=True))]
        result = subprocess.run(command, capture_output=True, text=True, timeout=120, check=False)
        save(output / (label + "-command.json"), {"command": command, "exit_code": result.returncode,
                                                 "stdout": result.stdout, "stderr": result.stderr})
        if result.returncode:
            raise ValueError(label + " compilation failed; outputs retained")
        compilations.append(read(destination / "world-sketch.compilation.v4.json"))
    check_geometry(output / "base", output / "camera-set")
    preview = read(paths["preview"])
    if (preview["source_world_sketch_sha256"] != compilations[0]["source_world_sketch_sha256"]
            or preview["source_terrain_sha256"] != compilations[0]["carved_terrain_sha256"]):
        raise ValueError("preview does not pin the base recipe/terrain")
    derived_preview = copy.deepcopy(preview)
    derived_preview["source_world_sketch_sha256"] = compilations[1]["source_world_sketch_sha256"]
    if any(digest(path) != pins[name] for name, path in paths.items()):
        raise ValueError("input changed while preparing proof snapshot")
    save(output / "runtime-preview.v1.json", derived_preview)
    save(output / "camera-set-evidence.json", {
        "schema": "forge.camera-only-proof-snapshot", "schema_version": 1,
        "status": "prepared_not_rendered_or_human_accepted",
        "inputs": {name: {"path": str(path), "sha256": pins[name]} for name, path in paths.items()},
        "canonical_sources_modified": False, "geometry_outputs_identical": True,
        "preview_changed_fields": ["source_world_sketch_sha256"],
        "world_sketch_changed_fields": ["validation_cameras"],
        "base_world_sketch_sha256": compilations[0]["source_world_sketch_sha256"],
        "snapshot_world_sketch_sha256": compilations[1]["source_world_sketch_sha256"],
        "camera_ids": [camera["id"] for camera in candidate["validation_cameras"]],
        "outputs": {name: digest(output / name) for name in
                    ("world-sketch.v1.json", "runtime-preview.v1.json")},
    })
    print("CAMERA_ONLY_SNAPSHOT_PREPARED cameras=" + str(len(candidate["validation_cameras"])))


if __name__ == "__main__":
    main()

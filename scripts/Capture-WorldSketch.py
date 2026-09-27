"""Collect existing authored cameras, clear/rain, without changing source documents.

Development evidence only: no artistic acceptance or performance qualification.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def save(path, value):
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2)
        stream.write("\n")


def verify_fixed(run, preview, quality, batches):
    """Frozen optical captures deliberately exclude frame-performance measurements."""
    events = [json.loads(line) for line in (run / "runtime.jsonl").read_text(
        encoding="utf-8").splitlines() if line.startswith("{")]
    def one(kind):
        found = [event for event in events if event.get("event_kind") == kind]
        if len(found) != 1:
            raise ValueError("expected one " + kind)
        return found[0]
    if any(event.get("level") == "ERROR" for event in events):
        raise ValueError("runtime ERROR")
    stop = one("runtime_stopped")
    if not stop.get("success") or stop.get("runtime_errors_observed"):
        raise ValueError("unclean shutdown")
    ready = one("capture_requested")
    if ready.get("pipelines_ready", 0) <= 0 or ready.get("stable_observations", 0) < max(
            30, ready.get("required_stable_observations", 0)):
        raise ValueError("fixed capture not ready")
    one("capture_verified")
    if not any(e.get("event_kind") == "fixed_view_stability_observed"
               and e.get("camera_id") == preview["camera_id"]
               and e.get("current_view_matches") for e in events):
        raise ValueError("fixed camera not observed")
    one("governed_asset_kit_loaded")
    one("world_sketch_bridge_projected")
    terrain = [e for e in events if e.get("event_kind") == "terrain_lod_projected"]
    if not terrain or any(e.get("terrain_hash") != preview["source_terrain_sha256"]
                          or e.get("tier", "").lower() != quality for e in terrain):
        raise ValueError("terrain projection mismatch")
    if batches and not any(e.get("event_kind") == "governed_far_batches_projected"
                           and e.get("batches", 0) > 0 for e in events):
        raise ValueError("far batches missing")
    header = (run / "capture.png").read_bytes()[:24]
    if header[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", header[16:24]) != (1920, 1080):
        raise ValueError("not a Full HD PNG")
    return {"capture_readiness": ready, "performance_qualified": False,
            "sha256": {name: digest(run / name) for name in
                       ("capture.png", "runtime.jsonl", "preview.json")}}


def verify(run, preview, weather, *, quality=None, forest=False, batches=False):
    events = [json.loads(line) for line in (run / "runtime.jsonl").read_text(
        encoding="utf-8").splitlines() if line.startswith("{")]
    if any(event.get("level") == "ERROR" for event in events):
        raise ValueError("runtime emitted ERROR")
    stops = [event for event in events if event.get("event_kind") == "runtime_stopped"]
    if not stops or not stops[-1].get("success") or stops[-1].get("runtime_errors_observed"):
        raise ValueError("successful runtime shutdown not observed")
    if not any(event.get("event_kind") == "governed_asset_kit_loaded" for event in events):
        raise ValueError("asset kit did not finish loading")
    if not any(event.get("event_kind") == "world_sketch_bridge_projected" for event in events):
        raise ValueError("derived bridge missing")
    header = (run / "capture.png").read_bytes()[:24]
    if header[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", header[16:24]) != (1920, 1080):
        raise ValueError("capture is not a Full HD PNG")
    metrics = json.loads((run / "metrics.json").read_text(encoding="utf-8"))
    if quality is not None:
        observed = metrics.get("global_quality", {})
        if (observed.get("requested_tier") != quality
                or not observed.get("available_budgets_projected")):
            raise ValueError("requested quality budgets not projected")
    if forest:
        vegetation = metrics.get("vegetation", {})
        observed = vegetation.get("prototype_preview", {})
        expected = vegetation.get("placements", {}).get("accepted_by_kind", {}).get("trees", 0)
        if (expected <= 0 or observed.get("trees") != expected
                or observed.get("primitive_entities", 0) <= 0
                or observed.get("procedural_geometry_counts_valid") is not False):
            raise ValueError("governed forest projection incomplete")
    if batches and not any(event.get("event_kind") == "governed_far_batches_projected"
                           and event.get("batches", 0) > 0 for event in events):
        raise ValueError("governed far batches missing")
    if metrics["source_hash"] != preview["source_terrain_sha256"]:
        raise ValueError("terrain hash mismatch")
    if metrics["weather"]["target_state"] != weather:
        raise ValueError("weather state mismatch")
    kit = metrics["governed_asset_kit"]
    for key in ("camera_id", "source_world_sketch_sha256", "source_terrain_sha256",
                "input_manifest_sha256"):
        if kit[key] != preview[key]:
            raise ValueError(f"asset-kit {key} mismatch")
    if (not kit["validation_camera_spawned"] or not kit["all_scene_dependencies_loaded"]
            or kit["loaded_scene_instances"] != len(preview["placements"])
            or kit["failed_scene_instances"] or kit["failed_collision_nodes"]
            or kit["ready_rapier_colliders"] != kit["configured_collision_nodes"]):
        raise ValueError("asset-kit projection incomplete")
    return {"frame_smoke_only": metrics["frame"], "gpu": metrics["gpu"],
            "sha256": {name: digest(run / name) for name in
                       ("capture.png", "metrics.json", "runtime.jsonl", "preview.json")}}


def verify_complete_coverage(report, world):
    """Require every pinned camera in both weather states; partial collections do not qualify."""
    cameras = world["validation_cameras"]
    identifiers = [camera["id"] for camera in cameras]
    if not identifiers or len(set(identifiers)) != len(identifiers):
        raise ValueError("empty or duplicate source camera IDs")
    expected = {camera["id"] + "-" + weather: (camera, weather)
                for camera in cameras for weather in ("clear", "rain")}
    observed = [run["id"] for run in report["runs"]]
    if len(observed) != len(expected) or set(observed) != set(expected):
        raise ValueError("incomplete or duplicate camera/weather coverage")
    for run in report["runs"]:
        camera, weather = expected[run["id"]]
        if run["camera"] != camera or run["weather"] != weather:
            raise ValueError("capture camera/weather differs from pinned source")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("runtime", "world-sketch", "kit-build", "preview", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--camera", help="Collect only this authored camera (clear and rain)")
    parser.add_argument("--capture-frame", type=int, default=240)
    parser.add_argument("--exit-after-frames", type=int, default=360)
    parser.add_argument("--quality", choices=("low", "medium", "high", "ultra"))
    parser.add_argument("--forest-catalog", type=Path,
                        help="Enable the governed forest preview with this pinned catalog")
    parser.add_argument("--forest-batches", action="store_true")
    parser.add_argument("--water-medium", type=Path)
    parser.add_argument("--cloud-profile", type=Path)
    args = parser.parse_args()
    if args.water_medium and not args.quality:
        parser.error("--water-medium requires --quality for fixed proof verification")
    if not 120 < args.capture_frame < args.exit_after_frames <= 3600:
        parser.error("require 120 < capture-frame < exit-after-frames <= 3600")
    sources = {name: getattr(args, name).resolve(strict=True) for name in
               ("runtime", "world_sketch", "kit_build", "preview")}
    if args.forest_catalog:
        sources["forest_catalog"] = args.forest_catalog.resolve(strict=True)
    if args.forest_batches and not args.forest_catalog:
        parser.error("--forest-batches requires --forest-catalog")
    for name in ("water_medium", "cloud_profile"):
        if getattr(args, name):
            sources[name] = getattr(args, name).resolve(strict=True)
    pins = {name: digest(path) for name, path in sources.items()}
    world = json.loads(sources["world_sketch"].read_text(encoding="utf-8"))
    preview = json.loads(sources["preview"].read_text(encoding="utf-8"))
    cameras = world["validation_cameras"]
    # IDs are used as directory names; reject unsafe or ambiguous values before creating outputs.
    ids = [camera["id"] for camera in cameras]
    if not ids or len(set(ids)) != len(ids) or any(
        not identifier or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789_-" for c in identifier)
        for identifier in ids
    ):
        raise ValueError("unsafe, empty or duplicate camera IDs")
    if args.camera:
        if args.camera not in ids:
            raise ValueError("requested camera is not in the WorldSketch")
        cameras = [camera for camera in cameras if camera["id"] == args.camera]
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    report = {"schema": "forge.world-sketch-capture-evidence", "schema_version": 1,
              "inputs": {name: {"path": str(path), "sha256": pins[name]}
                         for name, path in sources.items()},
              "status": "incomplete", "human_accepted": False,
              "video_verified": False, "quality": args.quality,
              "forest_preview": bool(args.forest_catalog),
              "forest_batches": args.forest_batches,
              "fixed_optical": bool(args.water_medium), "runs": []}
    env = dict(os.environ, RUST_LOG="info,wgpu_core=warn,wgpu_hal=warn,naga=warn")
    try:
        for camera in cameras:
            for weather in ("clear", "rain"):
                run = output / (camera["id"] + "-" + weather)
                run.mkdir()
                derived = dict(preview, camera_id=camera["id"])
                save(run / "preview.json", derived)
                command = [str(sources["runtime"]), "--world-sketch-visual-valley",
                           "--world-sketch-input", str(sources["world_sketch"]),
                           "--asset-kit-build", str(sources["kit_build"]),
                           "--asset-kit-preview", str(run / "preview.json"),
                           "--weather", weather, "--capture", str(run / "capture.png"),
                           "--capture-frame", str(args.capture_frame), "--metrics", str(run / "metrics.json"),
                           "--metrics-warmup-frames", "120", "--exit-after-frames", str(args.exit_after_frames)]
                if args.quality:
                    command.extend(("--quality", args.quality))
                if args.forest_catalog:
                    command.extend(("--prepare-vegetation-catalog",
                                    str(sources["forest_catalog"]), "--prototype-forest-batches"
                                    if args.forest_batches else "--prototype-forest-preview"))
                if args.water_medium:
                    index = command.index("--metrics")
                    del command[index:index + 4]
                    command.extend(("--fixed-proof-view", camera["id"]))
                    command.extend(("--water-depth-preview", "--water-medium-input",
                                    str(sources["water_medium"]), "--water-reflection-preview"))
                if args.cloud_profile:
                    command.extend(("--cloud-layer-preview", str(sources["cloud_profile"])))
                print("Capturing " + run.name, flush=True)
                with (run / "runtime.jsonl").open("xb") as log:
                    result = subprocess.run(command, stdout=log, stderr=log, env=env,
                                            timeout=120, check=False)
                if result.returncode != 0:
                    raise ValueError(f"{run.name}: runtime exit {result.returncode}")
                evidence = (verify_fixed(run, derived, args.quality, args.forest_batches)
                            if args.water_medium else verify(run, derived, weather, quality=args.quality,
                                  forest=bool(args.forest_catalog), batches=args.forest_batches))
                report["runs"].append(dict(id=run.name, camera=camera, weather=weather,
                                           command=command, **evidence))
        if any(digest(path) != pins[name] for name, path in sources.items()):
            raise ValueError("an input changed during capture")
        if args.camera is None:
            verify_complete_coverage(report, world)
        report["status"] = "capture_checks_passed_review_pending"
    finally:
        save(output / "manifest.json", report)
    print(f"Captured {len(report['runs'])} views; artistic review remains pending", flush=True)


if __name__ == "__main__":
    main()

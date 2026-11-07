"""Run a pinned procedural module twice; never claim Tidewater or release parity."""
import argparse
import bisect
import hashlib
import json
import math
import os
from pathlib import Path
import re
import statistics
import struct
import subprocess
import sys
import zlib
from datetime import datetime, timezone

import frame_intervals


HASH = re.compile(r"[0-9a-f]{64}\Z")
REPARSE_POINT = 0x400
PNG = b"\x89PNG\r\n\x1a\n"


def fail(message):
    raise ValueError(message)


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def physical(path, *, exists):
    if not path.is_absolute() or any(part in (".", "..") for part in path.parts):
        fail(f"Absolute normalized path required: {path}")
    for item in (path, *path.parents):
        try:
            info = item.lstat()
        except FileNotFoundError:
            continue
        if item.is_symlink() or getattr(info, "st_file_attributes", 0) & REPARSE_POINT:
            fail(f"Links and junctions are refused: {item}")
    if exists and not path.is_file():
        fail(f"A regular input file is required: {path}")
    if not exists and path.exists():
        fail(f"Fresh output path required: {path}")
    return path


def pinned(path, expected):
    physical(path, exists=True)
    if not HASH.fullmatch(expected) or digest(path) != expected:
        fail(f"Input hash differs: {path}")


def read_json(path):
    def invalid(value):
        fail(f"Non-finite JSON number: {value}")
    def unique(pairs):
        value = {}
        for key, item in pairs:
            if key in value:
                fail(f"Duplicate JSON field: {key}")
            value[key] = item
        return value
    value = json.loads(path.read_text(encoding="utf-8-sig"), parse_constant=invalid, object_pairs_hook=unique)
    if not isinstance(value, dict):
        fail(f"JSON object required: {path}")
    return value


def write_new(path, value):
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, indent=2, allow_nan=False)
        stream.write("\n")
        stream.flush()
        os.fsync(stream.fileno())


def check_plan(plan, head, archive):
    if (not isinstance(plan, dict) or plan.get("schema") != "forge.native-gameplay.scenario"
            or plan.get("version") != 1 or plan.get("source_hash") != head
            or plan.get("archive_hash") != archive):
        fail("Scenario schema or source pins differ")
    duration = plan.get("max_duration_s")
    steps = plan.get("steps")
    if type(duration) is not int or not 1 <= duration <= 600 or not isinstance(steps, list) or not 1 <= len(steps) <= 128:
        fail("Scenario duration or step count invalid")
    if not isinstance(steps[-1], dict) or steps[-1] != {"kind": "exit"}:
        fail("Scenario must end with the explicit exit step")
    if sum(isinstance(s, dict) and s.get("kind") == "move_to" for s in steps) < 2:
        fail("Scenario requires an outward and a return move_to")
    names = []
    capture_indices = []
    move_indices = []
    for index, step in enumerate(steps):
        if not isinstance(step, dict):
            fail("Invalid scenario step")
        if step.get("kind") == "move_to":
            move_indices.append(index)
        if step.get("kind") == "capture":
            name = step.get("name")
            if (not isinstance(name, str) or not name.endswith(".png") or name.startswith(".")
                    or any(char in name for char in "/\\:")):
                fail("Capture must be a simple PNG filename")
            names.append(name)
            capture_indices.append(index)
    if len(names) < 3 or len({os.path.normcase(name) for name in names}) != len(names):
        fail("Three distinct scenario captures are required")
    if not (capture_indices[0] < move_indices[0] < capture_indices[-1]
            and capture_indices[0] < move_indices[-1] < capture_indices[-1]
            and any(move_indices[0] < index < move_indices[-1] for index in capture_indices)):
        fail("Captures must document departure, the outward route and the return")
    return names


def check_build_receipt(receipt):
    if receipt.get("schema") != "forge.central-cargo-receipt.v1" or receipt.get("exit_code") != 0:
        fail("Successful central Cargo build receipt required")
    args = receipt.get("arguments")
    if not isinstance(args, list) or not all(isinstance(value, str) for value in args):
        fail("Invalid build receipt arguments")
    if "build" not in args or "forge_runtime" not in args or "forge-module" not in args:
        fail("Receipt must describe the forge-module build")
    features = []
    for index, value in enumerate(args):
        if value == "--features" and index + 1 < len(args):
            features.extend(args[index + 1].replace(",", " ").split())
        elif value.startswith("--features="):
            features.extend(value.split("=", 1)[1].replace(",", " ").split())
    if "qualification" not in features:
        fail("Receipt must include the qualification feature")


def distribution(values):
    if not values:
        return {"samples": 0, "mean_ms": None, "p95_ms": None}
    ordered = sorted(values)
    return {"samples": len(values), "mean_ms": statistics.fmean(values),
            "p95_ms": ordered[math.ceil(len(ordered) * 0.95) - 1]}


def analyze(report, rows, mode, pins, minimum_distance, return_tolerance, require_static):
    if (report.get("schema") != "forge.module-rpg-runtime-report" or report.get("version") != 2
            or report.get("runtime_exit_success") is not True or report.get("projection_ready") is not True):
        fail("Runtime report did not validate the procedural projection")
    expected = report.get("expected", {})
    if not isinstance(expected, dict):
        fail("Runtime report expected identity missing")
    if expected.get("source_hash") != pins["head"] or expected.get("area_id") != pins["area"] or expected.get("module_id") != pins["module_id"]:
        fail("Runtime report module, area or head differs")
    terrain = expected.get("procedural_streaming")
    observed = report.get("final_observation", {})
    if not isinstance(observed, dict):
        fail("Final runtime observation missing")
    actual = observed.get("procedural_streaming")
    static = observed.get("static_hlod")
    if not isinstance(terrain, dict) or not isinstance(actual, dict) or not isinstance(static, dict):
        fail("Procedural and static HLOD observations required")
    if terrain.get("document_hash") != pins["terrain"] or actual.get("identity") != terrain or terrain.get("dimensions") != [8, 8]:
        fail("Procedural source identity or dimensions differ")
    hlod = actual.get("hlod", {})
    cells = actual.get("resident_cells")
    if (type(cells) is not int or cells <= 0 or actual.get("detailed_cells", 0) <= 0
            or actual.get("detailed_cells", 0) + actual.get("coarse_cells", 0) != cells
            or cells > terrain.get("maximum_cells", 0) or actual.get("cpu_mesh_bytes", 0) <= 0
            or actual.get("resident_triangles", 0) <= 0 or actual.get("committed_transitions", 0) <= 0
            or actual.get("pending") is not False or actual.get("physics_ready") is not True
            or actual.get("source_synchronized") is not True or actual.get("error") is not None
            or hlod.get("tiles", 0) <= 0 or hlod.get("covered_base_cells", 0) <= 0
            or hlod.get("resident_triangles", 0) <= 0 or hlod.get("cpu_mesh_bytes", 0) <= 0
            or cells + hlod.get("covered_base_cells", 0) != 64
            or actual["cpu_mesh_bytes"] + hlod["cpu_mesh_bytes"] > terrain.get("maximum_cpu_mesh_bytes", 0)
            or hlod.get("peak_staging_mesh_bytes", 0) > terrain.get("maximum_staging_cpu_mesh_bytes", 0)):
        fail("Terrain residency, coverage, budget or physics not ready")
    if static.get("enabled") is not (mode == "hierarchy") or static.get("pending") is not False or static.get("error") is not None:
        fail("Static HLOD control did not reach the requested mode")
    replacement = (static.get("rendered_batches", 0) > 0 and static.get("replaced_primitives", 0) > 0
                   and static.get("gpu_upload_ready") is True)
    if require_static and mode == "hierarchy" and not replacement:
        fail("No GPU-admitted static HLOD replacement observed")
    if mode == "detail" and (static.get("rendered_batches") != 0 or static.get("replaced_primitives") != 0):
        fail("Detail control unexpectedly replaced static primitives")
    return {"mode": mode, **route_metrics(rows, minimum_distance, return_tolerance),
            "terrain": actual, "static_hlod": static,
            "static_hlod_replacement_exercised": replacement}


def route_positions(rows):
    if not rows:
        fail("Frame interval trace is empty")
    positions = []
    for row in rows:
        if not row["projection_ready"] or row.get("simulation_ready") is False:
            continue
        value = row.get("player_position")
        if (not isinstance(value, list) or len(value) != 3
                or any(type(number) not in (int, float) or not math.isfinite(number) for number in value)):
            fail("Invalid player trajectory")
        positions.append((value[0], value[2]))
    if not positions:
        fail("No ready frames in the complete trace")
    return positions


def route_metrics(rows, minimum_distance, return_tolerance):
    positions = route_positions(rows)
    start = positions[0]
    farthest = max(math.dist(start, point) for point in positions)
    returned = math.dist(start, positions[-1])
    if farthest < minimum_distance or returned > return_tolerance:
        fail("Observed route did not travel far enough and return")
    ready = [row for row in rows if row["projection_ready"]]
    return {"recorded_frames": distribution([row["frame_ms"] for row in rows]),
            "recorded_ready_frames": distribution([row["frame_ms"] for row in ready]),
            "recorded_not_ready_frames": distribution([row["frame_ms"] for row in rows if not row["projection_ready"]]),
            "start_xz": start, "farthest_distance_m": farthest, "return_error_m": returned}


def analyze_complete(report, rows, pins, minimum_distance, return_tolerance):
    expected = report.get("expected", {})
    actual = report.get("final_observation", {})
    if (report.get("schema") != "forge.module-rpg-runtime-report" or report.get("version") not in (1, 2)
            or type(report.get("version")) is not int
            or report.get("runtime_exit_success") is not True or report.get("projection_ready") is not True
            or not isinstance(expected, dict) or not isinstance(actual, dict)
            or expected.get("source_hash") != pins["head"] or expected.get("area_id") != pins["area"]
            or expected.get("module_id") != pins["module_id"]
            or expected.get("streamed_terrain") is not None
            or expected.get("procedural_streaming") is not None
            or actual.get("streamed_terrain") is not None
            or actual.get("procedural_streaming") is not None
            or actual.get("static_hlod") is not None):
        fail("Complete-terrain report or module identity differs")
    return {"mode": "complete", **route_metrics(rows, minimum_distance, return_tolerance),
            "terrain": None, "static_hlod": None, "static_hlod_replacement_exercised": None}


def resample_route(rows):
    points = route_positions(rows)
    distances = [0.0]
    unique = [points[0]]
    for point in points[1:]:
        distance = math.dist(unique[-1], point)
        if distance > 1.0e-6:
            distances.append(distances[-1] + distance)
            unique.append(point)
    if len(unique) < 2 or not math.isfinite(distances[-1]):
        fail("Trajectory has no measurable movement")
    sampled = []
    for step in range(101):
        target = distances[-1] * step / 100.0
        index = bisect.bisect_left(distances, target)
        if index == 0:
            sampled.append(unique[0])
        else:
            index = min(index, len(distances) - 1)
            span = distances[index] - distances[index - 1]
            ratio = (target - distances[index - 1]) / span
            sampled.append(tuple(a + (b - a) * ratio
                                 for a, b in zip(unique[index - 1], unique[index])))
    return sampled


def route_deviation(first, second, maximum):
    if type(maximum) not in (int, float) or not math.isfinite(maximum) or not 0 < maximum <= 1000:
        fail("An explicit positive route deviation limit is required")
    deviation = max(math.dist(a, b) for a, b in zip(resample_route(first), resample_route(second)))
    if deviation > maximum:
        fail("Observed trajectories exceed the requested deviation limit")
    return deviation


def check_terrain_comparison(args, plan):
    mode = getattr(args, "comparison", None)
    deadline = getattr(args, "max_run_seconds", None)
    deviation = getattr(args, "max_route_deviation_m", None)
    if mode is None:
        if deadline is not None or deviation is not None:
            fail("Terrain comparison mode is required with its limits")
        return False
    if (mode != "terrain" or type(deadline) is not int or not plan["max_duration_s"] + 30 <= deadline <= 630
            or type(deviation) not in (int, float) or not math.isfinite(deviation)
            or not 0 < deviation <= 1000 or args.require_static_replacement):
        fail("Terrain comparison requires an explicit bounded deadline and route limit")
    return True



def wait_bounded(child, timeout, mode):
    try:
        return child.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        try:
            child.terminate()
        except OSError as error:
            fail(f"{mode} process {child.pid} exceeded its deadline; terminate failed: {error}")
        try:
            child.wait(timeout=10)
        except subprocess.TimeoutExpired:
            try:
                child.kill()
            except OSError as error:
                fail(f"{mode} process {child.pid} exceeded its deadline; kill failed: {error}")
            try:
                child.wait(timeout=10)
            except subprocess.TimeoutExpired:
                fail(f"{mode} process {child.pid} remained alive after kill")
        fail(f"Owned {mode} process exceeded its scenario deadline")


def read_timings(path, report, pins, pid, required_schema="forge.native-main-frame-intervals.v2"):
    if required_schema not in ("forge.native-main-frame-intervals.v2", "forge.native-main-frame-intervals.v3"):
        fail("Invalid required frame trace schema")
    physical(path, exists=True)
    summary = report.get("frame_timings", {})
    if (not isinstance(summary, dict) or summary.get("enabled") is not True or summary.get("artifact") != path.name
            or summary.get("truncated") is not False or summary.get("sha256") != digest(path)
            or path.stat().st_size > frame_intervals.MAX_BYTES):
        fail("Frame trace summary or hash differs")
    document = read_json(path)
    if (not isinstance(document, dict) or document.get("schema") != required_schema
            or document.get("pid") != pid or document.get("source_hash") != pins["head"]):
        fail("Frame trace identity differs")
    rows = frame_intervals.decode(document)
    if (summary.get("sha256") != digest(path) or summary.get("recorded") != len(rows)
            or any(a["frame"] >= b["frame"] for a, b in zip(rows, rows[1:]))):
        fail("Frame trace count or frame ordering differs")
    return rows


def check_events(path, plan, pins, scenario_hash, functional_hash, pid):
    completed = []
    physical(path, exists=True)
    with path.open(encoding="utf-8") as stream:
        for line in stream:
            event = json.loads(line)
            if not isinstance(event, dict):
                fail("Invalid scenario observation")
            if event.get("kind") != "scenario":
                continue
            if (event.get("schema") != "forge.native-gameplay.observation" or event.get("version") != 1
                    or event.get("qualification") != "observations_only"
                    or event.get("pid") != pid or event.get("source_hash") != pins["head"]
                    or event.get("archive_hash") != pins["archive"] or event.get("plan_hash") != scenario_hash
                    or event.get("functional_hash") != functional_hash):
                fail("Scenario observation identity differs")
            if event.get("status") == "failed":
                fail("Scenario reported a failed step")
            if event.get("status") == "completed":
                index = event.get("step")
                if type(index) is not int or not 0 <= index < len(plan["steps"]):
                    fail("Invalid scenario step index")
                detail = event.get("detail")
                planned = plan["steps"][index]
                if (not isinstance(detail, dict) or detail.get("kind") != planned.get("kind")
                        or (planned.get("kind") == "capture" and detail.get("name") != planned.get("name"))):
                    fail("Scenario step differs from pinned plan")
                completed.append(index)
    if completed != list(range(len(plan["steps"]))):
        fail("Scenario did not complete each step exactly once in order")


def check_game_report(report, pins, functional_hash, pid):
    if (report.get("schema") != "forge.native-gameplay.report" or report.get("version") != 1
            or report.get("pid") != pid or report.get("source_hash") != pins["head"]
            or report.get("archive_hash") != pins["archive"] or report.get("exit_success") is not True
            or report.get("functional_hash") != functional_hash
            or report.get("ready") is not True or report.get("last_command_error") is not None
            or report.get("simulation_error") is not None):
        fail("Native gameplay report failed or has a different identity")


def capture_info(path):
    physical(path, exists=True)
    size = path.stat().st_size
    if not 45 <= size <= 64 * 1024 * 1024:
        fail("Scenario capture exceeds PNG size bounds")
    initial_hash = digest(path)
    with path.open("rb") as stream:
        if stream.read(8) != PNG:
            fail("Scenario capture is not a PNG")
        dimensions = None
        image_data = False
        while True:
            header = stream.read(8)
            if len(header) != 8:
                fail("Scenario capture has an incomplete PNG chunk")
            length, kind = struct.unpack(">I4s", header)
            if length > size or (dimensions is None and (kind != b"IHDR" or length != 13)):
                fail("Scenario capture has an invalid PNG header")
            if dimensions is not None and kind == b"IHDR":
                fail("Scenario capture has duplicate PNG headers")
            checksum = zlib.crc32(kind)
            first = b""
            remaining = length
            while remaining:
                block = stream.read(min(remaining, 1024 * 1024))
                if not block:
                    fail("Scenario capture has truncated PNG data")
                if dimensions is None:
                    first += block
                checksum = zlib.crc32(block, checksum)
                remaining -= len(block)
            recorded = stream.read(4)
            if len(recorded) != 4 or struct.unpack(">I", recorded)[0] != checksum:
                fail("Scenario capture PNG checksum differs")
            if dimensions is None:
                dimensions = struct.unpack(">II", first[:8])
                if not all(dimensions):
                    fail("Scenario capture has empty dimensions")
            if kind == b"IDAT":
                image_data = True
            if kind == b"IEND":
                if length or not image_data or stream.read(1):
                    fail("Scenario capture has an invalid PNG ending")
                break
    if digest(path) != initial_hash or path.stat().st_size != size:
        fail("Scenario capture changed while being checked")
    return {"bytes": size, "sha256": initial_hash, "width": dimensions[0], "height": dimensions[1],
            "pixel_decode_verified": False, "human_reviewed": False}


def arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("binary", "package", "scenario", "build-receipt", "source-manifest"):
        parser.add_argument("--" + name, type=Path, required=True)
        parser.add_argument("--" + name + "-sha256", required=True)
    for name in ("module-id", "area", "head", "archive", "transport", "terrain"):
        parser.add_argument("--" + name, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--work-root", type=Path, required=True)
    parser.add_argument("--quality", choices=("low", "medium", "high", "ultra"), required=True)
    parser.add_argument("--backend", choices=("vulkan", "dx12"), required=True)
    parser.add_argument("--minimum-distance-m", type=float, required=True)
    parser.add_argument("--return-tolerance-m", type=float, required=True)
    parser.add_argument("--require-static-replacement", action="store_true")
    parser.add_argument("--comparison", choices=("terrain",),
                        help="Explicitly compare the complete and qualification-streamed terrain hosts")
    parser.add_argument("--max-run-seconds", type=int,
                        help="Explicit per-process deadline for the terrain comparison")
    parser.add_argument("--max-route-deviation-m", type=float,
                        help="Explicit maximum spatial deviation for the terrain comparison")
    parser.add_argument("--describe", action="store_true", help="Validate pins and print the plan without launching")
    return parser.parse_args()


def run(args):
    for name in ("binary", "package", "scenario", "build_receipt", "source_manifest"):
        pinned(getattr(args, name), getattr(args, name + "_sha256"))
    for name in ("head", "archive", "transport", "terrain"):
        if not HASH.fullmatch(getattr(args, name)):
            fail(f"Invalid {name} SHA-256")
    if not args.module_id or not args.area or not all(math.isfinite(v) and v > 0 for v in (args.minimum_distance_m, args.return_tolerance_m)):
        fail("Module identity and positive route limits are required")
    physical(args.output, exists=False)
    physical(args.work_root, exists=False)
    if args.output == args.work_root or args.output in args.work_root.parents or args.work_root in args.output.parents:
        fail("Evidence and work directories must be distinct")
    plan = read_json(args.scenario)
    names = check_plan(plan, args.head, args.archive)
    terrain_ab = check_terrain_comparison(args, plan)
    modes = ("complete", "streamed") if terrain_ab else ("hierarchy", "detail")
    check_build_receipt(read_json(args.build_receipt))
    pins = {name: getattr(args, name) for name in ("module_id", "area", "head", "archive", "transport", "terrain")}
    inputs = {name: {"path": str(getattr(args, name)), "sha256": getattr(args, name + "_sha256")}
              for name in ("binary", "package", "scenario", "build_receipt", "source_manifest")}
    description = {"schema": "forge.gate7.procedural-module-plan.v1", "pins": pins, "inputs": inputs,
                   "modes": list(modes), "quality": args.quality, "backend": args.backend,
                   "minimum_distance_m": args.minimum_distance_m, "return_tolerance_m": args.return_tolerance_m,
                   "capture_files": names, "static_hlod_replacement_required": args.require_static_replacement,
                   "output": str(args.output), "work_root": str(args.work_root),
                   "build_source_link_verified": False, "tidewater_comparison": False,
                   "note": "Build receipt and source manifest are pinned separately; their linkage to the executable is not established by this runner."}
    if terrain_ab:
        description.update(comparison="terrain", max_run_seconds=args.max_run_seconds,
                           max_route_deviation_m=args.max_route_deviation_m)
    if args.describe:
        print(json.dumps(description, indent=2))
        return
    args.output.mkdir()
    args.created_output = True
    args.work_root.mkdir()
    write_new(args.output / "preflight.json", description)
    results = []
    traces = []
    functional_hashes = []
    for mode in modes:
        result_dir = args.output / mode
        work = args.work_root / mode
        result_dir.mkdir()
        work.mkdir()
        environment = {key: value for key, value in os.environ.items()
                       if not key.upper().startswith(("FORGE_", "OPENRPG_", "WGPU_", "BEVY_", "WINIT_"))}
        overrides = dict(FORGE_GAME_STATIC_HLOD="detail" if terrain_ab else mode,
                           FORGE_GAME_FRAME_TIMES="1", FORGE_GAME_REPORT_PATH=str(result_dir / "report.json"),
                           FORGE_GAME_OBSERVATIONS=str(result_dir / "observations.jsonl"),
                           FORGE_NATIVE_GAMEPLAY_SCENARIO_PATH=str(args.scenario),
                           FORGE_NATIVE_GAMEPLAY_SCENARIO_SHA256=args.scenario_sha256,
                           FORGE_NATIVE_GAMEPLAY_OUTPUT_ROOT=str(result_dir), WGPU_BACKEND=args.backend, RUST_LOG="info")
        if not terrain_ab or mode == "streamed":
            overrides["FORGE_QUALIFY_PROCEDURAL_STREAMING"] = "1"
        else:
            overrides["FORGE_QUALIFY_PROCEDURAL_STREAMING"] = "0"
        if terrain_ab:
            temporary = work / "temp"
            temporary.mkdir()
            overrides.update(TEMP=str(temporary), TMP=str(temporary))
            environment = {key: value for key, value in environment.items()
                           if not key.upper().startswith("WEBVIEW2_")}
        environment.update(overrides)
        command = [str(args.binary), "--module", str(args.package), "--zone", args.area,
                   "--cache", str(args.work_root / "cache"), "--save-root", str(work / "saves"),
                   "--expected-head", args.head, "--expected-archive-sha256", args.archive,
                   "--expected-package-manifest-sha256", args.transport, "--start-intent", "fresh",
                   "--default-quality", args.quality]
        started = datetime.now(timezone.utc).isoformat()
        with (result_dir / "process.log").open("xb") as log:
            child = subprocess.Popen(command, cwd=work, env=environment, stdout=log, stderr=subprocess.STDOUT)
            code = wait_bounded(child, args.max_run_seconds if terrain_ab else plan["max_duration_s"] + 30, mode)
        if code != 0:
            fail(f"{mode} runtime failed: exit {code}")
        report_path = result_dir / "module-runtime-report.json"
        timing_path = result_dir / "frame-timings.json"
        report = read_json(report_path)
        if report.get("pid") != child.pid:
            fail("Runtime identity differs")
        functional_hash = report.get("expected", {}).get("functional_hash")
        if not isinstance(functional_hash, str) or not HASH.fullmatch(functional_hash):
            fail("Runtime functional identity missing")
        game_report_path = result_dir / "report.json"
        physical(game_report_path, exists=True)
        check_game_report(read_json(game_report_path), pins, functional_hash, child.pid)
        observations = result_dir / "observations.jsonl"
        check_events(observations, plan, pins, args.scenario_sha256, functional_hash, child.pid)
        rows = read_timings(
            timing_path, report, pins, child.pid,
            "forge.native-main-frame-intervals.v3" if terrain_ab and mode == "complete"
            else "forge.native-main-frame-intervals.v2")
        if terrain_ab and any(type(row.get("simulation_ready")) is not bool for row in rows):
            fail("The terrain comparison requires dated simulation readiness for every frame")
        if terrain_ab and mode == "complete":
            if any(any(name in row for name in ("selected_triangles", "rendered_batches", "generation", "pending"))
                   for row in rows):
                fail("Complete-terrain trace unexpectedly contains static HLOD metrics")
            analysis = analyze_complete(report, rows, pins, args.minimum_distance_m, args.return_tolerance_m)
        else:
            analysis = analyze(report, rows, "detail" if terrain_ab else mode, pins,
                               args.minimum_distance_m, args.return_tolerance_m, args.require_static_replacement)
            if terrain_ab:
                analysis["mode"] = mode
        if terrain_ab:
            graphics = report.get("final_observation", {}).get("graphics", {})
            identity = graphics.get("identity", {}) if isinstance(graphics, dict) else {}
            if (not isinstance(graphics, dict) or graphics.get("requested") != args.quality
                    or graphics.get("applied") != args.quality or graphics.get("ready") is not True
                    or graphics.get("error") is not None or graphics.get("preference_error") is not None
                    or not isinstance(identity, dict) or identity.get("head") != pins["head"]
                    or not isinstance(identity.get("policy"), str) or not HASH.fullmatch(identity["policy"])):
                fail("Applied graphics quality or its pinned identity differs")
            analysis["graphics_policy"] = identity["policy"]
            analysis["recorded_simulation_not_ready_frames"] = distribution(
                [row["frame_ms"] for row in rows if not row["simulation_ready"]])
            traces.append(rows)
            functional_hashes.append(functional_hash)
        analysis.update(pid=child.pid, started_utc=started, finished_utc=datetime.now(timezone.utc).isoformat(),
                        command=command, environment_overrides=overrides,
                        process_log_sha256=digest(result_dir / "process.log"),
                        report_sha256=digest(report_path), game_report_sha256=digest(game_report_path),
                        frame_timings_sha256=digest(timing_path),
                        observations_sha256=digest(observations),
                        captures={name: capture_info(result_dir / name) for name in names})
        results.append(analysis)
    for name in inputs:
        pinned(getattr(args, name), inputs[name]["sha256"])
    if terrain_ab:
        if len(set(functional_hashes)) != 1:
            fail("The terrain runs do not share the same functional identity")
        deviation = route_deviation(traces[0], traces[1], args.max_route_deviation_m)
    elif any(results[0]["static_hlod"].get(key) != results[1]["static_hlod"].get(key)
             for key in ("source_objects", "source_primitives", "source_triangles")):
        fail("The two HLOD modes did not observe the same static source")
    if math.dist(results[0]["start_xz"], results[1]["start_xz"]) > args.return_tolerance_m:
        fail("The two runs did not start from the same position")
    sizes = {(capture["width"], capture["height"]) for result in results for capture in result["captures"].values()}
    if len(sizes) != 1:
        fail("Scenario captures do not share the same resolution")
    assessment = {
        "schema": "forge.gate7.procedural-module-assessment.v1", "procedural_probe_passed": True,
        "static_hlod_replacement_required": args.require_static_replacement,
        "human_visual_acceptance": False, "tidewater_comparison": False, "performance_qualified": False,
        "gate_closed": False, "build_source_link_verified": False,
        "residency_transition_causality_verified": False,
        "scope": "qualification-only 8x8 terrain; recorded main-frame intervals may omit frames before observations exist and include instrumentation and scheduling; no GPU timestamps",
        "runs": results,
    }
    if terrain_ab:
        assessment.update(
            schema="forge.gate7.procedural-terrain-comparison.v1",
            comparison="terrain", route_max_deviation_m=deviation,
            route_deviation_limit_m=args.max_route_deviation_m,
            scope=("Exploratory sequential comparison: complete terrain first, then qualification-streamed "
                   "terrain, using the same pinned module, binary, scenario, requested quality and backend. "
                   "Both derive common shadow, post-processing, TAA and MSAA settings from the same code "
                   "and terrain; their applied values are not independently cross-measured. Terrain LOD "
                   "and the full graphics-policy hashes intentionally differ. "
                   "The streamed host also changes physics and static admission and installs the static "
                   "HLOD plugin and its monitoring even in detail mode; the difference is not solely "
                   "the cost of terrain streaming. "
                   "The second run reuses the verified module cache and may benefit from warmer OS and "
                   "GPU driver caches; order is fixed, with no cold-cache or thermal controls. "
                   "Interactive keyboard and mouse sampling remains active and user input can alter the run. "
                   "The trajectories are compared only in horizontal X/Z coordinates at 101 normalized "
                   "path positions, not exact timing, height or camera orientation. "
                   "Projection readiness has host-specific criteria; simulation_ready records the observed "
                   "PlayerSimulationReady flag, not the actual duration of physics or gameplay pauses. "
                   "Recorded main-frame intervals can omit startup and include instrumentation and scheduling; "
                   "they are not GPU timestamps or present latency. "
                   "No independent adapter identity proof, Tidewater parity or release verdict."))
    write_new(args.output / "assessment.json", assessment)


if __name__ == "__main__":
    args = arguments()
    try:
        run(args)
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as error:
        if getattr(args, "created_output", False):
            try:
                write_new(args.output / "failure.json", {"schema": "forge.gate7.procedural-module-failure.v1",
                          "passed": False, "error": str(error), "partial_outputs_retained": True})
            except OSError as receipt_error:
                print(f"Could not write failure receipt: {receipt_error}", file=sys.stderr)
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1) from error

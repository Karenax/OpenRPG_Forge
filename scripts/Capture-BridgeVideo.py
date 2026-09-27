"""Offline 30 fps bridge traversal evidence. Not realtime FPS or human acceptance."""
import argparse
import importlib.util
import json
import math
import os
from pathlib import Path
import struct
import subprocess

spec = importlib.util.spec_from_file_location(
    "capture", Path(__file__).with_name("Capture-WorldSketch.py"))
capture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(capture)


def water_arguments(medium):
    if medium is None:
        return []
    return ["--water-depth-preview", "--water-medium-input", str(medium),
            "--water-reflection-preview"]


def cloud_arguments(profile):
    return [] if profile is None else ["--cloud-layer-preview", str(profile)]


def forest_arguments(catalog, mode):
    if mode not in ("individual", "batches"):
        raise ValueError("unknown forest mode")
    if catalog is None:
        if mode == "batches":
            raise ValueError("batched forest requires --forest-catalog")
        return []
    return ["--prepare-vegetation-catalog", str(catalog),
            "--prototype-forest-batches" if mode == "batches" else "--prototype-forest-preview"]


def verify_render_projection(events, terrain_hash, quality=None, forest=False, batches=False):
    """CPU projection/presence evidence only, not full quality or GPU LOD execution."""
    result = {}
    if batches and not forest:
        raise ValueError("batched projection requires governed forest")
    if quality is not None:
        terrain = [event for event in events if event.get("event_kind") == "terrain_lod_projected"]
        if not terrain or any(
            event.get("terrain_hash") != terrain_hash
            or event.get("tier", "").lower() != quality
            or any(event.get(key, 0) <= 0 for key in
                   ("canonical_chunks", "derived_meshes", "derived_vertices", "derived_triangles"))
            for event in terrain
        ):
            raise ValueError("requested terrain CPU projection missing or inconsistent")
        result["terrain_cpu_projection"] = terrain[-1]
    if forest:
        projections = [event for event in events
                       if event.get("event_kind") == "governed_forest_preview_projected"]
        if not projections or any(
            event.get("trees", 0) <= 0 or event.get("primitive_entities", 0) <= 0
            or event.get("procedural_triangle_metrics_valid") is not False
            for event in projections
        ):
            raise ValueError("governed forest presence missing or inconsistent")
        result["governed_forest_presence"] = projections[-1]
    if batches:
        projected = [event for event in events
                     if event.get("event_kind") == "governed_far_batches_projected"]
        if not projected or any(event.get("batches", 0) <= 0 for event in projected):
            raise ValueError("requested far batches missing or empty")
        result["governed_far_batch_presence"] = projected[-1]
    return result


def verify_frames(output, preview, *, quality=None, forest=False, batches=False):
    events = [json.loads(line) for line in (output / "runtime.jsonl").read_text(
        encoding="utf-8").splitlines() if line.startswith("{")]
    if any(event.get("level") == "ERROR" for event in events):
        raise ValueError("runtime emitted ERROR")
    projection = verify_render_projection(events, preview["source_terrain_sha256"], quality, forest, batches)
    def one(kind):
        matches = [event for event in events if event.get("event_kind") == kind]
        if len(matches) != 1:
            raise ValueError(f"expected one {kind}, got {len(matches)}")
        return matches[0]
    stop = one("runtime_stopped")
    crossing = one("bridge_crossing_probe_completed")
    recording = one("bridge_recording_completed")
    readiness = one("governed_asset_kit_playtest_ready")
    render_event = one("bridge_recording_render_ready")
    render = json.loads(render_event["readiness"])
    pipeline = render["pipeline"]
    if (not render.get("scene_ready") or pipeline.get("total", 0) <= 0
            or pipeline.get("ready") != pipeline.get("total")
            or pipeline.get("pending") != 0 or pipeline.get("failed") != 0
            or render.get("stable_observations", 0) < max(8, render.get("required_stable_observations", 0))):
        raise ValueError("render startup readiness incomplete")
    if events.index(render_event) >= events.index(recording):
        raise ValueError("render readiness must precede recording completion")
    if not stop.get("success") or stop.get("runtime_errors_observed") or not stop.get("automated_run"):
        raise ValueError("clean automated shutdown missing")
    if not crossing.get("success") or crossing["terrain_document_hash"] != preview["source_terrain_sha256"]:
        raise ValueError("crossing or terrain pin failed")
    if not all(crossing.get(key) for key in (
        "terrain_bindings_preserved", "player_bindings_preserved", "bridge_binding_preserved",
        "final_grounded", "crossed_audit_center")) or crossing["deck_collision_ticks"] <= 0:
        raise ValueError("physical crossing incomplete")
    if readiness["ready_instances"] != len(preview["placements"]) or readiness["registered_colliders"] <= 0:
        raise ValueError("kit readiness incomplete")
    if (not recording.get("success") or recording.get("frames_per_second") != 30
            or recording.get("mode") != "offline_manual_time"
            or recording.get("performance_qualified") is not False):
        raise ValueError("offline frame clock missing")
    count = recording["frames"]
    if not 2 <= count <= 600:
        raise ValueError("invalid frame count")
    frames = sorted((output / "frames").glob("*.png"))
    if [path.name for path in frames] != [f"frame-{i:04}.png" for i in range(count)]:
        raise ValueError("missing, extra or nonconsecutive frames")
    pins = {}
    for path in frames:
        with path.open("rb") as stream:
            header = stream.read(24)
        if header[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", header[16:24]) != (1920, 1080):
            raise ValueError(f"not a Full HD PNG: {path.name}")
        pins[path.name] = capture.digest(path)
    if len(set(pins.values())) < 2:
        raise ValueError("all frames are identical")
    return {"crossing": crossing, "recording": recording, "kit_readiness": readiness,
            "render_startup_readiness": render_event,
            "render_projection_observations": projection,
            "frame_sha256": pins, "runtime_log_sha256": capture.digest(output / "runtime.jsonl")}


def verify_weather_pair(clear, rain):
    """Check acquisition conditions after each payload has passed verify_frames."""
    required_inputs = {'runtime', 'world_sketch', 'kit_build', 'preview', 'ffmpeg',
                       'forest_catalog', 'water_medium', 'cloud_profile'}
    for report, weather in [(clear, 'clear'), (rain, 'rain')]:
        if (report['status'] != 'video_checks_passed_review_pending'
                or report['weather'] != weather or report['requested_quality'] != 'high'
                or report['forest_mode'] != 'batches'
                or not report['forest_preview_requested']
                or not report['forward_water_candidate_requested']
                or not report['cloud_layer_requested']
                or set(report['inputs']) != required_inputs
                or report['recording']['mode'] != 'offline_manual_time'
                or report['recording']['frames_per_second'] != 30):
            raise ValueError('incomplete or mismatched weather-pair protocol')
    left = {name: pin['sha256'] for name, pin in clear['inputs'].items()}
    right = {name: pin['sha256'] for name, pin in rain['inputs'].items()}
    if left != right or clear['recording']['frames'] != rain['recording']['frames']:
        raise ValueError('weather pair has different inputs or frame counts')
    if clear.get('temporal_lod_probe_requested', False) != rain.get('temporal_lod_probe_requested', False):
        raise ValueError('weather pair uses different motion itineraries')
    return {'status': 'matched_high_weather_acquisition_conditions_verified',
            'frames_per_video': clear['recording']['frames'],
            'same_input_hashes': left, 'mode': 'offline_manual_time',
            'performance_qualified': False, 'temporal_visual_review_verified': False,
            'human_accepted': False, 'gate1_pass': False}


def verify_temporal_trace(events):
    """Validate bounded CPU transport evidence; PNG review remains separate."""
    ends = [event for event in events if event.get('event_kind') == 'temporal_lod_probe_completed']
    if len(ends) != 1 or any(event.get('event_kind') == 'temporal_lod_probe_failed' for event in events):
        raise ValueError('temporal trace absent, repeated or failed')
    status = json.loads(ends[0]['status'])
    slots, samples = status['selected_slots'], status['samples']
    if (status['phase'] != 'completed' or status['failure'] is not None
            or status['cpu_settled_while_stopped'] is not True
            or not 1 <= len(slots) <= 8 or len(set(slots)) != len(slots)
            or not 4 <= len(samples) <= 64):
        raise ValueError('incomplete bounded temporal status')
    times = [sample['elapsed_seconds'] for sample in samples]
    if (any(not math.isfinite(time) for time in times) or times[0] != 0
            or times[-1] < .6 or any(b <= a for a, b in zip(times, times[1:]))):
        raise ValueError('invalid temporal sample clock')
    observed = [json.loads(event['sample']) for event in events
                if event.get('event_kind') == 'temporal_lod_probe_sample']
    if observed != samples:
        raise ValueError('terminal temporal report does not match emitted samples')
    for sample in samples:
        if ([group['slot'] for group in sample['groups']] != slots
                or len(sample['camera_position']) != 3
                or any(not math.isfinite(value) for value in sample['camera_position'])
                or any(not group['active'] or len(group['codes']) != 4
                       or any(not isinstance(code, int) or abs(code) > 16 for code in group['codes'])
                       for group in sample['groups'])):
            raise ValueError('invalid temporal group or camera sample')
    if not any(group['transition_active'] for group in samples[0]['groups']):
        raise ValueError('no initial transition observed')
    def settled(sample):
        return all(not group['transition_active'] and all(code in (-16, 0, 16) for code in group['codes'])
                   for group in sample['groups'])
    stable = [sample for sample in samples if sample['elapsed_seconds'] >= .3]
    if (len(stable) < 3 or not all(settled(sample) for sample in stable)
            or any(sample['groups'] != stable[0]['groups']
                   or math.dist(sample['camera_position'], stable[0]['camera_position']) > .002
                   for sample in stable)):
        raise ValueError('temporal coverage or camera did not remain stable')
    first_settled = next((sample['elapsed_seconds'] for sample in samples if settled(sample)), None)
    return {'status': 'bounded_cpu_temporal_transport_verified',
            'first_settled_seconds': first_settled, 'trace': status,
            'gpu_pixel_coverage_verified': False, 'visual_review_verified': False,
            'performance_qualified': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("runtime", "world-sketch", "kit-build", "preview", "ffmpeg", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--weather", choices=("clear", "rain"), default="clear")
    parser.add_argument("--quality", choices=("low", "medium", "high", "ultra"))
    parser.add_argument("--temporal-lod-probe", action="store_true",
                        help="Pause 600 ms during a temporal fade; distinct from the original itinerary")
    parser.add_argument("--forest-catalog", type=Path)
    parser.add_argument("--forest-mode", choices=("individual", "batches"), default="individual")
    parser.add_argument("--water-medium", type=Path,
                        help="Pin an explicit forward water candidate, including reflection/refraction traces")
    parser.add_argument("--cloud-profile", type=Path,
                        help="Include and pin the same authored cloud layer as the integrated candidate")
    args = parser.parse_args()
    if args.forest_mode == "batches" and args.forest_catalog is None:
        parser.error("--forest-mode batches requires --forest-catalog")
    sources = {name: getattr(args, name).resolve(strict=True) for name in
               ("runtime", "world_sketch", "kit_build", "preview", "ffmpeg")}
    if args.forest_catalog:
        sources["forest_catalog"] = args.forest_catalog.resolve(strict=True)
    if args.water_medium:
        sources["water_medium"] = args.water_medium.resolve(strict=True)
    if args.cloud_profile:
        sources["cloud_profile"] = args.cloud_profile.resolve(strict=True)
    pins = {name: capture.digest(path) for name, path in sources.items()}
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    report = {"schema": "forge.bridge-video-evidence", "schema_version": 1,
              "status": "incomplete", "human_accepted": False, "performance_qualified": False,
              "weather": args.weather,
              "requested_quality": args.quality,
              "temporal_lod_probe_requested": args.temporal_lod_probe,
              "forest_preview_requested": bool(args.forest_catalog),
              "forest_mode": args.forest_mode if args.forest_catalog else None,
              "forward_water_candidate_requested": bool(args.water_medium),
              "cloud_layer_requested": bool(args.cloud_profile),
              "quality_and_forest_projection_verified": False,
              "inputs": {name: {"path": str(path), "sha256": pins[name]}
                         for name, path in sources.items()}}
    try:
        command = [str(sources["runtime"]), "--world-sketch-visual-valley",
                   "--world-sketch-input", str(sources["world_sketch"]),
                   "--asset-kit-build", str(sources["kit_build"]),
                   "--asset-kit-preview", str(sources["preview"]),
                   "--bridge-crossing-probe", "--bridge-frames", str(output / "frames"),
                   "--weather", args.weather]
        if args.quality:
            command.extend(("--quality", args.quality))
        if args.temporal_lod_probe:
            command.append("--temporal-lod-probe")
        command.extend(forest_arguments(sources.get("forest_catalog"), args.forest_mode))
        command.extend(water_arguments(sources.get("water_medium")))
        command.extend(cloud_arguments(sources.get("cloud_profile")))
        report["runtime_command"] = command
        with (output / "runtime.jsonl").open("xb") as log:
            subprocess.run(command, stdout=log, stderr=log, check=True, timeout=210,
                           env=dict(os.environ, RUST_LOG="info,wgpu_core=warn,wgpu_hal=warn,naga=warn"))
        preview = json.loads(sources["preview"].read_text(encoding="utf-8"))
        report.update(verify_frames(output, preview, quality=args.quality,
                                    forest=bool(args.forest_catalog),
                                    batches=args.forest_mode == "batches"))
        if args.temporal_lod_probe:
            events = [json.loads(line) for line in (output / "runtime.jsonl").read_text(
                encoding="utf-8").splitlines() if line.startswith("{")]
            report['temporal_lod'] = verify_temporal_trace(events)
        command = [str(sources["ffmpeg"]), "-nostdin", "-n", "-v", "error", "-framerate", "30",
                   "-i", str(output / "frames" / "frame-%04d.png"), "-c:v", "libx264",
                   "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
                   str(output / "bridge.mp4")]
        report["encode_command"] = command
        subprocess.run(command, check=True, timeout=120)
        ffprobe = sources["ffmpeg"].with_name("ffprobe.exe")
        probe = subprocess.run([str(ffprobe), "-v", "error", "-count_frames",
                                "-select_streams", "v:0", "-show_entries",
                                "stream=width,height,r_frame_rate,nb_read_frames,duration",
                                "-of", "json", str(output / "bridge.mp4")],
                               capture_output=True, text=True, check=True, timeout=60)
        stream = json.loads(probe.stdout)["streams"][0]
        if ((stream["width"], stream["height"]) != (1920, 1080)
                or stream["r_frame_rate"] != "30/1"
                or int(stream["nb_read_frames"]) != report["recording"]["frames"]):
            raise ValueError("encoded video does not match source frames")
        report["video_stream"] = stream
        report["video_sha256"] = capture.digest(output / "bridge.mp4")
        if any(capture.digest(path) != pins[name] for name, path in sources.items()):
            raise ValueError("an input changed during recording")
        report["status"] = "video_checks_passed_review_pending"
    finally:
        capture.save(output / "manifest.json", report)
    print(json.dumps({"status": report["status"], "video": str(output / "bridge.mp4"),
                      "stream": report["video_stream"]}), flush=True)


if __name__ == "__main__":
    main()

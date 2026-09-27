"""Test frame evidence validation with small synthetic fixtures, not rendered-video qualification."""
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
import copy

spec = importlib.util.spec_from_file_location("video", Path(__file__).with_name("Capture-BridgeVideo.py"))
video = importlib.util.module_from_spec(spec)
spec.loader.exec_module(video)


class VideoEvidenceTests(unittest.TestCase):
    def test_temporal_trace_rejects_missing_samples_or_unsettled_coverage(self):
        samples = [{'elapsed_seconds': time, 'camera_position': [1., 2., 3.],
                    'groups': [{'slot': 1, 'active': True,
                                'codes': [8, -8, 16, 16] if time == 0 else [16, 0, 16, 16],
                                'transition_active': time == 0}]}
                   for time in [0, .2, .3, .4, .6]]
        status = {'phase': 'completed', 'failure': None, 'selected_slots': [1],
                  'samples': samples, 'cpu_settled_while_stopped': True}
        events = [{'event_kind': 'temporal_lod_probe_sample', 'sample': json.dumps(sample)}
                  for sample in samples]
        events.append({'event_kind': 'temporal_lod_probe_completed', 'status': json.dumps(status)})
        result = video.verify_temporal_trace(events)
        self.assertEqual(result['first_settled_seconds'], .2)
        self.assertFalse(result['gpu_pixel_coverage_verified'])
        with self.assertRaisesRegex(ValueError, 'match emitted'):
            video.verify_temporal_trace(events[1:])
        bad = copy.deepcopy(events)
        bad_status = json.loads(bad[-1]['status'])
        bad_status['samples'][-1]['camera_position'][0] += .1
        bad[-2]['sample'] = json.dumps(bad_status['samples'][-1])
        bad[-1]['status'] = json.dumps(bad_status)
        with self.assertRaisesRegex(ValueError, 'remain stable'):
            video.verify_temporal_trace(bad)

    def test_weather_pair_rejects_changed_conditions(self):
        clear = {'status': 'video_checks_passed_review_pending', 'weather': 'clear',
                 'requested_quality': 'high', 'forest_mode': 'batches',
                 'forest_preview_requested': True, 'forward_water_candidate_requested': True,
                 'cloud_layer_requested': True,
                 'inputs': {name: {'sha256': 'synthetic'} for name in
                     ['runtime', 'world_sketch', 'kit_build', 'preview', 'ffmpeg',
                      'forest_catalog', 'water_medium', 'cloud_profile']},
                 'recording': {'mode': 'offline_manual_time', 'frames_per_second': 30, 'frames': 240}}
        rain = copy.deepcopy(clear)
        rain['weather'] = 'rain'
        result = video.verify_weather_pair(clear, rain)
        self.assertEqual(result['frames_per_video'], 240)
        for field in ['performance_qualified', 'temporal_visual_review_verified', 'human_accepted', 'gate1_pass']:
            self.assertFalse(result[field])
        for change in [lambda r: r.update(requested_quality='ultra'),
                       lambda r: r.update(weather='clear'),
                       lambda r: r.update(status='incomplete'),
                       lambda r: r.update(cloud_layer_requested=False),
                       lambda r: r['inputs'].pop('water_medium'),
                       lambda r: r['inputs']['runtime'].update(sha256='other'),
                       lambda r: r['recording'].update(frames=241),
                       lambda r: r['recording'].update(mode='realtime')]:
            bad = copy.deepcopy(rain)
            change(bad)
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                video.verify_weather_pair(clear, bad)

    def test_cloud_profile_is_explicit_and_keeps_spaces_as_one_argument(self):
        self.assertEqual(video.cloud_arguments(None), [])
        profile = Path('E:/proof files/clouds.json')
        self.assertEqual(video.cloud_arguments(profile), ['--cloud-layer-preview', str(profile)])

    def test_forward_water_is_explicit_and_keeps_medium_as_one_argument(self):
        self.assertEqual(video.water_arguments(None), [])
        self.assertEqual(video.water_arguments(Path('E:/proof files/medium.json')),
                         ['--water-depth-preview', '--water-medium-input',
                          str(Path('E:/proof files/medium.json')), '--water-reflection-preview'])

    def test_batch_selection_requires_catalog_and_observed_nonempty_projection(self):
        self.assertEqual(video.forest_arguments(None, "individual"), [])
        self.assertEqual(video.forest_arguments("catalog.json", "batches"),
                         ["--prepare-vegetation-catalog", "catalog.json", "--prototype-forest-batches"])
        self.assertEqual(video.forest_arguments("catalog.json", "individual")[-1],
                         "--prototype-forest-preview")
        for catalog, mode in [(None, "batches"), ("catalog.json", "unknown")]:
            with self.assertRaises(ValueError):
                video.forest_arguments(catalog, mode)
        forest = {"event_kind": "governed_forest_preview_projected", "trees": 3,
                  "primitive_entities": 12, "procedural_triangle_metrics_valid": False}
        batch = {"event_kind": "governed_far_batches_projected", "batches": 2}
        actual = video.verify_render_projection([forest, batch], "pin", forest=True, batches=True)
        self.assertEqual(actual["governed_far_batch_presence"], batch)
        for events in ([forest], [forest, dict(batch, batches=0)],
                       [forest, dict(batch, event_kind="governed_far_batches_reused")],
                       [forest, batch, dict(batch, batches=0)]):
            with self.assertRaises(ValueError):
                video.verify_render_projection(events, "pin", forest=True, batches=True)
        with self.assertRaises(ValueError):
            video.verify_render_projection([batch], "pin", batches=True)

    def test_requested_projection_must_be_observed(self):
        terrain = {"event_kind": "terrain_lod_projected", "terrain_hash": "pin", "tier": "High",
                   "canonical_chunks": 2, "derived_meshes": 6, "derived_vertices": 60,
                   "derived_triangles": 80}
        forest = {"event_kind": "governed_forest_preview_projected", "trees": 3,
                  "primitive_entities": 12, "procedural_triangle_metrics_valid": False}
        self.assertEqual(video.verify_render_projection([], "pin"), {})
        valid = video.verify_render_projection([terrain, forest], "pin", "high", True)
        self.assertEqual(valid["governed_forest_presence"]["trees"], 3)
        invalid_cases = [[], [terrain], [forest],
                         [dict(terrain, tier="Low"), forest],
                         [dict(terrain, terrain_hash="stale"), forest],
                         [dict(terrain, derived_meshes=0), forest],
                         [terrain, dict(forest, trees=0)],
                         [terrain, dict(forest, procedural_triangle_metrics_valid=True)],
                         [terrain, forest, dict(terrain, tier="Low")]]
        for events in invalid_cases:
            with self.subTest(events=events), self.assertRaises(ValueError):
                video.verify_render_projection(events, "pin", "high", True)

    def test_valid_and_corrupt_frame_evidence(self):
        preview = {"source_terrain_sha256": "terrain-pin", "placements": [1, 2]}
        for corruption in (None, "error", "terrain", "bindings", "kit", "clock", "missing",
                           "resolution", "identical", "shutdown", "duplicate",
                           "render_missing", "render_pending", "render_empty", "render_unstable"):
            with self.subTest(corruption=corruption), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                (root / "frames").mkdir()
                crossing = {"event_kind": "bridge_crossing_probe_completed", "success": True,
                            "terrain_document_hash": "terrain-pin", "terrain_bindings_preserved": True,
                            "player_bindings_preserved": True, "bridge_binding_preserved": True,
                            "final_grounded": True, "crossed_audit_center": True, "deck_collision_ticks": 5}
                recording = {"event_kind": "bridge_recording_completed", "success": True,
                             "frames_per_second": 30, "mode": "offline_manual_time",
                             "performance_qualified": False, "frames": 2}
                ready = {"event_kind": "governed_asset_kit_playtest_ready", "ready_instances": 2,
                         "registered_colliders": 3}
                stop = {"event_kind": "runtime_stopped", "success": True,
                        "runtime_errors_observed": False, "automated_run": True}
                render = {"scene_ready": True, "stable_observations": 8,
                          "pipeline": {"total": 12, "ready": 12, "pending": 0, "failed": 0}}
                if corruption == "render_pending":
                    render["pipeline"]["pending"] = 1
                elif corruption == "render_empty":
                    render["pipeline"].update(total=0, ready=0)
                elif corruption == "render_unstable":
                    render["stable_observations"] = 7
                render_event = {"event_kind": "bridge_recording_render_ready",
                                "readiness": json.dumps(render)}
                events = [render_event, crossing, recording, ready, stop]
                if corruption == "render_missing":
                    events.remove(render_event)
                if corruption == "error":
                    events.append({"level": "ERROR"})
                elif corruption == "terrain":
                    crossing["terrain_document_hash"] = "wrong"
                elif corruption == "bindings":
                    crossing["bridge_binding_preserved"] = False
                elif corruption == "kit":
                    ready["ready_instances"] = 1
                elif corruption == "clock":
                    recording["frames_per_second"] = 60
                elif corruption == "shutdown":
                    stop["runtime_errors_observed"] = True
                elif corruption == "duplicate":
                    events.append(recording)
                (root / "runtime.jsonl").write_text("\n".join(map(json.dumps, events)), encoding="utf-8")
                for index in range(1 if corruption == "missing" else 2):
                    header = b"\x89PNG\r\n\x1a\n" + bytes(8) + struct.pack(
                        ">II", 100 if corruption == "resolution" else 1920, 1080)
                    (root / "frames" / f"frame-{index:04}.png").write_bytes(
                        header + bytes([0 if corruption == "identical" else index]))
                if corruption:
                    with self.assertRaises(ValueError):
                        video.verify_frames(root, preview)
                else:
                    result = video.verify_frames(root, preview)
                    self.assertEqual(len(result["frame_sha256"]), 2)


if __name__ == "__main__":
    unittest.main()

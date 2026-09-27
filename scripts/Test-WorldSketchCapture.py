"""Verify the capture auditor against real evidence and deliberately corrupted copies."""
import importlib.util
import copy
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("capture", Path(__file__).with_name("Capture-WorldSketch.py"))
capture = importlib.util.module_from_spec(spec)
spec.loader.exec_module(capture)
evidence_root = Path(sys.argv.pop(1)).resolve()


class CaptureTests(unittest.TestCase):
    def test_all_real_captures(self):
        manifest = json.loads((evidence_root / "manifest.json").read_text())
        source = manifest["inputs"]["world_sketch"]
        source_path = Path(source["path"])
        self.assertEqual(capture.digest(source_path), source["sha256"])
        world = json.loads(source_path.read_text(encoding="utf-8"))
        capture.verify_complete_coverage(manifest, world)
        for run in manifest["runs"]:
            root = evidence_root / run["id"]
            preview = json.loads((root / "preview.json").read_text())
            verified = capture.verify(root, preview, run["weather"], quality=manifest.get("quality"),
                                      forest=manifest.get("forest_preview", False),
                                      batches=manifest.get("forest_batches", False))
            self.assertEqual(verified["sha256"], run["sha256"])
            self.assertEqual(preview["camera_id"], run["camera"]["id"])

    def test_coverage_corruption_is_rejected(self):
        original = json.loads((evidence_root / "manifest.json").read_text())
        world = json.loads(Path(original["inputs"]["world_sketch"]["path"]).read_text())
        for corruption in ("missing", "duplicate", "renamed", "pose", "weather"):
            with self.subTest(corruption=corruption):
                manifest = copy.deepcopy(original)
                if corruption == "missing":
                    manifest["runs"].pop()
                elif corruption == "duplicate":
                    manifest["runs"][-1] = copy.deepcopy(manifest["runs"][0])
                elif corruption == "renamed":
                    manifest["runs"][0]["id"] = "unexpected-clear"
                elif corruption == "pose":
                    manifest["runs"][0]["camera"]["position_m"][0] += 1
                else:
                    manifest["runs"][0]["weather"] = "rain"
                with self.assertRaises(ValueError):
                    capture.verify_complete_coverage(manifest, world)

    def test_quality_and_forest_corruption_is_rejected(self):
        manifest = json.loads((evidence_root / "manifest.json").read_text())
        if manifest.get("quality") != "high" or not manifest.get("forest_preview"):
            self.skipTest("historical evidence does not request High governed forest")
        for corruption in ("tier", "budgets", "trees", "primitives", "geometry_counts"):
            with self.subTest(corruption=corruption), tempfile.TemporaryDirectory() as temp:
                root = Path(temp) / "copy"
                shutil.copytree(evidence_root / "hero_reference_match-clear", root)
                preview = json.loads((root / "preview.json").read_text())
                path = root / "metrics.json"
                metrics = json.loads(path.read_text())
                if corruption == "tier":
                    metrics["global_quality"]["requested_tier"] = "low"
                elif corruption == "budgets":
                    metrics["global_quality"]["available_budgets_projected"] = False
                elif corruption == "trees":
                    metrics["vegetation"]["prototype_preview"]["trees"] = 0
                elif corruption == "primitives":
                    metrics["vegetation"]["prototype_preview"]["primitive_entities"] = 0
                else:
                    metrics["vegetation"]["prototype_preview"]["procedural_geometry_counts_valid"] = True
                path.write_text(json.dumps(metrics))
                with self.assertRaises(ValueError):
                    capture.verify(root, preview, "clear", quality="high", forest=True)

    def test_corrupted_evidence_is_rejected(self):
        original = evidence_root / "hero_reference_match-clear"
        for corruption in ("error", "camera", "terrain", "weather", "colliders", "resolution", "shutdown"):
            with self.subTest(corruption=corruption), tempfile.TemporaryDirectory() as temp:
                root = Path(temp) / "copy"
                shutil.copytree(original, root)
                preview = json.loads((root / "preview.json").read_text())
                metrics_path = root / "metrics.json"
                metrics = json.loads(metrics_path.read_text())
                if corruption == "camera":
                    metrics["governed_asset_kit"]["camera_id"] = "wrong"
                elif corruption == "terrain":
                    metrics["source_hash"] = "wrong"
                elif corruption == "weather":
                    metrics["weather"]["target_state"] = "rain"
                elif corruption == "colliders":
                    metrics["governed_asset_kit"]["ready_rapier_colliders"] = 0
                elif corruption == "resolution":
                    (root / "capture.png").write_bytes(b"not a PNG")
                elif corruption == "error":
                    with (root / "runtime.jsonl").open("a") as log:
                        log.write('\n{"level":"ERROR"}\n')
                else:
                    (root / "runtime.jsonl").write_text('{}\n')
                metrics_path.write_text(json.dumps(metrics))
                with self.assertRaises(ValueError):
                    capture.verify(root, preview, "clear")


if __name__ == "__main__":
    unittest.main()

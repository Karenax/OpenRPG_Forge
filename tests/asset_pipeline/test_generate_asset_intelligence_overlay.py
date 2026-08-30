import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


MODULE_PATH = Path(__file__).resolve().parents[2] / "tools" / "asset_pipeline" / "generate_asset_intelligence_overlay.py"
SPEC = importlib.util.spec_from_file_location("generate_asset_intelligence_overlay", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class AssetIntelligenceOverlayTests(unittest.TestCase):
    def test_profile_converts_blender_z_up_bounds_and_hashes_dependency_closure(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            vault = root / "vault"
            model = vault / "normalized" / "fixture" / "door.gltf"
            model.parent.mkdir(parents=True)
            (model.parent / "door.bin").write_bytes(b"mesh")
            model.write_text(json.dumps({
                "asset": {"version": "2.0"},
                "buffers": [{"uri": "door.bin", "byteLength": 4}],
            }), encoding="utf-8")
            row = {
                "asset_id": "fixture:door",
                "name": "Door",
                "family": "Architecture/Doors",
                "assembly_role": "interactive-leaf",
                "bounds_min_x_m": "-0.1",
                "bounds_min_y_m": "-0.2",
                "bounds_min_z_m": "0",
                "bounds_max_x_m": "1.1",
                "bounds_max_y_m": "0.2",
                "bounds_max_z_m": "2.1",
                "pivot_class_heuristic": "edge-base",
                "mesh_object_count": "1",
                "vertices": "4",
                "triangles": "2",
                "material_count": "1",
                "armature_count": "0",
                "bone_count": "0",
                "animation_count": "0",
                "animation_names": "",
                "model_relative_path": "normalized/fixture/door.gltf",
            }

            result = MODULE.profile(row, vault)

            self.assertEqual("door", result["semantic_type"])
            self.assertEqual("hinge-side", result["placement"]["suggested_pivot"]["kind"])
            self.assertEqual({"x": -0.1, "y": 0.0, "z": -0.2}, result["geometry"]["bounds"]["minimum"])
            self.assertEqual({"x": 1.1, "y": 2.1, "z": 0.2}, result["geometry"]["bounds"]["maximum"])
            self.assertEqual(64, len(result["model_content_sha256"]))
            self.assertEqual(64, len(result["dependency_closure_sha256"]))
            self.assertTrue(result["placement"]["suggested_pivot"]["requires_review"])


if __name__ == "__main__":
    unittest.main()

"""Focused contract tests for governed additive and replacement kit assembly."""
import copy
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("Append-GovernedKitAssets.py")
SPEC = importlib.util.spec_from_file_location("append_governed_kit", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def digest(data):
    return hashlib.sha256(data).hexdigest()


class GovernedReplacementTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(dir=Path(__file__).parents[1] / "tmp")
        self.root = Path(self.tmp.name)
        self.source = self.root / "source"
        self.replacement = self.root / "replacement"
        self.output = self.root / "output"
        self.source.mkdir()
        self.replacement.mkdir()
        self._write_source()
        self._write_replacement()

    def tearDown(self):
        self.tmp.cleanup()

    @staticmethod
    def report(assets):
        return {
            "schema": "forge.blender-asset-kit-build", "schema_version": 1,
            "asset_kit_id": "test-kit", "asset_kit_revision": 1,
            "provenance": {
                "license": "LicenseRef-OpenRPG-Forge-Project-Generated",
                "source_kind": "procedural_original", "cadle_content_used": False,
                "commercial_game_content_used": False,
            },
            "qualification": {"technical_export_validated": True, "human_art_review": "approved", "publication_approved": True},
            "assets": assets,
        }

    @staticmethod
    def asset(asset_id, path, data):
        return {"id": asset_id, "kind": "architecture", "glb": {"path": path, "sha256": digest(data)}}

    @staticmethod
    def write(path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding="utf-8")

    def _write_source(self):
        self.a, self.b = b"source-a", b"source-b"
        self.assets = [self.asset("wall-a", "assets/wall-a.glb", self.a), self.asset("corner-a", "assets/corner-a.glb", self.b)]
        for asset, data in zip(self.assets, (self.a, self.b)):
            path = self.source / asset["glb"]["path"]
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        self.write(self.source / "build.json", self.report(self.assets))
        self.preview = {
            "schema": "forge.asset-kit-runtime-preview", "schema_version": 1,
            "asset_kit_id": "test-kit", "asset_kit_revision": 1,
            "assets": ["wall-a", "corner-a"],
            "placements": [{"id": "locked-wall", "asset_id": "wall-a", "locked": True}],
        }
        self.catalog = {
            "schema": "forge.vegetation-prototype-catalog", "schema_version": 2,
            "asset_kit_id": "test-kit", "asset_kit_revision": 1,
            "bindings": [{"asset_id": "wall-a", "glb_sha256": digest(self.a)}],
        }
        self.write(self.source / "preview.json", self.preview)
        self.write(self.source / "prototype-catalog.json", self.catalog)

    def _write_replacement(self, asset=None, data=b"replacement-b"):
        asset = asset or self.asset("corner-a", "assets/corner-a.glb", data)
        path = self.replacement / asset["glb"]["path"]
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        self.write(self.replacement / "asset-kit-build.v1.json", self.report([asset]))

    def test_replacement_preserves_other_assets_preview_catalog_and_placement_pins(self):
        MODULE.replace_existing(self.source, self.replacement, self.output)
        build = json.loads((self.output / "build.json").read_text(encoding="utf-8"))
        preview = json.loads((self.output / "preview.json").read_text(encoding="utf-8"))
        catalog = json.loads((self.output / "prototype-catalog.json").read_text(encoding="utf-8"))
        attestation = json.loads((self.output / "replacement-attestation.json").read_text(encoding="utf-8"))
        self.assertEqual((self.output / "assets/wall-a.glb").read_bytes(), self.a)
        self.assertEqual((self.output / "assets/corner-a.glb").read_bytes(), b"replacement-b")
        self.assertEqual(build["assets"][0], self.assets[0])
        self.assertEqual(build["assets"][1]["glb"]["sha256"], digest(b"replacement-b"))
        self.assertEqual(preview["assets"], self.preview["assets"])
        self.assertEqual(preview["placements"], self.preview["placements"])
        self.assertEqual(catalog["bindings"], self.catalog["bindings"])
        self.assertEqual(build["asset_kit_revision"], 2)
        self.assertEqual(preview["asset_kit_revision"], 2)
        self.assertEqual(catalog["asset_kit_revision"], 2)
        self.assertEqual(attestation["replacements"][0]["id"], "corner-a")
        self.assertEqual(build["qualification"]["human_art_review"], "pending")
        self.assertFalse(build["qualification"]["publication_approved"])

    def test_collision_without_replace_flag_is_refused(self):
        with self.assertRaisesRegex(ValueError, "use --replace-existing"):
            MODULE.assemble(self.source, self.replacement, self.output)
        self.assertFalse(self.output.exists())

    def test_replacement_rejects_mismatched_glb_path(self):
        self.replacement.unlink(missing_ok=True) if self.replacement.is_file() else None
        for child in self.replacement.rglob("*"):
            if child.is_file():
                child.unlink()
        mismatched = self.asset("corner-a", "assets/other-corner.glb", b"wrong-path")
        self._write_replacement(mismatched, b"wrong-path")
        with self.assertRaisesRegex(ValueError, "existing asset id and GLB path"):
            MODULE.replace_existing(self.source, self.replacement, self.output)
        self.assertFalse(self.output.exists())


if __name__ == "__main__":
    unittest.main()

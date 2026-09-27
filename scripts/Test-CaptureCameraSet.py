"""Camera snapshot invariants, including real compiler outputs and corruptions."""
import copy
import importlib.util
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

spec = importlib.util.spec_from_file_location(
    "prepare", Path(__file__).with_name("Prepare-CaptureCameraSet.py"))
prepare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prepare)
evidence = Path(sys.argv.pop(1)).resolve()


class CameraSetTests(unittest.TestCase):
    def test_extension_preserves_sources(self):
        original = {"validation_cameras": [{"id": "hero"}], "terrain": {"seed": 42}}
        additions = [{"id": "detail"}]
        before = copy.deepcopy(original)
        candidate = prepare.extend_cameras(original, additions)
        self.assertEqual(original, before)
        self.assertEqual(candidate["terrain"], original["terrain"])
        candidate["validation_cameras"][1]["id"] = "modified"
        self.assertEqual(additions, [{"id": "detail"}])

    def test_unsafe_duplicate_or_empty_additions_rejected(self):
        for additions in ([], [{"id": "hero"}], [{"id": "../escape"}],
                          [{"id": "x"}, {"id": "x"}], [{"id": ""}]):
            with self.subTest(additions=additions), self.assertRaises(ValueError):
                prepare.extend_cameras({"validation_cameras": [{"id": "hero"}]}, additions)

    def test_real_compilations_have_identical_geometry(self):
        prepare.check_geometry(evidence / "base", evidence / "camera-set")

    def test_geometry_and_lineage_corruptions_rejected(self):
        for name, field in (("terrain.v1.json", "unexpected_geometry"),
                            ("bridge-placements.v1.json", "placements"),
                            ("bridge-placements.v1.json", "source_traversal_audit_sha256"),
                            ("semantic-masks.v1.json", "source_world_sketch_sha256")):
            with self.subTest(field=field), tempfile.TemporaryDirectory() as temp:
                candidate = Path(temp) / "camera-set"
                shutil.copytree(evidence / "camera-set", candidate)
                path = candidate / name
                document = prepare.read(path)
                document[field] = "corrupted"
                path.unlink()
                prepare.save(path, document)
                with self.assertRaises(ValueError):
                    prepare.check_geometry(evidence / "base", candidate)


if __name__ == "__main__":
    unittest.main()

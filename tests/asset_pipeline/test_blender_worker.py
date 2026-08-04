import hashlib
import importlib.util
import json
import pathlib
import tempfile
import unittest
import uuid


WORKER_PATH = pathlib.Path(__file__).parents[2] / "tools" / "asset_pipeline" / "blender_worker.py"
SPEC = importlib.util.spec_from_file_location("blender_worker", WORKER_PATH)
worker = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(worker)


class BlenderWorkerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self.temporary.name)
        self.sources = self.root / "sources"
        self.workspace = self.root / "workspace"
        self.sources.mkdir()
        self.workspace.mkdir()
        self.source = self.sources / "tree.obj"
        self.source.write_text("o Tree\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n", encoding="utf-8")

    def tearDown(self):
        self.temporary.cleanup()

    def request(self):
        return {
            "schemaVersion": 1,
            "jobId": str(uuid.uuid4()),
            "assetId": "polyhaven:tree",
            "sourceRelativePath": "tree.obj",
            "outputRelativePath": "assets/imported/tree/model.glb",
            "sourceSha256": hashlib.sha256(self.source.read_bytes()).hexdigest(),
            "licenseSpdx": "CC0-1.0",
            "provenance": "asset-catalog:polyhaven:tree",
            "operations": ["validate_model", "normalize_model", "generate_lods", "export_glb"],
            "options": {
                "applyTransforms": True,
                "scaleMeters": 1.0,
                "pivot": "ground_center",
                "lodRatios": [0.25, 0.5, 0.5],
                "generateCollision": True,
            },
        }

    def test_validate_only_preserves_source_and_normalizes_typed_request(self):
        document = self.request()
        before = self.source.read_bytes()
        normalized = worker.validate_request(document, self.sources, self.workspace)
        self.assertEqual([0.5, 0.25], normalized["options"]["lodRatios"])
        response = worker.execute(normalized, self.root / "missing-blender.exe", self.root / "response.json", True)
        self.assertTrue(response["valid"])
        self.assertFalse(response["executed"])
        self.assertEqual(before, self.source.read_bytes())

    def test_rejects_path_escape_and_arbitrary_operation(self):
        path_escape = self.request()
        path_escape["outputRelativePath"] = "../outside.glb"
        with self.assertRaisesRegex(worker.WorkerError, "confined"):
            worker.validate_request(path_escape, self.sources, self.workspace)

        arbitrary = self.request()
        arbitrary["operations"] = ["python_exec"]
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(arbitrary, self.sources, self.workspace)
        self.assertEqual("asset.worker.operation_forbidden", context.exception.code)

    def test_reports_missing_blender_without_writing_output(self):
        normalized = worker.validate_request(self.request(), self.sources, self.workspace)
        with self.assertRaises(worker.WorkerError) as context:
            worker.execute(normalized, self.root / "missing-blender.exe", self.root / "response.json", False)
        self.assertEqual("asset.worker.blender_missing", context.exception.code)
        self.assertFalse((self.workspace / "assets" / "imported" / "tree" / "model.glb").exists())

    def test_cli_writes_stable_diagnostic_for_hash_mismatch(self):
        request_path = self.root / "request.json"
        response_path = self.root / "response.json"
        request = self.request()
        request["sourceSha256"] = "0" * 64
        request_path.write_text(json.dumps(request), encoding="utf-8")
        exit_code = worker.main([
            "--request", str(request_path),
            "--response", str(response_path),
            "--source-root", str(self.sources),
            "--workspace-root", str(self.workspace),
            "--validate-only",
        ])
        self.assertEqual(2, exit_code)
        response = json.loads(response_path.read_text(encoding="utf-8"))
        self.assertEqual("asset.worker.source_hash_mismatch", response["diagnostics"][0]["code"])


if __name__ == "__main__":
    unittest.main()

import hashlib
import importlib.util
import json
import pathlib
import struct
import tempfile
import unittest
import uuid
import zlib
from unittest import mock


WORKER_PATH = pathlib.Path(__file__).parents[2] / "tools" / "asset_pipeline" / "blender_worker.py"
SPEC = importlib.util.spec_from_file_location("blender_worker", WORKER_PATH)
worker = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(worker)


def fake_png(width=512, height=512, metadata=b""):
    def chunk(kind, payload):
        checksum = zlib.crc32(kind + payload) & 0xFFFFFFFF
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", checksum)

    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    text = chunk(b"tEXt", metadata) if metadata else b""
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + text + chunk(b"IEND", b"")


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
            "operations": [
                "validate_model",
                "normalize_model",
                "generate_lods",
                "export_glb",
                "generate_thumbnail",
                "audit_quality",
            ],
            "options": {
                "applyTransforms": True,
                "scaleMeters": 1.0,
                "pivot": "ground_center",
                "lodRatios": [0.25, 0.5, 0.5],
                "generateCollision": True,
                "animationSources": [],
                "materialTextures": [],
            },
        }

    def retarget_profile(self):
        return {
            "profileId": "source58-to-target41-v1",
            "sourceRig": "source-rig-58",
            "targetRig": "target-rig-41",
            "sourceBoneCount": 58,
            "targetBoneCount": 41,
            "rootMotionTarget": "root",
            "minimumMappedBoneRatio": 0.25,
            "maximumBoundsExpansion": 2.5,
            "bones": {
                "root": "SourceRoot",
                "hips": "SourceHips",
                "spine": "SourceSpine",
                "chest": "SourceChest",
                "head": "SourceHead",
                "upperarm.l": "SourceUpperArmL",
                "lowerarm.l": "SourceLowerArmL",
                "wrist.l": "SourceWristL",
                "upperarm.r": "SourceUpperArmR",
                "lowerarm.r": "SourceLowerArmR",
                "wrist.r": "SourceWristR",
                "upperleg.l": "SourceUpperLegL",
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
        self.assertEqual("assets/imported/tree/model.preview.png", response["previewRelativePath"])
        self.assertEqual("assets/imported/tree/model.quality.json", response["qualityReportRelativePath"])
        self.assertEqual(before, self.source.read_bytes())

    def test_validates_governed_animation_and_texture_companions(self):
        animation = self.sources / "idle.fbx"
        texture = self.sources / "skin.png"
        animation.write_bytes(b"fbx animation")
        texture.write_bytes(fake_png())
        document = self.request()
        document["options"]["animationSources"] = [{
            "name": "idle",
            "relativePath": "idle.fbx",
            "contentSha256": hashlib.sha256(animation.read_bytes()).hexdigest(),
        }]
        document["options"]["materialTextures"] = [{
            "material": "skin",
            "channel": "base_color",
            "relativePath": "skin.png",
            "contentSha256": hashlib.sha256(texture.read_bytes()).hexdigest(),
        }]

        normalized = worker.validate_request(document, self.sources, self.workspace)
        response = worker.execute(normalized, self.root / "missing-blender.exe", self.root / "response.json", True)

        self.assertTrue(response["valid"])
        self.assertEqual("idle", normalized["options"]["animationSources"][0]["name"])

        document["options"]["animationSources"][0]["relativePath"] = "../idle.fbx"
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(document, self.sources, self.workspace)
        self.assertEqual("asset.worker.path_unsafe", context.exception.code)

    def test_validates_typed_one_to_one_retarget_profile(self):
        animation = self.sources / "run.fbx"
        animation.write_bytes(b"fbx run animation")
        document = self.request()
        document["options"]["animationSources"] = [{
            "name": "run",
            "relativePath": "run.fbx",
            "contentSha256": hashlib.sha256(animation.read_bytes()).hexdigest(),
        }]
        document["options"]["retargetProfile"] = self.retarget_profile()

        normalized = worker.validate_request(document, self.sources, self.workspace)

        self.assertEqual("source58-to-target41-v1", normalized["options"]["retargetProfile"]["profileId"])
        self.assertEqual(12, len(normalized["options"]["retargetProfile"]["bones"]))
        self.assertEqual("SourceRoot", normalized["options"]["retargetProfile"]["bones"]["root"])

        document["options"]["retargetProfile"]["bones"]["hips"] = "SourceRoot"
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(document, self.sources, self.workspace)
        self.assertEqual("asset.worker.retarget_map_invalid", context.exception.code)

    def test_rejects_unused_or_under_specified_retarget_profile(self):
        document = self.request()
        document["options"]["retargetProfile"] = self.retarget_profile()
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(document, self.sources, self.workspace)
        self.assertEqual("asset.worker.retarget_profile_unused", context.exception.code)

        animation = self.sources / "run.fbx"
        animation.write_bytes(b"fbx run animation")
        document["options"]["animationSources"] = [{
            "name": "run",
            "relativePath": "run.fbx",
            "contentSha256": hashlib.sha256(animation.read_bytes()).hexdigest(),
        }]
        document["options"]["retargetProfile"]["bones"].pop("upperleg.l")
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(document, self.sources, self.workspace)
        self.assertEqual("asset.worker.retarget_map_invalid", context.exception.code)

    def test_thumbnail_canonicalization_removes_nondeterministic_metadata(self):
        first = self.root / "first.png"
        second = self.root / "second.png"
        first.write_bytes(fake_png(metadata=b"RenderTime\x001.25"))
        second.write_bytes(fake_png(metadata=b"RenderTime\x001.93"))

        worker._canonicalize_png(first)
        worker._canonicalize_png(second)

        self.assertEqual(first.read_bytes(), second.read_bytes())

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

    def test_execute_encodes_typed_payloads_for_windows_and_reports_output(self):
        blender = self.root / "blender.exe"
        blender.write_bytes(b"stub")
        normalized = worker.validate_request(self.request(), self.sources, self.workspace)
        output_path = pathlib.Path(normalized["_outputPath"])

        def run(command, **_kwargs):
            self.assertIn("--disable-autoexec", command)
            self.assertNotIn('"', command[-2])
            self.assertNotIn("{", command[-1])
            output_path.parent.mkdir(parents=True, exist_ok=True)
            pathlib.Path(command[-5]).write_bytes(struct.pack("<4sII", b"glTF", 2, 12))
            pathlib.Path(command[-4]).write_bytes(fake_png())
            pathlib.Path(command[-3]).write_text(json.dumps({
                "schemaVersion": 1,
                "passed": True,
                "metrics": {"triangleCount": 1, "vertexCount": 3},
                "checks": [{"code": "asset.quality.mesh_present", "status": "pass", "message": "ok"}],
            }), encoding="utf-8")
            return worker.subprocess.CompletedProcess(command, 0, stdout="Blender quit", stderr="")

        before = self.source.read_bytes()
        with mock.patch.object(worker.subprocess, "run", side_effect=run):
            response = worker.execute(normalized, blender, self.root / "response.json", False)

        self.assertTrue(response["executed"])
        self.assertEqual(12, response["outputBytes"])
        self.assertEqual(512, response["preview"]["width"])
        self.assertTrue(response["quality"]["passed"])
        self.assertEqual(100, response["quality"]["scores"]["overall"])
        self.assertTrue((output_path.parent / "model.preview.png").is_file())
        self.assertTrue((output_path.parent / "model.quality.json").is_file())
        self.assertEqual(before, self.source.read_bytes())

    def test_execute_rejects_failed_quality_gate_but_keeps_report(self):
        blender = self.root / "blender.exe"
        blender.write_bytes(b"stub")
        normalized = worker.validate_request(self.request(), self.sources, self.workspace)
        output_path = pathlib.Path(normalized["_outputPath"])

        def run(command, **_kwargs):
            output_path.parent.mkdir(parents=True, exist_ok=True)
            pathlib.Path(command[-5]).write_bytes(struct.pack("<4sII", b"glTF", 2, 12))
            pathlib.Path(command[-4]).write_bytes(fake_png())
            pathlib.Path(command[-3]).write_text(json.dumps({
                "schemaVersion": 1,
                "passed": False,
                "metrics": {"triangleCount": 0, "vertexCount": 0},
                "checks": [{
                    "code": "asset.quality.geometry_present",
                    "status": "fail",
                    "message": "geometry missing",
                }],
            }), encoding="utf-8")
            return worker.subprocess.CompletedProcess(command, 0, stdout="Blender quit", stderr="")

        with mock.patch.object(worker.subprocess, "run", side_effect=run):
            with self.assertRaises(worker.WorkerError) as context:
                worker.execute(normalized, blender, self.root / "response.json", False)

        self.assertEqual("asset.worker.quality_failed", context.exception.code)
        self.assertFalse(output_path.exists())
        self.assertFalse((output_path.parent / "model.preview.png").exists())
        quality = json.loads((output_path.parent / "model.quality.json").read_text(encoding="utf-8"))
        self.assertFalse(quality["passed"])
        self.assertLess(quality["scores"]["technical"], 100)

    def test_execute_surfaces_blender_traceback_even_when_blender_returns_zero(self):
        blender = self.root / "blender.exe"
        blender.write_bytes(b"stub")
        normalized = worker.validate_request(self.request(), self.sources, self.workspace)
        completed = worker.subprocess.CompletedProcess(
            [str(blender)],
            0,
            stdout="Blender quit",
            stderr="Traceback: bridge failed",
        )

        with mock.patch.object(worker.subprocess, "run", return_value=completed):
            with self.assertRaises(worker.WorkerError) as context:
                worker.execute(normalized, blender, self.root / "response.json", False)

        self.assertEqual("asset.worker.blender_failed", context.exception.code)
        self.assertIn("Traceback", str(context.exception))

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

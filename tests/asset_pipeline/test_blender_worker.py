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
                "supplementalMeshSources": [],
                "materialTextures": [],
                "includedMeshNames": ["Tree_Main"],
                "baseDecimationRatio": 0.125,
            },
            "securityPolicy": {
                "schemaVersion": 1,
                "id": "a1230000-0000-4000-8000-000000000001",
                "denyFreeFormPython": True,
                "denyShell": True,
                "denyEval": True,
                "requireContainedPaths": True,
                "networkAllowed": False,
                "maximumJobSeconds": 300,
                "maximumOutputBytes": 512 * 1024 * 1024,
                "allowedBlenderOperations": sorted(worker.ALLOWED_OPERATIONS),
                "requiredPermission": "configure",
                "provenance": "test:g12-l",
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

    def creation_request(self):
        recipe = {
            "schemaVersion": 1,
            "id": str(uuid.uuid4()),
            "slug": "forge-pillar",
            "displayName": "Forge Pillar",
            "provenance": "assistant:test",
            "licenseSpdx": "CC0-1.0",
            "primitives": [{
                "id": str(uuid.uuid4()),
                "name": "pillar",
                "kind": "cylinder",
                "positionMeters": {"x": 0.0, "y": 0.0, "z": 1.0},
                "rotationDegrees": {"x": 0.0, "y": 0.0, "z": 0.0},
                "scaleMeters": {"x": 1.0, "y": 1.0, "z": 2.0},
            }],
            "material": {
                "name": "forge_stone",
                "baseColor": {"red": 0.32, "green": 0.24, "blue": 0.18, "alpha": 1.0},
                "metallic": 0.05,
                "roughness": 0.72,
            },
            "uvProjection": "cube",
            "lodRatios": [0.5, 0.25],
            "generateCollision": True,
            "outputRelativePath": "assets/generated/forge-pillar/model.glb",
        }
        source = self.sources / "forge-pillar.recipe.json"
        source.write_text(json.dumps(recipe), encoding="utf-8")
        document = self.request()
        document.update({
            "assetId": "generated:forge-pillar",
            "sourceRelativePath": source.name,
            "outputRelativePath": recipe["outputRelativePath"],
            "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            "provenance": "assistant:test:typed-blender-creation",
            "operations": [
                "create_primitives", "validate_model", "normalize_model", "generate_lods",
                "generate_collision", "export_glb", "generate_thumbnail", "audit_quality",
            ],
        })
        document["options"].update({
            "lodRatios": recipe["lodRatios"],
            "generateCollision": True,
            "creationRecipe": recipe,
        })
        return document

    def test_validates_typed_primitive_creation_without_python_or_unbounded_fields(self):
        document = self.creation_request()
        normalized = worker.validate_request(document, self.sources, self.workspace)
        response = worker.execute(normalized, self.root / "missing-blender.exe", self.root / "response.json", True)

        self.assertTrue(response["valid"])
        self.assertFalse(response["executed"])
        self.assertEqual("cylinder", normalized["options"]["creationRecipe"]["primitives"][0]["kind"])
        self.assertTrue(normalized["securityPolicy"]["denyFreeFormPython"])
        self.assertNotIn("script", normalized)

        document["options"]["creationRecipe"]["primitives"][0]["kind"] = "python"
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(document, self.sources, self.workspace)
        self.assertEqual("asset.worker.creation_recipe_invalid", context.exception.code)

    def test_rejects_creation_recipe_that_differs_from_immutable_source(self):
        document = self.creation_request()
        document["options"]["creationRecipe"]["displayName"] = "Tampered"
        normalized = worker.validate_request(document, self.sources, self.workspace)

        with self.assertRaises(worker.WorkerError) as context:
            worker.execute(normalized, self.root / "missing-blender.exe", self.root / "response.json", True)
        self.assertEqual("asset.worker.creation_recipe_mismatch", context.exception.code)

    def test_validate_only_preserves_source_and_normalizes_typed_request(self):
        document = self.request()
        before = self.source.read_bytes()
        normalized = worker.validate_request(document, self.sources, self.workspace)
        self.assertEqual([0.5, 0.25], normalized["options"]["lodRatios"])
        self.assertEqual(["Tree_Main"], normalized["options"]["includedMeshNames"])
        self.assertEqual(0.125, normalized["options"]["baseDecimationRatio"])
        response = worker.execute(normalized, self.root / "missing-blender.exe", self.root / "response.json", True)
        self.assertTrue(response["valid"])
        self.assertFalse(response["executed"])
        self.assertEqual("assets/imported/tree/model.preview.png", response["previewRelativePath"])
        self.assertEqual("assets/imported/tree/model.quality.json", response["qualityReportRelativePath"])
        self.assertEqual(before, self.source.read_bytes())

    def test_rejects_unsafe_mesh_selection_and_decimation(self):
        unsafe_name = self.request()
        unsafe_name["options"]["includedMeshNames"] = ["../Tree"]
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(unsafe_name, self.sources, self.workspace)
        self.assertEqual("asset.worker.mesh_selection_invalid", context.exception.code)

        unsafe_ratio = self.request()
        unsafe_ratio["options"]["baseDecimationRatio"] = 0.0001
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(unsafe_ratio, self.sources, self.workspace)
        self.assertEqual("asset.worker.base_decimation_invalid", context.exception.code)

    def test_accepts_null_optional_mesh_selection_from_older_callers(self):
        document = self.request()
        document["options"]["includedMeshNames"] = None

        normalized = worker.validate_request(document, self.sources, self.workspace)

        self.assertEqual([], normalized["options"]["includedMeshNames"])

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

    def test_validates_governed_supplemental_skinned_mesh_source(self):
        supplement = self.sources / "base-character.gltf"
        supplement.write_text('{"asset":{"version":"2.0"}}', encoding="utf-8")
        document = self.request()
        document["options"]["supplementalMeshSources"] = [{
            "relativePath": supplement.name,
            "contentSha256": hashlib.sha256(supplement.read_bytes()).hexdigest(),
            "includedMeshNames": ["Eyes", "Eyebrows", "SuperHero_Male"],
            "minimumVertexZ": 1.46,
        }]

        normalized = worker.validate_request(document, self.sources, self.workspace)
        response = worker.execute(
            normalized,
            self.root / "missing-blender.exe",
            self.root / "response.json",
            True,
        )

        self.assertTrue(response["valid"])
        supplement_plan = normalized["options"]["supplementalMeshSources"][0]
        self.assertEqual(["Eyes", "Eyebrows", "SuperHero_Male"], supplement_plan["includedMeshNames"])
        self.assertEqual(1.46, supplement_plan["minimumVertexZ"])

        document["options"]["supplementalMeshSources"][0]["includedMeshNames"] = ["../Head"]
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(document, self.sources, self.workspace)
        self.assertEqual("asset.worker.supplemental_mesh_selection_invalid", context.exception.code)

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

    def test_rejects_exec_eval_shell_fields_and_unsafe_policy(self):
        for field in ("python", "exec", "eval", "shell", "script", "command"):
            document = self.request()
            document[field] = "arbitrary payload"
            with self.assertRaises(worker.WorkerError) as context:
                worker.validate_request(document, self.sources, self.workspace)
            self.assertEqual("asset.worker.request_field_forbidden", context.exception.code)

        for field, unsafe in (
            ("denyFreeFormPython", False),
            ("denyShell", False),
            ("denyEval", False),
            ("requireContainedPaths", False),
            ("networkAllowed", True),
        ):
            document = self.request()
            document["securityPolicy"][field] = unsafe
            with self.assertRaises(worker.WorkerError):
                worker.validate_request(document, self.sources, self.workspace)

    def test_policy_limits_operations_timeout_and_output_size(self):
        disabled = self.request()
        disabled["securityPolicy"]["allowedBlenderOperations"].remove("generate_thumbnail")
        with self.assertRaises(worker.WorkerError) as context:
            worker.validate_request(disabled, self.sources, self.workspace)
        self.assertEqual("asset.worker.operation_disabled", context.exception.code)

        blender = self.root / "blender.exe"
        blender.write_bytes(b"stub")
        document = self.request()
        document["operations"] = ["validate_model", "export_glb"]
        document["securityPolicy"]["maximumJobSeconds"] = 17
        document["securityPolicy"]["maximumOutputBytes"] = 1024 * 1024
        normalized = worker.validate_request(document, self.sources, self.workspace)
        output_path = pathlib.Path(normalized["_outputPath"])

        def run(command, **kwargs):
            self.assertEqual(17, kwargs["timeout"])
            output_path.parent.mkdir(parents=True, exist_ok=True)
            body = b"x" * (1024 * 1024)
            pathlib.Path(command[-5]).write_bytes(struct.pack("<4sII", b"glTF", 2, 12 + len(body)) + body)
            return worker.subprocess.CompletedProcess(command, 0, stdout="Blender quit", stderr="")

        with mock.patch.object(worker.subprocess, "run", side_effect=run):
            with self.assertRaises(worker.WorkerError) as context:
                worker.execute(normalized, blender, self.root / "response.json", False)
        self.assertEqual("asset.worker.output_too_large", context.exception.code)
        self.assertFalse(output_path.exists())

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

    def test_execute_maps_process_timeout_to_stable_policy_diagnostic(self):
        blender = self.root / "blender.exe"
        blender.write_bytes(b"stub")
        document = self.request()
        document["securityPolicy"]["maximumJobSeconds"] = 19
        normalized = worker.validate_request(document, self.sources, self.workspace)

        with mock.patch.object(worker.subprocess, "run",
                               side_effect=worker.subprocess.TimeoutExpired([str(blender)], 19)):
            with self.assertRaises(worker.WorkerError) as context:
                worker.execute(normalized, blender, self.root / "response.json", False)

        self.assertEqual("asset.worker.timeout", context.exception.code)
        self.assertIn("19 second", str(context.exception))

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

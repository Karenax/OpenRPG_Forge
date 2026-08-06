#!/usr/bin/env python3
"""Typed, confined bridge between OpenRPG Forge and Blender headless."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import pathlib
import struct
import subprocess
import sys
import tempfile
import uuid


SCHEMA_VERSION = 1
ALLOWED_OPERATIONS = {
    "validate_model",
    "normalize_model",
    "generate_collision",
    "generate_lods",
    "export_glb",
    "generate_thumbnail",
    "audit_quality",
}
ALLOWED_INPUT_EXTENSIONS = {".blend", ".dae", ".fbx", ".glb", ".gltf", ".obj"}
ALLOWED_PIVOTS = {"keep", "ground_center", "object_center"}
HASH_LENGTH = 64


class WorkerError(Exception):
    def __init__(self, code: str, message: str, path: str = "") -> None:
        super().__init__(message)
        self.code = code
        self.path = path


def _contained(root: pathlib.Path, relative: str, *, output: bool = False) -> pathlib.Path:
    value = pathlib.PurePosixPath(relative.replace("\\", "/"))
    if not relative or value.is_absolute() or ".." in value.parts:
        raise WorkerError("asset.worker.path_unsafe", "Path must be relative and confined.", relative)
    candidate = (root / pathlib.Path(*value.parts)).resolve()
    try:
        candidate.relative_to(root.resolve())
    except ValueError as error:
        raise WorkerError("asset.worker.path_unsafe", "Path escapes its configured root.", relative) from error
    if output and candidate.suffix.lower() != ".glb":
        raise WorkerError("asset.worker.output_format_invalid", "Blender output must be a GLB file.", relative)
    return candidate


def _sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _sidecar_path(output_path: pathlib.Path, label: str, extension: str) -> pathlib.Path:
    return output_path.with_name(f"{output_path.stem}.{label}{extension}")


def _sidecar_relative(output_relative: str, label: str, extension: str) -> str:
    value = pathlib.PurePosixPath(output_relative)
    return str(value.with_name(f"{value.stem}.{label}{extension}"))


def _validate_glb(path: pathlib.Path) -> None:
    if path.stat().st_size < 12:
        raise WorkerError("asset.worker.output_invalid", "Blender output is shorter than a GLB 2 header.", str(path))
    with path.open("rb") as stream:
        magic, version, declared_length = struct.unpack("<4sII", stream.read(12))
    if magic != b"glTF" or version != 2 or declared_length != path.stat().st_size:
        raise WorkerError("asset.worker.output_invalid", "Blender output is not a self-consistent GLB 2 file.", str(path))


def _validate_png(path: pathlib.Path) -> tuple[int, int]:
    if not path.is_file() or path.stat().st_size < 24:
        raise WorkerError("asset.worker.thumbnail_missing", "Blender did not produce a PNG thumbnail.", str(path))
    with path.open("rb") as stream:
        header = stream.read(24)
    if header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise WorkerError("asset.worker.thumbnail_invalid", "Generated thumbnail is not a valid PNG.", str(path))
    width, height = struct.unpack(">II", header[16:24])
    if width < 64 or height < 64 or width > 4096 or height > 4096:
        raise WorkerError("asset.worker.thumbnail_invalid", "Generated thumbnail dimensions are outside policy.", str(path))
    return width, height


def _canonicalize_png(path: pathlib.Path) -> None:
    if not path.is_file():
        raise WorkerError("asset.worker.thumbnail_missing", "Blender did not produce a PNG thumbnail.", str(path))
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise WorkerError("asset.worker.thumbnail_invalid", "Generated thumbnail is not a valid PNG.", str(path))
    position = 8
    chunks = []
    stable_ancillary = {b"cHRM", b"gAMA", b"iCCP", b"sRGB"}
    while position < len(data):
        if position + 12 > len(data):
            raise WorkerError("asset.worker.thumbnail_invalid", "PNG chunk header is truncated.", str(path))
        length = struct.unpack(">I", data[position : position + 4])[0]
        end = position + 12 + length
        if end > len(data):
            raise WorkerError("asset.worker.thumbnail_invalid", "PNG chunk payload is truncated.", str(path))
        chunk_type = data[position + 4 : position + 8]
        if not chunk_type or (not 65 <= chunk_type[0] <= 90 and chunk_type not in stable_ancillary):
            position = end
            continue
        chunks.append(data[position:end])
        position = end
        if chunk_type == b"IEND":
            break
    if not chunks or chunks[0][4:8] != b"IHDR" or chunks[-1][4:8] != b"IEND":
        raise WorkerError("asset.worker.thumbnail_invalid", "PNG critical chunks are incomplete.", str(path))
    descriptor, temporary = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data[:8])
            stream.write(b"".join(chunks))
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def _quality_report(request: dict, bridge_report: object, preview_generated: bool) -> dict:
    if not isinstance(bridge_report, dict) or bridge_report.get("schemaVersion") != SCHEMA_VERSION:
        raise WorkerError("asset.worker.quality_report_invalid", "Blender quality report schema is invalid.")
    metrics = bridge_report.get("metrics")
    checks = bridge_report.get("checks")
    if (
        not isinstance(metrics, dict)
        or not isinstance(checks, list)
        or not isinstance(bridge_report.get("passed"), bool)
        or any(
            not isinstance(item, dict)
            or item.get("status") not in {"pass", "warning", "fail"}
            or not str(item.get("code", "")).strip()
            for item in checks
        )
    ):
        raise WorkerError("asset.worker.quality_report_invalid", "Blender quality metrics or checks are invalid.")
    triangle_value = metrics.get("triangleCount", 0)
    if isinstance(triangle_value, bool) or not isinstance(triangle_value, int) or triangle_value < 0:
        raise WorkerError("asset.worker.quality_report_invalid", "Blender triangle metric is invalid.")
    failed = sum(1 for item in checks if item.get("status") == "fail")
    warnings = sum(1 for item in checks if item.get("status") == "warning")
    technical = max(0, 100 - failed * 40 - warnings * 8)
    triangles = triangle_value
    performance = 100 if triangles <= 500_000 else 75 if triangles <= 2_000_000 else 30
    visual = 100 if preview_generated else 50
    scores = {
        "license": 100,
        "sourceTrust": 100,
        "technical": technical,
        "visual": visual,
        "performance": performance,
        "metadata": 100,
    }
    scores["overall"] = round(sum(scores.values()) / len(scores))
    return {
        "schemaVersion": SCHEMA_VERSION,
        "assetId": request["assetId"],
        "sourceSha256": request["sourceSha256"],
        "licenseSpdx": request["licenseSpdx"],
        "provenance": request["provenance"],
        "passed": failed == 0 and bool(bridge_report.get("passed")),
        "metrics": metrics,
        "checks": checks,
        "scores": scores,
    }


def _validate_hash(value: object, field: str) -> str:
    normalized = str(value or "").strip().lower()
    if len(normalized) != HASH_LENGTH or any(character not in "0123456789abcdef" for character in normalized):
        raise WorkerError("asset.worker.hash_invalid", f"{field} must be a lowercase SHA-256 hash.", field)
    return normalized


def validate_request(document: object, source_root: pathlib.Path, workspace_root: pathlib.Path) -> dict:
    if not isinstance(document, dict):
        raise WorkerError("asset.worker.request_invalid", "Worker request must be a JSON object.")
    if document.get("schemaVersion") != SCHEMA_VERSION:
        raise WorkerError("asset.worker.schema_unsupported", "Unsupported worker request schema.", "schemaVersion")
    try:
        job_id = str(uuid.UUID(str(document.get("jobId", ""))))
    except ValueError as error:
        raise WorkerError("asset.worker.job_id_invalid", "jobId must be a non-empty UUID.", "jobId") from error

    operations = document.get("operations")
    if not isinstance(operations, list) or not operations:
        raise WorkerError("asset.worker.operations_missing", "At least one typed operation is required.", "operations")
    normalized_operations = []
    for operation in operations:
        name = str(operation).strip().lower()
        if name not in ALLOWED_OPERATIONS:
            raise WorkerError("asset.worker.operation_forbidden", f"Operation '{name}' is not allowed.", "operations")
        if name not in normalized_operations:
            normalized_operations.append(name)

    source_relative = str(document.get("sourceRelativePath", ""))
    output_relative = str(document.get("outputRelativePath", ""))
    source_path = _contained(source_root, source_relative)
    output_path = _contained(workspace_root, output_relative, output=True)
    if source_path.suffix.lower() not in ALLOWED_INPUT_EXTENSIONS:
        raise WorkerError("asset.worker.source_format_invalid", "Unsupported model source format.", source_relative)
    expected_hash = _validate_hash(document.get("sourceSha256"), "sourceSha256")

    options = document.get("options", {})
    if not isinstance(options, dict):
        raise WorkerError("asset.worker.options_invalid", "options must be a JSON object.", "options")
    unknown_options = set(options) - {
        "applyTransforms",
        "scaleMeters",
        "pivot",
        "lodRatios",
        "generateCollision",
    }
    if unknown_options:
        raise WorkerError(
            "asset.worker.option_forbidden",
            f"Unknown worker option: {sorted(unknown_options)[0]}.",
            "options",
        )
    scale = options.get("scaleMeters", 1.0)
    if isinstance(scale, bool) or not isinstance(scale, (int, float)) or not 0.001 <= float(scale) <= 1000:
        raise WorkerError("asset.worker.scale_invalid", "scaleMeters must be between 0.001 and 1000.", "options.scaleMeters")
    pivot = str(options.get("pivot", "keep")).strip().lower()
    if pivot not in ALLOWED_PIVOTS:
        raise WorkerError("asset.worker.pivot_invalid", "Unsupported pivot policy.", "options.pivot")
    lod_ratios = options.get("lodRatios", [])
    if not isinstance(lod_ratios, list) or any(
        isinstance(ratio, bool) or not isinstance(ratio, (int, float)) or not 0.01 <= float(ratio) < 1
        for ratio in lod_ratios
    ):
        raise WorkerError("asset.worker.lod_invalid", "LOD ratios must be numbers in [0.01, 1).", "options.lodRatios")
    normalized_lods = sorted({float(ratio) for ratio in lod_ratios}, reverse=True)

    return {
        "schemaVersion": SCHEMA_VERSION,
        "jobId": job_id,
        "assetId": str(document.get("assetId", "")).strip(),
        "sourceRelativePath": source_relative.replace("\\", "/"),
        "outputRelativePath": output_relative.replace("\\", "/"),
        "sourceSha256": expected_hash,
        "licenseSpdx": str(document.get("licenseSpdx", "")).strip(),
        "provenance": str(document.get("provenance", "")).strip(),
        "operations": normalized_operations,
        "options": {
            "applyTransforms": bool(options.get("applyTransforms", True)),
            "scaleMeters": float(scale),
            "pivot": pivot,
            "lodRatios": normalized_lods,
            "generateCollision": bool(options.get("generateCollision", False)),
        },
        "_sourcePath": str(source_path),
        "_outputPath": str(output_path),
    }


def _write_json_atomically(path: pathlib.Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(payload, stream, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def _encode_bridge_payload(payload: object) -> str:
    serialized = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    return base64.urlsafe_b64encode(serialized).decode("ascii")


def execute(request: dict, blender: pathlib.Path, response_path: pathlib.Path, validate_only: bool) -> dict:
    source_path = pathlib.Path(request.pop("_sourcePath"))
    output_path = pathlib.Path(request.pop("_outputPath"))
    if not source_path.is_file():
        raise WorkerError("asset.worker.source_missing", "Model source does not exist.", request["sourceRelativePath"])
    source_hash_before = _sha256(source_path)
    if source_hash_before != request["sourceSha256"]:
        raise WorkerError("asset.worker.source_hash_mismatch", "Model source hash differs from the request.", request["sourceRelativePath"])
    if not request["assetId"] or not request["licenseSpdx"] or not request["provenance"]:
        raise WorkerError(
            "asset.worker.governance_missing",
            "assetId, licenseSpdx and provenance are required.",
            request["sourceRelativePath"],
        )
    if validate_only:
        preview_relative = _sidecar_relative(request["outputRelativePath"], "preview", ".png")
        quality_relative = _sidecar_relative(request["outputRelativePath"], "quality", ".json")
        return {
            "valid": True,
            "executed": False,
            "schemaVersion": SCHEMA_VERSION,
            "jobId": request["jobId"],
            "operations": request["operations"],
            "previewRelativePath": preview_relative if "generate_thumbnail" in request["operations"] else None,
            "qualityReportRelativePath": quality_relative if "audit_quality" in request["operations"] else None,
            "diagnostics": [],
        }
    if not blender.is_file():
        raise WorkerError("asset.worker.blender_missing", "Configured Blender executable was not found.", str(blender))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    preview_path = _sidecar_path(output_path, "preview", ".png")
    quality_path = _sidecar_path(output_path, "quality", ".json")
    temporary_output_path = output_path.parent / f".{output_path.stem}.{request['jobId']}.tmp.glb"
    temporary_preview_path = output_path.parent / f".{output_path.stem}.{request['jobId']}.tmp.png"
    temporary_quality_path = output_path.parent / f".{output_path.stem}.{request['jobId']}.tmp.json"
    bridge_report_path = output_path.parent / f".{output_path.stem}.{request['jobId']}.bridge-report.json"
    temporary_paths = (temporary_output_path, temporary_preview_path, temporary_quality_path, bridge_report_path)
    for temporary_path in temporary_paths:
        if temporary_path.exists():
            temporary_path.unlink()
    bridge = pathlib.Path(__file__).with_name("blender_bridge_entry.py").resolve()
    command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--disable-autoexec",
        "--python",
        str(bridge),
        "--",
        str(source_path),
        str(temporary_output_path),
        str(temporary_preview_path),
        str(bridge_report_path),
        _encode_bridge_payload(request["operations"]),
        _encode_bridge_payload(request["options"]),
    ]
    try:
        completed = subprocess.run(command, capture_output=True, text=True, timeout=900, check=False)
        detail = (completed.stderr or completed.stdout or "").strip()
        if completed.returncode != 0 or "Traceback" in (completed.stderr or ""):
            raise WorkerError(
                "asset.worker.blender_failed",
                (detail or "Blender worker failed.")[-2000:],
                request["outputRelativePath"],
            )
        if not temporary_output_path.is_file():
            if detail:
                raise WorkerError("asset.worker.blender_failed", detail[-2000:], request["outputRelativePath"])
            raise WorkerError("asset.worker.output_missing", "Blender did not produce the requested GLB.", request["outputRelativePath"])
        _validate_glb(temporary_output_path)
        if _sha256(source_path) != source_hash_before:
            raise WorkerError("asset.worker.source_modified", "External worker modified the immutable source.", request["sourceRelativePath"])

        preview = None
        if "generate_thumbnail" in request["operations"]:
            _canonicalize_png(temporary_preview_path)
            width, height = _validate_png(temporary_preview_path)
            preview = {
                "relativePath": _sidecar_relative(request["outputRelativePath"], "preview", ".png"),
                "sha256": _sha256(temporary_preview_path),
                "bytes": temporary_preview_path.stat().st_size,
                "width": width,
                "height": height,
            }

        quality = None
        if "audit_quality" in request["operations"]:
            if not bridge_report_path.is_file():
                raise WorkerError("asset.worker.quality_report_missing", "Blender did not produce a quality report.")
            try:
                bridge_report = json.loads(bridge_report_path.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError) as error:
                raise WorkerError("asset.worker.quality_report_invalid", str(error), str(bridge_report_path)) from error
            quality = _quality_report(request, bridge_report, preview is not None)
            if not quality["passed"]:
                _write_json_atomically(quality_path, quality)
                failed_codes = [item.get("code", "unknown") for item in quality["checks"] if item.get("status") == "fail"]
                raise WorkerError(
                    "asset.worker.quality_failed",
                    f"Model quality gate failed: {', '.join(failed_codes)}.",
                    _sidecar_relative(request["outputRelativePath"], "quality", ".json"),
                )
            _write_json_atomically(temporary_quality_path, quality)
        os.replace(temporary_output_path, output_path)
        if preview is not None:
            os.replace(temporary_preview_path, preview_path)
        if quality is not None:
            os.replace(temporary_quality_path, quality_path)
    finally:
        for temporary_path in temporary_paths:
            if temporary_path.exists():
                temporary_path.unlink()

    result = {
        "valid": True,
        "executed": True,
        "schemaVersion": SCHEMA_VERSION,
        "jobId": request["jobId"],
        "operations": request["operations"],
        "sourceSha256": source_hash_before,
        "outputRelativePath": request["outputRelativePath"],
        "outputSha256": _sha256(output_path),
        "outputBytes": output_path.stat().st_size,
        "preview": preview,
        "quality": quality,
        "qualityReportRelativePath": (
            _sidecar_relative(request["outputRelativePath"], "quality", ".json") if quality is not None else None
        ),
        "diagnostics": [],
    }
    _write_json_atomically(response_path, result)
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--request", required=True, type=pathlib.Path)
    parser.add_argument("--response", required=True, type=pathlib.Path)
    parser.add_argument("--source-root", required=True, type=pathlib.Path)
    parser.add_argument("--workspace-root", required=True, type=pathlib.Path)
    parser.add_argument("--blender", default="blender", type=pathlib.Path)
    parser.add_argument("--validate-only", action="store_true")
    arguments = parser.parse_args(argv)
    try:
        document = json.loads(arguments.request.read_text(encoding="utf-8"))
        request = validate_request(document, arguments.source_root.resolve(), arguments.workspace_root.resolve())
        result = execute(request, arguments.blender, arguments.response, arguments.validate_only)
        _write_json_atomically(arguments.response, result)
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        return 0
    except (OSError, json.JSONDecodeError, WorkerError, subprocess.SubprocessError) as error:
        if isinstance(error, WorkerError):
            code, path = error.code, error.path
        else:
            code, path = "asset.worker.request_failed", str(arguments.request)
        result = {
            "valid": False,
            "executed": False,
            "schemaVersion": SCHEMA_VERSION,
            "diagnostics": [{"code": code, "message": str(error), "path": path}],
        }
        try:
            _write_json_atomically(arguments.response, result)
        except OSError:
            pass
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        return 2


if __name__ == "__main__":
    sys.exit(main())

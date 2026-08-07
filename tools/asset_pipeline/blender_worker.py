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
    "create_primitives",
    "validate_model",
    "normalize_model",
    "generate_collision",
    "generate_lods",
    "export_glb",
    "generate_thumbnail",
    "audit_quality",
}
ALLOWED_INPUT_EXTENSIONS = {".blend", ".dae", ".fbx", ".glb", ".gltf", ".obj"}
ALLOWED_ANIMATION_EXTENSIONS = {".fbx"}
ALLOWED_TEXTURE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}
ALLOWED_PIVOTS = {"keep", "ground_center", "object_center"}
HASH_LENGTH = 64
MINIMUM_JOB_SECONDS = 5
MAXIMUM_JOB_SECONDS = 900
MINIMUM_OUTPUT_BYTES = 1024 * 1024
MAXIMUM_OUTPUT_BYTES = 2 * 1024 * 1024 * 1024


class WorkerError(Exception):
    def __init__(self, code: str, message: str, path: str = "") -> None:
        super().__init__(message)
        self.code = code
        self.path = path


def _default_security_policy() -> dict:
    return {
        "schemaVersion": 1,
        "id": "a1230000-0000-4000-8000-000000000001",
        "denyFreeFormPython": True,
        "denyShell": True,
        "denyEval": True,
        "requireContainedPaths": True,
        "networkAllowed": False,
        "maximumJobSeconds": 300,
        "maximumOutputBytes": 512 * 1024 * 1024,
        "allowedBlenderOperations": sorted(ALLOWED_OPERATIONS),
        "requiredPermission": "configure",
        "provenance": "openrpg-forge:g12-l:secure-worker-default",
    }


def _validate_security_policy(value: object) -> dict:
    if value is None:
        return _default_security_policy()
    if not isinstance(value, dict):
        raise WorkerError("asset.worker.security_policy_invalid", "securityPolicy must be an object.", "securityPolicy")
    expected = set(_default_security_policy())
    if set(value) != expected:
        raise WorkerError(
            "asset.worker.security_policy_invalid",
            "securityPolicy fields must match the versioned contract exactly.",
            "securityPolicy",
        )
    if value.get("schemaVersion") != 1:
        raise WorkerError("asset.worker.security_schema_unsupported", "Unsupported security policy schema.",
                          "securityPolicy.schemaVersion")
    try:
        policy_id = str(uuid.UUID(str(value.get("id", ""))))
    except ValueError as error:
        raise WorkerError("asset.worker.security_id_invalid", "Security policy ID must be a UUID.",
                          "securityPolicy.id") from error
    safety_flags = ("denyFreeFormPython", "denyShell", "denyEval", "requireContainedPaths")
    if any(value.get(flag) is not True for flag in safety_flags):
        raise WorkerError("asset.worker.unsafe_execution_forbidden",
                          "Free-form Python, shell, eval and unconfined paths must remain forbidden.",
                          "securityPolicy.execution")
    if value.get("networkAllowed") is not False:
        raise WorkerError("asset.worker.network_forbidden", "Blender worker network access is forbidden.",
                          "securityPolicy.networkAllowed")
    timeout = value.get("maximumJobSeconds")
    if isinstance(timeout, bool) or not isinstance(timeout, int) or not MINIMUM_JOB_SECONDS <= timeout <= MAXIMUM_JOB_SECONDS:
        raise WorkerError("asset.worker.timeout_invalid", "Worker timeout is outside the safe bounds.",
                          "securityPolicy.maximumJobSeconds")
    output_limit = value.get("maximumOutputBytes")
    if (isinstance(output_limit, bool) or not isinstance(output_limit, int)
            or not MINIMUM_OUTPUT_BYTES <= output_limit <= MAXIMUM_OUTPUT_BYTES):
        raise WorkerError("asset.worker.output_limit_invalid", "Worker output limit is outside the safe bounds.",
                          "securityPolicy.maximumOutputBytes")
    allowed = value.get("allowedBlenderOperations")
    if (not isinstance(allowed, list) or not allowed or len(set(allowed)) != len(allowed)
            or any(not isinstance(item, str) or item not in ALLOWED_OPERATIONS for item in allowed)
            or "validate_model" not in allowed or "export_glb" not in allowed):
        raise WorkerError("asset.worker.security_operations_invalid",
                          "Security policy must contain unique typed operations plus validation and GLB export.",
                          "securityPolicy.allowedBlenderOperations")
    permission = value.get("requiredPermission")
    if permission not in {"configure", "applyApprovedPlan"}:
        raise WorkerError("asset.worker.security_permission_invalid", "Security changes require Configure permission.",
                          "securityPolicy.requiredPermission")
    provenance = value.get("provenance")
    if not isinstance(provenance, str) or not provenance.strip() or len(provenance) > 512:
        raise WorkerError("asset.worker.security_provenance_missing", "Security policy provenance is required.",
                          "securityPolicy.provenance")
    return {
        **value,
        "id": policy_id,
        "allowedBlenderOperations": list(allowed),
        "provenance": provenance.strip(),
    }


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


def _number(value: object, field: str, minimum: float, maximum: float) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not minimum <= float(value) <= maximum:
        raise WorkerError("asset.worker.creation_recipe_invalid", f"{field} is outside its bounded range.", field)
    return float(value)


def _safe_name(value: object, field: str) -> str:
    normalized = str(value or "").strip()
    if not normalized or len(normalized) > 64 or any(
        not (character.isascii() and (character.isalnum() or character in "-_")) for character in normalized
    ):
        raise WorkerError("asset.worker.creation_recipe_invalid", f"{field} must be a safe name.", field)
    return normalized


def _vector(value: object, field: str, minimum: float, maximum: float) -> dict:
    if not isinstance(value, dict) or set(value) != {"x", "y", "z"}:
        raise WorkerError("asset.worker.creation_recipe_invalid", f"{field} must be a typed vector.", field)
    return {axis: _number(value[axis], f"{field}.{axis}", minimum, maximum) for axis in ("x", "y", "z")}


def _validate_creation_recipe(value: object, output_relative: str) -> dict:
    required = {
        "schemaVersion", "id", "slug", "displayName", "provenance", "licenseSpdx", "primitives",
        "material", "uvProjection", "lodRatios", "generateCollision", "outputRelativePath",
    }
    if not isinstance(value, dict) or set(value) != required or value.get("schemaVersion") != SCHEMA_VERSION:
        raise WorkerError("asset.worker.creation_recipe_invalid", "Creation recipe fields or schema are invalid.",
                          "options.creationRecipe")
    try:
        recipe_id = str(uuid.UUID(str(value.get("id", ""))))
    except ValueError as error:
        raise WorkerError("asset.worker.creation_recipe_invalid", "Creation recipe ID must be a UUID.",
                          "options.creationRecipe.id") from error
    slug = _safe_name(value.get("slug"), "options.creationRecipe.slug")
    display_name = str(value.get("displayName", "")).strip()
    provenance = str(value.get("provenance", "")).strip()
    if not display_name or len(display_name) > 128 or not provenance or value.get("licenseSpdx") != "CC0-1.0":
        raise WorkerError("asset.worker.creation_recipe_invalid", "Creation governance and display name are invalid.",
                          "options.creationRecipe.governance")
    if value.get("outputRelativePath") != output_relative:
        raise WorkerError("asset.worker.creation_recipe_invalid", "Creation recipe output does not match request.",
                          "options.creationRecipe.outputRelativePath")
    primitives = value.get("primitives")
    if not isinstance(primitives, list) or not 1 <= len(primitives) <= 64:
        raise WorkerError("asset.worker.creation_recipe_invalid", "Creation requires one to 64 primitives.",
                          "options.creationRecipe.primitives")
    normalized_primitives = []
    ids, names = set(), set()
    for index, primitive in enumerate(primitives):
        root = f"options.creationRecipe.primitives[{index}]"
        fields = {"id", "name", "kind", "positionMeters", "rotationDegrees", "scaleMeters"}
        if not isinstance(primitive, dict) or set(primitive) != fields:
            raise WorkerError("asset.worker.creation_recipe_invalid", "Primitive fields are invalid.", root)
        try:
            primitive_id = str(uuid.UUID(str(primitive.get("id", ""))))
        except ValueError as error:
            raise WorkerError("asset.worker.creation_recipe_invalid", "Primitive ID must be a UUID.", f"{root}.id") from error
        name = _safe_name(primitive.get("name"), f"{root}.name")
        kind = str(primitive.get("kind", "")).strip().lower()
        if primitive_id in ids or name.lower() in names or kind not in {"cube", "cylinder", "sphere", "cone"}:
            raise WorkerError("asset.worker.creation_recipe_invalid", "Primitive identity or kind is invalid.", root)
        ids.add(primitive_id)
        names.add(name.lower())
        normalized_primitives.append({
            "id": primitive_id,
            "name": name,
            "kind": kind,
            "positionMeters": _vector(primitive["positionMeters"], f"{root}.positionMeters", -1000, 1000),
            "rotationDegrees": _vector(primitive["rotationDegrees"], f"{root}.rotationDegrees", -360, 360),
            "scaleMeters": _vector(primitive["scaleMeters"], f"{root}.scaleMeters", 0.001, 1000),
        })
    material = value.get("material")
    if not isinstance(material, dict) or set(material) != {"name", "baseColor", "metallic", "roughness"}:
        raise WorkerError("asset.worker.creation_recipe_invalid", "Material fields are invalid.",
                          "options.creationRecipe.material")
    color = material.get("baseColor")
    if not isinstance(color, dict) or set(color) != {"red", "green", "blue", "alpha"}:
        raise WorkerError("asset.worker.creation_recipe_invalid", "Material color is invalid.",
                          "options.creationRecipe.material.baseColor")
    normalized_material = {
        "name": _safe_name(material.get("name"), "options.creationRecipe.material.name"),
        "baseColor": {channel: _number(color[channel], f"options.creationRecipe.material.baseColor.{channel}", 0, 1)
                      for channel in ("red", "green", "blue", "alpha")},
        "metallic": _number(material.get("metallic"), "options.creationRecipe.material.metallic", 0, 1),
        "roughness": _number(material.get("roughness"), "options.creationRecipe.material.roughness", 0, 1),
    }
    uv = str(value.get("uvProjection", "")).strip().lower()
    if uv not in {"generated", "cube", "cylinder", "sphere"}:
        raise WorkerError("asset.worker.creation_recipe_invalid", "UV projection is invalid.",
                          "options.creationRecipe.uvProjection")
    lods = value.get("lodRatios")
    if not isinstance(lods, list) or not 1 <= len(lods) <= 4:
        raise WorkerError("asset.worker.creation_recipe_invalid", "Creation LOD ratios are invalid.",
                          "options.creationRecipe.lodRatios")
    normalized_lods = sorted({_number(item, "options.creationRecipe.lodRatios", 0.01, 0.99) for item in lods},
                             reverse=True)
    if len(normalized_lods) != len(lods) or not isinstance(value.get("generateCollision"), bool):
        raise WorkerError("asset.worker.creation_recipe_invalid", "Creation LOD or collision policy is invalid.",
                          "options.creationRecipe")
    return {
        "schemaVersion": SCHEMA_VERSION,
        "id": recipe_id,
        "slug": slug,
        "displayName": display_name,
        "provenance": provenance,
        "licenseSpdx": "CC0-1.0",
        "primitives": normalized_primitives,
        "material": normalized_material,
        "uvProjection": uv,
        "lodRatios": normalized_lods,
        "generateCollision": value["generateCollision"],
        "outputRelativePath": output_relative,
    }


def validate_request(document: object, source_root: pathlib.Path, workspace_root: pathlib.Path) -> dict:
    if not isinstance(document, dict):
        raise WorkerError("asset.worker.request_invalid", "Worker request must be a JSON object.")
    if document.get("schemaVersion") != SCHEMA_VERSION:
        raise WorkerError("asset.worker.schema_unsupported", "Unsupported worker request schema.", "schemaVersion")
    allowed_fields = {
        "schemaVersion", "jobId", "assetId", "sourceRelativePath", "outputRelativePath", "sourceSha256",
        "licenseSpdx", "provenance", "operations", "options", "securityPolicy",
    }
    unknown_fields = set(document) - allowed_fields
    if unknown_fields:
        raise WorkerError("asset.worker.request_field_forbidden",
                          f"Unknown worker request field: {sorted(unknown_fields)[0]}.", "request")
    security_policy = _validate_security_policy(document.get("securityPolicy"))
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
        if name not in security_policy["allowedBlenderOperations"]:
            raise WorkerError("asset.worker.operation_disabled",
                              f"Operation '{name}' is disabled by the canonical security policy.", "operations")
        if name not in normalized_operations:
            normalized_operations.append(name)

    source_relative = str(document.get("sourceRelativePath", ""))
    output_relative = str(document.get("outputRelativePath", ""))
    source_path = _contained(source_root, source_relative)
    output_path = _contained(workspace_root, output_relative, output=True)
    creation_mode = "create_primitives" in normalized_operations
    allowed_extension = source_path.suffix.lower() == ".json" if creation_mode else source_path.suffix.lower() in ALLOWED_INPUT_EXTENSIONS
    if not allowed_extension:
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
        "animationSources",
        "materialTextures",
        "retargetProfile",
        "creationRecipe",
        "includedMeshNames",
        "baseDecimationRatio",
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
    included_mesh_names = options.get("includedMeshNames")
    if included_mesh_names is None:
        included_mesh_names = []
    if (
        not isinstance(included_mesh_names, list)
        or len(included_mesh_names) > 16
        or any(
            not isinstance(name, str)
            or not name
            or len(name) > 64
            or any(not (character.isascii() and (character.isalnum() or character in "-_")) for character in name)
            for name in included_mesh_names
        )
        or len(set(included_mesh_names)) != len(included_mesh_names)
    ):
        raise WorkerError(
            "asset.worker.mesh_selection_invalid",
            "includedMeshNames must contain at most 16 unique safe mesh names.",
            "options.includedMeshNames",
        )
    base_decimation_ratio = options.get("baseDecimationRatio", 1.0)
    if (
        isinstance(base_decimation_ratio, bool)
        or not isinstance(base_decimation_ratio, (int, float))
        or not 0.001 <= float(base_decimation_ratio) <= 1.0
    ):
        raise WorkerError(
            "asset.worker.base_decimation_invalid",
            "baseDecimationRatio must be between 0.001 and 1.",
            "options.baseDecimationRatio",
        )
    creation_recipe = options.get("creationRecipe")
    if creation_mode:
        creation_recipe = _validate_creation_recipe(creation_recipe, output_relative)
    elif creation_recipe is not None:
        raise WorkerError("asset.worker.creation_recipe_unused", "Creation recipe requires create_primitives.",
                          "options.creationRecipe")
    animation_sources = options.get("animationSources", [])
    if not isinstance(animation_sources, list):
        raise WorkerError("asset.worker.animation_sources_invalid", "animationSources must be a list.", "options.animationSources")
    normalized_animations = []
    animation_names = set()
    for index, source in enumerate(animation_sources):
        root = f"options.animationSources[{index}]"
        if not isinstance(source, dict) or set(source) != {"name", "relativePath", "contentSha256"}:
            raise WorkerError("asset.worker.animation_source_invalid", "Animation source fields are invalid.", root)
        name = str(source.get("name", "")).strip().lower()
        if (
            not name
            or len(name) > 64
            or any(not (character.isascii() and (character.isalnum() or character in "-_")) for character in name)
            or name in animation_names
        ):
            raise WorkerError("asset.worker.animation_name_invalid", "Animation names must be unique safe identifiers.", f"{root}.name")
        relative = str(source.get("relativePath", ""))
        absolute = _contained(source_root, relative)
        if absolute.suffix.lower() not in ALLOWED_ANIMATION_EXTENSIONS:
            raise WorkerError("asset.worker.animation_format_invalid", "Animation source must be an FBX file.", relative)
        animation_names.add(name)
        normalized_animations.append({
            "name": name,
            "relativePath": relative.replace("\\", "/"),
            "contentSha256": _validate_hash(source.get("contentSha256"), f"{root}.contentSha256"),
            "_absolutePath": str(absolute),
        })

    retarget_profile = options.get("retargetProfile")
    normalized_retarget = None
    if retarget_profile is not None:
        required_profile_fields = {
            "profileId",
            "sourceRig",
            "targetRig",
            "sourceBoneCount",
            "targetBoneCount",
            "rootMotionTarget",
            "minimumMappedBoneRatio",
            "maximumBoundsExpansion",
            "bones",
        }
        if not isinstance(retarget_profile, dict) or set(retarget_profile) != required_profile_fields:
            raise WorkerError(
                "asset.worker.retarget_profile_invalid",
                "retargetProfile fields are invalid.",
                "options.retargetProfile",
            )

        def safe_rig_identifier(value: object, field: str) -> str:
            normalized = str(value or "").strip()
            if (
                not normalized
                or len(normalized) > 96
                or any(not (character.isascii() and (character.isalnum() or character in "-_.")) for character in normalized)
            ):
                raise WorkerError(
                    "asset.worker.retarget_identifier_invalid",
                    f"{field} must be a safe identifier.",
                    field,
                )
            return normalized

        source_bone_count = retarget_profile.get("sourceBoneCount")
        target_bone_count = retarget_profile.get("targetBoneCount")
        if (
            isinstance(source_bone_count, bool)
            or not isinstance(source_bone_count, int)
            or not 1 <= source_bone_count <= 1024
            or isinstance(target_bone_count, bool)
            or not isinstance(target_bone_count, int)
            or not 1 <= target_bone_count <= 1024
        ):
            raise WorkerError(
                "asset.worker.retarget_bone_count_invalid",
                "Retarget rig bone counts must be integers in [1, 1024].",
                "options.retargetProfile",
            )
        minimum_ratio = retarget_profile.get("minimumMappedBoneRatio")
        maximum_expansion = retarget_profile.get("maximumBoundsExpansion")
        if (
            isinstance(minimum_ratio, bool)
            or not isinstance(minimum_ratio, (int, float))
            or not 0.1 <= float(minimum_ratio) <= 1.0
            or isinstance(maximum_expansion, bool)
            or not isinstance(maximum_expansion, (int, float))
            or not 1.0 <= float(maximum_expansion) <= 10.0
        ):
            raise WorkerError(
                "asset.worker.retarget_threshold_invalid",
                "Retarget audit thresholds are outside policy.",
                "options.retargetProfile",
            )
        bones = retarget_profile.get("bones")
        if not isinstance(bones, dict) or len(bones) < 12:
            raise WorkerError(
                "asset.worker.retarget_map_invalid",
                "Retarget maps must contain at least twelve governed bone pairs.",
                "options.retargetProfile.bones",
            )
        normalized_bones = {}
        source_names = set()
        for target_name, source_name in bones.items():
            normalized_target = safe_rig_identifier(target_name, "options.retargetProfile.bones")
            normalized_source = safe_rig_identifier(source_name, f"options.retargetProfile.bones.{normalized_target}")
            if normalized_target in normalized_bones or normalized_source in source_names:
                raise WorkerError(
                    "asset.worker.retarget_map_invalid",
                    "Retarget bone pairs must be one-to-one.",
                    "options.retargetProfile.bones",
                )
            normalized_bones[normalized_target] = normalized_source
            source_names.add(normalized_source)
        root_motion_target = safe_rig_identifier(
            retarget_profile.get("rootMotionTarget"),
            "options.retargetProfile.rootMotionTarget",
        )
        if root_motion_target not in normalized_bones:
            raise WorkerError(
                "asset.worker.retarget_map_invalid",
                "rootMotionTarget must identify a mapped target bone.",
                "options.retargetProfile.rootMotionTarget",
            )
        if not animation_sources:
            raise WorkerError(
                "asset.worker.retarget_profile_unused",
                "retargetProfile requires at least one animation source.",
                "options.retargetProfile",
            )
        normalized_retarget = {
            "profileId": safe_rig_identifier(retarget_profile.get("profileId"), "options.retargetProfile.profileId"),
            "sourceRig": safe_rig_identifier(retarget_profile.get("sourceRig"), "options.retargetProfile.sourceRig"),
            "targetRig": safe_rig_identifier(retarget_profile.get("targetRig"), "options.retargetProfile.targetRig"),
            "sourceBoneCount": source_bone_count,
            "targetBoneCount": target_bone_count,
            "rootMotionTarget": root_motion_target,
            "minimumMappedBoneRatio": float(minimum_ratio),
            "maximumBoundsExpansion": float(maximum_expansion),
            "bones": dict(sorted(normalized_bones.items())),
        }

    material_textures = options.get("materialTextures", [])
    if not isinstance(material_textures, list):
        raise WorkerError("asset.worker.material_textures_invalid", "materialTextures must be a list.", "options.materialTextures")
    normalized_textures = []
    texture_bindings = set()
    for index, source in enumerate(material_textures):
        root = f"options.materialTextures[{index}]"
        if not isinstance(source, dict) or set(source) != {"material", "channel", "relativePath", "contentSha256"}:
            raise WorkerError("asset.worker.material_texture_invalid", "Material texture fields are invalid.", root)
        material = str(source.get("material", "")).strip()
        channel = str(source.get("channel", "")).strip().lower()
        if (
            not material
            or len(material) > 64
            or any(not (character.isascii() and (character.isalnum() or character in "-_")) for character in material)
            or channel != "base_color"
            or (material.lower(), channel) in texture_bindings
        ):
            raise WorkerError("asset.worker.material_binding_invalid", "Only one safe base_color binding per material is supported.", root)
        relative = str(source.get("relativePath", ""))
        absolute = _contained(source_root, relative)
        if absolute.suffix.lower() not in ALLOWED_TEXTURE_EXTENSIONS:
            raise WorkerError("asset.worker.texture_format_invalid", "Material texture format is unsupported.", relative)
        texture_bindings.add((material.lower(), channel))
        normalized_textures.append({
            "material": material,
            "channel": channel,
            "relativePath": relative.replace("\\", "/"),
            "contentSha256": _validate_hash(source.get("contentSha256"), f"{root}.contentSha256"),
            "_absolutePath": str(absolute),
        })

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
        "securityPolicy": security_policy,
        "options": {
            "applyTransforms": bool(options.get("applyTransforms", True)),
            "scaleMeters": float(scale),
            "pivot": pivot,
            "lodRatios": normalized_lods,
            "generateCollision": bool(options.get("generateCollision", False)),
            "animationSources": normalized_animations,
            "materialTextures": normalized_textures,
            "retargetProfile": normalized_retarget,
            "creationRecipe": creation_recipe,
            "includedMeshNames": included_mesh_names,
            "baseDecimationRatio": float(base_decimation_ratio),
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
    if "create_primitives" in request["operations"]:
        try:
            source_recipe = json.loads(source_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as error:
            raise WorkerError("asset.worker.creation_recipe_invalid", str(error), request["sourceRelativePath"]) from error
        if source_recipe != request["options"]["creationRecipe"]:
            raise WorkerError("asset.worker.creation_recipe_mismatch",
                              "Immutable creation recipe differs from the typed worker options.",
                              request["sourceRelativePath"])
    if not request["assetId"] or not request["licenseSpdx"] or not request["provenance"]:
        raise WorkerError(
            "asset.worker.governance_missing",
            "assetId, licenseSpdx and provenance are required.",
            request["sourceRelativePath"],
        )
    immutable_sources = []
    bridge_options = dict(request["options"])
    bridge_animations = []
    for source in request["options"]["animationSources"]:
        absolute = pathlib.Path(source["_absolutePath"])
        if not absolute.is_file():
            raise WorkerError("asset.worker.animation_source_missing", "Animation source does not exist.", source["relativePath"])
        actual = _sha256(absolute)
        if actual != source["contentSha256"]:
            raise WorkerError("asset.worker.animation_hash_mismatch", "Animation source hash differs from the request.", source["relativePath"])
        immutable_sources.append((absolute, actual, source["relativePath"]))
        bridge_animations.append({"name": source["name"], "path": str(absolute)})
    bridge_textures = []
    for source in request["options"]["materialTextures"]:
        absolute = pathlib.Path(source["_absolutePath"])
        if not absolute.is_file():
            raise WorkerError("asset.worker.texture_source_missing", "Material texture does not exist.", source["relativePath"])
        actual = _sha256(absolute)
        if actual != source["contentSha256"]:
            raise WorkerError("asset.worker.texture_hash_mismatch", "Material texture hash differs from the request.", source["relativePath"])
        immutable_sources.append((absolute, actual, source["relativePath"]))
        bridge_textures.append({
            "material": source["material"],
            "channel": source["channel"],
            "path": str(absolute),
        })
    bridge_options["animationSources"] = bridge_animations
    bridge_options["materialTextures"] = bridge_textures
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
        _encode_bridge_payload(bridge_options),
    ]
    try:
        completed = subprocess.run(
            command,
            capture_output=True,
            text=True,
            timeout=request["securityPolicy"]["maximumJobSeconds"],
            check=False,
        )
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
        if temporary_output_path.stat().st_size > request["securityPolicy"]["maximumOutputBytes"]:
            raise WorkerError("asset.worker.output_too_large",
                              "Blender output exceeds the canonical security policy limit.",
                              request["outputRelativePath"])
        if _sha256(source_path) != source_hash_before:
            raise WorkerError("asset.worker.source_modified", "External worker modified the immutable source.", request["sourceRelativePath"])
        for immutable_path, immutable_hash, immutable_relative in immutable_sources:
            if _sha256(immutable_path) != immutable_hash:
                raise WorkerError("asset.worker.source_modified", "External worker modified an immutable companion source.", immutable_relative)

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
    except subprocess.TimeoutExpired as error:
        raise WorkerError(
            "asset.worker.timeout",
            f"Blender exceeded the canonical {request['securityPolicy']['maximumJobSeconds']} second timeout.",
            request["outputRelativePath"],
        ) from error
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

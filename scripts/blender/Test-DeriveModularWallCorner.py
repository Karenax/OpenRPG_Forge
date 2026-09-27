#!/usr/bin/env python3
"""Static contract checks for the governed wall-corner derivation worker.

This deliberately does not import bpy or launch Blender.  It guards the
non-negotiable pin, descriptor contract and explicit miter treatment
before the heavier DCC proof is scheduled.
"""

from __future__ import annotations

import ast
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WORKER = ROOT / "scripts" / "blender" / "Derive-ModularWallCornerFromJoint.py"
PROBE = ROOT / "scripts" / "blender" / "Test-ModularWallCorner.py"
MANIFEST = ROOT / "examples" / "visual_valley" / "asset-kits" / "r2-025-wall-corner" / "asset-kit.v1.json"
PIN = "1be108a0113dcad4ff4fcd4d39de7d38e96967b615106eb52a865231efd9b3b9"


def load_bounds_validator(source: str):
    """Execute only the bpy-free contract constants and validator from worker."""
    tree = ast.parse(source, filename=str(WORKER))
    wanted_assignments = {
        "REQUIRED_LODS",
        "NOMINAL_COLLISION_DIMENSIONS_M",
        "COLLISION_DIMENSION_EPSILON_M",
        "RENDER_LENGTH_EPSILON_M",
        "RENDER_DEPTH_RANGE_M",
        "RENDER_HEIGHT_EPSILON_M",
        "CENTER_ALIGNMENT_EPSILON_M",
    }
    body = [
        node
        for node in tree.body
        if (
            isinstance(node, ast.Assign)
            and any(isinstance(target, ast.Name) and target.id in wanted_assignments for target in node.targets)
        )
        or isinstance(node, ast.FunctionDef)
        and node.name == "validate_source_bounds"
    ]
    module = ast.Module(body=body, type_ignores=[])
    namespace: dict = {}
    exec(compile(ast.fix_missing_locations(module), str(WORKER), "exec"), namespace)
    return namespace["validate_source_bounds"]


def bounds(dimensions: tuple[float, float, float], centre: tuple[float, float, float] = (0.0, 0.0, 1.6)):
    return (
        tuple(centre[index] - dimensions[index] * 0.5 for index in range(3)),
        tuple(centre[index] + dimensions[index] * 0.5 for index in range(3)),
    )


def assert_rejected(validator, render_bounds, collision_bounds, message: str) -> None:
    try:
        validator(render_bounds, collision_bounds)
    except RuntimeError:
        return
    raise AssertionError(message)


def assert_triangle_selector_rejected(selector, vertex_counts, normal_y_values, message: str) -> None:
    try:
        selector(vertex_counts, normal_y_values, "synthetic")
    except RuntimeError:
        return
    raise AssertionError(message)


def main() -> None:
    source = WORKER.read_text(encoding="utf-8")
    ast.parse(source, filename=str(WORKER))
    required = (
        'SOURCE_SHA256 = "' + PIN + '"',
        "forge.blender-authored-source", "derivation-report.json",
        "oriented_world_mesh", "bake_mitered_half", "complementary_45_degree_miter",
        "bmesh.ops.recalc_face_normals", "bmesh.ops.bisect_plane",
        "source_back_halves_removed", "transform.determinant() < 0",
        "validate_source_lod_topology", "validate_derived_triangle_budget",
    )
    missing = [item for item in required if item not in source]
    if missing:
        raise AssertionError(f"derivation worker contract missing: {missing}")
    probe_source = PROBE.read_text(encoding="utf-8")
    probe_required = (
        "compare_interior_facade",
        "interior_limestone_projection_match",
        "source_limestone_hits",
        "INTERIOR_FACADE_CLEARANCE_M = 0.0",
        "type=\"AREA\"",
        "fill.data.color = (1.0, 1.0, 1.0)",
        "obj.matrix_world @ Vector(corner)",
    )
    missing_probe = [item for item in probe_required if item not in probe_source]
    if missing_probe:
        raise AssertionError(f"probe must prove the real interior facade: {missing_probe}")
    validator = load_bounds_validator(source)
    # These are the measured dimensions of the pinned governed source.  The
    # render relief can exceed collision depth; the UCX mesh remains nominal.
    measured_render = {
        0: bounds((4.0, 0.782130, 3.2)),
        1: bounds((4.000679, 0.794251, 3.2)),
        2: bounds((4.0, 0.72, 3.2)),
    }
    measured_collision = bounds((4.0, 0.72, 3.2))
    report = validator(measured_render, measured_collision)
    assert report["collision"]["dimensions_m"] == [4.0, 0.72, 3.2]
    assert report["render_lods"]["LOD0"]["dimensions_m"] == [4.0, 0.782130, 3.2]
    assert report["render_lods"]["LOD1"]["dimensions_m"] == [4.000679, 0.794251, 3.2]
    assert report["render_lods"]["LOD2"]["dimensions_m"] == [4.0, 0.72, 3.2]
    assert_rejected(
        validator,
        {**measured_render, 1: bounds((4.011, 0.794251, 3.2))},
        measured_collision,
        "render length beyond the 1cm bevel allowance must be refused",
    )
    assert_rejected(
        validator,
        {**measured_render, 0: bounds((4.0, 0.821, 3.2))},
        measured_collision,
        "render relief beyond the bounded 0.82m depth must be refused",
    )
    assert_rejected(
        validator,
        {**measured_render, 2: bounds((4.0, 0.72, 3.211))},
        measured_collision,
        "render height beyond the 3.2m contract must be refused",
    )
    assert_rejected(
        validator,
        {**measured_render, 0: bounds((4.0, 0.782130, 3.2), (0.051, 0.0, 1.6))},
        measured_collision,
        "render mesh shifted from its collision centre must be refused",
    )
    assert_rejected(
        validator,
        measured_render,
        bounds((4.0, 0.723, 3.2)),
        "collision depth must remain the nominal 0.72m contract",
    )
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    assert manifest["provenance"]["source_kind"] == "procedural_original_with_authored_edits"
    asset = manifest["assets"]
    assert len(asset) == 1 and asset[0]["id"] == "vv-architecture-wall-corner-a"
    assert asset[0]["collision_boxes"] == 2
    print("PASS: static wall-corner derivation contract and measured bounds policy")


if __name__ == "__main__":
    main()

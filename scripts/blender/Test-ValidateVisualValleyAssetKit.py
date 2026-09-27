#!/usr/bin/env python3
"""Static tests for the export-stable GLB collider topology contract.

The actual validator runs inside Blender.  This focused test extracts only its
bpy-free comparison helper, so an ordinary Python worker can prove that a
source quad collider is accepted after glTF triangulation without relaxing
object, triangle or vertex identity checks.
"""

from __future__ import annotations

import ast
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
VALIDATOR = ROOT / "scripts" / "blender" / "Validate-VisualValleyAssetKit.py"


def load_contract(source: str):
    tree = ast.parse(source, filename=str(VALIDATOR))
    wanted_assignments = {
        "COLLISION_STATISTIC_KEYS",
        "COLLISION_EXPORT_STABLE_KEYS",
        "COLLISION_BOUNDS_EPSILON_M",
    }
    body = [
        node
        for node in tree.body
        if (
            isinstance(node, ast.Assign)
            and any(isinstance(target, ast.Name) and target.id in wanted_assignments for target in node.targets)
        )
        or isinstance(node, ast.FunctionDef)
        and node.name == "validate_export_stable_collision_statistics"
    ]
    namespace: dict = {}
    module = ast.Module(body=body, type_ignores=[])
    exec(compile(ast.fix_missing_locations(module), str(VALIDATOR), "exec"), namespace)
    return namespace["validate_export_stable_collision_statistics"]


def rejected(validator, expected: dict, actual: dict, geometry: dict, message: str) -> None:
    try:
        validator(expected, actual, geometry, asset_id="synthetic")
    except RuntimeError:
        return
    raise AssertionError(message)


def main() -> None:
    source = VALIDATOR.read_text(encoding="utf-8")
    ast.parse(source, filename=str(VALIDATOR))
    validator = load_contract(source)
    source_cuboids = {"objects": 2, "polygons": 12, "triangles": 24, "vertices": 16}
    geometry = {
        "UCX_synthetic_ArmX": {
            "unique_positions": 8,
            "triangles": 12,
            "bounds_min_m": [0.0, 0.0, 0.0],
            "bounds_max_m": [4.0, 0.72, 3.2],
            "dimensions_m": [4.0, 0.72, 3.2],
        },
        "UCX_synthetic_ArmY": {
            "unique_positions": 8,
            "triangles": 12,
            "bounds_min_m": [0.0, 0.0, 0.0],
            "bounds_max_m": [0.72, 4.0, 3.2],
            "dimensions_m": [0.72, 4.0, 3.2],
        },
    }
    imported_glb = {
        "objects": 2,
        "polygons": 24,
        "triangles": 24,
        "vertex_slots": 48,
        "unique_positions": 16,
        "degenerate_triangles": 0,
        "colliders": {
            name: {**record, "vertex_slots": 24}
            for name, record in geometry.items()
        },
    }
    report = validator(source_cuboids, imported_glb, geometry, asset_id="synthetic")
    assert report == {
        "objects": 2,
        "triangles": 24,
        "source_vertices": 16,
        "imported_vertex_slots": 48,
        "imported_unique_positions": 16,
        "polygons_source": 12,
        "polygons_imported": 24,
        "polygon_encoding": "source_faces_exported_as_triangles",
        "unique_position_method": "world_space_quantized_1e-6_m",
        "bounds_epsilon_m": 1e-5,
        "colliders": imported_glb["colliders"],
    }
    rejected(
        validator,
        source_cuboids,
        {**imported_glb, "objects": 1},
        geometry,
        "object count must stay pinned across export",
    )
    rejected(
        validator,
        source_cuboids,
        {**imported_glb, "triangles": 22},
        geometry,
        "triangle count must stay pinned across export",
    )
    rejected(
        validator,
        source_cuboids,
        {**imported_glb, "unique_positions": 15},
        geometry,
        "unique positions must stay pinned across normal-split export",
    )
    rejected(
        validator,
        source_cuboids,
        {**imported_glb, "polygons": 12},
        geometry,
        "imported GLB collider polygons must be triangles",
    )
    rejected(
        validator,
        {"objects": 2, "triangles": 24, "vertices": 16},
        imported_glb,
        geometry,
        "source collision polygon count remains required",
    )
    drifted = {
        **imported_glb,
        "colliders": {
            **imported_glb["colliders"],
            "UCX_synthetic_ArmX": {
                **imported_glb["colliders"]["UCX_synthetic_ArmX"],
                "bounds_max_m": [4.002, 0.72, 3.2],
                "dimensions_m": [4.002, 0.72, 3.2],
            },
        },
    }
    rejected(
        validator,
        source_cuboids,
        drifted,
        geometry,
        "collider AABB drift must be refused even when topology is unchanged",
    )
    rejected(
        validator,
        source_cuboids,
        {**imported_glb, "degenerate_triangles": 1},
        geometry,
        "degenerate collider triangles must be refused",
    )
    print("PASS: export-stable collider topology contract")


if __name__ == "__main__":
    main()

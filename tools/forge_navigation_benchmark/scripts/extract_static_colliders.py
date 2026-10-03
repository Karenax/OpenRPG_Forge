#!/usr/bin/env python3
"""Extract a small, hash-pinned set of Tidewater collision triangles for lab replay.

This script deliberately consumes the same package ``pos``/``indices`` payloads that the
Forge Tidewater scene loader turns into ``Collider::trimesh``.  It does not edit the
authoring source and does not pretend that a metadata-only placement is a collider.
The output is a new canonical report for the isolated navigation laboratory.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
from pathlib import Path
from typing import Any, Iterable


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def f32(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]


def f32_vec(values: Iterable[float]) -> list[float]:
    return [f32(value) for value in values]


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def canonical_json_hash(value: Any) -> str:
    encoded = json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")
    return sha256_bytes(encoded)


def verify_file(package_root: Path, manifest: dict[str, Any], name: str) -> bytes:
    entry = manifest["files"].get(name)
    if entry is None:
        raise ValueError(f"package manifest has no payload {name}")
    path = package_root / name
    data = path.read_bytes()
    if len(data) != entry["bytes"] or sha256_bytes(data) != entry["sha256"]:
        raise ValueError(f"package payload pin mismatch: {name}")
    return data


def verify_descriptor(
    package_root: Path,
    manifest: dict[str, Any],
    asset_hash: str,
) -> tuple[dict[str, Any], str]:
    name = f"{asset_hash}.json"
    data = verify_file(package_root, manifest, name)
    if sha256_bytes(data) != asset_hash:
        raise ValueError(f"descriptor pin mismatch: {name}")
    descriptor = json.loads(data)
    if descriptor.get("schema") != "forge.tidewater.building-asset":
        raise ValueError(f"unsupported building descriptor: {name}")
    return descriptor, name


def quat_rotate(point: list[float], rotation: dict[str, float]) -> list[float]:
    """Match glam/Bevy Quat::mul_vec3 in f32 arithmetic for the Y-up contract."""

    x, y, z = f32_vec(point)
    qx, qy, qz, qw = f32_vec(
        [rotation["x"], rotation["y"], rotation["z"], rotation["w"]]
    )
    uv = [
        f32(f32(qy * z) - f32(qz * y)),
        f32(f32(qz * x) - f32(qx * z)),
        f32(f32(qx * y) - f32(qy * x)),
    ]
    uuv = [
        f32(f32(qy * uv[2]) - f32(qz * uv[1])),
        f32(f32(qz * uv[0]) - f32(qx * uv[2])),
        f32(f32(qx * uv[1]) - f32(qy * uv[0])),
    ]
    return f32_vec(
        [
            f32(x + f32(f32(uv[0] * qw) + uuv[0]) * 2.0),
            f32(y + f32(f32(uv[1] * qw) + uuv[1]) * 2.0),
            f32(z + f32(f32(uv[2] * qw) + uuv[2]) * 2.0),
        ]
    )


def placed_point(
    point: list[float], transform: dict[str, Any],
) -> list[float]:
    scale = transform["scale"]
    scaled = f32_vec(
        [
            point[0] * scale["x"],
            point[1] * scale["y"],
            point[2] * scale["z"],
        ]
    )
    rotated = quat_rotate(scaled, transform["rotation"])
    translation = transform["translation_m"]
    return f32_vec(
        [
            rotated[0] + translation["x"],
            rotated[1] + translation["y"],
            rotated[2] + translation["z"],
        ]
    )


def decode_positions(data: bytes) -> list[list[float]]:
    if len(data) % 12 != 0:
        raise ValueError("position payload is not Float32x3")
    return [list(values) for values in struct.iter_unpack("<fff", data)]


def decode_indices(data: bytes, vertex_count: int) -> list[list[int]]:
    if len(data) % 12 != 0:
        raise ValueError("index payload is not a triangle-aligned u32 stream")
    flat = [index for values in struct.iter_unpack("<III", data) for index in values]
    if any(index >= vertex_count for index in flat):
        raise ValueError("collision index is outside the position payload")
    return [flat[offset : offset + 3] for offset in range(0, len(flat), 3)]


def building_triangles(
    package_root: Path,
    manifest: dict[str, Any],
    asset_hash: str,
    placement: dict[str, Any],
) -> tuple[list[list[float]], list[list[int]], dict[str, Any]]:
    descriptor, descriptor_name = verify_descriptor(package_root, manifest, asset_hash)
    placement_transform = placement.get("transform") or {
        "rotation": placement["rotation"],
        "scale": placement["scale"],
        "translation_m": placement["translation_m"],
    }
    vertices: list[list[float]] = []
    triangles: list[list[int]] = []
    parts: list[dict[str, Any]] = []
    for part_index, part in enumerate(descriptor["building"]["parts"]):
        position_ref = part["attributes"]["pos"]
        index_ref = part["indices"]
        position_bytes = verify_file(package_root, manifest, position_ref["file"])
        index_bytes = verify_file(package_root, manifest, index_ref["file"])
        if sha256_bytes(position_bytes) != position_ref["sha256"]:
            raise ValueError(f"position pin mismatch: {position_ref['file']}")
        if sha256_bytes(index_bytes) != index_ref["sha256"]:
            raise ValueError(f"index pin mismatch: {index_ref['file']}")
        local_positions = decode_positions(position_bytes)
        local_triangles = decode_indices(index_bytes, len(local_positions))
        base = len(vertices)
        vertices.extend(placed_point(position, placement_transform) for position in local_positions)
        triangles.extend(
            [[base + index for index in triangle] for triangle in local_triangles]
        )
        parts.append(
            {
                "part_index": part_index,
                "material": part["material"],
                "position_payload": position_ref["file"],
                "position_sha256": position_ref["sha256"],
                "position_bytes": len(position_bytes),
                "indices_payload": index_ref["file"],
                "indices_sha256": index_ref["sha256"],
                "indices_bytes": len(index_bytes),
                "vertices": len(local_positions),
                "triangles": len(local_triangles),
            }
        )
    return vertices, triangles, {
        "kind": "imported_mesh_collider",
        "asset_hash": asset_hash,
        "asset_id": placement["asset_id"],
        "placement_id": placement["id"],
        "logical_name": placement["name"],
        "transform": placement_transform,
        "descriptor": descriptor_name,
        "descriptor_sha256": asset_hash,
        "source_frame": descriptor["building"].get("source_frame"),
        "parts": parts,
        "vertices": len(vertices),
        "triangles": len(triangles),
        "collider_contract": "Collider::trimesh(local_positions,indices) under placement root transform",
    }


BOX_FACES = (
    (4, 7, 5),
    (5, 7, 6),
    (0, 1, 3),
    (1, 2, 3),
    (0, 4, 1),
    (1, 4, 5),
    (1, 5, 2),
    (2, 5, 6),
    (2, 6, 3),
    (3, 6, 7),
    (3, 7, 0),
    (0, 7, 4),
)


def append_box(
    vertices: list[list[float]],
    triangles: list[list[int]],
    minimum: list[float],
    maximum: list[float],
    transform: dict[str, Any] | None = None,
) -> tuple[int, int]:
    local = [
        [minimum[0], minimum[1], minimum[2]],
        [maximum[0], minimum[1], minimum[2]],
        [maximum[0], minimum[1], maximum[2]],
        [minimum[0], minimum[1], maximum[2]],
        [minimum[0], maximum[1], minimum[2]],
        [maximum[0], maximum[1], minimum[2]],
        [maximum[0], maximum[1], maximum[2]],
        [minimum[0], maximum[1], maximum[2]],
    ]
    base = len(vertices)
    vertices.extend(
        placed_point(point, transform) if transform is not None else f32_vec(point)
        for point in local
    )
    triangles.extend([[base + index for index in face] for face in BOX_FACES])
    return 8, len(BOX_FACES)


def room_boxes(room: dict[str, Any]) -> list[tuple[str, list[float], list[float]]]:
    dimensions = room["dimensions"]
    x = dimensions["width_m"] / 2.0
    z = dimensions["depth_m"] / 2.0
    h = dimensions["clear_height_m"]
    wall = dimensions["wall_m"]
    left = dimensions["door_offset_x_m"] - dimensions["door_width_m"] / 2.0
    right = dimensions["door_offset_x_m"] + dimensions["door_width_m"] / 2.0
    front = -z + wall
    return [
        ("floor", [-x, -dimensions["slab_m"], -z], [x, 0.0, z]),
        ("roof", [-x, h, -z], [x, h + dimensions["slab_m"], z]),
        ("west_wall", [-x, 0.0, -z], [-x + wall, h, z]),
        ("east_wall", [x - wall, 0.0, -z], [x, h, z]),
        ("back_wall", [-x + wall, 0.0, z - wall], [x - wall, h, z]),
        ("entrance_left", [-x + wall, 0.0, -z], [left, h, front]),
        ("entrance_right", [right, 0.0, -z], [x - wall, h, front]),
        (
            "entrance_lintel",
            [left, dimensions["door_height_m"], -z],
            [right, h, front],
        ),
    ]


def transform_from_position(position: dict[str, float]) -> dict[str, Any]:
    return {
        "rotation": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0},
        "scale": {"x": 1.0, "y": 1.0, "z": 1.0},
        "translation_m": position,
    }


def add_box_obstacle(
    vertices: list[list[float]],
    triangles: list[list[int]],
    record: dict[str, Any],
    minimum: list[float],
    maximum: list[float],
    transform: dict[str, Any] | None = None,
) -> dict[str, Any]:
    vertex_count, triangle_count = append_box(
        vertices, triangles, minimum, maximum, transform
    )
    return {
        **record,
        "vertices": vertex_count,
        "triangles": triangle_count,
        "collider_contract": "Collider::cuboid-equivalent box represented as exact triangles",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--terrain-report", type=Path, required=True)
    parser.add_argument("--package-root", type=Path, required=True)
    parser.add_argument("--base009", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument(
        "--include",
        action="append",
        choices=("building11", "building12", "room", "door", "support"),
        help="obstacle sources to append; defaults to every static source",
    )
    args = parser.parse_args()
    included = set(args.include or ("building11", "building12", "room", "door", "support"))

    terrain = read_json(args.terrain_report)
    package_root = args.package_root.resolve()
    manifest = read_json(package_root / "package.json")
    base009 = read_json(args.base009)
    package_bytes = (package_root / "package.json").read_bytes()

    positions = [list(point) for point in terrain["canonical_geometry"]["positions_m"]]
    triangles = [list(triangle) for triangle in terrain["canonical_geometry"]["triangles"]]
    source_obstacles: list[dict[str, Any]] = []
    catalog_by_hash = {
        asset["content_hash"]: asset for asset in read_json(package_root / "catalog.json")["assets"]
    }
    placements = {
        placement["name"].rsplit(":", 1)[-1]: placement
        for placement in base009["static_building_placements"]
    }

    for building_number in ("11", "12"):
        if f"building{building_number}" not in included:
            continue
        placement = placements.get(building_number)
        if placement is None:
            raise ValueError(f"base009 has no building:{building_number} placement")
        catalog_asset = catalog_by_hash.get(placement["asset_hash"])
        if catalog_asset is None:
            raise ValueError(f"package catalog has no asset {placement['asset_hash']}")
        placement = {**placement, "asset_id": catalog_asset["id"]}
        local_positions, local_triangles, record = building_triangles(
            package_root, manifest, placement["asset_hash"], placement
        )
        base = len(positions)
        positions.extend(local_positions)
        triangles.extend(
            [[base + index for index in triangle] for triangle in local_triangles]
        )
        source_obstacles.append(record)

    room = base009["building_room_correction"]
    if "room" in included:
        room_origin = {
            "x": room["origin_m"]["x"],
            "y": room["origin_m"]["y"],
            "z": room["origin_m"]["z"],
        }
        room_transform = transform_from_position(room_origin)
        room_start = len(positions)
        room_parts = []
        for role, minimum, maximum in room_boxes(room):
            room_parts.append(
                add_box_obstacle(
                    positions,
                    triangles,
                    {
                        "kind": "procedural_room_collider",
                        "role": role,
                        "source_id": room["id"],
                        "origin_m": room["origin_m"],
                        "dimensions": room["dimensions"],
                    },
                    minimum,
                    maximum,
                    room_transform,
                )
            )
        source_obstacles.append(
            {
                "kind": "procedural_room_collider",
                "source_id": room["id"],
                "name": room["name"],
                "origin_m": room["origin_m"],
                "dimensions": room["dimensions"],
                "part_count": len(room_parts),
                "parts": room_parts,
                "vertices": len(positions) - room_start,
                "triangles": len(triangles)
                - (len(terrain["canonical_geometry"]["triangles"])
                   + sum(item["triangles"] for item in source_obstacles)),
                "collider_contract": "forge_world::building_layout::RoomLayout exact local boxes under origin",
            }
        )

    for key, label in (("authored_door", "door"), ("authored_support", "support")):
        if label not in included:
            continue
        object_record = base009[key]
        half = object_record["physics"]["body_half_extents_m"]
        position = object_record["position"]
        source_obstacles.append(
            add_box_obstacle(
                positions,
                triangles,
                {
                    "kind": f"authored_{label}_collider",
                    "source_id": object_record["id"],
                    "name": object_record["name"],
                    "position_m": position,
                    "body_half_extents_m": half,
                    "mass_kg": object_record["physics"]["mass_kg"],
                    "initially_open": object_record.get("kind", {}).get("initially_open"),
                    "supports": object_record["physics"]["supports"],
                },
                [
                    -half["x"],
                    -half["y"],
                    -half["z"],
                ],
                [half["x"], half["y"], half["z"]],
                transform_from_position(position),
            )
        )

    charge = base009["authored_charge"]
    geometry_pair = [positions, triangles]
    combined_hash = canonical_json_hash(geometry_pair)
    original_geometry = terrain["canonical_geometry"]
    obstacle_vertex_count = len(positions) - len(original_geometry["positions_m"])
    obstacle_triangle_count = len(triangles) - len(original_geometry["triangles"])
    report = {
        "schema": "forge.navigation.canonical-terrain-with-static-colliders",
        "version": 1,
        "area_id": terrain["area_id"],
        "source_hash": terrain["source_hash"],
        "terrain_hash": terrain["terrain_hash"],
        "raster_hash": terrain["raster_hash"],
        "geometry_hash": combined_hash,
        "crop_xz_m": terrain["crop_xz_m"],
        "canonical_geometry": {
            "positions_m": positions,
            "triangles": triangles,
            "geometry_hash": combined_hash,
            "source_hash": terrain["source_hash"],
            "raster_hash": terrain["raster_hash"],
            "terrain_hash": terrain["terrain_hash"],
            "crop_xz_m": terrain["crop_xz_m"],
        },
        "start_m": terrain["start_m"],
        "end_m": terrain["end_m"],
        "terrain_geometry": {
            "geometry_hash": original_geometry["geometry_hash"],
            "vertices": len(original_geometry["positions_m"]),
            "triangles": len(original_geometry["triangles"]),
        },
        "static_obstacles": {
            "package_manifest": str(package_root / "package.json"),
            "package_manifest_sha256": sha256_bytes(package_bytes),
            "base009_path": str(args.base009.resolve()),
            "base009_sha256": sha256_bytes(args.base009.read_bytes()),
            "source_zone_authoring": base009["source_zone_authoring"],
            "workspace_head": base009["workspace_head"],
            "included": source_obstacles,
            "selected": sorted(included),
            "excluded_dynamic": {
                "source_id": charge["id"],
                "name": charge["name"],
                "position_m": charge["position"],
                "body_half_extents_m": charge["physics"]["body_half_extents_m"],
                "mass_kg": charge["physics"]["mass_kg"],
                "reason": "dynamic gameplay object; not a static navigation collider",
            },
            "vertices": obstacle_vertex_count,
            "triangles": obstacle_triangle_count,
            "geometry_source": "Tidewater pos/indices + forge_world RoomLayout + authored static box colliders",
        },
        "input_contract": {
            "terrain_triangles_preserved_first": len(original_geometry["triangles"]),
            "obstacle_triangles_appended": obstacle_triangle_count,
            "winding_normalization": False,
            "source_transform": "Forge domain_transform_to_bevy (f32 translation/Quat/scale)",
        },
        "selection": {"status": "blocked_pending_full_world_contract"},
        "missing_proofs": [
            "cross-tile Landmass links",
            "native tile cache and partial reconstruction",
            "water exclusions",
            "multi-island seams",
            "dynamic obstacle insertion/removal runtime API",
            "dependency governance and ADR acceptance",
            "human browser acceptance",
        ],
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

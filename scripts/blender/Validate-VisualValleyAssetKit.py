#!/usr/bin/env python3
"""Validate and compare governed R1-063 Blender asset-kit builds.

The validator verifies recorded hashes, reimports every GLB, checks the authored LOD/collision/PBR
contract and compares decoded PNG pixels across two independent builds. Texture PNG hashes are
strict; Blender files and rendered PNG container hashes stay informative because their metadata may
vary even when decoded pixels remain inside the localized GPU stability contract.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

import bpy
import numpy as np


EXPECTED_SCHEMA = "forge.blender-asset-kit-build"
EXPECTED_SCHEMA_VERSION = 1
RECORDED_BLENDER_VERSION_PATTERN = re.compile(
    r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:\.(0|[1-9][0-9]*))?(?:\s+.*)?$"
)
PIXEL_EPSILON = 1e-7
MAX_PIXEL_DIFFERENCE = (8.0 / 255.0) + PIXEL_EPSILON
MAX_CHANGED_CHANNELS_PER_IMAGE = 64
MAX_CHANGED_PIXELS_PER_IMAGE = 24
MAX_MEAN_ABSOLUTE_DIFFERENCE = 1e-7
COLLISION_STATISTIC_KEYS = ("objects", "polygons", "triangles", "vertices")
COLLISION_EXPORT_STABLE_KEYS = ("objects", "triangles")
COLLISION_POSITION_QUANTIZATION_M = 1e-6
COLLISION_BOUNDS_EPSILON_M = 1e-5
MIN_COLLISION_TRIANGLE_AREA_M2 = 1e-10


def parse_arguments() -> argparse.Namespace:
    separator = sys.argv.index("--") if "--" in sys.argv else len(sys.argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--primary-dir", type=Path, required=True)
    parser.add_argument("--rebuild-dir", type=Path, required=True)
    parser.add_argument("--output-report", type=Path, required=True)
    return parser.parse_args(sys.argv[separator + 1 :])


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def recorded_blender_major_minor(value: object, *, build: str) -> tuple[int, int]:
    if not isinstance(value, str):
        raise RuntimeError(f"{build} build has no recorded generator Blender version")
    match = RECORDED_BLENDER_VERSION_PATTERN.fullmatch(value)
    if match is None:
        raise RuntimeError(f"{build} build has invalid recorded generator Blender version: {value!r}")
    return int(match.group(1)), int(match.group(2))


def require_running_blender_major_minor(expected: tuple[int, int]) -> None:
    actual = tuple(bpy.app.version[:2])
    if actual != expected:
        required = ".".join(str(part) for part in expected)
        raise RuntimeError(f"validator requires Blender {required}.x, found {bpy.app.version_string}")


def load_build(path: Path) -> tuple[Path, dict]:
    directory = path.resolve()
    report_path = directory / "asset-kit-build.v1.json"
    if not report_path.is_file():
        raise RuntimeError(f"missing asset-kit build report: {report_path}")
    report = json.loads(report_path.read_text(encoding="utf-8"))
    if report.get("schema") != EXPECTED_SCHEMA or report.get("schema_version") != EXPECTED_SCHEMA_VERSION:
        raise RuntimeError(f"unexpected asset-kit build schema: {report_path}")
    if report.get("qualification", {}).get("r1_063_complete") is not False:
        raise RuntimeError("candidate report must not claim R1-063 completion")
    return directory, report


def verify_recorded_file(root: Path, record: dict) -> Path:
    path = root / record["path"]
    if not path.is_file():
        raise RuntimeError(f"recorded artifact missing: {path}")
    actual_hash = sha256(path)
    if actual_hash != record["sha256"]:
        raise RuntimeError(f"recorded SHA-256 mismatch: {path}")
    if path.stat().st_size != record["bytes"]:
        raise RuntimeError(f"recorded byte length mismatch: {path}")
    return path


def clear_import_state() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def imported_triangle_count(objects: list[bpy.types.Object]) -> int:
    return sum(
        sum(max(0, len(polygon.vertices) - 2) for polygon in obj.data.polygons)
        for obj in objects
        if obj.type == "MESH"
    )


def expected_corner_collision_geometry(asset: dict, collision_names: list[str]) -> dict[str, dict]:
    """Return the authored compound-box geometry for the governed corner."""
    if asset.get("generator_profile") != "limestone_wall_corner":
        raise RuntimeError(f"no export-stable collision geometry contract for {asset['id']}")
    dimensions = asset.get("dimensions_m")
    if (
        not isinstance(dimensions, list)
        or len(dimensions) != 3
        or any(isinstance(value, bool) or not isinstance(value, (int, float)) or value <= 0 for value in dimensions)
    ):
        raise RuntimeError(f"corner collision dimensions are invalid for {asset['id']}")
    width, depth, height = (float(value) for value in dimensions)
    thickness = 0.72
    expected = {
        f"UCX_{asset['id']}_ArmX": {
            "unique_positions": 8,
            "triangles": 12,
            "bounds_min_m": [0.0, 0.0, 0.0],
            "bounds_max_m": [width, thickness, height],
            "dimensions_m": [width, thickness, height],
        },
        f"UCX_{asset['id']}_ArmY": {
            "unique_positions": 8,
            "triangles": 12,
            "bounds_min_m": [0.0, 0.0, 0.0],
            "bounds_max_m": [thickness, depth, height],
            "dimensions_m": [thickness, depth, height],
        },
    }
    if set(collision_names) != set(expected):
        raise RuntimeError(f"corner collision names do not match its compound-box contract for {asset['id']}")
    return expected


def quantized_world_position(value: object) -> tuple[int, int, int]:
    """Make a deterministic 1 micrometre key for glTF split vertex slots."""
    return tuple(int(round(float(value[index]) / COLLISION_POSITION_QUANTIZATION_M)) for index in range(3))


def collision_geometry_statistics(objects: list[bpy.types.Object]) -> dict:
    """Measure world-space collider geometry after glTF import.

    glTF can split one source vertex into several slots for normal/UV seams.
    The physical collision mesh is instead its quantized world-space positions,
    triangle area and bounds.  One micrometre is far tighter than the 10µm
    AABB tolerance and only absorbs IEEE float transport noise.
    """
    colliders: dict[str, dict] = {}
    vertex_slots = 0
    unique_positions = 0
    triangles = 0
    degenerate_triangles = 0
    for obj in sorted(objects, key=lambda item: item.name):
        positions = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        if not positions:
            raise RuntimeError(f"GLB collision object has no vertices: {obj.name}")
        quantized_positions = [quantized_world_position(position) for position in positions]
        unique = set(quantized_positions)
        obj.data.calc_loop_triangles()
        object_triangles = len(obj.data.loop_triangles)
        for triangle in obj.data.loop_triangles:
            first, second, third = (positions[index] for index in triangle.vertices)
            if (
                quantized_world_position(first) == quantized_world_position(second)
                or quantized_world_position(second) == quantized_world_position(third)
                or quantized_world_position(first) == quantized_world_position(third)
                or (second - first).cross(third - first).length_squared <= MIN_COLLISION_TRIANGLE_AREA_M2 * 4.0
            ):
                degenerate_triangles += 1
        lower = [min(float(position[index]) for position in positions) for index in range(3)]
        upper = [max(float(position[index]) for position in positions) for index in range(3)]
        colliders[obj.name] = {
            "vertex_slots": len(positions),
            "unique_positions": len(unique),
            "triangles": object_triangles,
            "bounds_min_m": lower,
            "bounds_max_m": upper,
            "dimensions_m": [upper[index] - lower[index] for index in range(3)],
        }
        vertex_slots += len(positions)
        unique_positions += len(unique)
        triangles += object_triangles
    return {
        "vertex_slots": vertex_slots,
        "unique_positions": unique_positions,
        "triangles": triangles,
        "degenerate_triangles": degenerate_triangles,
        "colliders": colliders,
    }


def validate_export_stable_collision_statistics(expected: dict, actual: dict, expected_geometry: dict, *, asset_id: str) -> dict:
    """Compare collider topology across Blender source and triangulated glTF.

    Blender's generated cuboids record six quad polygons per object.  glTF
    export/import represents those same faces as triangles and may split a
    source vertex into normal-specific slots.  Polygon and raw slot counts are
    encoding-specific; object identity, triangles, unique positions and AABBs
    remain the invariant collision contract.
    """
    if not isinstance(expected, dict) or set(expected) != set(COLLISION_STATISTIC_KEYS):
        raise RuntimeError(f"GLB collision statistics are incomplete for {asset_id}")
    actual_keys = {"objects", "polygons", "triangles", "vertex_slots", "unique_positions", "degenerate_triangles", "colliders"}
    if set(actual) != actual_keys:
        raise RuntimeError(f"GLB imported collision statistics are incomplete for {asset_id}")
    for key in COLLISION_STATISTIC_KEYS:
        if isinstance(expected[key], bool) or not isinstance(expected[key], int) or expected[key] < 0:
            raise RuntimeError(f"GLB collision statistic is invalid for {asset_id}: {key}")
    for key in ("objects", "polygons", "triangles", "vertex_slots", "unique_positions", "degenerate_triangles"):
        if isinstance(actual[key], bool) or not isinstance(actual[key], int) or actual[key] < 0:
            raise RuntimeError(f"GLB imported collision statistic is invalid for {asset_id}: {key}")
    mismatches = {
        key: {"expected": expected[key], "actual": actual[key]}
        for key in COLLISION_EXPORT_STABLE_KEYS
        if actual[key] != expected[key]
    }
    if mismatches:
        raise RuntimeError(f"GLB export-stable collision topology mismatch for {asset_id}: {mismatches}")
    if actual["unique_positions"] != expected["vertices"]:
        raise RuntimeError(
            f"GLB collision unique-position mismatch for {asset_id}: "
            f"{actual['unique_positions']} != {expected['vertices']}"
        )
    if actual["vertex_slots"] < actual["unique_positions"]:
        raise RuntimeError(f"GLB collision vertex slots cannot be fewer than unique positions for {asset_id}")
    if actual["degenerate_triangles"] != 0:
        raise RuntimeError(f"GLB collision contains degenerate triangles for {asset_id}")
    if actual["polygons"] != actual["triangles"]:
        raise RuntimeError(
            f"GLB collision polygons must be triangulated after import for {asset_id}: "
            f"{actual['polygons']} != {actual['triangles']}"
        )
    if actual["polygons"] < expected["polygons"]:
        raise RuntimeError(
            f"GLB collision polygon count lost source faces for {asset_id}: "
            f"{actual['polygons']} < {expected['polygons']}"
        )
    if not isinstance(expected_geometry, dict) or set(actual["colliders"]) != set(expected_geometry):
        raise RuntimeError(f"GLB collision geometry names mismatch for {asset_id}")
    collider_report: dict[str, dict] = {}
    for name, required in expected_geometry.items():
        measured = actual["colliders"][name]
        if measured["unique_positions"] != required["unique_positions"] or measured["triangles"] != required["triangles"]:
            raise RuntimeError(f"GLB collision geometry topology mismatch for {asset_id} {name}")
        for key in ("bounds_min_m", "bounds_max_m", "dimensions_m"):
            if len(measured[key]) != 3 or any(
                abs(float(measured[key][index]) - float(required[key][index])) > COLLISION_BOUNDS_EPSILON_M
                for index in range(3)
            ):
                raise RuntimeError(f"GLB collision bounds drift for {asset_id} {name}: {key}")
        collider_report[name] = measured
    return {
        "objects": actual["objects"],
        "triangles": actual["triangles"],
        "source_vertices": expected["vertices"],
        "imported_vertex_slots": actual["vertex_slots"],
        "imported_unique_positions": actual["unique_positions"],
        "polygons_source": expected["polygons"],
        "polygons_imported": actual["polygons"],
        "polygon_encoding": "source_faces_exported_as_triangles",
        "unique_position_method": "world_space_quantized_1e-6_m",
        "bounds_epsilon_m": COLLISION_BOUNDS_EPSILON_M,
        "colliders": collider_report,
    }


def validate_glb_import(asset: dict, glb_path: Path) -> dict:
    clear_import_state()
    bpy.ops.import_scene.gltf(filepath=str(glb_path))
    mesh_objects = sorted((obj for obj in bpy.context.scene.objects if obj.type == "MESH"), key=lambda obj: obj.name)
    if not mesh_objects:
        raise RuntimeError(f"GLB imported no mesh objects: {glb_path}")
    imported_names = {obj.name for obj in mesh_objects}
    expected_render_names = {
        name
        for lod in range(3)
        for name in asset["validation"]["lods"][f"LOD{lod}"]["objects_named"]
    }
    expected_collision_names = set(asset["validation"]["collision_objects"])
    expected_names = expected_render_names | expected_collision_names
    if imported_names != expected_names:
        raise RuntimeError(
            f"GLB object-name contract mismatch for {asset['id']}: "
            f"missing={sorted(expected_names - imported_names)} extra={sorted(imported_names - expected_names)}"
        )
    render_objects = [obj for obj in mesh_objects if obj.name in expected_render_names]
    missing_uv = [obj.name for obj in render_objects if obj.data.uv_layers.get("UVMap") is None]
    if missing_uv:
        raise RuntimeError(f"GLB meshes missing UVMap for {asset['id']}: {missing_uv}")
    missing_material = [obj.name for obj in render_objects if not obj.data.materials]
    if missing_material:
        raise RuntimeError(f"GLB meshes missing material for {asset['id']}: {missing_material}")
    render_materials = sorted(
        {material for obj in render_objects for material in obj.data.materials if material},
        key=lambda material: material.name,
    )
    texture_nodes_per_material: dict[str, int] = {}
    for material in render_materials:
        count = (
            sum(
                1
                for node in material.node_tree.nodes
                if node.type == "TEX_IMAGE" and node.image is not None
            )
            if material.use_nodes and material.node_tree
            else 0
        )
        texture_nodes_per_material[material.name] = count
    untextured_materials = {
        name: count for name, count in texture_nodes_per_material.items() if count < 3
    }
    if untextured_materials:
        raise RuntimeError(
            f"GLB render materials do not expose three embedded PBR image maps for "
            f"{asset['id']}: {untextured_materials}"
        )
    lod_triangles: dict[str, int] = {}
    for lod in range(3):
        lod_name = f"LOD{lod}"
        objects = [obj for obj in mesh_objects if obj.name in set(asset["validation"]["lods"][lod_name]["objects_named"])]
        triangles = imported_triangle_count(objects)
        expected_triangles = asset["validation"]["lods"][lod_name]["triangles"]
        if triangles != expected_triangles:
            raise RuntimeError(
                f"GLB triangle mismatch for {asset['id']} {lod_name}: {triangles} != {expected_triangles}"
            )
        lod_triangles[lod_name] = triangles
    collision_objects = [obj for obj in mesh_objects if obj.name in expected_collision_names]
    imported_collisions = sorted(obj.name for obj in collision_objects)
    if imported_collisions != sorted(expected_collision_names):
        raise RuntimeError(f"GLB collision contract mismatch for {asset['id']}")
    # UCX meshes intentionally have no render UV/material requirement.  They
    # are nevertheless a governed part of the asset: verify their exact
    # exported topology statistics instead of silently treating them as
    # arbitrary extra mesh objects.
    expected_collision_statistics = asset["validation"].get("collisions")
    collision_geometry = collision_geometry_statistics(collision_objects)
    actual_collision_statistics = {
        "objects": len(collision_objects),
        "polygons": sum(len(obj.data.polygons) for obj in collision_objects),
        "triangles": imported_triangle_count(collision_objects),
        "vertex_slots": collision_geometry["vertex_slots"],
        "unique_positions": collision_geometry["unique_positions"],
        "degenerate_triangles": collision_geometry["degenerate_triangles"],
        "colliders": collision_geometry["colliders"],
    }
    collision_topology = validate_export_stable_collision_statistics(
        expected_collision_statistics,
        actual_collision_statistics,
        expected_corner_collision_geometry(asset, imported_collisions),
        asset_id=asset["id"],
    )
    return {
        "mesh_objects": len(mesh_objects),
        "materials": len({material.name for obj in mesh_objects for material in obj.data.materials if material}),
        "render_material_texture_nodes": texture_nodes_per_material,
        "embedded_images": sorted(
            image.name for image in bpy.data.images if image.name != "Render Result"
        ),
        "lod_triangles": lod_triangles,
        "collision_objects": imported_collisions,
        "collision_topology": collision_topology,
        "uv_layer": "UVMap",
    }


def decoded_pixels(path: Path) -> tuple[tuple[int, int], np.ndarray]:
    image = bpy.data.images.load(str(path), check_existing=False)
    try:
        dimensions = (int(image.size[0]), int(image.size[1]))
        pixels = np.empty(len(image.pixels), dtype=np.float32)
        image.pixels.foreach_get(pixels)
        return dimensions, pixels
    finally:
        bpy.data.images.remove(image)


def compare_decoded_images(primary_path: Path, rebuild_path: Path) -> dict:
    primary_size, primary_pixels = decoded_pixels(primary_path)
    rebuild_size, rebuild_pixels = decoded_pixels(rebuild_path)
    if primary_size != rebuild_size:
        raise RuntimeError(f"decoded image dimensions differ: {primary_path} vs {rebuild_path}")
    difference = np.abs(primary_pixels - rebuild_pixels)
    maximum = float(difference.max(initial=0.0))
    mean = float(difference.mean())
    changed_channels = int(np.count_nonzero(difference > PIXEL_EPSILON))
    difference_rgba = difference.reshape(primary_size[1], primary_size[0], 4)
    changed_pixel_mask = np.any(difference_rgba > PIXEL_EPSILON, axis=2)
    changed_pixels = int(np.count_nonzero(changed_pixel_mask))
    changed_coordinates = np.argwhere(changed_pixel_mask)
    if changed_pixels:
        minimum_y, minimum_x = (int(value) for value in changed_coordinates.min(axis=0))
        maximum_y, maximum_x = (int(value) for value in changed_coordinates.max(axis=0))
        changed_pixel_bounds = {
            "x": minimum_x,
            "y": minimum_y,
            "width": maximum_x - minimum_x + 1,
            "height": maximum_y - minimum_y + 1,
        }
    else:
        changed_pixel_bounds = None
    localized_gpu_stable = (
        maximum <= MAX_PIXEL_DIFFERENCE
        and changed_channels <= MAX_CHANGED_CHANNELS_PER_IMAGE
        and changed_pixels <= MAX_CHANGED_PIXELS_PER_IMAGE
        and mean <= MAX_MEAN_ABSOLUTE_DIFFERENCE
    )
    return {
        "path": primary_path.name,
        "dimensions": list(primary_size),
        "pixel_channels": int(primary_pixels.size),
        "identical": changed_channels == 0,
        "quantization_stable": localized_gpu_stable,
        "localized_gpu_stable": localized_gpu_stable,
        "changed_channels": changed_channels,
        "changed_pixels": changed_pixels,
        "changed_pixel_bounds": changed_pixel_bounds,
        "changed_channel_fraction": changed_channels / max(1, int(primary_pixels.size)),
        "maximum_absolute_difference": maximum,
        "mean_absolute_difference": mean,
        "container_sha256_match": sha256(primary_path) == sha256(rebuild_path),
    }


def image_records(report: dict) -> list[dict]:
    records = [report["lineup"]]
    for asset in report["assets"]:
        records.extend(asset["turntable"])
    return records


def texture_record_map(report: dict) -> dict[tuple[str, str], dict]:
    records = report.get("textures")
    if not isinstance(records, list) or not records:
        raise RuntimeError("asset-kit build report contains no texture records")
    required_maps = report.get("texture_policy", {}).get("required_maps")
    if required_maps != ["base_color", "roughness", "normal"]:
        raise RuntimeError("asset-kit build report has an invalid texture policy")
    if report.get("texture_policy", {}).get("foliage_alpha_mode") not in ("blend", "mask"):
        raise RuntimeError("asset-kit build report must require foliage blend or mask")
    result: dict[tuple[str, str], dict] = {}
    materials: dict[str, set[str]] = {}
    for record in records:
        key = (record.get("material_key"), record.get("map"))
        if not all(isinstance(value, str) and value for value in key):
            raise RuntimeError(f"invalid texture record identity: {record}")
        if key in result:
            raise RuntimeError(f"duplicate texture record: {key}")
        if record.get("resolution") != [report["texture_policy"]["resolution"]] * 2:
            raise RuntimeError(f"texture resolution does not match policy: {key}")
        if record.get("channels") != 4:
            raise RuntimeError(f"texture must record four channels: {key}")
        expected_colorspace = "sRGB" if key[1] == "base_color" else "Non-Color"
        if record.get("colorspace") != expected_colorspace:
            raise RuntimeError(f"texture colorspace mismatch: {key}")
        if record.get("packed_in_blend") is not True or record.get("embed_in_glb") is not True:
            raise RuntimeError(f"texture is not recorded as packed and embedded: {key}")
        result[key] = record
        materials.setdefault(key[0], set()).add(key[1])
    required_set = set(required_maps)
    incomplete = {
        material: sorted(required_set - maps)
        for material, maps in materials.items()
        if maps != required_set
    }
    if incomplete:
        raise RuntimeError(f"incomplete material texture sets: {incomplete}")
    return result


def foliage_alpha_contract(path: Path) -> dict:
    dimensions, pixels = decoded_pixels(path)
    alpha = pixels[3::4]
    minimum = float(alpha.min(initial=1.0))
    maximum = float(alpha.max(initial=0.0))
    transparent_channels = int(np.count_nonzero(alpha < 0.05))
    opaque_channels = int(np.count_nonzero(alpha > 0.95))
    if transparent_channels == 0 or opaque_channels == 0:
        raise RuntimeError(f"foliage base-color texture has no effective alpha silhouette: {path}")
    return {
        "dimensions": list(dimensions),
        "minimum_alpha": minimum,
        "maximum_alpha": maximum,
        "transparent_pixels": transparent_channels,
        "opaque_pixels": opaque_channels,
    }


def main() -> None:
    args = parse_arguments()
    output_report = args.output_report.resolve()
    if output_report.exists():
        raise RuntimeError(f"validation output already exists: {output_report}")
    primary_root, primary = load_build(args.primary_dir)
    rebuild_root, rebuild = load_build(args.rebuild_dir)
    primary_version = primary.get("generator", {}).get("blender_version")
    rebuild_version = rebuild.get("generator", {}).get("blender_version")
    if primary_version != rebuild_version:
        raise RuntimeError("builds do not use the same recorded generator Blender version")
    require_running_blender_major_minor(
        recorded_blender_major_minor(primary_version, build="primary")
    )
    if primary["asset_kit_id"] != rebuild["asset_kit_id"]:
        raise RuntimeError("builds do not target the same asset kit")
    if primary.get("asset_kit_revision") != rebuild.get("asset_kit_revision"):
        raise RuntimeError("builds do not target the same asset kit revision")
    if primary.get("authored_source") != rebuild.get("authored_source"):
        raise RuntimeError("builds do not use the same authored asset source")
    if primary.get("provenance") != rebuild.get("provenance"):
        raise RuntimeError("builds do not use the same provenance")
    if primary["generator"]["script_sha256"] != rebuild["generator"]["script_sha256"]:
        raise RuntimeError("builds do not use the same worker script")
    if primary["generator"]["input_manifest_sha256"] != rebuild["generator"]["input_manifest_sha256"]:
        raise RuntimeError("builds do not use the same input manifest")
    if primary.get("texture_policy") != rebuild.get("texture_policy"):
        raise RuntimeError("builds do not use the same texture policy")

    primary_textures = texture_record_map(primary)
    rebuild_textures = texture_record_map(rebuild)
    if primary_textures.keys() != rebuild_textures.keys():
        raise RuntimeError("builds do not contain the same material texture sets")
    texture_results: list[dict] = []
    foliage_alpha_results: dict[str, dict] = {}
    for key, record in primary_textures.items():
        other = rebuild_textures[key]
        primary_texture = verify_recorded_file(primary_root, record)
        rebuild_texture = verify_recorded_file(rebuild_root, other)
        comparison = compare_decoded_images(primary_texture, rebuild_texture)
        comparison["path"] = record["path"]
        comparison["material_key"] = key[0]
        comparison["map"] = key[1]
        if not comparison["identical"]:
            raise RuntimeError(f"generated texture pixels are not identical: {key}")
        if not comparison["container_sha256_match"]:
            raise RuntimeError(f"generated texture containers are not byte-identical: {key}")
        texture_results.append(comparison)
        if key[1] == "base_color" and (key[0].startswith("oak_leaf") or key[0].startswith("pine")):
            foliage_alpha_results[key[0]] = foliage_alpha_contract(primary_texture)

    rebuild_assets = {asset["id"]: asset for asset in rebuild["assets"]}
    asset_results: list[dict] = []
    for asset in primary["assets"]:
        asset_id = asset["id"]
        other = rebuild_assets.get(asset_id)
        if other is None:
            raise RuntimeError(f"rebuild missing asset: {asset_id}")
        primary_glb = verify_recorded_file(primary_root, asset["glb"])
        rebuild_glb = verify_recorded_file(rebuild_root, other["glb"])
        glb_identical = sha256(primary_glb) == sha256(rebuild_glb)
        if not glb_identical:
            raise RuntimeError(f"GLB rebuild is not byte-identical: {asset_id}")
        if asset["validation"] != other["validation"]:
            raise RuntimeError(f"validation statistics differ between builds: {asset_id}")
        imported = validate_glb_import(asset, primary_glb)
        asset_results.append(
            {
                "id": asset_id,
                "glb_byte_identical": True,
                "glb_sha256": asset["glb"]["sha256"],
                "validation_statistics_identical": True,
                "reimport": imported,
            }
        )

    primary_images = image_records(primary)
    rebuild_images = {record["path"]: record for record in image_records(rebuild)}
    pixel_results: list[dict] = []
    for record in primary_images:
        other = rebuild_images.get(record["path"])
        if other is None:
            raise RuntimeError(f"rebuild missing image: {record['path']}")
        primary_path = verify_recorded_file(primary_root, record)
        rebuild_path = verify_recorded_file(rebuild_root, other)
        pixel_result = compare_decoded_images(primary_path, rebuild_path)
        pixel_result["path"] = record["path"]
        pixel_results.append(pixel_result)
    non_identical_pixels = [result["path"] for result in pixel_results if not result["identical"]]
    materially_changed_pixels = [
        result["path"] for result in pixel_results if not result["localized_gpu_stable"]
    ]

    report = {
        "schema": "forge.blender-asset-kit-validation",
        "schema_version": 1,
        "asset_kit_id": primary["asset_kit_id"],
        "blender_version": bpy.app.version_string,
        "primary_build": str(primary_root),
        "rebuild": str(rebuild_root),
        "assets": asset_results,
        "textures": {
            "count": len(texture_results),
            "material_sets": len({result["material_key"] for result in texture_results}),
            "decoded_pixels_identical": True,
            "container_hashes_identical": True,
            "foliage_alpha_verified": True,
            "foliage_alpha": foliage_alpha_results,
            "comparisons": texture_results,
        },
        "renders": {
            "count": len(pixel_results),
            "decoded_pixels_identical": not non_identical_pixels,
            "decoded_pixels_quantization_stable": not materially_changed_pixels,
            "decoded_pixels_localized_gpu_stable": not materially_changed_pixels,
            "non_identical_paths": non_identical_pixels,
            "materially_changed_paths": materially_changed_pixels,
            "acceptance_contract": {
                "maximum_absolute_difference": MAX_PIXEL_DIFFERENCE,
                "maximum_changed_channels_per_image": MAX_CHANGED_CHANNELS_PER_IMAGE,
                "maximum_changed_pixels_per_image": MAX_CHANGED_PIXELS_PER_IMAGE,
                "maximum_mean_absolute_difference": MAX_MEAN_ABSOLUTE_DIFFERENCE,
                "rationale": "Bounds isolated GPU edge-sampling variance while rejecting diffuse or structural render changes.",
            },
            "container_hashes_informative_only": True,
            "comparisons": pixel_results,
        },
        "qualification": {
            "deterministic_geometry": True,
            "deterministic_texture_maps": True,
            "deterministic_decoded_renders": not materially_changed_pixels,
            "decoded_render_stability_contract": not materially_changed_pixels,
            "glb_reimport_contract": True,
            "glb_textures_reimported": True,
            "foliage_alpha_verified": True,
            "runtime_bevy_integrated": False,
            "publication_approved": False,
            "r1_063_complete": False,
        },
    }
    output_report.parent.mkdir(parents=True, exist_ok=True)
    output_report.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    if materially_changed_pixels:
        print(
            "OPENRPG_FORGE_R1_063_ASSET_KIT_VALIDATION_FAIL "
            f"reason=decoded_pixels_outside_localized_gpu_contract renders={len(materially_changed_pixels)} "
            f"report={output_report} "
            f"sha256={sha256(output_report)}"
        )
        raise RuntimeError(f"decoded render pixels outside localized GPU contract: {materially_changed_pixels}")
    print(
        "OPENRPG_FORGE_R1_063_ASSET_KIT_VALIDATION_PASS "
        f"assets={len(asset_results)} textures={len(texture_results)} renders={len(pixel_results)} "
        f"report={output_report} sha256={sha256(output_report)}"
    )


if __name__ == "__main__":
    main()

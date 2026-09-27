#!/usr/bin/env python3
"""Create a governed authored wall-corner source from the pinned straight wall.

This worker deliberately derives only a new DCC source.  It neither replaces a
kit nor writes a Studio catalogue.  The source GLB is pinned because a visually
similar wall with another mortar layout would invalidate the corner contract.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


SOURCE_SHA256 = "1be108a0113dcad4ff4fcd4d39de7d38e96967b615106eb52a865231efd9b3b9"
SOURCE_ASSET_ID = "vv-architecture-wall-joint-a"
TARGET_ASSET_ID = "vv-architecture-wall-corner-a"
REQUIRED_LODS = (0, 1, 2)

# The collision mesh expresses the placement contract.  The authored render
# mesh deliberately retains its limestone relief, which protrudes beyond the
# 0.72 m collision depth.  A bevel may also add a sub-centimetre length delta.
NOMINAL_COLLISION_DIMENSIONS_M = (4.0, 0.72, 3.2)
COLLISION_DIMENSION_EPSILON_M = 0.002
RENDER_LENGTH_EPSILON_M = 0.01
RENDER_DEPTH_RANGE_M = (0.72, 0.82)
RENDER_HEIGHT_EPSILON_M = 0.01
CENTER_ALIGNMENT_EPSILON_M = 0.05
INTERIOR_FACADE_CLEARANCE_M = 0.0
# The governed source is triangulated glTF geometry.  Pin the complete render
# topology separately from its limestone subset: the first scan accidentally
# recorded only R1063_Limestone, omitting the 108/44 mortar triangles which
# Blender imports with the same LOD mesh.  Two complete source copies form the
# corner base, so their budget must use the complete render count.
SOURCE_RENDER_TRIANGLES = {0: 7236, 1: 1408, 2: 12}
SOURCE_TRIANGLES_BY_MATERIAL = {
    0: {"R1063_Limestone": 7128, "R1063_Mortar": 108},
    1: {"R1063_Limestone": 1364, "R1063_Mortar": 44},
    2: {"R1063_Limestone": 12, "R1063_Mortar": 0},
}
BASE_CORNER_TRIANGLES = {lod: 2 * triangles for lod, triangles in SOURCE_RENDER_TRIANGLES.items()}
INTERIOR_FACADE_TRIANGLE_ALLOWANCE = {
    0: 24000 - BASE_CORNER_TRIANGLES[0],
    1: 7000 - BASE_CORNER_TRIANGLES[1],
}
EXTERIOR_FRONT_NORMAL_Y_MAX = -0.95


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while block := stream.read(1024 * 1024):
            digest.update(block)
    return digest.hexdigest()


def parse_args() -> argparse.Namespace:
    separator = sys.argv.index("--") if "--" in sys.argv else len(sys.argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-wall-glb", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser.parse_args(sys.argv[separator + 1 :])


def require_new_directory(path: Path) -> None:
    if path.exists():
        raise RuntimeError(f"output directory already exists: {path}")
    path.mkdir(parents=True)


def load_manifest(path: Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("schema") != "forge.asset-kit" or data.get("schema_version") != 1:
        raise RuntimeError("corner manifest schema mismatch")
    assets = data.get("assets")
    if not isinstance(assets, list) or len(assets) != 1 or assets[0].get("id") != TARGET_ASSET_ID:
        raise RuntimeError("corner manifest must contain only the governed corner asset")
    asset = assets[0]
    if asset.get("generator_profile") != "limestone_wall_corner" or asset.get("collision_boxes") != 2:
        raise RuntimeError("corner manifest profile or collision contract mismatch")
    if asset.get("dimensions_m") != [4, 4, 3.2] or asset.get("lod_triangle_budgets") != [24000, 7000, 1800]:
        raise RuntimeError("corner manifest dimensions or LOD budgets mismatch")
    return data


def packed_image_record(image: bpy.types.Image) -> dict:
    if image.packed_file is None:
        raise RuntimeError(f"source image is not packed: {image.name}")
    pixels = struct.pack("<%sf" % len(image.pixels), *image.pixels[:])
    return {
        "name": image.name,
        "dimensions": list(image.size),
        "channels": image.channels,
        "packed_sha256": hashlib.sha256(image.packed_file.data).hexdigest(),
        "pixels_sha256_f32le": hashlib.sha256(pixels).hexdigest(),
    }


def material_record(material: bpy.types.Material) -> dict:
    if material is None or not material.use_nodes or material.node_tree is None:
        raise RuntimeError("source mesh has a non-node material")
    nodes = material.node_tree.nodes
    images = [packed_image_record(node.image) for node in nodes if node.type == "TEX_IMAGE" and node.image]
    if len(images) < 3:
        raise RuntimeError(f"source material has fewer than three packed image maps: {material.name}")
    principled = [node for node in nodes if node.type == "BSDF_PRINCIPLED"]
    if len(principled) != 1:
        raise RuntimeError(f"source material is not a single Principled PBR graph: {material.name}")
    return {"name": material.name, "nodes": sorted(node.type for node in nodes), "images": sorted(images, key=lambda item: item["name"])}


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    return (Vector(tuple(min(point[index] for point in points) for index in range(3))),
            Vector(tuple(max(point[index] for point in points) for index in range(3))))


def validate_source_bounds(
    render_bounds: dict[int, tuple[tuple[float, float, float], tuple[float, float, float]]],
    collision_bounds: tuple[tuple[float, float, float], tuple[float, float, float]],
) -> dict:
    """Accept bounded render relief while pinning placement to UCX geometry."""
    collision_min, collision_max = collision_bounds
    collision_dimensions = tuple(collision_max[index] - collision_min[index] for index in range(3))
    if any(
        abs(collision_dimensions[index] - NOMINAL_COLLISION_DIMENSIONS_M[index]) > COLLISION_DIMENSION_EPSILON_M
        for index in range(3)
    ):
        raise RuntimeError(
            "source collision bounds do not match the governed 4m x 0.72m x 3.2m placement contract"
        )
    collision_centre = tuple((collision_min[index] + collision_max[index]) * 0.5 for index in range(3))
    render_records: dict[str, dict] = {}
    for lod in REQUIRED_LODS:
        if lod not in render_bounds:
            raise RuntimeError(f"source bounds are missing LOD{lod}")
        render_min, render_max = render_bounds[lod]
        dimensions = tuple(render_max[index] - render_min[index] for index in range(3))
        centre = tuple((render_min[index] + render_max[index]) * 0.5 for index in range(3))
        if abs(dimensions[0] - NOMINAL_COLLISION_DIMENSIONS_M[0]) > RENDER_LENGTH_EPSILON_M:
            raise RuntimeError(f"source render LOD{lod} length exceeds the bounded bevel allowance")
        if not RENDER_DEPTH_RANGE_M[0] <= dimensions[1] <= RENDER_DEPTH_RANGE_M[1]:
            raise RuntimeError(f"source render LOD{lod} depth is outside the collision-plus-relief allowance")
        if abs(dimensions[2] - NOMINAL_COLLISION_DIMENSIONS_M[2]) > RENDER_HEIGHT_EPSILON_M:
            raise RuntimeError(f"source render LOD{lod} height is outside the governed 3.2m contract")
        if any(abs(centre[index] - collision_centre[index]) > CENTER_ALIGNMENT_EPSILON_M for index in range(3)):
            raise RuntimeError(f"source render LOD{lod} centre is not aligned with the governed collision")
        render_records[f"LOD{lod}"] = {
            "min_m": list(render_min),
            "max_m": list(render_max),
            "dimensions_m": list(dimensions),
            "centre_m": list(centre),
        }
    return {
        "nominal_collision_dimensions_m": list(NOMINAL_COLLISION_DIMENSIONS_M),
        "collision_dimension_epsilon_m": COLLISION_DIMENSION_EPSILON_M,
        "render_length_epsilon_m": RENDER_LENGTH_EPSILON_M,
        "render_depth_range_m": list(RENDER_DEPTH_RANGE_M),
        "render_height_epsilon_m": RENDER_HEIGHT_EPSILON_M,
        "centre_alignment_epsilon_m": CENTER_ALIGNMENT_EPSILON_M,
        "collision": {
            "min_m": list(collision_min),
            "max_m": list(collision_max),
            "dimensions_m": list(collision_dimensions),
            "centre_m": list(collision_centre),
        },
        "render_lods": render_records,
    }


def oriented_world_mesh(source: bpy.types.Object) -> bpy.types.Mesh:
    """Repair the pinned source's inward geometric winding on a derived copy.

    glTF splits vertices at UV/normal seams. Weld coincident vertices before
    orienting each closed stone/mortar component; per-loop UVs stay separate.
    The original GLB remains immutable. No position is otherwise changed.
    """
    mesh = source.data.copy()
    mesh.transform(source.matrix_world)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    # Imported split normals describe the old, inward faces. Recompute them
    # from the repaired geometry instead of preserving contradictory normals.
    mesh.normals_split_custom_set([(0.0, 0.0, 0.0)] * len(mesh.loops))
    mesh.update()
    return mesh


def bake_mitered_half(source, name, arm_transform, source_center, interior=False):
    """A real half-thickness masonry shell, mirrored and cut at a 45° elbow.

    Both exposed sides use the pinned outer stone surface. The original back
    half is removed, so the inner surface never overlays an existing facade.
    X owns x>=y and Y owns y>=x: no overlapping coplanar elbow panels remain.
    """
    mesh = oriented_world_mesh(source)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.bisect_plane(
        bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
        dist=0.0000001, plane_co=source_center, plane_no=Vector((0, 1, 0)),
        clear_inner=False, clear_outer=True,
    )
    bm.to_mesh(mesh)
    bm.free()
    transform = arm_transform.copy()
    if interior:
        mirror = Matrix.Identity(4)
        mirror[1][1] = -1
        mirror[1][3] = 2 * source_center.y
        transform = transform @ mirror
    mesh.transform(transform)
    if transform.determinant() < 0:
        mesh.flip_normals()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    normal = Vector((-1, 1, 0)) if "_ArmX" in name else Vector((1, -1, 0))
    bmesh.ops.bisect_plane(
        bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
        dist=0.0000001, plane_co=Vector((0, 0, 0)), plane_no=normal,
        clear_inner=False, clear_outer=True,
    )
    bm.to_mesh(mesh)
    bm.free()
    mesh.normals_split_custom_set([(0.0, 0.0, 0.0)] * len(mesh.loops))
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj["forge_asset_id"] = TARGET_ASSET_ID
    obj["forge_role"] = "render"
    obj["forge_lod"] = int(name.split("_LOD", 1)[1][0])
    obj["forge_surface"] = "interior_limestone_facade" if interior else "exterior_masonry_half"
    return obj


def validate_triangular_face_arities(vertex_counts: list[int], source_label: str) -> None:
    """Refuse quads and n-gons rather than implicitly re-triangulating them."""
    unexpected = sorted({count for count in vertex_counts if count != 3})
    if unexpected:
        raise RuntimeError(
            f"{source_label} contains unexpected non-triangular polygons: {unexpected}"
        )


def validate_source_lod_topology(source_by_lod: dict[int, bpy.types.Object]) -> dict[str, dict]:
    """Pin the triangulated source before deriving a new authored asset."""
    report: dict[str, dict] = {}
    for lod in REQUIRED_LODS:
        source = source_by_lod[lod]
        vertex_counts = [len(polygon.vertices) for polygon in source.data.polygons]
        validate_triangular_face_arities(vertex_counts, f"source LOD{lod} {source.name}")
        source.data.calc_loop_triangles()
        actual = len(source.data.loop_triangles)
        expected = SOURCE_RENDER_TRIANGLES[lod]
        if actual != expected:
            raise RuntimeError(
                f"source LOD{lod} triangle count changed: {actual} != governed {expected}"
            )
        expected_by_material = SOURCE_TRIANGLES_BY_MATERIAL[lod]
        triangles_by_material = {material_name: 0 for material_name in expected_by_material}
        for triangle in source.data.loop_triangles:
            if triangle.material_index < 0 or triangle.material_index >= len(source.data.materials):
                raise RuntimeError(
                    f"source LOD{lod} triangle has an invalid material slot: {triangle.material_index}"
                )
            material = source.data.materials[triangle.material_index]
            if material is None:
                raise RuntimeError(f"source LOD{lod} has a triangle without a material: {source.name}")
            if material.name not in triangles_by_material:
                raise RuntimeError(
                    f"source LOD{lod} triangle has an unexpected material: {material.name}"
                )
            triangles_by_material[material.name] += 1
        if triangles_by_material != expected_by_material:
            raise RuntimeError(
                f"source LOD{lod} material triangle counts changed: "
                f"{triangles_by_material} != governed {expected_by_material}"
            )
        limestone_triangles = triangles_by_material["R1063_Limestone"]
        report[f"LOD{lod}"] = {
            "expected_total_triangles": expected,
            "actual_total_triangles": actual,
            "expected_triangles_by_material": expected_by_material,
            "actual_triangles_by_material": triangles_by_material,
            "expected_limestone_triangles": expected_by_material["R1063_Limestone"],
            "actual_limestone_triangles": limestone_triangles,
            "polygon_arity": "triangles_only",
        }
    return report


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def validate_derived_triangle_budget(
    render_objects_by_lod: dict[int, list[bpy.types.Object]], asset: dict
) -> dict[str, dict]:
    """Reject a facade that would exceed the manifest's immutable budgets."""
    budgets = asset["lod_triangle_budgets"]
    report: dict[str, dict] = {}
    for lod, objects in render_objects_by_lod.items():
        actual = sum(triangle_count(obj) for obj in objects)
        base = BASE_CORNER_TRIANGLES[lod]
        added = actual - base
        record = {
            "historical_two_full_arms_triangles": base,
            "net_miter_and_half_volume_triangle_delta": added,
            "total_triangles": actual,
            "manifest_budget": budgets[lod],
        }
        if actual > budgets[lod]:
            raise RuntimeError(
                f"visible-only interior facade exceeds LOD{lod} manifest budget: {actual} > {budgets[lod]}"
            )
        report[f"LOD{lod}"] = record
    return report


def create_collider(name: str, center: tuple[float, float, float], dimensions: tuple[float, float, float]) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj["forge_asset_id"] = TARGET_ASSET_ID
    obj["forge_role"] = "collision"
    obj.hide_render = True
    obj.display_type = "WIRE"
    return obj


def main() -> None:
    args = parse_args()
    source_path, manifest_path, output = (args.source_wall_glb.resolve(strict=True), args.manifest.resolve(strict=True), args.output_dir.resolve())
    require_new_directory(output)
    manifest = load_manifest(manifest_path)
    asset = manifest["assets"][0]
    if sha256(source_path) != SOURCE_SHA256:
        raise RuntimeError("source wall GLB SHA-256 does not match the governed R2-025 joint")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    source_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    source_by_lod: dict[int, bpy.types.Object] = {}
    for lod in REQUIRED_LODS:
        matches = [obj for obj in source_meshes if obj.name.startswith(f"{SOURCE_ASSET_ID}_LOD{lod}_")]
        if len(matches) != 1:
            raise RuntimeError(f"source must contain exactly one {SOURCE_ASSET_ID} LOD{lod} mesh")
        source_by_lod[lod] = matches[0]
    source_colliders = [obj for obj in source_meshes if obj.name.startswith("UCX_")]
    if len(source_colliders) != 1:
        raise RuntimeError("source must contain exactly one governed UCX collision mesh")
    unexpected = [obj.name for obj in source_meshes if obj not in source_by_lod.values() and obj not in source_colliders]
    if unexpected:
        raise RuntimeError(f"source includes unexpected render meshes: {unexpected}")
    materials = {material.name: material_record(material) for obj in source_by_lod.values() for material in obj.data.materials}
    if set(materials) != {"R1063_Limestone", "R1063_Mortar"}:
        raise RuntimeError(f"source material set mismatch: {sorted(materials)}")
    render_bounds_vectors = {lod: world_bounds([source]) for lod, source in source_by_lod.items()}
    collision_bounds_vectors = world_bounds(source_colliders)
    # Validate and record the exact measured coordinates.  In particular, do
    # not reconstruct the render bounds from a centre: the relief is allowed
    # to be slightly asymmetric.
    bounds_contract = validate_source_bounds(
        {
            lod: (
                tuple(float(value) for value in bounds_min),
                tuple(float(value) for value in bounds_max),
            )
            for lod, (bounds_min, bounds_max) in render_bounds_vectors.items()
        },
        (
            tuple(float(value) for value in collision_bounds_vectors[0]),
            tuple(float(value) for value in collision_bounds_vectors[1]),
        ),
    )
    source_topology = validate_source_lod_topology(source_by_lod)
    source_center = (collision_bounds_vectors[0] + collision_bounds_vectors[1]) * 0.5
    target_vertical_centre = float(source_center.z)
    arm_x = Matrix.Translation(Vector((2.0, 0.36, target_vertical_centre)) - source_center)
    # The +Y arm keeps the source +X length axis, but mirrors its local X before
    # rotating -90 degrees.  This maps source +X to world +Y while sending the
    # source faced side (local -Y) to the outside world -X plane.  A plain +90
    # rotation would expose the un-faced back of the source on the public side.
    arm_y = (
        Matrix.Translation(Vector((0.36, 2.0, target_vertical_centre)))
        @ Matrix.Rotation(-math.pi * 0.5, 4, "Z")
        @ Matrix.Scale(-1.0, 4, Vector((1.0, 0.0, 0.0)))
        @ Matrix.Translation(-source_center)
    )
    objects: list[str] = []
    interior_facades: dict[str, dict] = {}
    render_objects_by_lod: dict[int, list[bpy.types.Object]] = {lod: [] for lod in REQUIRED_LODS}
    for lod, source in source_by_lod.items():
        for arm, transform in (("ArmX", arm_x), ("ArmY", arm_y)):
            for interior in (False, True):
                suffix = "_InteriorFacade" if interior else ""
                obj = bake_mitered_half(source, f"{TARGET_ASSET_ID}_LOD{lod}_{arm}{suffix}", transform, source_center, interior)
                objects.append(obj.name)
                render_objects_by_lod[lod].append(obj)
            interior_facades[f"LOD{lod}_{arm}"] = {"construction": "mirrored_half_volume_without_overlapping_backing", "miter_degrees": 45}
    for name, center, size in (
        (f"UCX_{TARGET_ASSET_ID}_ArmX", (2.0, 0.36, 1.6), (4.0, 0.72, 3.2)),
        (f"UCX_{TARGET_ASSET_ID}_ArmY", (0.36, 2.0, 1.6), (0.72, 4.0, 3.2)),
    ):
        obj = create_collider(name, center, size)
        objects.append(obj.name)
    triangle_budget = validate_derived_triangle_budget(render_objects_by_lod, asset)
    bpy.ops.wm.save_as_mainfile(filepath=str(output / "authored.blend"))
    blend = output / "authored.blend"
    descriptor = {
        "schema": "forge.blender-authored-source",
        "schema_version": 1,
        "base_manifest_sha256": sha256(manifest_path),
        "blend": {"path": "authored.blend", "sha256": sha256(blend)},
        "assets": {TARGET_ASSET_ID: sorted(objects)},
        "provenance": manifest["provenance"],
    }
    (output / "authored-source.v1.json").write_text(json.dumps(descriptor, indent=2) + "\n", encoding="utf-8")
    report = {
        "schema": "forge.modular-wall-corner-derivation",
        "schema_version": 1,
        "source": {"asset_id": SOURCE_ASSET_ID, "glb": str(source_path), "sha256": SOURCE_SHA256},
        "target": {"asset_id": TARGET_ASSET_ID, "manifest": str(manifest_path), "manifest_sha256": sha256(manifest_path)},
        "blender": bpy.app.version_string,
        "derivation_script_sha256": sha256(Path(__file__)),
        "source_bounds_m": bounds_contract,
        "source_topology": source_topology,
        "arms": {"x": {"centre_m": [2.0, 0.36, target_vertical_centre], "rotation_z_degrees": 0, "source_mirror": None}, "y": {"centre_m": [0.36, 2.0, target_vertical_centre], "rotation_z_degrees": -90, "source_mirror": "local_x"}},
        "elbow_overlap_treatment": {
            "strategy": "complementary_45_degree_miter",
            "source_back_halves_removed": True,
            "rationale": "Two complementary half spaces remove overlapping elbow geometry and source backing.",
        },
        "interior_limestone_facades": {
            "source_material": "R1063_Limestone",
            "lods": interior_facades,
            "lod2": "mirrored_mitered_silhouette",
            "mirror_median_planes_m": {"ArmX_y": 0.36, "ArmY_x": 0.36},
            "outward_clearance_m": INTERIOR_FACADE_CLEARANCE_M,
            "source_back_halves_replaced": True,
            "volumetric_source_duplication": False,
        },
        "triangle_budget": triangle_budget,
        "objects": sorted(objects),
        "materials": materials,
        "authored_blend": {"path": "authored.blend", "sha256": sha256(blend)},
        "descriptor": {"path": "authored-source.v1.json", "sha256": sha256(output / "authored-source.v1.json")},
        "scope": "DCC derivation only; not a Studio/Rapier/human qualification or promotion.",
    }
    (output / "derivation-report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"pass": True, "descriptor": str(output / "authored-source.v1.json"), "objects": len(objects)}))


if __name__ == "__main__":
    main()

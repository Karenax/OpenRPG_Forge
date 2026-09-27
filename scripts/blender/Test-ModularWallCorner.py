#!/usr/bin/env python3
"""Reimport a modular wall corner and verify both joins and exposed masonry."""

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
from mathutils.bvhtree import BVHTree


INTERIOR_FACADE_CLEARANCE_M = 0.0


def image_contract(image):
    """Hash decoded pixels so PNG/GLB container metadata cannot mask a change."""
    pixels = struct.pack("<%sf" % len(image.pixels), *image.pixels[:])
    return {"dimensions": list(image.size), "channels": image.channels,
            "pixels_sha256_f32le": hashlib.sha256(pixels).hexdigest()}


def material_contract(objects):
    """Return the actual reimported PBR texture-node contract per material."""
    result = {}
    render_objects = [obj for obj in objects if not obj.name.startswith("UCX_")]
    for obj in render_objects:
        for material in obj.data.materials:
            if material is None or not material.use_nodes or material.node_tree is None:
                raise RuntimeError(f"mesh {obj.name} has a non-node material")
            canonical_name = material.name.removesuffix(".001")
            images, node_types = {}, []
            for node in material.node_tree.nodes:
                node_types.append(node.type)
                if node.type == "TEX_IMAGE":
                    if node.image is None:
                        raise RuntimeError(f"texture node without image in {material.name}")
                    images[node.name.removesuffix(".001")] = image_contract(node.image)
            if not images or "BSDF_PRINCIPLED" not in node_types:
                raise RuntimeError(f"incomplete PBR material in {material.name}")
            record = {"node_types": sorted(node_types), "images": dict(sorted(images.items()))}
            previous = result.setdefault(canonical_name, record)
            if previous != record:
                raise RuntimeError(f"inconsistent material instances: {canonical_name}")
    return dict(sorted(result.items()))


def use_eevee(scene):
    """Select the Eevee enum exposed by the running Blender version."""
    engines = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        if engine in engines:
            scene.render.engine = engine
            return
    raise RuntimeError(f"Blender {bpy.app.version_string} does not expose an Eevee render engine")


def mesh_tree(objects, material_name=None):
    vertices, polygons = [], []
    for obj in objects:
        start = len(vertices)
        vertices.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
        for face in obj.data.polygons:
            if material_name is not None:
                material = obj.data.materials[face.material_index] if face.material_index < len(obj.data.materials) else None
                if material is None or material.name.removesuffix(".001") != material_name:
                    continue
            indices = tuple(start + index for index in face.vertices)
            # A reflected object transform reverses triangle winding. Account
            # for its determinant as the renderer does for front-facing faces.
            polygons.append(indices[::-1] if obj.matrix_world.determinant() < 0 else indices)
    if not polygons:
        raise RuntimeError("mesh probe has no polygons after material filtering")
    return BVHTree.FromPolygons(vertices, polygons)


def duplicate_lod(sources, lod, location, rotation_z=0.0):
    """Copy one imported LOD into Blender world space at an authored join.

    glTF import keeps the meshes below a conversion parent.  Transforming the
    child in that local space made the second arm rotate around the imported
    axis rather than Blender's vertical Z axis.  Preserve the imported world
    basis, detach the copy, then compose its authored placement in world space.
    This tests the same +X/+Y corner geometry on both arms.
    """
    result = []
    for source in sources:
        obj = source.copy()
        obj.data = source.data.copy()
        bpy.context.scene.collection.objects.link(obj)
        source_world = source.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = (
            Matrix.Translation(Vector(location))
            @ Matrix.Rotation(rotation_z, 4, "Z")
            @ source_world
        )
        result.append(obj)
    return result


def duplicate_lod_with_matrix(sources, matrix):
    """Copy a source LOD using the same full transform as the derivation.

    This is deliberately separate from the scene-composition helper.  A
    translation-only reference shifts the public face by half the collision
    depth and can make a correct corner fail its material-relief comparison.
    """
    result = []
    for source in sources:
        obj = source.copy()
        obj.data = source.data.copy()
        bpy.context.scene.collection.objects.link(obj)
        source_world = source.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = matrix @ source_world
        result.append(obj)
    return result


def world_bounds(objects):
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[index] for point in points) for index in range(3))),
        Vector(tuple(max(point[index] for point in points) for index in range(3))),
    )


def probe_face(tree, origin, direction, samples):
    misses = []
    for sample in samples:
        hit = tree.ray_cast(origin(sample), direction, 20.0)
        if hit[0] is None:
            misses.append(sample)
    return misses


def profile_samples(width, height, lod):
    courses = max(3, round(height / (0.34 if lod == 0 else 0.56)))
    return [
        (width * (0.19 + 0.62 * column / 12.0), height * (row + 0.5) / courses)
        for row in range(courses)
        for column in range(13)
    ]


def exterior_projection_depths(tree, arm, width, height, lod):
    """Measure visible masonry projection from one exterior arm plane.

    The elbow itself is omitted because its two arms intentionally overlap to
    close the modular volume.  The remaining samples compare a corner arm with
    a separately reimported straight wall, not a generator assumption.
    """
    samples = profile_samples(width, height, lod)
    depths, misses = [], []
    for distance, z in samples:
        if arm == "x":
            hit = tree.ray_cast(Vector((distance, -10.0, z)), Vector((0.0, 1.0, 0.0)), 20.0)
            coordinate = hit[0].y if hit[0] is not None else None
        else:
            hit = tree.ray_cast(Vector((-10.0, distance, z)), Vector((1.0, 0.0, 0.0)), 20.0)
            coordinate = hit[0].x if hit[0] is not None else None
        if coordinate is None:
            misses.append((distance, z))
        else:
            depths.append(-coordinate)
    return depths, misses


def compare_exterior_projection(corner_tree, wall_tree, arm, width, height, lod):
    corner_depths, corner_misses = exterior_projection_depths(corner_tree, arm, width, height, lod)
    wall_depths, wall_misses = exterior_projection_depths(wall_tree, arm, width, height, lod)
    if not corner_depths or not wall_depths:
        return {
            "pass": False,
            "reason": "no exterior samples available",
            "corner_miss_count": len(corner_misses),
            "wall_miss_count": len(wall_misses),
        }
    if len(corner_depths) != len(wall_depths):
        return {
            "pass": False,
            "reason": "corner and source wall yielded different sample counts",
            "corner_miss_count": len(corner_misses),
            "wall_miss_count": len(wall_misses),
        }
    corner_sorted, wall_sorted = sorted(corner_depths), sorted(wall_depths)
    corner_median = corner_sorted[len(corner_sorted) // 2]
    wall_median = wall_sorted[len(wall_sorted) // 2]
    corner_max, wall_max = max(corner_depths), max(wall_depths)
    # Both sides originate from the same pinned source mesh and the reference
    # uses the derivation transform verbatim.  A millimetre is the largest
    # acceptable numerical difference; a statistical similarity threshold
    # could hide a missing facade or a changed brick profile.
    tolerance = 0.001
    deltas = [abs(corner - wall) for corner, wall in zip(corner_depths, wall_depths)]
    maximum_delta = max(deltas, default=float("inf"))
    source_span = max(wall_depths) - min(wall_depths)
    source_relief_minimum = 0.01
    return {
        "samples": len(corner_depths),
        "corner_miss_count": len(corner_misses),
        "wall_miss_count": len(wall_misses),
        "corner_projection_m": {"median": corner_median, "max": corner_max},
        "wall_projection_m": {"median": wall_median, "max": wall_max},
        "maximum_sample_delta_m": maximum_delta,
        "tolerance_m": tolerance,
        "source_projection_span_m": source_span,
        "source_relief_span_minimum_m": source_relief_minimum,
        "source_has_relief": source_span >= source_relief_minimum,
        "pass": (
            not corner_misses
            and not wall_misses
            and maximum_delta <= tolerance
            and source_span >= source_relief_minimum
        ),
    }


def interior_projection_coordinates(tree, arm, width, height, lod):
    """Return point-by-point inner visible-face hits and their normals."""
    samples = profile_samples(width, height, lod)
    coordinates = []
    for distance, z in samples:
        if arm == "x":
            hit = tree.ray_cast(Vector((distance, 10.0, z)), Vector((0.0, -1.0, 0.0)), 20.0)
            coordinate = hit[0].y if hit[0] is not None else None
            normal = hit[1].y if hit[1] is not None else None
        else:
            hit = tree.ray_cast(Vector((10.0, distance, z)), Vector((-1.0, 0.0, 0.0)), 20.0)
            coordinate = hit[0].x if hit[0] is not None else None
            normal = hit[1].x if hit[1] is not None else None
        coordinates.append((coordinate, normal))
    return samples, coordinates


def compare_interior_facade(facade_tree, source_limestone_tree, arm, width, height, lod):
    """Require every source limestone hit to survive the governed mirror.

    Mortar backing is deliberately excluded.  A source point that is mortar is
    allowed to miss on both the source-limestone and inner-facade trees; every
    actual limestone point must reappear at its mirrored position within 1mm.
    """
    samples, interior_coordinates = interior_projection_coordinates(
        facade_tree, arm, width, height, lod
    )
    source_samples = profile_samples(width, height, lod)
    # `exterior_projection_depths` currently emits depth values and a separate
    # miss list. Re-run on the shared grid to retain correspondence by index.
    source_coordinates = []
    for distance, z in source_samples:
        if arm == "x":
            hit = source_limestone_tree.ray_cast(Vector((distance, -10.0, z)), Vector((0.0, 1.0, 0.0)), 20.0)
            coordinate = hit[0].y if hit[0] is not None else None
            normal = hit[1].y if hit[1] is not None else None
        else:
            hit = source_limestone_tree.ray_cast(Vector((-10.0, distance, z)), Vector((1.0, 0.0, 0.0)), 20.0)
            coordinate = hit[0].x if hit[0] is not None else None
            normal = hit[1].x if hit[1] is not None else None
        source_coordinates.append((coordinate, normal))
    tolerance = 0.001
    expected_plane = 0.72 + INTERIOR_FACADE_CLEARANCE_M
    source_hits = 0
    misses, extraneous, normal_mismatches, deltas = 0, 0, 0, []
    for (source_coordinate, source_normal), (interior_coordinate, interior_normal) in zip(source_coordinates, interior_coordinates):
        if source_coordinate is None:
            if interior_coordinate is not None:
                extraneous += 1
            continue
        source_hits += 1
        if interior_coordinate is None:
            misses += 1
            continue
        # The direct source ray hits an exterior-facing quad.  Its mirror is
        # visible from inside the elbow and therefore has the opposite normal.
        # This catches a copied back/side volume even when its projected depth
        # happens to match the intended profile.
        # Bevel samples are oblique. Compare the mirrored normal component,
        # rather than insisting every hit be a front polygon with |n|>.95.
        if source_normal is None or interior_normal is None or source_normal >= 0 or interior_normal <= 0 or abs(source_normal + interior_normal) > 0.001:
            normal_mismatches += 1
        deltas.append(abs(interior_coordinate - (expected_plane - source_coordinate)))
    maximum_delta = max(deltas, default=float("inf"))
    return {
        "samples": len(samples),
        "source_limestone_hits": source_hits,
        "missing_mirrored_limestone_samples": misses,
        "extraneous_inner_samples": extraneous,
        "visible_face_normal_mismatch_count": normal_mismatches,
        "maximum_sample_delta_m": maximum_delta,
        "tolerance_m": tolerance,
        "mirror_plane_plus_clearance_m": expected_plane,
        "pass": (
            source_hits > 0
            and misses == 0
            and extraneous == 0
            and normal_mismatches == 0
            and maximum_delta <= tolerance
        ),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--corner-glb", type=Path, required=True)
    parser.add_argument("--wall-glb", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--width", type=float, default=4.0)
    parser.add_argument("--height", type=float, default=3.2)
    parser.add_argument("--thickness", type=float, default=0.72)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])
    args.output = args.output.resolve()
    if min(args.width, args.height, args.thickness) <= 0:
        raise ValueError("positive wall dimensions required")
    args.output.mkdir(parents=True, exist_ok=False)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(args.corner_glb.resolve()))
    corner_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    bpy.ops.import_scene.gltf(filepath=str(args.wall_glb.resolve()))
    wall_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj not in corner_meshes]
    # The pinned historical wall is inside-out (all six directions were
    # independently measured). Use its unchanged positions/UVs as the profile
    # reference, with outward winding for the normal comparison. Record this
    # explicitly; it is not a promotion or a repair of the pinned GLB on disk.
    reference_winding = []
    for obj in wall_meshes:
        if obj.name.startswith("UCX_"):
            continue
        old_normals = [p.normal.copy() for p in obj.data.polygons]
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.normals_split_custom_set([(0.0, 0.0, 0.0)] * len(obj.data.loops))
        obj.data.update()
        reference_winding.append({"mesh": obj.name, "reoriented_faces": sum(a.dot(b.normal) < 0 for a, b in zip(old_normals, obj.data.polygons))})
    wall_colliders = [obj for obj in wall_meshes if obj.name.startswith("UCX_")]
    if len(wall_colliders) != 1:
        raise RuntimeError("the governed straight-wall reference must contain exactly one UCX mesh")
    source_collision_minimum, source_collision_maximum = world_bounds(wall_colliders)
    source_collision_centre = (source_collision_minimum + source_collision_maximum) * 0.5
    corner_materials = material_contract(corner_meshes)
    wall_materials = material_contract(wall_meshes)
    materials_match = corner_materials == wall_materials
    if not materials_match:
        raise RuntimeError("corner texture pixels, dimensions or PBR node maps differ from the governed straight wall")
    results, render_objects = [], []
    for lod in range(3):
        corner = [obj for obj in corner_meshes if f"_LOD{lod}_" in obj.name]
        wall = [obj for obj in wall_meshes if f"_LOD{lod}_" in obj.name]
        if not corner or not wall:
            raise RuntimeError(f"missing LOD{lod} corner or wall")
        # Corner occupies +X/+Y in Blender's horizontal plane from its exterior
        # pivot. Append a straight module to each arm; rotate the Y arm +90°
        # around Blender's vertical Z axis.
        scene_objects = duplicate_lod(corner, lod, (0.0, 0.0, 0.0))
        scene_objects += duplicate_lod(wall, lod, (args.width * 1.5, args.thickness * 0.5, 0.0))
        scene_objects += duplicate_lod(wall, lod, (args.thickness * 0.5, args.width * 1.5, 0.0), math.pi * 0.5)
        bpy.context.view_layer.update()
        tree = mesh_tree(scene_objects)
        # Separate straight-wall references quantify the exterior profile
        # without helping any join or elbow ray pass.  They reproduce the
        # exact transforms used by the derivation worker, including ArmY's
        # mirror.  The old translation-only reference was offset by 0.36m.
        reference_x_transform = Matrix.Translation(
            Vector((args.width * 0.5, args.thickness * 0.5, source_collision_centre.z))
        ) @ Matrix.Translation(-source_collision_centre)
        reference_y_transform = (
            Matrix.Translation(Vector((args.thickness * 0.5, args.width * 0.5, source_collision_centre.z)))
            @ Matrix.Rotation(-math.pi * 0.5, 4, "Z")
            @ Matrix.Scale(-1.0, 4, Vector((1.0, 0.0, 0.0)))
            @ Matrix.Translation(-source_collision_centre)
        )
        reference_x = duplicate_lod_with_matrix(wall, reference_x_transform)
        reference_y = duplicate_lod_with_matrix(wall, reference_y_transform)
        bpy.context.view_layer.update()
        reference_x_tree = mesh_tree(reference_x)
        reference_y_tree = mesh_tree(reference_y)
        if lod < 2:
            facade_x = [obj for obj in corner if obj.name.endswith("_ArmX_InteriorFacade")]
            facade_y = [obj for obj in corner if obj.name.endswith("_ArmY_InteriorFacade")]
            if len(facade_x) != 1 or len(facade_y) != 1:
                raise RuntimeError(f"LOD{lod} must contain one limestone interior facade per arm")
            source_x_limestone_tree = mesh_tree(reference_x, material_name="R1063_Limestone")
            source_y_limestone_tree = mesh_tree(reference_y, material_name="R1063_Limestone")
            interior_x_tree = mesh_tree(facade_x, material_name="R1063_Limestone")
            interior_y_tree = mesh_tree(facade_y, material_name="R1063_Limestone")
        z_values = [0.1 + (args.height - 0.2) * index / 60 for index in range(61)]
        band = [index * 0.005 for index in range(-10, 11)]
        joins = []
        for arm, coordinate in (("x", args.width), ("y", args.width)):
            for side in (-1, 1):
                if arm == "x":
                    samples = [(coordinate + delta, z) for delta in band for z in z_values]
                    misses = probe_face(tree, lambda sample: Vector((sample[0], side * 10.0, sample[1])), Vector((0, -side, 0)), samples)
                else:
                    samples = [(coordinate + delta, z) for delta in band for z in z_values]
                    misses = probe_face(tree, lambda sample: Vector((side * 10.0, sample[0], sample[1])), Vector((-side, 0, 0)), samples)
                joins.append({"arm": arm, "side": side, "rays": len(samples), "miss_count": len(misses), "misses": misses})
        # Probe the exposed inner and outer elbow faces as well as the two joins.
        elbow_samples = [0.05 + (args.height - 0.1) * index / 60 for index in range(61)]
        inner_misses = probe_face(tree, lambda z: Vector((args.thickness * 0.5, args.thickness * 0.5, z)), Vector((1, 0, 0)), elbow_samples)
        outer_misses = probe_face(tree, lambda z: Vector((-10.0, -10.0, z)), Vector((1, 1, 0)).normalized(), elbow_samples)
        results.append({
            "lod": lod,
            "joins": joins,
            "inner_corner": {"rays": len(elbow_samples), "miss_count": len(inner_misses), "misses": inner_misses},
            "outer_corner": {"rays": len(elbow_samples), "miss_count": len(outer_misses), "misses": outer_misses},
            "exterior_projection_match": (
                {
                    "arm_x": compare_exterior_projection(tree, reference_x_tree, "x", args.width, args.height, lod),
                    "arm_y": compare_exterior_projection(tree, reference_y_tree, "y", args.width, args.height, lod),
                }
                if lod < 2
                else {"not_applicable": "LOD2 is the approved silhouette-only fallback"}
            ),
            "interior_limestone_projection_match": (
                {
                    "arm_x": compare_interior_facade(
                        interior_x_tree, source_x_limestone_tree, "x", args.width, args.height, lod
                    ),
                    "arm_y": compare_interior_facade(
                        interior_y_tree, source_y_limestone_tree, "y", args.width, args.height, lod
                    ),
                }
                if lod < 2
                else {"not_applicable": "LOD2 is the approved silhouette-only fallback"}
            ),
        })
        for obj in reference_x + reference_y:
            bpy.data.objects.remove(obj, do_unlink=True)
        # A deliberately displaced successor must reveal the 2 cm defect.  It
        # proves the ray probe does not merely pass because it samples no gap.
        witness_objects = duplicate_lod(corner, lod, (0.0, 0.0, 0.0)) + duplicate_lod(
            wall,
            lod,
            (args.width * 1.5 + 0.02, args.thickness * 0.5, 0.0),
        )
        bpy.context.view_layer.update()
        witness_tree = mesh_tree(witness_objects)
        witness_samples = [(args.width + 0.01, z) for z in z_values]
        witness_misses = probe_face(
            witness_tree,
            lambda sample: Vector((sample[0], -10.0, sample[1])),
            Vector((0, 1, 0)),
            witness_samples,
        )
        for obj in witness_objects:
            bpy.data.objects.remove(obj, do_unlink=True)
        results[-1]["witness_gap_2cm"] = {
            "rays": len(witness_samples),
            "miss_count": len(witness_misses),
            "must_fail": True,
        }
        render_objects.extend(scene_objects if lod == 0 else [])
    # Every LOD was instantiated for the geometry probes. Only LOD0 belongs
    # in this image: LOD1 and especially the LOD2 solid silhouette must not
    # cover its joints and turn its relief into broad overlapping panels.
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.hide_render = True
    for obj in render_objects:
        obj.hide_render = False
    visible_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and not obj.hide_render]
    if set(visible_meshes) != set(render_objects) or any("_LOD0_" not in obj.name for obj in visible_meshes):
        raise RuntimeError("composition must render exclusively its LOD0 copies")
    scene = bpy.context.scene
    use_eevee(scene)
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 1200, 800, 100
    scene.world.color = (0.3, 0.3, 0.3)
    bpy.ops.object.light_add(type="SUN")
    bpy.context.object.rotation_euler = (math.radians(35), math.radians(-20), math.radians(-25))
    bpy.context.object.data.energy = 3
    # The fill is neutral and deliberately weaker than the sun: it makes the
    # interior limestone readable while retaining directional relief shadows.
    bpy.ops.object.light_add(type="AREA", location=(2.4, 2.4, 4.8))
    fill = bpy.context.object
    fill.data.energy = 180
    fill.data.shape = "DISK"
    fill.data.size = 4.0
    fill.data.color = (1.0, 1.0, 1.0)
    fill.rotation_euler = (
        Vector((2.0, 2.0, args.height * 0.5)) - fill.location
    ).to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.camera_add(location=(11.0, -13.0, 8.0))
    camera = bpy.context.object
    target = Vector((args.width, args.width, args.height * 0.5))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type, camera.data.ortho_scale = "ORTHO", args.width * 3.5
    scene.camera = camera
    scene.render.filepath = str(args.output / "corner-lod0.png")
    bpy.ops.render.render(write_still=True)
    camera.location = (11.0, 11.0, 8.0)
    camera.rotation_euler = (Vector((args.width * 0.7, args.width * 0.7, args.height * 0.5)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(args.output / "corner-interior-lod0.png")
    bpy.ops.render.render(write_still=True)
    passed = all(
        item["inner_corner"]["miss_count"] == 0
        and item["outer_corner"]["miss_count"] == 0
        and all(join["miss_count"] == 0 for join in item["joins"])
        and item["witness_gap_2cm"]["miss_count"] > 0
        and (
            item["lod"] == 2
            or (
                all(result["pass"] for result in item["exterior_projection_match"].values())
                and all(result["pass"] for result in item["interior_limestone_projection_match"].values())
            )
        )
        for item in results
    )
    report = {
        "schema": "forge.modular-wall-corner-probe",
        "schema_version": 1,
        "corner_glb": str(args.corner_glb.resolve()),
        "corner_sha256": hashlib.sha256(args.corner_glb.read_bytes()).hexdigest(),
        "wall_glb": str(args.wall_glb.resolve()),
        "wall_sha256": hashlib.sha256(args.wall_glb.read_bytes()).hexdigest(),
        "blender": bpy.app.version_string,
        "width_m": args.width,
        "height_m": args.height,
        "thickness_m": args.thickness,
        "materials": {
            "corner": corner_materials,
            "wall": wall_materials,
            "exact_reimport_match": materials_match,
        },
        "reference_winding_repair": reference_winding,
        "rendered_meshes": sorted(obj.name for obj in visible_meshes),
        "results": results,
        "pass": passed,
        "scope": "sampled visual LOD joins and elbow faces; not Studio, Rapier or human qualification",
    }
    (args.output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"pass": passed, "lods": [item["lod"] for item in results]}))
    if not passed:
        raise RuntimeError("through-gap detected in reimported wall corner")


if __name__ == "__main__":
    main()

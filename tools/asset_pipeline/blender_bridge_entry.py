"""Bundled Blender entry point. Invoked only by blender_worker.py."""

from __future__ import annotations

import base64
import json
import math
import pathlib
import sys

import bpy
from mathutils import Vector


def _decode_payload(value: str):
    return json.loads(base64.urlsafe_b64decode(value.encode("ascii")).decode("utf-8"))


def _arguments() -> tuple[pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path, list[str], dict]:
    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 6:
        raise ValueError("Expected source, output, preview, report, operations and options.")
    operations = _decode_payload(values[4])
    options = _decode_payload(values[5])
    if not isinstance(operations, list) or not isinstance(options, dict):
        raise ValueError("Expected typed operations and options payloads.")
    return (
        pathlib.Path(values[0]),
        pathlib.Path(values[1]),
        pathlib.Path(values[2]),
        pathlib.Path(values[3]),
        operations,
        options,
    )


def _reset() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def _import_model(path: pathlib.Path) -> None:
    extension = path.suffix.lower()
    if extension == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(path))
    elif extension == ".obj":
        bpy.ops.wm.obj_import(filepath=str(path))
    elif extension == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    elif extension == ".dae":
        bpy.ops.wm.collada_import(filepath=str(path))
    elif extension in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=str(path))
    else:
        raise ValueError(f"Unsupported model format: {extension}")


def _normalize(options: dict) -> None:
    scale = float(options["scaleMeters"])
    for item in bpy.context.scene.objects:
        if item.type not in {"MESH", "ARMATURE", "EMPTY"}:
            continue
        item.scale = tuple(component * scale for component in item.scale)
        item.select_set(True)
    if options["applyTransforms"] and bpy.context.selected_objects:
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if options["pivot"] in {"ground_center", "object_center"}:
        meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
        if meshes:
            minimum, maximum = _bounds(meshes)
            center = (minimum + maximum) * 0.5
            offset = -center if options["pivot"] == "object_center" else Vector((-center.x, -center.y, -minimum.z))
            for item in [candidate for candidate in bpy.context.scene.objects if candidate.parent is None]:
                item.location += offset
            bpy.context.view_layer.update()


def _bounds(meshes: list) -> tuple[Vector, Vector]:
    coordinates = [item.matrix_world @ Vector(corner) for item in meshes for corner in item.bound_box]
    if not coordinates:
        raise ValueError("Imported model has no renderable bounds.")
    return (
        Vector(tuple(min(point[index] for point in coordinates) for index in range(3))),
        Vector(tuple(max(point[index] for point in coordinates) for index in range(3))),
    )


def _generate_lods(ratios: list[float]) -> None:
    sources = [item for item in bpy.context.scene.objects if item.type == "MESH" and "_LOD" not in item.name]
    for source in sources:
        for level, ratio in enumerate(ratios, start=1):
            duplicate = source.copy()
            duplicate.data = source.data.copy()
            duplicate.name = f"{source.name}_LOD{level}"
            bpy.context.collection.objects.link(duplicate)
            modifier = duplicate.modifiers.new(name="OpenRPG Forge LOD", type="DECIMATE")
            modifier.ratio = ratio
            bpy.context.view_layer.objects.active = duplicate
            duplicate.select_set(True)
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            duplicate.select_set(False)


def _generate_collision() -> None:
    for source in [item for item in bpy.context.scene.objects if item.type == "MESH" and "_LOD" not in item.name]:
        collision = source.copy()
        collision.data = source.data.copy()
        collision.name = f"{source.name}-col"
        bpy.context.collection.objects.link(collision)


def _look_at(item, target: Vector) -> None:
    item.rotation_euler = (target - item.location).to_track_quat("-Z", "Y").to_euler()


def _render_thumbnail(path: pathlib.Path) -> None:
    meshes = [
        item for item in bpy.context.scene.objects
        if item.type == "MESH" and "_LOD" not in item.name and not item.name.endswith("-col")
    ]
    minimum, maximum = _bounds(meshes)
    center = (minimum + maximum) * 0.5
    radius = max((maximum - minimum).length * 0.5, 0.1)
    direction = Vector((1.6, -1.8, 1.15)).normalized()
    camera_distance = max(radius / math.tan(math.radians(50) * 0.5) * 0.92, radius * 2.1)
    hidden = {}
    temporary_objects = []
    try:
        for item in bpy.context.scene.objects:
            if item.type == "MESH" and ("_LOD" in item.name or item.name.endswith("-col")):
                hidden[item.name] = item.hide_render
                item.hide_render = True

        camera_data = bpy.data.cameras.new("OpenRPG Forge Preview Camera")
        camera = bpy.data.objects.new("OpenRPG Forge Preview Camera", camera_data)
        bpy.context.collection.objects.link(camera)
        camera.location = center + direction * camera_distance
        camera.data.lens = 52
        _look_at(camera, center)
        bpy.context.scene.camera = camera
        temporary_objects.append(camera)

        for name, location, energy_factor, size, color in (
            ("OpenRPG Forge Key", center + Vector((2.2, -2.4, 3.0)) * radius, 1000, 4.0, (1.0, 0.88, 0.78)),
            ("OpenRPG Forge Fill", center + Vector((-2.5, -0.8, 1.6)) * radius, 350, 3.0, (0.68, 0.8, 1.0)),
            ("OpenRPG Forge Rim", center + Vector((0.5, 2.8, 2.2)) * radius, 600, 3.0, (1.0, 0.94, 0.84)),
        ):
            light_data = bpy.data.lights.new(name, type="AREA")
            light_data.energy = energy_factor * radius * radius
            light_data.color = color
            light_data.shape = "DISK"
            light_data.size = max(size * radius, 0.5)
            light = bpy.data.objects.new(name, light_data)
            bpy.context.collection.objects.link(light)
            light.location = location
            _look_at(light, center)
            temporary_objects.append(light)

        scene = bpy.context.scene
        scene.render.engine = "BLENDER_EEVEE_NEXT"
        scene.render.resolution_x = 512
        scene.render.resolution_y = 512
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = "PNG"
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.film_transparent = True
        scene.render.dither_intensity = 0.0
        scene.render.filepath = str(path)
        scene.render.image_settings.color_depth = "8"
        scene.eevee.taa_render_samples = 16
        scene.eevee.use_taa_reprojection = False
        scene.eevee.use_bokeh_jittered = False
        scene.eevee.use_shadow_jitter_viewport = False
        scene.view_settings.look = "AgX - Medium High Contrast"
        scene.view_settings.exposure = -0.65
        scene.world.color = (0.025, 0.02, 0.035)
        path.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.render.render(write_still=True)
    finally:
        for item in temporary_objects:
            bpy.data.objects.remove(item, do_unlink=True)
        for name, value in hidden.items():
            item = bpy.data.objects.get(name)
            if item is not None:
                item.hide_render = value


def _audit(options: dict, operations: list[str], preview: pathlib.Path) -> dict:
    base_meshes = [
        item for item in bpy.context.scene.objects
        if item.type == "MESH" and "_LOD" not in item.name and not item.name.endswith("-col")
    ]
    lod_meshes = [item for item in bpy.context.scene.objects if item.type == "MESH" and "_LOD" in item.name]
    collision_meshes = [item for item in bpy.context.scene.objects if item.type == "MESH" and item.name.endswith("-col")]
    triangles = 0
    vertices = 0
    invalid_normals = 0
    materials = set()
    for item in base_meshes:
        item.data.calc_loop_triangles()
        triangles += len(item.data.loop_triangles)
        vertices += len(item.data.vertices)
        invalid_normals += sum(
            1 for polygon in item.data.polygons if any(not math.isfinite(component) for component in polygon.normal)
        )
        materials.update(slot.material.name for slot in item.material_slots if slot.material is not None)

    images = [image for image in bpy.data.images if image.source == "FILE" and image.name != "Render Result"]
    missing_textures = []
    absolute_texture_paths = []
    for image in images:
        if image.packed_file is None and image.filepath:
            resolved = pathlib.Path(bpy.path.abspath(image.filepath))
            if not resolved.is_file():
                missing_textures.append(image.name)
            if pathlib.Path(image.filepath).is_absolute():
                absolute_texture_paths.append(image.name)

    minimum = maximum = Vector((0.0, 0.0, 0.0))
    if base_meshes:
        minimum, maximum = _bounds(base_meshes)
    dimensions = maximum - minimum
    required_lods = len(base_meshes) * len(options["lodRatios"])
    collision_required = "generate_collision" in operations or options["generateCollision"]
    preview_required = "generate_thumbnail" in operations
    checks = []

    def check(code: str, passed: bool, message: str, warning: bool = False) -> None:
        checks.append({
            "code": code,
            "status": "pass" if passed else "warning" if warning else "fail",
            "message": message,
        })

    check("asset.quality.mesh_present", bool(base_meshes), "At least one base mesh is required.")
    check("asset.quality.geometry_present", triangles > 0 and vertices > 0, "Renderable geometry is required.")
    check("asset.quality.normals_finite", invalid_normals == 0, "Mesh normals must contain finite values.")
    check("asset.quality.textures_resolved", not missing_textures, "Every referenced texture must exist.")
    check(
        "asset.quality.texture_paths_portable",
        not absolute_texture_paths,
        "Absolute texture paths are embedded during GLB export but should be normalized at source.",
        warning=True,
    )
    check(
        "asset.quality.material_present",
        bool(materials),
        "The model has no material; a neutral fallback will be used.",
        warning=True,
    )
    check(
        "asset.quality.size_coherent",
        all(math.isfinite(value) and 0 <= value <= 1000 for value in dimensions),
        "Model dimensions must be finite and at most 1000 metres per axis.",
    )
    if options["pivot"] == "ground_center":
        check("asset.quality.pivot_grounded", abs(minimum.z) <= 0.01, "Ground pivot must place minimum Z at zero.")
    if collision_required:
        check(
            "asset.quality.collision_present",
            len(collision_meshes) >= len(base_meshes),
            "A generated collision mesh is required for every base mesh.",
        )
    if "generate_lods" in operations and required_lods:
        check(
            "asset.quality.lods_present",
            len(lod_meshes) >= required_lods,
            "Every requested LOD level must be generated.",
        )
    check(
        "asset.quality.triangle_budget",
        triangles <= 500_000,
        "Base mesh exceeds the recommended 500k triangle authoring budget.",
        warning=triangles <= 2_000_000,
    )
    if preview_required:
        check("asset.quality.thumbnail_present", preview.is_file(), "A generated PNG thumbnail is required.")

    return {
        "schemaVersion": 1,
        "passed": all(item["status"] != "fail" for item in checks),
        "metrics": {
            "baseMeshCount": len(base_meshes),
            "lodMeshCount": len(lod_meshes),
            "collisionMeshCount": len(collision_meshes),
            "vertexCount": vertices,
            "triangleCount": triangles,
            "materialCount": len(materials),
            "textureCount": len(images),
            "missingTextureCount": len(missing_textures),
            "invalidNormalCount": invalid_normals,
            "dimensionsMeters": [round(value, 6) for value in dimensions],
        },
        "checks": checks,
    }


def main() -> None:
    source, output, preview, report, operations, options = _arguments()
    _reset()
    _import_model(source)
    if "normalize_model" in operations:
        _normalize(options)
    if "generate_lods" in operations:
        _generate_lods(options["lodRatios"])
    if "generate_collision" in operations or options["generateCollision"]:
        _generate_collision()
    if "generate_thumbnail" in operations:
        _render_thumbnail(preview)
    quality = _audit(options, operations, preview)
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        export_apply=True,
        export_yup=True,
        export_extras=True,
    )
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(quality, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()

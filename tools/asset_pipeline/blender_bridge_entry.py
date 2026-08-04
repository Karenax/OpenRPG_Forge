"""Bundled Blender entry point. Invoked only by blender_worker.py."""

from __future__ import annotations

import json
import pathlib
import sys

import bpy


def _arguments() -> tuple[pathlib.Path, pathlib.Path, list[str], dict]:
    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 4:
        raise ValueError("Expected source, output, operations and options.")
    return pathlib.Path(values[0]), pathlib.Path(values[1]), json.loads(values[2]), json.loads(values[3])


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
    if options["pivot"] == "ground_center":
        meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
        if meshes:
            minimum_z = min((item.matrix_world @ corner).z for item in meshes for corner in item.bound_box)
            for item in bpy.context.scene.objects:
                item.location.z -= minimum_z


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


def main() -> None:
    source, output, operations, options = _arguments()
    _reset()
    _import_model(source)
    if "normalize_model" in operations:
        _normalize(options)
    if "generate_lods" in operations:
        _generate_lods(options["lodRatios"])
    if "generate_collision" in operations or options["generateCollision"]:
        _generate_collision()
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        export_apply=True,
        export_yup=True,
        export_extras=True,
    )


if __name__ == "__main__":
    main()

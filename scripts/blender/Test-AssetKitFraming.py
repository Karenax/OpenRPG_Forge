"""Blender 4.5: validate framing from an existing kit, without rebuilding assets."""
from __future__ import annotations

import argparse
import importlib.util
import json
from pathlib import Path
import sys

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-build", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    spec = importlib.util.spec_from_file_location("worker", Path(__file__).with_name("Build-VisualValleyAssetKit.py"))
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    base = args.base_build.resolve(strict=True)
    output = args.output_dir.resolve()
    worker.require_new_output_directory(output)
    files = [base / "visual-valley-r1-063-asset-kit.blend", base / "asset-kit-build.v1.json", *sorted((base / "assets").glob("*.glb"))]
    before = {str(path): worker.sha256(path) for path in files}
    report = json.loads(files[1].read_text(encoding="utf-8"))
    recorded_version = report.get("generator", {}).get("blender_version")
    if not isinstance(recorded_version, str):
        raise RuntimeError("base build has no recorded generator Blender version")
    version_parts = recorded_version.split(".")
    if len(version_parts) < 2 or not all(part.isdecimal() for part in version_parts[:2]):
        raise RuntimeError(f"base build has invalid recorded generator Blender version: {recorded_version!r}")
    worker.require_running_blender_major_minor(
        (int(version_parts[0]), int(version_parts[1])),
        context="base build",
    )
    bpy.ops.wm.open_mainfile(filepath=str(files[0]), use_scripts=False)
    scene = bpy.context.scene
    camera = bpy.data.objects["R1063_TurntableCamera"]
    scene.camera = camera
    all_built = {}
    for asset in report["assets"]:
        objects = [obj for obj in scene.objects if obj.get("forge_asset_id") == asset["id"] and not obj.name.startswith("Preview_")]
        all_built[asset["id"]] = {
            "lods": {lod: [obj for obj in objects if obj.get("forge_role") == "render" and obj.get("forge_lod") == lod] for lod in range(3)},
            "collisions": [obj for obj in objects if obj.get("forge_role") == "collision"],
        }
    angles = list(range(0, 360, 45))
    checks = []

    def check(objects, label):
        original = {obj.name: tuple(obj.rotation_euler) for obj in objects}
        worker.camera_for_asset(camera, objects, angles)
        assert original == {obj.name: tuple(obj.rotation_euler) for obj in objects}
        projected = []
        try:
            for angle in angles:
                for obj in objects:
                    obj.rotation_euler.z = original[obj.name][2] + worker.math.radians(angle)
                bpy.context.view_layer.update()
                for obj in objects:
                    for corner in obj.bound_box:
                        point = world_to_camera_view(scene, camera, obj.matrix_world @ Vector(corner))
                        assert 0.0799 <= point.x <= 0.9201 and 0.0799 <= point.y <= 0.9201, (label, angle, tuple(point))
                        assert camera.data.clip_start < point.z < camera.data.clip_end
                        projected.append(tuple(point))
        finally:
            for obj in objects:
                obj.rotation_euler = original[obj.name]
            bpy.context.view_layer.update()
        checks.append({"asset": label, "resolution": [scene.render.resolution_x, scene.render.resolution_y], "pixel_aspect": [scene.render.pixel_aspect_x, scene.render.pixel_aspect_y], "poses": len(angles), "corners": len(projected), "minimum_image_margin": min(min(p[0], p[1], 1 - p[0], 1 - p[1]) for p in projected)})

    for width, height, pixel_x in [(1920, 1080, 1.0), (1080, 1920, 1.0), (1920, 1080, 1.5)]:
        scene.render.resolution_x, scene.render.resolution_y = width, height
        scene.render.pixel_aspect_x = pixel_x
        for asset_id, built in all_built.items():
            check(built["lods"][0], asset_id)
    # Non-uniform scaling, translation and all-axis rotations simulate an authored edit.
    clone = next(iter(all_built.values()))["lods"][0][0].copy()
    scene.collection.objects.link(clone)
    clone.location = (11.0, -7.0, 3.0)
    clone.rotation_euler = (0.35, 0.2, 0.7)
    clone.scale = (1.4, 0.6, 1.2)
    check([clone], "synthetic-authored-transform")
    bpy.data.objects.remove(clone, do_unlink=True)
    for objects, poses in [([], angles), (next(iter(all_built.values()))["lods"][0], [])]:
        try:
            worker.camera_for_asset(camera, objects, poses)
        except RuntimeError:
            pass
        else:
            raise AssertionError("empty framing input was accepted")
    scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
    scene.render.pixel_aspect_x = scene.render.pixel_aspect_y = 1.0
    for obj in scene.objects:
        if obj.type == "MESH":
            obj.hide_render = obj.name != "R1063_PresentationGround"
    renders = []
    for asset in report["assets"]:
        if asset["kind"] == "tree":
            directory = output / asset["id"]
            directory.mkdir()
            built = all_built[asset["id"]]
            original = {obj.name: tuple(obj.rotation_euler) for obj in built["lods"][0]}
            [path] = worker.render_turntable(asset, built, all_built, camera, [0], directory)
            assert original == {obj.name: tuple(obj.rotation_euler) for obj in built["lods"][0]}
            renders.append({"path": str(path), "sha256": worker.sha256(path)})
    # Exercise the production group renderer, not only the single-asset helper.
    lineup_checks = []
    source_matrices = {obj.name: tuple(tuple(row) for row in obj.matrix_world)
                       for built in all_built.values() for obj in built["lods"][0]}

    def inspect_lineup(render_scene):
        objects = [obj for obj in render_scene.objects if not obj.hide_render
                   and (obj.name.startswith("Preview_") or obj.name == "R1063_HumanScale_1_8m")]
        assert {obj.get("forge_asset_id") for obj in objects if obj.name.startswith("Preview_")} == set(all_built)
        assert any(obj.name == "R1063_HumanScale_1_8m" for obj in objects)
        points = [world_to_camera_view(render_scene, camera, obj.matrix_world @ Vector(corner))
                  for obj in objects for corner in obj.bound_box]
        assert points
        for point in points:
            assert 0.0799 <= point.x <= 0.9201 and 0.0799 <= point.y <= 0.9201, tuple(point)
            assert camera.data.clip_start < point.z < camera.data.clip_end
        lineup_checks.append({"resolution": [render_scene.render.resolution_x, render_scene.render.resolution_y],
                              "pixel_aspect_x": render_scene.render.pixel_aspect_x,
                              "corners": len(points),
                              "minimum_image_margin": min(min(p.x, p.y, 1-p.x, 1-p.y) for p in points)})

    bpy.app.handlers.render_pre.append(inspect_lineup)
    try:
        presentation = {"camera": camera, "collection": bpy.data.collections["R1063_Presentation"]}
        material = bpy.data.materials["R1063_ScaleMarker"]
        for index, (width, height, pixel_x) in enumerate([(1920, 1080, 1.0), (1080, 1920, 1.0), (1920, 1080, 1.5)]):
            scene.render.resolution_x, scene.render.resolution_y = width, height
            scene.render.pixel_aspect_x = pixel_x
            count_before = len(lineup_checks)
            path = worker.render_lineup(report["assets"], all_built, presentation, material, output / f"lineup-{index}.png")
            assert len(lineup_checks) == count_before + 1, "lineup projection check did not complete"
            assert not any(obj.name.startswith("Preview_") for obj in scene.objects), "preview objects leaked"
            assert source_matrices == {obj.name: tuple(tuple(row) for row in obj.matrix_world)
                                       for built in all_built.values() for obj in built["lods"][0]}
            renders.append({"path": str(path), "sha256": worker.sha256(path)})
    finally:
        bpy.app.handlers.render_pre.remove(inspect_lineup)
    assert before == {str(path): worker.sha256(path) for path in files}, "source files changed"
    result = {"status": "PASS", "blender": bpy.app.version_string, "worker_sha256": worker.sha256(Path(worker.__file__)), "source_files_unchanged": before, "checks": checks, "invalid_inputs_rejected": 2, "renders": renders, "scope": "DCC framing only; no asset regeneration, runtime or human acceptance"}
    result["lineup_checks"] = lineup_checks
    (output / "framing-validation.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"FORGE_ASSET_FRAMING_PASS checks={len(checks)} poses={sum(c['poses'] for c in checks)} renders={len(renders)}")


if __name__ == "__main__":
    main()

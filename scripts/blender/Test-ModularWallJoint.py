#!/usr/bin/env python3
"""Reimport a wall GLB and probe straight joints at each LOD, without editing it."""

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--glb", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--width", type=float, default=4.0)
    parser.add_argument("--height", type=float, default=3.2)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if args.width <= 0 or args.height <= 0.2:
        raise ValueError("positive module width and height > 0.2 m required")
    args.output.mkdir(parents=True, exist_ok=False)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(args.glb.resolve()))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    results = []
    pairs = []
    for lod in range(3):
        sources = [obj for obj in meshes if f"_LOD{lod}_" in obj.name]
        if not sources:
            raise RuntimeError(f"missing LOD{lod}")
        vertices, polygons = [], []
        pair = []
        for offset in (0.0, args.width):
            for source in sources:
                obj = source.copy()
                obj.data = source.data.copy()
                bpy.context.scene.collection.objects.link(obj)
                obj.location.x += offset
                pair.append(obj)
        bpy.context.view_layer.update()
        for obj in pair:
            start = len(vertices)
            vertices.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
            polygons.extend(tuple(start + index for index in face.vertices) for face in obj.data.polygons)
        tree = BVHTree.FromPolygons(vertices, polygons)
        misses = []
        count = 0
        for side in (-1, 1):
            for ix in range(-10, 11):
                for iz in range(61):
                    x = args.width * 0.5 + ix * 0.005
                    z = 0.1 + (args.height - 0.2) * iz / 60
                    hit = tree.ray_cast(Vector((x, side * 10.0, z)), Vector((0, -side, 0)), 20.0)
                    count += 1
                    if hit[0] is None:
                        misses.append([side, x, z])
        results.append({"lod": lod, "rays": count, "miss_count": len(misses), "misses": misses})
        pairs.append(pair)
    for obj in meshes:
        obj.hide_render = True
    for lod, pair in enumerate(pairs):
        for obj in pair:
            obj.hide_render = lod != 0
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 650
    scene.render.resolution_percentage = 100
    scene.world.color = (0.3, 0.3, 0.3)
    bpy.ops.object.light_add(type="SUN")
    bpy.context.object.rotation_euler = (math.radians(35), math.radians(-20), math.radians(-25))
    bpy.context.object.data.energy = 3
    bpy.ops.object.camera_add(location=(args.width * 0.5, -12, args.height * 0.6))
    camera = bpy.context.object
    target = Vector((args.width * 0.5, 0, args.height * 0.5))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = args.width * 2.2
    scene.camera = camera
    scene.render.filepath = str(args.output / "joint-lod0.png")
    bpy.ops.render.render(write_still=True)
    report = {
        "schema": "forge.modular-wall-joint-probe", "schema_version": 1,
        "source": str(args.glb.resolve()),
        "sha256": hashlib.sha256(args.glb.read_bytes()).hexdigest(),
        "blender": bpy.app.version_string, "width_m": args.width, "height_m": args.height,
        "results": results, "pass": all(item["miss_count"] == 0 for item in results),
        "scope": "sampled straight joint; not corners, Studio, physics or human qualification",
    }
    (args.output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"pass": report["pass"], "results": [{k: v for k, v in r.items() if k != "misses"} for r in results]}))
    if not report["pass"]:
        raise RuntimeError("through-gap detected in reimported wall joint")


if __name__ == "__main__":
    main()

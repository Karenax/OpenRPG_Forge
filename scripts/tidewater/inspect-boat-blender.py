"""Reopen the acquired GLB and render its source origin/anchors with Blender 5.2.

The source WGSL materials remain separate. This inspects geometry and transforms;
it cannot qualify the source appearance, gameplay, Studio or interactive controls.
"""
import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


def digest(data):
    return hashlib.sha256(data).hexdigest()


def checked(root, ref):
    name = ref["file"]
    if Path(name).name != name:
        raise ValueError("Unsafe package filename")
    data = (root / name).read_bytes()
    if len(data) != ref["bytes"] or digest(data) != ref["sha256"]:
        raise ValueError("Package payload changed")
    return data


def source_matrix(columns):
    return Matrix([[columns[c * 4 + r] for c in range(4)] for r in range(4)])


def minmax(points):
    return {"min": [min(p[i] for p in points) for i in range(3)], "max": [max(p[i] for p in points) for i in range(3)]}


def colored_material(name, color):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    material.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (*color, 1)
    return material


def rod(name, start, end, color, radius=0.025):
    direction = Vector(end) - Vector(start)
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=direction.length, location=(Vector(start) + Vector(end)) / 2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    obj.data.materials.append(colored_material(name, color))


def label(name, location, color):
    bpy.ops.object.text_add(location=location)
    obj = bpy.context.object
    obj.name = "inspection-label-" + name
    obj.data.body = name
    obj.data.size = 0.19
    obj.data.materials.append(colored_material(obj.name, color))
    direction = bpy.context.scene.camera.location - obj.location
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if bpy.app.version[:2] != (5, 2):
        raise RuntimeError("This inspection requires the configured Blender 5.2")
    args.output.mkdir(parents=False, exist_ok=False)
    manifest_bytes = (args.package / "manifest.json").read_bytes()
    manifest = json.loads(manifest_bytes)
    boat = json.loads(checked(args.package, manifest["boat"]))
    checked(args.package, manifest["inspection_glb"])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.ops.import_scene.gltf(filepath=str(args.package / manifest["inspection_glb"]["file"]))
    bpy.context.view_layer.update()
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 12:
        raise ValueError("GLB did not reopen as twelve visible mesh parts")
    # glTF +Y up -> Blender +Z up. Preserve this explicit coordinate transform.
    conversion = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    node_rows = {node["id"]: node for node in boat["nodes"]}
    part_reports, source_points, reopened_points = [], [], []
    for part in boat["parts"]:
        obj = bpy.data.objects.get(part["id"])
        if obj is None or obj.type != "MESH":
            raise ValueError("Missing original mesh node")
        obj.data.calc_loop_triangles()
        triangles = len(obj.data.loop_triangles)
        if triangles != part["triangles"]:
            raise ValueError("Triangle count changed on reopening")
        # Blender also converts mesh-local positions, so its object transforms
        # change basis by conjugation rather than receiving a root C*M matrix.
        expected = conversion @ source_matrix(node_rows[part["node"]]["matrix_world"]) @ conversion.inverted()
        error = max(abs(expected[r][c] - obj.matrix_world[r][c]) for r in range(4) for c in range(4))
        if error > 2e-6:
            raise ValueError(f"Node transform changed: {part['id']}: {error}")
        raw = checked(args.package, part["attributes"]["position"])
        original = [expected @ (conversion @ Vector(p)) for p in struct.iter_unpack("<fff", raw)]
        reopened = [obj.matrix_world @ v.co for v in obj.data.vertices]
        original_bounds, reopened_bounds = minmax(original), minmax(reopened)
        bounds_error = max(abs(original_bounds[k][i] - reopened_bounds[k][i]) for k in ("min", "max") for i in range(3))
        if bounds_error > 2e-5:
            raise ValueError("Bounds changed on reopening")
        if len(obj.data.materials) != 1 or obj.data.materials[0].name != part["material"]:
            raise ValueError("Source material slot mapping changed")
        source_points.extend(original)
        reopened_points.extend(reopened)
        part_reports.append({"id": part["id"], "triangles": triangles, "reopened_vertices": len(obj.data.vertices), "transform_max_error": error, "bounds_max_error_m": bounds_error, "bounds_blender_m": reopened_bounds, "material": obj.data.materials[0].name})
    for node in boat["nodes"]:
        obj = bpy.data.objects.get(node["id"])
        if obj is None or (obj.parent.name if obj.parent else None) != node["parent"]:
            raise ValueError("Original hierarchy was not preserved")
    result = {"schema": "forge.tidewater.boat-blender-inspection", "version": 1, "status": "geometry-reopen-pass",
              "blender": {"version": bpy.app.version_string, "build_hash": bpy.app.build_hash.decode()},
              "manifest_sha256": digest(manifest_bytes), "glb_sha256": manifest["inspection_glb"]["sha256"],
              "mesh_parts": len(meshes), "triangles": sum(row["triangles"] for row in part_reports), "hierarchy_nodes": len(boat["nodes"]),
              "source_to_blender_rows": [list(row) for row in conversion], "source_bounds_blender_m": minmax(source_points), "reopened_bounds_blender_m": minmax(reopened_points), "parts": part_reports,
              "limits": {"appearance": "inspection PBR parameters and vertex colors; source procedural WGSL pending", "anchor_orientation": "source_declared controls; other point orientations unverified", "studio": "not tested", "runtime": "not tested"}}
    (args.output / "geometry.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(9, 12, 7.5))
    scene.camera = bpy.context.object
    scene.camera.rotation_euler = (Vector((0, 0, 0.9)) - scene.camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera.data.type = "ORTHO"
    scene.camera.data.ortho_scale = 13
    camera_basis = scene.camera.rotation_euler.to_matrix()
    camera_right, camera_up, toward_camera = [camera_basis.col[i].copy() for i in range(3)]
    center = Vector((0, 0, 0.9))
    def overlay_position(x, y):
        return center + camera_right * x + camera_up * y + toward_camera * 5.0
    # Display a translated orientation reference with a leader to the actual
    # source origin; callouts stay in front of the hull for legibility.
    gizmo = overlay_position(-5.2, -2.2)
    rod("source-origin-leader", (0, 0, 0), gizmo, (0.6, 0.6, 0.6), 0.014)
    label("repere source / origine eau", overlay_position(-5.9, -3.2), (0.9, 0.9, 0.9))
    for name, direction, color in [("source +X port", (1.7, 0, 0), (0.9, 0.08, 0.08)), ("source +Y up", (0, 0, 1.7), (0.08, 0.65, 0.14)), ("source +Z forward", (0, -1.7, 0), (0.08, 0.3, 0.9))]:
        end = gizmo + Vector(direction) * 0.6
        rod(name, gizmo, end, color)
        label(name.split()[1], end, color)
    rendered_anchors = []
    callouts = {"helmEye": (-5.8, 2.4), "boardPoint": (3.7, 0.6), "propeller": (3.7, -2.4), "rudder": (1.0, -3.3)}
    for name in ("helmEye", "boardPoint", "propeller", "rudder"):
        position = conversion @ Vector(boat["anchors"][name])
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.09, location=position)
        bpy.context.object.name = "inspection-anchor-" + name
        bpy.context.object.data.materials.append(colored_material(name, (1, 0.32, 0.05)))
        callout = overlay_position(*callouts[name])
        rod("inspection-leader-" + name, position, callout, (1, 0.32, 0.05), 0.012)
        label(name, callout, (1, 0.32, 0.05))
        rendered_anchors.append({"name": name, "source_position_m": boat["anchors"][name], "blender_position_m": list(position), "orientation": "unverified"})
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.9))
    bpy.context.object.name = "inspection-ground"
    bpy.context.object.data.materials.append(colored_material("inspection-ground", (0.07, 0.09, 0.12)))
    world = bpy.data.worlds.new("inspection-world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.4, 0.45, 0.55, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5
    scene.world = world
    bpy.ops.object.light_add(type="AREA", location=(4, 3, 9))
    bpy.context.object.data.energy = 1800
    bpy.context.object.data.shape = "DISK"
    bpy.context.object.data.size = 8
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(args.output / "boat-origin-anchors.png")
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output / "boat-inspection.blend"))
    bpy.ops.render.render(write_still=True)
    result["render"] = {"file": "boat-origin-anchors.png", "sha256": digest((args.output / "boat-origin-anchors.png").read_bytes()), "anchors": rendered_anchors, "inspector": "agent visual inspection pending"}
    (args.output / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": result["status"], "mesh_parts": 12, "triangles": result["triangles"], "render": str(args.output / "boat-origin-anchors.png")}))


main()

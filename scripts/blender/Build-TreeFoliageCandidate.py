"""Isolated original foliage study. Never promotes or overwrites the source kit."""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import random
import struct
import sys

import bpy
from mathutils import Vector


def conifer_spray_sites(endpoints, height):
    """Sites along existing branch outer halves; shared by every candidate LOD."""
    assert len(endpoints) % 2 == 1
    sites = []
    for index in range(0, len(endpoints) - 1, 2):
        middle, tip = endpoints[index:index + 2]
        for fraction in (0.0, 0.33, 0.67, 1.0):
            sites.append(middle.lerp(tip, fraction))
    # Cover the existing leader instead of leaving a bare spike above the crown.
    top = endpoints[-1]
    sites.extend(top - Vector((0, 0, height * 0.11 * index / 7)) for index in range(8))
    return sites


def foliage(worker, asset, lod, materials, collection, canopy_profile="legacy", conifer_area_scale=1.0):
    """Two triangles per twisted leaf, twice the original leaf count.

    Reuse the original atlas, seed and branch endpoints. This is a DCC candidate,
    not the production generator or a new canonical vegetation representation.
    """
    oak = asset["generator_profile"] == "broadleaf_oak"
    width, depth, height = asset["dimensions_m"]
    radius = max(width, depth) * 0.5
    _, _, endpoints = worker.tree_skeleton(asset["seed"], height, radius, asset["generator_profile"], lod)
    count = ((80, 30, 10) if oak else (38, 14, 5))[lod] * 2
    length = ((0.29, 0.42, 0.64) if oak else (0.42, 0.62, 0.96))[lod]
    half_width = length * (0.48 if oak else 0.1)
    spread = radius * (((0.18, 0.23, 0.31) if oak else (0.13, 0.17, 0.22))[lod])
    sprays = canopy_profile == "branch_sprays" and not oak
    if canopy_profile in ("coherent", "branch_sprays"):
        # Preserve crown attachment centres across LODs, without increasing card counts.
        # Far cards represent leaf groups, with equal nominal surface area at every LOD.
        _, _, reference = worker.tree_skeleton(asset["seed"], height, radius, asset["generator_profile"], 0)
        original_site_count = len(reference)
        if sprays:
            reference = conifer_spray_sites(reference, height)
        count = max(1, len(endpoints) * count // len(reference))
        endpoints = reference
        near_count = (80 if oak else 38) * 2
        length = (0.29 if oak else 0.42) * 1.4 * math.sqrt(near_count * original_site_count / (count * len(reference)))
        half_width = length * (0.48 if oak else 0.1)
        spread = radius * (0.18 if oak else 0.13)
    if not oak:
        length *= math.sqrt(conifer_area_scale)
        half_width *= math.sqrt(conifer_area_scale)
    rng = random.Random(asset["seed"] + 7001 + lod * 131)
    vertices, faces, tones = [], [], []
    for endpoint in endpoints:
        for _ in range(count):
            radial = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.55, 0.75)))
            if radial.length_squared < 0.01:
                radial = Vector((1, 0, 0))
            center = endpoint + radial.normalized() * spread * rng.random() ** 0.55
            yaw = rng.random() * math.tau
            if sprays:
                # Flat, outward-facing sprays rather than spherical needle clouds.
                branch_yaw = math.atan2(endpoint.y, endpoint.x)
                outward = Vector((math.cos(branch_yaw), math.sin(branch_yaw), 0))
                lateral = Vector((-outward.y, outward.x, 0))
                center = endpoint + outward * radial.x * spread * 0.55 + lateral * radial.y * spread
                center.z += radial.z * spread * 0.25
                yaw = branch_yaw + (yaw / math.tau - 0.5) * 1.3
            forward = Vector((math.cos(yaw), math.sin(yaw), math.sin(rng.uniform(-0.7, 0.7)))).normalized()
            side = Vector((-math.sin(yaw), math.cos(yaw), rng.uniform(-0.18, 0.18))).normalized()
            normal = forward.cross(side).normalized()
            fold = normal * half_width * rng.uniform(0.12, 0.28)
            base = len(vertices)
            vertices.extend(tuple(p) for p in (
                center - forward * length * 0.52 - side * half_width,
                center - forward * length * 0.52 + side * half_width,
                center + forward * length * 0.58 + side * half_width + fold,
                center + forward * length * 0.58 - side * half_width - fold,
            ))
            faces.extend([(base, base + 1, base + 2), (base, base + 2, base + 3)])
            tone = rng.random()
            tones.extend([1 if tone < 0.56 else (0 if tone < 0.78 else 2)] * 2)
    obj = worker.mesh_object(f"{asset['id']}_LOD{lod}_FoliageCandidate", vertices, faces, collection, materials, tones, smooth=False)
    uv = obj.data.uv_layers.new(name="UVMap")
    pattern = ((0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0))
    for polygon in obj.data.polygons:
        for vertex, loop in zip(polygon.vertices, polygon.loop_indices, strict=True):
            uv.data[loop].uv = pattern[vertex % 4]
    worker.mark_asset_object(obj, asset["id"], "render", lod)
    return obj


def mesh_hash(obj):
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(struct.pack("<3f", *vertex.co))
    for polygon in obj.data.polygons:
        digest.update(struct.pack("<2I", len(polygon.vertices), polygon.material_index))
        digest.update(struct.pack(f"<{len(polygon.vertices)}I", *polygon.vertices))
    for loop in obj.data.uv_layers["UVMap"].data:
        digest.update(struct.pack("<2f", *loop.uv))
    return digest.hexdigest()


def wood_candidate(worker, source, lod, collection):
    """Offline derivative only: retain LOD0, simplify copies of distant wood."""
    assert lod in (1, 2) and source.type == "MESH"
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = source.name + "_WoodCandidate"
    collection.objects.link(obj)
    modifier = obj.modifiers.new("CandidateWoodReduction", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = (1.0, 0.35, 0.25)[lod]
    modifier.use_collapse_triangulate = True
    worker.apply_modifier(obj, modifier.name)
    before = worker.object_stats([source])["triangles"]
    after = worker.object_stats([obj])["triangles"]
    assert 0 < after < before, "wood reduction failed"
    assert obj.data.uv_layers.get("UVMap") is not None
    assert list(obj.data.materials) == list(source.data.materials)
    assert obj.matrix_world == source.matrix_world
    assert all(math.isfinite(value) for vertex in obj.data.vertices for value in vertex.co)
    return obj


def validate_candidate_source(manifest, base_report, manifest_sha256, blend_sha256):
    if base_report["generator"]["input_manifest_sha256"] != manifest_sha256:
        raise RuntimeError("candidate manifest hash mismatch")
    if base_report["blend"]["sha256"] != blend_sha256:
        raise RuntimeError("candidate blend hash mismatch")
    authored_assets = set(base_report.get("authored_source", {}).get("assets", []))
    tree_ids = {asset["id"] for asset in manifest["assets"] if asset["kind"] == "tree"}
    if tree_ids & authored_assets:
        raise RuntimeError("candidate cannot replace authored trees; an explicit source rebase is required")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-build", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--canopy-profile", choices=("legacy", "coherent", "branch_sprays"), default="legacy")
    parser.add_argument("--wood-profile", choices=("source", "reduced"), default="source")
    parser.add_argument("--conifer-area-scale", type=float, default=1.0)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if not math.isfinite(args.conifer_area_scale) or not 0.25 <= args.conifer_area_scale <= 1.0:
        parser.error("conifer area scale must be finite and between 0.25 and 1.0")
    spec = importlib.util.spec_from_file_location("worker", Path(__file__).with_name("Build-VisualValleyAssetKit.py"))
    worker = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(worker)
    manifest = worker.load_manifest(args.manifest)
    worker.require_running_blender_major_minor(
        worker.blender_major_minor(manifest["generator"]["blender_version"], field="generator.blender_version"),
        context=f"asset-kit manifest {args.manifest.name}",
    )
    base = args.base_build.resolve(strict=True)
    base_report = json.loads((base / "asset-kit-build.v1.json").read_text(encoding="utf-8"))
    validate_candidate_source(manifest, base_report, worker.sha256(args.manifest),
                              worker.sha256(base / "visual-valley-r1-063-asset-kit.blend"))
    output = args.output_dir.resolve()
    worker.require_new_output_directory(output)
    source_files = [args.manifest, base / "asset-kit-build.v1.json", base / "visual-valley-r1-063-asset-kit.blend", *sorted((base / "assets").glob("*.glb"))]
    source_hashes = {str(path): worker.sha256(path) for path in source_files}
    bpy.ops.wm.open_mainfile(filepath=str(base / "visual-valley-r1-063-asset-kit.blend"))
    scene = bpy.context.scene
    for obj in scene.objects:
        if obj.type == "MESH":
            obj.hide_render = obj.name != "R1063_PresentationGround"
    camera = bpy.data.objects["R1063_TurntableCamera"]
    scene.camera = camera
    all_built, results, far_sources = {}, [], {}
    for asset in manifest["assets"]:
        if asset["kind"] != "tree":
            continue
        objects = [obj for obj in scene.objects if obj.get("forge_asset_id") == asset["id"] and not obj.name.startswith("Preview_")]
        built = {"lods": {}, "collisions": [obj for obj in objects if obj.get("forge_role") == "collision"]}
        counts = []
        for lod in range(3):
            old = [obj for obj in objects if obj.get("forge_role") == "render" and obj.get("forge_lod") == lod]
            leaves = next(obj for obj in old if "Foliage" in obj.name)
            collection = leaves.users_collection[0]
            new = foliage(worker, asset, lod, list(leaves.data.materials), collection, args.canopy_profile, args.conifer_area_scale)
            duplicate = foliage(worker, asset, lod, list(leaves.data.materials), collection, args.canopy_profile, args.conifer_area_scale)
            assert mesh_hash(new) == mesh_hash(duplicate), "non-deterministic geometry"
            mesh = duplicate.data
            bpy.data.objects.remove(duplicate, do_unlink=True)
            bpy.data.meshes.remove(mesh)
            wood = [obj for obj in old if obj != leaves]
            wood_before = worker.object_stats(wood)
            if lod == 2:
                far_sources[asset["id"]] = wood + [new]
            if args.wood_profile == "reduced" and lod > 0:
                reduced = []
                for source in wood:
                    original_hash = mesh_hash(source)
                    part = wood_candidate(worker, source, lod, collection)
                    repeat = wood_candidate(worker, source, lod, collection)
                    assert mesh_hash(part) == mesh_hash(repeat), "non-deterministic wood reduction"
                    assert mesh_hash(source) == original_hash, "source wood changed"
                    mesh = repeat.data
                    bpy.data.objects.remove(repeat, do_unlink=True)
                    bpy.data.meshes.remove(mesh)
                    reduced.append(part)
                wood = reduced
            built["lods"][lod] = wood + [new]
            previous = worker.object_stats(old)
            current = worker.object_stats(built["lods"][lod])
            assert current["triangles"] <= previous["triangles"]
            counts.append({"lod": lod, "before": previous, "candidate": current, "foliage_mesh_sha256": mesh_hash(new),
                           "wood_before": wood_before, "wood_candidate": worker.object_stats(wood),
                           "wood_mesh_sha256": [mesh_hash(obj) for obj in wood],
                           "foliage_cards": len(new.data.polygons) // 2,
                           "foliage_surface_m2": sum(polygon.area for polygon in new.data.polygons)})
        if args.canopy_profile in ("coherent", "branch_sprays"):
            near_area = counts[0]["foliage_surface_m2"]
            assert all(0.9 <= item["foliage_surface_m2"] / near_area <= 1.1 for item in counts), "LOD foliage area mismatch"
        validation = worker.validate_asset(asset, built)
        all_built[asset["id"]] = built
        glb = output / f"{asset['id']}.glb"
        worker.export_asset_glb(asset["id"], built, glb)
        repeated_glb = output / f"{asset['id']}-repeat.glb"
        worker.export_asset_glb(asset["id"], built, repeated_glb)
        assert worker.sha256(glb) == worker.sha256(repeated_glb), "same-scene GLB exports differ"
        results.append({"id": asset["id"], "counts": counts, "validation": validation, "glb_sha256": worker.sha256(glb), "same_scene_repeat_export_identical": True})
    for asset in manifest["assets"]:
        if asset["id"] not in all_built:
            continue
        folder = output / asset["id"]
        folder.mkdir()
        worker.render_turntable(asset, all_built[asset["id"]], all_built, camera, [0, 90], folder)
        if args.wood_profile == "reduced":
            # Same camera/framing and unchanged foliage for the far-wood A/B.
            for label, parts in [("source", far_sources[asset["id"]]),
                                 ("reduced", all_built[asset["id"]]["lods"][2])]:
                for obj in scene.objects:
                    if obj.type == "MESH":
                        obj.hide_render = obj.name != "R1063_PresentationGround"
                for obj in parts:
                    obj.hide_render = False
                scene.render.filepath = str(folder / f"far-wood-{label}.png")
                bpy.ops.render.render(write_still=True)
            for obj in parts:
                obj.hide_render = True
    assert source_hashes == {str(path): worker.sha256(path) for path in source_files}
    result = {"schema": "forge.foliage-candidate-proof", "schema_version": 1, "blender": bpy.app.version_string, "script_sha256": worker.sha256(Path(__file__)), "worker_sha256": worker.sha256(Path(worker.__file__)), "provenance": manifest["provenance"], "source_files_unchanged": source_hashes, "assets": results, "runtime_promoted": False, "human_accepted": False, "scope": "deterministic mesh generation, same-scene repeat exports and DCC renders; not independent rebuild, runtime or human qualification"}
    result["canopy_profile"] = args.canopy_profile
    result["wood_profile"] = args.wood_profile
    result["conifer_area_scale"] = args.conifer_area_scale
    (output / "candidate-proof.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    renders = 8 if args.wood_profile == "reduced" else 4
    print(f"FORGE_FOLIAGE_CANDIDATE_PASS assets=2 lods=6 renders={renders} source_unchanged=true")


if __name__ == "__main__":
    main()

"""Read-only source/candidate GLB checks, Blender reimport and source rejection cases."""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

import bpy
from mathutils import Vector


def load_module(filename):
    spec = importlib.util.spec_from_file_location(filename, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def material_signatures(path):
    data = path.read_bytes()
    assert data[:4] == b"glTF" and struct.unpack_from("<II", data, 4) == (2, len(data))
    json_size, json_kind = struct.unpack_from("<II", data, 12)
    assert json_kind == 0x4E4F534A
    document = json.loads(data[20:20 + json_size])
    binary_size, binary_kind = struct.unpack_from("<II", data, 20 + json_size)
    assert binary_kind == 0x004E4942
    binary = data[28 + json_size:]
    assert len(binary) == binary_size
    assert all("uri" not in buffer for buffer in document["buffers"])

    def texture(index):
        record = document["textures"][index]
        image = document["images"][record["source"]]
        assert "uri" not in image
        view = document["bufferViews"][image["bufferView"]]
        assert view["buffer"] == 0
        start = view.get("byteOffset", 0)
        pixels = binary[start:start + view["byteLength"]]
        assert len(pixels) == view["byteLength"]
        return {"image_sha256": hashlib.sha256(pixels).hexdigest(),
                "mime_type": image["mimeType"],
                "sampler": document.get("samplers", [])[record["sampler"]] if "sampler" in record else {}}

    def normalize(value):
        if isinstance(value, list):
            return [normalize(item) for item in value]
        if isinstance(value, dict):
            result = {}
            for key, item in value.items():
                if key.endswith("Texture") and isinstance(item, dict) and "index" in item:
                    result[key] = {**item, "index": texture(item["index"])}
                else:
                    result[key] = normalize(item)
            return result
        return value

    return {material["name"]: normalize(material) for material in document["materials"]}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--candidate-dir", type=Path, required=True)
    parser.add_argument("--base-build", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--reference-candidate", type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    worker = load_module("Build-VisualValleyAssetKit.py")
    candidate = load_module("Build-TreeFoliageCandidate.py")
    endpoints = [Vector((1, 0, 2)), Vector((4, 0, 2)), Vector((0, 0, 10))]
    sites = candidate.conifer_spray_sites(endpoints, 10)
    assert len(sites) == 12 and sites[0] == endpoints[0] and sites[3] == endpoints[1]
    assert sites[4] == endpoints[-1] and abs(sites[-1].z - 8.9) < 0.00001
    assert sites == candidate.conifer_spray_sites(endpoints, 10)
    assert endpoints == [Vector((1, 0, 2)), Vector((4, 0, 2)), Vector((0, 0, 10))]
    try:
        candidate.conifer_spray_sites(endpoints[:2], 10)
    except AssertionError:
        pass
    else:
        raise AssertionError("unpaired branch endpoints accepted")
    validator = load_module("Validate-VisualValleyAssetKit.py")
    assert tuple(bpy.app.version[:2]) == (4, 5)
    worker.require_new_output_directory(args.output_dir)
    report = json.loads((args.candidate_dir / "candidate-proof.json").read_text(encoding="utf-8"))
    base = json.loads((args.base_build / "asset-kit-build.v1.json").read_text(encoding="utf-8"))
    manifest = worker.load_manifest(args.manifest)
    manifest_hash = worker.sha256(args.manifest)
    blend_hash = worker.sha256(args.base_build / "visual-valley-r1-063-asset-kit.blend")
    candidate.validate_candidate_source(manifest, base, manifest_hash, blend_hash)
    failures = []
    for label, authored, recipe_hash, mesh_hash in [
        ("authored-oak", ["vv-tree-oak-a"], manifest_hash, blend_hash),
        ("authored-pine", ["vv-tree-pine-a"], manifest_hash, blend_hash),
        ("changed-manifest", [], "0" * 64, blend_hash),
        ("changed-blend", [], manifest_hash, "0" * 64),
    ]:
        altered = copy.deepcopy(base)
        altered.setdefault("authored_source", {})["assets"] = authored
        try:
            candidate.validate_candidate_source(manifest, altered, recipe_hash, mesh_hash)
        except RuntimeError as error:
            failures.append({"case": label, "error": str(error)})
        else:
            raise AssertionError(f"unsafe source accepted: {label}")
    results = []
    for asset in report["assets"]:
        glb = args.candidate_dir / f"{asset['id']}.glb"
        assert worker.sha256(glb) == asset["glb_sha256"]
        source = args.base_build / "assets" / glb.name
        original = material_signatures(source)
        actual = material_signatures(glb)
        assert original == actual, f"PBR/image/sampler changed for {asset['id']}"
        preserved = None
        if args.reference_candidate:
            reference = args.reference_candidate / glb.name
            validator.clear_import_state()
            bpy.ops.import_scene.gltf(filepath=str(reference))
            preserved = {
                obj.name: (candidate.mesh_hash(obj), tuple(tuple(row) for row in obj.matrix_world))
                for obj in bpy.context.scene.objects if obj.type == "MESH"
                and (obj.get("forge_role") == "collision" or obj.get("forge_lod") == 0
                     or "Foliage" in obj.name)
            }
            assert preserved, "no unchanged reference meshes"
        imported = validator.validate_glb_import(asset, glb)
        if preserved is not None:
            actual_preserved = {
                obj.name: (candidate.mesh_hash(obj), tuple(tuple(row) for row in obj.matrix_world))
                for obj in bpy.context.scene.objects if obj.type == "MESH"
                and (obj.get("forge_role") == "collision" or obj.get("forge_lod") == 0
                     or "Foliage" in obj.name)
            }
            assert actual_preserved == preserved, "LOD0, foliage or collisions changed"
            imported["unchanged_reference"] = {"glb_sha256": worker.sha256(reference),
                                                "meshes": sorted(preserved)}
        lod_bounds = []
        for lod in range(3):
            points = [obj.matrix_world @ Vector(corner) for obj in bpy.context.scene.objects
                      if obj.type == "MESH" and obj.get("forge_role") == "render" and obj.get("forge_lod") == lod
                      for corner in obj.bound_box]
            assert points, f"missing bounds for {asset['id']} LOD{lod}"
            lod_bounds.append({"lod": lod, "min": [min(point[axis] for point in points) for axis in range(3)],
                               "max": [max(point[axis] for point in points) for axis in range(3)]})
        imported["blender_z_up_bounds"] = lod_bounds
        results.append({"id": asset["id"], "glb_sha256": worker.sha256(glb), "material_signatures_identical_to_base": True, "materials": sorted(actual), "reimport": imported})
    assert all(worker.sha256(Path(path)) == expected for path, expected in report["source_files_unchanged"].items())
    result = {"status": "PASS", "blender": bpy.app.version_string, "candidate_report_sha256": worker.sha256(args.candidate_dir / "candidate-proof.json"), "test_script_sha256": worker.sha256(Path(__file__)), "source_guard_script_sha256": worker.sha256(Path(candidate.__file__)), "source_guard_positive_case": "existing authored rock allowed", "negative_source_cases": failures, "assets": results, "source_files_unchanged": True, "scope": "GLB reimport and equality to base GLB materials; not Blender-to-runtime visual parity, performance or human acceptance"}
    result["spray_site_contract"] = "PASS: interpolated branch sites, leader, deterministic, source intact, unpaired input rejected"
    (args.output_dir / "reimport-proof.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("FORGE_FOLIAGE_REIMPORT_PASS assets=2 source_rejections=4 materials_identical=true")


if __name__ == "__main__":
    main()

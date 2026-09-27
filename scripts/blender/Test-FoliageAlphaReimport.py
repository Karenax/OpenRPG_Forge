"""Compare actual source/derived tree geometry after Blender reimport, headless only."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy


def fingerprint(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    objects = []
    for obj in sorted(bpy.context.scene.objects, key=lambda obj: obj.name):
        item = {"name": obj.name, "type": obj.type,
                "world": [list(row) for row in obj.matrix_world]}
        if obj.type == "MESH":
            mesh = obj.data
            item.update(vertices=[list(v.co) for v in mesh.vertices],
                        normals=[list(v.normal) for v in mesh.vertices],
                        polygons=[(list(p.vertices), p.material_index) for p in mesh.polygons],
                        slots=[m.name if m else None for m in mesh.materials],
                        uvs={layer.name: [list(value.uv) for value in layer.data]
                             for layer in mesh.uv_layers})
        objects.append(item)
    return hashlib.sha256(json.dumps(objects, sort_keys=True).encode()).hexdigest(), len(objects)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-kit", required=True, type=Path)
    parser.add_argument("--derived-kit", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if args.output.exists():
        raise RuntimeError("Output exists")
    proof = json.loads((args.derived_kit / "alpha-derivative-proof.json").read_text())
    results = []
    for asset in proof["assets"]:
        source = args.source_kit / "assets" / f"{asset['id']}.glb"
        derived = args.derived_kit / "assets" / f"{asset['id']}.glb"
        assert sha(source) == asset["source_sha256"]
        assert sha(derived) == asset["derived_sha256"]
        before, count = fingerprint(source)
        after, new_count = fingerprint(derived)
        assert before == after and count == new_count, asset["id"]
        assert sha(source) == asset["source_sha256"]
        assert sha(derived) == asset["derived_sha256"]
        results.append({"id": asset["id"], "geometry_transform_uv_slots_sha256": before,
                        "objects": count, "identical": True})
    result = {"status": "REIMPORT_GEOMETRY_PASS", "blender_version": bpy.app.version_string,
              "derivative_proof_sha256": sha(args.derived_kit / "alpha-derivative-proof.json"),
              "human_qualified": False, "assets": results}
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()

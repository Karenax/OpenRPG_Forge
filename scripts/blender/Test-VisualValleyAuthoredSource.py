"""Synthetic DCC preservation proof; never claims a human sculpt or Studio workflow."""

import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys

import bpy


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def geometry(obj):
    return {
        "vertices": [list(vertex.co) for vertex in obj.data.vertices],
        "faces": [list(face.vertices) for face in obj.data.polygons],
        "uv": [list(loop.uv) for loop in obj.data.uv_layers["UVMap"].data],
        "matrix": [list(row) for row in obj.matrix_world],
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-build", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    root = args.output_dir.resolve()
    if root.exists():
        raise RuntimeError("proof directory already exists")
    root.mkdir(parents=True)
    worker = Path(__file__).with_name("Build-VisualValleyAssetKit.py")
    spec = importlib.util.spec_from_file_location("kit_worker", worker)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    manifest = args.manifest.resolve()
    recipe = module.load_manifest(manifest)
    base = json.loads((args.base_build / "asset-kit-build.v1.json").read_text(encoding="utf-8"))
    expected_provenance = copy.deepcopy(recipe["provenance"])
    if base.get("authored_source"):
        expected_provenance["source_kind"] = "procedural_original_with_authored_edits"
    if base["provenance"] != expected_provenance:
        raise RuntimeError("base build provenance does not match the generating recipe")
    if base["generator"]["input_manifest_sha256"] != digest(manifest):
        raise RuntimeError("base build uses a different generating recipe")
    source_blend = args.base_build / base["blend"]["path"]
    original_hash = digest(source_blend)
    if original_hash != base["blend"]["sha256"]:
        raise RuntimeError("original build source hash mismatch")
    bpy.ops.wm.open_mainfile(filepath=str(source_blend), use_scripts=False)
    asset = base["assets"][0]
    names = [name for lod in asset["validation"]["lods"].values() for name in lod["objects_named"]]
    names += asset["validation"]["collision_objects"]
    edited_name = asset["validation"]["lods"]["LOD0"]["objects_named"][0]
    before = geometry(bpy.data.objects[edited_name])
    bpy.data.objects[edited_name].data.vertices[0].co.x += 0.125
    bpy.data.objects[edited_name].rotation_euler.z = 0.15
    bpy.context.view_layer.update()
    expected = {name: geometry(bpy.data.objects[name]) for name in names}
    authored_blend = root / "authored.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(authored_blend), check_existing=False)
    authored_hash = digest(authored_blend)
    descriptor = {
        "schema": "forge.blender-authored-source", "schema_version": 1,
        "base_manifest_sha256": digest(manifest),
        "blend": {"path": "authored.blend", "sha256": authored_hash},
        # The descriptor binds to the recipe's provenance, not the derived build's
        # source_kind. This also exercises a second edit of an already edited kit.
        "assets": {asset["id"]: names}, "provenance": recipe["provenance"],
    }
    descriptor_path = root / "authored-source.v1.json"
    descriptor_path.write_text(json.dumps(descriptor, indent=2) + "\n", encoding="utf-8")
    bpy.ops.wm.open_mainfile(filepath=str(authored_blend), use_scripts=False)
    assert expected == {name: geometry(bpy.data.objects[name]) for name in names}
    assert before != expected[edited_name]
    # Establish a valid baseline before negative cases: otherwise an unrelated
    # common error (for example provenance) could make every rejection look green.
    loaded = module.load_authored_source(descriptor_path, manifest, recipe)
    assert loaded["document"] == descriptor
    assert loaded["blend_path"] == authored_blend
    negative_cases = []
    for name, mutate in (
        ("stale_recipe", lambda data: data.update(base_manifest_sha256="0" * 64)),
        ("changed_source", lambda data: data["blend"].update(sha256="0" * 64)),
        ("path_escape", lambda data: data["blend"].update(path="../authored.blend")),
        ("unknown_asset", lambda data: data.update(assets={"unknown": names})),
        ("duplicate_objects", lambda data: data.update(assets={asset["id"]: names + names})),
        ("changed_provenance", lambda data: data["provenance"].update(license="invalid-test-license")),
    ):
        invalid = copy.deepcopy(descriptor)
        mutate(invalid)
        invalid_path = root / f"invalid-{name}.json"
        invalid_path.write_text(json.dumps(invalid), encoding="utf-8")
        try:
            module.load_authored_source(invalid_path, manifest, recipe)
        except RuntimeError:
            negative_cases.append(name)
        else:
            raise AssertionError(f"invalid source accepted: {name}")

    def run(script, parameters, log_name):
        with (root / log_name).open("w", encoding="utf-8") as log:
            subprocess.run(
                [bpy.app.binary_path, "--background", "--disable-autoexec", "--python-exit-code", "1",
                 "--python", str(script), "--", *parameters],
                stdout=log, stderr=subprocess.STDOUT, check=True,
                creationflags=subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0,
            )

    for index in (1, 2):
        output = root / f"rebuild-{index}"
        run(worker, ["--manifest", str(manifest), "--authored-source", str(descriptor_path),
                     "--output-dir", str(output)], f"rebuild-{index}.log")
        bpy.ops.wm.open_mainfile(filepath=str(output / "visual-valley-r1-063-asset-kit.blend"), use_scripts=False)
        actual = {name: geometry(bpy.data.objects[name]) for name in names}
        assert actual == expected, f"authored geometry, UV or transform lost in rebuild {index}"
        report = json.loads((output / "asset-kit-build.v1.json").read_text(encoding="utf-8"))
        assert report["authored_source"]["blend_sha256"] == authored_hash
        assert report["assets"][0]["glb"]["sha256"] != asset["glb"]["sha256"]
    validation_path = root / "validation-rebuilds.v1.json"
    run(worker.with_name("Validate-VisualValleyAssetKit.py"),
        ["--primary-dir", str(root / "rebuild-1"), "--rebuild-dir", str(root / "rebuild-2"),
         "--output-report", str(validation_path)], "validation.log")
    assert digest(authored_blend) == authored_hash
    assert digest(source_blend) == original_hash
    proof = {"schema": "forge.authored-source-preservation-proof", "schema_version": 1,
             "synthetic_edit": True, "human_review": "pending", "studio_workflow": "not_implemented",
             "asset_id": asset["id"], "edited_object": edited_name,
             "rebuilds": 2, "geometry_uv_transform_preserved": True,
             "source_files_unchanged": True, "rejected_cases": negative_cases,
             "valid_descriptor_accepted": True,
             "base_was_already_authored": bool(base.get("authored_source")),
             "authored_source_sha256": authored_hash, "worker_sha256": digest(worker),
             "validation_sha256": digest(validation_path)}
    (root / "preservation-proof.v1.json").write_text(json.dumps(proof, indent=2) + "\n", encoding="utf-8")
    print(f"AUTHORED_SOURCE_PRESERVATION_PASS report={root / 'preservation-proof.v1.json'}", flush=True)


if __name__ == "__main__":
    main()

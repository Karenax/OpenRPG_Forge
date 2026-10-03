"""Create an additive FishStand bundle from a pinned source export.

The source bundle is never modified.  Existing files are hard-linked on the
same volume; new JSON manifests and the decoded texture-array base levels are
written only in the new target.  Source GPU-generated mip chains are explicitly
not represented as equivalent data.
"""
import hashlib
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
ATTRIBUTES = {
    "position": ("position.f32le.bin", 3), "normal": ("normal.f32le.bin", 3),
    "uv": ("uv.f32le.bin", 2), "color": ("color.f32le.bin", 4),
    "aLayer": ("aLayer.f32le.bin", 1), "aUV2": ("aUV2.f32le.bin", 2),
}
SURFACES = ["weathered_brown_planks", "weathered_planks", "worn_corrugated_iron", "weathered_peeling_timber"]

def digest(data): return hashlib.sha256(data).hexdigest()
def load_json(path): return json.loads(path.read_text(encoding="utf-8"))

def link_tree(source, target):
    for path in source.rglob("*"):
        relative = path.relative_to(source)
        output = target / relative
        if path.is_dir(): output.mkdir(parents=True, exist_ok=True)
        elif path.is_file():
            output.parent.mkdir(parents=True, exist_ok=True)
            os.link(path, output)
        else: raise ValueError(f"Unexpected source member {relative}")

def record(path, item_size, count):
    data = path.read_bytes()
    return {"file": path.name, "bytes": len(data), "sha256": digest(data), "item_size": item_size, "count": count}

def rgba(path):
    probe = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "json", str(path)], check=True, capture_output=True, text=True)
    stream = json.loads(probe.stdout)["streams"]
    if len(stream) != 1: raise ValueError(f"Missing image stream {path}")
    width, height = stream[0]["width"], stream[0]["height"]
    decode = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgba", "-"], check=True, capture_output=True)
    if len(decode.stdout) != width * height * 4: raise ValueError(f"Unexpected decoded length {path}")
    return width, height, decode.stdout

def array_payload(props, names, prefix, suffix, output):
    pixels = []
    dimensions = None
    for name in names:
        path = props / f"{prefix}{name}_{suffix}.jpg"
        width, height, data = rgba(path)
        if dimensions is None: dimensions = (width, height)
        if (width, height) != dimensions: raise ValueError(f"Unequal array image size {path}")
        pixels.append(data)
    data = b"".join(pixels)
    output.write_bytes(data)
    return {"file": output.name, "bytes": len(data), "sha256": digest(data),
            "width": dimensions[0], "height": dimensions[1], "layers": len(names),
            "srgb": suffix == "a", "address_mode": "repeat",
            "mip_status": "base_level_only_source_gpu_mips_unported"}

def sign_payload(props, output):
    width, height, data = rgba(props / "signs.png")
    output.write_bytes(data)
    return {"file": output.name, "bytes": len(data), "sha256": digest(data),
            "width": width, "height": height, "layers": 1, "srgb": True,
            "address_mode": "clamp", "mip_status": "base_level_only_source_gpu_mips_unported"}

def verify_bundle(root, expected):
    index = root / "preview-scene.json"
    if digest(index.read_bytes()) != expected: raise ValueError("Preview manifest pin mismatch")
    manifest = load_json(index)
    for name, entry in manifest["files"].items():
        data = (root / name).read_bytes()
        if entry != {"bytes": len(data), "sha256": digest(data)}: raise ValueError(f"Manifest differs: {name}")
    fish = root / "fish-stand"
    desc = load_json(fish / "fish-stand.json")
    if desc["source_commit"] != COMMIT or desc["geometry"]["triangles"] != 40259:
        raise ValueError("FishStand identity differs")
    for payload in list(desc["geometry"]["attributes"].values()) + [desc["geometry"]["indices"]]:
        data = (fish / payload["file"]).read_bytes()
        if len(data) != payload["bytes"] or digest(data) != payload["sha256"]: raise ValueError("FishStand geometry differs")
    for texture in desc["textures"].values():
        data = (fish / texture["file"]).read_bytes()
        expected_bytes = texture["width"] * texture["height"] * texture["layers"] * 4
        if len(data) != expected_bytes or len(data) != texture["bytes"] or digest(data) != texture["sha256"]:
            raise ValueError("FishStand texture array differs")
    return {"manifest_sha256": expected, "fish_vertices": 32333, "fish_triangles": 40259,
            "fish_arrays": len(desc["textures"]), "files": len(manifest["files"])}

def prepare(source, export, target):
    if not source.is_dir() or not export.is_dir() or target.exists(): raise ValueError("Need existing inputs and a new target")
    if source.drive != target.drive: raise ValueError("New bundle must share source volume for hard links")
    source_fixture = (source / "scene" / "fixture.json").read_bytes()
    source_index = (source / "preview-scene.json").read_bytes()
    source_export = load_json(export / "manifest.json")
    if source_export["source"]["commit"] != COMMIT or source_export["geometry"]["triangles"] != 40259:
        raise ValueError("Pinned source export mismatch")
    link_tree(source, target)
    # The two top-level manifests are target-specific values, not links.
    (target / "scene" / "fixture.json").unlink()
    (target / "preview-scene.json").unlink()
    fish = target / "fish-stand"; fish.mkdir()
    for filename, _ in ATTRIBUTES.values(): os.link(export / "geometry" / filename, fish / filename)
    os.link(export / "geometry" / "indices.u32le.bin", fish / "indices.u32le.bin")
    # Preserve the acquired source and both license records byte-for-byte in the new bundle.
    link_tree(export / "source", fish / "source")
    props = export / "source" / "props"
    prop_layers = load_json(props / "props.json")["layers"]
    textures = {
        "surf_a": array_payload(props, SURFACES, "s_", "a", fish / "surf-a.rgba8.bin"),
        "surf_n": array_payload(props, SURFACES, "s_", "n", fish / "surf-n.rgba8.bin"),
        "surf_r": array_payload(props, SURFACES, "s_", "r", fish / "surf-r.rgba8.bin"),
        "prop_a": array_payload(props, prop_layers, "p_", "a", fish / "prop-a.rgba8.bin"),
        "prop_n": array_payload(props, prop_layers, "p_", "n", fish / "prop-n.rgba8.bin"),
        "prop_r": array_payload(props, prop_layers, "p_", "r", fish / "prop-r.rgba8.bin"),
        "signs": sign_payload(props, fish / "signs.rgba8.bin"),
    }
    attributes = {name: record(fish / filename, item_size, 32333) for name, (filename, item_size) in ATTRIBUTES.items()}
    descriptor = {
        "schema": "forge.tidewater.fish-stand-bevy-input", "version": 1, "source_commit": COMMIT,
        "source_export_manifest_sha256": digest((export / "manifest.json").read_bytes()),
        "placement": {"x": 49.9, "z": -74.6, "yaw_radians": 1.45, "y": "terrain.heightAt(x,z)"},
        "geometry": {"attributes": attributes, "indices": record(fish / "indices.u32le.bin", 4, 120777), "triangles": 40259},
        "textures": textures,
        "material": {
            "source_wgsl": "source/StallKit.js#createStallMaterial",
            "status": "bevy_base_array_shader_not_source_parity",
            "limitations": [
                "source_WGSL_surface_branches_unported", "source_textureSampleGrad_unported",
                "source_generated_mips_unported_base_levels_only", "source_anisotropic_filtering_unqualified",
                "source_lantern_night_flicker_unported", "source_ice_clearcoat_translucency_unported",
                "source_sign_chalk_dial_procedural_wear_unported", "source_rope_float_plain_procedural_branches_unported",
                "source_normal_map_TBN_and_corrugation_response_unported", "no_rendered_pixel_equivalence_claim"
            ]
        },
        "licenses": {"tidewater": "source/LICENSE-Tidewater.txt", "props": "source/props/CREDITS.md"}
    }
    (fish / "fish-stand.json").write_text(json.dumps(descriptor, indent=2) + "\n", encoding="utf-8")
    # link_tree hard-links the inherited fixture.  Break that link before
    # writing the target-specific descriptor, otherwise an additive bundle
    # would mutate its immutable input bundle.
    target_fixture = target / "scene" / "fixture.json"
    target_fixture.unlink()
    fixture = load_json(source / "scene" / "fixture.json")
    camera_reference = fixture.get("camera_reference")
    if not isinstance(camera_reference, dict) or camera_reference.get("lighting_and_scene_not_matched") is not True:
        raise ValueError("Expected honest source camera-reference limitation")
    camera_reference["view"] = "seaward-clear004"
    camera_reference["sha256"] = "db8c7019315897be00c8018591bbf0ac05949b11b1be8fe77dcd417529868a95"
    # `files` is consumed by the legacy plant-probe root loader and therefore
    # cannot name nested bundle members.  Keep the additive input explicit
    # without making that loader claim it has instantiated the stall already.
    fixture["fish_stand"] = {
        "descriptor": "../fish-stand/fish-stand.json",
        "status": "bevy_base_array_shader_not_source_parity",
    }
    fixture_bytes = (json.dumps(fixture, indent=2, ensure_ascii=False) + "\n").encode()
    target_fixture.write_bytes(fixture_bytes)
    index = load_json(source / "preview-scene.json")
    index["files"]["scene/fixture.json"] = {"bytes": len(fixture_bytes), "sha256": digest(fixture_bytes)}
    for path in fish.rglob("*"):
        if path.is_file():
            data = path.read_bytes(); index["files"][path.relative_to(target).as_posix()] = {"bytes": len(data), "sha256": digest(data)}
    index_bytes = (json.dumps(index, indent=2, ensure_ascii=False) + "\n").encode()
    (target / "preview-scene.json").write_bytes(index_bytes)
    if (source / "scene" / "fixture.json").read_bytes() != source_fixture or (source / "preview-scene.json").read_bytes() != source_index:
        raise ValueError("Input bundle was modified")
    return verify_bundle(target, digest(index_bytes))

if __name__ == "__main__":
    if len(sys.argv) == 4: print(json.dumps(prepare(*(Path(value).resolve() for value in sys.argv[1:])), sort_keys=True))
    elif len(sys.argv) == 3: print(json.dumps(verify_bundle(Path(sys.argv[1]).resolve(), sys.argv[2]), sort_keys=True))
    else: raise SystemExit("Usage: prepare-fish-stand-bundle.py SOURCE_BUNDLE SOURCE_EXPORT NEW_BUNDLE | BUNDLE SHA256")

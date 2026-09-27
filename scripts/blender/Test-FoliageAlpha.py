"""Bounded real Blender/glTF export test of the generated foliage material rule.

Run with --background --factory-startup --disable-autoexec --python-exit-code 1
--python this-file -- --output-dir <fresh directory on E:>.
Does not rebuild, import or promote the asset kit.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import struct
import sys

import bpy


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    root = args.output_dir
    root.mkdir(parents=True, exist_ok=False)
    worker = Path(__file__).with_name("Build-VisualValleyAssetKit.py")
    spec = importlib.util.spec_from_file_location("kit_alpha_test", worker)
    kit = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(kit)
    images = {}
    for key in ("base_color", "roughness", "normal"):
        image = bpy.data.images.new(f"fixture_{key}", width=2, height=2, alpha=True)
        image.pixels = [value for alpha in (0.0, 0.25, 0.75, 1.0)
                        for value in ((0.15, 0.35, 0.08, alpha) if key == "base_color"
                                      else (0.5, 0.5, 1.0, 1.0))]
        if key != "base_color":
            image.colorspace_settings.name = "Non-Color"
        image.pack()
        images[key] = image
    for foliage, mode in ((True, "mask"), (True, "blend"), (False, "mask")):
        label = f"foliage_{mode}" if foliage else "opaque_control"
        material = bpy.data.materials.new(label)
        kit.bind_pbr_texture_set(material, label, images, foliage, mode)
        bpy.ops.mesh.primitive_plane_add(location=(0, 0, 0))
        obj = bpy.context.object
        obj.data.materials.append(material)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        path = root / f"{label}.glb"
        bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB",
                                  use_selection=True, export_extras=True)
        raw = path.read_bytes()
        length = struct.unpack_from("<I", raw, 12)[0]
        document = json.loads(raw[20:20 + length])
        exported = document["materials"][0]
        expected = mode.upper() if foliage else "OPAQUE"
        assert exported.get("alphaMode", "OPAQUE") == expected, exported
        if foliage and mode == "mask":
            assert exported.get("alphaCutoff", 0.5) == 0.5
            assert exported["doubleSided"] is True
        assert "baseColorTexture" in exported["pbrMetallicRoughness"]
        assert "normalTexture" in exported
        assert all("uri" not in image and "bufferView" in image
                   for image in document["images"])
        assert material["forge_foliage_alpha_mode"] == expected.lower()
    try:
        kit.bind_pbr_texture_set(material, label, images, True, "unknown")
    except ValueError:
        pass
    else:
        raise AssertionError("unknown alpha policy accepted")
    print("FOLIAGE_ALPHA_EXPORT_PASS: MASK 0.5, embedded textures; BLEND/opaque controls; unknown mode refused")


if __name__ == "__main__":
    main()

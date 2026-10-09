"""Retain pinned scanned GLBs, JPEG maps and source instance records without conversion."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys

from PIL import Image


def sha(data):
    return hashlib.sha256(data).hexdigest()


def inspect_glb(data):
    assert data[:4] == b"glTF" and struct.unpack_from("<II", data, 4) == (2, len(data))
    chunks, offset = [], 12
    while offset < len(data):
        length, kind = struct.unpack_from("<II", data, offset)
        assert length % 4 == 0 and offset + 8 + length <= len(data)
        chunks.append((kind, data[offset + 8:offset + 8 + length]))
        offset += 8 + length
    assert offset == len(data) and [kind for kind, _ in chunks] == [0x4E4F534A, 0x004E4942]
    document = json.loads(chunks[0][1])
    assert document["asset"]["version"] == "2.0" and len(document["buffers"]) == 1
    assert "uri" not in document["buffers"][0]
    assert document["buffers"][0]["byteLength"] <= len(chunks[1][1])
    assert not document.get("skins") and not document.get("animations")
    nodes = [node["name"] for node in document["nodes"] if "mesh" in node]
    assert all(any(name.startswith(f"LOD{level}") for name in nodes) for level in range(3))
    return nodes


def main():
    source, acquisition, output = map(Path, sys.argv[1:])
    assert not output.exists() and not output.resolve().is_relative_to(source.resolve())
    inventory_bytes = (acquisition / "inventory.json").read_bytes()
    inventory = json.loads(inventory_bytes)
    assert inventory["source_commit"] == "1438b1abfcaee3267092b75573014f4d9b4a983c"
    def git(*args):
        return subprocess.check_output(["git", "-c", f"safe.directory={source.resolve().as_posix()}",
                                        "-C", str(source), *args], text=True).strip()

    assert git("rev-parse", "HEAD") == inventory["source_commit"]
    assert not git("status", "--porcelain", "--untracked-files=all")
    contract_path = "src/world/debris/ScannedDebris.js"
    assert sha((source / contract_path).read_bytes()) == inventory["source_inputs"][contract_path]
    names = ["dead_quiver_trunk", "dead_quiver_branch_02", "dead_quiver_branch_01", "lambis_shell"]
    sizes = [[1.994, .306, .268], [.526, .287, .201], [.439, .255, .227], [.139, .047, .075]]
    files, pending, assets = {}, {}, []

    def retain(relative):
        file = source / relative
        assert file.is_file() and not file.is_symlink()
        data = file.read_bytes()
        assert sha(data) == inventory["source_inputs"][relative]
        name = file.name
        assert name not in pending
        pending[name] = data
        files[name] = {"bytes": len(data), "sha256": sha(data), "source_path": relative}
        return {"file": name, **files[name]}

    for ordinal, name in enumerate(names):
        prefix = f"public/models/debris/{name}"
        glb = retain(prefix + ".glb")
        nodes = inspect_glb(pending[glb["file"]])
        images = {}
        for role in ["albedo", "normal", "arm"]:
            reference = retain(prefix + "_" + role + ".jpg")
            with Image.open(source / reference["source_path"]) as image:
                assert image.format == "JPEG" and image.size == (1024, 1024)
                width, height = image.size
                image.verify()
            images[role] = {**reference, "width": width, "height": height,
                            "source_sampling": "sRGB" if role == "albedo" else "linear"}
        assets.append({"ordinal": ordinal, "source_name": name,
                       "source_declared_size_m": sizes[ordinal], "glb": glb,
                       "lod_nodes": nodes, "images": images})
    credits = retain("public/models/debris/CREDITS.md")
    license_reference = retain("LICENSE")
    instances = inventory["scanned_instances"]
    assert len(instances) == 141 and all(0 <= instance["asset"] < 4 for instance in instances)
    result = {"schema": "forge.tidewater.debris-scanned-source", "version": 1,
              "source_commit": inventory["source_commit"], "source_inventory_sha256": sha(inventory_bytes),
              "source_contract_sha256": inventory["source_inputs"][contract_path],
              "assets": assets, "instances": instances, "credits": credits, "license": license_reference,
              "source_declared_license": "Poly Haven CC0; see retained credits",
              "files": files, "converted": False, "runtime_qualified": False, "rendered": False,
              "limits": ["Source retention and structural GLB/JPEG inspection only.",
                         "Geometry normalization, texture atlases/mips and camera LOD policy remain pending.",
                         "Node transforms, source Euler ordering and instance scales must be preserved by conversion.",
                         "Source-declared CC0 does not constitute new redistribution qualification."]}
    # Ensure the exact retained inputs remain unchanged before creating a derivative.
    for name, entry in files.items():
        assert sha((source / entry["source_path"]).read_bytes()) == entry["sha256"]
    assert not git("status", "--porcelain", "--untracked-files=all")
    output.mkdir()
    for name, data in pending.items():
        (output / name).write_bytes(data)
        assert sha((output / name).read_bytes()) == files[name]["sha256"]
    (output / "scanned-source.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps({"assets": len(assets), "instances": len(instances), "retained_files": len(files),
                      "bytes": sum(entry["bytes"] for entry in files.values()), "rendered": False}))


if __name__ == "__main__":
    main()

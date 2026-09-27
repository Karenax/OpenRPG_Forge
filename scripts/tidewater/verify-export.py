"""Validate immutable extraction bytes and compare independent executions.

This verifies source extraction only; it does not qualify Studio or rendering.
"""
import hashlib
import json
import math
import runpy
import struct
import sys
from pathlib import Path

PIN = "1438b1abfcaee3267092b75573014f4d9b4a983c"


def validate(root: Path) -> dict:
    root = root.resolve(strict=True)
    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["schema"] == "forge.tidewater.source-export"
    assert manifest["version"] == 1 and manifest["source"]["commit"] == PIN
    paths = set()
    for entry in manifest["files"]:
        path = entry["path"]
        assert path not in paths, f"duplicate {path}"
        paths.add(path)
        full = (root / path).resolve(strict=True)
        assert full.is_relative_to(root) and full.is_file(), f"unsafe {path}"
        data = full.read_bytes()
        assert len(data) == entry["bytes"], f"length {path}"
        assert hashlib.sha256(data).hexdigest() == entry["sha256"], f"hash {path}"
    actual = {p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file()}
    assert actual == paths | {"manifest.json"}, "unmanifested or absent files"
    metadata = json.loads((root / "terrain/metadata.json").read_text())
    assert metadata["stage"] == "after_village_foundations"
    assert metadata["resolution"] == 2048 and metadata["size_m"] == 2048
    heights = root / metadata["maps"]["heights"]["path"]
    assert heights.stat().st_size == 2048 * 2048 * 4
    for (height,) in struct.iter_unpack("<f", heights.read_bytes()):
        assert math.isfinite(height), "non-finite source height"
    for species, count in manifest["vegetation_counts"].items():
        records = json.loads((root / "vegetation" / f"{species}.json").read_text())
        assert len(records) == count and count > 0
        assert len({r["source_id"] for r in records}) == count
        for record in records:
            for field in ("x", "y", "z", "s", "yaw", "seed"):
                assert math.isfinite(record[field]), (species, field)
            assert record["s"] > 0
    catalog = json.loads((root / "prototypes/catalog.json").read_text())
    for name, prototype in catalog["prototypes"].items():
        attrs = prototype["attributes"]
        vertices = attrs["position"]["count"]
        for attr in attrs.values():
            assert attr["count"] == vertices
            assert attr["length"] == vertices * attr["item_size"]
            for (value,) in struct.iter_unpack("<f", (root / attr["path"]).read_bytes()):
                assert math.isfinite(value), name
        indices = list(struct.iter_unpack("<I", (root / prototype["indices"]["path"]).read_bytes()))
        assert len(indices) == prototype["triangles"] * 3
        assert all(index < vertices for (index,) in indices), name
    if (root / "materials/vegetation.json").exists():
        materials = json.loads((root / "materials/vegetation.json").read_text())
        assert materials["schema"] == "forge.tidewater.source-materials"
        modules = {item["name"]: item for item in materials["modules"]}
        assert len(modules) == len(materials["modules"])
        for module in modules.values():
            assert set(module["dependencies"]) <= modules.keys()
            assert module["code"] in paths
            assert (root / module["code"]).stat().st_size > 0
        for material in materials["materials"]:
            assert set(material["modules"]) <= modules.keys()
            assert material["vertex"] and material["surface"]
            assert material["shadow"] or material["name"] == "veg-impostor"
        textures = json.loads((root / "materials/textures.json").read_text())
        detail = textures["terrain_detail"]
        assert (root / detail["path"]).stat().st_size == detail["width"] * detail["height"] * 4
        mips = textures["leaf_atlas"]["mips"]
        assert len(mips) == int(math.log2(max(mips[0]["width"], mips[0]["height"]))) + 1
        for level, mip in enumerate(mips):
            assert mip["level"] == level
            assert mip["width"] == max(1, mips[0]["width"] >> level)
            assert mip["height"] == max(1, mips[0]["height"] >> level)
            data = (root / mip["path"]).read_bytes()
            assert len(data) == mip["width"] * mip["height"] * 4
            assert set(data[3::4]) == {255}, "atlas bake did not write all pixels"
        coverage = (root / mips[0]["path"]).read_bytes()[::4]
        assert min(coverage) == 0 and max(coverage) == 255, "empty or solid leaf mask"
        for name in ("impostor_a", "impostor_b"):
            if name not in textures:
                continue
            mips = textures[name]["mips"]
            assert (mips[0]["width"], mips[0]["height"]) == (3840, 768)
            assert len(mips) == 12
            for level, mip in enumerate(mips):
                assert mip["level"] == level
                assert mip["width"] == max(1, 3840 >> level)
                assert mip["height"] == max(1, 768 >> level)
                assert (root / mip["path"]).stat().st_size == mip["width"] * mip["height"] * 4
            data = (root / mips[0]["path"]).read_bytes()
            assert min(data[3::4]) == 0 and max(data[3::4]) > 0, "empty impostor bake"
    if (root / "vegetation/runtime.json").exists():
        runtime = json.loads((root / "vegetation/runtime.json").read_text())
        assert runtime["schema"] == "forge.tidewater.source-runtime-vegetation"
        assert runtime["matrix_order"] == "column-major"
        assert len(runtime["types"]) == 6
        assert sum(t["count"] for t in runtime["types"]) == sum(manifest["vegetation_counts"].values())
        f32 = lambda v: struct.unpack("<f", struct.pack("<f", v))[0]
        kinds = {"youngPalms": catalog["understory_kinds"]["YOUNG"],
                 "ferns": catalog["understory_kinds"]["FERN"],
                 "monsteras": catalog["broadleaf_kinds"]["MONSTERA"],
                 "elephantEars": catalog["broadleaf_kinds"]["ELEPHANT"],
                 "heliconias": catalog["broadleaf_kinds"]["HELICONIA"],
                 "strelitzias": catalog["broadleaf_kinds"]["STRELITZIA"]}
        for kind in runtime["types"]:
            streams = {}
            for name, width in (("matrices", 16), ("iPos", 4), ("iDat", 4), ("qr2", 1)):
                entry = kind["streams"][name]
                assert entry["length"] == kind["count"] * width
                streams[name] = list(struct.iter_unpack("<" + "f" * width, (root / entry["path"]).read_bytes()))
                assert all(math.isfinite(v) for row in streams[name] for v in row)
            ordered = [(species, r) for species in runtime["species_order"][kind["name"]]
                       for r in json.loads((root / "vegetation" / f"{species}.json").read_text())]
            assert len(ordered) == kind["count"]
            for index, (species, record) in enumerate(ordered):
                pos, dat, matrix = (streams[n][index] for n in ("iPos", "iDat", "matrices"))
                assert pos == tuple(f32(record[n]) for n in ("x", "y", "z", "s"))
                assert matrix[12:15] == pos[:3] and matrix[15] == 1
                assert matrix[3] == matrix[7] == matrix[11] == 0
                seed, height = record["seed"], record.get("H") or 1
                if species in kinds:
                    seed = kinds[species] + seed * .999
                elif species == "bananas":
                    seed = catalog["understory_kinds"]["BANANA" if seed < .5 else "BANANA_B"] + seed * .999
                    height = .02
                assert dat == tuple(f32(v) for v in (record.get("la") or 0, record.get("l") or 0, height, seed))
                assert streams["qr2"][index][0] >= 0
            for level in kind["levels"]:
                assert level["capacity"] > 0
                for mesh in level["meshes"]:
                    vertices = mesh["attributes"]["position"]["count"]
                    for attr in mesh["attributes"].values():
                        assert attr["count"] == vertices and attr["length"] == vertices * attr["item_size"]
                        assert all(math.isfinite(v) for (v,) in struct.iter_unpack("<f", (root / attr["path"]).read_bytes()))
                    indices = list(struct.iter_unpack("<I", (root / mesh["indices"]["path"]).read_bytes()))
                    assert len(indices) % 3 == 0 and all(i < vertices for (i,) in indices)
    if (root / "materials/terrain/material.json").exists():
        material = json.loads((root / "materials/terrain/material.json").read_text())
        assert material["schema"] == "forge.tidewater.source-terrain-material"
        assert material["material"]["vertex"] and material["material"]["surface"]
        modules = {m["name"] for m in material["modules"]}
        for module in material["modules"]:
            assert module["code"] in paths and set(module["dependencies"]) <= modules
        textures = json.loads((root / "materials/terrain/textures.json").read_text())
        for name in ("normal", "splat"):
            texture = textures[name]
            assert texture["width"] == texture["height"] == 2048
            data = (root / texture["path"]).read_bytes()
            assert len(data) == 2048 * 2048 * 4
            assert min(data) != max(data), "empty or constant terrain bake"
        levels = textures["shadow_heights"]["levels"]
        assert len(levels) == 10
        for level, entry in enumerate(levels):
            assert entry["width"] == entry["height"] == max(1, 512 >> level)
            data = (root / entry["path"]).read_bytes()
            assert len(data) == entry["width"] * entry["height"] * 2
            assert all(math.isfinite(value) for (value,) in struct.iter_unpack("<e", data))
        if "mip_chains" in textures:
            assert set(textures["mip_chains"]) == {"normal", "splat", "detail"}
            for name, chain in textures["mip_chains"].items():
                size = 512 if name == "detail" else 2048
                assert len(chain) == int(math.log2(size)) + 1
                for level, mip in enumerate(chain):
                    assert mip["level"] == level
                    assert mip["width"] == mip["height"] == max(1, size >> level)
                    assert (root / mip["path"]).stat().st_size == mip["width"] * mip["height"] * 4
                original = root / ("materials/terrain-detail.rgba8.bin" if name == "detail" else textures[name]["path"])
                assert (root / chain[0]["path"]).read_bytes() == original.read_bytes()
    if 'village/render.json' in paths:
        runpy.run_path(str(Path(__file__).with_name('verify-village.py')))['validate_village'](root, paths)
    return manifest


if __name__ == "__main__":
    if len(sys.argv) not in (2, 3):
        raise SystemExit("Usage: python verify-export.py EXPORT [INDEPENDENT_REPEAT]")
    first = validate(Path(sys.argv[1]))
    if len(sys.argv) == 3:
        second = validate(Path(sys.argv[2]))
        assert first == second, "independent exports differ"
    print(json.dumps({"status": "pass_source_extraction_only", "files": len(first["files"]),
                      "instances": sum(first["vegetation_counts"].values()),
                      "repeat_identical": len(sys.argv) == 3}))

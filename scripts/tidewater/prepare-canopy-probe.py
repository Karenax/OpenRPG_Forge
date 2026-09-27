"""Expand source far cards into one bounded Bevy diagnostic mesh.

This is a far-canopy view only, not a Forge zone or a production instancer.
Requires a freshly prepared trees_far fixture as a read-only template.
"""
import hashlib
import json
import runpy
import shutil
import struct
import sys
from pathlib import Path

source, template, destination = (Path(v).resolve() for v in sys.argv[1:4])
if destination.exists() or destination.is_relative_to(source) or destination.is_relative_to(template):
    raise SystemExit("new destination outside source and template required")
here = Path(__file__).resolve().parent
manifest = runpy.run_path(str(here / "verify-export.py"))["validate"](source)
fixture = json.loads((template / "fixture.json").read_text())
assert fixture["source"] == manifest["source"] and fixture["lod"] == "far"
for name, expected in fixture["files"].items():
    assert Path(name).name == name
    assert hashlib.sha256((template / name).read_bytes()).hexdigest() == expected
runtime = json.loads((source / "vegetation/runtime.json").read_text())
canopy = next(t for t in runtime["types"] if t["name"] == "canopy")
n = canopy["count"]
assert n == 18343
mesh = canopy["levels"][1]["meshes"][0]
vertices = mesh["attributes"]["position"]["count"]
assert vertices == 4
destination.mkdir(parents=True)
for name in fixture["files"]:
    shutil.copyfile(template / name, destination / name)
for name in ("position", "normal", "uv", "aLobe"):
    (destination / f"{name}.bin").write_bytes((template / f"{name}.bin").read_bytes() * n)
for name, stream in (("aVeg", "iPos"), ("aMat", "iDat")):
    raw = (source / canopy["streams"][stream]["path"]).read_bytes()
    (destination / f"{name}.bin").write_bytes(b"".join(raw[i*16:(i+1)*16] * vertices for i in range(n)))
indices = [i for (i,) in struct.iter_unpack("<I", (template / "indices.bin").read_bytes())]
(destination / "indices.bin").write_bytes(b"".join(struct.pack("<I", i + k * vertices) for k in range(n) for i in indices))
shader = (template / "plant.wgsl").read_text()
old = "source.iPos=probe.iPos; source.iDat=probe.iDat;"
assert shader.count(old) == 1
shader = shader.replace(old, "source.iPos=v.aVeg; source.iDat=v.aMat;")
(destination / "plant.wgsl").write_text(shader, encoding="utf-8")
fixture["instance"] = {"x":0,"y":0,"z":0,"s":1,"sy":1,"yaw":0,"la":0,"l":1,"H":1,"seed":0,
                       "source_id":"diagnostic_batch_transform_only"}
fixture["camera_offset"] = [520,700,820]
fixture["species"] = "canopy_batch"
fixture["source_instance_count"] = n
fixture["limitations"] += ["far_canopy_only", "expanded_cards_not_production_instancing", "no_terrain_or_other_scene_objects"]
if len(sys.argv) == 5:
    terrain_path = Path(sys.argv[4]).resolve(strict=True)
    terrain = json.loads(terrain_path.read_text())
    metadata = json.loads((source / "terrain/metadata.json").read_text())
    assert terrain["schema"] == "forge.terrain" and terrain["schema_version"] == 4
    assert terrain["sampled_source"]["samples_sha256"] == metadata["maps"]["heights"]["sha256"]
    shutil.copyfile(terrain_path,destination / "terrain.json")
    shutil.copyfile(source / "terrain/heights.bin",destination / "heights.bin")
    fixture["limitations"].remove("no_terrain_or_other_scene_objects")
    fixture["limitations"] += ["neutral_terrain_material_not_source_shading", "one_metre_lattice_not_source_cdlod", "other_scene_objects_missing"]
fixture["preparation"]["prepare-canopy-probe.py"] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture["files"] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.iterdir()}
(destination / "fixture.json").write_text(json.dumps(fixture, indent=2), encoding="utf-8")
print(json.dumps({"output":str(destination),"source_instances":n,"triangles":n*2,"status":"far_canopy_probe_only"}))

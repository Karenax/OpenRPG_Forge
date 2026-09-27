"""Create an isolated Bevy asset fixture from a hash-validated source export."""
import hashlib
import json
import runpy
import shutil
import sys
import struct
from pathlib import Path

here = Path(__file__).resolve().parent
source, destination = map(lambda value: Path(value).resolve(), sys.argv[1:3])
species = sys.argv[3] if len(sys.argv) > 3 else "palms"
far = species.endswith("_far")
species = species.removesuffix("_far")
choices = {"palms": ("buildPalmNear", "veg-plant-leaf"),
           "trees": ("buildTreeNear", "veg-canopy"), "shrubs": ("buildShrubNear", "veg-canopy")}
prototype, material_name = choices[species]
if far:
    assert species in ("trees", "shrubs")
    material_name = "veg-impostor"
if destination.exists() or destination.is_relative_to(source):
    raise SystemExit("A new output directory outside the export is required")
manifest = runpy.run_path(str(here / "verify-export.py"))["validate"](source)
materials = json.loads((source / "materials/vegetation.json").read_text())
plant = next(item for item in materials["materials"] if item["name"] == material_name)
veg = next(item for item in materials["modules"] if item["name"] == "veg")
uniform = veg["bindings"]["vegParams"]["uniform"]["wgsl"]
shader = (here / "plant-probe-header.wgsl").read_text() + "\n" + uniform
common = (source / "materials/modules/common.wgsl").read_text()
assert common.count("// ---- depth (reversed-Z") == 1
# Preserve source math helpers; camera/depth helpers belong to the Bevy adapter.
shader += common.split("// ---- depth (reversed-Z")[0]
fields = veg["bindings"]["vegParams"]["uniform"]["fields"]
lobe_table = fields["lobeTable"]["value"]
lobe_rows = ",".join("vec4f(" + ",".join(str(float(x)) for x in row) + ")" for row in lobe_table)
near = fields["canopyNear"]["value"]
shader += f"\nfn setupSourceLobes(){{vegParams.lobeTable=array<vec4f,{len(lobe_table)}>({lobe_rows});"
shader += f"vegParams.canopyNear=vec2f({float(near['x'])},{float(near['y'])});}}\n"
modules = ("lodFade", "veg", "vegPlant") if species == "palms" else ("lodFade", "veg", "vegCanopy", "vegLeafAtlas", "vegCanopyMask")
if far:
    modules = ("lodFade", "veg", "vegCanopy", "vegImpostorSample")
for module in modules:
    shader += "\n" + (source / f"materials/modules/{module}.wgsl").read_text()
shader += "\nfn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,ProbeOut>){\n" + plant["vertex"] + "\n}\n"
shader += "\nfn sourceSurface(in:FragInput,s:ptr<function,SourceSurface>){\n" + plant["surface"] + "\n}\n"
shader += (here / "plant-probe-footer.wgsl").read_text()
destination.mkdir(parents=True)
(destination / "plant.wgsl").write_text(shader, encoding="utf-8")
for name in ("LICENSE-Tidewater.txt", "CREDITS-Tidewater.md"):
    shutil.copyfile(source / name, destination / name)
if far:
    runtime = json.loads((source / "vegetation/runtime.json").read_text())
    canopy = next(t for t in runtime["types"] if t["name"] == "canopy")
    mesh = canopy["levels"][1]["meshes"][0]
    for name in ("position", "normal"):
        shutil.copyfile(source / mesh["attributes"][name]["path"], destination / f"{name}.bin")
    (destination / "uv.bin").write_bytes(struct.pack("<f", 0) * 2 * mesh["attributes"]["position"]["count"])
    shutil.copyfile(source / mesh["indices"]["path"], destination / "indices.bin")
    # Fixed material vertex layout includes near-only attributes; the unmodified
    # far vertex body never reads these zero-valued slots.
    for name in ("aVeg", "aMat", "aLobe"):
        (destination / f"{name}.bin").write_bytes(struct.pack("<f", 0) * 4 * mesh["attributes"]["position"]["count"])
else:
    for name in ("position", "normal", "uv", "aVeg", "aMat", "aLobe", "indices"):
        shutil.copyfile(source / f"prototypes/{prototype}/{name}.bin", destination / f"{name}.bin")
shutil.copyfile(source / "materials/terrain-detail.rgba8.bin", destination / "detail.bin")
textures = json.loads((source / "materials/textures.json").read_text())
leaf = textures["leaf_atlas"]["mips"]
(destination / "leaf.bin").write_bytes(b"".join((source / mip["path"]).read_bytes() for mip in leaf))
if far:
    for name in ("a", "b"):
        (destination / f"impostor-{name}.bin").write_bytes(b"".join(
            (source / mip["path"]).read_bytes() for mip in textures[f"impostor_{name}"]["mips"]))
instances = json.loads((source / f"vegetation/{species}.json").read_text())
fixture = {"schema":"forge.tidewater.plant-probe", "version":2,
    "source":manifest["source"], "instance":instances[0], "species":species, "lod":"far" if far else "near",
    "leaf_texture":{"width":leaf[0]["width"], "height":leaf[0]["height"], "mips":len(leaf)},
    "status":"developmental_geometry_material_probe_not_scene_parity",
    "limitations":["bevy_lighting", "no_shadow_parity", "no_temporal_parity", "not_studio_asset"],
    "files":{file.name:hashlib.sha256(file.read_bytes()).hexdigest() for file in destination.iterdir()},
    "preparation":{name:hashlib.sha256((here/name).read_bytes()).hexdigest()
                   for name in ("prepare-plant-probe.py","plant-probe-header.wgsl","plant-probe-footer.wgsl")}}
if far:
    mips = textures["impostor_a"]["mips"]
    fixture["impostor_texture"] = {"width":mips[0]["width"],"height":mips[0]["height"],"mips":len(mips)}
    fixture["camera_offset"] = [55,20,65]
(destination / "fixture.json").write_text(json.dumps(fixture,indent=2),encoding="utf-8")
print(json.dumps({"output":str(destination),"source_instance":instances[0]["source_id"]}))

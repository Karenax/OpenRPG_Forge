"""Replace expanded diagnostic far cards with one mesh and source instance data."""
import hashlib
import json
import runpy
import shutil
import sys
from pathlib import Path

source, template, destination = (Path(v).resolve() for v in sys.argv[1:4])
if destination.exists() or destination.is_relative_to(source) or destination.is_relative_to(template):
    raise SystemExit("new destination outside inputs required")
here = Path(__file__).resolve().parent
manifest = runpy.run_path(str(here / "verify-export.py"))["validate"](source)
fixture = json.loads((template / "fixture.json").read_text())
assert fixture["source"] == manifest["source"]
assert fixture["species"] == "canopy_batch" and fixture["lod"] == "far"
for name, expected in fixture["files"].items():
    assert Path(name).name == name
    assert hashlib.sha256((template / name).read_bytes()).hexdigest() == expected
runtime = json.loads((source / "vegetation/runtime.json").read_text())
canopy = next(t for t in runtime["types"] if t["name"] == "canopy")
assert canopy["count"] == fixture["source_instance_count"] == 18343
mesh = canopy["levels"][1]["meshes"][0]
assert mesh["attributes"]["position"]["count"] == 4
destination.mkdir(parents=True)
for name in fixture["files"]:
    shutil.copyfile(template / name, destination / name)
for name in ("position", "normal"):
    shutil.copyfile(source / mesh["attributes"][name]["path"], destination / f"{name}.bin")
shutil.copyfile(source / mesh["indices"]["path"], destination / "indices.bin")
for name, arity in (("uv", 2), ("aVeg", 4), ("aMat", 4), ("aLobe", 4)):
    (destination / f"{name}.bin").write_bytes(bytes(4 * arity * 4))
pos = (source / canopy["streams"]["iPos"]["path"]).read_bytes()
dat = (source / canopy["streams"]["iDat"]["path"]).read_bytes()
assert len(pos) == len(dat) == canopy["count"] * 16
(destination / "canopy-instances.bin").write_bytes(b"".join(
    pos[i:i+16] + dat[i:i+16] for i in range(0, len(pos), 16)))
shader = (template / "plant.wgsl").read_text()
old = "source.iPos=v.aVeg; source.iDat=v.aMat;"
assert shader.count(old) == 1
shader = shader.replace(old,
    "let record=canopyInstances[mesh_functions::get_tag(v.instance)]; "
    "source.iPos=record.iPos; source.iDat=record.iDat;")
shader += "\nstruct CanopyInstance { iPos:vec4f, iDat:vec4f };\n"
shader += "@group(#{MATERIAL_BIND_GROUP}) @binding(8) var<storage,read> canopyInstances:array<CanopyInstance>;\n"
(destination / "plant.wgsl").write_text(shader, encoding="utf-8")
fixture["limitations"].remove("expanded_cards_not_production_instancing")
fixture["limitations"].append("all_far_instances_submitted_no_spatial_query")
fixture["preparation"][Path(__file__).name] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
fixture["files"] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.iterdir()}
(destination / "fixture.json").write_text(json.dumps(fixture, indent=2), encoding="utf-8")
print(json.dumps({"instances":canopy["count"], "shared_vertices":4, "record_bytes":len(pos)*2}))

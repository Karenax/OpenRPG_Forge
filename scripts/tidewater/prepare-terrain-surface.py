"""Add original surface WGSL and exact GPU mip bytes to a new island fixture."""
import hashlib
import json
import re
import runpy
import shutil
import sys
from pathlib import Path

source, template, destination = (Path(v).resolve() for v in sys.argv[1:4])
here = Path(__file__).resolve().parent
if destination.exists() or destination.is_relative_to(source) or destination.is_relative_to(template):
    raise SystemExit("new destination required outside inputs")
manifest = runpy.run_path(str(here / "verify-export.py"))["validate"](source)
fixture = json.loads((template / "fixture.json").read_text())
assert fixture["source"] == manifest["source"] and "terrain.json" in fixture["files"]
terrain_document = json.loads((template / "terrain.json").read_text())
source_metadata = json.loads((source / "terrain/metadata.json").read_text())
assert terrain_document["sampled_source"]["samples_sha256"] == source_metadata["maps"]["heights"]["sha256"]
assert terrain_document["extent_m"] == {"size_x_m":2048,"size_z_m":2048}
for name, expected in fixture["files"].items():
    assert Path(name).name == name
    assert hashlib.sha256((template / name).read_bytes()).hexdigest() == expected

def without_function(code, name):
    start = code.index(f"fn {name}(")
    begin = code.index("{", start)
    depth, end = 1, begin + 1
    while depth:
        depth += (code[end] == "{") - (code[end] == "}")
        end += 1
    return code[:start] + code[end:]

def selected_defines(code):
    # All remaining code is byte-preserved. These are the source's declared
    # no-shore, main-pass paths; fail on any unfamiliar conditional.
    stack, enabled, lines = [], True, []
    for line in code.splitlines(keepends=True):
        match = re.fullmatch(r"#if (\w+)\s*", line)
        if match:
            assert match[1] in ("HAS_WETNESS", "REFRACTION_CLIP")
            stack.append(enabled); enabled = False
        elif line.strip() == "#else":
            enabled = stack[-1] and not enabled
        elif line.strip() == "#endif":
            enabled = stack.pop()
        elif enabled:
            lines.append(line)
    assert not stack
    return "".join(lines)

material = json.loads((source / "materials/terrain/material.json").read_text())
terrain = next(m for m in material["modules"] if m["name"] == "terrain")
shader = (here / "terrain-probe-header.wgsl").read_text()
shader += terrain["bindings"]["terrainParams"]["uniform"]["wgsl"]
shader += material["material"]["uniforms"]["wgsl"].replace(material["material"]["uniforms"]["struct"], "SourceMaterialParams")
common = (source / "materials/terrain/modules/common.wgsl").read_text()
assert common.count("// ---- depth (reversed-Z") == 1
shader += common.split("// ---- depth (reversed-Z")[0]
terrain_code = (source / terrain["code"]).read_text()
for name in ("terrainHeightAt", "terrainShoreSample", "terrainSunShadowAt"):
    terrain_code = without_function(terrain_code, name)
shader += terrain_code
shader += (source / "materials/terrain/modules/terrainShading.wgsl").read_text()
module = (source / "materials/terrain/modules/terrainMaterial.wgsl").read_text()
shader += selected_defines(without_function(module, "materialSunModulation"))
shader += "\nfn sourceSurface(in:FragInput,s:ptr<function,SourceSurface>){\n" + selected_defines(material["material"]["surface"]) + "\n}\n"
shader += (here / "terrain-probe-footer.wgsl").read_text()
destination.mkdir(parents=True)
for name in fixture["files"]:
    shutil.copyfile(template / name, destination / name)
(destination / "terrain.wgsl").write_text(shader, encoding="utf-8")
textures = json.loads((source / "materials/terrain/textures.json").read_text())
for name, mips in textures["mip_chains"].items():
    (destination / f"terrain-{name}.bin").write_bytes(b"".join((source / m["path"]).read_bytes() for m in mips))
fixture["limitations"].remove("neutral_terrain_material_not_source_shading")
fixture["limitations"] += ["source_terrain_surface_with_bevy_lighting", "source_no_shore_material_path", "no_heightfield_sun_shadow"]
if len(sys.argv) == 5:
    camera_path = Path(sys.argv[4]).resolve(strict=True)
    reference = json.loads(camera_path.read_text())
    assert reference["source_commit"] == manifest["source"]["commit"]
    camera = reference["camera"]
    fixture["camera_pose"] = {"position":[camera["position"][axis] for axis in ("x","y","z")],
        "rotation":[camera["quaternion"][axis] for axis in ("_x","_y","_z","_w")], "fov_degrees":camera["fov"]}
    fixture["camera_reference"] = {"sha256":hashlib.sha256(camera_path.read_bytes()).hexdigest(),
        "view":reference["view"],"lighting_and_scene_not_matched":True}
fixture["preparation"].update({name:hashlib.sha256((here / name).read_bytes()).hexdigest()
    for name in ("prepare-terrain-surface.py", "terrain-probe-header.wgsl", "terrain-probe-footer.wgsl")})
fixture["files"] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in destination.iterdir()}
(destination / "fixture.json").write_text(json.dumps(fixture, indent=2), encoding="utf-8")
print(json.dumps({"output":str(destination),"status":"terrain_source_surface_probe_not_full_scene"}))

"""Adapt acquired Nature bodies to the shared Bevy Village scene receiver.

Usage: DEBRIS_PACKAGE BUILDING_PACKAGE NEW_OUTPUT
No texture placeholders are generated; callers must bind the actual terrain maps.
"""
import hashlib
import json
from pathlib import Path
import sys


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def function(code, name):
    marker = f"fn {name}("
    assert code.count(marker) == 1
    start = code.index(marker)
    opening = code.index("{", start)
    end, depth = opening + 1, 1
    while depth:
        depth += (code[end] == "{") - (code[end] == "}")
        end += 1
    return code[start:end]


def main():
    debris_root, building_root, output = map(lambda value: Path(value).resolve(), sys.argv[1:])
    assert not output.exists() and all(not output.is_relative_to(root) for root in [debris_root, building_root])
    debris_bytes = (debris_root / "package.json").read_bytes()
    building_bytes = (building_root / "package.json").read_bytes()
    debris, building = json.loads(debris_bytes), json.loads(building_bytes)
    assert debris["source_commit"] == building["source_commit"] == "1438b1abfcaee3267092b75573014f4d9b4a983c"

    def read(root, manifest, reference):
        name = reference["file"]
        assert Path(name).name == name
        assert manifest["files"][name] == {key: reference[key] for key in ["bytes", "sha256"]}
        raw = (root / name).read_bytes()
        assert sha(raw) == reference["sha256"] and len(raw) == reference["bytes"]
        return raw

    contract = json.loads(read(debris_root, debris, debris["materials"]))["source_contracts"]["nature"]
    assert contract["defines"] == {"MATERIAL_SUN_MODULATION": 1}
    assert contract["side"] == "front" and not contract["transparent"] and contract["depth_write"]
    library = json.loads(read(building_root, building, building["material_library"]))
    source_shader = read(building_root, building, library["shader"]).decode().replace("\r\n", "\n")
    prefix = source_shader[:source_shader.index("#ifdef VILLAGE_KIND_0\n")]
    last = source_shader.index("#ifdef VILLAGE_KIND_6\n")
    tail = source_shader[source_shader.index("\n#endif", last) + len("\n#endif"):]
    assert "#ifdef VILLAGE_KIND_" not in prefix + tail
    assert prefix.count("night:f32 }") == 1
    prefix = prefix.replace("night:f32 }", "night:f32,cameraPos:vec3f,frameIndex:u32 }")
    prefix = prefix.replace("frame.night=settings.flags.y;",
                            "frame.night=settings.flags.y; frame.cameraPos=view.world_position; frame.frameIndex=shadowParams.frameIndex;")
    modules = {module["name"]: module for module in contract["modules"]}
    assert set(modules) == {"common", "lodFade", "terrainShading", "terrain", "debrisStone", "terrainSunModulation"}
    programs = {name: read(debris_root, debris, entry["code"]).decode().replace("\r\n", "\n")
                for name, entry in modules.items()}
    common = programs["common"]
    start = common.index("fn perturbNormalByMap(")
    end = common.index("\n}", start) + 2
    common = common.split("// ---- depth (reversed-Z")[0] + common[start:end]
    terrain = programs["terrain"]
    # Only these source functions are reachable from Nature. Other TerrainGPU helpers
    # require unrelated maps; retain the originals below without rewriting their math.
    selected = "\n".join(function(terrain, name) for name in ["terrainUvOf", "terrainHeightAt"])
    old_shadow = function(tail, "terrainSunShadowAt")
    tail = tail.replace(old_shadow, function(terrain, "terrainSunShadowAt"))
    terrain_uniform = modules["terrain"]["bindings"]["terrainParams"]["uniform"]
    declarations = terrain_uniform["wgsl"] + """
@group(#{MATERIAL_BIND_GROUP}) @binding(100) var terrainHeightTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(101) var terrainDetailTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(102) var smpAniso4Repeat:sampler;
@group(#{MATERIAL_BIND_GROUP}) @binding(103) var<uniform> terrainParams:TerrainParams;
"""
    vertex = read(debris_root, debris, contract["stages"]["vertex"]).decode()
    surface = read(debris_root, debris, contract["stages"]["surface"]).decode()
    shader = prefix + common + programs["lodFade"] + programs["terrainShading"] + selected
    shader += programs["debrisStone"] + declarations
    shader += "\nfn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){\n" + vertex + "\n}\n"
    shader += "fn sourceSurface(in:FragInput,s:ptr<function,Surface>){\n" + surface + "\n}\n" + tail
    assert shader.count("fn terrainSunShadowAt(") == 1
    assert "sceneLighting.sunColor.xyz*terrainSunShadowAt(P)" in shader
    # No second application of MATERIAL_SUN_MODULATION: shared direct lighting already
    # invokes the same terrainSunShadowAt at the scene point.
    assert "materialSunModulation(" not in shader
    fields = {name: entry["value"] for name, entry in contract["uniforms"]["fields"].items()}
    descriptor = {"schema": "forge.tidewater.debris-nature-program", "version": 1,
                  "source_commit": debris["source_commit"], "debris_package_sha256": sha(debris_bytes),
                  "building_package_sha256": sha(building_bytes), "preparation_sha256": sha(Path(__file__).read_bytes()),
                  "shader": {"file": "nature.wgsl", "sha256": sha(shader.encode()), "bytes": len(shader.encode())},
                  "uniform_fields": fields, "terrain_uniform_layout": terrain_uniform,
                  "bindings": {"100": "actual terrain height R32Float, unfilterable",
                               "101": "actual terrain detail RGBA8Unorm and source mip chain",
                               "102": "repeat linear mip sampler, anisotropy 4",
                               "103": "live terrain domain/resolution and sun bake values",
                               "12": "actual terrain sun shadow, shared base material"},
                  "source_modules": {name: module["code"]["sha256"] for name, module in modules.items()},
                  "limits": ["Shader preparation only; no pipeline, draw, texture pixels or Studio parity proved",
                             "Acquired TerrainParams contains an unbaked sun and placeholder shore; runtime must supply live values"]}
    output.mkdir()
    (output / "nature.wgsl").write_bytes(shader.encode())
    (output / "program.json").write_text(json.dumps(descriptor, indent=2), encoding="utf-8")
    print(json.dumps({"output": str(output), "shader_bytes": len(shader.encode()), "status": "prepared_not_rendered"}))


if __name__ == "__main__":
    main()

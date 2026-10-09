"""Prepare the acquired scanned surface on the existing Village scene ABI.

Usage: SCANNED_MATERIAL BUILDING_PACKAGE NEW_OUTPUT
No pixels, geometry or renderer resources are fabricated here.
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
    root, buildings, output = map(lambda arg: Path(arg).resolve(), sys.argv[1:])
    assert not output.exists() and all(not output.is_relative_to(p) for p in [root, buildings])
    raw = (root / "scanned-material.json").read_bytes()
    source = json.loads(raw)
    building_raw = (buildings / "package.json").read_bytes()
    building = json.loads(building_raw)
    assert source["source_commit"] == building["source_commit"] == "1438b1abfcaee3267092b75573014f4d9b4a983c"

    def read(parent, index, ref):
        assert Path(ref["file"]).name == ref["file"]
        data = (parent / ref["file"]).read_bytes()
        assert len(data) == ref["bytes"] and sha(data) == ref["sha256"]
        assert index["files"][ref["file"]] == {k: ref[k] for k in ["bytes", "sha256"]}
        return data.decode("utf-8").replace("\r\n", "\n")

    contract = source["contract"]
    assert contract["attributes"] == {"aAsset": "f32", "iData": "vec4f"}
    assert contract["defines"] == {"MATERIAL_SUN_MODULATION": 1}
    assert contract["side"] == "front" and contract["depth_write"] and not contract["transparent"]
    library = json.loads(read(buildings, building, building["material_library"]))
    template = read(buildings, building, library["shader"])
    prefix = template[:template.index("#ifdef VILLAGE_KIND_0\n")]
    last = template.index("#ifdef VILLAGE_KIND_6\n")
    tail = template[template.index("\n#endif", last) + len("\n#endif"):]
    assert "#ifdef VILLAGE_KIND_" not in prefix + tail
    assert prefix.count("night:f32 }") == 1
    prefix = prefix.replace("night:f32 }", "night:f32,cameraPos:vec3f,frameIndex:u32 }")
    prefix = prefix.replace("frame.night=settings.flags.y;", "frame.night=settings.flags.y; frame.cameraPos=view.world_position; frame.frameIndex=shadowParams.frameIndex;")
    assert prefix.count("vdata:vec4f };") == 1
    prefix = prefix.replace("vdata:vec4f };", "vdata:vec4f,aAsset:f32,iData:vec4f };")
    assert prefix.count("@location(4) vData:vec4f,") == 1
    prefix = prefix.replace("@location(4) vData:vec4f,", "@location(4) vIData:vec4f,")
    assert tail.count("v.tint=input.tint;v.vdata=input.vdata;") == 1
    # Retain the common five-attribute ABI: aAsset travels in tint.x;
    # source per-instance asset/random/fade/outgoing travels in vdata.
    tail = tail.replace("v.tint=input.tint;v.vdata=input.vdata;", "v.tint=input.tint;v.vdata=input.vdata;v.aAsset=input.tint.x;v.iData=input.vdata;")
    modules = {m["name"]: m for m in contract["modules"]}
    assert set(modules) == {"common", "lodFade", "terrain", "terrainSunModulation"}
    programs = {name: read(root, source, m["code"]) for name, m in modules.items()}
    common = programs["common"]
    common = common.split("// ---- depth (reversed-Z")[0] + function(common, "perturbNormalByMap")
    terrain = programs["terrain"]
    tail = tail.replace(function(tail, "terrainSunShadowAt"), function(terrain, "terrainSunShadowAt"))
    uniform = modules["terrain"]["bindings"]["terrainParams"]["uniform"]
    declarations = uniform["wgsl"] + """
@group(#{MATERIAL_BIND_GROUP}) @binding(100) var terrainHeightTex:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(101) var<uniform> terrainParams:TerrainParams;
@group(#{MATERIAL_BIND_GROUP}) @binding(102) var scanAlbedo:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(103) var scanNormal:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(104) var scanArm:texture_2d<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(105) var smpAnisoClamp:sampler;
"""
    vertex, surface, shadow = [read(root, source, contract["stages"][stage]) for stage in ["vertex", "surface", "shadow"]]
    shader = prefix + common + programs["lodFade"] + declarations
    shader += "\n".join(function(terrain, name) for name in ["terrainUvOf", "terrainHeightAt"])
    shader += "\nfn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>){\n" + vertex + "\n}\n"
    shader += "fn sourceSurface(in:FragInput,s:ptr<function,Surface>){\n" + surface + "\n}\n"
    shader += "fn sourceShadow(in:FragInput)->bool{\n" + shadow + "\n}\n" + tail
    assert "materialSunModulation(" not in shader
    assert shader.count("fn terrainSunShadowAt(") == 1
    assert "sceneLighting.sunColor.xyz*terrainSunShadowAt(P)" in shader
    data = shader.encode("utf-8")
    descriptor = {"schema": "forge.tidewater.debris-scanned-program", "version": 1,
                  "source_commit": source["source_commit"], "material_sha256": sha(raw),
                  "building_package_sha256": sha(building_raw), "atlas_descriptor_sha256": source["atlas_descriptor_sha256"],
                  "preparation_sha256": sha(Path(__file__).read_bytes()),
                  "shader": {"file": "scanned.wgsl", "bytes": len(data), "sha256": sha(data)},
                  "uniform_fields": {k: v["value"] for k, v in contract["uniforms"]["fields"].items()},
                  "terrain_uniform_layout": uniform,
                  "vertex_mapping": {"tint.x": "aAsset", "vdata": "iData (asset, random, fade, outgoing)"},
                  "source_modules": {name: m["code"]["sha256"] for name, m in modules.items()},
                  "bindings": {"100": "resident R32Float height, unfilterable", "101": "live TerrainParams",
                               "102": "acquired sRGB albedo mip chain", "103": "acquired linear normal mip chain",
                               "104": "acquired linear ARM mip chain", "105": "clamp linear trilinear anisotropy 8",
                               "12": "resident terrain sun shadow from shared base"},
                  "limits": ["Preparation only; no pipeline, draw, Studio or visual parity qualification",
                             "Source shadow predicate retained but shared caster routing must still invoke it",
                             "LOD selection, live per-instance data and acquired images must be supplied by runtime"]}
    output.mkdir()
    (output / "scanned.wgsl").write_bytes(data)
    (output / "program.json").write_text(json.dumps(descriptor, indent=2), encoding="utf-8")
    print(json.dumps({"status": "prepared_not_rendered", "shader_bytes": len(data), "shader_sha256": sha(data)}))


if __name__ == "__main__":
    main()

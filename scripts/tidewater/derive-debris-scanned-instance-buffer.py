"""Use the existing Bevy mesh tag for acquired scanned instance parameters.

Usage: VERIFIED_SCANNED_PROGRAM NEW_OUTPUT
Source vertex/surface/shadow snippets and acquired textures remain unchanged.
"""
import hashlib
import json
from pathlib import Path
import sys


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


root, output = [Path(arg).resolve() for arg in sys.argv[1:]]
assert not output.exists() and not output.is_relative_to(root)
raw = (root / "program.json").read_bytes()
descriptor = json.loads(raw)
shader_raw = (root / descriptor["shader"]["file"]).read_bytes()
assert sha(shader_raw) == descriptor["shader"]["sha256"] == "f1870e89558c87bf103d192fe179c27c4df901bd2d4e8a96d57ee9a8a566b0c6"
assert len(shader_raw) == descriptor["shader"]["bytes"]
assert descriptor["schema"] == "forge.tidewater.debris-scanned-program"
shader = shader_raw.decode("utf-8")
marker = "v.aAsset=input.tint.x;v.iData=input.vdata;"
assert shader.count(marker) == 1 and "@binding(106)" not in shader
shader = shader.replace(marker, "v.aAsset=input.tint.x;v.iData=scanInstances[mesh_functions::get_tag(input.instance)];")
shader += "\n@group(#{MATERIAL_BIND_GROUP}) @binding(106) var<storage,read> scanInstances:array<vec4f>;\n"
data = shader.encode("utf-8")
descriptor["source_program_sha256"] = sha(raw)
descriptor["instance_derivation_sha256"] = sha(Path(__file__).read_bytes())
descriptor["shader"] = {"file": "scanned.wgsl", "bytes": len(data), "sha256": sha(data)}
descriptor["vertex_mapping"] = {"tint.x": "aAsset", "MeshTag": "index into scanInstances", "scanInstances": "asset, random, fade, outgoing"}
descriptor["bindings"]["106"] = "resident storage array of source per-instance vec4f records; bounded MeshTag indices"
descriptor["limits"].append("MeshTag and storage buffer wiring prepared only; live indexing/batching and temporal tag interaction require runtime validation")
output.mkdir()
(output / "scanned.wgsl").write_bytes(data)
(output / "program.json").write_text(json.dumps(descriptor, indent=2), encoding="utf-8")
print(json.dumps({"status": "prepared_not_rendered", "shader_bytes": len(data), "shader_sha256": sha(data)}))

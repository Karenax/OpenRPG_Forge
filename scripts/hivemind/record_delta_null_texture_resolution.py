"""Resolve declared identities for refused null texture bindings, without fallback."""
import hashlib
import json
from pathlib import Path

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-delta-null-texture-resolution-v1"


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))


def main():
    if ROOT.exists():
        raise RuntimeError("Previous null texture ledger preserved")
    prior_path = W / "nested-prefab-delta-material-wgsl-v1/RESULT.v1.json"
    topology_path = W / "nested-prefab-delta-material-active-ir-v1/RESULT.v1.json"
    properties_path = W / "nested-prefab-delta-graph-property-bindings-v1/RESULT.v1.json"
    prior, topology, properties = [read(p) for p in [prior_path, topology_path, properties_path]]
    refused = {(r["pack_id"],r["material_guid"]) for r in prior["failures"]
               if r["error"] == "Active texture null/default or source unresolved; native adapter required"}
    if len(refused) != 13:
        raise RuntimeError("Exact thirteen-material refusal scope required")
    pins = {str(p):sha(p) for p in [prior_path,topology_path,properties_path,Path(__file__).resolve()]}
    by_material = {(r["pack_id"],r["material_identity"]["guid"]):r for r in properties["rows"]}
    rows, seen = [], set()
    for source in topology["rows"]:
        key = (source["pack_id"],source["material_guid"])
        if key not in refused:
            continue
        ir_path = Path(source["output"])
        if sha(ir_path) != source["output_sha256"]:
            raise RuntimeError("Material active topology changed")
        pins[str(ir_path)] = source["output_sha256"]
        ir = read(ir_path)
        binding = by_material[key]
        gp = Path(binding["graph"]["path"])
        if sha(gp) != binding["graph"]["sha256"]:
            raise RuntimeError("Original ShaderGraph changed")
        pins[str(gp)] = binding["graph"]["sha256"]
        graph_properties = {p["reference_name"]:p for p in binding["bindings"]}
        for node in ir["nodes"]:
            for entry in node["inputs"]:
                value = entry["value"]
                literal = value.get("literal")
                if not isinstance(literal,dict) or "guid" not in literal or literal["guid"]:
                    continue
                identity = (*key,node["node_id"],entry["slot_id"])
                if identity in seen:
                    continue
                seen.add(identity)
                provenance = value["provenance"]
                if not provenance.startswith("material:"):
                    raise RuntimeError("Null binding source declaration unavailable")
                name = provenance.split(':',1)[1]
                prop = graph_properties[name]
                declaration = prop["source_property_object"]
                if not declaration["m_Type"].endswith(".Texture2DShaderProperty"):
                    raise RuntimeError("Refused binding not declared as original Texture2D")
                reference = json.loads(declaration["m_Value"]["m_SerializedTexture"])["texture"]
                override = prop["material_override"]["value"]
                if override["m_Texture"].get("fileID") != 0:
                    raise RuntimeError("Material override is not explicitly null")
                rows.append({"pack_id":key[0],"material_guid":key[1],"material_name":binding["material_identity"]["name"],
                             "node_id":node["node_id"],"slot_id":entry["slot_id"],"property":name,
                             "original_active_value":value,"original_property_declaration":declaration,
                             "material_override":override,"graph_declared_texture_reference":reference,
                             "shaderlab_default_enum":declaration["m_DefaultType"],
                             "shader_configured_default_texture_native_verified":False,
                             "default_substitution_authorized":False,"runtime_binding_qualified":False})
    matches = {r["graph_declared_texture_reference"]["guid"]:[] for r in rows}
    manifests = sorted((W/"hivemind-run").glob("*/source.v1.json"))
    for path in manifests:
        pins[str(path)] = sha(path)
        manifest = read(path)
        for guid in matches:
            if guid in manifest["guid_paths"]:
                matches[guid].append({"manifest":str(path),"asset_path":manifest["guid_paths"][guid]})
    for p,expected in pins.items():
        if sha(p) != expected:
            raise RuntimeError("Texture declaration changed during reopening")
    if len({(r["pack_id"],r["material_guid"]) for r in rows}) != 13:
        raise RuntimeError("Not all refused materials have explicit declarations")
    result = {"status":"null_material_bindings_and_explicit_graph_texture_identities_reopened",
              "refused_materials":13,"unique_node_slot_bindings":len(rows),"source_manifests_searched":len(manifests),
              "rows":rows,"cross_pack_declared_texture_matches":matches,"source_pins":pins,
              "all_input_pins_unchanged":True,"missing_authored_texture_substitution_performed":False,
              "native_material_effective_texture_verified":False,"studio_verified":False,"full_import":False}
    ROOT.mkdir()
    (ROOT/"RESULT.v1.json").write_text(json.dumps(result,indent=2),"utf-8")
    print(json.dumps({k:v for k,v in result.items() if k not in {"rows","source_pins"}}))


if __name__ == "__main__":
    main()

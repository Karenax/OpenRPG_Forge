"""Bind preserved material properties to explicit ShaderGraph blackboard IDs."""
import copy
import datetime
import hashlib
import json
from pathlib import Path
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-graph-property-bindings-v1"

def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def graph_properties(text):
    decoder, documents, offset = json.JSONDecoder(), [], 0
    while offset < len(text):
        while offset < len(text) and text[offset].isspace():
            offset += 1
        if offset == len(text):
            break
        document, offset = decoder.raw_decode(text, offset)
        if not isinstance(document, dict):
            raise ValueError("Graph document is not an object")
        documents.append(document)
    roots = [d for d in documents if d.get("m_Type") == "UnityEditor.ShaderGraph.GraphData"]
    if len(roots) != 1:
        raise ValueError("Exactly one source GraphData required")
    index = {}
    for d in documents:
        object_id = d.get("m_ObjectId")
        if not isinstance(object_id, str) or object_id in index:
            raise ValueError("Missing/duplicate graph object identity")
        index[object_id] = d
    output, names = [], set()
    for ref in roots[0]["m_Properties"]:
        prop = index.get(ref["m_Id"])
        if prop is None or not prop.get("m_Type", "").endswith("ShaderProperty"):
            raise ValueError("Declared graph property object absent")
        name = prop.get("m_OverrideReferenceName") or prop.get("m_DefaultReferenceName")
        if not isinstance(name, str) or not name or name in names:
            raise ValueError("Missing/duplicate explicit graph property reference name")
        names.add(name)
        output.append({"reference_name": name, "source_property_object": prop})
    return output

def controls():
    root = {"m_Type": "UnityEditor.ShaderGraph.GraphData", "m_ObjectId": "root", "m_Properties": [{"m_Id": "texture"}]}
    prop = {"m_Type": "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", "m_ObjectId": "texture", "m_DefaultReferenceName": "_BaseMap", "m_OverrideReferenceName": "_Albedo"}
    encode = lambda objects: "\n\n".join(json.dumps(o) for o in objects)
    assert graph_properties(encode([root, prop]))[0]["reference_name"] == "_Albedo"
    passed = ["explicit_override_and_multi_document_graph"]
    missing = copy.deepcopy(root); missing["m_Properties"][0]["m_Id"] = "absent"
    unnamed = copy.deepcopy(prop); unnamed["m_DefaultReferenceName"] = unnamed["m_OverrideReferenceName"] = ""
    duplicate_name = {**prop, "m_ObjectId": "other"}
    extra_ref = {**root, "m_Properties": root["m_Properties"] + [{"m_Id": "other"}]}
    for name, text in [("duplicate_object", encode([root, prop, prop])), ("missing_declared_property", encode([missing, prop])),
                       ("missing_explicit_name", encode([root, unnamed])), ("duplicate_property_name", encode([extra_ref, prop, duplicate_name])),
                       ("trailing_non_json_data", encode([root, prop]) + "garbage")]:
        try:
            graph_properties(text)
        except (ValueError, KeyError):
            passed.append(name)
        else:
            raise RuntimeError(f"Bad graph control accepted: {name}")
    return passed

def main():
    if ROOT.exists():
        raise RuntimeError("Existing graph property evidence preserved")
    passed = controls()
    ir_path = W / "nested-prefab-material-source-ir-v1/RESULT.v1.json"
    inventory_path = W / "nested-prefab-material-dependency-inventory-v1/RESULT.v1.json"
    ir, inventory = [json.loads(p.read_text("utf-8-sig")) for p in [ir_path, inventory_path]]
    pins = {str(p): sha(p) for p in [ir_path, inventory_path, Path(__file__).resolve()]}
    by_material = {(r["pack_id"], r["material"]["guid"]): r for r in inventory["rows"]}
    rows, graphs = [], {}
    for material in ir["rows"]:
        identity = material["material_identity"]
        deps = by_material[(material["pack_id"], identity["guid"])]["dependencies"]
        shader_guid = material["source_material_properties"]["m_Shader"].get("guid")
        shader = next((d for d in deps if d["guid"] == shader_guid), None)
        if shader is None or not shader["asset_path"].endswith(".shadergraph"):
            continue  # HDRP/Lit retains its separate explicit package route.
        source_path = Path(next(f["path"] for f in shader["verified_files"] if f["path"].endswith(".shadergraph")))
        expected = inventory["source_pins"][str(source_path)]
        if sha(source_path) != expected:
            raise RuntimeError("Original graph changed")
        pins[str(source_path)] = expected
        if expected not in graphs:
            text = source_path.read_text("utf-8-sig")
            graphs[expected] = (graph_properties(text), text)
        properties, text = graphs[expected]
        values = {}
        saved = material["source_material_properties"]["m_SavedProperties"]
        for kind in ["m_TexEnvs", "m_Ints", "m_Floats", "m_Colors"]:
            for entry in saved.get(kind, []):
                for name, value in entry.items():
                    if name in values:
                        raise RuntimeError("Duplicate serialized material property")
                    values[name] = {"source_kind": kind, "value": value}
        bindings = [{**prop, "material_override": values.get(prop["reference_name"]),
                     "uses_source_graph_default_when_no_override": prop["reference_name"] not in values}
                    for prop in properties]
        names = {p["reference_name"] for p in properties}
        legacy = [{**ref, "declared_in_graph_blackboard": ref["property"] in names,
                   "identifier_mentioned_in_graph_serialization": ref["property"] in text,
                   "native_compiler_liveness_qualified": False}
                  for ref in material["unresolved_rendering_references"] if ref["role"] == "texture"]
        rows.append({"pack_id": material["pack_id"], "material_identity": identity,
                     "graph": {"path": str(source_path), "sha256": expected, "source_shader_guid": shader_guid},
                     "bindings": bindings, "unresolved_serialized_texture_references": legacy,
                     "serialized_material_properties_preserved": True, "program_qualified": False, "render_qualified": False})
    for path, expected in pins.items():
        if sha(path) != expected:
            raise RuntimeError("Input changed during graph property binding")
    result = {"status": "explicit_source_graph_property_bindings_reopened", "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "materials": len(rows), "unique_graph_source_hashes": len(graphs), "rows": rows, "source_pins": pins,
              "actual_parser_controls_passed": passed, "all_input_pins_unchanged": True,
              "scope": "Serialized ShaderGraph blackboard identity and material override binding; native compiler and GPU liveness remain unqualified",
              "glb_created": False, "studio_verified": False, "full_import": False}
    ROOT.mkdir()
    (ROOT / "RESULT.v1.json").write_text(json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False), "utf-8")
    print(json.dumps({k: v for k, v in result.items() if k not in {"rows", "source_pins"}}))

if __name__ == "__main__":
    main()

"""Compare IR against original graph objects and installed Unity generation."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re
W=Path("F:/OpenRPG_ImportWork");ROOT=W/"townsmith-s-paint-source-program-v1"
SDK=W/"townsmith-s-paint-sdk-generated-source-v2";OUT=W/"townsmith-s-paint-source-program-reopen-v1"
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def objects(p):
    text=p.read_text(encoding="utf-8-sig");offset=0;decoder=json.JSONDecoder();result=[]
    while offset<len(text):
        while offset<len(text) and text[offset].isspace():offset+=1
        if offset==len(text):break
        value,offset=decoder.raw_decode(text,offset);result.append(value)
    return result
def main():
    assert not OUT.exists()
    receipt_path=ROOT/"RESULT.v1.json";receipt=read(receipt_path)
    assert receipt["actual_exit_code"]==0 and receipt["active_nodes"]==134
    pins=dict(receipt["input_pins"])
    for p,h in pins.items():assert sha(p)==h,p
    closure=read(ROOT/"MASTERPROPS-SUBGRAPH-CLOSURE.v1.json");ir=read(Path(receipt["ir"]))
    lookup={};original_graphs=0
    for graph in closure["graphs"]:
        original=objects(Path(graph["path"]))
        assert original==graph["objects"]
        lookup[graph["guid"]]={o["m_ObjectId"]:o for o in original}
        original_graphs+=1
    scopes={"root":receipt["root_graph_guid"]}
    for binding in ir["subgraph_bindings"]:
        assert binding["parent_scope"] in scopes
        caller=lookup[scopes[binding["parent_scope"]]][binding["caller_node"]]
        assert json.loads(caller["m_SerializedSubGraph"])["subGraph"]["guid"]==binding["subgraph_guid"]
        assert set(binding["inputs"])==set(caller["m_PropertyGuids"])
        scope=binding["parent_scope"]+"/"+binding["caller_node"]
        assert scope not in scopes;scopes[scope]=binding["subgraph_guid"]
    omitted={"m_DrawState","m_Group","m_CustomColors","m_PreviewExpanded","m_PreviewMode","synonyms","m_Slots"}
    refs=set();input_slots=0
    for node in ir["nodes"]:
        assert node["scope"] in scopes and scopes[node["scope"]]==node["graph_guid"]
        source=lookup[node["graph_guid"]][node["node_id"]]
        assert node["opcode"]==source["m_Type"].split(".")[-1]
        assert node["source_parameters"]=={k:v for k,v in source.items() if k not in omitted}
        slots=[lookup[node["graph_guid"]][r["m_Id"]] for r in source.get("m_Slots",[])]
        assert node["output_slot"]==next(s for s in slots if s["m_Id"]==node["output_slot_id"])
        source_inputs={s["m_Id"]:s for s in slots if s["m_SlotType"]==0}
        assert set(source_inputs)=={s["slot_id"] for s in node["inputs"]}
        for inp in node["inputs"]:
            slot=source_inputs[inp["slot_id"]]
            assert inp["name"]==slot["m_DisplayName"] and inp["slot_type"]==slot["m_Type"]
            if "ref" in inp["value"]:assert inp["value"]["ref"] in refs
            elif "source_slot" in inp["value"]:
                assert inp["value"]["literal"]==inp["value"]["source_slot"].get("m_Value")
            input_slots+=1
        assert node["id"] not in refs;refs.add(node["id"])
    for branch in ir["selected_branches"]:
        source=lookup[scopes[branch["scope"]]][branch["node_id"]]
        assert source["m_Type"].endswith(".BranchNode")
        assert branch["chosen_input"]==("True" if branch["predicate"]["literal"] else "False")
        slots=[lookup[scopes[branch["scope"]]][r["m_Id"]] for r in source["m_Slots"]]
        assert next(s for s in slots if s["m_Id"]==branch["source_input_slot"])["m_DisplayName"]==branch["chosen_input"]
    run=read(SDK/"RUN-RESULT.v1.json");native=read(SDK/"NATIVE-RESULT.v1.json");prep=read(SDK/"PREPARED.v1.json")
    assert run["actual_exit_code"]==0 and run["source_inputs_unchanged"] and native["shader_guid"]==receipt["root_graph_guid"]
    for p,h in {**prep["input_pins"],**prep["private_before_pins"]}.items():assert sha(p)==h,p
    generated=Path(native["generated_source"]);assert sha(generated)==run["generated_source_sha256"]
    text=generated.read_text(encoding="utf-8-sig")
    vertex_bindings=[]
    for field,attribute in [("Position","ObjectSpacePosition"),("Normal","ObjectSpaceNormal"),("Tangent","ObjectSpaceTangent")]:
        terminal=ir["terminals"]["VertexDescription."+field]
        assert "source_slot" in terminal and terminal["literal"]=={"x":0.0,"y":0.0,"z":0.0}
        equation=f"description.{field} = IN.{attribute};"
        assert equation in text,equation
        vertex_bindings.append({"terminal":"VertexDescription."+field,"serialized_slot_value":terminal["literal"],"unity_generated_binding":"IN."+attribute,"treat_as_constant_zero":False,"requires_original_vertex_attribute":True})
    pins.update({str(p):sha(p) for p in [receipt_path,Path(__file__),generated,SDK/"RUN-RESULT.v1.json",SDK/"NATIVE-RESULT.v1.json",SDK/"PREPARED.v1.json",SDK/"Unity.log"]})
    result={"status":"source_paint_ir_objects_and_sdk_vertex_bindings_independently_reopened","sub_lot":"L03.TOWNSMITH.SHADER.S_PAINT.SOURCE","finished_utc":datetime.now(timezone.utc).isoformat(),"original_graphs":original_graphs,"active_nodes_reopened":len(refs),"input_slots_reopened":input_slots,"selected_branches_reopened":len(ir["selected_branches"]),"subgraph_instances_reopened":len(ir["subgraph_bindings"]),"terminals":list(ir["terminals"]),"vertex_default_bindings":vertex_bindings,"sdk_generated_source":str(generated),"sdk_generated_source_sha256":sha(generated),"old_importer_signature_failure_preserved":str(W/"townsmith-s-paint-sdk-generated-source-v1/RUN-RESULT.v1.json"),"source_numeric_gpu_semantics_qualified":False,"wgsl_generated":False,"forge_material_render_qualified":False,"forge_editor_verified":False,"full_import":False,"complete_assets_delivered_increment":0,"next_action":"Translate all active operations with Unity-resolved types, precision, original-space inputs and exact texture sampling; compare against native source on GPU","input_pins":pins}
    OUT.mkdir();(OUT/"RESULT.v1.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    print(json.dumps({k:v for k,v in result.items() if k not in ["input_pins","vertex_default_bindings","terminals"]}))
if __name__=="__main__":main()

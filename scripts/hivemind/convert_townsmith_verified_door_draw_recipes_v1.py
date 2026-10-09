"""Convert measured colour assignments into reversible per-binding recipes.

Retain complete geometry and source slots. These recipes are not delivered assets.
"""
from pathlib import Path
import json,hashlib
from datetime import datetime,timezone
W=Path("F:/OpenRPG_ImportWork");OUT=W/"townsmith-verified-door-draw-recipes-v1"
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def key(r):return r["guid"],str(r["file_id"])
def main():
    assert not OUT.exists()
    scope_path=W/"townsmith-current-draw-binding-scope-v1/RESULT.v1.json"
    proof_path=W/"townsmith-shadow-mode-controls-reopen-v1/RESULT.v1.json"
    request_path=W/"townsmith-shadow-mode-controls-v1/REQUEST.v1.json"
    report_path=W/"townsmith-shadow-mode-controls-v1/NATIVE-RESULT.v1.json"
    scope=read(scope_path);proof=read(proof_path);request=read(request_path);report=read(report_path)
    assert proof["source_inputs_unchanged"] and not proof["forge_conversion_qualified"]
    pins={str(p):sha(p) for p in [scope_path,proof_path,request_path,report_path,Path(__file__)]}
    for p,h in proof["input_pins"].items():assert sha(p)==h,p
    evidence={};native={r["id"]:r for r in report["rows"]}
    for case in request["cases"]:
        mesh_key=key(case["mesh"])
        evidence.setdefault(mesh_key,{"cases":{},"mesh":case["mesh"],"materials":case["materials"]})
        observation=next(r for r in proof["comparisons"] if r["id"]==case["id"])
        evidence[mesh_key]["cases"][case["control_mode"]]=observation
    assert len(evidence)==3
    for item in evidence.values():
        for mode in ["source_default","visible_default","visible_cast_off","visible_shadowed"]:
            observation=item["cases"][mode]
            assert not observation["reference_is_this_capture"] and observation["reference_bytes_identical"]
        assert item["cases"]["source_shadowed"]["legacy_source_changed_pixels"]>1000
        assert item["cases"]["source_default"]["light_shadows"]=="None"
        assert item["cases"]["visible_shadowed"]["light_shadows"]=="Soft"
        assert len(item["materials"])==1
    bindings=[];geometries={}
    for row in scope["rows"]:
        mesh_key=key(row["source_mesh_identity"])
        if mesh_key not in evidence:continue
        item=evidence[mesh_key];geometry=Path(row["geometry_derivative"]);receipt=geometry/"GEOMETRY.v1.json"
        bank_path=geometry.parent.parent/"RESULT.v1.json";bank=read(bank_path)
        assert bank["verified_meshes"]==bank["expected_meshes"]==len(bank["rows"])
        assert bank["status"] in ["verified_townsmith_geometry_derivatives_reopened","20_verified_townsmith_geometry_derivatives_reopened"]
        selected=next(r for r in bank["rows"] if key(r["source_mesh_identity"])==mesh_key)
        assert Path(selected["derivative"])==geometry and selected["geometry_receipt_sha256"]==sha(receipt)
        data=read(receipt);metadata=data["metadata"]
        assert (metadata["actual_guid"],str(metadata["actual_file_id"]))==mesh_key
        assert metadata["submeshes"]==row["source_submesh_count"]==2
        assert metadata["vertices"]==native[item["cases"]["source_default"]["id"]]["vertices"]
        assert [key(m) for m in row["material_slots"]]==[key(m) for m in item["materials"]]
        for name,stream in data["derived_streams"].items():
            p=geometry/name
            assert p.stat().st_size==stream["bytes"] and sha(p)==stream["sha256"]
            pins[str(p)]=stream["sha256"]
        pins[str(receipt)]=sha(receipt);pins[str(bank_path)]=sha(bank_path)
        geometries["/".join(mesh_key)]={"derivative":str(geometry),"vertices":metadata["vertices"],"retained_submeshes":2,"all_original_submesh_streams_retained":True,"geometry_receipt":str(receipt)}
        bindings.append({
            "prefab_asset_id":row["prefab_asset_id"],"node_path":row["node_path"],
            "renderer_identity":row["renderer_identity"],"source_mesh_identity":row["source_mesh_identity"],
            "original_material_slots":row["material_slots"],"geometry_derivative":str(geometry),
            "visible_draw_recipe":[{"submesh":0,"source_material_slot":0,"indices_stream":str(geometry/"submesh-0.target.u32")}],
            "remaining_submeshes":[{"submesh":1,"indices_stream":str(geometry/"submesh-1.target.u32"),"preserved":True,"no_colour_draw_in_measured_context":True}],
            "source_gpu_visible_equivalence":{"context":"isolated HDRP 17.5 on Unity 6000.5.8f1; fixed camera, directional light","light_modes":["None","Soft"],"proof":str(proof_path)},
            "source_shadow_policy_preserved_as_unresolved":True,
            "native_draw_event_count_observed":False,"prefab_transform_render_qualified":False,
            "forge_material_program_qualified":False,"forge_runtime_render_qualified":False,"forge_editor_verified":False,
        })
    assert len(bindings)==15 and len(geometries)==3
    identities=[(r["prefab_asset_id"],r["node_path"],key(r["renderer_identity"])) for r in bindings]
    assert len(set(identities))==15
    result={
        "schema":"openrpg.source-visible-draw-recipes.v1","status":"fifteen_source_verified_door_colour_bindings_converted_complete_geometry_retained",
        "sub_lot":"L03.TOWNSMITH.CONVERSION.AFFECTATIONS_PORTES","finished_utc":datetime.now(timezone.utc).isoformat(),
        "bindings_converted":15,"unique_meshes":3,"bindings":bindings,"geometries":geometries,
        "original_sources_material_slots_and_remaining_geometry_preserved":True,"shadow_policy_qualified":False,
        "scope":"Source-GPU colour recipe only; no event-count proof, original Unity-version qualification, Forge shader fidelity or final prefab/editor qualification.",
        "glb_delivered":False,"forge_editor_verified":False,"full_import":False,"complete_assets_delivered_increment":0,
        "next_action":"Convert and qualify original material graph for these recipes, then apply to complete prefab copies and verify Forge rendering and editor usage",
        "input_pins":pins,
    }
    OUT.mkdir()
    p=OUT/"DRAW_BINDINGS.v1.json";p.write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding="utf-8")
    reopened=read(p)
    assert reopened["bindings"]==bindings and reopened["geometries"]==geometries
    for p,h in pins.items():assert sha(p)==h,p
    print(json.dumps({"status":result["status"],"bindings":15,"meshes":3,"pins_verified":len(pins),"full_import":False}))
if __name__=="__main__":main()

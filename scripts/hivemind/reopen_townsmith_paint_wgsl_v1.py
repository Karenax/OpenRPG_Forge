from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,re,shutil
W=Path("F:/OpenRPG_ImportWork");ROOT=W/"townsmith-s-paint-wgsl-v1";OUT=W/"townsmith-s-paint-wgsl-reopen-v1"
R=Path(__file__).parents[2]
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def main():
    assert not OUT.exists()
    receipt_path=ROOT/"RESULT.v1.json";receipt=read(receipt_path)
    assert receipt["emitter_exit_code"]==receipt["validator_exit_code"]==0
    assert receipt["translated_operations"]==134 and receipt["normal_texture_bindings"]==3
    pins=dict(receipt["input_pins"])
    for p,h in pins.items():assert sha(p)==h,p
    types_path=W/"townsmith-s-paint-native-types-v3/RESULT.v1.json";types=read(types_path)
    ir_path=W/"townsmith-s-paint-source-program-v1/material-family-active-ir-v1/abc00000000009486088228754689894.json";ir=read(ir_path)
    shader=Path(receipt["shader"]);text=shader.read_text()
    lines={m[1]:(m[2],m[3],m[4]) for m in re.finditer(r"let n(\d+):(f32|vec[234]<f32>)=.*?; // (\w+) ([a-f0-9]{32})",text)}
    assert len(lines)==len(ir["nodes"])==134
    lookup={r["ir_id"]:r for r in types["rows"]}
    for node in ir["nodes"]:
        expected="f32" if lookup[node["id"]]["width"]==1 else f'vec{lookup[node["id"]]["width"]}<f32>'
        assert lines[node["id"]]==(expected,node["opcode"],node["node_id"])
    generation_path=ROOT/"typed-program-v1/GENERATION.v1.json";generation=read(generation_path)
    assert len(generation["textures"])==10 and len(generation["normal_texture_indices"])==3
    declared=[int(v) for v in re.findall(r"var t(\d+):texture_2d<f32>;",text)]
    assert declared==list(range(10))
    assert "normal_unpack_modes:vec3<u32>" in text and "source_tangent_space_normal" in text
    assert "packed.a" in text and "max(1.0e-16" in text
    validator=ROOT/"validator.stdout.log"
    assert "PASS_REAL_BEVY0191_NAGA_OIL_COMPOSITION" in validator.read_text()
    source_assets=R/"crates/forge_assets/src/source_graph_bundle.rs"
    source_engine=R/"crates/forge_engine/src/source_graph.rs"
    assert re.search(r"required_texture_count\s*>\s*7",source_engine.read_text())
    assert re.search(r"self\.textures\.len\(\)\s*>\s*7",source_assets.read_text())
    OUT.mkdir();snapshots=OUT/"reference-engine-contracts";snapshots.mkdir()
    for p in [source_assets,source_engine]:
        copy=snapshots/p.name;shutil.copy2(p,copy);assert sha(copy)==sha(p);pins[str(copy)]=sha(copy)
    for p in [receipt_path,types_path,ir_path,generation_path,validator,ROOT/"validator.stderr.log",Path(__file__)]:pins[str(p)]=sha(p)
    result={"status":"all_134_native_typed_paint_wgsl_operations_reopened_runtime_texture_capacity_gap_verified","sub_lot":"L03.TOWNSMITH.SHADER.S_PAINT.WGSL","finished_utc":datetime.now(timezone.utc).isoformat(),"operations_reopened":134,"shader_fragment_type_validation":True,"texture_bindings_required":10,"normal_texture_bindings":3,"normal_unpack_profile_count":3,"source_implicit_bent_normal_input_preserved":True,"current_common_runtime_texture_capacity_snapshot":7,"runtime_texture_capacity_change_required":True,"original_engine_contract_snapshots":str(snapshots),"source_texture_import_qualified":False,"source_numeric_gpu_equivalence_qualified":False,"forge_material_render_qualified":False,"forge_editor_verified":False,"full_import":False,"complete_assets_delivered_increment":0,"next_action":"Extend the common authored-texture contract to ten bindings without scene-binding collisions, qualify imported resources and normal-format profiles, then compare source and Forge on GPU","input_pins":pins}
    (OUT/"RESULT.v1.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    print(json.dumps({k:v for k,v in result.items() if k!="input_pins"}))
if __name__=="__main__":main()

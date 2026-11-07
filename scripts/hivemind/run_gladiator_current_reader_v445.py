"""Build and execute actual current Forge carrier/geometry readers on all five GLBs."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import shutil
import subprocess
import traceback

S = Path(__file__).resolve().parent
R = S.parents[1]
W = Path('F:/OpenRPG_ImportWork')
O = W / 'gladiator-current-product-reader-v445'
OLD = W / 'firepit-static-glb-product-check-v2'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert not O.exists()
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
def save(state):
    state.update(updated_utc=datetime.now(timezone.utc).isoformat(), full_import=False, gpu_rendered=False, studio_verified=False)
    p = O/'RUN-RESULT.tmp'
    p.write_text(json.dumps(state,indent=2),'utf-8')
    p.replace(O/'RUN-RESULT.v1.json')

O.mkdir(); (O/'src').mkdir(); (O/'temp').mkdir()
manifest = (OLD/'Cargo.toml').read_text('utf-8')
manifest = manifest.replace('edition="2024"','edition="2024"\nautobins=false')
manifest += '\n[[bin]]\nname="gladiator-current-reader-v445"\npath="src/main.rs"\n'
(O/'Cargo.toml').write_text(manifest,'utf-8')
(O/'Cargo.lock').write_bytes((OLD/'Cargo.lock').read_bytes())
source = r'''use serde_json::json;
fn main() {
 let path=std::env::args().nth(1).expect("explicit GLB");
 let raw=std::fs::read(&path).expect("GLB bytes");
 let parsed=forge_assets::source_graph_glb::read_source_graph_glb(&raw).expect("actual current carrier admission").expect("source graph required");
 let doc=parsed.document();
 let mut primitives=0usize;
 let mut vertices=0usize;
 let mut triangles=0usize;
 for (mesh_index,mesh) in doc["meshes"].as_array().unwrap().iter().enumerate() {
  for primitive_index in 0..mesh["primitives"].as_array().unwrap().len() {
   let g=forge_assets::source_graph_attributes::read_source_graph_primitive_geometry(&parsed,mesh_index,primitive_index).expect("actual current geometry admission");
   assert_eq!(g.positions.len(),g.normals.len());
   assert_eq!(g.positions.len(),g.uv0.len());
   assert_eq!(g.positions.len(),g.source.original_uv0.len());
   assert!(g.tangents.is_some() && g.source.original_tangents.is_some());
   assert!(g.source.original_additional_uvs[0].is_some());
   assert!(g.additional_uvs[0].is_some());
   assert!(g.source.original_colors.is_none());
   primitives+=1; vertices+=g.positions.len(); triangles+=g.indices.len()/3;
  }
 }
 let json_len=u32::from_le_bytes(raw[12..16].try_into().unwrap()) as usize;
 let view_index=doc["materials"][0]["extensions"]["OPENRPG_source_graph"]["forward"]["buffer_view"].as_u64().unwrap() as usize;
 let at=doc["bufferViews"][view_index]["byteOffset"].as_u64().unwrap() as usize;
 drop(parsed);
 let mut changed=raw.clone(); changed[28+json_len+at]^=1;
 assert!(forge_assets::source_graph_glb::read_source_graph_glb(&changed).is_err());
 changed[0]=0;
 assert!(forge_assets::source_graph_glb::read_source_graph_glb(&changed).is_err());
 println!("{}",json!({"status":"actual_current_source_carrier_and_geometry_admitted","glb":path,"sha256":forge_core::bytes_sha256(&raw).to_string(),"primitives":primitives,"primitive_vertex_instances":vertices,"triangles":triangles,"original_uv1_admitted":true,"shader_hash_negative_control":true,"header_negative_control":true,"gpu_rendered":false,"full_import":false}));
}
'''
(O/'src/main.rs').write_text(source,'utf-8')
assembly_path = W/'gladiator-autonomous-glb-v442/RESULT.v1.json'
reopen_path = W/'gladiator-glb-independent-reopen-v444/RESULT.v1.json'
assembly, reopened = read(assembly_path), read(reopen_path)
assert len(assembly['rows'])==len(reopened['rows'])==5
pins = {str(p):sha(p) for crate in ['forge_assets','forge_core'] for p in (R/'crates'/crate).rglob('*') if p.is_file() and (p.suffix=='.rs' or p.name=='Cargo.toml')}
for p in [Path(__file__),O/'Cargo.toml',O/'Cargo.lock',O/'src/main.rs',R/'Cargo.toml',R/'Cargo.lock',R/'rust-toolchain.toml',assembly_path,reopen_path]: pins[str(p)]=sha(p)
for row in assembly['rows']:
    assert sha(row['glb'])==row['sha256']; pins[row['glb']]=row['sha256']
(O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,rows=assembly['rows']),indent=2),'utf-8')
state = dict(status='prepared_current_gladiator_product_reader',rows=[])
save(state)
try:
    env=os.environ.copy()
    env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(OLD/'target'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',TEMP=str(O/'temp'),TMP=str(O/'temp'))
    with (O/'cargo.log').open('xb') as out:
        p=subprocess.Popen([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml'),'--bin','gladiator-current-reader-v445'],env=env,cwd=O,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
        state.update(status='building_current_gladiator_reader',cargo_pid=p.pid); save(state)
        state['cargo_exit_code']=p.wait()
    assert state['cargo_exit_code']==0,'See cargo.log'
    exe=O/'current-product-reader.exe'
    shutil.copyfile(OLD/'target/debug/gladiator-current-reader-v445.exe',exe)
    state['checker_sha256']=sha(exe)
    for row in assembly['rows']:
        case=O/row['prefab_guid']; case.mkdir()
        with (case/'stdout.json').open('xb') as out,(case/'stderr.log').open('xb') as err:
            p=subprocess.Popen([str(exe),row['glb']],env=env,cwd=O,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='executing_current_gladiator_reader',checker_pid=p.pid,current_prefab=row['prefab_guid']); save(state)
            exit_code=p.wait()
        assert exit_code==0,(case/'stderr.log').read_text('utf-8',errors='replace')[-2200:]
        checked=read(case/'stdout.json'); assert checked['sha256']==row['sha256']
        checked.update(prefab_guid=row['prefab_guid'],actual_exit_code=exit_code,stdout_sha256=sha(case/'stdout.json'),stderr_sha256=sha(case/'stderr.log'))
        state['rows'].append(checked)
        save(state)
    assert len(state['rows'])==5 and sum(r['primitives'] for r in state['rows'])==24
    assert sum(r['triangles'] for r in state['rows'])==10000
    for p,h in pins.items(): assert sha(p)==h,p
    state.update(status='all_five_gladiator_glbs_admitted_by_current_product_readers',input_pins=pins,source_inputs_unchanged=True)
except Exception as e:
    state.update(status='gladiator_current_product_reader_refusal_retained',error=repr(e),traceback=traceback.format_exc())
state['finished_utc']=datetime.now(timezone.utc).isoformat(); save(state)
print(json.dumps(dict(status=state['status'],accepted=len(state['rows']),error=state.get('error'))))

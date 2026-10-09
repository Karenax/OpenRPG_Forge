"""Exercise the installed state adapter on preserved scene headers and real ECS entities."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, struct, subprocess, sys

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'studio-source-state-native-v1'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
MODULE = R / 'apps/forge_studio/src/authoring_assets/source_graph_state.rs'

def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()
def save(state):
    state['updated_utc'] = datetime.now(timezone.utc).isoformat()
    tmp = ROOT / 'RUN-RESULT.tmp'
    tmp.write_text(json.dumps(state, indent=2), 'utf-8')
    tmp.replace(ROOT / 'RUN-RESULT.v1.json')

TESTS = r'''
use bevy::{prelude::*, light::{NotShadowCaster, NotShadowReceiver}};
use serde_json::Value;
use std::collections::BTreeMap;
use source_graph_state::*;
fn fixtures() -> Vec<Value> { vec![serde_json::from_str(include_str!("../scene-0.json")).unwrap(), serde_json::from_str(include_str!("../scene-1.json")).unwrap()] }
fn hierarchy(doc: &Value) -> (BTreeMap<usize, Option<usize>>, Vec<usize>) {
    let nodes = doc["nodes"].as_array().unwrap();
    let mut parents: BTreeMap<_, _> = (0..nodes.len()).map(|i| (i, None)).collect();
    let mut draws = vec![];
    for (i, n) in nodes.iter().enumerate() {
        if let Some(children) = n["children"].as_array() { for c in children { parents.insert(c.as_u64().unwrap() as usize, Some(i)); } }
        if let Some(mesh) = n["mesh"].as_u64() { for _ in doc["meshes"][mesh as usize]["primitives"].as_array().unwrap() { draws.push(i); } }
    }
    (parents, draws)
}
fn plan(doc: &Value) -> Result<StatePlan, String> { let (p,d) = hierarchy(doc); prepare(doc,&p,&d)?.ok_or("No authored state".into()) }
fn renderer_index(doc: &Value) -> usize { doc["nodes"].as_array().unwrap().iter().position(|n| !n["extras"]["unity_source_node"]["renderers"].as_array().unwrap().is_empty()).unwrap() }
fn mount(p: &StatePlan, parents: &BTreeMap<usize,Option<usize>>, draws: &[usize]) -> (World,Entity,BTreeMap<usize,Entity>,Vec<Entity>) {
    let mut w = World::new(); let root = w.spawn(Visibility::Hidden).id();
    let nodes: BTreeMap<_,_> = parents.keys().map(|i| (*i,w.spawn(Visibility::Inherited).id())).collect();
    for (i,parent) in parents { w.entity_mut(nodes[i]).insert(ChildOf(parent.map(|a| nodes[&a]).unwrap_or(root))); }
    let prim: Vec<_> = draws.iter().map(|i| w.spawn((Visibility::Inherited, ChildOf(nodes[i]))).id()).collect();
    p.apply(&mut w,root,&nodes,&prim); (w,root,nodes,prim)
}
#[test]
fn preserved_scenes_apply_exact_flags_and_lod_identities_to_real_entities() {
    let mut counts = [0,0,0];
    for doc in fixtures() { let (parents, draws) = hierarchy(&doc); let p = plan(&doc).unwrap();
        counts[0] += p.active_nodes.len(); counts[1] += p.renderers.len(); counts[2] += p.lod_groups.len();
        let (w,root,nodes,prim) = mount(&p,&parents,&draws);
        assert_eq!(*w.get::<Visibility>(root).unwrap(), Visibility::Hidden);
        assert!(w.get::<OriginalLodDriverPending>(root).is_some());
        for (i,active) in &p.active_nodes { assert_eq!(*w.get::<Visibility>(nodes[i]).unwrap(), if *active {Visibility::Inherited} else {Visibility::Hidden}); }
        for (i,g) in &p.lod_groups { assert_eq!(&w.get::<OriginalLodGroup>(nodes[i]).unwrap().0,g); }
        for (e,i) in prim.iter().zip(&draws) { let a=w.get::<OriginalRendererState>(*e).unwrap(); let b=&p.renderers[i];
            assert_eq!((&a.guid,&a.file_id,a.enabled,a.force_rendering_off,a.shadow_casting_mode,a.receive_shadows),(&b.guid,&b.file_id,b.enabled,b.force_rendering_off,b.shadow_casting_mode,b.receive_shadows));
            assert_eq!(*w.get::<Visibility>(*e).unwrap(),if b.enabled && !b.force_rendering_off {Visibility::Inherited} else {Visibility::Hidden});
            assert_eq!(w.get::<NotShadowCaster>(*e).is_some(),b.shadow_casting_mode==0);
            assert_eq!(w.get::<NotShadowReceiver>(*e).is_some(),!b.receive_shadows);
        }
    } assert_eq!(counts,[95,63,14]);
}
#[test]
fn missing_force_flag_and_wrong_active_chain_are_refused() {
    for mut doc in fixtures() { let i=renderer_index(&doc); let old=doc.clone(); doc["nodes"][i]["extras"]["unity_source_node"]["renderers"][0].as_object_mut().unwrap().remove("force_rendering_off"); assert!(plan(&doc).is_err());
        doc=old; let b=doc["nodes"][i]["extras"]["unity_source_node"]["active_source_parent_chain"].as_bool().unwrap(); doc["nodes"][i]["extras"]["unity_source_node"]["active_source_parent_chain"]=(!b).into(); assert!(plan(&doc).is_err()); }
}
#[test]
fn lod_wrong_identity_and_invalid_order_are_refused() {
    for mut doc in fixtures() { let old=doc.clone(); doc["extras"]["original_lod_groups"][0]["levels"][0]["renderer_components"][0]["guid"]="00000000000000000000000000000000".into(); assert!(plan(&doc).is_err());
        doc=old; doc["extras"]["original_lod_groups"][0]["levels"][0]["screen_relative_height"]=1.5.into(); assert!(plan(&doc).is_err()); }
}
#[test]
fn prototype_identity_reuse_is_resolved_by_instance_path() {
    let mut doc=fixtures().remove(0); let i=renderer_index(&doc); let component=doc["nodes"][i]["extras"]["unity_source_node"]["renderers"][0]["component"].clone();
    let mut changed=0;
    for n in doc["nodes"].as_array_mut().unwrap() { for r in n["extras"]["unity_source_node"]["renderers"].as_array_mut().unwrap() { r["component"]=component.clone(); changed+=1; } }
    for group in doc["extras"]["original_lod_groups"].as_array_mut().unwrap() { for level in group["levels"].as_array_mut().unwrap() { for id in level["renderer_components"].as_array_mut().unwrap() { id["guid"]=component["guid"].clone(); id["file_id"]=component["file_id"].clone(); } } }
    assert!(changed>1); assert_eq!(plan(&doc).unwrap().renderers.len(),57);
}
#[test]
fn disabled_and_forced_off_renderers_hide_actual_primitives() {
    for force in [false,true] { let mut doc=fixtures().remove(0); let (_,draws)=hierarchy(&doc); let i=draws[0];
        let r=&mut doc["nodes"][i]["extras"]["unity_source_node"]["renderers"][0]; r["enabled"]=force.into(); r["force_rendering_off"]=force.into(); r["shadow_casting_mode"]=0.into(); r["receive_shadows"]=false.into();
        let (parents,draws)=hierarchy(&doc); let p=plan(&doc).unwrap(); let (w,_,_,prim)=mount(&p,&parents,&draws);
        for (e,j) in prim.iter().zip(draws) { if j==i { assert_eq!(*w.get::<Visibility>(*e).unwrap(),Visibility::Hidden); assert!(w.get::<NotShadowCaster>(*e).is_some()); assert!(w.get::<NotShadowReceiver>(*e).is_some()); } }
    }
}
#[test]
fn authored_inactive_parent_preserves_hidden_hierarchy() {
    let mut doc=fixtures().remove(0); let (parents,draws)=hierarchy(&doc); let i=*parents.iter().find(|(_,p)|p.is_none()).unwrap().0;
    doc["nodes"][i]["extras"]["unity_source_node"]["active_self"]=false.into();
    for j in parents.keys() { let mut next=Some(*j); let mut active=true; while let Some(k)=next { active &= doc["nodes"][k]["extras"]["unity_source_node"]["active_self"].as_bool().unwrap(); next=parents[&k]; } doc["nodes"][*j]["extras"]["unity_source_node"]["active_source_parent_chain"]=active.into(); }
    let p=plan(&doc).unwrap(); let (w,root,nodes,_)=mount(&p,&parents,&draws); assert_eq!(*w.get::<Visibility>(nodes[&i]).unwrap(),Visibility::Hidden); assert_eq!(*w.get::<Visibility>(root).unwrap(),Visibility::Hidden);
}
#[test]
fn generic_scene_without_source_metadata_keeps_compatibility() {
    assert!(prepare(&serde_json::json!({"nodes":[{}]}),&BTreeMap::from([(0,None)]),&[]).unwrap().is_none());
}
'''

def prepare_run():
    prior = read(W/'studio-current-source-wasm-check-v8/RUN-RESULT.v1.json')
    assert prior['status'] == 'actual_studio_wasm_frontend_compile_check_passed'
    assert not prior['changed_source_files']
    assert not ROOT.exists()
    ROOT.mkdir(); (ROOT/'src').mkdir()
    receipt = W/'rocks-firepit-renderer-state-glb-v1/RESULT.v1.json'
    rows = read(receipt)['rows']; assert len(rows)==2
    pins = {str(MODULE):sha(MODULE), str(Path(__file__).resolve()):sha(__file__), str(receipt):sha(receipt)}
    for i,row in enumerate(rows):
        source=Path(row['target']); assert sha(source)==row['target_sha256']
        with source.open('rb') as f:
            magic,version,length=struct.unpack('<III',f.read(12)); assert magic==0x46546c67 and version==2 and length==source.stat().st_size
            size,kind=struct.unpack('<II',f.read(8)); assert kind==0x4e4f534a and size<=16*1024*1024
            doc=json.loads(f.read(size))
        p=ROOT/f'scene-{i}.json'; p.write_text(json.dumps(doc,separators=(',',':')),'utf-8'); pins[str(p)]=sha(p); pins[str(source)]=row['target_sha256']
    cargo=ROOT/'Cargo.toml'; cargo.write_text('[package]\nname="openrpg-studio-source-state-check"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\nbevy={version="=0.19.1",default-features=false,features=["3d","ui"]}\nbevy_ecs="=0.19.1"\nserde_json={version="=1.0.151",features=["float_roundtrip"]}\n','utf-8')
    wrapper=ROOT/'src/lib.rs'; wrapper.write_text('#[path="'+MODULE.as_posix()+'"]\nmod source_graph_state;\n'+TESTS,'utf-8')
    for p in [cargo,wrapper,W/'studio-current-source-wasm-check-v8/RUN-RESULT.v1.json']:
        pins[str(p)]=sha(p)
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,expected_tests=7),indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=child.pid),indent=2),'utf-8');print(json.dumps(dict(pid=child.pid,expected_tests=7)))

def run():
    state=dict(status='checking_installed_source_state',gpu_rendered=False,studio_verified=False,full_import=False)
    save(state)
    try:
        pins=read(ROOT/'PREPARED.v1.json')['input_pins']
        for p,digest in pins.items(): assert sha(p)==digest,p
        env=os.environ.copy(); env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        for stage,args in [('lock',['generate-lockfile','--offline']),('native',['test','--locked','--offline','--lib','--','--nocapture'])]:
            state['status']='checking_installed_source_state_'+stage;save(state)
            with (ROOT/(stage+'.stdout.log')).open('xb') as out,(ROOT/(stage+'.stderr.log')).open('xb') as err:
                proc=subprocess.Popen([str(BIN/'cargo.exe'),*args],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
                state['cargo_pid']=proc.pid;save(state);state['actual_exit_code']=proc.wait();assert proc.returncode==0,stage
        assert '7 passed; 0 failed' in (ROOT/'native.stdout.log').read_text('utf-8',errors='replace')
        for p,digest in pins.items():assert sha(p)==digest,p
        state.update(status='seven_actual_state_tests_and_both_preserved_scenes_passed',actual_tests_passed=7,nodes_verified=95,renderer_declarations_verified=63,lod_groups_retained=14,input_pins=pins,stdout_sha256=sha(ROOT/'native.stdout.log'),stderr_sha256=sha(ROOT/'native.stderr.log'),camera_lod_driver_qualified=False)
    except Exception as exc:state.update(status='actual_source_state_test_refusal_retained',error=repr(exc))
    finally:save(state)

if __name__=='__main__': prepare_run() if sys.argv[-1]=='prepare' else run()

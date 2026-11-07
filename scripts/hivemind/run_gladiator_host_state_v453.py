"""Execute unmodified current Studio state and hierarchy modules in an isolated World."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,re,shutil,subprocess,traceback
W=Path('F:/OpenRPG_ImportWork');R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');O=W/'gladiator-current-host-state-v453';B=W/'firepit-source-shadow-origin-native-v1';T=W/'firepit-static-glb-product-check-v2/target';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
 v.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False,gpu_rendered=False,studio_verified=False);p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
cargo=(B/'Cargo.toml').read_text('utf-8');cargo=re.sub(r'^forge_engine=.*\n','',cargo,flags=re.M).replace('edition="2024"','edition="2024"\nautobins=false');cargo+='\n[[bin]]\nname="gladiator-host-state-v453"\npath="src/main.rs"\n';(O/'Cargo.toml').write_text(cargo,'utf-8')
lock=(B/'Cargo.lock').read_text('utf-8');start=lock.index('name = "openrpg-firepit-source-ao-scene-v1"');end=lock.index('[[package]]',start);block=lock[start:end];assert block.count(' "forge_engine",')==1;lock=lock[:start]+block.replace(' "forge_engine",\n','')+lock[end:];(O/'Cargo.lock').write_text(lock,'utf-8')
state_module=R/'apps/forge_studio/src/authoring_assets/source_graph_state.rs';scene_module=R/'crates/forge_engine/src/source_graph_scene.rs'
source='#[path="'+state_module.as_posix()+'"] mod source_state;\n#[path="'+scene_module.as_posix()+'"] mod source_scene;\n'+r'''
use bevy::prelude::*;
use std::collections::BTreeMap;
fn main(){
 let path=std::env::args().nth(1).unwrap();let raw=std::fs::read(&path).unwrap();
 let glb=forge_assets::source_graph_glb::read_source_graph_glb(&raw).unwrap().unwrap();
 let hierarchy=source_scene::read_source_graph_scene(&glb,None).unwrap();
 let parents:BTreeMap<_,_>=hierarchy.nodes.iter().map(|n|(n.source_node,n.parent_source_node)).collect();
 let draws:Vec<_>=hierarchy.nodes.iter().filter(|n|n.mesh.is_some()).map(|n|n.source_node).collect();
 let plan=source_state::prepare(glb.document(),&parents,&draws).unwrap().unwrap();
 assert_eq!(plan.renderers.len(),draws.len());assert_eq!(plan.lod_groups.len(),1);
 let mut world=World::new();let root=world.spawn((Transform::IDENTITY,GlobalTransform::IDENTITY,Visibility::Hidden)).id();
 let nodes=source_scene::spawn_source_graph_hierarchy(&hierarchy,&mut world,root).unwrap();
 let mut primitives=Vec::new();
 for node in &hierarchy.nodes {if let Some(mesh)=node.mesh {for _ in glb.document()["meshes"][mesh]["primitives"].as_array().unwrap(){primitives.push(world.spawn((ChildOf(nodes[&node.source_node]),Visibility::Hidden)).id());}}}
 plan.apply(&mut world,root,&nodes,&primitives);
 assert!(world.get::<source_state::OriginalLodDriverPending>(root).is_some());
 assert_eq!(world.get::<Visibility>(root),Some(&Visibility::Hidden));
 for entity in &primitives {let r=world.get::<source_state::OriginalRendererState>(*entity).unwrap();assert_eq!(*world.get::<Visibility>(*entity).unwrap(),if r.enabled&&!r.force_rendering_off{Visibility::Inherited}else{Visibility::Hidden});assert_eq!(world.get::<bevy::light::NotShadowCaster>(*entity).is_some(),r.shadow_casting_mode==0);assert_eq!(world.get::<bevy::light::NotShadowReceiver>(*entity).is_some(),!r.receive_shadows);}
 let mut bad=glb.document().clone();bad["nodes"][0]["extras"]["unity_source_node"]["active_source_parent_chain"]=serde_json::json!(false);assert!(source_state::prepare(&bad,&parents,&draws).is_err());
 println!("{}",serde_json::json!({"status":"actual_current_host_state_and_hierarchy_applied","glb":path,"nodes":nodes.len(),"renderers":plan.renderers.len(),"primitives":primitives.len(),"lod_groups":plan.lod_groups.len(),"root_hidden_pending_lod_driver":true,"invalid_active_chain_refused":true,"gpu_rendered":false,"full_import":false}));
}
''';(O/'src/main.rs').write_text(source,'utf-8')
assembly_path=W/'gladiator-runtime-state-glb-v452/RESULT.v1.json';assembly=read(assembly_path)
pins={str(p):sha(p) for p in [Path(__file__),state_module,scene_module,assembly_path,O/'src/main.rs',O/'Cargo.toml',O/'Cargo.lock',R/'Cargo.toml',R/'Cargo.lock']}
for crate in ['forge_assets','forge_core']:
 for p in (R/'crates'/crate).rglob('*'):
  if p.is_file() and (p.suffix=='.rs' or p.name=='Cargo.toml'):pins[str(p)]=sha(p)
for row in assembly['rows']:assert sha(row['glb'])==row['sha256'];pins[row['glb']]=row['sha256']
(O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,rows=assembly['rows']),indent=2),'utf-8');state=dict(status='host_state_prepared',rows=[]);save(state)
try:
 env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(T),CARGO_INCREMENTAL='0',CARGO_BUILD_JOBS='1',TEMP=str(O/'temp'),TMP=str(O/'temp'))
 with (O/'cargo.log').open('xb') as out:
  proc=subprocess.Popen([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml'),'--bin','gladiator-host-state-v453'],env=env,cwd=O,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='building_current_host_state_checker',cargo_pid=proc.pid);save(state);state['cargo_exit_code']=proc.wait()
 assert state['cargo_exit_code']==0,'See cargo.log'
 exe=O/'current-host-checker.exe';shutil.copyfile(T/'debug/gladiator-host-state-v453.exe',exe);state['checker_sha256']=sha(exe)
 for row in assembly['rows']:
  case=O/row['prefab_guid'];case.mkdir()
  with (case/'stdout.json').open('xb') as out,(case/'stderr.log').open('xb') as err:
   proc=subprocess.Popen([str(exe),row['glb']],env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='executing_current_host_state_checker',checker_pid=proc.pid);save(state);code=proc.wait()
  assert code==0,(case/'stderr.log').read_text('utf-8',errors='replace')[-1800:]
  checked=read(case/'stdout.json');checked.update(prefab_guid=row['prefab_guid'],actual_exit_code=code,stdout_sha256=sha(case/'stdout.json'));state['rows'].append(checked);save(state)
 assert len(state['rows'])==5 and sum(r['renderers'] for r in state['rows'])==17 and sum(r['primitives'] for r in state['rows'])==24
 for p,h in pins.items():assert sha(p)==h,p
 state.update(status='five_current_studio_host_state_and_hierarchy_world_checks_passed',input_pins=pins)
except Exception as e:state.update(status='current_host_state_refusal_retained',error=repr(e),traceback=traceback.format_exc())
state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state);print(json.dumps(dict(status=state['status'],accepted=len(state['rows']),error=state.get('error'))))

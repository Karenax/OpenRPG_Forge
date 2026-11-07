"""Compare the current common box adapter with the captured Unity references."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,re,shutil,subprocess,traceback
W=Path('F:/OpenRPG_ImportWork');R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');O=W/'gladiator-rapier-box-query-v475';B=W/'gladiator-viewport-lod-cycle-v469';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');T=W/'firepit-static-glb-product-check-v2/target'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
def save():
 state['updated_utc']=datetime.now(timezone.utc).isoformat();p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(state,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
name='gladiator-rapier-box-query-v475'
cargo=(B/'Cargo.toml').read_text('utf-8').split('[[bin]]')[0];cargo=re.sub(r'^forge_engine=.*\n','',cargo,flags=re.M)
cargo+='\nbevy_rapier3d={version="=0.36.0",default-features=false,features=["dim3"]}\n[patch.crates-io]\nrapier3d={path="'+(R/'third_party/rapier3d-0.35.0-glamx0.2').as_posix()+'"}\n[[bin]]\nname="'+name+'"\npath="src/main.rs"\n'
(O/'Cargo.toml').write_text(cargo,'utf-8');shutil.copyfile(R/'Cargo.lock',O/'Cargo.lock')
module=R/'crates/forge_engine/src/source_colliders.rs'
source='#[allow(dead_code)]\n#[path="'+module.as_posix()+'"] mod source_colliders;\n'+r'''
use bevy::prelude::*;
fn vec3(v:&serde_json::Value)->Vec3{Vec3::new(v[0].as_f64().unwrap() as f32,v[1].as_f64().unwrap() as f32,v[2].as_f64().unwrap() as f32)}
fn near(a:f32,b:f32)->bool{a.is_finite()&&b.is_finite()&&(a-b).abs()<=1e-6*b.abs().max(1.)}
fn vector_near(a:Vec3,b:Vec3)->bool{a.to_array().into_iter().zip(b.to_array()).all(|(a,b)|near(a,b))}
fn main(){
 let path=std::env::args().nth(1).unwrap();let raw=std::fs::read(&path).unwrap();let glb=forge_assets::source_graph_glb::read_source_graph_glb(&raw).unwrap().unwrap();let doc=glb.document();
 let native:serde_json::Value=serde_json::from_slice(&std::fs::read(std::env::args().nth(2).unwrap()).unwrap()).unwrap();let basis=Mat4::from_scale(Vec3::new(1.,1.,-1.));let boxes=source_colliders::prepare(doc,basis).unwrap();let mut points=0;let mut rays=0;let mut disabled_controls=0;
 for b in &boxes{
  let reference=native["boxes"].as_array().unwrap().iter().find(|v|v["source_guid"]==b.guid&&v["source_file_id"]==b.file_id).unwrap();assert!(b.enabled&&!b.is_trigger);assert_eq!(b.center,basis.transform_point3(vec3(&reference["local_center"])));assert_eq!(b.half_extents,vec3(&reference["local_size"])*.5);
  for sample in reference["points"].as_array().unwrap(){let query=basis.transform_point3(vec3(&sample["world_query"]));let expected=basis.transform_point3(vec3(&sample["closest_world"]));let actual=b.closest_point(query).unwrap();assert!(vector_near(actual,expected),"point: {actual:?} != {expected:?}");points+=1;}
  for sample in reference["rays"].as_array().unwrap(){let origin=basis.transform_point3(vec3(&sample["origin"]));let direction=basis.transform_vector3(vec3(&sample["direction"]));let result=b.raycast(origin,direction,sample["max_distance"].as_f64().unwrap() as f32).unwrap();assert_eq!(result.is_some(),sample["hit"].as_bool().unwrap());let hit=result.unwrap();assert!(near(hit.time_of_impact,sample["distance"].as_f64().unwrap() as f32));assert!(vector_near(hit.point,basis.transform_point3(vec3(&sample["point"]))));assert!(vector_near(hit.normal,basis.transform_vector3(vec3(&sample["normal"]))));rays+=1;}
  assert!(b.closest_point(Vec3::splat(f32::NAN)).is_err());assert!(b.raycast(Vec3::ZERO,Vec3::ZERO,1.).is_err());
  let mut disabled_doc=doc.clone();let properties=disabled_doc["nodes"][b.node]["extras"]["unity_source_node"]["native_collider_observations"][0]["serialized"].as_array_mut().unwrap();properties.iter_mut().find(|v|v["property_path"]=="m_Enabled").unwrap()["boolean"]=false.into();let disabled=source_colliders::prepare(&disabled_doc,basis).unwrap();let disabled=disabled.iter().find(|v|v.file_id==b.file_id&&v.guid==b.guid).unwrap();assert!(!disabled.enabled);let query=basis.transform_point3(vec3(&reference["disabled_query"]));assert!(vector_near(disabled.closest_point(query).unwrap(),basis.transform_point3(vec3(&reference["disabled_closest"]))));let ray=&reference["rays"][0];assert_eq!(disabled.raycast(basis.transform_point3(vec3(&ray["origin"])),basis.transform_vector3(vec3(&ray["direction"])),ray["max_distance"].as_f64().unwrap() as f32).unwrap().is_some(),reference["disabled_raycast_hit"].as_bool().unwrap());disabled_controls+=1;
  let mut bad=doc.clone();bad["nodes"][b.node]["extras"]["unity_source_node"]["native_collider_observations"][0]["identity"]["guid"]="00000000000000000000000000000000".into();assert!(source_colliders::prepare(&bad,basis).is_err());
 }
 assert!(source_colliders::prepare(doc,Mat4::from_scale(Vec3::splat(2.))).is_err());
 println!("{}",serde_json::json!({"status":"current_common_rapier_box_queries_match_unity","boxes":boxes.len(),"points":points,"rays":rays,"disabled_controls":disabled_controls,"invalid_identity_and_basis_refused":true,"invalid_query_refused":true,"product_rapier_patch_used":true,"physics_world_integration_qualified":false,"material_contact_simulation_qualified":false,"studio_gui_verified":false,"full_import":false}));
}
'''
(O/'src/main.rs').write_text(source,'utf-8')
native_path=W/'gladiator-native-box-metric-v471/NATIVE-RESULT.v1.json';assembly_path=W/'gladiator-runtime-state-glb-v452/RESULT.v1.json';assembly=read(assembly_path)
files=[Path(__file__),module,R/'crates/forge_engine/src/lib.rs',O/'Cargo.toml',O/'src/main.rs',R/'Cargo.toml',R/'Cargo.lock',native_path,assembly_path]
for root in [R/'third_party/rapier3d-0.35.0-glamx0.2',R/'crates/forge_assets',R/'crates/forge_core']:
 files.extend(p for p in root.rglob('*') if p.is_file() and (p.suffix=='.rs' or p.name=='Cargo.toml'))
pins={str(p):sha(p) for p in files}
for row in assembly['rows']:assert sha(row['glb'])==row['sha256'];pins[row['glb']]=row['sha256']
state=dict(status='prepared',rows=[],full_import=False,physics_world_integration_qualified=False);save()
env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(T),CARGO_INCREMENTAL='0',CARGO_BUILD_JOBS='1',TEMP=str(O/'temp'),TMP=str(O/'temp'))
try:
 with (O/'lock.log').open('xb') as log:
  code=subprocess.run([str(BIN/'cargo.exe'),'update','--offline','--workspace','--manifest-path',str(O/'Cargo.toml')],env=env,cwd=O,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW).returncode
 assert code==0,'Offline lock reconciliation refused';pins[str(O/'Cargo.lock')]=sha(O/'Cargo.lock')
 (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,product_rapier_patch_used=True),indent=2),'utf-8')
 with (O/'cargo.log').open('xb') as log:
  proc=subprocess.Popen([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml'),'--bin',name],env=env,cwd=O,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='building_current_common_box_adapter',cargo_pid=proc.pid);save();state['cargo_exit_code']=proc.wait()
 assert state['cargo_exit_code']==0,'See cargo.log'
 exe=O/'rapier-box-checker.exe';shutil.copyfile(T/'debug'/(name+'.exe'),exe);state['checker_sha256']=sha(exe)
 for row in assembly['rows']:
  case=O/row['prefab_guid'];case.mkdir()
  with (case/'stdout.json').open('xb') as out,(case/'stderr.log').open('xb') as err:
   proc=subprocess.Popen([str(exe),row['glb'],str(native_path)],env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='executing_current_common_box_adapter',checker_pid=proc.pid);save();code=proc.wait()
  assert code==0,(case/'stderr.log').read_text('utf-8',errors='replace')[-2400:]
  checked=read(case/'stdout.json');checked.update(prefab_guid=row['prefab_guid'],actual_exit_code=code,stdout_sha256=sha(case/'stdout.json'));state['rows'].append(checked);save()
 assert sum(r['boxes'] for r in state['rows'])==3 and sum(r['points'] for r in state['rows'])==63 and sum(r['rays'] for r in state['rows'])==18 and sum(r['disabled_controls'] for r in state['rows'])==3
 for p,h in pins.items():assert sha(p)==h,p
 state.update(status='three_common_rapier_boxes_match_63_unity_points_and_18_rays',input_pins=pins)
except Exception as e:state.update(status='common_rapier_box_refusal_retained',error=repr(e),traceback=traceback.format_exc())
state['finished_utc']=datetime.now(timezone.utc).isoformat();save();print(json.dumps(dict(status=state['status'],accepted=len(state['rows']),error=state.get('error'))))

"""Replay native cameras through actual Studio lifecycle and Bevy transform schedule."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,re,shutil,subprocess,tomllib,traceback
W=Path('F:/OpenRPG_ImportWork');R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');O=W/'gladiator-viewport-lod-cycle-v466';B=W/'studio-source-bootstrap-native-v1';T=W/'firepit-static-glb-product-check-v2/target';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
def save():
 state['updated_utc']=datetime.now(timezone.utc).isoformat();p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(state,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
name='openrpg-gladiator-viewport-lod-v466'
cargo=(B/'Cargo.toml').read_text('utf-8').replace('openrpg-studio-source-bootstrap-check',name)
(O/'Cargo.toml').write_text(cargo+'\n[[bin]]\nname="'+name+'"\npath="src/main.rs"\n','utf-8')
(O/'Cargo.lock').write_text((B/'Cargo.lock').read_text('utf-8').replace('openrpg-studio-source-bootstrap-check',name),'utf-8')
modules=['route','intake','scene','resources','frame','state','context','lights','host','lod','runtime']
paths=[R/'apps/forge_studio/src/authoring_assets'/('source_graph_'+m+'.rs') for m in modules]
source='\n'.join('#[allow(dead_code)]\n#[path="'+p.as_posix()+'"] mod source_graph_'+m+';' for p,m in zip(paths,modules))+r'''
use bevy::prelude::*;
use std::collections::{BTreeMap,BTreeSet};
use forge_engine::source_graph::SourceGraphMaterial;
fn vec3(v:&serde_json::Value)->Vec3 {Vec3::new(v[0].as_f64().unwrap() as f32,v[1].as_f64().unwrap() as f32,v[2].as_f64().unwrap() as f32)}
fn main(){
 let path=std::env::args().nth(1).unwrap();let raw=std::fs::read(&path).unwrap();let glb=forge_assets::source_graph_glb::read_source_graph_glb(&raw).unwrap().unwrap();
 let hierarchy=forge_engine::source_graph_scene::read_source_graph_scene(&glb,None).unwrap();
 let parents:BTreeMap<_,_>=hierarchy.nodes.iter().map(|n|(n.source_node,n.parent_source_node)).collect();let draws:Vec<_>=hierarchy.nodes.iter().filter(|n|n.mesh.is_some()).map(|n|n.source_node).collect();
 let plan=source_graph_state::prepare(glb.document(),&parents,&draws).unwrap().unwrap();let expected_groups=plan.lod_groups.clone();let expected_renderers=plan.renderers.clone();
 let basis=source_graph_context::ObjectBasis::from_document(glb.document()).unwrap().unwrap();assert_eq!(basis.target_from_source,Mat4::from_scale(Vec3::new(1.,1.,-1.)));
 let mut app=App::new();app.add_plugins((MinimalPlugins,bevy::transform::TransformPlugin));app.world_mut().init_resource::<Assets<SourceGraphMaterial>>();source_graph_runtime::register(&mut app);
 let root=app.world_mut().spawn((Transform::IDENTITY,GlobalTransform::IDENTITY,Visibility::Hidden,source_graph_scene::SourceSceneAttached)).id();
 let nodes=forge_engine::source_graph_scene::spawn_source_graph_hierarchy(&hierarchy,app.world_mut(),root).unwrap();let mut primitives=Vec::new();
 for n in &hierarchy.nodes {if let Some(mesh)=n.mesh {for _ in glb.document()["meshes"][mesh]["primitives"].as_array().unwrap(){primitives.push(app.world_mut().spawn((ChildOf(nodes[&n.source_node]),Visibility::Hidden)).id());}}}
 plan.apply(app.world_mut(),root,&nodes,&primitives);
 // LOD lifecycle scope: no GPU resources or material substitutes are admitted.
 let host=source_graph_host::SourceSceneHost{root,attached:source_graph_scene::AttachedScene{nodes:nodes.clone(),primitives:primitives.clone()},registered:source_graph_resources::RegisteredScene{owned_image_ids:BTreeSet::new(),sampling:Default::default(),bindings:Default::default(),original_passes:BTreeMap::new()},original_state:Some(plan),object_basis:Some(basis)};
 let policy=source_graph_runtime::RuntimePolicy{source_world_from_target_world:basis.source_from_target,source_absolute_world_from_target_world:basis.source_from_target,global_mip_bias:0.,source_float_min:f32::MIN_POSITIVE};source_graph_runtime::retain(app.world_mut(),host,policy).unwrap();
 let view=app.world_mut().spawn((Camera3d::default(),Camera::default(),Projection::Perspective(PerspectiveProjection::default()),Transform::IDENTITY,GlobalTransform::IDENTITY)).id();
 let name=glb.document()["nodes"][0]["name"].as_str().unwrap();let metric:serde_json::Value=serde_json::from_slice(&std::fs::read(std::env::args().nth(2).unwrap()).unwrap()).unwrap();let mut cases=0;
 for s in metric["samples"].as_array().unwrap().iter().filter(|s|s["prefab_path"].as_str().unwrap().ends_with(&format!("/{name}.prefab"))) {
  let position=basis.target_from_source.transform_point3(vec3(&s["camera_position"]));app.world_mut().entity_mut(view).insert((Transform::from_translation(position),Projection::Perspective(PerspectiveProjection{fov:(s["field_of_view"].as_f64().unwrap() as f32).to_radians(),..Default::default()})));app.update();
  assert_eq!(app.world().get::<GlobalTransform>(view).unwrap().translation(),position);assert_eq!(app.world().get::<source_graph_runtime::SourceFrameStatus>(root),Some(&source_graph_runtime::SourceFrameStatus::Updated));assert!(app.world().get::<source_graph_state::OriginalLodDriverPending>(root).is_none());
  let native=s["native_fields"].as_array().unwrap().iter().find(|f|f["name"]=="activeLODLevel").unwrap()["integer_value"].as_i64().unwrap();let group=expected_groups.values().next().unwrap();
  for p in &primitives {let parent=app.world().get::<ChildOf>(*p).unwrap().parent();let node=nodes.iter().find(|(_,e)|**e==parent).unwrap().0;let r=&expected_renderers[node];let expected=group["levels"].as_array().unwrap().iter().enumerate().any(|(i,l)|i as i64==native&&l["renderer_components"].as_array().unwrap().iter().any(|m|m["guid"]==r.guid&&m["file_id"]==r.file_id))&&r.enabled&&!r.force_rendering_off;assert_eq!(*app.world().get::<Visibility>(*p).unwrap(),if expected{Visibility::Inherited}else{Visibility::Hidden},"{name}: {s}");}
  let before:Vec<_>=primitives.iter().map(|p|*app.world().get::<Visibility>(*p).unwrap()).collect();
  app.world_mut().get_mut::<Camera>(view).unwrap().is_active=false;app.update();assert!(matches!(app.world().get::<source_graph_runtime::SourceFrameStatus>(root),Some(source_graph_runtime::SourceFrameStatus::Refused(_))));assert_eq!(before,primitives.iter().map(|p|*app.world().get::<Visibility>(*p).unwrap()).collect::<Vec<_>>());app.world_mut().get_mut::<Camera>(view).unwrap().is_active=true;
  let extra=app.world_mut().spawn((Camera3d::default(),Camera::default(),Projection::Perspective(PerspectiveProjection::default()),Transform::IDENTITY,GlobalTransform::IDENTITY)).id();app.update();assert!(matches!(app.world().get::<source_graph_runtime::SourceFrameStatus>(root),Some(source_graph_runtime::SourceFrameStatus::Refused(_))));
  app.world_mut().entity_mut(extra).insert(forge_engine::tidewater_sky::preview::PreviewCamera);app.update();assert_eq!(app.world().get::<source_graph_runtime::SourceFrameStatus>(root),Some(&source_graph_runtime::SourceFrameStatus::Updated));app.world_mut().despawn(extra);
  assert_eq!(app.world().get::<Visibility>(root),Some(&Visibility::Hidden));cases+=1;
 }
 assert!(cases>0);app.world_mut().despawn(root);app.update();
 println!("{}",serde_json::json!({"status":"actual_scheduled_viewport_lod_matches_native_camera_samples","cases":cases,"renderers":draws.len(),"primitives":primitives.len(),"inactive_camera_refused_without_lod_mutation":true,"ambiguous_camera_refused":true,"offscreen_camera_excluded":true,"automatic_recovery_verified":true,"bevy_transform_propagation_used":true,"actual_product_host_runtime_modules_used":true,"despawn_update_no_panic":true,"root_reveal_qualified":false,"material_binding_count":0,"gpu_rendered":false,"studio_gui_verified":false,"full_import":false}));
}
'''
(O/'src/main.rs').write_text(source,'utf-8')
assembly_path=W/'gladiator-runtime-state-glb-v452/RESULT.v1.json';metric=W/'gladiator-native-lod-metric-v457/NATIVE-RESULT.v1.json';assembly=read(assembly_path)
files=[Path(__file__),O/'src/main.rs',O/'Cargo.toml',assembly_path,metric,R/'Cargo.toml',R/'Cargo.lock',*paths]
for crate in ['forge_engine','forge_assets','forge_core']:
 files.extend(p for p in (R/'crates'/crate).rglob('*') if p.is_file() and (p.suffix=='.rs' or p.name=='Cargo.toml'))
pins={str(p):sha(p) for p in files}
for row in assembly['rows']:assert sha(row['glb'])==row['sha256'];pins[row['glb']]=row['sha256']
state=dict(status='prepared',rows=[],full_import=False,gpu_rendered=False,studio_gui_verified=False);save()
env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(T),CARGO_INCREMENTAL='0',CARGO_BUILD_JOBS='1',TEMP=str(O/'temp'),TMP=str(O/'temp'))
try:
 before=tomllib.loads((O/'Cargo.lock').read_text('utf-8'))
 with (O/'lock.log').open('xb') as log:
  process=subprocess.run([str(BIN/'cargo.exe'),'update','--offline','--workspace','--manifest-path',str(O/'Cargo.toml')],env=env,cwd=O,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
 assert process.returncode==0,'Offline lock reconciliation refused'
 after=tomllib.loads((O/'Cargo.lock').read_text('utf-8'));identities=lambda data:{(p['name'],p['version'],p.get('source')) for p in data['package']}
 assert identities(after).issubset(identities(before)),'Dependency version drift';pins[str(O/'Cargo.lock')]=sha(O/'Cargo.lock')
 (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,dependency_versions_unchanged=True),indent=2),'utf-8')
 with (O/'cargo.log').open('xb') as log:
  proc=subprocess.Popen([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml'),'--bin',name],env=env,cwd=O,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='building_actual_viewport_cycle',cargo_pid=proc.pid);save();state['cargo_exit_code']=proc.wait()
 assert state['cargo_exit_code']==0,'See cargo.log'
 exe=O/'viewport-cycle-checker.exe';shutil.copyfile(T/'debug'/(name+'.exe'),exe);state['checker_sha256']=sha(exe)
 for row in assembly['rows']:
  case=O/row['prefab_guid'];case.mkdir()
  with (case/'stdout.json').open('xb') as out,(case/'stderr.log').open('xb') as err:
   proc=subprocess.Popen([str(exe),row['glb'],str(metric)],env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='executing_actual_viewport_cycle',checker_pid=proc.pid);save();code=proc.wait()
  assert code==0,(case/'stderr.log').read_text('utf-8',errors='replace')[-2400:]
  checked=read(case/'stdout.json');checked.update(prefab_guid=row['prefab_guid'],actual_exit_code=code,stdout_sha256=sha(case/'stdout.json'));state['rows'].append(checked);save()
 assert len(state['rows'])==5 and sum(r['cases'] for r in state['rows'])==207
 for p,h in pins.items():assert sha(p)==h,p
 state.update(status='207_actual_viewport_lod_cycles_and_camera_refusal_recovery_checks_passed',input_pins=pins)
except Exception as e:state.update(status='viewport_lod_cycle_refusal_retained',error=repr(e),traceback=traceback.format_exc())
state['finished_utc']=datetime.now(timezone.utc).isoformat();save();print(json.dumps(dict(status=state['status'],accepted=len(state['rows']),error=state.get('error'))))

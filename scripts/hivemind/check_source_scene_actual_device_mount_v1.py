"""Mount the actual FirePit carrier on a real Bevy device; no draw qualification."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'studio-source-actual-device-mount-v1';OLD=W/'studio-source-preview-entry-native-v1'
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');A=R/'apps/forge_studio/src/authoring_assets';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
MODULES=['route','intake','scene','resources','frame','state','context','host','bootstrap','runtime']
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,v):
 p=ROOT/name;t=p.with_suffix(p.suffix+'.tmp');t.write_text(json.dumps(v,indent=2,allow_nan=False),'utf-8');t.replace(p)
def prepare():
 assert not ROOT.exists();ROOT.mkdir();(ROOT/'src').mkdir();name='openrpg-source-actual-device-mount'
 (ROOT/'Cargo.toml').write_text((OLD/'Cargo.toml').read_text().replace('openrpg-studio-source-clock-check',name),'utf-8');(ROOT/'Cargo.lock').write_text((OLD/'Cargo.lock').read_text().replace('name = "openrpg-studio-source-clock-check"','name = "'+name+'"'),'utf-8')
 fixture=Path((OLD/'fixture-path.txt').read_text().strip());assert fixture.is_file() and sha(fixture)==read(OLD/'PREPARED.v1.json')['input_pins'][str(fixture)]
 imports='\n'.join(f'#[allow(dead_code)] #[path="{(A/("source_graph_"+m+".rs")).as_posix()}"] mod source_graph_{m};' for m in MODULES)
 rust=imports+r'''
use bevy::{app::PluginsState,prelude::*,render::{RenderApp,RenderPlugin,pipelined_rendering::PipelinedRenderingPlugin,renderer::{RenderDevice,RenderAdapterInfo},settings::{RenderCreation,WgpuSettings,WgpuFeatures,Backends}},window::ExitCondition,winit::WinitPlugin};
use forge_engine::source_graph::*;
use std::{collections::BTreeMap,time::{Duration,Instant}};
fn main()->Result<(),Box<dyn std::error::Error>>{
 let args:Vec<_>=std::env::args().collect();let bytes=std::fs::read(&args[1])?;
 let glb=source_graph_route::read(&bytes)?.ok_or("Source carrier required")?;
 let scene=forge_engine::source_graph_scene_geometry::prepare_source_graph_scene_geometry(&glb,None)?;
 let expected_nodes=scene.hierarchy.nodes.len();let expected_primitives=scene.instances.len();let expected_materials=scene.instances.iter().filter_map(|p|match p.route{forge_engine::source_graph_scene_geometry::ScenePrimitiveRoute::SourceGraph{material}=>Some((p.source_node,material)),_=>None}).collect::<std::collections::BTreeSet<_>>();
 let mut app=App::new();app.add_plugins(DefaultPlugins.set(WindowPlugin{primary_window:None,exit_condition:ExitCondition::DontExit,..default()}).set(RenderPlugin{render_creation:RenderCreation::Automatic(WgpuSettings{backends:Some(Backends::DX12),features:WgpuFeatures::TEXTURE_COMPRESSION_BC,force_fallback_adapter:false,..default()}),..default()}).disable::<WinitPlugin>().disable::<PipelinedRenderingPlugin>());app.add_plugins(SourceGraphPlugin);source_graph_runtime::register(&mut app);
 let deadline=Instant::now()+Duration::from_secs(30);while app.plugins_state()!=PluginsState::Ready{if Instant::now()>=deadline{return Err("Renderer initialization exceeded 30 seconds".into());}std::thread::sleep(Duration::from_millis(10));}app.finish();app.cleanup();
 let render=app.get_sub_app(RenderApp).ok_or("RenderApp absent")?;let device=render.world().resource::<RenderDevice>().clone();let info=render.world().resource::<RenderAdapterInfo>();let adapter=serde_json::json!({"name":info.name,"backend":format!("{:?}",info.backend),"device_type":format!("{:?}",info.device_type)});assert!(device.features().contains(WgpuFeatures::TEXTURE_COMPRESSION_BC));assert!(app.world().resource::<RenderDevice>()==&device);
 let offset=Vec3::new(0.,1024.,0.);let reflection=Mat4::from_scale(Vec3::new(-1.,1.,1.));let mapping=source_graph_context::world_mapping(reflection,offset)?;
 let policy=source_graph_runtime::RuntimePolicy{source_world_from_target_world:mapping,source_absolute_world_from_target_world:mapping,global_mip_bias:0.,source_float_min:f32::MIN_POSITIVE};let frame=policy.frame(source_graph_runtime::SourceClock{current_seconds:0.,previous_seconds:0.})?;
 let before=(app.world().resource::<Assets<Mesh>>().len(),app.world().resource::<Assets<Image>>().len(),app.world().resource::<Assets<Shader>>().len(),app.world().resource::<Assets<SourceGraphMaterial>>().len());
 let pose=Transform::from_translation(offset);let bad=app.world_mut().spawn((Visibility::Hidden,pose,GlobalTransform::from(pose))).id();assert!(source_graph_host::mount_initial(app.world_mut(),bad,&bytes,None,Mat4::ZERO,&frame,BTreeMap::new()).is_err());app.world_mut().despawn(bad);
 assert_eq!(before,(app.world().resource::<Assets<Mesh>>().len(),app.world().resource::<Assets<Image>>().len(),app.world().resource::<Assets<Shader>>().len(),app.world().resource::<Assets<SourceGraphMaterial>>().len()));
 let root=app.world_mut().spawn((Visibility::Hidden,pose,GlobalTransform::from(pose))).id();let host=source_graph_host::mount_initial(app.world_mut(),root,&bytes,None,pose.to_matrix(),&frame,BTreeMap::new())?.ok_or("Actual source mount absent")?;
 assert_eq!(host.attached.nodes.len(),expected_nodes);assert_eq!(host.attached.primitives.len(),expected_primitives);assert_eq!(host.registered.bindings.source_materials.keys().copied().collect::<std::collections::BTreeSet<_>>(),expected_materials);assert!(host.registered.bindings.ordinary.is_empty());assert_eq!(app.world().get::<Visibility>(root),Some(&Visibility::Hidden));assert!(app.world().get::<source_graph_scene::SourceSceneAttached>(root).is_some());
 let nodes=host.attached.nodes.clone();let draws=host.attached.primitives.clone();let materials=host.registered.bindings.source_materials.clone();let original=host.original_state.as_ref().ok_or("Original state absent")?;assert!(app.world().get::<source_graph_state::OriginalLodDriverPending>(root).is_some());
 for (index,group) in &original.lod_groups{assert_eq!(&app.world().get::<source_graph_state::OriginalLodGroup>(nodes[index]).unwrap().0,group);}
 for draw in &draws{let parent=app.world().get::<ChildOf>(*draw).unwrap().parent();let index=nodes.iter().find(|(_,e)|**e==parent).unwrap().0;let actual=app.world().get::<source_graph_state::OriginalRendererState>(*draw).unwrap();let expected=&original.renderers[index];assert_eq!(actual.guid,expected.guid);assert_eq!(actual.file_id,expected.file_id);assert_eq!(actual.enabled,expected.enabled);assert_eq!(actual.force_rendering_off,expected.force_rendering_off);assert_eq!(actual.shadow_casting_mode,expected.shadow_casting_mode);assert_eq!(actual.receive_shadows,expected.receive_shadows);}
 let passes:Vec<_>=host.registered.original_passes.iter().map(|(i,p)|serde_json::json!({"material":i,"render_queue":p.render_queue,"disabled_passes":p.disabled})).collect();
 let after=(app.world().resource::<Assets<Mesh>>().len(),app.world().resource::<Assets<Image>>().len(),app.world().resource::<Assets<Shader>>().len(),app.world().resource::<Assets<SourceGraphMaterial>>().len());assert!(after.0>before.0&&after.1>before.1&&after.2>before.2&&after.3>before.3);
 source_graph_runtime::retain(app.world_mut(),host,policy)?;app.update();let clock=source_graph_runtime::SourceClock{current_seconds:6.,previous_seconds:5.};app.world_mut().insert_resource(clock);source_graph_runtime::update(app.world_mut());assert_eq!(app.world().get::<source_graph_runtime::SourceFrameStatus>(root),Some(&source_graph_runtime::SourceFrameStatus::Updated));
 for ((node,_),h) in &materials{let u=&app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.source;assert_eq!(u.source_time_parameters.x,6.);assert_eq!(u.source_previous_time_parameters.x,5.);let matrix=app.world().get::<GlobalTransform>(nodes[node]).unwrap().to_matrix();let expected=policy.frame(clock)?.uniform(host_basis(&glb)?,matrix,u.normal_decode_modes)?;assert_eq!(u.source_scale_mip_min,expected.source_scale_mip_min);}
 app.world_mut().insert_resource(source_graph_runtime::SourceClock{current_seconds:f32::NAN,previous_seconds:6.});source_graph_runtime::update(app.world_mut());assert!(matches!(app.world().get::<source_graph_runtime::SourceFrameStatus>(root),Some(source_graph_runtime::SourceFrameStatus::Refused(_))));for h in materials.values(){assert_eq!(app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.source.source_time_parameters.x,6.);}assert_eq!(app.world().get::<Visibility>(root),Some(&Visibility::Hidden));
 app.world_mut().despawn(root);source_graph_runtime::update(app.world_mut());assert!(nodes.values().chain(draws.iter()).all(|e|app.world().get_entity(*e).is_err()));for h in materials.values(){assert!(app.world().resource::<Assets<SourceGraphMaterial>>().get(h).is_some());}
 let report=serde_json::json!({"status":"actual_firepit_source_scene_mounted_on_bevy_device_and_lifecycle_verified","adapter":adapter,"same_bevy_device":true,"bc_feature":true,"nodes":expected_nodes,"primitives":expected_primitives,"material_instances":materials.len(),"new_meshes":after.0-before.0,"new_images":after.1-before.1,"new_shader_assets":after.2-before.2,"original_passes":passes,"negative_singular_root_no_allocations":true,"finite_clock_update":true,"invalid_clock_no_stale_advance":true,"authored_renderer_flags_verified":true,"lod_data_preserved_driver_pending":true,"despawn_releases_resident_and_preserves_borrowed_materials":true,"root_revealed":false,"draw_executed":false,"gpu_shader_compile_qualified":false,"editor_visual_verified":false,"full_import":false});
 use std::io::Write;let mut file=std::fs::OpenOptions::new().create_new(true).write(true).open(&args[2])?;file.write_all(&serde_json::to_vec_pretty(&report)?)?;println!("{}",report);Ok(())
}
fn host_basis(glb:&forge_assets::source_graph_glb::SourceGraphGlb<'_>)->Result<source_graph_context::ObjectBasis,String>{source_graph_context::ObjectBasis::from_document(glb.document())?.ok_or("Declared source basis absent".into())}
'''
 # RenderDevice does not implement PartialEq; the Bevy resource is the clone
 # used by registration, and both identify the same wrapped wgpu device.
 rust=rust.replace('assert!(app.world().resource::<RenderDevice>()==&device);','assert_eq!(app.world().resource::<RenderDevice>().wgpu_device(),device.wgpu_device());')
 (ROOT/'src/main.rs').write_text(rust,'utf-8')
 pins={str(p):sha(p) for p in [Path(__file__).resolve(),fixture,*[A/('source_graph_'+m+'.rs') for m in MODULES],*list(ROOT.rglob('*'))] if p.is_file()}
 for p in (R/'crates').rglob('*'):
  if p.is_file() and p.suffix in ['.rs','.wgsl','.toml']:pins[str(p)]=sha(p)
 save('PREPARED.v1.json',dict(input_pins=pins,fixture=str(fixture),actual_device_required=True,root_reveal=False,draw_executed=False,full_import=False))
 with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=proc.pid,root=str(ROOT))))
def run():
 state=dict(status='actual_source_device_mount_check_running',full_import=False,gpu_shader_compile_qualified=False,editor_visual_verified=False)
 try:
  prep=read(ROOT/'PREPARED.v1.json')
  for p,d in prep['input_pins'].items():assert sha(p)==d,p
  env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   proc=subprocess.Popen([str(BIN/'cargo.exe'),'run','--locked','--offline','--',prep['fixture'],str(ROOT/'NATIVE-RESULT.v1.json')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['cargo_pid']=proc.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=proc.wait();assert proc.returncode==0
  native=read(ROOT/'NATIVE-RESULT.v1.json');assert native['same_bevy_device'] and native['bc_feature'] and not native['root_revealed'];
  for p,d in prep['input_pins'].items():assert sha(p)==d,p
  state.update(status='actual_firepit_bevy_device_mount_and_lifecycle_passed',nodes=native['nodes'],primitives=native['primitives'],material_instances=native['material_instances'],input_pins=prep['input_pins'],native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'))
 except Exception as exc:state.update(status='actual_source_device_mount_refusal_retained',error=repr(exc))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()

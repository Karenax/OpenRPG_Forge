"""Test dynamic source contexts using actual Studio modules and engine materials."""
from pathlib import Path
from datetime import datetime,timezone
import ctypes,ctypes.wintypes as wt,hashlib,json,os,re,shutil,subprocess,sys
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork');ROOT=W/'studio-source-context-native-v1'
M=R/'apps/forge_studio/src/authoring_assets';PRIOR=W/'studio-current-source-wasm-check-v10';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
MODULES=['source_graph_route','source_graph_intake','source_graph_scene','source_graph_resources','source_graph_frame','source_graph_state','source_graph_context','source_graph_host']
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v['updated_utc']=datetime.now(timezone.utc).isoformat();p=ROOT/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(ROOT/'RUN-RESULT.v1.json')
TESTS=r'''
use bevy::prelude::*;
use std::collections::BTreeMap;
use source_graph_context::*;
use forge_engine::source_graph::*;
fn basis()->ObjectBasis{let f=Mat4::from_scale(Vec3::new(-1.,1.,1.));ObjectBasis{source_from_target:f,target_from_source:f}}
fn frame()->HostFrame{let f=basis().source_from_target;HostFrame{source_world_from_target_world:f,source_absolute_world_from_target_world:f,time_parameters:time_parameters(0.).unwrap(),previous_time_parameters:time_parameters(0.).unwrap(),global_mip_bias:-0.75,source_float_min:f32::MIN_POSITIVE}}
#[test] fn ctx_preserved_scene_bases_are_exact(){for raw in [include_str!("../scene-0.json"),include_str!("../scene-1.json")]{let d=serde_json::from_str(raw).unwrap();let b=ObjectBasis::from_document(&d).unwrap().unwrap();assert_eq!(b.source_from_target,basis().source_from_target);assert_eq!(b.target_from_source,basis().target_from_source);}}
#[test] fn ctx_presentation_translation_does_not_change_authored_coordinates(){let f=basis().source_from_target;let offset=Vec3::new(0.,1024.,0.);let mapping=world_mapping(f,offset).unwrap();let point=Vec3::new(12.,7.,-3.);assert_eq!(mapping.transform_point3(point+offset),f.transform_point3(point));}
#[test] fn ctx_rotated_nonuniform_scale_and_decoder_ownership_are_retained(){let p=frame();let pose=Mat4::from_scale_rotation_translation(Vec3::new(2.,3.,4.),Quat::from_rotation_z(0.71),Vec3::new(13.,9.,-5.));let modes=[Vec4::new(2.,1.,0.,0.),Vec4::new(3.,0.,1.,0.)];let u=p.uniform(basis(),pose,modes).unwrap();assert!((u.source_scale_mip_min.x-0.5).abs()<1e-6);assert!((u.source_scale_mip_min.y-0.25).abs()<1e-6);assert_eq!(u.source_scale_mip_min.z,-0.75);assert_eq!(u.normal_decode_modes,modes);}
#[test] fn ctx_partial_noninverse_and_singular_bases_are_refused(){assert!(ObjectBasis::from_document(&serde_json::json!({"extras":{"source_frame_binding":{}}})).is_err());let mut d:serde_json::Value=serde_json::from_str(include_str!("../scene-0.json")).unwrap();d["extras"]["source_frame_binding"]["target_object_from_source_object"][0]=2.0.into();assert!(ObjectBasis::from_document(&d).is_err());assert!(frame().uniform(basis(),Mat4::ZERO,[Vec4::ZERO;2]).is_err());}
#[test] fn ctx_source_clock_has_separate_current_previous_and_rejects_nan(){let now=time_parameters(std::f32::consts::FRAC_PI_2).unwrap();let before=time_parameters(-std::f32::consts::FRAC_PI_2).unwrap();assert!((now.y-1.).abs()<1e-6&&(before.y+1.).abs()<1e-6);assert!(now.z.abs()<1e-6&&before.z.abs()<1e-6);assert_eq!(now.w,0.);assert!(time_parameters(f32::NAN).is_err());let mut p=frame();p.time_parameters=now;p.previous_time_parameters=before;let u=p.uniform(basis(),Mat4::IDENTITY,[Vec4::ZERO;2]).unwrap();assert_eq!(u.source_time_parameters,now);assert_eq!(u.source_previous_time_parameters,before);p.global_mip_bias=f32::INFINITY;assert!(p.uniform(basis(),Mat4::IDENTITY,[Vec4::ZERO;2]).is_err());}
fn fixture()->(World,source_graph_host::SourceSceneHost,Handle<SourceGraphMaterial>,Entity){
 let mut w=World::new();w.init_resource::<Assets<SourceGraphMaterial>>();let root=w.spawn((Visibility::Hidden,source_graph_scene::SourceSceneAttached)).id();
 let key=SourceGraphKey{color_write_mask:15,prepass_cull:2,shadow_only:false,vertex:123,fragment:123,prepass_vertex:456,prepass_fragment:456,original_alpha_to_mask:false,requires_original_vertex_color:false,original_uv_requirements:Default::default(),forward_state:SourceGraphForwardState{color_src:1,color_dst:0,alpha_src:1,alpha_dst:0,cull:2,compare:4,write_depth:true}};
 let modes=[Vec4::new(2.,1.,0.,0.),Vec4::ZERO];let initial=frame().uniform(basis(),Mat4::IDENTITY,modes).unwrap();let mat=source_graph_material(SourceGraphExtension::from_source_bindings(key,initial,0,vec![]).unwrap(),SourceGraphSurfacePhase::Opaque);let handle=w.resource_mut::<Assets<SourceGraphMaterial>>().add(mat);
 let pose=Transform::from_scale(Vec3::new(2.,3.,4.));let node=w.spawn(GlobalTransform::from(pose)).id();let draw=w.spawn((MeshMaterial3d(handle.clone()),GlobalTransform::from(pose),ChildOf(node))).id();
 let host=source_graph_host::SourceSceneHost{root,attached:source_graph_scene::AttachedScene{nodes:BTreeMap::from([(3,node)]),primitives:vec![draw]},registered:source_graph_resources::RegisteredScene{bindings:source_graph_scene::SceneBindings{source_materials:BTreeMap::from([((3,2),handle.clone())]),..Default::default()},original_passes:BTreeMap::new()},original_state:None,object_basis:Some(basis())};(w,host,handle,node)
}
#[test] fn ctx_real_host_reads_current_entities_and_preserves_admitted_decoders(){let (mut w,host,h,node)=fixture();let mut p=frame();p.time_parameters=time_parameters(2.).unwrap();p.previous_time_parameters=time_parameters(1.).unwrap();assert_eq!(host.update_runtime_frame(&mut w,&p).unwrap(),1);let u=&w.resource::<Assets<SourceGraphMaterial>>().get(&h).unwrap().extension.source;assert_eq!(u.source_time_parameters,p.time_parameters);assert_eq!(u.normal_decode_modes[0],Vec4::new(2.,1.,0.,0.));assert_eq!(u.source_scale_mip_min.x,0.5);w.entity_mut(node).insert(GlobalTransform::from(Transform::from_scale(Vec3::new(4.,3.,8.))));host.update_runtime_frame(&mut w,&p).unwrap();let u=&w.resource::<Assets<SourceGraphMaterial>>().get(&h).unwrap().extension.source;assert_eq!(u.source_scale_mip_min.x,0.25);assert_eq!(u.source_scale_mip_min.y,0.125);assert_eq!(*w.get::<Visibility>(host.root).unwrap(),Visibility::Hidden);}
#[test] fn ctx_later_bad_node_does_not_advance_any_material(){let (mut w,mut host,h,_)=fixture();let mat=w.resource::<Assets<SourceGraphMaterial>>().get(&h).unwrap().clone();let second=w.resource_mut::<Assets<SourceGraphMaterial>>().add(mat);let bad=w.spawn_empty().id();host.attached.nodes.insert(7,bad);host.registered.bindings.source_materials.insert((7,2),second);w.entity_mut(host.root).insert(Visibility::Inherited);let mut p=frame();p.time_parameters=time_parameters(7.).unwrap();assert!(host.update_runtime_frame(&mut w,&p).is_err());assert_eq!(w.resource::<Assets<SourceGraphMaterial>>().get(&h).unwrap().extension.source.source_time_parameters.x,0.);assert_eq!(*w.get::<Visibility>(host.root).unwrap(),Visibility::Hidden);}
'''
def prepare():
    assert not ROOT.exists();ROOT.mkdir();(ROOT/'src').mkdir()
    cargo=ROOT/'Cargo.toml';cargo.write_text('[package]\nname="openrpg-studio-source-context-check"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\nforge_engine={path="'+(R/'crates/forge_engine').as_posix()+'"}\nforge_assets={path="'+(R/'crates/forge_assets').as_posix()+'"}\nbevy={version="=0.19.1",default-features=false,features=["3d","ui"]}\nbevy_ecs="=0.19.1"\nserde_json={version="=1.0.151",features=["float_roundtrip"]}\n','utf-8')
    shutil.copyfile(W/'studio-source-state-native-v1/Cargo.lock',ROOT/'Cargo.lock')
    wrapper=ROOT/'src/lib.rs';wrapper.write_text('\n'.join('#[allow(dead_code)]\n#[path="'+(M/(n+'.rs')).as_posix()+'"]\nmod '+n+';' for n in MODULES)+'\n'+TESTS,'utf-8')
    files=[Path(__file__).resolve(),cargo,wrapper,R/'Cargo.toml',R/'Cargo.lock',*[M/(n+'.rs') for n in MODULES]]
    for i in [0,1]:
        p=ROOT/f'scene-{i}.json';shutil.copyfile(W/f'studio-source-state-native-v1/scene-{i}.json',p);files.append(p)
    for p in (R/'crates').rglob('*'):
        if p.is_file() and p.suffix in ['.rs','.toml','.wgsl'] and ('src' in p.relative_to(R/'crates').parts or p.name=='Cargo.toml'):files.append(p)
    prep=dict(input_pins={str(p):sha(p) for p in files},expected_new_context_tests=7,wait_pid=read(PRIOR/'JOB.v1.json')['pid'])
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');print(json.dumps(dict(pid=proc.pid,expected_new_context_tests=7)))
def run():
    state=dict(status='waiting_for_current_frontend_check',actual_context_tests_passed=0,gpu_rendered=False,studio_verified=False,full_import=False);save(state)
    try:
        prep=read(ROOT/'PREPARED.v1.json');prior=read(PRIOR/'RUN-RESULT.v1.json')
        if 'actual_exit_code' not in prior:
            k=ctypes.WinDLL('kernel32',use_last_error=True);k.OpenProcess.argtypes=[wt.DWORD,wt.BOOL,wt.DWORD];k.OpenProcess.restype=wt.HANDLE;k.WaitForSingleObject.argtypes=[wt.HANDLE,wt.DWORD];k.WaitForSingleObject.restype=wt.DWORD;k.CloseHandle.argtypes=[wt.HANDLE];h=k.OpenProcess(0x100000,False,prep['wait_pid'])
            if h:
                try:assert k.WaitForSingleObject(h,3*60*60*1000)==0
                finally:k.CloseHandle(h)
        assert read(PRIOR/'RUN-RESULT.v1.json')['actual_exit_code']==0,'Frontend compile failed'
        for p,digest in prep['input_pins'].items():assert sha(p)==digest,p
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        for stage,args in [('lock',['generate-lockfile','--offline']),('native',['test','--locked','--offline','--lib','--','--nocapture'])]:
            state['status']='checking_actual_dynamic_context_'+stage;save(state)
            with (ROOT/(stage+'.stdout.log')).open('xb') as out,(ROOT/(stage+'.stderr.log')).open('xb') as err:
                proc=subprocess.Popen([str(BIN/'cargo.exe'),*args],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['cargo_pid']=proc.pid;save(state);state['actual_exit_code']=proc.wait();assert proc.returncode==0,stage
        text=(ROOT/'native.stdout.log').read_text('utf-8',errors='replace');names=re.findall(r'^test (ctx_\S+) \.\.\. ok$',text,re.M);assert len(names)==7 and '0 failed' in text
        for p,digest in prep['input_pins'].items():assert sha(p)==digest,p
        state.update(status='seven_actual_dynamic_context_tests_and_existing_host_tests_passed',actual_context_tests_passed=7,context_test_names=names,input_pins=prep['input_pins'],stdout_sha256=sha(ROOT/'native.stdout.log'),stderr_sha256=sha(ROOT/'native.stderr.log'))
    except Exception as exc:state.update(status='dynamic_source_context_test_refusal_retained',error=repr(exc))
    finally:save(state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()

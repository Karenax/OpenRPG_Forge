"""Test staged common host lifetime after the live bootstrap controller."""
from pathlib import Path
from datetime import datetime,timezone
import importlib.util,json,os,re,shutil,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');P=W/'studio-source-bootstrap-native-v1';ROOT=W/'studio-source-runtime-native-v1'
HELPER=Path(__file__).with_name('queue_source_bootstrap_native_v1.py')
spec=importlib.util.spec_from_file_location('bootstrap_queue_helpers',HELPER);helpers=importlib.util.module_from_spec(spec);spec.loader.exec_module(helpers)
sha=helpers.sha;read=helpers.read;process=helpers.process;BIN=helpers.BIN
def save(v):
    v['updated_utc']=datetime.now(timezone.utc).isoformat();p=ROOT/'RUN.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(ROOT/'RUN-RESULT.v1.json')
TESTS=r'''
#[cfg(test)] mod runtime_tests {
 use super::*;use crate::{fixture,frame};use forge_engine::source_graph::SourceGraphMaterial;
 fn policy()->RuntimePolicy{let f=frame();RuntimePolicy{source_world_from_target_world:f.source_world_from_target_world,source_absolute_world_from_target_world:f.source_absolute_world_from_target_world,global_mip_bias:f.global_mip_bias,source_float_min:f.source_float_min}}
 #[test] fn runtime_clock_updates_real_material_and_preserves_decoder(){
  let (mut w,host,h,_)=fixture();let root=host.root;retain(&mut w,host,policy()).unwrap();w.insert_resource(SourceClock{current_seconds:3.,previous_seconds:2.});update(&mut w);
  let u=&w.resource::<Assets<SourceGraphMaterial>>().get(&h).unwrap().extension.source;assert_eq!(u.source_time_parameters.x,3.);assert_eq!(u.source_previous_time_parameters.x,2.);assert_eq!(u.normal_decode_modes[0],Vec4::new(2.,1.,0.,0.));assert_eq!(w.get::<SourceFrameStatus>(root),Some(&SourceFrameStatus::Updated));assert_eq!(w.get::<Visibility>(root),Some(&Visibility::Hidden));
 }
 #[test] fn runtime_missing_clock_hides_root_without_material_advance(){
  let (mut w,host,h,_)=fixture();let root=host.root;retain(&mut w,host,policy()).unwrap();w.entity_mut(root).insert(Visibility::Inherited);update(&mut w);
  assert!(matches!(w.get::<SourceFrameStatus>(root),Some(SourceFrameStatus::Refused(_))));assert_eq!(w.get::<Visibility>(root),Some(&Visibility::Hidden));assert_eq!(w.resource::<Assets<SourceGraphMaterial>>().get(&h).unwrap().extension.source.source_time_parameters.x,0.);
 }
 #[test] fn runtime_invalid_clock_preserves_last_admitted_material(){
  let (mut w,host,h,_)=fixture();let root=host.root;retain(&mut w,host,policy()).unwrap();w.insert_resource(SourceClock{current_seconds:f32::NAN,previous_seconds:2.});update(&mut w);
  assert!(matches!(w.get::<SourceFrameStatus>(root),Some(SourceFrameStatus::Refused(_))));assert_eq!(w.resource::<Assets<SourceGraphMaterial>>().get(&h).unwrap().extension.source.source_time_parameters.x,0.);
 }
 #[test] fn runtime_despawn_releases_resident_without_deleting_borrowed_material(){
  let (mut w,host,h,_)=fixture();let root=host.root;retain(&mut w,host,policy()).unwrap();assert_eq!(w.resource::<SourceSceneResidents>().scenes.len(),1);w.despawn(root);update(&mut w);assert!(w.resource::<SourceSceneResidents>().scenes.is_empty());assert!(w.resource::<Assets<SourceGraphMaterial>>().get(&h).is_some());
 }
 #[test] fn runtime_root_mapping_is_explicit_and_retained(){
  let (mut w,host,h,_)=fixture();let mut p=policy();let mapping=p.source_world_from_target_world*Mat4::from_translation(Vec3::new(0.,-1024.,0.));p.source_world_from_target_world=mapping;p.source_absolute_world_from_target_world=mapping;retain(&mut w,host,p).unwrap();w.insert_resource(SourceClock{current_seconds:1.,previous_seconds:0.});update(&mut w);let u=&w.resource::<Assets<SourceGraphMaterial>>().get(&h).unwrap().extension.source;assert_eq!(u.source_world_from_target_world,mapping);assert_eq!(u.source_absolute_world_from_target_world,mapping);
 }
}
'''
def prepare():
    assert not ROOT.exists();ROOT.mkdir();(ROOT/'src').mkdir()
    job=read(P/'JOB.v1.json');k,h,creation=process(job['pid']);assert h,'Bootstrap controller not live';k.CloseHandle(h)
    old=W/'studio-source-scene-runtime-v1/source_graph_runtime.rs';candidate=ROOT/'source_graph_runtime.rs';candidate.write_text(old.read_text('utf-8')+'\n'+TESTS,'utf-8')
    shutil.copyfile(P/'source_graph_bootstrap.rs',ROOT/'source_graph_bootstrap.rs')
    (ROOT/'Cargo.toml').write_text((P/'Cargo.toml').read_text('utf-8').replace('openrpg-studio-source-bootstrap-check','openrpg-studio-source-runtime-check'),'utf-8')
    (ROOT/'src/lib.rs').write_text((P/'src/lib.rs').read_text('utf-8')+'\n#[allow(dead_code)]\n#[path="../source_graph_runtime.rs"] mod source_graph_runtime;\n','utf-8')
    for name in ['scene-0.json','scene-1.json','fixture-path.txt']:shutil.copyfile(P/name,ROOT/name)
    inputs=[Path(__file__).resolve(),HELPER,old,*[p for p in ROOT.rglob('*') if p.is_file()]]
    inputs += [Path(p) for p in read(P/'PREPARED.v1.json')['input_pins'] if p.startswith('E:') or p.endswith('.glb')]
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(dict(wait_pid=job['pid'],wait_creation_filetime=creation,expected_runtime_tests=5,input_pins={str(p):sha(p) for p in inputs}),indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');print(json.dumps(dict(pid=proc.pid,queued_runtime_tests=5)))
def run():
    state=dict(status='waiting_for_live_bootstrap_controller',runtime_tests_passed=0,installed_in_product=False,gpu_rendered=False,studio_verified=False,full_import=False);save(state)
    try:
        prep=read(ROOT/'PREPARED.v1.json');k,h,creation=process(prep['wait_pid'])
        if h:
            try:assert creation==prep['wait_creation_filetime'];assert k.WaitForSingleObject(h,4*60*60*1000)==0,'Prior wait expired'
            finally:k.CloseHandle(h)
        assert read(P/'RUN-RESULT.v1.json')['status']=='three_initial_context_tests_and_prior_actual_tests_passed','Prior bootstrap tests did not qualify'
        for p,d in prep['input_pins'].items():assert sha(Path(p))==d,p
        lock=(P/'Cargo.lock').read_text('utf-8');assert lock.count('name = "openrpg-studio-source-bootstrap-check"')==1
        (ROOT/'Cargo.lock').write_text(lock.replace('name = "openrpg-studio-source-bootstrap-check"','name = "openrpg-studio-source-runtime-check"'),'utf-8');state['lock_sha256']=sha(ROOT/'Cargo.lock')
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        state['status']='testing_actual_shared_host_lifecycle';save(state)
        with (ROOT/'native.stdout.log').open('xb') as out,(ROOT/'native.stderr.log').open('xb') as err:
            proc=subprocess.Popen([str(BIN/'cargo.exe'),'test','--locked','--offline','--lib','--','--nocapture'],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['cargo_pid']=proc.pid;save(state);state['actual_exit_code']=proc.wait();assert proc.returncode==0,'native tests'
        text=(ROOT/'native.stdout.log').read_text('utf-8',errors='replace');names=re.findall(r'^test (source_graph_runtime::runtime_tests::runtime_\S+) \.\.\. ok$',text,re.M);assert len(names)==5 and '0 failed' in text
        for p,d in prep['input_pins'].items():assert sha(Path(p))==d,p
        state.update(status='five_shared_host_lifecycle_tests_and_prior_actual_tests_passed',runtime_tests_passed=5,test_names=names,input_pins=prep['input_pins'],stdout_sha256=sha(ROOT/'native.stdout.log'),stderr_sha256=sha(ROOT/'native.stderr.log'))
    except Exception as exc:state.update(status='shared_host_lifecycle_native_refusal_retained',error=repr(exc))
    finally:save(state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()

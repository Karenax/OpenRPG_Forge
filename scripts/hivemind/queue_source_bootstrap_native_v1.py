"""Queue real Bevy transform propagation tests behind the live context compiler."""
from pathlib import Path
from datetime import datetime,timezone
import ctypes,ctypes.wintypes as wt,hashlib,json,os,re,shutil,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');P=W/'studio-source-context-native-v1';ROOT=W/'studio-source-bootstrap-native-v1'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v['updated_utc']=datetime.now(timezone.utc).isoformat();p=ROOT/'RUN.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(ROOT/'RUN-RESULT.v1.json')
def process(pid):
    k=ctypes.WinDLL('kernel32',use_last_error=True)
    k.OpenProcess.argtypes=[wt.DWORD,wt.BOOL,wt.DWORD];k.OpenProcess.restype=wt.HANDLE
    k.GetProcessTimes.argtypes=[wt.HANDLE,*([ctypes.POINTER(wt.FILETIME)]*4)];k.GetProcessTimes.restype=wt.BOOL
    k.WaitForSingleObject.argtypes=[wt.HANDLE,wt.DWORD];k.WaitForSingleObject.restype=wt.DWORD
    k.CloseHandle.argtypes=[wt.HANDLE]
    h=k.OpenProcess(0x101000,False,pid)
    if not h:return k,None,None
    times=[wt.FILETIME() for _ in range(4)]
    assert k.GetProcessTimes(h,*[ctypes.byref(t) for t in times])
    creation=(times[0].dwHighDateTime<<32)|times[0].dwLowDateTime
    return k,h,creation
TESTS=r'''
#[allow(dead_code)]
#[path="../source_graph_bootstrap.rs"] mod source_graph_bootstrap;
fn bootstrap_glb()->Vec<u8>{std::fs::read(include_str!("../fixture-path.txt").trim()).unwrap()}
#[test] fn bootstrap_actual_firepit_matches_bevy_propagated_hierarchy(){
 let bytes=bootstrap_glb();let glb=source_graph_route::read(&bytes).unwrap().unwrap();
 let scene=forge_engine::source_graph_scene_geometry::prepare_source_graph_scene_geometry(&glb,None).unwrap();
 let root_pose=Transform::from_translation(Vec3::new(12.,1029.,-7.)).with_rotation(Quat::from_rotation_y(0.63)).with_scale(Vec3::new(2.,3.,4.));
 let mut f=frame();let map=world_mapping(basis().source_from_target,Vec3::new(0.,1024.,0.)).unwrap();f.source_world_from_target_world=map;f.source_absolute_world_from_target_world=map;
 let initial=source_graph_bootstrap::initial_contexts(&glb,&scene,root_pose.to_matrix(),&f).unwrap();
 let mut app=App::new();app.add_plugins((MinimalPlugins,bevy::transform::TransformPlugin));
 let root=app.world_mut().spawn((root_pose,GlobalTransform::IDENTITY)).id();
 let entities=forge_engine::source_graph_scene::spawn_source_graph_hierarchy(&scene.hierarchy,app.world_mut(),root).unwrap();app.update();
 let expected:std::collections::BTreeSet<_>=scene.instances.iter().filter_map(|p|match p.route{forge_engine::source_graph_scene_geometry::ScenePrimitiveRoute::SourceGraph{material}=>Some((p.source_node,material)),_=>None}).collect();
 assert_eq!(initial.keys().copied().collect::<std::collections::BTreeSet<_>>(),expected);assert_eq!(initial.len(),4);
 for ((node,_),u) in initial{let actual=app.world().get::<GlobalTransform>(entities[&node]).unwrap().to_matrix();let propagated=f.uniform(basis(),actual,[Vec4::ZERO;2]).unwrap();
  for (a,b) in u.source_scale_mip_min.to_array().into_iter().zip(propagated.source_scale_mip_min.to_array()){assert!((a-b).abs()<=1e-5*a.abs().max(1.),"node {node}: {a} vs {b}");}
  assert_eq!(u.source_world_from_target_world,propagated.source_world_from_target_world);assert_eq!(u.source_time_parameters,propagated.source_time_parameters);
 }
}
#[test] fn bootstrap_singular_root_and_missing_parent_are_refused(){
 let bytes=bootstrap_glb();let glb=source_graph_route::read(&bytes).unwrap().unwrap();let mut scene=forge_engine::source_graph_scene_geometry::prepare_source_graph_scene_geometry(&glb,None).unwrap();
 assert!(source_graph_bootstrap::initial_contexts(&glb,&scene,Mat4::ZERO,&frame()).is_err());
 scene.hierarchy.nodes[0].parent_source_node=Some(999999);
 assert!(source_graph_bootstrap::initial_contexts(&glb,&scene,Mat4::IDENTITY,&frame()).is_err());
}
#[test] fn bootstrap_duplicate_source_identity_is_refused(){
 let bytes=bootstrap_glb();let glb=source_graph_route::read(&bytes).unwrap().unwrap();let mut scene=forge_engine::source_graph_scene_geometry::prepare_source_graph_scene_geometry(&glb,None).unwrap();
 scene.hierarchy.nodes[1].source_node=scene.hierarchy.nodes[0].source_node;
 assert!(source_graph_bootstrap::initial_contexts(&glb,&scene,Mat4::IDENTITY,&frame()).is_err());
}
'''
def prepare():
    assert not ROOT.exists();ROOT.mkdir();(ROOT/'src').mkdir()
    job=read(P/'JOB.v1.json');k,h,creation=process(job['pid']);assert h,'Prior controller must be confirmed live';k.CloseHandle(h)
    old=W/'studio-source-context-bootstrap-v1/source_graph_bootstrap.rs'
    original=old.read_text('utf-8');candidate=ROOT/'source_graph_bootstrap.rs'
    candidate.write_text(original.replace('let mut globals=BTreeMap::new();','let mut globals:BTreeMap<usize,Mat4>=BTreeMap::new();'),'utf-8')
    (ROOT/'Cargo.toml').write_text((P/'Cargo.toml').read_text('utf-8').replace('openrpg-studio-source-context-check','openrpg-studio-source-bootstrap-check'),'utf-8')
    (ROOT/'src/lib.rs').write_text((P/'src/lib.rs').read_text('utf-8')+'\n'+TESTS,'utf-8')
    for i in [0,1]:shutil.copyfile(P/f'scene-{i}.json',ROOT/f'scene-{i}.json')
    row=next(r for r in read(W/'rocks-firepit-renderer-state-glb-v1/RESULT.v1.json')['rows'] if 'firepit-01-' in r['target'])
    fixture=Path(row['target']);assert sha(fixture)==row['target_sha256'];(ROOT/'fixture-path.txt').write_text(str(fixture),'utf-8')
    inputs=[Path(__file__).resolve(),old,candidate,fixture,ROOT/'Cargo.toml',ROOT/'src/lib.rs',ROOT/'fixture-path.txt',ROOT/'scene-0.json',ROOT/'scene-1.json']
    inputs += [Path(p) for p in read(P/'PREPARED.v1.json')['input_pins'] if p.startswith('E:') and Path(p)!=Path(__file__).resolve()]
    prep=dict(wait_pid=job['pid'],wait_creation_filetime=creation,input_pins={str(p):sha(p) for p in inputs},expected_bootstrap_tests=3)
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');print(json.dumps(dict(pid=proc.pid,queued_tests=3)))
def run():
    state=dict(status='waiting_for_live_context_test_controller',bootstrap_tests_passed=0,installed_in_product=False,gpu_rendered=False,studio_verified=False,full_import=False);save(state)
    try:
        prep=read(ROOT/'PREPARED.v1.json');k,h,creation=process(prep['wait_pid'])
        if h:
            try:
                assert creation==prep['wait_creation_filetime'],'Controller PID reused';assert k.WaitForSingleObject(h,3*60*60*1000)==0,'Prior controller wait expired'
            finally:k.CloseHandle(h)
        assert read(P/'RUN-RESULT.v1.json')['status']=='seven_actual_dynamic_context_tests_and_existing_host_tests_passed','Prior tests did not qualify; preserve refusal'
        for p,d in prep['input_pins'].items():assert sha(Path(p))==d,p
        lock=(P/'Cargo.lock').read_text('utf-8');assert lock.count('name = "openrpg-studio-source-context-check"')==1
        (ROOT/'Cargo.lock').write_text(lock.replace('name = "openrpg-studio-source-context-check"','name = "openrpg-studio-source-bootstrap-check"'),'utf-8');state['cargo_lock_sha256']=sha(ROOT/'Cargo.lock')
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        state['status']='testing_initial_contexts_against_actual_bevy_propagation';save(state)
        with (ROOT/'native.stdout.log').open('xb') as out,(ROOT/'native.stderr.log').open('xb') as err:
            proc=subprocess.Popen([str(BIN/'cargo.exe'),'test','--locked','--offline','--lib','--','--nocapture'],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['cargo_pid']=proc.pid;save(state);state['actual_exit_code']=proc.wait();assert proc.returncode==0,'native tests'
        text=(ROOT/'native.stdout.log').read_text('utf-8',errors='replace');names=re.findall(r'^test (bootstrap_\S+) \.\.\. ok$',text,re.M);assert len(names)==3 and '0 failed' in text
        for p,d in prep['input_pins'].items():assert sha(Path(p))==d,p
        state.update(status='three_initial_context_tests_and_prior_actual_tests_passed',bootstrap_tests_passed=3,bootstrap_test_names=names,source_pins=prep['input_pins'],stdout_sha256=sha(ROOT/'native.stdout.log'),stderr_sha256=sha(ROOT/'native.stderr.log'))
    except Exception as exc:state.update(status='initial_context_native_refusal_retained',error=repr(exc))
    finally:save(state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()

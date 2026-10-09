"""Exercise the installed lifecycle/clock with native Bevy and preserved fixtures."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,re,shutil,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'studio-source-clock-native-v1';OLD=W/'studio-source-runtime-native-v1'
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');A=R/'apps/forge_studio/src/authoring_assets'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def save(name,data):
    p=ROOT/name;t=p.with_suffix(p.suffix+'.tmp');t.write_text(json.dumps(data,indent=2),'utf-8');t.replace(p)
def prepare():
    assert not ROOT.exists();assert read(OLD/'RUN-RESULT.v1.json')['runtime_tests_passed']==5
    ROOT.mkdir();(ROOT/'src').mkdir()
    package='openrpg-studio-source-clock-check'
    (ROOT/'Cargo.toml').write_text((OLD/'Cargo.toml').read_text().replace('openrpg-studio-source-runtime-check',package),'utf-8')
    (ROOT/'Cargo.lock').write_text((OLD/'Cargo.lock').read_text().replace('name = "openrpg-studio-source-runtime-check"','name = "'+package+'"'),'utf-8')
    wrapper=(OLD/'src/lib.rs').read_text('utf-8');assert wrapper.count('#[path="../source_graph_runtime.rs"] mod source_graph_runtime;')==1
    # Compile the installed production bytes, adding the previous native test
    # module separately because its fixture lives in this private harness.
    installed=(A/'source_graph_runtime.rs').read_text('utf-8')
    tested=(OLD/'source_graph_runtime.rs').read_text('utf-8');tests='#[cfg(test)] mod runtime_tests {'+tested.split('#[cfg(test)] mod runtime_tests {',1)[1]
    extra='''
 #[test] fn clock_installed_registration_tracks_current_previous_and_pause(){
  let mut app=App::new();app.insert_resource(Time::<()>::default());register(&mut app);
  app.world_mut().resource_mut::<Time>().advance_by(std::time::Duration::from_secs(3));app.update();
  let clock=*app.world().resource::<SourceClock>();assert_eq!(clock.current_seconds,3.);assert_eq!(clock.previous_seconds,0.);
  app.world_mut().resource_mut::<Time>().advance_by(std::time::Duration::ZERO);app.update();
  let clock=*app.world().resource::<SourceClock>();assert_eq!(clock.current_seconds,3.);assert_eq!(clock.previous_seconds,3.);
 }
 #[test] fn clock_schedule_updates_actual_material_after_clock_capture(){
  let (mut w,host,h,_)=fixture();retain(&mut w,host,policy()).unwrap();w.insert_resource(SourceClock{current_seconds:0.,previous_seconds:0.});
  let mut time=Time::<()>::default();time.advance_by(std::time::Duration::from_secs(6));w.insert_resource(time);
  let mut schedule=Schedule::default();schedule.add_systems((capture_viewport_clock,update).chain());schedule.run(&mut w);
  let u=&w.resource::<Assets<SourceGraphMaterial>>().get(&h).unwrap().extension.source;
  assert_eq!(u.source_time_parameters.x,6.);assert_eq!(u.source_previous_time_parameters.x,0.);assert_eq!(u.normal_decode_modes[0],Vec4::new(2.,1.,0.,0.));
 }
'''
    tests=tests.rstrip();assert tests.endswith('}');tests=tests[:-1]+extra+'}\n'
    (ROOT/'source_graph_runtime.rs').write_text(installed+tests,'utf-8');(ROOT/'src/lib.rs').write_text(wrapper,'utf-8')
    for name in ['scene-0.json','scene-1.json','fixture-path.txt','source_graph_bootstrap.rs']:shutil.copyfile(OLD/name,ROOT/name)
    pins={str(p):sha(p) for p in [Path(__file__).resolve(),A/'source_graph_runtime.rs',A/'source_graph_bootstrap.rs',A/'preview.rs',A.parent/'authoring_assets.rs',*list(ROOT.rglob('*'))] if p.is_file()}
    for p in read(OLD/'PREPARED.v1.json')['input_pins']:
        if p.startswith('E:') or p.endswith('.glb'):pins[p]=sha(p)
    save('PREPARED.v1.json',dict(input_pins=pins,installed_production_sha256=sha(A/'source_graph_runtime.rs'),expected_clock_tests=2,source_routes_connected=False,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=proc.pid,expected_clock_tests=2)))
def run():
    state=dict(status='checking_installed_viewport_clock_and_lifecycle',clock_tests_passed=0,full_import=False,gpu_qualified=False,studio_verified=False)
    try:
        pins=read(ROOT/'PREPARED.v1.json')['input_pins']
        for p,d in pins.items():assert sha(p)==d,p
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        with (ROOT/'native.stdout.log').open('xb') as out,(ROOT/'native.stderr.log').open('xb') as err:
            proc=subprocess.Popen([str(BIN/'cargo.exe'),'test','--locked','--offline','--lib','--','--nocapture'],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['cargo_pid']=proc.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=proc.wait();assert proc.returncode==0
        output=(ROOT/'native.stdout.log').read_text('utf-8');names=re.findall(r'^test (source_graph_runtime::runtime_tests::clock_\S+) \.\.\. ok$',output,re.M);assert len(names)==2 and '0 failed' in output
        for p,d in pins.items():assert sha(p)==d,p
        state.update(status='two_installed_clock_tests_and_prior_native_tests_passed',clock_tests_passed=2,input_pins=pins,test_names=names,stdout_sha256=sha(ROOT/'native.stdout.log'),stderr_sha256=sha(ROOT/'native.stderr.log'))
    except Exception as exc:state.update(status='installed_clock_check_refusal_retained',error=repr(exc))
    state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()

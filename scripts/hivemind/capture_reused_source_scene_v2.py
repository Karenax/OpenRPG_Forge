"""Reuse an exactly pinned capture executable for shader/data-only private cases."""
from pathlib import Path
import datetime,hashlib,json,os,shutil,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');BASE=W/'firepit-source-shadow-raster-native-v3';BINARY=W/'source-scene-capture-binary-v2';EXE=W/'firepit-static-glb-product-check-v2/target/debug/openrpg-firepit-source-ao-scene-v1.exe'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def save(p,value):
 t=p.with_suffix('.tmp');t.write_text(json.dumps(value,indent=2),'utf-8');t.replace(p)
def check(pins):
 for p,h in pins.items():assert sha(p)==h,p
def binary():
 if BINARY.exists():
  receipt=read(BINARY/'BINARY.v1.json');check(receipt['input_pins']);return BINARY/'capture.exe'
 prior=read(BASE/'RUN-RESULT.v1.json');assert prior.get('actual_exit_code')==0
 pins=read(BASE/'PREPARED.v1.json')['input_pins'];check(pins)
 stderr=(BASE/'stderr.log').read_text('utf-8');assert 'Finished `dev`' in stderr and str(EXE) in stderr and 'Running `' in stderr
 assert not any(p.exists() for p in [W/'source-scene-capture-binary-v2/BINARY.v1.json'])
 BINARY.mkdir();shutil.copyfile(EXE,BINARY/'capture.exe')
 files=[EXE,BASE/'src/main.rs',BASE/'Cargo.toml',BASE/'Cargo.lock',BASE/'stderr.log',BASE/'PREPARED.v1.json',BASE/'RUN-RESULT.v1.json',BINARY/'capture.exe']
 # Mutable target exe is only checked during acquisition. The retained executable has its own SHA.
 capture_pins={**pins,**{str(p):sha(p) for p in files if p!=EXE}}
 save(BINARY/'BINARY.v1.json',{'status':'compiled_source_capture_executable_retained_for_identical_rust_inputs','origin_executable':str(EXE),'origin_executable_sha256':sha(EXE),'original_runtime_exit':prior['actual_exit_code'],'runtime_shader_case_qualified':False,'input_pins':capture_pins,'full_import':False})
 check(capture_pins);return BINARY/'capture.exe'
def prepare(case_root,root):
 assert root.parent==W and case_root.parent==W and not root.exists()
 proof=read(case_root/'RESULT.v1.json');case=read(case_root/'CASE.v1.json');check(proof['input_pins'])
 fixture=Path(proof['private_fixture']);assert sha(fixture)==proof['private_fixture_sha256']
 executable=binary();bp=read(BINARY/'BINARY.v1.json');check(bp['input_pins'])
 root.mkdir();(root/'src').mkdir()
 for name in ['Cargo.toml','Cargo.lock','src/main.rs','depth-probe.wgsl']:shutil.copyfile(BASE/name,root/name)
 assert sha(root/'src/main.rs')==sha(BASE/'src/main.rs')
 shutil.copyfile(case_root/'CASE.v1.json',root/'CASE.v1.json')
 files=[Path(__file__).resolve(),executable,BINARY/'BINARY.v1.json',fixture,case_root/'RESULT.v1.json',case_root/'CASE.v1.json',*[p for p in root.rglob('*') if p.is_file()]]
 save(root/'PREPARED.v1.json',{'fixture':str(fixture),'input_pins':{**bp['input_pins'],**proof['input_pins'],**{str(p):sha(p) for p in files}},'binary_source_identical':True,'compiler_not_repeated':True,'full_import':False})
 with (root/'controller.log').open('xb') as out:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run',str(root)],stdout=out,stderr=out,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save(root/'JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(root),'binary_reused':True}))
def run(root):
 state={'status':'actual_reused_source_scene_capture_running','full_import':False,'editor_visual_verified':False,'gpu_shader_compile_qualified':False,'compiler_not_repeated':True}
 try:
  prep=read(root/'PREPARED.v1.json');check(prep['input_pins'])
  env=os.environ.copy();env.update(TEMP=str(root),TMP=str(root))
  with (root/'stdout.log').open('xb') as out,(root/'stderr.log').open('xb') as err:
   child=subprocess.Popen([str(BINARY/'capture.exe'),prep['fixture'],str(root/'NATIVE-RESULT.v1.json')],cwd=root,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state['capture_pid']=child.pid;save(root/'RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert state['actual_exit_code']==0
  report=read(root/'NATIVE-RESULT.v1.json');assert report['same_bevy_device'] and report['bc_feature'] and not report['root_revealed']
  pair=read(root/'GPU-PAIR.v1.json');assert len(pair['images'])==2 and len(pair['source_pipelines'])>=5 and all(p['state']=='ready' for p in pair['source_pipelines'])
  check(prep['input_pins']);state.update(status='actual_reused_source_scene_capture_two_gpu_views_passed',nodes=report['nodes'],primitives=report['primitives'],material_instances=report['material_instances'],source_pipelines_ready=len(pair['source_pipelines']),input_pins=prep['input_pins'],native_receipt_sha256=sha(root/'NATIVE-RESULT.v1.json'))
 except Exception as error:state.update(status='actual_reused_source_scene_capture_refusal_retained',error=repr(error))
 state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save(root/'RUN-RESULT.v1.json',state)
if __name__=='__main__':
 if sys.argv[1]=='binary':print(binary())
 elif sys.argv[1]=='prepare':prepare(Path(sys.argv[2]),Path(sys.argv[3]))
 else:run(Path(sys.argv[2]))

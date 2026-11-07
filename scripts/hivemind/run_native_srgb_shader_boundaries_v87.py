from pathlib import Path
import subprocess,sys,os,json,hashlib
from datetime import datetime,timezone
S=Path(__file__).parent;R=S.parents[1];W=Path('F:/OpenRPG_ImportWork');O=W/'native-srgb-shader-boundaries-v87';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');E=R/'crates/forge_engine/examples/verify_native_srgb_shader_boundaries_v87.rs'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(v):
 t=O/'RUN-RESULT.tmp';t.write_text(json.dumps(v,indent=2),encoding='utf-8');t.replace(O/'RUN-RESULT.v1.json')
if len(sys.argv)==1:
 assert not O.exists();assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process cargo -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0';subprocess.run([str(BIN/'rustfmt.exe'),'--edition','2024',str(E)],check=True);O.mkdir();pins={str(p):sha(p) for p in [Path(__file__),E,R/'Cargo.lock',R/'Cargo.toml',R/'.cargo/config.toml']};(O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,purchased_asset_inputs=0,unity_pixel_inputs=0,full_import=False),indent=2),encoding='utf-8');save(dict(status='generated_srgb_transfer_ramp_prepared',full_import=False))
 prior=W/'native-srgb-generated-boundaries-reopen-v85/RESULT.v1.json';p=json.loads(prior.read_text(encoding='utf-8'));curve=W/'native-srgb-generated-boundaries-v85/NATIVE-SRGB-INPUT-THRESHOLDS.f32';ref=W/'native-srgb-generated-boundaries-v85/ADJACENT-0.rgba8srgb';assert sha(curve)==p['input_pins'][str(curve)];assert sha(ref)==p['input_pins'][str(ref)];(O/'NATIVE-SRGB-INPUT-THRESHOLDS.f32').write_bytes(curve.read_bytes());(O/'EXPECTED-NATIVE-SRGB.rgba8srgb').write_bytes(ref.read_bytes());adj=W/'native-srgb-generated-boundaries-v85/ADJACENT-0.inputs.f32';assert sha(adj)==p['input_pins'][str(adj)];(O/'ADJACENT-INPUTS.f32').write_bytes(adj.read_bytes());(O/'CURVE-METADATA.v1.json').write_bytes(prior.read_bytes());prep=json.loads((O/'PREPARED.v1.json').read_text());prep['input_pins'].update({str(k):sha(k) for k in [prior,curve,ref,adj,O/'ADJACENT-INPUTS.f32',O/'NATIVE-SRGB-INPUT-THRESHOLDS.f32',O/'EXPECTED-NATIVE-SRGB.rgba8srgb',O/'CURVE-METADATA.v1.json',R/'crates/forge_engine/src/source_graph_srgb_encode.wgsl']});(O/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),encoding='utf-8')

 with (O/'controller.log').open('xb') as f:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],stdout=f,stderr=f,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid)),encoding='utf-8');print(json.dumps(dict(controller_pid=child.pid,root=str(O))));sys.exit()
state=dict(status='generated_srgb_transfer_ramp_compiling',full_import=False)
try:
 prep=json.loads((O/'PREPARED.v1.json').read_text());
 for key,h in prep['input_pins'].items():assert sha(Path(key))==h,key
 env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(O),TMP=str(O));cmd=[str(BIN/'cargo.exe'),'run','--locked','--offline','-p','forge_engine','--example','verify_native_srgb_shader_boundaries_v87','--',str(O)]
 with (O/'stdout.log').open('xb') as out,(O/'stderr.log').open('xb') as err:
  child=subprocess.Popen(cmd,cwd=R,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['cargo_pid']=child.pid;save(state);state['actual_exit_code']=child.wait()
 assert state['actual_exit_code']==0;native=json.loads((O/'NATIVE-RESULT.v1.json').read_text());assert native['repeated_raw_readbacks_byte_equal']==3 and native['all_256_codes_observed'];state.update(status=native['status'],gpu_probe_executed=True,native_receipt_sha256=sha(O/'NATIVE-RESULT.v1.json'))
 for key,h in prep['input_pins'].items():assert sha(Path(key))==h,key
except Exception as e:state.update(status='generated_srgb_transfer_probe_refusal_retained',error=repr(e),traceback=__import__('traceback').format_exc())
state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)

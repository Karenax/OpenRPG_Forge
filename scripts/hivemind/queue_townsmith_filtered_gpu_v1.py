from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys,os,shutil
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-native-filtered-gpu-control-v1';S=Path(__file__).parent;DEPS=W/'firepit-static-glb-product-check-v2/target/debug/deps';BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,d):
 p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(d,indent=2),encoding='utf-8');t.replace(p)
def prepare():
 assert not ROOT.exists();ROOT.mkdir()
 manifest=W/'townsmith-s-paint-native-resources-v1/RESOURCES.v1.json';d=read(manifest);pins=dict(d['input_pins']);pins[str(manifest)]=sha(manifest)
 for p,h in pins.items():assert sha(p)==h,p
 normal=W/'townsmith-s-paint-native-normal-decode-reopen-v1/RESULT.v1.json';n=read(normal)
 assert all(row['native_unpack_mode']==1 for row in n['rows']);pins[str(normal)]=sha(normal)
 program=W/'townsmith-s-paint-normal-abi-v1/material-program-common-normal-abi.wgsl';text=program.read_text(encoding='utf-8');prefix=text.split('// Source graph material arithmetic candidate.',1)[0];assert prefix.startswith('fn source_unpack_normal_sample')
 shader=ROOT/'load-decode.wgsl';shader.write_text(prefix+'''struct Params { count:u32, mode:u32, decode:u32, reserved:u32 }
@group(0) @binding(0) var source_texture:texture_2d<f32>;
@group(0) @binding(1) var<storage,read> coordinates:array<vec4<f32>>;
@group(0) @binding(2) var<uniform> params:Params;
@group(0) @binding(3) var<storage,read_write> values:array<vec4<f32>>;
@group(0) @binding(4) var source_sampler:sampler;
@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) id:vec3<u32>) {
 if id.x>=params.count {return;}
 let p=coordinates[id.x];let packed=textureSampleLevel(source_texture,source_sampler,p.xy,p.z);
 values[2u*id.x]=packed;
 if params.decode!=0u {values[2u*id.x+1u]=source_unpack_normal_sample(packed,params.mode);}
 else {values[2u*id.x+1u]=packed;}
}
''',encoding='utf-8')
 code=S/'candidates/SourceFilteredNativeTextureV1.rs';target=ROOT/'gpu-control.rs';shutil.copy2(code,target)
 libs={}
 for name in ['wgpu','serde_json']:
  p=max(DEPS.glob('lib'+name+'-*.rlib'),key=lambda p:p.stat().st_mtime);libs[name]=str(p);pins[str(p)]=sha(p)
 inputpath=ROOT/'INPUT.v1.json';inputpath.write_bytes(manifest.read_bytes())
 for p in [code,target,shader,program,inputpath,Path(__file__),W/'townsmith-s-paint-normal-abi-v1/PROFILES.v1.json']:pins[str(p)]=sha(p)
 save('PREPARED.v1.json',{'input_pins':pins,'libraries':libs,'normal_reference':str(normal),'full_import':False})
 with (ROOT/'controller.log').open('xb') as log:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(ROOT)}))
def run():
 state={'status':'native_texture_gpu_control_compiling','full_import':False}
 try:
  prep=read(ROOT/'PREPARED.v1.json')
  for p,h in prep['input_pins'].items():assert sha(p)==h,p
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT))
  command=[str(BIN/'rustc.exe'),'--edition=2024',str(ROOT/'gpu-control.rs'),'-L','dependency='+str(DEPS),'-o',str(ROOT/'gpu-control.exe')]
  for name,p in prep['libraries'].items():command+=['--extern',name+'='+p]
  save('COMMAND.v1.json',{'command':command})
  with (ROOT/'compile.stdout.log').open('xb') as out,(ROOT/'compile.stderr.log').open('xb') as err:
   child=subprocess.Popen(command,cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['rustc_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['compile_exit_code']=child.wait();assert child.returncode==0
  with (ROOT/'gpu.stdout.log').open('xb') as out,(ROOT/'gpu.stderr.log').open('xb') as err:
   child=subprocess.Popen([str(ROOT/'gpu-control.exe'),str(ROOT)],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state.update(status='native_texture_gpu_control_running',gpu_pid=child.pid);save('RUN-RESULT.v1.json',state);state['gpu_exit_code']=child.wait();assert child.returncode==0
  native=read(ROOT/'NATIVE-RESULT.v1.json');assert len(native['rows'])==10
  for p,h in prep['input_pins'].items():assert sha(p)==h,p
  state.update(status='native_texture_gpu_control_finished_inputs_unchanged',native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),artifact_pins={r['file']:sha(r['file']) for r in native['rows']},source_gpu_equivalence_verified=False,actual_engine_material_render_qualified=False,complete_assets_delivered_increment=0)
 except Exception as e:state.update(status='native_texture_gpu_control_refusal_retained',error=repr(e))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if len(sys.argv)==1 else run()

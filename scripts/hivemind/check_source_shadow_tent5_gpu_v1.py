"""Run the isolated WGSL tent5 math on the actual native Bevy device."""
from pathlib import Path
import datetime,hashlib,json,os,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-shadow-tent5-gpu-check-v1';S=Path(__file__).parent
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,v):
    p=ROOT/name;t=p.with_suffix('.tmp');t.write_text(json.dumps(v,indent=2),'utf-8');t.replace(p)
def prepare():
    assert not ROOT.exists();ROOT.mkdir();(ROOT/'src').mkdir()
    protocol=W/'firepit-source-shadow-tent5-v1/TENT5-PROTOCOL.v1.json'
    for name in ['Cargo.toml','Cargo.lock']:(ROOT/name).write_bytes((W/'source-punctual-light-contract-check-v1'/name).read_bytes())
    recipe=S/'candidates/SourceShadowTent5.wgsl';runner=S/'candidates/SourceShadowTent5CheckV1.rs'
    (ROOT/'src/main.rs').write_bytes(runner.read_bytes());(ROOT/'PROTOCOL.v1.json').write_bytes(protocol.read_bytes())
    kernel='''
@group(0) @binding(0) var<storage,read> cases:array<vec4<f32>>;
@group(0) @binding(1) var<storage,read_write> output:array<vec4<f32>>;
@compute @workgroup_size(64) fn main(@builtin(global_invocation_id) id:vec3<u32>) {
 if(id.x>=arrayLength(&cases)){return;}
 let item=cases[id.x];let s=source_tent5_samples(vec4<f32>(1.0/item.z,1.0/item.w,item.zw),item.xy/item.zw);
 for(var i=0u;i<9u;i+=1u){output[id.x*9u+i]=vec4<f32>(s.weights[i],s.uv[i],0.0);}
}
'''
    (ROOT/'shader.wgsl').write_text(recipe.read_text('utf-8')+kernel,'utf-8')
    files=[Path(__file__).resolve(),protocol,recipe,runner,*[p for p in ROOT.rglob('*') if p.is_file()]]
    save('PREPARED.v1.json',{'input_pins':{str(p):sha(p) for p in files},'expected_cases':25,'expected_taps_per_case':9,'full_import':False})
    with (ROOT/'controller.log').open('xb') as out:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],stdout=out,stderr=out,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(ROOT)}))
def run():
    state={'status':'native_tent5_gpu_check_running','full_import':False}
    try:
        pins=json.loads((ROOT/'PREPARED.v1.json').read_text())['input_pins'];assert all(sha(p)==h for p,h in pins.items())
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
        with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
            child=subprocess.Popen([str(BIN/'cargo.exe'),'run','--locked','--offline','--',str(ROOT)],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['cargo_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait()
        assert state['actual_exit_code']==0;assert all(sha(p)==h for p,h in pins.items())
        state.update(status='actual_native_tent5_gpu_weights_uv_readback_complete',input_pins=pins,native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'))
    except Exception as error:state.update(status='native_tent5_gpu_refusal_retained',error=repr(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if len(sys.argv)==1 else run()

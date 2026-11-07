from pathlib import Path
import json,hashlib,os,subprocess,datetime
R=Path(__file__).resolve().parents[2];W=Path('F:/OpenRPG_ImportWork');O=W/'placement-reviewed-physics-wasm-v803';B=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin');assert not O.exists();O.mkdir();(O/'temp').mkdir()
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
files=[Path(__file__),R/'Cargo.toml',R/'Cargo.lock',R/'apps/forge_studio/Cargo.toml'];files += [p for p in (R/'crates').rglob('*') if p.is_file() and (p.suffix in ['.rs','.wgsl','.toml'] or p.name=='build.rs')];files += [p for p in (R/'apps/forge_studio/src').rglob('*') if p.is_file()];pins={str(p):sha(p) for p in files};(O/'INPUT-PINS.v1.json').write_text(json.dumps(pins,indent=2),'utf-8')
env=os.environ.copy();env.update(RUSTC=str(B/'rustc.exe'),RUSTDOC=str(B/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
command=[str(B/'cargo.exe'),'check','--locked','--offline','-j','1','-p','forge_studio','--no-default-features','--features','web','--target','wasm32-unknown-unknown','--lib']
with (O/'cargo.log').open('wb') as log:
 proc=subprocess.Popen(command,env=env,cwd=R,stdout=log,stderr=log);(O/'JOB.v1.json').write_text(json.dumps({'pid':proc.pid,'command':command}));code=proc.wait()
changed=[p for p,h in pins.items() if sha(p)!=h];result={'status':'studio_wasm_compile_verified' if code==0 and not changed else 'failed_or_inputs_changed','exit_code':code,'changed_inputs':changed,'input_count':len(pins),'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'studio_visual_verified':False,'full_import_increment':0};(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(result)






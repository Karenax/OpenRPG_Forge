"""Read one current source-card image through the native product reader."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,subprocess
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'house-bench-current-native-library-probe-v2';OLD=W/'house-bench-current-native-library-probe-v1'
A=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src')
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists();ROOT.mkdir();(ROOT/'src').mkdir()
 for name in ['Cargo.toml','Cargo.lock']:(ROOT/name).write_bytes((OLD/name).read_bytes())
 original=(A/'asset_library_native.rs').read_text('utf-8');needle='mod history;';replacement='#[path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src/asset_library_native/history.rs"] mod history;';assert original.count(needle)==1
 reader=original.replace(needle,replacement);assert reader.replace(replacement,needle)==original
 (ROOT/'src/asset_library_native.rs').write_bytes(reader.encode('utf-8'))
 main_source=(OLD/'src/main.rs').read_text('utf-8');old='#[path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src/asset_library_native.rs"] mod asset_library_native;';assert main_source.count(old)==1
 (ROOT/'src/main.rs').write_bytes(main_source.replace(old,'mod asset_library_native;').encode('utf-8'))
 inputs=[Path(__file__).resolve(),*[A/s for s in ['asset_library_native.rs','asset_library_native/history.rs','asset_library_model_wire.rs','asset_library_protocol.rs']],ROOT/'Cargo.toml',ROOT/'Cargo.lock',ROOT/'src/main.rs',ROOT/'src/asset_library_native.rs']
 pins={str(p):sha(p) for p in inputs};(ROOT/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,only_reader_change='Explicit path to the same product history module, needed in this isolated crate.',editor_visual_verified=False,full_import=False),indent=2),'utf-8')
 env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_BUILD_JOBS='1',CARGO_TARGET_DIR=str(W/'asset-library-all-entries-native-audit-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
 state=dict(status='current_native_bench_library_probe_refusal_retained',editor_visual_verified=False,full_import=False)
 try:
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   proc=subprocess.Popen([str(BIN/'cargo.exe'),'run','--locked','--offline','--',str(ROOT/'NATIVE-RESULT.v1.json')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   (ROOT/'JOB.v1.json').write_text(json.dumps(dict(cargo_pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');state['actual_exit_code']=proc.wait();assert proc.returncode==0
  native=json.loads((ROOT/'NATIVE-RESULT.v1.json').read_text());assert native['non_placeable_enforced'] and native['indexed_cards']==26735
  for p,d in pins.items():assert sha(p)==d,p
  state.update(status='current_native_reader_bench_search_filter_details_image_passed',native_result_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),input_pins=pins,thumbnail_sha256=native['thumbnail_sha256'],library_revision=native['library_revision'],reader_logic_unchanged=True)
 except Exception as exc:state['error']=repr(exc)
 state['finished_utc']=datetime.now(timezone.utc).isoformat();(ROOT/'RESULT.v1.json').write_text(json.dumps(state,indent=2),'utf-8');print(json.dumps({k:v for k,v in state.items() if k!='input_pins'}))
if __name__=='__main__':main()

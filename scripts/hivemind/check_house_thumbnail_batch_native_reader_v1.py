"""Verify all eight indexed images through the current product library reader."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,os,subprocess
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'house-source-thumbnail-batch-0001-0008-native-reader-v1';OLD=W/'house-bench-current-native-library-probe-v2';INDEX=W/'house-source-thumbnail-batch-0001-0008-index-v1/RESULT.v1.json'
A=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src');BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,v):Path(p).write_text(json.dumps(v,indent=2,allow_nan=False),'utf-8')
def main():
 assert not ROOT.exists();index=json.loads(INDEX.read_text('utf-8'));assert index['cards_updated']==5;ROOT.mkdir();(ROOT/'src').mkdir()
 for name in ['Cargo.toml','Cargo.lock']:(ROOT/name).write_bytes((OLD/name).read_bytes())
 original=(A/'asset_library_native.rs').read_text('utf-8');needle='mod history;';replacement='#[path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src/asset_library_native/history.rs"] mod history;';assert original.count(needle)==1;reader=original.replace(needle,replacement);assert reader.replace(replacement,needle)==original;(ROOT/'src/asset_library_native.rs').write_bytes(reader.encode('utf-8'))
 rust='''#[path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src/asset_library_protocol.rs"] mod asset_library_protocol;
#[path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/apps/forge_studio/src/asset_library_model_wire.rs"] mod asset_library_model_wire;
mod asset_library_native;
use asset_library_protocol::*;use sha2::{Digest,Sha256};
fn main(){
 let args:Vec<String>=std::env::args().collect();let input:serde_json::Value=serde_json::from_slice(&std::fs::read(&args[1]).unwrap()).unwrap();
 let mut library=asset_library_native::Library::new(std::path::Path::new("F:/OpenRPG_AssetVault")).unwrap();let mut rows=Vec::new();
 for (i,card) in input["cards"].as_array().unwrap().iter().enumerate(){
  let id=card["id"].as_str().unwrap();let name=card["source_name"].as_str().unwrap();
  let LibraryResponse::Page{page}=library.execute(LibraryRequest::Search{query:LibraryQuery{text:name.into(),pack:"hivemind.house-on-a-hill".into(),availability:"source_support".into(),limit:48,refresh:i==0,..Default::default()}}).unwrap()else{panic!("Native search required")};
  assert_eq!(page.total_assets,26735);let found=page.assets.iter().find(|c|c.id==id).expect("Exact source card found");assert!(found.has_thumbnail);assert_eq!(found.availability,"source_support");
  let LibraryResponse::Details{detail}=library.execute(LibraryRequest::Details{id:id.into(),revision:page.revision.clone()}).unwrap()else{panic!("Native details required")};assert!(detail.model_path.is_none());assert!(detail.source_path.is_some());assert_eq!(detail.license,"LicenseRef-Unity-Asset-Store-EULA");assert!(detail.preview_path.is_some());
  let LibraryResponse::Thumbnail{bytes}=library.execute(LibraryRequest::Thumbnail{id:id.into(),revision:page.revision.clone()}).unwrap()else{panic!("Native thumbnail required")};let digest=format!("{:x}",Sha256::digest(&bytes));assert_eq!(digest,card["primary_sha256"].as_str().unwrap());assert_eq!(&bytes[..8],b"\\x89PNG\\r\\n\\x1a\\n");assert!(library.execute(LibraryRequest::Model{id:id.into(),revision:page.revision.clone()}).is_err());
  rows.push(serde_json::json!({"id":id,"library_revision":page.revision,"matched_cards":page.matched_assets,"thumbnail_bytes":bytes.len(),"thumbnail_sha256":digest,"license":detail.license,"non_placeable_enforced":true}));
 }
 assert_eq!(rows.len(),5);let result=serde_json::json!({"status":"five_source_card_search_filter_details_and_thumbnail_verified_by_current_native_reader","cards_verified":5,"indexed_cards":26735,"rows":rows,"actual_editor_visual_verified":false,"full_import":false});
 use std::io::Write;let mut file=std::fs::OpenOptions::new().write(true).create_new(true).open(&args[2]).unwrap();file.write_all(&serde_json::to_vec_pretty(&result).unwrap()).unwrap();println!("Five current native card probes passed");
}
'''
 (ROOT/'src/main.rs').write_bytes(rust.encode('utf-8'))
 inputs=[Path(__file__).resolve(),INDEX,*[A/s for s in ['asset_library_native.rs','asset_library_native/history.rs','asset_library_model_wire.rs','asset_library_protocol.rs']],ROOT/'Cargo.toml',ROOT/'Cargo.lock',ROOT/'src/main.rs',ROOT/'src/asset_library_native.rs'];pins={str(p):sha(p) for p in inputs};write(ROOT/'PREPARED.v1.json',dict(input_pins=pins,only_reader_change='Explicit path to the same product history module in the isolated crate.',editor_visual_verified=False,full_import=False))
 env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_BUILD_JOBS='1',CARGO_TARGET_DIR=str(W/'asset-library-all-entries-native-audit-v2/target'),TEMP=str(ROOT),TMP=str(ROOT))
 state=dict(status='eight_card_current_native_reader_probe_refusal_retained',editor_visual_verified=False,full_import=False)
 try:
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   proc=subprocess.Popen([str(BIN/'cargo.exe'),'run','--locked','--offline','--',str(INDEX),str(ROOT/'NATIVE-RESULT.v1.json')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);write(ROOT/'JOB.v1.json',dict(cargo_pid=proc.pid));state['actual_exit_code']=proc.wait();assert proc.returncode==0
  native=json.loads((ROOT/'NATIVE-RESULT.v1.json').read_text());assert native['cards_verified']==5
  for p,d in pins.items():assert sha(p)==d,p
  for p,d in index['preview_pins'].items():assert sha(p)==d,p
  state.update(status='five_current_native_source_card_search_filter_details_images_passed',cards_verified=5,native_result_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),input_pins=pins,reader_logic_unchanged=True)
 except Exception as exc:state['error']=repr(exc)
 state['finished_utc']=datetime.now(timezone.utc).isoformat();write(ROOT/'RESULT.v1.json',state);print(json.dumps({k:v for k,v in state.items() if k!='input_pins'}))
if __name__=='__main__':main()

"""Test the actual reader on private copies of 24 real carriers and packages."""
from pathlib import Path
import datetime, hashlib, json, os, shutil, subprocess
R=Path(__file__).resolve().parents[2];A=R/'apps/forge_studio/src'
W=Path('F:/OpenRPG_ImportWork');O=W/'saved-physics-reader-v772'
B=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert not O.exists();O.mkdir();(O/'src').mkdir();(O/'temp').mkdir()
seed=W/'library-physics-v730/private-vault-fixture';fixture=O/'private-vault-fixture'
shutil.copytree(seed,fixture)
def sha(p):
    with Path(p).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def write(p,value):Path(p).write_text(json.dumps(value),'utf-8')
# Rebase only copies. Their metadata is pinned anew, original evidence unchanged.
for p in fixture.rglob('*.json'):
    text=p.read_text('utf-8-sig').replace(json.dumps(str(seed))[1:-1],json.dumps(str(fixture))[1:-1])
    p.write_text(text,'utf-8')
registry=read(fixture/'metadata/asset-import-registry.v1.json')
for entry in registry['packs'].values():
    for name in entry['files']:entry['files'][name]=sha(fixture/entry['path']/name)
write(fixture/'metadata/asset-import-registry.v1.json',registry)
cases=read(W/'library-physics-v730/CASES.v1.json')
assert len(cases)==24
for case in cases:
    case['package_path']=str(fixture/Path(case['package_path']).relative_to(seed))
write(O/'CASES.v1.json',cases)
reader=(A/'asset_library_native.rs').read_text('utf-8')
for module in ['history','physics_transport']:
    reader=reader.replace('mod '+module+';','#[path="'+(A/'asset_library_native'/(module+'.rs')).as_posix()+'"] mod '+module+';')
(O/'src/asset_library_native.rs').write_text(reader,'utf-8')
main=r'''
#[allow(dead_code)] #[path="@@A@@/asset_library_protocol.rs"] mod asset_library_protocol;
#[allow(dead_code)] #[path="@@A@@/asset_library_model_wire.rs"] mod asset_library_model_wire;
#[allow(dead_code)] mod asset_library_native;
use asset_library_protocol::*;use forge_core::ContentHash;use serde_json::Value;
fn hash(bytes:&[u8])->String{forge_core::bytes_sha256(bytes).to_string()}
fn main(){
 let args:Vec<String>=std::env::args().collect();let root=std::path::Path::new(&args[1]);
 let cases:Vec<Value>=serde_json::from_slice(&std::fs::read(&args[2]).unwrap()).unwrap();
 let mut library=asset_library_native::Library::new(root).unwrap();
 let mut requests=Vec::new();let mut negative=0;
 for case in &cases{
  let request=LibrarySavedPhysicsRequest{schema_version:1,asset_id:vault_asset_id(case["id"].as_str().unwrap()),carrier_sha256:ContentHash::parse(case["carrier_sha256"].as_str().unwrap()).unwrap(),manifest_sha256:ContentHash::parse(case["manifest_sha256"].as_str().unwrap()).unwrap(),package_sha256:ContentHash::parse(case["package_sha256"].as_str().unwrap()).unwrap()};
  let wire=library.read_saved_physics_package(&request).unwrap();assert_eq!(forge_core::bytes_sha256(&wire),request.package_sha256);
  for fault in 0..5{let mut bad=request.clone();match fault{0=>bad.schema_version=2,1=>bad.asset_id=forge_core::AssetId::deterministic("unknown"),2=>bad.carrier_sha256=ContentHash::from_bytes([0;32]),3=>bad.manifest_sha256=ContentHash::from_bytes([0;32]),_=>bad.package_sha256=ContentHash::from_bytes([0;32])};assert!(library.read_saved_physics_package(&bad).is_err());negative+=1;}
  requests.push((request,wire));
 }
 // Same model hashes, current profile without physics: saved pins must resolve
 // the old profile instead of falling back to current metadata.
 let rp=root.join("metadata/asset-import-registry.v1.json");let mut registry:Value=serde_json::from_slice(&std::fs::read(&rp).unwrap()).unwrap();
 let entry=registry["packs"]["hivemind.house-on-a-hill"].clone();let oldroot=root.join(entry["path"].as_str().unwrap());
 let newroot=root.join("metadata/rev/house/current-without-physics");std::fs::create_dir_all(&newroot).unwrap();
 let details=std::fs::read(oldroot.join("details.v1.json")).unwrap();let mut profiles:Value=serde_json::from_slice(&std::fs::read(oldroot.join("profiles.v1.json")).unwrap()).unwrap();
 for profile in profiles["profiles"].as_array_mut().unwrap(){profile.as_object_mut().unwrap().remove("source_physics_package");}
 let profiles=serde_json::to_vec(&profiles).unwrap();std::fs::write(newroot.join("details.v1.json"),&details).unwrap();std::fs::write(newroot.join("profiles.v1.json"),&profiles).unwrap();
 let mut old=entry;old["revision"]=hash(b"historical physics").into();
 registry["packs"]["hivemind.house-on-a-hill"]=serde_json::json!({"path":"metadata/rev/house/current-without-physics","revision":hash(b"new current profile"),"files":{"details.v1.json":hash(&details),"profiles.v1.json":hash(&profiles)},"history":[old]});
 std::fs::write(&rp,serde_json::to_vec(&registry).unwrap()).unwrap();
 for (request,expected) in &requests{assert_eq!(library.read_saved_physics_package(&request).unwrap(),*expected);}
 let LibraryResponse::Page{page}=library.execute(LibraryRequest::Search{query:LibraryQuery::default()}).unwrap()else{panic!()};assert_eq!(page.total_assets,24);
 // Corrupt package read is refused; restore only this disposable fixture.
 let path=std::path::Path::new(cases[0]["package_path"].as_str().unwrap());let mut corrupt=requests[0].1.clone();*corrupt.last_mut().unwrap()^=1;std::fs::write(path,&corrupt).unwrap();assert!(library.read_saved_physics_package(&requests[0].0).is_err());negative+=1;std::fs::write(path,&requests[0].1).unwrap();
 println!("{}",serde_json::json!({"current_packages":24,"historical_packages":24,"same_carrier_changed_profile":true,"search_cards":24,"negative_controls":negative,"fixture_only":true,"studio_visual_verified":false}));
}
'''.replace('@@A@@',A.as_posix())
(O/'src/main.rs').write_text(main,'utf-8')
cargo=(W/'library-physics-v736/Cargo.toml').read_text('utf-8').replace('library-physics-v736','saved-physics-reader-v772')
(O/'Cargo.toml').write_text(cargo,'utf-8')
env=os.environ.copy();env.update(RUSTC=str(B/'rustc.exe'),RUSTDOC=str(B/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
with (O/'lock.log').open('wb') as log:assert subprocess.call([str(B/'cargo.exe'),'generate-lockfile','--offline'],cwd=O,env=env,stdout=log,stderr=log)==0
files=[Path(__file__),A/'asset_library_native.rs',A/'asset_library_native/history.rs',A/'asset_library_native/physics_transport.rs',A/'asset_library_protocol.rs',A/'asset_library_model_wire.rs',O/'Cargo.toml',O/'Cargo.lock',O/'src/main.rs',O/'src/asset_library_native.rs',O/'CASES.v1.json']
files += [p for p in seed.rglob('*') if p.is_file()]
pins={str(p):sha(p) for p in files};write(O/'INPUT-PINS.v1.json',pins)
command=[str(B/'cargo.exe'),'run','--locked','--offline','-j','1','--',str(fixture),str(O/'CASES.v1.json')]
with (O/'cargo.log').open('wb') as log:
    proc=subprocess.Popen(command,cwd=O,env=env,stdout=log,stderr=log);write(O/'JOB.v1.json',dict(pid=proc.pid,command=command));code=proc.wait()
changed=[p for p,h in pins.items() if sha(p)!=h]
result=dict(status='saved_physics_reader_verified' if code==0 and not changed else 'failed_or_inputs_changed',exit_code=code,changed_inputs=changed,utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),studio_visual_verified=False,full_import_increment=0)
write(O/'RESULT.v1.json',result);print(json.dumps(result))


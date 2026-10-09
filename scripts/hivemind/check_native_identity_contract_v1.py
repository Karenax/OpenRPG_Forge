"""Compile isolated validator against cached dependencies; never changes Cargo output."""
from pathlib import Path
from datetime import datetime,timezone
import subprocess,json,hashlib,os
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'native-texture-identity-contract-check-v1'
CAND=W/'native-texture-identity-contract-candidate-v1/source_graph_bundle.candidate.rs'
DEPS=W/'firepit-static-glb-product-check-v2/target/debug/deps'
BIN=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists();ROOT.mkdir()
    wrapper=ROOT/'validator.rs'
    wrapper.write_text('''#[path = "../native-texture-identity-contract-candidate-v1/source_material_parameters.rs"]
mod source_material_parameters;
#[path = "../native-texture-identity-contract-candidate-v1/source_graph_bundle.candidate.rs"]
mod source_graph_bundle;
#[cfg(test)] mod native_resources {
 use crate::source_graph_bundle::{SourceGraphImportedTextureStorage as ImportedTextureStorage,
 SourceGraphImportedStorageMip as ImportedStorageMip, SourceGraphResolvedStorageSampler as ResolvedStorageSampler,
 SourceGraphImportedStorageError as StorageError, NativeCaptureIdentity, NativeCaptureProtocol,
 SourceGraphTexture, SourceGraphContractError, validate_texture_integrity};
 use crate::source_graph_bundle::SourceGraphBlob;
 use forge_core::bytes_sha256;
 use serde_json::{Value,json};
 use std::fs;
 #[test] fn ten_actual_resource_chains_and_identity_sampler_refusals() {
  let manifest:Value=serde_json::from_slice(&fs::read("F:/OpenRPG_ImportWork/townsmith-s-paint-native-resources-v1/RESOURCES.v1.json").unwrap()).unwrap();
  for row in manifest["rows"].as_array().unwrap() {
   let data=fs::read(row["native_storage"].as_str().unwrap()).unwrap();
   let capture=fs::read(row["storage_capture"].as_str().unwrap()).unwrap();
   let mips:Vec<ImportedStorageMip>=row["mips"].as_array().unwrap().iter().map(|m|ImportedStorageMip{
    width:m["width"].as_u64().unwrap() as u32,height:m["height"].as_u64().unwrap() as u32,
    offset:m["offset"].as_u64().unwrap(),bytes:m["bytes"].as_u64().unwrap(),sha256:serde_json::from_value(m["sha256"].clone()).unwrap()}).collect();
   let sampler=&row["sampler"];let filter=match sampler["filter_mode"].as_str().unwrap(){"Point"=>0,"Bilinear"=>1,"Trilinear"=>2,_=>panic!()};
   let wrap=|key:&str|match sampler[key].as_str().unwrap(){"Repeat"=>0,"Clamp"=>1,"Mirror"=>2,"MirrorOnce"=>3,_=>panic!()};
   let mut value=ImportedTextureStorage{schema_version:2,native_capture:Some(NativeCaptureIdentity{
    protocol:serde_json::from_value(row["storage_capture_protocol"].clone()).unwrap(),source_guid:row["guid"].as_str().unwrap().to_string()}),
    storage:SourceGraphBlob{buffer_view:0,sha256:bytes_sha256(&data)},capture:SourceGraphBlob{buffer_view:1,sha256:bytes_sha256(&capture)},
    capture_case_index:row["storage_capture_case_index"].as_u64().unwrap() as u32,width:row["width"].as_u64().unwrap() as u32,height:row["height"].as_u64().unwrap() as u32,
    format:serde_json::from_value(row["encoding"].clone()).unwrap(),mips,sampler:ResolvedStorageSampler{filter_mode:filter,wrap:[wrap("wrap_u"),wrap("wrap_v"),wrap("wrap_w")],anisotropy:sampler["aniso_level"].as_u64().unwrap() as u8,mip_bias:sampler["mip_map_bias"].as_f64().unwrap(),source_mipmaps_enabled:sampler["mipmap_enabled"].as_bool().unwrap()}};
   assert_eq!(value.validate(&[&data,&capture]).unwrap(),data.as_slice());
   let png=fs::read(row["original_png"].as_str().unwrap()).unwrap();
   let meta=fs::read(row["original_meta"].as_str().unwrap()).unwrap();
   // Full verbatim meta preserves serialized Unity defaults. mip_bias here is
   // the captured effective bias; this test does not overwrite original meta.
   let texture=SourceGraphTexture{png:SourceGraphBlob{buffer_view:2,sha256:bytes_sha256(&png)},source_meta:SourceGraphBlob{buffer_view:3,sha256:bytes_sha256(&meta)},source_meta_sha256:bytes_sha256(&meta),
    is_srgb:sampler["srgb"].as_bool().unwrap(),source_mipmaps:sampler["mipmap_enabled"].as_bool().unwrap(),filter_mode:-1,wrap:[-1;3],anisotropy:-1,mip_bias:sampler["mip_map_bias"].as_f64().unwrap(),imported_storage:Some(value.clone())};
   assert_eq!(validate_texture_integrity(&texture,&[&data,&capture,&png,&meta]),Ok(()));
   let original=String::from_utf8(meta.clone()).unwrap();let guid=row["guid"].as_str().unwrap();
   for altered in [original.replace(guid,"00000000000000000000000000000000"),format!("{}guid: {}\\n",original,guid),original.replace(&format!("guid: {}\\n",guid),"")] {
    let badmeta=altered.as_bytes();let mut changed=texture.clone();changed.source_meta.sha256=bytes_sha256(badmeta);changed.source_meta_sha256=changed.source_meta.sha256.clone();
    assert_eq!(validate_texture_integrity(&changed,&[&data,&capture,&png,badmeta]),Err(SourceGraphContractError::InvalidMetadata));
   }

   let mut changed=value.clone();changed.native_capture.as_mut().unwrap().source_guid="00000000000000000000000000000000".to_string();assert_eq!(changed.validate(&[&data,&capture]),Err(StorageError::Capture));
   changed=value.clone();changed.sampler.filter_mode=(filter+1)%3;assert_eq!(changed.validate(&[&data,&capture]),Err(StorageError::Capture));
   changed=value.clone();changed.schema_version=1;assert_eq!(changed.validate(&[&data,&capture]),Err(StorageError::Schema));
   let mut raw:Value=serde_json::from_slice(&capture).unwrap();let i=value.capture_case_index as usize;
   if value.native_capture.as_ref().unwrap().protocol==NativeCaptureProtocol::GpuMipReadback {raw["rows"][i]["mips"][0]["native_readback_ok"]=json!(false)} else {raw["rows"][i]["settings_restored_equal"]=json!(false)}
   let refused=serde_json::to_vec(&raw).unwrap();value.capture.sha256=bytes_sha256(&refused);assert_eq!(value.validate(&[&data,&refused]),Err(StorageError::Capture));
  }
 }
}
''',encoding='utf-8')
    command=[str(BIN/'rustc.exe'),'--edition=2024','--test',str(wrapper),'-L','dependency='+str(DEPS),'-o',str(ROOT/'validator.exe')]
    core=max(DEPS.glob('libforge_core-*.rlib'),key=lambda p:p.stat().st_mtime)
    fingerprint=DEPS.parent/'.fingerprint'
    coreinfo=json.loads((fingerprint/core.stem.removeprefix('lib')/'lib-forge_core.json').read_text())
    expected={row[1]:row[3] for row in coreinfo['deps']}
    deps={}
    for name in ['forge_core','serde','serde_json']:
        if name=='forge_core':p=core
        else:
            matches=[p for p in DEPS.glob('lib'+name+'-*.rlib') if int.from_bytes(bytes.fromhex((fingerprint/p.stem.removeprefix('lib')/('lib-'+name)).read_text().strip()),'little')==expected[name]]
            assert len(matches)==1,(name,matches)
            p=matches[0]
        deps[str(p)]=sha(p);command+=['--extern',name+'='+str(p)]
    pins={str(p):sha(p) for p in CAND.parent.glob('*.rs')};pins.update({str(wrapper):sha(wrapper),**deps})
    state={'status':'isolated_native_capture_candidate_compiling','input_pins':pins,'command':command,'common_sources_modified':False,'full_import':False,'complete_assets_delivered_increment':0}
    env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT))
    with (ROOT/'compile.stdout.log').open('wb') as out,(ROOT/'compile.stderr.log').open('wb') as err:
        run=subprocess.run(command,env=env,stdout=out,stderr=err);state['compile_exit_code']=run.returncode
    if run.returncode==0:
        with (ROOT/'tests.stdout.log').open('wb') as out,(ROOT/'tests.stderr.log').open('wb') as err:
            run=subprocess.run([str(ROOT/'validator.exe'),'--nocapture'],env=env,stdout=out,stderr=err);state['test_exit_code']=run.returncode
        state['status']='isolated_native_capture_candidate_tests_passed' if run.returncode==0 else 'isolated_native_capture_candidate_tests_refused'
    else:state['status']='isolated_native_capture_candidate_compile_refused'
    state['finished_utc']=datetime.now(timezone.utc).isoformat()
    for p,h in pins.items():assert sha(p)==h,p
    (ROOT/'RESULT.v1.json').write_text(json.dumps(state,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in state.items() if k not in ['input_pins','command']}))
if __name__=='__main__':main()

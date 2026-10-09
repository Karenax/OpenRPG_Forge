"""Prepare the real common parent module, retaining all legacy checks and fixtures."""
from pathlib import Path
from datetime import datetime,timezone
import shutil,json,hashlib,subprocess
R=Path(__file__).parents[2];S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');ROOT=W/'native-texture-identity-contract-candidate-v1'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    ROOT.mkdir(exist_ok=False)
    pins={};source=R/'crates/forge_assets/src/source_graph_bundle.rs';text=source.read_text();pins[str(source)]=sha(source)
    copied=[(W/'native-texture-capture-contract-candidate-v1/imported_storage.candidate.rs','imported_storage.candidate.rs'),
            (R/'crates/forge_assets/src/source_graph_bundle/scalar_parameters.rs','scalar_parameters.rs'),
            (R/'crates/forge_assets/src/source_material_parameters.rs','source_material_parameters.rs')]
    for p,name in copied:shutil.copy2(p,ROOT/name);pins[str(p)]=sha(p)
    text=text.replace('source_graph_bundle/imported_storage.rs','imported_storage.candidate.rs').replace('source_graph_bundle/scalar_parameters.rs','scalar_parameters.rs')
    text=text.replace('    ImportedStorageFormat as SourceGraphImportedStorageFormat,','    NativeCaptureIdentity, NativeCaptureProtocol,\n    ImportedStorageFormat as SourceGraphImportedStorageFormat,',1)
    anchor='    if let Some(storage) = &texture.imported_storage {\n';assert text.count(anchor)==1
    block='''        if let Some(expected_guid) = storage.source_guid() {
            // A native row may not be attached to a different purchased texture,
            // even when an attacker recomputes the outer metadata hashes.
            let mut guids = meta.lines().filter_map(|line| line.strip_prefix("guid: "));
            if guids.next() != Some(expected_guid) || guids.next().is_some() {
                return Err(SourceGraphContractError::InvalidMetadata);
            }
        }
'''
    text=text.replace(anchor,anchor+block,1)
    target=ROOT/'source_graph_bundle.candidate.rs';target.write_text(text,encoding='utf-8')
    subprocess.run(['C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe','--edition','2024',str(target)],check=True)
    result={'status':'common_parent_native_identity_candidate_prepared_not_installed','prepared_utc':datetime.now(timezone.utc).isoformat(),
            'input_pins':pins,'candidate_pins':{str(p):sha(p) for p in ROOT.glob('*.rs')},'common_sources_modified':False,'full_import':False,'complete_assets_delivered_increment':0}
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    # Derive a real-parent test from the previous isolated harness. Keep prior runs immutable.
    p=S/'check_native_capture_candidate_v2.py';t=p.read_text()
    t=t.replace('native-texture-capture-contract-check-v2','native-texture-identity-contract-check-v1')
    t=t.replace("CAND=W/'native-texture-capture-contract-candidate-v1/imported_storage.candidate.rs'","CAND=W/'native-texture-identity-contract-candidate-v1/source_graph_bundle.candidate.rs'")
    start=t.index("    wrapper.write_text('''mod source_graph_bundle {")
    end=t.index('#[cfg(test)] mod native_resources {',start)
    t=t[:start]+'''    wrapper.write_text('''+"'''"+'''#[path = "../native-texture-identity-contract-candidate-v1/source_material_parameters.rs"]
mod source_material_parameters;
#[path = "../native-texture-identity-contract-candidate-v1/source_graph_bundle.candidate.rs"]
mod source_graph_bundle;
'''+t[end:]
    t=t.replace(' use super::imported_storage::*;', ''' use crate::source_graph_bundle::{SourceGraphImportedTextureStorage as ImportedTextureStorage,
 SourceGraphImportedStorageMip as ImportedStorageMip, SourceGraphResolvedStorageSampler as ResolvedStorageSampler,
 SourceGraphImportedStorageError as StorageError, NativeCaptureIdentity, NativeCaptureProtocol,
 SourceGraphTexture, SourceGraphContractError, validate_texture_integrity};''')
    anchor='   assert_eq!(value.validate(&[&data,&capture]).unwrap(),data.as_slice());'
    assert t.count(anchor)==1
    added='''
   let png=fs::read(row["original_png"].as_str().unwrap()).unwrap();
   let meta=fs::read(row["original_meta"].as_str().unwrap()).unwrap();
   // Full verbatim meta preserves serialized Unity defaults. mip_bias here is
   // the captured effective bias; this test does not overwrite original meta.
   let texture=SourceGraphTexture{png:SourceGraphBlob{buffer_view:2,sha256:bytes_sha256(&png)},source_meta:SourceGraphBlob{buffer_view:3,sha256:bytes_sha256(&meta)},source_meta_sha256:bytes_sha256(&meta),
    is_srgb:sampler["srgb"].as_bool().unwrap(),source_mipmaps:sampler["mipmap_enabled"].as_bool().unwrap(),filter_mode:-1,wrap:[-1;3],anisotropy:-1,mip_bias:sampler["mip_map_bias"].as_f64().unwrap(),imported_storage:Some(value.clone())};
   assert_eq!(validate_texture_integrity(&texture,&[&data,&capture,&png,&meta]),Ok(()));
   let original=String::from_utf8(meta.clone()).unwrap();let guid=row["guid"].as_str().unwrap();
   for altered in [original.replace(guid,"00000000000000000000000000000000"),format!("{}guid: {}\\\\n",original,guid),original.replace(&format!("guid: {}\\\\n",guid),"")] {
    let badmeta=altered.as_bytes();let mut changed=texture.clone();changed.source_meta.sha256=bytes_sha256(badmeta);changed.source_meta_sha256=changed.source_meta.sha256.clone();
    assert_eq!(validate_texture_integrity(&changed,&[&data,&capture,&png,badmeta]),Err(SourceGraphContractError::InvalidMetadata));
   }
'''
    t=t.replace(anchor,anchor+added)
    # Pin every actual common candidate source compiled by this harness.
    t=t.replace("pins={str(CAND):sha(CAND),str(wrapper):sha(wrapper),**deps}","pins={str(p):sha(p) for p in CAND.parent.glob('*.rs')};pins.update({str(wrapper):sha(wrapper),**deps})")
    (S/'check_native_identity_contract_v1.py').write_text(t,encoding='utf-8')
    print(json.dumps({'status':result['status'],'candidate':str(target)}))
if __name__=='__main__':main()

"""Decouple per-texture integrity from batch count/status; retain failed captures."""
from pathlib import Path
from datetime import datetime,timezone
import shutil,json,hashlib,subprocess
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');PREV=W/'native-texture-identity-contract-candidate-v1';ROOT=W/'native-texture-identity-contract-candidate-v2'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    prep=json.loads((PREV/'PREPARED.v1.json').read_text())
    for p,h in prep['candidate_pins'].items():assert sha(p)==h,p
    ROOT.mkdir(exist_ok=False)
    for p in PREV.glob('*.rs'):shutil.copy2(p,ROOT/p.name)
    p=ROOT/'imported_storage.candidate.rs';s=p.read_text()
    start=s.index('        let expected_status = match identity.protocol {');end=s.index('        let row = capture["rows"]',start)
    s=s[:start]+'''        // Batch status is retained verbatim as diagnostic evidence. It may report
        // failures in other rows; storage integrity is determined by this row's
        // explicit protocol, identity, restoration/readback flags and mip chain.
        // This does not qualify capture execution or renderer equivalence.
'''+s[end:];p.write_text(s,encoding='utf-8')
    subprocess.run(['C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe','--edition','2024',str(p)],check=True)
    d={'status':'generic_per_resource_native_capture_candidate_prepared_not_installed','prepared_utc':datetime.now(timezone.utc).isoformat(),
        'input_pins':{str(p):sha(p) for p in PREV.glob('*.rs')},'candidate_pins':{str(p):sha(p) for p in ROOT.glob('*.rs')},
        'common_sources_modified':False,'full_import':False,'complete_assets_delivered_increment':0}
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(d,indent=2),encoding='utf-8')
    t=(S/'check_native_identity_contract_v1.py').read_text().replace('native-texture-identity-contract-check-v1','native-texture-identity-contract-check-v2').replace('native-texture-identity-contract-candidate-v1','native-texture-identity-contract-candidate-v2')
    anchor='   assert_eq!(value.validate(&[&data,&capture]).unwrap(),data.as_slice());';assert t.count(anchor)==1
    add='''
   // Same resource in a partial batch of one: no asset name or batch size decides admission.
   let full:Value=serde_json::from_slice(&capture).unwrap();
   let mut partial=json!({"status":"partial_native_capture","rows":[full["rows"][value.capture_case_index as usize].clone()]});
   let partialbytes=serde_json::to_vec(&partial).unwrap();let mut one=value.clone();one.capture_case_index=0;one.capture.sha256=bytes_sha256(&partialbytes);
   assert_eq!(one.validate(&[&data,&partialbytes]).unwrap(),data.as_slice());
   partial["rows"][0]["error"]=json!("native resource capture failed");
   let failedbytes=serde_json::to_vec(&partial).unwrap();one.capture.sha256=bytes_sha256(&failedbytes);
   assert_eq!(one.validate(&[&data,&failedbytes]),Err(StorageError::Capture));
'''
    t=t.replace(anchor,anchor+add)
    anchor=' #[test] fn ten_actual_resource_chains_and_identity_sampler_refusals() {';assert t.count(anchor)==1
    add=''' #[test] fn unknown_native_capture_protocol_and_version_refuse() {
  assert!(serde_json::from_value::<NativeCaptureIdentity>(json!({"protocol":"unity_isolated_readable_native_mips.v2","source_guid":"00000000000000000000000000000000"})).is_err());
  assert!(serde_json::from_value::<NativeCaptureIdentity>(json!({"protocol":"inferred_png","source_guid":"00000000000000000000000000000000"})).is_err());
 }
'''
    t=t.replace(anchor,add+anchor);(S/'check_native_identity_contract_v2.py').write_text(t,encoding='utf-8')
    print(json.dumps({'status':d['status'],'root':str(ROOT)}))
if __name__=='__main__':main()

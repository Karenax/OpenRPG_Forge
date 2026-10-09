from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');PREV=W/'native-texture-identity-contract-candidate-v2';ROOT=W/'native-texture-identity-contract-candidate-v3'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
 prep=json.loads((PREV/'PREPARED.v1.json').read_text(encoding='utf-8'))
 for p,h in prep['candidate_pins'].items():assert sha(p)==h,p
 ROOT.mkdir(exist_ok=False)
 for p in PREV.glob('*.rs'):shutil.copy2(p,ROOT/p.name)
 p=ROOT/'imported_storage.candidate.rs'
 extra='''
#[cfg(test)]
mod native_protocol_tests {
 use super::*;
 fn fixture(protocol:NativeCaptureProtocol)->(Vec<u8>,Vec<u8>,ImportedTextureStorage){
  let (data,_,mut value)=super::tests::fixture();
  let guid="0123456789abcdef0123456789abcdef";
  value.schema_version=2;value.native_capture=Some(NativeCaptureIdentity{protocol,source_guid:guid.into()});
  let settings=serde_json::json!({"m_IsReadable":0,"mipmaps":{"m_EnableMipMap":1,"m_sRGBTexture":0}}).to_string();
  let mips:Vec<_>=value.mips.iter().enumerate().map(|(i,m)|serde_json::json!({"level":i,"width":m.width,"height":m.height,"bytes":m.bytes,"native_readback_ok":true,"error":""})).collect();
  let mut row=serde_json::json!({"guid":guid,"width":4,"height":4,"graphics_format":"R_BC4_UNorm","error":"","mips":mips});
  let fields=match protocol {
   NativeCaptureProtocol::IsolatedReadableMips=>serde_json::json!({"settings_before":settings,"settings_restored":settings,"settings_restored_equal":true,"gpu_samples_bit_exact":true,"private_readability_override":true,"mip_count":3,"whole_bytes":24,"filter_mode":1,"wrap_u":1,"wrap_v":1,"wrap_w":1,"aniso":1,"mip_bias":0.0}),
   NativeCaptureProtocol::GpuMipReadback=>serde_json::json!({"mipmap_count":3,"srgb":false,"mipmap_enabled":true,"filter_mode":"Bilinear","wrap_u":"Clamp","wrap_v":"Clamp","wrap_w":"Clamp","aniso_level":1,"mip_map_bias":0.0}),
  };
  row.as_object_mut().unwrap().extend(fields.as_object().unwrap().clone());
  let capture=serde_json::to_vec(&serde_json::json!({"status":"other_rows_refused","rows":[row]})).unwrap();value.capture.sha256=bytes_sha256(&capture);
  (data,capture,value)
 }
 #[test] fn complete_native_rows_survive_batch_refusals_for_both_protocols(){
  for protocol in [NativeCaptureProtocol::IsolatedReadableMips,NativeCaptureProtocol::GpuMipReadback] {
   let (data,capture,value)=fixture(protocol);assert_eq!(value.validate(&[&data,&capture]).unwrap(),data);
   let wire=serde_json::to_vec(&value).unwrap();let reopened:ImportedTextureStorage=serde_json::from_slice(&wire).unwrap();assert_eq!(reopened,value);
  }
 }
 #[test] fn native_row_refusals_remain_refusals_after_outer_rehash(){
  for protocol in [NativeCaptureProtocol::IsolatedReadableMips,NativeCaptureProtocol::GpuMipReadback] {
   let(data,capture,mut value)=fixture(protocol);let mut raw:serde_json::Value=serde_json::from_slice(&capture).unwrap();
   match protocol {NativeCaptureProtocol::IsolatedReadableMips=>raw["rows"][0]["settings_restored_equal"]=serde_json::json!(false),NativeCaptureProtocol::GpuMipReadback=>raw["rows"][0]["mips"][1]["native_readback_ok"]=serde_json::json!(false)};
   let refused=serde_json::to_vec(&raw).unwrap();value.capture.sha256=bytes_sha256(&refused);assert_eq!(value.validate(&[&data,&refused]),Err(StorageError::Capture));
  }
 }
 #[test] fn legacy_native_protocol_mixing_and_missing_identity_refuse(){
  let(data,capture,mut value)=fixture(NativeCaptureProtocol::GpuMipReadback);value.schema_version=1;assert_eq!(value.validate(&[&data,&capture]),Err(StorageError::Schema));
  value.schema_version=2;value.native_capture=None;assert_eq!(value.validate(&[&data,&capture]),Err(StorageError::Schema));
 }
}
'''
 p.write_text(p.read_text(encoding='utf-8')+extra,encoding='utf-8')
 d={'status':'native_capture_common_regressions_prepared','prepared_utc':datetime.now(timezone.utc).isoformat(),'input_pins':{str(p):sha(p) for p in PREV.glob('*.rs')},'candidate_pins':{str(p):sha(p) for p in ROOT.glob('*.rs')},'common_sources_modified':False,'full_import':False}
 (ROOT/'PREPARED.v1.json').write_text(json.dumps(d,indent=2),encoding='utf-8')
 t=(S/'check_native_identity_contract_v2.py').read_text(encoding='utf-8').replace('native-texture-identity-contract-check-v2','native-texture-identity-contract-check-v3').replace('native-texture-identity-contract-candidate-v2','native-texture-identity-contract-candidate-v3')
 (S/'check_native_identity_contract_v3.py').write_text(t,encoding='utf-8')
 print(d['status'])
if __name__=='__main__':main()

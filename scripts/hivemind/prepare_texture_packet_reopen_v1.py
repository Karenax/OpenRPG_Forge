from pathlib import Path
S=Path(__file__).parent
t=(S/'check_native_identity_contract_v3.py').read_text(encoding='utf-8').replace('native-texture-identity-contract-check-v3','townsmith-native-texture-contract-packets-reopen-v1')
anchor=' #[test] fn ten_actual_resource_chains_and_identity_sampler_refusals() {'
assert t.count(anchor)==1
extra=''' #[test] fn ten_serialized_texture_packets_reopen_as_real_common_types(){
  let packet:Value=serde_json::from_slice(&fs::read("F:/OpenRPG_ImportWork/townsmith-native-texture-contract-packets-v1/TEXTURE-CONTRACTS.v1.json").unwrap()).unwrap();
  assert_eq!(packet["rows"].as_array().unwrap().len(),10);
  for row in packet["rows"].as_array().unwrap(){
   let texture:SourceGraphTexture=serde_json::from_value(row["texture"].clone()).unwrap();
   let buffers:Vec<Vec<u8>>=row["local_buffer_views"].as_array().unwrap().iter().map(|view|{
    let bytes=fs::read(view["file"].as_str().unwrap()).unwrap();assert_eq!(bytes.len() as u64,view["bytes"].as_u64().unwrap());assert_eq!(serde_json::to_value(bytes_sha256(&bytes)).unwrap(),view["sha256"]);bytes
   }).collect();
   let views:Vec<&[u8]>=buffers.iter().map(|v|v.as_slice()).collect();assert_eq!(validate_texture_integrity(&texture,&views),Ok(()));
   let imported=texture.imported_storage.as_ref().unwrap();assert_eq!(imported.schema_version,2);assert_eq!(imported.source_guid(),row["guid"].as_str());
  }
 }
'''
t=t.replace(anchor,extra+anchor)
t=t.replace("pins={str(p):sha(p) for p in CAND.parent.glob('*.rs')};pins.update({str(wrapper):sha(wrapper),**deps})", "pins={str(p):sha(p) for p in CAND.parent.glob('*.rs')};pins.update({str(wrapper):sha(wrapper),**deps});packet=W/'townsmith-native-texture-contract-packets-v1/TEXTURE-CONTRACTS.v1.json';pins[str(packet)]=sha(packet)")
(S/'check_texture_packet_reopen_v1.py').write_text(t,encoding='utf-8')

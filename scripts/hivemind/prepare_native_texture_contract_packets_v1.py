"""Export real typed texture packets for material assembly, never placeholder GLBs."""
from pathlib import Path
S=Path(__file__).parent
def main():
 t=(S/'check_native_identity_contract_v3.py').read_text(encoding='utf-8')
 t=t.replace('native-texture-identity-contract-check-v3','townsmith-native-texture-contract-packets-v1')
 anchor='  for row in manifest["rows"].as_array().unwrap() {';assert t.count(anchor)==1
 t=t.replace(anchor,'  let mut exported=Vec::new();\n'+anchor)
 anchor='   assert_eq!(validate_texture_integrity(&texture,&[&data,&capture,&png,&meta]),Ok(()));';assert t.count(anchor)==1
 extra='''
   exported.push(json!({"guid":row["guid"],"authored_texture_index":row["authored_texture_index"],"normal_profile_index":row["normal_decoder_profile_index"],"texture":serde_json::to_value(&texture).unwrap(),
    "local_buffer_views":[{"file":row["native_storage"],"sha256":bytes_sha256(&data),"bytes":data.len()},{"file":row["storage_capture"],"sha256":bytes_sha256(&capture),"bytes":capture.len()},{"file":row["original_png"],"sha256":bytes_sha256(&png),"bytes":png.len()},{"file":row["original_meta"],"sha256":bytes_sha256(&meta),"bytes":meta.len()}],
    "sampler_provenance":"Verbatim original meta retains serialized defaults; packet mip_bias is the measured effective native bias","full_import":false}));
'''
 t=t.replace(anchor,anchor+extra)
 anchor='  }\n }\n}\n\'\'\',encoding=\'utf-8\')';assert t.count(anchor)==1
 extra='''  }
  assert_eq!(exported.len(),10);
  fs::write("F:/OpenRPG_ImportWork/townsmith-native-texture-contract-packets-v1/TEXTURE-CONTRACTS.v1.json",serde_json::to_vec_pretty(&json!({"status":"ten_real_typed_source_texture_packets_exported","rows":exported,"renderer_material_admission_verified":false,"full_import":false,"complete_assets_delivered_increment":0})).unwrap()).unwrap();
 }
}
'''
 t=t.replace(anchor,extra+"''',encoding='utf-8')")
 (S/'check_native_texture_contract_packets_v1.py').write_text(t,encoding='utf-8')
 print('Typed resource export prepared from the validated common modules')
if __name__=='__main__':main()

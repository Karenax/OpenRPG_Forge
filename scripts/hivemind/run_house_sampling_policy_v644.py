"""Capture native QualitySettings API, independently of config inference."""
from pathlib import Path
recipe=Path(__file__).with_name('run_house_compressed_storage_v641.py')
code=recipe.read_text('utf-8-sig').replace('house-compressed-storage-v641','house-native-sampling-policy-v644').replace('HouseCompressedStorageV641','HouseSamplingPolicyV644')
code=code.replace("ap=W/'house-dxc-surface-reference-v638/HELPER-OWNERSHIP.v1.json'","ap=W/'house-compressed-storage-v641/HELPER-OWNERSHIP.v1.json'").replace("len(audit['helpers'])==30","len(audit['helpers'])==31")
old="assert not state['private_importer_changes'] and len(native['textures'])==18 and all(t['native_decoded_mips_equal'] and t['restored_decoded_mips_equal'] and t['importer_restored_equal'] and t['meta_bytes_restored_equal'] and len(t['levels'])==12 for t in native['textures'])"
new="assert not state['private_importer_changes'] and len(native['textures'])==18 and native['quality_anisotropy']=='ForceEnable' and native['global_mipmap_limit']==0 and all(t['source_texture_settings_unchanged'] and t['mip_bias']==0 and t['aniso']==1 and t['filter']=='Bilinear' and t['mips']==12 for t in native['textures'])"
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace('private_readability_temporary=True,meta_bytes_restoration_required=True,all_decoded_mips_before_readable_after_readable_after_restoration_required=True','read_only_native_sampling_policy=True')
code=code.replace('house_compressed_storage_prepared','house_native_sampling_policy_prepared').replace('house_compressed_storage_running','house_native_sampling_policy_running').replace('house_compressed_storage_captured_review_pending','house_native_sampling_policy_captured_review_pending').replace('house_compressed_storage_refusal_retained','house_native_sampling_policy_refusal_retained')
code=code.replace('for p in [Path(__file__),cp,ap,','for p in [Path(__file__),recipe,cp,ap,')
exec(compile(code,str(recipe)+'#native-quality-api','exec'),globals())

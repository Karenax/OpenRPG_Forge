"""Independently reopen the ten assembled resources, without renderer qualification."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'townsmith-s-paint-native-resources-reopen-v1'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def main():
    manifest=W/'townsmith-s-paint-native-resources-v1/RESOURCES.v1.json'
    d=read(manifest);pins={str(manifest):sha(manifest),str(Path(__file__)):sha(__file__)}
    for p,h in d['input_pins'].items():assert sha(p)==h,p
    assert d['resources_composed']==10 and len(d['rows'])==10
    bindings=[(101,102),(103,104),(105,106),(107,108),(109,110),(111,112),(113,114),(115,116),(117,118),(125,126)]
    formats={'bc1_rgba_unorm_srgb':(4,8),'bc3_rgba_unorm_srgb':(4,16),'bc3_rgba_unorm':(4,16),'rgba8_unorm':(1,4)}
    total=mips=0;guids=set()
    for i,r in enumerate(d['rows']):
        assert r['authored_texture_index']==i and (r['texture_binding'],r['sampler_binding'])==bindings[i]
        assert r['guid'] not in guids;guids.add(r['guid'])
        for k,h in [('native_storage','storage_sha256'),('original_png','source_sha256'),('original_meta','source_meta_sha256')]:
            assert sha(r[k])==r[h],r[k];pins[r[k]]=r[h]
        assert 'guid: '+r['guid'] in Path(r['original_meta']).read_text(encoding='utf-8-sig')
        profile=read(r['source_profile_capture'])['rows'][r['source_profile_capture_case_index']]
        capture=read(r['storage_capture'])['rows'][r['storage_capture_case_index']]
        assert profile['guid']==capture['guid']==r['guid']
        assert profile['graphics_format']==capture['graphics_format']==r['native_graphics_format']
        assert (profile['width'],profile['height'])==(r['width'],r['height'])
        assert r['sampler']=={k:profile[k] for k in r['sampler']}
        if r['storage_capture_protocol']=='unity_isolated_readable_native_mips.v1':
            assert capture['settings_restored_equal'] and capture['gpu_samples_bit_exact'] and not capture.get('error')
            assert capture['settings_before']==capture['settings_restored']
        else:
            assert r['storage_capture_protocol']=='unity_native_gpu_mip_readback.v1'
            assert all(m['native_readback_ok'] for m in capture['mips'])
        blob=Path(r['native_storage']).read_bytes();offset=0;block,bbytes=formats[r['encoding']]
        assert len(r['mips'])==len(capture['mips'])==12
        for level,(m,c) in enumerate(zip(r['mips'],capture['mips'])):
            width=max(1,r['width']>>level);height=max(1,r['height']>>level)
            size=((width+block-1)//block)*((height+block-1)//block)*bbytes
            assert (m['level'],m['width'],m['height'],m['offset'],m['bytes'])==(level,width,height,offset,size)
            part=blob[offset:offset+size];assert hashlib.sha256(part).hexdigest()==m['sha256']
            assert part==Path(c['file']).read_bytes();offset+=size;mips+=1
        assert offset==len(blob)==r['storage_bytes'];total+=offset
        assert not r['normal_decoder_qualified']
    assert (mips,total)==(120,78293832)
    result={'status':'ten_native_resources_independently_reopened_120_mips_exact','finished_utc':datetime.now(timezone.utc).isoformat(),
        'resources_verified':10,'native_mips_verified':mips,'native_bytes_verified':total,'original_meta_guids_verified':10,
        'input_pins':pins,'renderer_material_contract_admission_verified':False,'normal_decoder_gpu_equivalence_qualified':False,
        'forge_texture_upload_qualified':False,'forge_editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0}
    ROOT.mkdir(exist_ok=False);(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()

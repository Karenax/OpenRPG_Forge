from pathlib import Path
from datetime import datetime, timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-native-textures-v1';OUT=W/'townsmith-s-paint-native-textures-reopen-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not OUT.exists()
run=read(ROOT/'RUN-RESULT.v1.json');native=read(ROOT/'NATIVE-RESULT.v1.json');prep=read(ROOT/'PREPARED.v1.json')
assert run['actual_exit_code']==0 and run['source_inputs_unchanged']
assert sha(ROOT/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
pins={**prep['input_pins'],**prep['private_before_pins'],**run['native_mip_pins']}
for p,h in pins.items():assert sha(p)==h,p
inventory_path=W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json';inventory=read(inventory_path)
lookup={r['guid']:r for r in inventory['rows']}
assert len(native['rows'])==len(lookup)==10
captured=[];refused=[];byte_count=0
for r in native['rows']:
    original=lookup[r['guid']]
    project=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
    # Unity canonicalizes HIVEMIND's casing; require the same Windows file identity.
    assert (project/r['asset_path']).samefile(project/original['source_path'])
    assert sha(project/r['asset_path'])==original['source_sha256']
    assert r['authored_texture_index']==original['authored_texture_index']
    assert (r['width'],r['height'])==(original['png_width'],original['png_height'])==(2048,2048)
    assert r['mipmap_count']==len(r['mips'])==12 and not r['source_readable']
    if original['normal_profile_index'] is not None:
        assert r['texture_type']=='NormalMap' and r['graphics_format']=='RGBA_DXT5_UNorm' and not r['srgb']
    successes=[m for m in r['mips'] if m['native_readback_ok']]
    if successes:
        assert len(successes)==12 and r['graphics_format']=='R8G8B8A8_UNorm'
        for level,m in enumerate(r['mips']):
            assert m['level']==level and (m['width'],m['height'])==(max(1,2048>>level),max(1,2048>>level))
            expected=m['width']*m['height']*4
            assert m['bytes']==Path(m['file']).stat().st_size==expected
            assert sha(m['file'])==run['native_mip_pins'][m['file']]
            byte_count+=expected
        captured.append(r['guid'])
    else:
        assert r['graphics_format'] in ['RGBA_DXT1_SRGB','RGBA_DXT5_SRGB','RGBA_DXT5_UNorm']
        assert all('Native-format mip readback refused' in m['error'] for m in r['mips'])
        refused.append(r['guid'])
assert len(captured)==2 and len(refused)==8 and not native['native_readback_complete']
log=(ROOT/'Unity.log').read_text(encoding='utf-8-sig')
assert 'is a compressed format which is not supported by async read back' in log
for p in [ROOT/'RUN-RESULT.v1.json',ROOT/'NATIVE-RESULT.v1.json',ROOT/'PREPARED.v1.json',ROOT/'Unity.log',inventory_path,Path(__file__)]:pins[str(p)]=sha(p)
OUT.mkdir()
result={'status':'ten_native_profiles_reopened_two_unorm_mip_chains_verified_eight_compressed_readback_refusals_retained',
 'sub_lot':'L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.NATIVE',
 'finished_utc':datetime.now(timezone.utc).isoformat(),'native_profiles_verified':10,
 'complete_native_unorm_texture_chains':2,'native_unorm_mips_verified':24,'native_unorm_bytes_verified':byte_count,
 'compressed_textures_refused':8,'compressed_mips_refused':96,'captured_texture_guids':captured,'refused_texture_guids':refused,
 'original_and_private_sources_unchanged':True,'texture_normal_decoder_gpu_equivalence_qualified':False,
 'forge_texture_upload_qualified':False,'full_import':False,'complete_assets_delivered_increment':0,
 'next_action':'Use the existing isolated readable-import extraction recipe for compressed storage, explicitly preserve original settings and verify reimport storage and sampling; never replace original compressed textures by recompressed PNG defaults',
 'input_pins':pins}
(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','captured_texture_guids','refused_texture_guids']}))

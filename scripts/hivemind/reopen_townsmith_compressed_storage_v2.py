from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct,math
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-compressed-storage-v2';OUT=W/'townsmith-s-paint-compressed-storage-reopen-v2'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not OUT.exists()
 run=read(ROOT/'RUN-RESULT.v1.json');native=read(ROOT/'NATIVE-RESULT.v1.json');prep=read(ROOT/'PREPARED.v1.json')
 assert run['actual_exit_code']==0 and run['originals_unchanged']
 assert native['status']=='eight_compressed_native_mip_chains_extracted_in_isolated_readable_copy'
 assert sha(ROOT/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
 pins={**prep['input_pins'],**run['artifact_pins']}
 for p,h in pins.items():assert sha(p)==h,p
 original_profiles=read(W/'townsmith-s-paint-native-textures-v1/NATIVE-RESULT.v1.json')
 expected={r['guid']:r for r in original_profiles['rows'] if r['format'] in ['DXT1','DXT5']}
 assert len(native['rows'])==len(expected)==8
 formats=[];mip_count=0;byte_count=0;sample_count=0
 filter_modes={'Point':0,'Bilinear':1,'Trilinear':2};wrap_modes={'Repeat':0,'Clamp':1,'Mirror':2,'MirrorOnce':3}
 for row in native['rows']:
  original=expected[row['guid']]
  assert not row.get('error') and row['private_readability_override'] and row['settings_restored_equal'] and row['gpu_samples_bit_exact']
  assert row['settings_before']==row['settings_restored']
  for key in ['guid','authored_texture_index','width','height','format','graphics_format']:
   assert row[key]==original[key],(row['guid'],key)
  assert row['mip_count']==original['mipmap_count']==len(row['mips'])==12
  assert row['filter_mode']==filter_modes[original['filter_mode']]
  assert row['aniso']==original['aniso_level'] and row['mip_bias']==original['mip_map_bias']
  for key in ['wrap_u','wrap_v','wrap_w']:assert row[key]==wrap_modes[original[key]]
  source=Path(row['source_samples']).read_bytes();readable=Path(row['readable_samples']).read_bytes()
  assert row['sample_count']==108 and source==readable and len(source)==108*16
  values=[f[0] for f in struct.iter_unpack('<f',source)]
  assert all(math.isfinite(v) and 0.0<=v<=1.0 for v in values)
  assert any(v!=0.0 for v in values), 'Empty GPU probe cannot qualify sampling'
  sample_count+=row['sample_count']
  chain=[]
  for level,mip in enumerate(row['mips']):
   w=max(1,2048>>level);h=max(1,2048>>level);block_bytes=8 if row['format']=='DXT1' else 16
   size=max(1,(w+3)//4)*max(1,(h+3)//4)*block_bytes
   assert mip['level']==level and (mip['width'],mip['height'])==(w,h)
   data=Path(mip['file']).read_bytes();assert len(data)==mip['bytes']==size
   chain.append(data);mip_count+=1;byte_count+=size
  assert b''.join(chain)==Path(row['whole_file']).read_bytes()
  assert sum(len(c) for c in chain)==row['whole_bytes']
  formats.append({'guid':row['guid'],'authored_texture_index':row['authored_texture_index'],'graphics_format':row['graphics_format'],'mip_count':12,'bytes':row['whole_bytes'],'probe_samples_bit_exact':108,'source_settings_restored':True})
 assert mip_count==96 and sample_count==864
 original_profile_path=W/'townsmith-s-paint-native-textures-v1/NATIVE-RESULT.v1.json'
 for p in [ROOT/'RUN-RESULT.v1.json',ROOT/'NATIVE-RESULT.v1.json',ROOT/'PREPARED.v1.json',original_profile_path,Path(__file__)]:pins[str(p)]=sha(p)
 OUT.mkdir()
 result={'status':'eight_compressed_native_mip_chains_reopened_864_readability_probe_samples_bit_exact',
  'sub_lot':'L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.BC_STORAGE','finished_utc':datetime.now(timezone.utc).isoformat(),
  'compressed_textures_verified':8,'compressed_mips_verified':96,'compressed_bytes_verified':byte_count,
  'source_to_readable_probe_samples_verified':864,'private_importer_settings_restored':8,
  'rows':formats,'input_pins':pins,'original_and_existing_private_sources_unchanged':True,
  'source_full_texture_sampling_fidelity_qualified':False,'normal_decoder_gpu_equivalence_qualified':False,
  'forge_texture_upload_qualified':False,'full_import':False,'complete_assets_delivered_increment':0,
  'next_action':'Compose all ten imported texture resources preserving compressed bytes, original mip order, sRGB and sampler profiles; adapt the generic material contract and compare native source and Forge sampling before scene qualification'}
 (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps({k:v for k,v in result.items() if k not in ['rows','input_pins']}))
if __name__=='__main__':main()

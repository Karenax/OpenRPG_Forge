"""Seal all ten source resources; this is not material or renderer admission."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,shutil,re
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-native-resources-v1';R=Path(__file__).parents[2]
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def pin(p,pins):pins[str(p)]=sha(p)
def verified(p,pins):
 d=read(p)
 for path,h in d['input_pins'].items():assert sha(path)==h,path
 pin(p,pins);return d
def main():
 assert not ROOT.exists()
 pins={};inventory=verified(W/'townsmith-s-paint-texture-inventory-v1/RESULT.v1.json',pins)
 unorm=verified(W/'townsmith-s-paint-native-textures-reopen-v1/RESULT.v1.json',pins)
 compressed=verified(W/'townsmith-s-paint-compressed-storage-reopen-v2/RESULT.v1.json',pins)
 assert unorm['native_unorm_mips_verified']==24 and compressed['compressed_mips_verified']==96
 native_path=W/'townsmith-s-paint-native-textures-v1/NATIVE-RESULT.v1.json'
 compressed_path=W/'townsmith-s-paint-compressed-storage-v2/NATIVE-RESULT.v1.json'
 native=read(native_path);bc=read(compressed_path);pin(native_path,pins);pin(compressed_path,pins)
 assert bc['status']=='eight_compressed_native_mip_chains_extracted_in_isolated_readable_copy'
 native_lookup={r['guid']:(i,r) for i,r in enumerate(native['rows'])}
 compressed_lookup={r['guid']:(i,r) for i,r in enumerate(bc['rows'])}
 binding_path=R/'crates/forge_assets/src/source_graph_bundle.rs';source=binding_path.read_text()
 table=source.split('pub const SOURCE_GRAPH_TEXTURE_BINDINGS:',1)[1].split('];',1)[0]
 bindings=[tuple(map(int,m)) for m in re.findall(r'\((\d+),\s*(\d+)\)',table)]
 assert len(bindings)==10 and bindings[-1]==(125,126)
 assert sorted(r['authored_texture_index'] for r in inventory['rows'])==list(range(10))
 pin(binding_path,pins);pin(Path(__file__),pins)
 format_map={'RGBA_DXT1_SRGB':('bc1_rgba_unorm_srgb',4,8),'RGBA_DXT5_SRGB':('bc3_rgba_unorm_srgb',4,16),'RGBA_DXT5_UNorm':('bc3_rgba_unorm',4,16),'R8G8B8A8_UNorm':('rgba8_unorm',1,4)}
 rows=[];total=0
 # Validate everything before producing a resource directory.
 for original in sorted(inventory['rows'],key=lambda r:r['authored_texture_index']):
  guid=original['guid'];slot=original['authored_texture_index'];profile_index,profile=native_lookup[guid]
  if guid in compressed_lookup:
   case_index,row=compressed_lookup[guid];capture_path=compressed_path;protocol='unity_isolated_readable_native_mips.v1'
   assert row['settings_restored_equal'] and row['gpu_samples_bit_exact'] and not row.get('error')
  else:
   case_index,row=profile_index,profile;capture_path=native_path;protocol='unity_native_gpu_mip_readback.v1'
   assert guid in unorm['captured_texture_guids'] and all(m['native_readback_ok'] for m in row['mips'])
  encoding,block,block_bytes=format_map[profile['graphics_format']]
  chain=[];mips=[];offset=0
  for level,mip in enumerate(row['mips']):
   w=max(1,profile['width']>>level);h=max(1,profile['height']>>level)
   data=Path(mip['file']).read_bytes();expected=((w+block-1)//block)*((h+block-1)//block)*block_bytes
   assert len(data)==mip['bytes']==expected and (mip['width'],mip['height'])==(w,h)
   mips.append({'level':level,'width':w,'height':h,'offset':offset,'bytes':expected,'sha256':hashlib.sha256(data).hexdigest()})
   chain.append(data);offset+=expected;pin(Path(mip['file']),pins)
  payload=b''.join(chain);assert len(mips)==12
  if guid in compressed_lookup:assert payload==Path(row['whole_file']).read_bytes()
  rows.append({'guid':guid,'authored_texture_index':slot,'texture_binding':bindings[slot][0],'sampler_binding':bindings[slot][1],
   'source_path':original['source_path'],'source_sha256':original['source_sha256'],'source_meta_sha256':original['original_meta_sha256'],
   'encoding':encoding,'native_graphics_format':profile['graphics_format'],'width':profile['width'],'height':profile['height'],
   'storage_bytes':len(payload),'storage_sha256':hashlib.sha256(payload).hexdigest(),'mips':mips,
   'source_profile_capture_case_index':profile_index,'storage_capture_protocol':protocol,'storage_capture_case_index':case_index,
   'sampler':{k:profile[k] for k in ['filter_mode','wrap_u','wrap_v','wrap_w','aniso_level','mip_map_bias','mipmap_enabled','srgb']},
   'normal_decoder_profile_index':original['normal_profile_index'],'normal_decoder_qualified':False,
   '_original':original,'_payload':payload,'_capture_path':capture_path})
  total+=len(payload)
 assert len(rows)==10 and sum(len(r['mips']) for r in rows)==120
 ROOT.mkdir()
 for p,name in [(native_path,'original-native-profiles.json'),(compressed_path,'original-readable-storage-capture.json'),(binding_path,'binding-contract-reference.rs')]:
  target=ROOT/name;shutil.copy2(p,target);assert sha(target)==sha(p);pin(target,pins)
 for row in rows:
  original=row.pop('_original');payload=row.pop('_payload');capture_path=row.pop('_capture_path')
  folder=ROOT/('texture-'+str(row['authored_texture_index']).zfill(2));folder.mkdir()
  storage=folder/'native-mips.bin';storage.write_bytes(payload);assert sha(storage)==row['storage_sha256'];pin(storage,pins)
  for key,path,name in [('original_png',Path(original['source_file']),'original.png'),('original_meta',Path(original['original_meta']),'original.meta')]:
   target=folder/name;shutil.copy2(path,target);assert sha(target)==sha(path);pin(target,pins);row[key]=str(target)
  row['native_storage']=str(storage);row['source_profile_capture']=str(ROOT/'original-native-profiles.json')
  row['storage_capture']=str(ROOT/('original-native-profiles.json' if capture_path==native_path else 'original-readable-storage-capture.json'))
 result={'status':'ten_native_source_texture_resources_composed_original_capture_json_preserved',
  'sub_lot':'L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.TRANSPORT','finished_utc':datetime.now(timezone.utc).isoformat(),
  'resources_composed':10,'native_mips_composed':120,'native_storage_bytes':total,'rows':rows,'input_pins':pins,
  'renderer_material_contract_admission_verified':False,'normal_decoder_gpu_equivalence_qualified':False,
  'forge_texture_upload_qualified':False,'forge_editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0,
  'next_action':'Admit both original native capture protocols explicitly in the common transport without rewriting captures, then validate all ten resources and compare source/Forge sampling'}
 (ROOT/'RESOURCES.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps({k:v for k,v in result.items() if k not in ['rows','input_pins']}))
if __name__=='__main__':main()

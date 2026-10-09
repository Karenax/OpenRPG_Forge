from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct,math
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-native-normal-decode-reopen-v1';CAP=W/'townsmith-s-paint-native-normal-decode-v1'
CPU_ABSOLUTE_TOLERANCE=1e-6  # Fixed before observing numerical comparisons.
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def main():
 prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json')
 assert run['actual_exit_code']==0 and run['status']=='native_normal_decode_capture_finished_originals_unchanged'
 assert sha(CAP/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
 pins={}
 for entries in [prep['input_pins'],prep['isolated_copy_pins'],run['artifact_pins']]:
  for p,h in entries.items():assert sha(p)==h,p;pins[p]=h
 bcpath=W/'townsmith-s-paint-compressed-storage-v2/NATIVE-RESULT.v1.json';bc=read(bcpath);lookup={r['guid']:r for r in bc['rows']}
 for p in [CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',bcpath,Path(__file__)]:pins[str(p)]=sha(p)
 assert native['status']=='native_normal_decode_capture_complete' and len(native['rows'])==3
 count=0;max_error=0;rows=[]
 for row in native['rows']:
  assert row['settings_unchanged'] and row['settings_before']==row['settings_after'] and not row['error']
  assert row['sample_count']==108 and row['mip_count']==12 and row['graphics_format']=='RGBA_DXT5_UNorm'
  old=lookup[row['guid']];reference=Path(old['source_samples']).read_bytes();pins[old['source_samples']]=sha(old['source_samples'])
  payload=Path(row['samples_file']).read_bytes();assert len(payload)==108*48 and len(reference)==108*16
  mode=row['shader_unpack_mode'];assert mode in [0,1,2]
  for i in range(108):
   record=payload[i*48:(i+1)*48];assert record[:16]==reference[i*16:(i+1)*16]
   packed=struct.unpack('<4f',record[:16]);decoded=struct.unpack('<4f',record[16:32]);flags=struct.unpack('<4f',record[32:48])
   assert all(math.isfinite(v) for v in packed+decoded+flags);assert flags==(float(mode),0.0,0.0,0.0)
   assert decoded[3]==packed[3]
   if mode==0:expected=tuple(f32(f32(v*2.0)-1.0) for v in packed[:3])
   else:
    channel=f32(packed[0]*packed[3]) if mode==1 else packed[3]
    x=f32(f32(channel*2.0)-1.0);y=f32(f32(packed[1]*2.0)-1.0)
    square=f32(f32(x*x)+f32(y*y));z=max(f32(1e-16),f32(math.sqrt(f32(1.0-min(1.0,max(0.0,square))))))
    expected=(x,y,z)
   error=max(abs(a-b) for a,b in zip(decoded[:3],expected));assert error<=CPU_ABSOLUTE_TOLERANCE,(i,error)
   max_error=max(max_error,error);count+=1
  rows.append({'guid':row['guid'],'captured_file_id':row['file_id'],'normal_profile_index':row['normal_profile_index'],'authored_texture_index':row['authored_texture_index'],'native_unpack_mode':mode,'observations_verified':108})
 assert count==324 and sorted(r['normal_profile_index'] for r in rows)==[0,1,2]
 result={'status':'three_source_normal_profiles_reopened_324_native_decodes_original_texels_exact','finished_utc':datetime.now(timezone.utc).isoformat(),
  'native_normal_profiles_verified':3,'native_decode_observations_verified':324,'original_packed_texels_bit_exact':324,'original_alpha_values_preserved':324,
  'cpu_expression_absolute_tolerance':CPU_ABSOLUTE_TOLERANCE,'cpu_expression_max_error':max_error,'rows':rows,'input_pins':pins,
  'filtered_sampling_qualified':False,'forge_gpu_normal_decoder_qualified':False,'actual_material_render_qualified':False,'forge_editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0,
  'next_action':'Map captured native mode explicitly to common decoder ABI, compare Forge GPU sampling/decodes, and retain full material/Studio qualification separately'}
 ROOT.mkdir(exist_ok=False);(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
if __name__=='__main__':main()

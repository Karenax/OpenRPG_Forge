from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');J=W/'native-srgb-generated-transfer-ramp-v84';O=W/'native-srgb-generated-transfer-ramp-reopen-v84';assert not O.exists();pins={}
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def pin(p,h=None):
 v=hashlib.sha256(p.read_bytes()).hexdigest();assert h is None or h==v,str(p);pins[str(p)]=v;return v
run=read(J/'RUN-RESULT.v1.json');native=read(J/'NATIVE-RESULT.v1.json');assert run['actual_exit_code']==0 and run['gpu_probe_executed'] and run['status']==native['status'];pin(J/'NATIVE-RESULT.v1.json',run['native_receipt_sha256'])
for key,h in read(J/'PREPARED.v1.json')['input_pins'].items():pin(Path(key),h)
for name in ['RUN-RESULT.v1.json','PREPARED.v1.json','JOB.v1.json','stdout.log','stderr.log']:pin(J/name)
buffers=[]
for i in range(3):
 p=J/f'NATIVE-SRGB-{i}.rgba8srgb';pin(p);buffers.append(p.read_bytes())
assert buffers[0]==buffers[1]==buffers[2] and len(buffers[0])==65536;p=J/'GENERATED-VALUES.f32';pin(p);data=p.read_bytes();assert len(data)==65536;values=list(struct.unpack('<16384f',data));bits=list(struct.unpack('<16384I',data));assert all(a<b for a,b in zip(values,values[1:]));codes=list(buffers[0][0::4]);assert codes==list(buffers[0][1::4])==list(buffers[0][2::4]);assert all(v==255 for v in buffers[0][3::4]);assert all(a<=b for a,b in zip(codes,codes[1:]));assert set(codes)==set(range(256)) and codes[0]==0 and codes[-1]==255;hist=[codes.count(i) for i in range(256)];assert hist==native['code_histogram'];assert native['format']=='Rgba8UnormSrgb' and native['backend']=='Dx12' and native['purchased_asset_inputs']==native['unity_pixel_inputs']==native['gpu_validation_errors']==0
brackets=[]
for code in range(1,256):
 index=next(i for i,v in enumerate(codes) if v>=code);assert index>0;brackets.append(dict(code=code,low_bits=bits[index-1],low_value=values[index-1],low_observed_code=codes[index-1],high_bits=bits[index],high_value=values[index],high_observed_code=codes[index]))
O.mkdir();bp=O/'COARSE-THRESHOLD-BRACKETS.v1.json';bp.write_text(json.dumps(dict(status='observed_coarse_transition_intervals_not_exact_boundaries',device_info=native['device_info'],adapter=native['adapter'],backend=native['backend'],brackets=brackets,source_pixels_applied=False,threshold_boundaries_qualified=False),indent=2)+'\n',encoding='utf-8');pin(bp)
result=dict(status='native_generated_srgb_transfer_ramp_and_initial_brackets_verified',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L01.SRGB.NATIF.S01',probe_values=16384,raw_bytes_per_readback=65536,consecutive_equal_raw_readbacks=3,all_256_codes_observed=True,monotonic_transfer_on_observed_ramp=True,coarse_transition_brackets=255,exact_threshold_boundaries_qualified=False,backend=native['backend'],adapter=native['adapter'],device_info=native['device_info'],input_pins=pins,purchased_asset_inputs=0,unity_pixel_inputs=0,source_pixel_fit=False,installed_in_product=False,full_import=False,complete_assets_delivered_increment=0);(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))

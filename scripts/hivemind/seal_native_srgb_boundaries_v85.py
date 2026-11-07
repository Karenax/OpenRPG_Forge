from pathlib import Path
import json,hashlib,struct,bisect
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');J=W/'native-srgb-generated-boundaries-v85';O=W/'native-srgb-generated-boundaries-reopen-v85';assert not O.exists();pins={}
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def pin(p,h=None):
 v=hashlib.sha256(p.read_bytes()).hexdigest();assert h is None or h==v,str(p);pins[str(p)]=v;return v
r=read(J/'RUN-RESULT.v1.json');n=read(J/'NATIVE-RESULT.v1.json');assert r['actual_exit_code']==0 and r['gpu_probe_executed'] and n['exact_adjacent_float32_boundaries']==255;pin(J/'NATIVE-RESULT.v1.json',r['native_receipt_sha256'])
for key,h in read(J/'PREPARED.v1.json')['input_pins'].items():pin(Path(key),h)
for p in J.glob('*'):
 if p.is_file():pin(p)
raw=[(J/f'ADJACENT-{i}.rgba8srgb').read_bytes() for i in range(3)];assert raw[0]==raw[1]==raw[2] and len(raw[0])==2048;inputs=list(struct.unpack('<512f',(J/'ADJACENT-0.inputs.f32').read_bytes()));curve=list(struct.unpack('<256f',(J/'NATIVE-SRGB-INPUT-THRESHOLDS.f32').read_bytes()));assert curve[0]==0 and all(a<b for a,b in zip(curve,curve[1:]))
for i,row in enumerate(n['boundaries']):
 assert row['code']==i+1 and row['threshold_bits']-row['lower_bits']==1;assert inputs[i*2]==row['lower_value'] and inputs[i*2+1]==curve[i+1]==row['threshold_value'];assert raw[0][i*8]==i and raw[0][i*8+4]==i+1
for b in raw[0:1]:
 for p in range(0,len(b),4):assert b[p]==b[p+1]==b[p+2] and b[p+3]==255
prior=W/'native-srgb-generated-transfer-ramp-reopen-v84/RESULT.v1.json';p=read(prior);pin(prior);priorjob=W/'native-srgb-generated-transfer-ramp-v84';vp=priorjob/'GENERATED-VALUES.f32';rp=priorjob/'NATIVE-SRGB-0.rgba8srgb';pin(vp,p['input_pins'][str(vp)]);pin(rp,p['input_pins'][str(rp)]);values=struct.unpack('<16384f',vp.read_bytes());codes=rp.read_bytes()[::4];pred=[max(0,min(255,bisect.bisect_right(curve,max(0.,min(1.,x)))-1)) for x in values];assert bytes(pred)==codes
assert n['device_info']==p['device_info'] and n['adapter']==p['adapter'] and n['backend']==p['backend'];assert n['purchased_asset_inputs']==n['unity_pixel_inputs']==n['gpu_validation_errors']==0
O.mkdir();res=dict(status='native_srgb_exact_boundaries_and_dense_prior_ramp_verified',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L01.SRGB.NATIF.S02',float32_adjacent_boundaries_verified=255,repeated_adjacent_readbacks=3,search_steps=n['search_steps'],independent_prior_gpu_ramp_values_equal=16384,threshold_bytes=1024,device_info=n['device_info'],adapter=n['adapter'],backend=n['backend'],input_pins=pins,purchased_asset_inputs=0,unity_pixel_inputs=0,source_pixel_fit=False,shader_consumer_qualified=False,installed_in_product=False,full_import=False,complete_assets_delivered_increment=0);(O/'RESULT.v1.json').write_text(json.dumps(res,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in res.items() if k!='input_pins'}))

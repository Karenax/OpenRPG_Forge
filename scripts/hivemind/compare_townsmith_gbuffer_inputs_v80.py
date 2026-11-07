from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,numpy as np
W=Path('F:/OpenRPG_ImportWork');O=W/'townsmith-gbuffer-input-comparison-v80';assert not O.exists();pins={}
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def pin(p,h=None):
 with p.open('rb') as f:v=hashlib.file_digest(f,'sha256').hexdigest()
 assert h is None or h==v,str(p);pins[str(p)]=v;return v
batch=W/'townsmith-native-gbuffer-input-batch-v80/BATCH.v1.json';b=read(batch);assert b['status']=='six_native_gbuffer_input_channels_drawn_with_stable_readbacks' and b['completed_channels']==list(range(6));pin(batch)
sb=W/'townsmith-direct-gbuffer-input-captures-v78-v79/BATCH.v1.json';s=read(sb);assert s['status']=='two_source_gbuffer_inputs_captured_and_reopened' and not s['sdk_restore_pending'];pin(sb)
arrays={};masks=[]
for index,ver in [(0,79),(2,78)]:
 proof=W/f'townsmith-door-source-declared-gbuffer{index}-reopen-v{ver}/RESULT.v1.json';r=read(proof);assert r[f'actual_rendergraph_gbuffer{index}_binding_qualified'] and r['sdk_restored_byte_equal'] and r['original_lit_reference_byte_equal'];pin(proof)
 for key,h in r['input_pins'].items():pin(Path(key),h)
 root=W/f'townsmith-door-source-declared-gbuffer{index}-v{ver}';meta=read(root/f'door-nopost-linear.inpass-gbuffer{index}-3.json');a=np.fromfile(root/f'door-nopost-linear.inpass-gbuffer{index}-3.rgba32f',dtype='<f4').reshape(meta['height'],meta['width'],4)[:512,:512][::-1];mask=(np.fromfile(root/'door-nopost-linear.depth-3.r32f',dtype='<f4').reshape(512,512)>0)[::-1];assert mask.sum()==34445 and np.isfinite(a).all();arrays[index]=a;masks.append(mask)
assert np.array_equal(*masks);mask=masks[0];assert np.all(np.rint(arrays[2][:,:,3][mask]*255)==0)
context=W/'townsmith-door-source-declared-gbuffer0-v79/door-nopost-linear.context.json';pin(context);assert read(context)['actual_lit_shader_mode']=='Deferred'
values=[]
for channel in range(6):
 root=W/f'townsmith-native-gbuffer-input-channel-{channel}-v80';run=read(root/'RUN-RESULT.v1.json');n=read(root/'NATIVE-RESULT.v1.json');prep=read(root/'PREPARED.v1.json');assert run['actual_exit_code']==0 and run['gpu_draw_executed'] and n['consecutive_equal_readbacks']>=3 and n['diagnostic_channel']==channel
 for key,h in prep['input_pins'].items():pin(Path(key),h)
 for file in ['RUN-RESULT.v1.json','NATIVE-RESULT.v1.json','PREPARED.v1.json']:pin(root/file)
 raw=root/'FORGE-LINEAR.rgba16f';pin(raw,run['forge_image_sha256']);tr=np.fromfile(raw,dtype='<f2').astype('float32').reshape(512,512,4)[mask];assert np.isfinite(tr).all();f=tr*255;codes=np.rint(f);assert np.abs(f-codes).max()<.5;bc=codes.astype('uint32');bits=bc[:,0]|(bc[:,1]<<8)|(bc[:,2]<<16)|(bc[:,3]<<24);decoded=bits.astype('<u4').view('<f4');assert np.isfinite(decoded).all();values.append(decoded)
native=np.stack(values,-1);source=np.concatenate([arrays[0][:,:,:3][mask],arrays[2][:,:,:3][mask]],axis=-1);rows=[]
for ch,name in enumerate(['diffuse_r','diffuse_g','diffuse_b','f0_encoded_r','f0_encoded_g','f0_encoded_b']):
 a=source[:,ch];v=native[:,ch];delta=np.abs(a.astype('float64')-v.astype('float64'));row=dict(channel=name,exact_float_pixels=int((delta==0).sum()),mean_error=float(delta.mean()),max_error=float(delta.max()))
 if ch>=3:
  sc=np.rint(a*255).astype('int32');nc=np.rint(v*255).astype('int32');dc=np.abs(sc-nc);row.update(exact_code_pixels=int((dc==0).sum()),max_code_error=int(dc.max()),mean_code_error=float(dc.mean()))
 rows.append(row)
# CPU validation of source storage rule against original graph is diagnostic only.
graphproof=W/'townsmith-graph-quad-difference-comparison-v69/RESULT.v1.json';g=read(graphproof);pin(graphproof);sv=[]
for group in [0,1]:
 raw=W/f'townsmith-door-source-material-values-v41/door-nopost-linear.graph-probe-{9+group}.rgba32f';mp=raw.with_suffix('.json');pin(raw,g['input_pins'][str(raw)]);pin(mp,g['input_pins'][str(mp)]);m=read(mp);sv.append(np.fromfile(raw,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512,:3][::-1][mask])
rgb=sv[0];met=sv[1][:,0:1];f0=np.float32(.04)*(1-met)+rgb*met;enc=np.clip(np.float32(1.055)*np.power(np.abs(f0),np.float32(.416666667))-np.float32(.055),0,1);pred=np.rint(enc*255).astype('int32');actual=np.rint(arrays[2][:,:,:3][mask]*255).astype('int32');d=np.abs(pred-actual);source_rule=dict(cpu_diagnostic_only=True,exact_rgb_code_pixels=int(np.all(d==0,axis=1).sum()),max_code_error=int(d.max()))
pin(Path(__file__));O.mkdir();np.save(O/'NATIVE-INPUTS.npy',native);np.save(O/'SOURCE-INPUTS.npy',source);pin(O/'NATIVE-INPUTS.npy');pin(O/'SOURCE-INPUTS.npy')
res=dict(status='actual_deferred_diffuse_and_f0_inputs_compared',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L03.TOWNSMITH.ECLAIRAGE.ENTREES.GBUFFER',covered_pixels=34445,source_captures_verified=24,native_channels_verified=6,native_readbacks_per_channel=3,source_lit_mode='Deferred',source_material_feature_alpha_codes=[0],rows=rows,source_f0_storage_rule_diagnostic=source_rule,sdk_restored_byte_equal=True,source_reference_byte_unchanged=True,rehashed_pins=len(pins),input_pins=pins,source_pixels_applied_to_native=False,pixel_fit_or_threshold_relaxation=False,installed_in_product=False,full_material_fidelity_qualified=False,forge_editor_verified=False,full_import=False,complete_assets_delivered_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(res,indent=2)+'\n');print(json.dumps({k:v for k,v in res.items() if k!='input_pins'}))

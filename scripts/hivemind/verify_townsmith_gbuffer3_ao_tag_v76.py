from pathlib import Path
import json,hashlib,numpy as np
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');O=W/'townsmith-gbuffer3-ao-tag-control-v76';assert not O.exists()
P={}
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def pin(p,h=None):
 d=hashlib.sha256(p.read_bytes()).hexdigest();assert h is None or d==h,str(p);P[str(p)]=d;return d
seal=W/'townsmith-door-source-declared-gbuffer3-reopen-v75/RESULT.v1.json';r=read(seal);pin(seal);assert r['actual_rendergraph_gbuffer3_binding_qualified'] and r['sdk_restored_byte_equal']
for s,h in r['input_pins'].items():pin(Path(s),h)
src=W/'townsmith-door-source-declared-gbuffer3-v75';meta=read(src/'door-nopost-linear.inpass-gbuffer3-3.json');raw=src/'door-nopost-linear.inpass-gbuffer3-3.rgba32f';a=np.fromfile(raw,dtype='<f4').reshape(meta['height'],meta['width'],4)[:512,:512][::-1]
mask=(np.fromfile(src/'door-nopost-linear.depth-3.r32f',dtype='<f4').reshape(512,512)>0)[::-1];assert mask.sum()==34445
sdk=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Material/Lit/Lit.hlsl';pin(sdk);text=sdk.read_text();assert '#define AO_IN_GBUFFER3_TAG float3((1 << 11), 1, (1 << 10))' in text and 'bsdfData.ambientOcclusion = gbuffer3.y;' in text
is_tag=(a[:,:,0]==2048)&(a[:,:,2]==1024);assert is_tag[mask].all();ao=a[:,:,1][mask];assert ((ao>=0)&(ao<=1)).all()
graph=W/'townsmith-graph-quad-difference-comparison-v69/RESULT.v1.json';g=read(graph);pin(graph)
sp=W/'townsmith-door-source-material-values-v41/door-nopost-linear.graph-probe-10.rgba32f';sm=W/'townsmith-door-source-material-values-v41/door-nopost-linear.graph-probe-10.json';pin(sp,g['input_pins'][str(sp)]);pin(sm,g['input_pins'][str(sm)]);m=read(sm);sao=np.fromfile(sp,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512,2][::-1][mask]
npth=W/'townsmith-native-graph-quad-difference-channel-5-v69/FORGE-LINEAR.rgba16f';pin(npth,g['input_pins'][str(npth)]);tr=np.fromfile(npth,dtype='<f2').astype('float32').reshape(512,512,4)[mask];codes=np.rint(tr*255).astype('uint32');bits=codes[:,0]|(codes[:,1]<<8)|(codes[:,2]<<16)|(codes[:,3]<<24);nao=bits.astype('<u4').view('<f4')
def quant(v,mode):
 bits=v.astype('<f4').view('<u4');shift=17
 if mode=='nearest_even':bits=bits+((1<<(shift-1))-1)+((bits>>shift)&1)
 return (bits&np.uint32(0xffffffff ^ ((1<<shift)-1))).view('<f4')
rows=[]
for name,v in [('source_graph_raw',sao),('source_graph_floor_uf11',quant(sao,'floor')),('source_graph_nearest_even_uf11',quant(sao,'nearest_even')),('native_graph_raw',nao),('native_graph_nearest_even_uf11',quant(nao,'nearest_even'))]:
 d=np.abs(ao.astype('float64')-v.astype('float64'));rows.append(dict(target=name,equal_pixels=int((d==0).sum()),mean_error=float(d.mean()),max_error=float(d.max())))
old=W/'townsmith-declared-diffuse-buffer-comparison-v75/RESULT.v1.json';pin(old);pin(Path(__file__));O.mkdir();(O/'Lit.source.cs.hlsl').write_bytes(sdk.read_bytes());pin(O/'Lit.source.cs.hlsl',P[str(sdk)])
res=dict(status='actual_gbuffer3_ao_tag_identified_and_compared',finished_utc=datetime.now(timezone.utc).isoformat(),covered_pixels=34445,ao_tag_pixels=int(is_tag[mask].sum()),tag_red=2048,tag_blue=1024,ao_min=float(ao.min()),ao_max=float(ao.max()),rows=rows,previous_rgb_comparison_not_lighting_evidence=True,previous_diffuse_store_hypothesis_not_applicable_to_observed_tag_branch=True,input_pins=P,source_pixels_applied_to_native=False,pixel_fit_or_threshold_relaxation=False,installed_in_product=False,material_fidelity_qualified=False,forge_editor_verified=False,full_import=False,complete_assets_delivered_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(res,indent=2)+'\n');print(json.dumps({k:v for k,v in res.items() if k!='input_pins'}))

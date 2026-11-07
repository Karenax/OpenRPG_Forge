"""Locate diagnostic differences without changing the qualification tolerance."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct
W=Path('F:/OpenRPG_ImportWork');C=W/'house-active-sampling-diagnostic-comparison-v629';N=W/'house-active-sampling-diagnostic-v628';O=W/'house-active-sampling-analysis-v630'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def values(p):return struct.unpack('<16384f',Path(p).read_bytes())
assert not O.exists()
r=read(C/'RESULT.v1.json');native=read(N/'NATIVE-RESULT.v1.json');run=read(N/'RUN-RESULT.v1.json')
assert r['actual_gpu_exit_code']==run['unity_exit_code']==0 and r['diagnostic_only'] and not run['source_changes'] and not run['private_importer_changes']
pins={}
for key in ['input_pins','output_pins']:
    for p,h in r[key].items():assert sha(p)==h,p;pins[p]=h
names=['base_color_alpha','raw_albedo_rgba','parallax_uv_depth_padding','coarse_uv_derivatives','fine_uv_derivatives','pixel_orientation']
summary=[];frames=[];native_stable=True;wgsl_stable=True
old_native=W/'house-hdrp-bias-surface-reference-v623/frames';old_gpu=W/'house-hdrp-bias-surface-gpu-comparison-v624/outputs'
for f in native['frames']:
    key=f['material_guid']+'/'+Path(f['folder']).name
    pairs=[];color_pixels=set();uv_max_at_color=0;raw_max_at_color=0
    for mode,p in enumerate(f['planes']):
        q=C/'outputs'/key/('plane-'+str(mode)+'.f32');a,b=values(p),values(q);errors=[abs(x-y) for x,y in zip(a,b)]
        pairs.append((a,b,errors))
        if mode==0:
            color_pixels={j//4 for j,e in enumerate(errors) if e>1e-5}
            native_stable &= sha(p)==sha(old_native/key/'plane-0.f32')
            wgsl_stable &= sha(q)==sha(old_gpu/key/'plane-0.f32')
        summary.append(dict(key=key,plane=mode,name=names[mode],different_components=sum(x!=y for x,y in zip(a,b)),failed_components=sum(e>(0 if mode==5 else 1e-5) for e in errors),max_absolute_error=max(errors),max_by_component=[max(errors[c::4]) for c in range(4)]))
    for pixel in color_pixels:
        uv_max_at_color=max(uv_max_at_color,*pairs[2][2][pixel*4:pixel*4+2]);raw_max_at_color=max(raw_max_at_color,*pairs[1][2][pixel*4:pixel*4+4])
    frames.append(dict(key=key,color_failed_pixels=len(color_pixels),max_uv_error_at_color_failed_pixels=uv_max_at_color,max_raw_albedo_error_at_color_failed_pixels=raw_max_at_color))
for p in [Path(__file__),C/'RESULT.v1.json',N/'RUN-RESULT.v1.json']:pins[str(p)]=sha(p)
aggregate=[dict(plane=i,name=names[i],different_components=sum(x['different_components'] for x in summary if x['plane']==i),failed_components=sum(x['failed_components'] for x in summary if x['plane']==i),max_absolute_error=max(x['max_absolute_error'] for x in summary if x['plane']==i),max_by_component=[max(x['max_by_component'][c] for x in summary if x['plane']==i) for c in range(4)]) for i in range(6)]
O.mkdir();result=dict(status='house_active_sampling_diagnostic_analysed',utc=datetime.now(timezone.utc).isoformat(),planes=aggregate,failed_color_frames=[f for f in frames if f['color_failed_pixels']],native_uninstrumented_color_byte_exact=native_stable,wgsl_uninstrumented_color_byte_exact=wgsl_stable,input_pins=pins,full_import=False,complete_conversion_increment=0,diagnostic_only=True)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');(O/'FRAME-PLANES.v1.json').write_text(json.dumps(summary,indent=2),'utf-8')
print(json.dumps({k:result[k] for k in ['status','planes','failed_color_frames','native_uninstrumented_color_byte_exact','wgsl_uninstrumented_color_byte_exact']}))

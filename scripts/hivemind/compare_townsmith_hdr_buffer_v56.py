"""Decode a real GPU RG11B10 readback; never fit or edit captured RGB."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,numpy as np
W=Path('F:/OpenRPG_ImportWork');D=W/'townsmith-native-hdr-buffer-layer-0-v56';ROOT=W/'townsmith-hdr-buffer-comparison-v56'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not ROOT.exists()
run=read(D/'RUN-RESULT.v1.json');native=read(D/'NATIVE-RESULT.v1.json');prep=read(D/'PREPARED.v1.json')
assert run['actual_exit_code']==0 and run['gpu_draw_executed'] and native['consecutive_equal_readbacks']>=3
assert sha(D/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
assert native['target_format']=='Rg11b10Ufloat' and native['raw_readback_preserved'] and not native['cpu_color_adjustment']
pins=dict(prep['input_pins'])
for path,h in pins.items():assert sha(Path(path))==h,path
packed_path=D/'FORGE-PACKED.rg11b10';assert sha(packed_path)==run['forge_packed_image_sha256']
assert packed_path.stat().st_size==512*512*4
packed=np.fromfile(packed_path,dtype='<u4').reshape(512,512);channels=[]
for shift,bits in [(0,6),(11,6),(22,5)]:
 value=(packed>>shift)&((1<<(bits+5))-1);mant=value&((1<<bits)-1);exponent=value>>bits
 assert not np.any(exponent==31),'Nonfinite RG11B10 channel'
 scale=np.where(exponent==0,-14,exponent.astype('int32')-15)
 significand=mant.astype('float64')/(1<<bits)+np.where(exponent==0,0.,1.)
 channels.append(np.ldexp(significand,scale).astype('<f4'))
rgb=np.stack(channels,-1);assert np.isfinite(rgb).all() and (rgb>=0).all()
source_proof_path=W/'townsmith-source-microshadow-inputs-reopen-v55/RESULT.v1.json';proof=read(source_proof_path)
source=W/'townsmith-door-source-microshadow-inputs-v55/door-nopost-linear.rgba16f';assert sha(source)==proof['input_pins'][str(source)]
original=W/'townsmith-door-source-direct-controls-v18/door-nopost-linear.rgba16f';old_proof=read(W/'townsmith-door-source-direct-controls-reopen-v18/RESULT.v1.json');assert sha(original)==old_proof['input_pins'][str(original)] and sha(source)==sha(original)
src=np.fromfile(source,dtype='<f2').reshape(512,512,4)[::-1,:,:3].astype('float32')
depth=W/'townsmith-door-source-material-values-v41/door-nopost-linear.depth-3.r32f';mask=(np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1];assert mask.sum()==34445
baseline_root=W/'townsmith-native-lighting-layer-0-v54';baseline_run=read(baseline_root/'RUN-RESULT.v1.json');baseline_path=baseline_root/'FORGE-LINEAR.rgba16f';assert sha(baseline_path)==baseline_run['forge_image_sha256']
baseline=np.fromfile(baseline_path,dtype='<f2').reshape(512,512,4)[:,:,:3].astype('float32')
rows=[]
for name,pixels in [('unchanged_glb_rgba16_target',baseline),('unchanged_glb_actual_rg11b10_target',rgb)]:
 delta=np.abs(src-pixels)[mask];rows.append(dict(control=name,mean_rgb_error=float(delta.mean()),max_rgb_error=float(delta.max()),source_mean=float(src[mask].mean()),native_mean=float(pixels[mask].mean()),exact_channels_fraction=float((delta==0).mean()),pixels_over_001=int((delta.max(axis=1)>.001).sum())))
for path in [Path(__file__),D/'RUN-RESULT.v1.json',D/'NATIVE-RESULT.v1.json',D/'PREPARED.v1.json',packed_path,source_proof_path,source,original,W/'townsmith-door-source-direct-controls-reopen-v18/RESULT.v1.json',depth,baseline_root/'RUN-RESULT.v1.json',baseline_path]:pins[str(path)]=sha(path)
ROOT.mkdir();decoded=ROOT/'FORGE-DECODED.rgb32f';rgb.astype('<f4').tofile(decoded);pins[str(decoded)]=sha(decoded)
r=dict(status='actual_gpu_rg11b10_target_readback_compared_fidelity_open',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L03.TOWNSMITH.MATERIAUX.HDR.FORMAT',rows=rows,input_pins=pins,covered_pixels=34445,consecutive_equal_gpu_readbacks=native['consecutive_equal_readbacks'],packed_gpu_bytes=packed_path.stat().st_size,decoded_pixels_bit_format_only=True,source_reference_byte_unchanged=True,pixel_fit_or_threshold_relaxation=False,source_pixels_applied_to_native=False,source_hdr_internal_buffer_equivalence_qualified=False,full_material_fidelity_qualified=False,forge_editor_verified=False,full_import=False,complete_assets_delivered_increment=0)
(ROOT/'RESULT.v1.json').write_text(json.dumps(r,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in r.items() if k!='input_pins'}))

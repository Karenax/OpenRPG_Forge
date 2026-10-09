"""Reopen GPU pixels; compare shadow response and analytic presentation, never qualify UI."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct,math,sys
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-forge-shadow-pair-v3';ROOT=W/'firepit-forge-shadow-pair-comparison-v1'
SDK=Path('F:/OpenRPG_ImportWork/unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary/Color.hlsl')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(p,v):p.write_text(json.dumps(v,indent=2,ensure_ascii=False),'utf-8')
def protocol():
 assert not ROOT.exists() and not (CAP/'gpu-0.rgba16f').exists() and not (CAP/'gpu-1.rgba16f').exists(),'Define protocol before GPU images exist'
 ROOT.mkdir();save(ROOT/'PROTOCOL.v1.json',dict(created_utc=datetime.now(timezone.utc).isoformat(),source_case_sha256=sha(W/'firepit-shadow-comparison-case-v1/CASE.v1.json'),gpu_pixels_absent_at_definition=True,geometry_and_camera_no_registration_fit=True,unity_raw_rows_bottom_to_top=True,bevy_raw_rows_top_to_bottom=True,manual_pixel_alignment_adjustments_allowed=False,mask_linear_channel_delta=1e-4,maximum_comparison_mae=2/255,minimum_shadow_mask_iou=.95,analytic_hable_neutral_curve_coefficients=[.2,.29,.24,.272,.02,.3],neutral_white_level=5.3,analytic_cpu_transfer_not_gpu_qualification=True,full_import=False,input_pins={str(Path(__file__).resolve()):sha(Path(__file__).resolve()),str(SDK):sha(SDK)}));print('Comparison protocol recorded before GPU readback')
def curve(x):
 x=max(0.,x);return ((x*(.2*x+.24*.29)+.272*.02)/(x*(.2*x+.29)+.272*.3))-.02/.3
def neutral(x):w=1/curve(5.3);return curve(max(0.,x)*w)*w
def gamma(x):return 12.92*x if x<=.0031308 else 1.055*x**(1/2.4)-.055
def pixels(p):
 b=p.read_bytes();assert len(b)==512*512*8
 result=[v[:3] for v in struct.iter_unpack('<4e',b)];assert all(math.isfinite(x) for v in result for x in v);return result
def main():
 proto=read(ROOT/'PROTOCOL.v1.json');assert not (ROOT/'RESULT.v1.json').exists()
 for p,h in proto['input_pins'].items():assert sha(p)==h,p
 run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');pair=read(CAP/'GPU-PAIR.v1.json');case=read(CAP/'CASE.v1.json');assert run['actual_exit_code']==0 and len(pair['images'])==2
 for p,h in prep['input_pins'].items():assert sha(p)==h,p
 assert sha(CAP/'CASE.v1.json')==proto['source_case_sha256']
 assert len(pair['source_pipelines'])>=3 and all(p['state']=='ready' for p in pair['source_pipelines'])
 source=[];gpu=[];files=[Path(__file__).resolve(),SDK,ROOT/'PROTOCOL.v1.json',CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'GPU-PAIR.v1.json',CAP/'CASE.v1.json']
 for view in range(2):
  row=case['source_images'][view];p=Path(row['raw_path']);assert sha(p)==row['raw_sha256'];a=pixels(p)
  source.append([v for y in range(511,-1,-1) for v in a[y*512:(y+1)*512]])
  raw=CAP/f'gpu-{view}.rgba16f';meta=read(CAP/f'gpu-{view}.metadata.json');assert meta['format']=='Rgba16Float' and meta['gpu_readback'] and meta['bytes']==512*512*8
  data=pixels(raw);gpu.append([tuple(neutral(x) for x in v) for v in data]);files += [p,raw,CAP/f'gpu-{view}.metadata.json']
  from PIL import Image
  out=bytearray(round(max(0,min(1,gamma(x)))*255) for v in gpu[-1] for x in v)
  Image.frombytes('RGB',(512,512),bytes(out)).save(ROOT/f'forge-analytic-neutral-{view}.png')
 errors=[];counts=[]
 for a,b in zip(source,gpu):
  e=[abs(x-y) for va,vb in zip(a,b) for x,y in zip(va,vb)];errors.append(dict(mae=sum(e)/len(e),maximum=max(e),rmse=math.sqrt(sum(x*x for x in e)/len(e))))
  counts.append(dict(source_min=min(x for v in a for x in v),source_max=max(x for v in a for x in v),forge_neutral_min=min(x for v in b for x in v),forge_neutral_max=max(x for v in b for x in v)))
 threshold=proto['mask_linear_channel_delta'];s_mask=[max(abs(x-y) for x,y in zip(a,b))>threshold for a,b in zip(source[0],source[1])];g_mask=[max(abs(x-y) for x,y in zip(a,b))>threshold for a,b in zip(gpu[0],gpu[1])]
 union=sum(a or b for a,b in zip(s_mask,g_mask));intersection=sum(a and b for a,b in zip(s_mask,g_mask));iou=intersection/union if union else 0
 matches=iou>=proto['minimum_shadow_mask_iou'] and all(e['mae']<=proto['maximum_comparison_mae'] for e in errors)
 save(ROOT/'RESULT.v1.json',dict(status='analytic_shadow_comparison_within_predeclared_thresholds' if matches else 'forge_source_shadow_comparison_refused',finished_utc=datetime.now(timezone.utc).isoformat(),sub_lot='L01.OMBRES.02.FORGE',gpu_images_reopened=2,raw_pixels_reopened=1048576,product_source_pins_unchanged=len(prep['input_pins']),source_pipelines_ready=len(pair['source_pipelines']),source_shadow_response_pixels=sum(s_mask),forge_shadow_response_pixels=sum(g_mask),shadow_mask_intersection=intersection,shadow_mask_union=union,shadow_mask_iou=iou,linear_color_errors=errors,pixel_ranges=counts,thresholds_not_fitted_after_readback=True,raw_vertical_origin_conversion_only=True,analytic_cpu_neutral_presentation_only=True,gpu_neutral_transfer_qualified=False,source_shadow_settings_matched=False,full_shadow_fidelity_qualified=False,editor_visual_verified=False,full_import=False,input_pins={str(p):sha(p) for p in files},next_action='Resolve shadow map/bias/filter and presentation/color discrepancies with source-native state evidence'))
 print(json.dumps({k:v for k,v in read(ROOT/'RESULT.v1.json').items() if k!='input_pins'},ensure_ascii=False))
if __name__=='__main__':protocol() if sys.argv[-1]=='protocol' else main()

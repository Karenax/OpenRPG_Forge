from pathlib import Path
from datetime import datetime, timezone
import hashlib,json,shutil
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-texture-layout-admission-v3'
OUT=W/'source-texture-layout-admission-reopen-v3'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not OUT.exists()
run=read(ROOT/'RUN-RESULT.v1.json');native=read(ROOT/'NATIVE-RESULT.v1.json');prepared=read(ROOT/'PREPARED.v1.json')
assert run['actual_exit_code']==0 and native['native_four_group_pipeline_layouts_created']
assert sha(ROOT/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
for p,h in prepared['input_pins'].items():assert sha(p)==h,p
assert native['baseline_webgpu_sampled_texture_limit']==native['baseline_webgpu_sampler_limit']==16
assert len(native['four_group_cases'])==3
for row, expected in zip(native['four_group_cases'],[(18,17,21,20),(21,18,24,21),(25,19,28,22)]):
    assert len(row['groups_before_material'])==3
    old,new=row['results']
    assert old['material']=='existing' and new['material']=='ten_textures'
    assert tuple(r['fragment'][k] for r in [old,new] for k in ['sampled_textures','samplers'])==expected
    for r in [old,new]:
        assert r['native_four_group_pipeline_layout_created'] and not r['baseline_webgpu_texture_sampler_limits_admitted']
assert not run['actual_render_pipeline_created'] and not run['web_gpu_verified']
OUT.mkdir();snapshots=OUT/'source-snapshots';snapshots.mkdir()
pins={}
for p,h in prepared['input_pins'].items():
    path=Path(p)
    if 'crates' in path.parts:
        copy=snapshots/('-'.join(path.parts[-4:]));shutil.copy2(path,copy);assert sha(copy)==h;pins[str(copy)]=h
    else:pins[p]=h
for p in [ROOT/'RUN-RESULT.v1.json',ROOT/'NATIVE-RESULT.v1.json',ROOT/'PREPARED.v1.json',ROOT/'stderr.log',Path(__file__)]:pins[str(p)]=sha(p)
result={'status':'six_native_bevy_four_group_layouts_reopened_baseline_webgpu_limit_gap_recorded',
 'sub_lot':'L03.TOWNSMITH.SHADER.S_PAINT.PIPELINE.GPU','finished_utc':datetime.now(timezone.utc).isoformat(),
 'native_pipeline_layouts_verified':6,'view_profiles':3,'texture_sampler_binding_counts':native['four_group_cases'],
 'baseline_webgpu_limits_admitted':False,'actual_browser_device_verified':False,
 'binding_array_element_limit_admission_qualified':False,
 'actual_shader_render_pipeline_created':False,'asset_render_qualified':False,'full_import':False,
 'complete_assets_delivered_increment':0,'input_pins':pins,
 'next_action':'Qualify native imported resources and implement the complete faithful program with explicit device resource admission; do not discard any source texture or silently reduce the render requirements'}
(OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','texture_sampler_binding_counts']}))

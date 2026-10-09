"""Reopen effective source environment evidence; keep comparison refusal explicit."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-shadow-source-reference-v10';ROOT=W/'firepit-shadow-effective-environment-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def component(d,key):
    value=json.loads(d[key]);assert len(value)==1;return next(iter(value.values()))
def value(d,key):return d[key]['m_Value']
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');native=read(CAP/'NATIVE-RESULT.v1.json')
    assert run['status']=='source_shadow_pair_and_effective_environment_captured' and run['actual_exit_code']==0
    assert not run['private_changed'] and run['source_inputs_unchanged']
    for files in [prep['input_pins'],prep['private_before_pins'],run['environment_files']]:
        for p,h in files.items():assert sha(p)==h,p
    states=[read(CAP/f'SOURCE-STATE-{i}.json') for i in range(2)]
    assert states[0]==states[1]
    d=states[0];fog=component(d,'fog');sky=component(d,'visual_environment');light=component(d,'hd_light');shadow=component(d,'shadow_settings')
    source_pairs=[];old=read(W/'firepit-shadow-source-reference-v8/NATIVE-RESULT.v1.json')
    for row,previous in zip(native['images'],old['images']):
        assert sha(row['path'])==row['sha256'] and sha(row['raw_path'])==row['raw_sha256']
        source_pairs.append(dict(view=row['view'],raw_sha256=row['raw_sha256'],same_raw_bytes_as_original_reference=row['raw_sha256']==previous['raw_sha256']))
    summary=dict(fog_enabled=value(fog,'enabled'),volumetric_fog_enabled=value(fog,'enableVolumetricFog'),sky_type=value(sky,'skyType'),ambient_probe_all_zero=all(v==0 for v in d['ambient_probe']),
        source_shadow_resolution=light['m_ShadowResolution'],source_normal_bias=light['m_NormalBias'],source_slope_bias=light['m_SlopeBias'],source_max_depth_bias=light['m_MaxDepthBias'],
        source_shadow_algorithm=light['m_ShadowAlgorithm'],source_shadow_variant=light['m_ShadowVariant'],source_cascade_count=value(shadow,'cascadeShadowSplitCount'),source_max_shadow_distance=value(shadow,'maxShadowDistance'))
    files=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',CAP/'NATIVE-RESULT.v1.json',CAP/'Unity.log',*[CAP/f'SOURCE-STATE-{i}.json' for i in range(2)]]
    result=dict(status='effective_unity_camera_environment_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),environment_states_verified=2,source_dependencies_verified=len(prep['dependency_paths']),source_inputs_verified=len(prep['input_pins']),
        summary=summary,reference_pairs=source_pairs,full_import=False,editor_visual_verified=False,source_shadow_settings_matched=False,render_fidelity_qualified=False,
        retained_insufficient_trial=str(W/'firepit-shadow-source-reference-v9/RUN-RESULT.v1.json'),input_pins={str(p):sha(p) for p in files},
        next_action='Resolve source/target normal-lighting and shadow filter/cascade/bias discrepancies; no arbitrary ambient fill or fitted thresholds')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()

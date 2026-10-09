"""Close only the measured source-light transport, never the asset delivery."""
from pathlib import Path
import datetime, hashlib, json
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-punctual-light-technical-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    native_path=W/'firepit-punctual-scene-v2/RUN-RESULT.v1.json'
    tests_path=W/'source-punctual-light-contract-check-v1/RUN-RESULT.v1.json'
    web_path=W/'studio-current-source-wasm-check-v22/RUN-RESULT.v1.json'
    comparison_path=W/'firepit-punctual-scene-comparison-v2/RESULT.v1.json'
    native,tests,web,comparison=map(read,[native_path,tests_path,web_path,comparison_path])
    assert native['actual_exit_code']==tests['actual_exit_code']==web['actual_exit_code']==0
    assert tests['tests_verified']==7 and not web['changed_source_files']
    assert (native['nodes'],native['primitives'],native['material_instances'])==(9,4,4)
    for result in [native,tests,web]:
        for p,h in result['input_pins'].items():assert sha(p)==h,p
    base=W/'firepit-punctual-scene-v2'
    transport=read(base/'POINT-LIGHT-TRANSPORT.json')
    assert all(transport[k] for k in ['common_mount','photometry_verified','source_metadata_preserved','missing_transport_policy_refused'])
    assert transport['point_lights_cpu']==1
    files=[native_path,tests_path,web_path,comparison_path,base/'POINT-LIGHT-TRANSPORT.json',Path(__file__).resolve()]
    for view in range(2):
        p=base/f'POINT-LIGHT-FRAME-{view}.json';frame=read(p)
        assert frame['actual_point_component_present'] and frame['world']==frame['expected_world']
        assert not frame['gpu_punctual_count_readback'];files.append(p)
    ROOT.mkdir()
    result=dict(status='source_punctual_light_common_mount_lifecycle_tests_and_web_technical_verified',
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),tests_verified=7,
        source_nodes=9,source_primitives=4,source_material_instances=4,point_lights_cpu=1,
        native_exit_code=0,web_exit_code=0,web_source_files=len(web['input_pins']),
        shadow_mask_iou=comparison['shadow_mask_iou'],scene_fidelity_qualified=False,
        punctual_gpu_count_readback_verified=False,editor_visual_verified=False,full_import=False,
        complete_assets_delivered_increment=0,input_pins={str(p):sha(p) for p in files},
        next_action='Measure actual native cascade bounds, matrices and world texels against the source shadow packet; preserve original comparison thresholds.')
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
if __name__=='__main__':main()

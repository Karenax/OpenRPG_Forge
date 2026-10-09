"""Pin source culling inputs before a native shadow-context replay."""
from pathlib import Path
import datetime,hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-shadow-context-case-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(value):return struct.unpack('<f',struct.pack('<f',value))[0]
def main():
    assert not ROOT.exists()
    cap=W/'firepit-source-shadow-culling-v1';run=read(cap/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    prep=read(cap/'PREPARED.v1.json')
    for p,h in prep['input_pins'].items():assert sha(p)==h,p
    rows=[read(cap/f'SOURCE-CULLING-{i}.json') for i in range(2)];assert rows[0]==rows[1]
    source=rows[0];assert source['cascade_count']==4 and source['hdrp_shadow_distance_from_sdk_rule']>source['camera_near']
    distance=source['hdrp_shadow_distance_from_sdk_rule'];bounds=[f32(f32(x)*f32(distance)) for x in source['cascade_split_ratios']]+[f32(distance)]
    assert all(a<b for a,b in zip([source['camera_near'],*bounds[:-1]],bounds))
    gpu_path=W/'firepit-source-all-lights-gpu-reopen-v2/RESULT.v1.json';gpu=read(gpu_path)
    assert gpu['directional_resolution']==[512,512]
    old=W/'firepit-punctual-scene-case-v1';case=read(old/'CASE.v1.json');protocol=read(old/'RESULT.v1.json')
    assert sha(protocol['private_fixture'])==protocol['private_fixture_sha256']
    files=[Path(__file__).resolve(),cap/'RUN-RESULT.v1.json',cap/'PREPARED.v1.json',gpu_path,old/'CASE.v1.json',old/'RESULT.v1.json']
    for i in range(2):
        new=cap/f'images/{i}.png';original=W/f'firepit-shadow-source-reference-v8/images/{i}.png'
        assert sha(new)==sha(original);files.extend([cap/f'SOURCE-CULLING-{i}.json',new,original])
    hdrp=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'
    files.extend([hdrp/'Lighting/LightLoop/LightLoop.cs',hdrp/'Lighting/Shadow/HDShadowCullingUtils.cs'])
    context=dict(schema_version=1,cascade_bounds=bounds,minimum_distance=source['camera_near'],directional_map_size=512,
        source_cascade_borders=source['cascade_borders'],source_culling_inputs=source,
        native_overlap_proportion=0.0,source_border_transport_complete=False,
        source_filter=gpu['directional_filter_from_sdk'],native_filter='Gaussian (unchanged)',source_filter_transport_complete=False,
        native_bias_unchanged=True,shadow_matrices_equivalent=False)
    case['source_shadow_context_replay']=context
    ROOT.mkdir();(ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
    protocol.update(status='explicit_source_shadow_bounds_and_resolution_replay_prepared',finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        input_pins={**protocol['input_pins'],**{str(p):sha(p) for p in files}},source_shadow_context_replay=context,full_import=False)
    (ROOT/'RESULT.v1.json').write_text(json.dumps(protocol,indent=2),'utf-8')
    print(json.dumps({'root':str(ROOT),'bounds':bounds,'directional_map_size':512,'full_import':False}))
if __name__=='__main__':main()

"""Verify every GPU field against the SDK layout and unchanged reference images."""
from pathlib import Path
from datetime import datetime,timezone
import json,base64,struct,re,math,hashlib
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-directional-gpu-v1';ROOT=W/'firepit-source-directional-gpu-reopen-v1'
SDK=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Lighting'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');assert run['actual_exit_code']==0 and run['status']=='actual_source_directional_gpu_buffer_and_live_cbuffer_count_captured'
    for p,h in prep['input_pins'].items():assert sha(p)==h,p
    definition=SDK/'LightDefinition.cs';loop=SDK/'LightLoop/LightLoop.cs'
    body=re.search(r'struct DirectionalLightData\s*\{(.*?)\}\s*;',definition.read_text('utf-8'),re.S).group(1)
    fields=re.findall(r'public\s+(\w+)\s+(\w+)\s*;',body)
    formats=dict(Vector3='3f',Vector4='4f',float='f',int='i',uint='I',CookieMode='i')
    layout=[];stride=0
    for kind,name in fields:
        assert kind in formats,kind
        fmt='<'+formats[kind];layout.append(dict(name=name,kind=kind,offset=stride,format=fmt));stride+=struct.calcsize(fmt)
    assert stride==176 and len(fields)==26
    states=[];files=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',definition,loop]
    case_path=W/'firepit-source-ao-scene-case-v1/CASE.v1.json';case=read(case_path);files.append(case_path)
    expected_forward=[.8,-.4,.6];size=math.sqrt(sum(v*v for v in expected_forward));expected_forward=[v/size for v in expected_forward]
    for view in range(2):
        p=CAP/f'SOURCE-STATE-{view}.json';state=read(p);assert state['source_history_channel']=='CustomUserHistory0'
        count=state['directional_light_gpu_count'];raw=base64.b64decode(state['directional_light_gpu_base64'],validate=True);records=json.loads(state['directional_light_gpu_json'])
        assert count==1 and state['directional_light_gpu_stride']==stride and len(raw)==state['directional_light_buffer_count']*stride and len(records)==count
        for index,record in enumerate(records):
            assert set(record)=={f['name'] for f in layout}
            for f in layout:
                values=struct.unpack_from(f['format'],raw,index*stride+f['offset']);wanted=record[f['name']]
                if len(values)>1:wanted=[wanted[k] for k in ['x','y','z','w'][:len(values)]]
                else:wanted=[wanted]
                for actual,value in zip(values,wanted):assert math.isfinite(actual) and actual==value,(f['name'],actual,value)
            forward=[record['forward'][k] for k in ['x','y','z']]
            assert max(abs(a-b) for a,b in zip(forward,expected_forward))<1e-6
            assert all(record['color'][k]==case['source_light']['intensity'] for k in ['x','y','z'])
            assert record['cookieMode']==0 and record['contactShadowMask']==0
            assert all(record[k]==1 for k in ['lightDimmer','diffuseDimmer','specularDimmer','shadowDimmer'])
            assert record['shadowIndex']==(0 if view==0 else -1)
        image=CAP/f'linear-{view}.rgba16f';reference=W/f'firepit-shadow-source-reference-v8/linear-{view}.rgba16f';assert sha(image)==sha(reference)
        states.append(dict(view=view,active_lights=count,active_gpu_record_sha256=hashlib.sha256(raw[:count*stride]).hexdigest(),records=records,original_image_unchanged=True));files.extend([p,image,reference])
    stable=[k for k in states[0]['records'][0] if k!='shadowIndex']
    assert all(states[0]['records'][0][k]==states[1]['records'][0][k] for k in stable)
    result=dict(status='source_directional_gpu_fields_live_count_and_original_images_verified',finished_utc=datetime.now(timezone.utc).isoformat(),views=2,active_directional_lights_per_view=1,gpu_stride=stride,gpu_fields_per_record=len(fields),layout=layout,rows=states,source_color_matches_target_declared_illuminance=True,source_forward_matches_target_declared_direction_after_basis_mapping=True,source_dependencies_verified=24,target_direct_brdf_qualified=False,shadow_filter_qualified=False,full_import=False,editor_visual_verified=False,input_pins={str(p):sha(p) for p in files},next_action='Do not adjust illuminance or direction. Compare material/normal diagnostics and direct BRDF; retain source angular diameter and shadow settings for shadow-filter work.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','layout','rows']}))
if __name__=='__main__':main()

"""Verify all source GPU light/shadow layouts and identify missing case inputs."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,base64,struct,math
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-source-all-lights-gpu-v1'
ROOT=W/'firepit-source-all-lights-gpu-reopen-v2'
SDK=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Lighting'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def vector(value,n):return [value[k] for k in 'xyzw'[:n]]
def pack_light(row):
    layout=[('positionRWS',3,'f'),('lightLayers',1,'I'),('lightDimmer',1,'f'),('volumetricLightDimmer',1,'f'),('angleScale',1,'f'),('angleOffset',1,'f'),('forward',3,'f'),('iesCut',1,'f'),('lightType',1,'i'),('right',3,'f'),('penumbraTint',1,'f'),('range',1,'f'),('cookieMode',1,'i'),('shadowIndex',1,'i'),('up',3,'f'),('rangeAttenuationScale',1,'f'),('color',3,'f'),('rangeAttenuationBias',1,'f'),('cookieScaleOffset',4,'f'),('shadowTint',3,'f'),('shadowDimmer',1,'f'),('volumetricShadowDimmer',1,'f'),('useShadowMask',1,'i'),('minRoughness',1,'f'),('screenSpaceShadowIndex',1,'i'),('shadowMaskSelector',4,'f'),('size',4,'f'),('contactShadowMask',1,'i'),('diffuseDimmer',1,'f'),('specularDimmer',1,'f'),('__unused__',1,'f'),('padding',2,'f'),('isRayTracedContactShadow',1,'f'),('boxLightSafeExtent',1,'f')]
    return b''.join(struct.pack('<'+kind*n,*(vector(row[name],n) if n>1 else [row[name]])) for name,n,kind in layout)
def pack_shadow(row):
    layout=[('rot0',3),('rot1',3),('rot2',3),('pos',3),('proj',4),('atlasOffset',2),('worldTexelSize',1),('normalBias',1),('zBufferParam',4),('shadowMapSize',4),('shadowFilterParams0',4),('dirLightPCSSParams0',4),('dirLightPCSSParams1',4),('cacheTranslationDelta',3),('isInCachedAtlas',1)]
    values=[v for name,n in layout for v in (vector(row[name],n) if n>1 else [row[name]])]
    # Serialized eRC keys name row/column; native Matrix4x4 storage is column ordered.
    values += [row['shadowToWorld'][f'e{r}{column}'] for column in range(4) for r in range(4)]
    assert len(values)==60
    return struct.pack('<60f',*values)
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json')
    assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    assert len(prep['dependency_paths'])==24
    for p,h in prep['input_pins'].items():assert sha(p)==h,p
    files=[Path(__file__).resolve(),CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',SDK/'LightDefinition.cs',SDK/'Shadow/HDShadowManager.cs',SDK/'Shadow/HDShadowAlgorithms.hlsl']
    native=read(CAP/'NATIVE-RESULT.v1.json');lights=[];states=[];shadow_rows=[];json_zero_sign_differences=0
    for view in range(2):
        state_path=CAP/f'SOURCE-STATE-{view}.json';state=read(state_path);states.append(state);files.append(state_path)
        assert state['source_history_channel']=='CustomUserHistory0'
        for relative in [f'images/{view}.png',f'linear-{view}.rgba16f']:
            p=CAP/relative;reference=W/'firepit-shadow-source-reference-v8'/relative
            assert sha(p)==sha(reference);files.extend([p,reference])
        rows=json.loads(state['punctual_light_gpu_json']);raw=base64.b64decode(state['punctual_light_gpu_base64'])
        assert state['punctual_light_gpu_count']==len(rows)==1 and state['punctual_light_gpu_stride']==224
        assert len(raw)==state['punctual_light_gpu_buffer_count']*224
        assert pack_light(rows[0])==raw[:224]
        assert rows[0]['lightType']==1 and rows[0]['range']==1 and rows[0]['shadowIndex']==(4 if view==0 else -1)
        lights.append(rows[0])
        shadows=json.loads(state['shadow_gpu_json']);shadow_raw=base64.b64decode(state['shadow_gpu_base64'])
        assert state['shadow_gpu_stride']==240 and len(shadow_raw)==240*state['shadow_gpu_buffer_count']
        assert len(shadows)==state['shadow_gpu_live_count']==(10 if view==0 else 0)
        for index,row in enumerate(shadows):
            actual=struct.unpack('<60I',pack_shadow(row));expected=struct.unpack_from('<60I',shadow_raw,index*240)
            for a,b in zip(actual,expected):
                # JsonUtility writes -0 as 0. Retain the native bytes; permit
                # only this loss in JSON, never rounding or a nonzero mismatch.
                assert a==b or (a&0x7fffffff)==(b&0x7fffffff)==0,(index,hex(a),hex(b))
                json_zero_sign_differences+=int(a!=b)
        if view==0:shadow_rows=shadows
    without_shadow=dict(lights[0]);without_shadow['shadowIndex']=-1;assert without_shadow==lights[1]
    camera=native['images'][0]['camera_position'];relative=vector(lights[0]['positionRWS'],3)
    position=[relative[i]+camera[k] for i,k in enumerate('xyz')]
    assert max(abs(a-b) for a,b in zip(position,[0.,.33,0.]))<1e-6
    directional=struct.unpack('<26f',base64.b64decode(states[0]['directional_shadow_gpu_base64']))
    assert states[0]['directional_shadow_gpu_stride']==104 and all(math.isfinite(v) for v in directional)
    assert directional[19]==4.0
    spheres=[list(directional[i*4:i*4+4]) for i in range(4)]
    bounds=native['images'][0]['source_bounds_center'];object_relative=[bounds[k]-camera[k] for k in 'xyz']
    selected=next((i for i,sphere in enumerate(spheres) if sum((v-c)**2 for v,c in zip(object_relative,sphere[:3]))<sphere[3]),None)
    case_src=W/'firepit-scene-common-sampling-v1/src/main.rs';shader=W/'firepit-source-scene-instance-fix-case-v1/private-receiver-forward.wgsl'
    assert 'DirectionalLight{' in case_src.read_text('utf-8') and 'PointLight' not in case_src.read_text('utf-8')
    assert 'n_directional_lights' in shader.read_text('utf-8') and 'lighting::point_light(' in shader.read_text('utf-8')
    files.extend([case_src,shader])
    result=dict(status='source_punctual_light_and_shadow_records_verified_forge_case_omission_identified',finished_utc=datetime.now(timezone.utc).isoformat(),
        original_main_reference_images_unchanged=True,source_dependencies_verified=24,source_punctual_lights_verified=1,
        punctual_record_bytes_verified_per_view=224,punctual_record_scalar_slots_verified_per_view=56,
        punctual_world_position=position,punctual_linear_color=lights[0]['color'],punctual_range_m=1.0,
        punctual_shadow_index_with_without=[4,-1],punctual_record_changes_other_than_shadow_index=False,
        shadow_gpu_records_verified=10,shadow_gpu_bytes_verified=2400,directional_cascades_verified=4,punctual_shadow_faces_verified=6,
        shadow_json_signed_zero_differences=json_zero_sign_differences,shadow_json_nonzero_bit_differences=0,original_gpu_packet_bytes_preserved=True,
        directional_cascade_spheres=spheres,source_object_center_selected_cascade=selected,
        directional_world_texel_sizes=[row['worldTexelSize'] for row in shadow_rows[:4]],
        directional_resolution=[512,512],directional_filter_from_sdk='PCF tent 7x7 / 16 comparison taps (Medium)',
        forge_case_light_definition_omits_prefab_point_light=True,forge_receiver_shader_directional_only=False,forge_receiver_shader_point_loop_present=True,
        forge_runtime_punctual_count_readback_verified=False,case_omission_evidence_scope='case construction omits PointLight; receiver shader point loop exists; native GPU light count not read back',supersedes_incorrect_shader_loop_claim='firepit-source-all-lights-gpu-reopen-v1',
        full_import=False,complete_assets_delivered_increment=0,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in files},next_action='Preserve and admit the prefab point light in the common scene contract; implement punctual source lighting and shadow routes, then recompare original references.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()

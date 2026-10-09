"""Add the source prefab light via standard KHR transport; keep binary exact."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct,re,base64
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-punctual-scene-case-v1'
OLD=W/'firepit-source-scene-instance-fix-case-v1';CAP=W/'firepit-source-all-lights-gpu-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    evidence=read(W/'firepit-source-all-lights-gpu-reopen-v1/RESULT.v1.json')
    assert evidence['source_punctual_lights_verified']==1
    for p,h in evidence['input_pins'].items():assert sha(p)==h,p
    case=read(OLD/'CASE.v1.json');fixture=Path(case['fixture']);assert sha(fixture)==case['fixture_sha256']
    original=fixture.read_bytes();length=struct.unpack_from('<I',original,12)[0]
    doc=json.loads(original[20:20+length]);binary=original[28+length:]
    light_nodes=[i for i,node in enumerate(doc['nodes']) if 'UnityEngine.Light' in node.get('extras',{}).get('unity_source_node',{}).get('original_component_types',[])]
    assert len(light_nodes)==1;index=light_nodes[0]
    source=doc['nodes'][index]['extras']['unity_source_node'];identity=source['gameobject']
    prefab=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'/identity['asset_path']
    sections=[v for v in re.split(r'(?=^--- !u!)',prefab.read_text('utf-8-sig'),flags=re.M) if v.startswith('--- !u!108 ') and ('m_GameObject: {fileID: '+identity['file_id']+'}') in v]
    assert len(sections)==1;component_id=re.match(r'--- !u!108 &(-?\d+)',sections[0]).group(1)
    state=read(CAP/'SOURCE-STATE-0.json');gpu=json.loads(state['punctual_light_gpu_json'])[0]
    components=json.loads(state['prefab_light_components_json']);assert len(components)==1
    light=components[0]['light']['Light'];hd=components[0]['hd_light']['MonoBehaviour']
    assert light['m_Enabled'] and light['m_Type']==2 and hd['m_ApplyRangeAttenuation']
    assert gpu['lightType']==1 and gpu['cookieMode']==0 and gpu['contactShadowMask']==0
    assert gpu['rangeAttenuationScale']==gpu['rangeAttenuationBias']==1.
    color=[gpu['color'][k] for k in 'xyz'];intensity=max(color);assert intensity>0
    normalized=[v/intensity for v in color]
    metadata=dict(schema_version=1,enabled=light['m_Enabled'],shadow_maps_enabled=gpu['shadowIndex']>=0,
        contact_shadows_enabled=False,radius_m=gpu['size']['x'],
        component_identity=dict(guid=identity['guid'],file_id=component_id,unity_class_id=108),
        source_prefab_sha256=sha(prefab),source_capture_sha256=sha(CAP/'NATIVE-RESULT.v1.json'),
        source_gpu_record_sha256=hashlib.sha256(base64.b64decode(state['punctual_light_gpu_base64'])[:224]).hexdigest(),
        original_light=light,original_hd_light=hd,source_shadow_parameters_preserved=True,
        source_shadow_filter_matched=False,source_color_temperature_baked_from_gpu=True,renderer_parity=False)
    descriptor=dict(type='point',color=normalized,intensity=intensity,range=gpu['range'],extras={'OPENRPG_source_light':metadata})
    assert 'KHR_lights_punctual' not in doc.get('extensions',{})
    doc.setdefault('extensions',{})['KHR_lights_punctual']={'lights':[descriptor]}
    doc['nodes'][index].setdefault('extensions',{})['KHR_lights_punctual']={'light':0}
    for field in ['extensionsUsed','extensionsRequired']:
        if 'KHR_lights_punctual' not in doc.setdefault(field,[]):doc[field].append('KHR_lights_punctual')
    encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
    derived=b'glTF'+struct.pack('<II',2,28+len(encoded)+len(binary))+struct.pack('<I',len(encoded))+b'JSON'+encoded+struct.pack('<I',len(binary))+b'BIN\0'+binary
    ROOT.mkdir();out=ROOT/'private-source-punctual-scene.glb';out.write_bytes(derived)
    reread=out.read_bytes();new_len=struct.unpack_from('<I',reread,12)[0];assert reread[28+new_len:]==binary
    case.update(fixture=str(out),fixture_sha256=sha(out),private_punctual_light_transport=True,source_punctual_node=index,
        source_punctual_light=descriptor,source_punctual_shadow_filter_matched=False)
    (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
    (ROOT/'private-receiver-forward.wgsl').write_bytes((OLD/'private-receiver-forward.wgsl').read_bytes())
    files=[Path(__file__).resolve(),fixture,OLD/'CASE.v1.json',OLD/'private-receiver-forward.wgsl',prefab,CAP/'SOURCE-STATE-0.json',CAP/'NATIVE-RESULT.v1.json',W/'firepit-source-all-lights-gpu-reopen-v1/RESULT.v1.json']
    result=dict(status='original_source_punctual_light_khr_scene_case_prepared',created_utc=datetime.now(timezone.utc).isoformat(),
        private_fixture=str(out),private_fixture_sha256=sha(out),original_binary_bytes_unchanged=len(binary),
        source_light_node=index,source_component_identity=metadata['component_identity'],source_light_gpu_measured=True,
        geometry_shader_and_receiver_unchanged=True,shadows_source_metadata_preserved_native_filter_unqualified=True,
        full_import=False,complete_assets_delivered_increment=0,input_pins={str(p):sha(p) for p in files})
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()

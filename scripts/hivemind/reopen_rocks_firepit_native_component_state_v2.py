from pathlib import Path
import json,hashlib,struct,re,datetime
W=Path('F:/OpenRPG_ImportWork');N=W/'rocks-firepit-native-component-state-v1';ROOT=W/'rocks-firepit-native-component-state-reopen-v2'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.file_digest(Path(p).open('rb'),'sha256').hexdigest()
def f32(a,b):return struct.pack('<f',a)==struct.pack('<f',b)
def vector(a,b):return len(a)==len(b) and all(f32(x,y) for x,y in zip(a,b))
def main():
 assert not ROOT.exists();run=read(N/'RUN-RESULT.v1.json');assert run['actual_unity_exit_code']==0 and run['status']=='refused_or_unverified' and run['error']=="KeyError('root/0000')"
 pins=read(N/'PREPARED.v1.json')['input_pins']
 for path,h in pins.items():assert sha(path)==h,path
 actual=read(N/'NATIVE-RESULT.v1.json');assembly=read(W/'rocks-firepit-scene-bindings-v1/RESULT.v1.json');assert len(actual['prefabs'])==len(assembly['rows'])==2
 rows=[];light_script_resolution=[]
 for prefab,scene in zip(actual['prefabs'],assembly['rows']):
  assert prefab['identity']['guid']==scene['asset_id'].split('-')[-1]
  expected={e['source_node']['path']:e['source_node'] for e in scene['nodes']};groups={g['node_path']:g for g in scene['original_lod_groups']};assert len(expected)==len(prefab['nodes'])
  seen=set();native_groups=0;renderers=0;lights=0;missing_before=0;missing_after=0
  for node in prefab['nodes']:
   key=node['node_path'];assert key in expected and key not in seen;seen.add(key);old=expected[key];assert node['active_self']==old['active_self']
   missing_before+=old['missing_script_components'];missing_after+=node['missing_script_components'];assert len(node['renderers'])==len(old['renderers'])
   for renderer,prior in zip(node['renderers'],old['renderers']):
    identity=prior['component'];assert renderer['identity']['guid']==identity['guid'] and renderer['identity']['file_id']==identity['file_id']
    assert renderer['enabled']==prior['enabled'] and renderer['shadow_casting_mode']==prior['shadow_casting_mode'] and renderer['receive_shadows']==prior['receive_shadows'];renderers+=1
   has_group='UnityEngine.LODGroup' in node['component_types'];group=node.get('lod_group')
   if not has_group:
    assert key not in groups and group['identity']['guid']=='' and group['identity']['type']=='' and group['levels']==[]
   else:
    assert key in groups and group['identity']['type']=='UnityEngine.LODGroup' and group['identity']['guid'] and group['identity']['file_id'];old_group=groups[key];native_groups+=1
    assert f32(group['size'],old_group['size']) and vector(group['local_reference_point'],old_group['local_reference_point'])
    assert group['fade_mode']==old_group['fade_mode'] and group['animate_cross_fading']==old_group['animate_cross_fading'] and len(group['levels'])==len(old_group['levels'])
    for level,prior in zip(group['levels'],old_group['levels']):
     assert f32(level['screen_relative_height'],prior['screen_relative_height']) and f32(level['fade_transition_width'],prior['fade_transition_width'])
     assert [(x['guid'],x['file_id']) for x in level['renderers']]==[(x['guid'],x['file_id']) for x in prior['renderer_components']]
   lights+=len(node['lights'])
  assert seen==set(expected) and native_groups==len(groups)
  original=W/'hivemind-run/native-american-village/source'/prefab['asset_path'];scripts=set(re.findall(r'm_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([a-f0-9]{32})',original.read_text('utf-8')))
  for node in prefab['nodes']:
   for light in node['lights']:
    if light['identity']['type']!='UnityEngine.Rendering.HighDefinition.HDAdditionalLightData':continue
    script=next(v['reference'] for v in light['serialized'] if v['property_path']=='m_Script')
    assert script['guid'] in scripts and script['file_id']=='11500000' and script['path'].startswith('Packages/com.unity.render-pipelines.high-definition/')
    package=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2'
    file=package/script['path'].split('Packages/com.unity.render-pipelines.high-definition/',1)[1];assert file.exists() and file.with_suffix(file.suffix+'.meta').exists()
    assert ('guid: '+script['guid']) in file.with_suffix(file.suffix+'.meta').read_text('utf-8')
    pins[str(file)]=sha(file);pins[str(file.with_suffix(file.suffix+'.meta'))]=sha(file.with_suffix(file.suffix+'.meta'))
    light_script_resolution.append(dict(asset_id=scene['asset_id'],node_path=node['node_path'],original_prefab_script_guid=script['guid'],resolved_type=light['identity']['type'],package_script=str(file),package_script_sha256=sha(file),original_prefab_unchanged=True,light_runtime_qualified=False))
  rows.append(dict(asset_id=scene['asset_id'],nodes=len(expected),lod_groups=native_groups,lod_levels=sum(len(g['levels']) for g in groups.values()),renderers=renderers,light_components=lights,missing_scripts_prior_capture=missing_before,missing_scripts_current_native=missing_after))
 assert rows[0]['lod_groups']==13 and rows[1]['lod_groups']==1 and len(light_script_resolution)==1
 # Prove normalization of JSON float transport keeps strict IEEE-f32 equality.
 assert f32(0.3,0.30000001192092896)
 x=struct.pack('<f',0.3);bad=struct.unpack('<f',struct.pack('<I',struct.unpack('<I',x)[0]+1))[0];assert not f32(bad,0.3)
 for path,h in pins.items():assert sha(path)==h,path
 pins.update({str(p):sha(p) for p in [Path(__file__),N/'NATIVE-RESULT.v1.json',N/'RUN-RESULT.v1.json',N/'PREPARED.v1.json']})
 ROOT.mkdir();result=dict(status='native_lod_renderer_and_hdrp_light_identity_reopened',rows=rows,light_script_resolution=light_script_resolution,captured_context='loaded_prefab_asset_not_scene_instance',active_in_hierarchy_is_not_replacement_for_authored_parent_chain=True,json_null_lod_placeholder_not_admitted_as_component=True,lod_fields_f32_bits_exact=True,one_ulp_float_mutation_refused=True,captured_project_lod_bias=actual['lod_bias'],captured_project_maximum_lod_level=actual['maximum_lod_level'],original_source_and_private_prefab_configuration_pins_unchanged=True,input_pins=pins,native_camera_lod_decisions_measured=False,lod_runtime_qualified=False,light_runtime_qualified=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()

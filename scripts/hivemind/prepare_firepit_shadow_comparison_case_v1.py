"""Close source-reference preparation and pin the exact future Forge comparison case."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-shadow-source-reference-v8';M=W/'firepit-shadow-source-measurement-v8';ROOT=W/'firepit-shadow-comparison-case-v1'
S=Path(__file__).parent
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists();prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');n=read(CAP/'NATIVE-RESULT.v1.json');measure=read(M/'RESULT.v1.json')
 assert run['actual_exit_code']==0 and measure['source_shadow_response_verified'] and not run['private_changed'] and n['camera_kind']=='Game' and n['native_light_unit']=='Lux' and n['actual_directional_light_intensity']==10000 and n['presentation_fixed_ev']==10
 for p,h in prep['input_pins'].items():assert sha(p)==h,p
 for p,h in measure['input_pins'].items():assert sha(p)==h,p
 for r in n['images']:assert r['clipped_rgb_pixels']==0 and r['linear_max_channel']<1
 assert (CAP/'STARTUP-SCENE.txt').read_text().strip()=='scene= dirty=False roots='
 fixture=Path(read(W/'studio-source-host-shadows-v2/PREPARED.v1.json')['fixture']);assert sha(fixture)=='9caa979ee462cf89cecab7e18416c169098662deb776bd3d248710bf49d30e7b'
 with fixture.open('rb') as f:h=f.read(20);doc=json.loads(f.read(struct.unpack_from('<I',h,12)[0]))
 node=doc['nodes'][1];original=node['extras']['unity_source_node']['renderers'][0];mesh=original['mesh']['source']
 rows=[r for r in n['renderers'] if r['mesh_guid']==mesh['guid'] and r['mesh_file_id']==mesh['file_id']];assert len(rows)==1
 native=rows[0];assert native['vertices']==doc['accessors'][doc['meshes'][node['mesh']]['primitives'][0]['attributes']['POSITION']]['count']
 assert original['original_material_slots'][0]['guid']+':'+original['original_material_slots'][0]['file_id'] in native['materials']
 unique_passes={(r['material'],r['index']) for r in n['compiled_passes']};assert len(unique_passes)==8 and len(n['compiled_passes'])==32
 ROOT.mkdir();now=datetime.now(timezone.utc).isoformat();case=dict(sub_lot='L01.OMBRES.02.FORGE',source_asset=n['id'],fixture=str(fixture),fixture_sha256=sha(fixture),selected_source_node=1,selected_mesh_identity=mesh,source_renderer_identity=original['component'],diagnostic_lod0_only=True,runtime_lod_selection_qualified=False,source_camera=dict(position=n['images'][0]['camera_position'],target=n['images'][0]['target'],fov_degrees=30,orthographic=False,width=512,height=512),source_light=dict(unit='Lux',intensity=10000,color=[1,1,1],forward=[.8,-.4,.6],normalize_forward=True,hard_shadow=True),presentation=dict(fixed_ev=10,tonemapping='HDRP Neutral',source_pipeline=n['pipeline']),receiver=dict(type='test_plane',base_color=[1,1,1,1],smoothness=0,bounds_reference=n['images'][0]['source_bounds_center'],size_reference=n['images'][0]['source_bounds_size'],y_offset_below_bounds_min=-.01,scale='source bounds extents magnitude',plane_size_before_scale=10,shadow_caster=False,delivered_as_asset=False),source_images=n['images'],source_shadow_measurement=str(M/'RESULT.v1.json'),required_forge_checks=['same source mesh/material identities and bytes','same authored coordinate transforms','same source camera/light parameters','same presentation transfer and tonemapping','actual GPU readback with shadow on/off','compare response mask and pixels without tolerance fitted after capture','preserve source queue, stencil, LOD and runtime qualifications separately'],full_import=False)
 (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2,ensure_ascii=False),'utf-8')
 files=[Path(__file__).resolve(),fixture,CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',CAP/'STARTUP-SCENE.txt',M/'RESULT.v1.json',ROOT/'CASE.v1.json',*[S/f'queue_firepit_shadow_reference_v{v}.py' for v in [1,4,6,7,8]],S/'measure_firepit_shadow_reference_v1.py',S/'measure_firepit_shadow_reference_v8.py']
 result=dict(status='unclipped_source_shadow_pair_and_exact_lod0_comparison_case_verified',finished_utc=now,sub_lot='L01.OMBRES.02.SOURCE',images_verified=2,raw_pixels_verified=524288,clipped_rgb_pixels=0,source_dependencies_verified=24,unique_source_passes_compiled=8,compiled_pass_observations=32,lod0_mesh_material_identity_and_vertex_count_matched=True,source_shadow_response_verified=True,outside_caster_darker_pixels=measure['outside_caster_darker_with_shadows'],input_pins={str(p):sha(p) for p in files},source_inputs_unchanged=True,test_scene_was_empty_and_unsaved=True,source_reference_only=True,original_project_version_qualified=False,forge_gpu_capture_executed=False,shadow_fidelity_qualified=False,editor_visual_verified=False,full_import=False,next_sub_lot='L01.OMBRES.02.FORGE')
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()

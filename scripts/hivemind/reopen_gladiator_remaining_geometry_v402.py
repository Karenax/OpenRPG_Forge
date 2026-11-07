"""Reopen thirteen new native meshes and compare every root/LOD/material identity."""
from pathlib import Path
from datetime import datetime,timezone
import base64,hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-remaining-geometry-reopen-v402';P=W/'gladiator-remaining-native-geometry-v400'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def vec(a,b):
    assert set(a)==set(b)
    error=max(abs(a[k]-b[k]) for k in a)
    assert all(math.isfinite(a[k]) and math.isfinite(b[k]) and abs(a[k]-b[k])<=1e-5+1e-6*abs(b[k]) for k in a),(a,b)
    return error
assert not O.exists()
rp=P/'RUN-RESULT.v1.json';run=read(rp);assert run['status']=='remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending' and not run['source_changes']
pp=P/'PREPARED.v1.json';prep=read(pp);np=P/'NATIVE-RESULT.v1.json';assert sha(np)==run['native_receipt_sha256'];native=read(np)
bp=W/'gladiator-source-binding-bridge-v390/RESULT.v1.json';bridge=read(bp)
oldp=W/'gladiator-native-geometry-reopen-v381/RESULT.v1.json';old=read(oldp);assert old['status']=='four_native_lod_mesh_buffers_reopened'
helper=Path(__file__).with_name('reopen_gladiator_native_geometry_v381.py');text=helper.read_text('utf-8')
meshcode=text[text.index('meshes=[]; ids=set()'):text.index('assert len(meshes)==4 and len(native')]
compile(meshcode,str(helper)+'#unchanged-buffer-guard','exec')
pins={str(p):sha(p) for p in [rp,pp,np,bp,oldp,helper,Path(__file__)]}
for p,h in prep['input_pins'].items():assert sha(p)==h;pins[p]=h
cases=[];all_materials={}
assert len(native['cases'])==len(prep['cases'])==5
for request,case in zip(prep['cases'],native['cases']):
    assert request['prefab_guid']==case['prefab_guid'] and case['source_components_unchanged']
    cp=Path(request['output']);assert read(cp)==case;pins[str(cp)]=sha(cp)
    source=next(r for r in bridge['rows'] if r['prefab_guid']==case['prefab_guid']);nodes={n['path']:n for n in source['nodes']};bindings={b['path']:b for b in case['bindings']}
    assert len(bindings)==len(case['bindings'])==source['renderer_count']==request['expected_mesh_filters']
    root_errors={target:vec(case[target],nodes['root'][source_field]) for target,source_field in [('root_position','local_position'),('root_scale','local_scale'),('root_rotation','local_rotation')]}
    scope=dict(native=case,base64=base64,struct=struct,math=math);exec(compile(meshcode,str(helper)+'#unchanged-buffer-guard','exec'),scope)
    measured=scope['meshes']
    if request['include_geometry']:assert len(measured)==source['renderer_count']
    else:assert not measured # Ignore the historical geometry_exported default flag; request and actual arrays are authoritative.
    for path,b in bindings.items():
        node=nodes[path];renderer=node['renderer'];assert renderer and b['role']=='mesh_filter'
        assert (b['mesh']['guid'],b['mesh']['file_id'])==(renderer['mesh_guid'],renderer['mesh_file_id'])
        assert len(b['materials'])==len(renderer['material_slots'])
        for actual,expected in zip(b['materials'],renderer['material_slots']):assert actual['type']=='UnityEngine.Material' and (actual['guid'],actual['file_id'])==(expected['guid'],str(expected['fileID']))
        for field in ['local_position','local_rotation','local_scale']:vec(b[field],node[field])
        if measured:
            m=next(m for m in measured if m['identity']==b['mesh']);assert m['submeshes']==len(b['materials'])
    assert len(case['lod_groups'])==len(source['lod_groups'])==1
    actual_group=case['lod_groups'][0];expected_group=source['lod_groups'][0]
    assert actual_group['path']=='root' and len(actual_group['lods'])==len(expected_group['levels'])
    vec(actual_group['reference_point'],expected_group['reference_point']);assert abs(actual_group['size']-expected_group['size'])<=1e-5+1e-6*abs(expected_group['size'])
    for actual,expected in zip(actual_group['lods'],expected_group['levels']):
        assert expected['explicit_empty_renderer_slots']==0 and len(actual['renderers'])==len(expected['renderers'])==1
        assert abs(actual['screen_relative_height']-expected['screen_relative_height'])<=1e-6 and abs(actual['fade_width']-expected['fade_width'])<=1e-6
        for a,e in zip(actual['renderers'],expected['renderers']):assert a['type']=='UnityEngine.MeshRenderer' and (a['guid'],a['file_id'])==(source['prefab_guid'],e['source_renderer_file_id'])
    assert case['materials']
    for material in case['materials']:
        guid=material['identity']['guid']
        if guid in all_materials:assert all_materials[guid]==material
        all_materials[guid]=material
    cases.append(dict(prefab_guid=case['prefab_guid'],source_path=source['source_path'],native_case_path=str(cp),native_case_sha256=sha(cp),root_transform_max_errors=root_errors,renderer_bindings=len(bindings),geometry_captured_in_this_batch=request['include_geometry'],meshes=measured,geometry_default_flag_not_used_as_proof=True))
assert sum(len(c['meshes']) for c in cases)==13 and len(all_materials)==6
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='thirteen_new_native_meshes_and_all_five_root_lod_bindings_reopened',cases=cases,new_meshes=13,prior_four_meshes_reused=True,total_lod_bindings=17,native_root_transforms_verified=5,native_material_snapshots=list(all_materials.values()),private_importer_changes=run['private_importer_changes'],input_pins=pins,material_gpu_qualified=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],new_meshes=13,native_root_transforms_verified=5,total_lod_bindings=17,vertices=sum(m['vertices'] for c in cases for m in c['meshes']),triangles=sum(m['triangles'] for c in cases for m in c['meshes']))))

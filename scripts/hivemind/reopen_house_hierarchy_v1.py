"""Independently reopen native House identities, poses, hierarchy and counts."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,re
W=Path('F:/OpenRPG_ImportWork');SRC=W/'house-native-prefab-hierarchy-v2';ROOT=W/'house-native-prefab-hierarchy-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def finite(values,n):assert len(values)==n and all(isinstance(x,(int,float)) and math.isfinite(x) for x in values)
def mul(a,b):return [sum(a[r*4+k]*b[k*4+c] for k in range(4)) for r in range(4) for c in range(4)]
def close(a,b):assert all(abs(x-y)<=3e-5*max(1.,abs(x),abs(y)) for x,y in zip(a,b)),'Composed native matrix differs'
def trs(p,q,s):
    x,y,z,w=q;return [(1-2*(y*y+z*z))*s[0],2*(x*y-z*w)*s[1],2*(x*z+y*w)*s[2],p[0],2*(x*y+z*w)*s[0],(1-2*(x*x+z*z))*s[1],2*(y*z-x*w)*s[2],p[1],2*(x*z-y*w)*s[0],2*(y*z+x*w)*s[1],(1-2*(x*x+y*y))*s[2],p[2],0.,0.,0.,1.]
def identity(value):
    if value.get('is_null'):return
    assert value['exact_asset_identity_available'] and re.fullmatch('[0-9a-f]{32}',value['guid']) and re.fullmatch('-?[0-9]+',str(value['file_id']))
def main():
    assert not ROOT.exists();ROOT.mkdir();state=dict(status='independently_reopening_house_hierarchies',geometry_converted=False,material_qualified=False,gpu_rendered=False,studio_verified=False,full_import=False)
    try:
        run=read(SRC/'RUN-RESULT.v1.json');assert run['status']=='fifty_house_native_prefab_hierarchies_captured_independent_reopen_pending' and run['source_inputs_unchanged']
        native=read(SRC/'NATIVE-RESULT.v1.json');assert sha(SRC/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256'];assert native['total']==native['captured']==50 and native['failed']==0
        requests=read(SRC/'REQUEST.v1.json')['cases'];actual={c['expected_guid']:c for c in native['cases']};assert len(actual)==50 and set(actual)=={r['expected_guid'] for r in requests}
        pins=read(SRC/'PREPARED.v1.json')['input_pins'].copy()
        for p in [Path(__file__).resolve(),SRC/'RUN-RESULT.v1.json',SRC/'NATIVE-RESULT.v1.json',SRC/'REQUEST.v1.json',SRC/'PREPARED.v1.json']:pins[str(p)]=sha(p)
        for p,d in pins.items():assert sha(Path(p))==d,p
        rows=[];meshes=set();material_ids=set()
        for request in requests:
            case=actual[request['expected_guid']];assert not case['error'];identity(case['root']);assert case['root']['guid']==request['expected_guid'] and case['root']['asset_path']==request['asset_path'] and case['asset_id']==request['asset_id']
            nodes={};renderers=0;colliders=0;missing=0;static=0
            for node in case['nodes']:
                path=node['path'];parent=node['parent_path'];assert path not in nodes
                assert (not parent and path=='root') or parent in nodes
                for key,n in [('local_position',3),('local_rotation_xyzw',4),('local_scale',3),('source_local_matrix_row_major',16),('source_local_to_world_matrix_row_major',16)]:finite(node[key],n)
                close(trs(node['local_position'],node['local_rotation_xyzw'],node['local_scale']),node['source_local_matrix_row_major'])
                expected=mul(nodes[parent]['source_local_to_world_matrix_row_major'],node['source_local_matrix_row_major']) if parent else node['source_local_matrix_row_major'];close(expected,node['source_local_to_world_matrix_row_major'])
                assert isinstance(node['active_self'],bool) and isinstance(node['active_source_parent_chain'],bool)
                assert node['active_source_parent_chain']==(node['active_self'] and (nodes[parent]['active_source_parent_chain'] if parent else True))
                identity(node['gameobject']);identity(node['transform']);missing+=node['missing_script_components'];colliders+=len(node['colliders'])
                for renderer in node['renderers']:
                    renderers+=1;identity(renderer['component']);assert isinstance(renderer['enabled'],bool)
                    for material in renderer['original_material_slots']:
                        identity(material)
                        if not material.get('is_null'):material_ids.add((material['guid'],str(material['file_id'])))
                    if renderer['static_mesh_adapter_applicable']:
                        static+=1;mesh=renderer['mesh'];identity(mesh['source']);assert mesh['vertices']>0;meshes.add((mesh['source']['guid'],str(mesh['source']['file_id'])))
                nodes[path]=node
            assert renderers==case['renderer_count'] and missing==case['missing_script_components']
            rows.append(dict(asset_id=case['asset_id'],prefab_guid=request['expected_guid'],nodes=len(nodes),renderers=renderers,static_renderers=static,colliders=colliders,missing_script_components=missing,lod_groups=len(case['lod_groups'])))
        for p,d in pins.items():assert sha(Path(p))==d,p
        state.update(status='fifty_house_native_hierarchies_independently_reopened',prefabs_reopened=50,nodes=sum(r['nodes'] for r in rows),renderers=sum(r['renderers'] for r in rows),static_renderers=sum(r['static_renderers'] for r in rows),unique_static_meshes=len(meshes),unique_material_identities=len(material_ids),colliders=sum(r['colliders'] for r in rows),missing_script_components=sum(r['missing_script_components'] for r in rows),rows=rows,input_pins=pins,source_inputs_unchanged=True,lod_decision_qualified=False)
    except Exception as exc:state.update(status='house_hierarchy_independent_reopen_refusal_retained',error=repr(exc))
    state['finished_utc']=datetime.now(timezone.utc).isoformat();(ROOT/'RESULT.v1.json').write_text(json.dumps(state,indent=2,allow_nan=False)+'\n','utf-8');print(json.dumps({k:v for k,v in state.items() if k not in ['rows','input_pins']}))
if __name__=='__main__':main()

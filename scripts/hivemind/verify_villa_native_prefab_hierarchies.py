"""Independently compare native prefab poses/bindings with all logical inputs."""
import json
import math
from pathlib import Path

from plan_villa_native_prefab_hierarchies import sha, snapshot, SOURCE
from reconcile_villa_scene_transforms import trs

WORK=Path('F:/OpenRPG_ImportWork')
PLAN=WORK/'villa-native-prefab-hierarchy-plan-v1/PLAN.v1.json'
CAPTURE=WORK/'villa-native-prefab-hierarchy-project-v1/NATIVE-HIERARCHY.v1.json'
ROOT=WORK/'villa-native-prefab-hierarchy-independent-reopen-v1'


def matrix_equal(native,expected_columns):
    assert len(native)==len(expected_columns)==16
    expected=[expected_columns[col*4+row] for row in range(4) for col in range(4)]
    return all(math.isfinite(a) and abs(a-b)<=5e-5*max(1,abs(a),abs(b))
               for a,b in zip(native,expected))


def review(alias,case):
    job,snapshot_sha=snapshot(Path(alias['declared_glb']))
    assert snapshot_sha==alias['original_snapshot_sha256']
    assert job['guid']==alias['prefab_guid']
    assert sha(SOURCE/job['source'])==job['source_sha256']
    paths={}
    nodes={}
    for node in case['nodes']:
        assert node['path'] not in paths,'Duplicate native hierarchy path'
        local=node['source_local_matrix_row_major']
        world=node['source_local_to_world_matrix_row_major']
        assert len(local)==len(world)==16 and all(math.isfinite(v) for v in local+world)
        parent=node['parent_path']
        if parent:
            assert parent in paths,'Native hierarchy is not parent-first'
            parent_world=paths[parent]['source_local_to_world_matrix_row_major']
            expected=[sum(parent_world[row*4+k]*local[k*4+col] for k in range(4))
                      for row in range(4) for col in range(4)]
        else:
            assert node['path']=='root','Unexpected native hierarchy root'
            expected=local
        assert all(abs(a-b)<=5e-5*max(1,abs(a),abs(b)) for a,b in zip(world,expected)), \
            'Native composed matrix differs from its parent/local poses'
        paths[node['path']]=node
        ref=node['transform']
        if ref['exact_asset_identity_available'] and ref['guid']==job['guid']:
            assert ref['file_id'] not in nodes,'Duplicate native source Transform identity'
            nodes[ref['file_id']]=node
    checked=0
    for key,original in job['transforms'].items():
        assert key in nodes,'Retained source Transform absent from native hierarchy: '+key
        assert matrix_equal(nodes[key]['source_local_matrix_row_major'],trs(original)), \
            'Native local pose differs from retained authored transform: '+key
        assert all(math.isfinite(v) for v in nodes[key]['source_local_to_world_matrix_row_major'])
        checked+=1
    matched=0
    for binding in alias['expected_source_renderer_bindings']:
        node=nodes[binding['source_transform_id']]
        candidates=[r for r in node['renderers'] if r['static_mesh_adapter_applicable']
                    and r['mesh']['source']['exact_asset_identity_available']
                    and r['mesh']['source']['guid']==binding['mesh_guid']
                    and r['mesh']['source']['file_id']==binding['mesh_file_id']]
        assert len(candidates)==1,'Source renderer/native mesh binding is absent or ambiguous'
        renderer=candidates[0]
        slots=renderer['original_material_slots']
        assert len(slots)==len(binding['material_guids']),'Original material slot count differs'
        assert all(not ref['is_null'] and ref['exact_asset_identity_available']
                   and ref['guid']==guid for ref,guid in zip(slots,binding['material_guids'])), \
            'Native material slot identity/order differs'
        matched+=1
    return {'asset_id':alias['asset_id'],'native_prefab_root':case['asset_id'],
            'source_transforms_reconciled':checked,'source_renderer_bindings_reconciled':matched,
            'native_world_matrices_observed':True,'shader_world_mapping_qualified':False}


def main():
    assert not ROOT.exists(),'Prior review preserved'
    plan=json.loads(PLAN.read_text('utf-8-sig'))
    capture=json.loads(CAPTURE.read_text('utf-8-sig'))
    assert capture['total']==capture['captured']==len(capture['cases'])==76 and capture['failed']==0
    roots={r['expected_guid']:r for r in capture['cases']}
    assert len(roots)==76
    pins={str(p):sha(p) for p in [Path(__file__).resolve(),PLAN,CAPTURE,
          Path(__file__).with_name('plan_villa_native_prefab_hierarchies.py'),
          Path(__file__).with_name('reconcile_villa_scene_transforms.py')]}
    for name,expected in plan['source_pins'].items():
        assert sha(Path(name))==expected
        pins[name]=expected
    rows,refusals=[],[]
    for alias in plan['logical_scene_bindings']:
        try:
            case=roots[alias['prefab_guid']]
            assert not case['error'] and case['root']['guid']==alias['prefab_guid']
            rows.append(review(alias,case))
        except Exception as error:
            refusals.append({'asset_id':alias['asset_id'],'reason':str(error)})
    assert len(rows)+len(refusals)==226
    assert len({r['asset_id'] for r in rows+refusals})==226
    changed=[name for name,expected in pins.items() if sha(Path(name))!=expected]
    ROOT.mkdir()
    result={'status':('226_native_prefab_pose_and_renderer_bindings_reopened'
                     if not refusals and not changed else 'native_pose_or_binding_disagreements_retained'),
            'expected_logical_scenes':226,'logical_scenes_reconciled':len(rows),
            'native_prefab_roots':76,'rows':rows,'refusals':refusals,'input_pins':pins,
            'changed_inputs':changed,'gpu_rendered':False,'studio_verified':False,'full_import':False}
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ('rows','input_pins')}))


if __name__=='__main__':
    main()

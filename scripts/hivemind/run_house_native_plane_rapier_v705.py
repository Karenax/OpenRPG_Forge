"""Verify native-plane candidates against untouched source controls and distinct native holdout rays."""
from pathlib import Path
parent=Path(__file__).with_name('run_house_rapier_mesh_queries_v686.py')
recipe=parent.read_text('utf-8-sig').replace('house-rapier-mesh-query-v686','house-rapier-native-plane-query-v705')
needle='assert len(cases) == 19'
setup='''
derived_path=W/'house-native-convex-candidate-v704/RESULT.v1.json'
derived=read(derived_path)
for p,h in derived['input_pins'].items():assert sha(p)==h,p
by_identity={(r['source_guid'],r['source_file_id']):r for r in derived['rows']}
candidate_pins={str(derived_path):sha(derived_path)}
for case in cases:
    reference=case['reference']
    if not reference['convex']:continue
    candidate=by_identity[(reference['source_guid'],reference['source_file_id'])]
    assert (candidate['source_mesh_guid'],candidate['source_mesh_file_id'])==(reference['mesh_guid'],reference['mesh_file_id'])
    assert candidate['source_cooking_options']==reference['cooking_options']
    assert sha(candidate['target_vertices'])==candidate['target_vertices_sha256']
    case['native_convex_vertices']=candidate['target_vertices']
    case['native_plane_holdout_rays']=candidate['holdout_rays']
    candidate_pins[candidate['target_vertices']]=candidate['target_vertices_sha256']
'''
assert recipe.count(needle)==1
recipe=recipe.replace(needle,needle+setup)
needle='let floats=words(glb.views[view]);'
replacement='let floats=if let Some(path)=case["native_convex_vertices"].as_str(){words(&std::fs::read(path).unwrap())}else{words(glb.views[view])};'
assert recipe.count(needle)==1
recipe=recipe.replace(needle,replacement)
needle='reference["rays"].as_array().unwrap().iter().enumerate()'
assert recipe.count(needle)==1
recipe=recipe.replace(needle,'reference["rays"].as_array().unwrap().iter().chain(case["native_plane_holdout_rays"].as_array().into_iter().flatten()).enumerate()')
recipe=recipe.replace('pins = {str(p): sha(p) for p in files}',
                      'files.append(parent)\npins = {str(p): sha(p) for p in files}\npins.update(candidate_pins)')
recipe=recipe.replace("sum(r['rays'] for r in measured['rows']) == 114",
                      "sum(r['rays'] for r in measured['rows']) == 17522")
recipe=recipe.replace('"rapier_constructor_only":true',
                      '"native_plane_candidate_used":convex,"independent_native_holdout_included":convex,"rapier_constructor_only":true')
exec(compile(recipe,str(parent)+'#native-plane-candidates-with-independent-rays-v705','exec'),globals())

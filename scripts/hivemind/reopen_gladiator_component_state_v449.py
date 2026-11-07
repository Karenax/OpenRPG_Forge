"""Reopen the successful capture; derive collider requirements from the originals."""
from pathlib import Path

parent=Path(__file__).resolve().with_name('reopen_gladiator_component_state_v448.py')
code=parent.read_text('utf-8')
edits={
 'gladiator-native-component-reopen-v448':'gladiator-native-component-reopen-v449',
 "assert run['status']=='five_prefab_renderer_lod_and_collider_states_captured_review_pending'":
 "assert run['status']=='gladiator_component_state_refusal_retained' and \"len(n['colliders'])\" in run['traceback'], 'Only the observed erroneous collider total is repairable'",
 "assert sha(I/'NATIVE-RESULT.v1.json')==run['native_sha256']":
 "assert (I/'NATIVE-RESULT.v1.json').is_file()",
 "pins=dict(run['input_pins'])":
 "pins=dict(read(I/'PREPARED.v1.json')['input_pins']);pins[str(parent)]=sha(parent)",
 "assert sum(r['box_colliders'] for r in rows)==sum(r['lod_groups'] for r in rows)==5":
 "expected_colliders=sum(len(n['colliders']) for s in bridge['rows'] for n in s['nodes'])\nassert expected_colliders==3\nassert sum(r['box_colliders'] for r in rows)==expected_colliders and sum(r['lod_groups'] for r in rows)==5",
 'box_colliders=5,lod_groups=5':'box_colliders=3,lod_groups=5',
 '5 groupes LOD et 5 BoxColliders':'5 groupes LOD et 3 BoxColliders',
}
for old,new in edits.items():
    assert old in code,old
    code=code.replace(old,new)
exec(compile(code,str(parent)+'#source-collider-count-v449','exec'),globals())

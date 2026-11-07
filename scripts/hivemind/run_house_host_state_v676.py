"""Apply House state through the current Studio host modules, preserving its absence of LOD groups."""
from pathlib import Path
BASE=Path(__file__).with_name('run_gladiator_host_state_v453.py')
code=BASE.read_text('utf-8-sig')
changes={
    'gladiator-current-host-state-v453':'house-current-host-state-v676',
    'gladiator-host-state-v453':'house-host-state-v676',
    'gladiator-runtime-state-glb-v452/RESULT.v1.json':'house-runtime-state-glb-v675/RESULT.v1.json',
    'assert_eq!(plan.lod_groups.len(),1);':'assert_eq!(plan.lod_groups.len(),0);',
    'world.get::<source_state::OriginalLodDriverPending>(root).is_some()':'world.get::<source_state::OriginalLodDriverPending>(root).is_none()',
    '"root_hidden_pending_lod_driver":true':'"root_hidden_test_fixture":true,"unnecessary_lod_driver_absent":true',
    "len(state['rows'])==5 and sum(r['renderers'] for r in state['rows'])==17 and sum(r['primitives'] for r in state['rows'])==24":"len(state['rows'])==24 and sum(r['renderers'] for r in state['rows'])==24 and sum(r['primitives'] for r in state['rows'])==26",
    'five_current_studio_host_state_and_hierarchy_world_checks_passed':'twentyfour_current_studio_host_state_and_hierarchy_world_checks_passed',
}
for old,new in changes.items():
    assert old in code,old
    code=code.replace(old,new)
code=code.replace("[Path(__file__),state_module,scene_module,", "[Path(__file__),BASE,state_module,scene_module,")
exec(compile(code,str(BASE)+'#house-no-source-lod-groups','exec'),globals())

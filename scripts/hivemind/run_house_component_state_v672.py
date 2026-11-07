"""Capture all House prefab renderer, activation, LOD and collider state with the owned reader."""
from pathlib import Path
S = Path(__file__).resolve().parent
BASE = S / 'run_gladiator_component_state_v447.py'
code = BASE.read_text('utf-8-sig')
start = code.index("bridge_path=W/'gladiator-source-binding-bridge-v390/RESULT.v1.json'")
end = code.index("old='string[] paths=",start)
setup = '''bridge_path=W/'house-scene-geometry-package-v667/RESULT.v1.json'
closure_path=W/'house-wire-native-storage-v668/HELPER-OWNERSHIP.v1.json'
bridge,audit=read(bridge_path),read(closure_path)
assert len(bridge['rows'])==24 and len(audit['helpers'])==37
assert {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
for h in audit['helpers']:assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
queue=read(W/'queue/house-on-a-hill/prefabs.json')
files=[]
paths=[]
for scene in bridge['rows']:
    relative=Path(scene['source_prefab']).relative_to(Path(queue['source_root'])).as_posix()
    assert relative.startswith('Assets/')
    paths.append(relative)
    for suffix in ['', '.meta']:
        source=Path(scene['source_prefab']+suffix)
        files.append(dict(source=str(source),relative_project_path=relative+suffix,sha256=sha(source)))
closure=dict(files=files)
'''
code=code[:start]+setup+code[end:]
changes={
    "O=W/'gladiator-native-component-state-v447'":"O=W/'house-native-component-state-v672'",
    "PROJECT=W/'gladiator-native-geometry-v380/project'":"PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'",
    'OpenRPGGladiatorComponentStateV447':'OpenRPGHouseComponentStateV672',
    'GladiatorComponentStateV447.cs':'HouseComponentStateV672.cs',
    "len(native['prefabs'])==5 and sum(len(n['renderers']) for p in native['prefabs'] for n in p['nodes'])==17":"len(native['prefabs'])==24 and sum(len(n['renderers']) for p in native['prefabs'] for n in p['nodes'])==24",
    "sum(len(n['colliders']) for p in native['prefabs'] for n in p['nodes'])==5":"sum(len(n['colliders']) for p in native['prefabs'] for n in p['nodes'])==24",
    'five_prefab_renderer_lod_and_collider_states_captured_review_pending':'twentyfour_house_prefab_component_states_captured_review_pending',
}
for old,new in changes.items():
    assert old in code,old
    code=code.replace(old,new)
anchor="O.mkdir();(O/'temp').mkdir();helper.write_text(code,'utf-8');pins[str(helper)]=sha(helper)"
replacement=anchor+'''
controlled=S/'candidates/HouseComponentStateV672.cs'
assert not controlled.exists()
controlled.write_text(code,'utf-8');assert sha(controlled)==sha(helper)
audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(controlled),sha256=sha(helper)))
(O/'HELPER-OWNERSHIP.v1.json').write_text(json.dumps(audit,indent=2),'utf-8')
for p in [BASE,controlled,O/'HELPER-OWNERSHIP.v1.json',W/'queue/house-on-a-hill/prefabs.json']:pins[str(p)]=sha(p)
'''
assert code.count(anchor)==1
code=code.replace(anchor,replacement)
exec(compile(code,str(BASE)+'#house-owned-component-capture','exec'),globals())

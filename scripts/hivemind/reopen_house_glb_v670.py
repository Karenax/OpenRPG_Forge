"""Independently reopen House GLBs against shaders, geometry, collision and native capture."""
from pathlib import Path
BASE = Path(__file__).with_name('reopen_gladiator_glb_v443.py')
code = BASE.read_text('utf-8-sig')
edits = {
    'gladiator-glb-independent-reopen-v443':'house-glb-independent-reopen-v670',
    'gladiator-autonomous-glb-v442/RESULT.v1.json':'house-autonomous-glb-v669/RESULT.v1.json',
    'gladiator-scene-geometry-package-v441/RESULT.v1.json':'house-scene-geometry-package-v667/RESULT.v1.json',
    'gladiator-render-contracts-v432/RESULT.v1.json':'house-native-pass-states-v664/RESULT.v1.json',
    "contracts = {r['material_guid']:r for r in contracts['rows']}":"contracts = {r['material_guid']:dict(**r['original_source_contract'],name=r['material_guid'],native_resolved_state=r['native_resolved_state'],native_render_queue=r['native_render_queue']) for r in contracts['rows']}",
    'assert len(rows)==5 and sum(r[\'primitives_verified\'] for r in rows)==41':"expected_primitives=sum(len(n['geometry']['primitives']) for s in scene_proof['rows'] for n in s['nodes'] if 'geometry' in n)\nassert len(rows)==24 and sum(r['primitives_verified'] for r in rows)==expected_primitives",
    'five_autonomous_glbs_independently_reopened_against_pinned_sources':'twentyfour_house_glbs_independently_reopened_against_pinned_sources',
    'glbs=5,primitives=41':'glbs=24,primitives=expected_primitives',
}
for old,new in edits.items():
    assert old in code,old
    code=code.replace(old,new)
anchor="    assert doc['scenes'][doc['scene']]['nodes']==[paths['root']]"
extra='''    assert doc['extras']['original_component_snapshots']==scene['component_snapshots']
    assert doc['extras']['unassigned_mesh_colliders']==scene['unassigned_mesh_colliders']
    assert doc['extras']['source_quality_anisotropy']=='ForceEnable'
    expected_colliders=[(n['source_node']['path'],g) for n in scene['nodes'] for g in n['collider_geometry']]
    saved=doc['extras']['preserved_collider_buffers']
    assert len(saved)==len(expected_colliders)
    for retained,(node_path,g) in zip(saved,expected_colliders):
        assert retained['node_path']==node_path and retained['identity']==g['identity']
        assert retained['primitives']==g['primitives']
        for semantic,desc in g['attributes'].items():
            assert bytes(blob(retained['attributes'][semantic]))==Path(desc['path']).read_bytes()
        for stored,p in zip(retained['index_buffers'],g['primitives']):
            assert bytes(blob(stored))==Path(p['index_path']).read_bytes()
'''
assert code.count(anchor)==1
code=code.replace(anchor,extra+anchor)
start=code.index("note = '''## Gladiator")
end=code.index("(O/'LOT.md').write_text",start)
note="""note = '''## House — 24 GLB candidats créés et réouverts (v670)

- 24 fichiers autonomes sur F:/OpenRPG_ImportWork/house-autonomous-glb-v669 : 880 584 636 octets.
- Hiérarchies, géométrie de rendu, attributs source, shaders, PNG/.meta, textures BC et mipmaps comparés aux sources épinglées. Buffers de collision et composants originaux conservés et réouverts.
- Recapture Unity v668 terminée avec code 0 : 18 textures restaurées, aucune modification des sources/importeurs détectée.
- Preuve : F:/OpenRPG_ImportWork/house-glb-independent-reopen-v670/RESULT.v1.json.
- Candidats privés : admission Rust, rendu GPU, collisions runtime et disponibilité Studio restent à vérifier. Incrément de conversion complète : 0.

'''
"""
code=code[:start]+note+code[end:]
code=code.replace("rows = []", "pins[str(BASE)]=sha(BASE)\nrows = []",1)
exec(compile(code,str(BASE)+'#house-independent-reopen','exec'),globals())

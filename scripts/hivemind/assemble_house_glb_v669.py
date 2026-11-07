"""Assemble private House GLBs from native evidence; renderer/Studio admission remains separate."""
from pathlib import Path
S = Path(__file__).resolve().parent
BASE = S / 'assemble_gladiator_glb_v442.py'
code = BASE.read_text('utf-8-sig')
edits = {
    'gladiator-autonomous-glb-v442':'house-autonomous-glb-v669',
    'gladiator-scene-geometry-package-v441/RESULT.v1.json':'house-scene-geometry-package-v667/RESULT.v1.json',
    "scenes['prefabs'] == 5 and scenes['meshes'] == 17":"scenes['prefabs'] == 24 and scenes['meshes'] == 42",
    'gladiator-native-lit-delivery-v440/RESULT.v1.json':'house-native-lit-checkpoint-v663/RESULT.v1.json',
    "contracts = {r['material_guid']: r for r in load('gladiator-render-contracts-v432/RESULT.v1.json')['rows']}":"contracts = {r['material_guid']:dict(**r['original_source_contract'],name=r['material_guid'],native_resolved_state=r['native_resolved_state'],native_render_queue=r['native_render_queue']) for r in load('house-native-pass-states-v664/RESULT.v1.json')['rows']}",
    "kernels = {r['material_guid']: r for r in load('gladiator-material-kernels-v419/RESULT.v1.json')['rows']}":"load('house-source-material-package-v647/RESULT.v1.json')\nkernels = {r['material_guid']: r for r in load('house-source-material-package-v647/MATERIAL-PACKAGE.v1.json')['materials']}",
    'gladiator-native-stages-v434/RESULT.v1.json':'house-native-stages-v655/RESULT.v1.json',
    "load('gladiator-all-texture-gpu-delivery-v427/RESULT.v1.json')":"load('house-resolved-native-bc-comparison-v646/RESULT.v1.json')",
    "load('gladiator-native-texture-seal-v413/RESULT.v1.json')":"load('house-normal-decode-gpu-v614/RESULT.v1.json')",
    'gladiator-native-texture-storage-v412/NATIVE-RESULT.v1.json':'house-wire-native-storage-v668/NATIVE-RESULT.v1.json',
    "'RGBA_DXT5_UNorm': 'bc3_rgba_unorm',":"'RGBA_DXT5_UNorm': 'bc3_rgba_unorm', 'RGBA_DXT5_SRGB':'bc3_rgba_unorm_srgb',",
    "assert not t['error']":"assert not t.get('error')",
    "t['whole_file']":"t['whole_path']",
    "m['file']":"m['path']",
    "texture_by_guid[binding['guid']]":"texture_by_guid[binding['texture_guid']]",
    "png = next(f for f in binding['source_files'] if f['path'].endswith('.png'))\n            meta = next(f for f in binding['source_files'] if f['path'].endswith('.meta'))":"png = dict(path=binding['source'],sha256=sha(binding['source']))\n            meta = dict(path=binding['source']+'.meta',sha256=sha(binding['source']+'.meta'))",
    "if binding['requires_normal_decode']:":"if 'normal_mode_'+str(slot) in k['required_inputs']:\n                assert k['required_inputs']['normal_mode_'+str(slot)] == 2",
    "assert len(results) == 5 and sum(r['meshes'] for r in results) == 17":"assert len(results) == 24 and sum(r['meshes'] for r in results) == 24",
    'OpenRPG source-preserving Gladiator conversion':'OpenRPG source-preserving House conversion',
    'five_autonomous_source_program_glb_candidates_created':'twentyfour_autonomous_source_program_glb_candidates_created',
    'glbs=5, meshes=17':'glbs=24, meshes=24',
}
for old,new in edits.items():
    assert old in code,old
    code=code.replace(old,new)
anchor='    doc = dict(asset=dict('
colliders='''    collider_buffers=[]
    for entry in scene['nodes']:
        for geometry in entry['collider_geometry']:
            attrs={}
            for semantic,desc in geometry['attributes'].items():
                attrs[semantic]=embed(desc['path'],desc['sha256'])
            indices=[embed(p['index_path']) for p in geometry['primitives']]
            collider_buffers.append(dict(node_path=entry['source_node']['path'],identity=geometry['identity'],attributes=attrs,primitives=geometry['primitives'],index_buffers=indices,actual_runtime_admission=False))
'''
assert code.count(anchor)==1
code=code.replace(anchor,colliders+anchor)
code=code.replace("original_lod_groups=scene['original_lod_groups'], source_frame_binding=dict(", "original_lod_groups=scene['original_lod_groups'], original_component_snapshots=scene['component_snapshots'], unassigned_mesh_colliders=scene['unassigned_mesh_colliders'], preserved_collider_buffers=collider_buffers, source_quality_anisotropy='ForceEnable', source_frame_binding=dict(")
code=code.replace('O.mkdir()\nresults = []', 'pins[str(BASE)]=sha(BASE)\nO.mkdir()\nresults = []')
exec(compile(code,str(BASE)+'#house-native-capture-and-colliders','exec'),globals())

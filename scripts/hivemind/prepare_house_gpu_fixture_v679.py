"""Prepare a controlled real Bevy draw of an admitted nonzero-POM source material."""
from pathlib import Path
import hashlib
import json
import math
import struct
from bind_native_scene_render_state_v1 import load_json

R=Path(__file__).resolve().parents[2];W=Path('F:/OpenRPG_ImportWork');O=W/'house-controlled-gpu-fixture-v679'
BASE=R/'crates/forge_engine/examples/verify_source_glb_draw.rs'
TARGET=BASE.with_name('verify_source_material_fixture_v679.rs')
assert not O.exists() and not TARGET.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assembly_path=W/'house-runtime-state-glb-v675/RESULT.v1.json'
contract_path=W/'house-native-pass-states-v664/RESULT.v1.json'
assembly,contracts=map(read,[assembly_path,contract_path])
by_guid={r['material_guid']:r for r in contracts['rows']}
selected=None
for row in assembly['rows']:
    doc,offset,_=load_json(Path(row['glb']))
    if len(doc['materials'])!=1 or len(doc['meshes'])!=1 or len(doc['meshes'][0]['primitives'])!=1:continue
    guid=doc['materials'][0]['extras']['source_guid']
    amplitude=by_guid[guid]['original_source_contract']['source_floats']['_PomHeight']
    if amplitude<=0:continue
    selected=(row,doc,offset,guid,amplitude);break
assert selected
row,doc,offset,guid,amplitude=selected
node=next(n for n in doc['nodes'] if 'mesh' in n)
matrix=node['extras']['target_world_matrix']
accessor=doc['accessors'][doc['meshes'][0]['primitives'][0]['attributes']['POSITION']]
view=doc['bufferViews'][accessor['bufferView']]
with Path(row['glb']).open('rb') as f:
    f.seek(offset+8+view.get('byteOffset',0));positions=f.read(view['byteLength'])
points=[]
for x,y,z in struct.iter_unpack('<fff',positions):
    points.append([matrix[r]*x+matrix[4+r]*y+matrix[8+r]*z+matrix[12+r] for r in range(3)])
lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
center=[(a+b)*.5 for a,b in zip(lo,hi)]
radius=max(math.dist(p,center) for p in points);assert radius>0
camera=[center[0]+radius*1.8,center[1]+radius*1.2,center[2]+radius*2.2]
text=BASE.read_text('utf-8-sig')
edits={
    'assert_eq!(contract.textures.len(), 10);':'assert!(!contract.textures.is_empty());',
    '.into_iter()\n    .map(|h| h.clone().expect("Every authored texture is required"))':'.into_iter()\n    .take(contract.textures.len())\n    .map(|h| h.clone().expect("Every authored texture is required"))',
    'for index in 0..3 {':'for index in 0..glb.document()["meshes"].as_array().unwrap().len() {',
    'Transform::IDENTITY,\n    ));':'Transform::from_matrix(Mat4::from_cols_array(&serde_json::from_value::<[f32;16]>(context["target_world_from_object"].clone())?)),\n    ));',
    'let reflection = Mat4::from_scale(Vec3::new(-1., 1., 1.));':'let reflection = Mat4::IDENTITY;',
    '"native_images":10,"native_mips":120':'"native_images":handles.len(),"native_mips":rows.iter().map(|r|r["mips"].as_u64().unwrap()).sum::<u64>()',
}
for old,new in edits.items():
    assert text.count(old)==1,old
    text=text.replace(old,new)
TARGET.write_text(text,'utf-8')
O.mkdir();(O/'temp').mkdir()
context=dict(fixture_kind='controlled_admission_draw_not_unity_scene_reference',anisotropic_filtering='ForceEnable',bounds_center=dict(zip('xyz',center)),camera_position=dict(zip('xyz',camera)),target_world_from_object=matrix,fov=45,near=max(.0001,radius*.01),far=max(100,radius*20),exposure_ev=0,light_intensity=1000)
context_path=O/'CONTROLLED-CONTEXT.v1.json';context_path.write_text(json.dumps(context,indent=2),'utf-8')
(O/'INPUT.v1.json').write_text(json.dumps(dict(glb=row['glb'],glb_sha256=row['sha256'],source_context=str(context_path)),indent=2),'utf-8')
pins={str(p):sha(p) for p in [Path(__file__),BASE,TARGET,assembly_path,contract_path,Path(row['glb']),context_path,O/'INPUT.v1.json',R/'Cargo.toml',R/'Cargo.lock']}
for crate in ['forge_engine','forge_assets','forge_core']:
    for p in (R/'crates'/crate).rglob('*'):
        if p.is_file() and (p.suffix=='.rs' or p.name=='Cargo.toml'):pins[str(p)]=sha(p)
(O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,example='verify_source_material_fixture_v679',prefab_guid=row['prefab_guid'],material_guid=guid,source_pom_height_cm=amplitude,camera_and_light_are_controlled_fixture=True,unity_scene_parity=False,gpu_rendered=False,full_import=False),indent=2),'utf-8')
print(json.dumps(dict(prepared=True,prefab=row['prefab_guid'],source_pom_height_cm=amplitude,root=str(O))))

"""Compare the existing box adapter to Unity controls; retain explicit refusal of mixed mesh shapes."""
from pathlib import Path
BASE=Path(__file__).with_name('run_gladiator_rapier_boxes_v475.py')
code=BASE.read_text('utf-8-sig')
edits={
    'gladiator-rapier-box-query-v475':'house-rapier-box-query-v683',
    'gladiator-native-box-metric-v471/NATIVE-RESULT.v1.json':'house-native-component-state-v681/NATIVE-RESULT.v1.json',
    'gladiator-runtime-state-glb-v452/RESULT.v1.json':'house-runtime-state-glb-v675/RESULT.v1.json',
}
for old,new in edits.items():
    assert old in code,old
    code=code.replace(old,new)
anchor='let doc=glb.document();'
projection='''let original_doc=glb.document();
 // Explicit shape-query projection only. No GLB is changed; this does not
 // admit or qualify the complete mixed-collider scene in the product.
 let mut projected=original_doc.clone();let mut excluded=0usize;
 for node in projected["nodes"].as_array_mut().unwrap(){
  let observations=node["extras"]["unity_source_node"]["native_collider_observations"].as_array_mut().unwrap();
  excluded+=observations.iter().filter(|o|o["identity"]["type"]!="UnityEngine.BoxCollider").count();
  observations.retain(|o|o["identity"]["type"]=="UnityEngine.BoxCollider");
 }
 let basis=Mat4::from_scale(Vec3::new(1.,1.,-1.));
 assert!(excluded>0);assert!(source_colliders::prepare(original_doc,basis).is_err(),"Mixed scene must remain refused by box-only adapter");
 let doc=&projected;'''
assert code.count(anchor)==1
code=code.replace(anchor,projection)
code=code.replace('"boxes":boxes.len(),','"boxes":boxes.len(),"excluded_mesh_observations":excluded,"mixed_scene_admission_refused":true,"box_query_projection_only":true,')
anchor='assembly=read(assembly_path)'
selection='''assembly=read(assembly_path)
checkpoint_path=W/'house-native-box-checkpoint-v682/RESULT.v1.json'
checkpoint=read(checkpoint_path)
for p,h in checkpoint['input_pins'].items():assert sha(p)==h,p
box_guids={b['source_guid'] for b in checkpoint['rows']}
assembly['rows']=[r for r in assembly['rows'] if r['prefab_guid'] in box_guids]
assert len(assembly['rows'])==3'''
assert code.count(anchor)==1
code=code.replace(anchor,selection)
code=code.replace('files=[Path(__file__),module,','files=[Path(__file__),BASE,checkpoint_path,module,')
exec(compile(code,str(BASE)+'#house-explicit-box-query-boundary','exec'),globals())

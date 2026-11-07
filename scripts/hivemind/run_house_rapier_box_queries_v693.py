"""Check the three complete box-only prefab documents, without a shape projection."""
from pathlib import Path

parent=Path(__file__).with_name('run_house_rapier_box_queries_v691.py')
recipe=parent.read_text('utf-8-sig').replace('v691','v693')
anchor="code=code.replace('*.5','*0.5')"
repair='''
start=code.index('let original_doc=glb.document();')
end=code.index('let native:serde_json::Value',start)
code=code[:start]+'let doc=glb.document(); '+code[end:]
fields='"excluded_mesh_observations":excluded,"mixed_scene_admission_refused":true,"box_query_projection_only":true,'
assert code.count(fields)==1
code=code.replace(fields,'"unmodified_complete_glb_document":true,')
'''
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,anchor+repair)
exec(compile(recipe,str(parent)+'#complete-box-prefabs-v693','exec'),globals())

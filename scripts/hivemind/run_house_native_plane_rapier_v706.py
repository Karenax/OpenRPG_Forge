"""Validate preserved source indices against source vertices, separately from the reconstructed hull."""
from pathlib import Path
parent=Path(__file__).with_name('run_house_native_plane_rapier_v705.py')
recipe=parent.read_text('utf-8-sig').replace('v705','v706')
anchor="exec(compile(recipe,str(parent)+'#native-plane-candidates-with-independent-rays-v706','exec'),globals())"
repair='''
needle='(*v as usize)<vertices.len()'
assert recipe.count(needle)==1
recipe=recipe.replace(needle,'(*v as usize)<(glb.views[view].len()/12)')
'''
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,repair+anchor)
exec(compile(recipe,str(parent)+'#source-and-candidate-index-domains-v706','exec'),globals())

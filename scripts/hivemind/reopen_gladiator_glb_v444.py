"""Use actual source submesh counts; 41 was a prior buffer-file count."""
from pathlib import Path

parent = Path(__file__).resolve().with_name('reopen_gladiator_glb_v443.py')
code = parent.read_text('utf-8')
code = code.replace('gladiator-glb-independent-reopen-v443', 'gladiator-glb-independent-reopen-v444')
old = "assert len(rows)==5 and sum(r['primitives_verified'] for r in rows)==41"
new = "expected_primitives=sum(len(n['geometry']['primitives']) for s in scene_proof['rows'] for n in s['nodes'] if 'geometry' in n)\nassert expected_primitives==24\nassert len(rows)==5 and sum(r['primitives_verified'] for r in rows)==expected_primitives"
assert code.count(old)==1
code = code.replace(old,new).replace('17 meshes, 41 primitives','17 meshes, 24 primitives').replace('glbs=5,primitives=41','glbs=5,primitives=expected_primitives')
code = code.replace("pins = {str(p):sha(p) for p in [assembly_path, scene_path, contract_path, Path(__file__)]}",
    "pins = {str(p):sha(p) for p in [assembly_path, scene_path, contract_path, Path(__file__), parent]}")
exec(compile(code,str(parent)+'#actual-submesh-count-v444','exec'),globals())

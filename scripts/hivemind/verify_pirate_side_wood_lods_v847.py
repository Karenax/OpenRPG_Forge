"""Reopen the side-panel LOD comparison using its own Wood01 diff proof."""
from pathlib import Path

recipe_path = Path(__file__).with_name('verify_pirate_held_wood_lods_v820.py')
recipe = recipe_path.read_text('utf-8')
recipe = recipe.replace('pirate-wood-lod-migration-v819', 'pirate-wood-side-lod-migration-v845')
recipe = recipe.replace('pirate-held-wood-lod-review-v820', 'pirate-side-wood-lod-review-v847')
anchor = "exec(compile(code, str(parent)+'#six-refused-lod-comparisons', 'exec'), globals())"
assert recipe.count(anchor) == 1
adapt = '''
start = code.index("# The historical proof")
end = code.index("rows=[]", start)
proof = """priorpath=W/'pirate-side-wood-serialization-v846/RESULT.v1.json';prior=read(priorpath)
assert prior['textures_keywords_and_other_serialized_values_unchanged'] and prior['stencil_references_unchanged']
assert prior['material_guid']=='abc00000000002303923814896909187'
for name,h in prior['input_pins'].items():
    assert sha(name)==h,name;pins[name]=h
helperpath=CAP/'PirateWoodLodMigrationV819.cs'
helper=helperpath.read_text('utf-8')
assert 'Added AO selector differs from shader default' in helper
assert 'Added property differs from current shader default' in helper
assert all(c['comparison_material_guid']==prior['material_guid'] for c in prep['cases'])
pins[str(helperpath)]=sha(helperpath)
"""
code = code[:start] + proof + code[end:]
code = code.replace("Path(__file__).with_name('run_pirate_wood_held_lods_v819.py')", "Path(__file__).with_name('run_pirate_wood_side_held_lods_v845.py')")
'''
recipe = recipe.replace(anchor, adapt + '\n' + anchor)
exec(compile(recipe, str(recipe_path)+'#wood01-diff-proof', 'exec'), globals())

"""Compare exact side-panel LODs, reusing the owned V819 helper unchanged."""
from pathlib import Path

parent_recipe = Path(__file__).with_name('run_pirate_wood_held_lods_v819.py')
recipe = parent_recipe.read_text('utf-8')
replacements = {
    'pirate-wood-held-frontier-v819': 'pirate-wood-side-held-frontier-v845',
    'pirate-helper-ownership-recheck-v790': 'pirate-helper-ownership-recheck-v839',
    "guid = 'abc00000000004592200770135964921'": "guid = 'abc00000000002303923814896909187'",
}
for before, after in replacements.items():
    assert before in recipe, before
    recipe = recipe.replace(before, after)

# Keep the helper filename and class at V819: the C# already selects material
# and LOD from the request. Only this new immutable capture root differs.
anchor = "exec(compile(code, str(parent)+'#two-refused-wood-lod-groups', 'exec'), globals())"
assert recipe.count(anchor) == 1
adapt = '''
code = code.replace("pirate-wood-lod-migration-v819", "pirate-wood-side-lod-migration-v845")
assert "guid='abc00000000004592200770135964921'" in code
code = code.replace("guid='abc00000000004592200770135964921'", "guid='abc00000000002303923814896909187'")
controller_anchor = "selected=[n for n in selected if n is not run_node]"
assert code.count(controller_anchor) == 1
controller_reuse = """assert 'assert not target.exists()' in run_code
assert 'shutil.copyfile(O / target.name, target)' in run_code
run_code = run_code.replace('assert not target.exists()', 'assert target.exists() and sha(target) == sha(O / target.name)')
run_code = run_code.replace('shutil.copyfile(O / target.name, target)', 'None')
"""
code = code.replace(controller_anchor, controller_reuse + controller_anchor)
'''
recipe = recipe.replace(anchor, adapt + '\n' + anchor)
exec(compile(recipe, str(parent_recipe)+'#owned-helper-side-panels', 'exec'), globals())

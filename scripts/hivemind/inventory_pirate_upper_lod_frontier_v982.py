"""Route only current unillustrated upper LODs with refreshed source constraints."""
from pathlib import Path

recipe_path = Path(__file__).with_name('inventory_pirate_upper_lod_frontier_v960.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    "'pirate-upper-lod-frontier-v960'": "'pirate-upper-lod-frontier-v982'",
    'pirate-source-lod-hierarchy-v959/RESULT.v1.json': 'pirate-source-lod-hierarchy-v981/RESULT.v1.json',
}.items():
    assert code.count(old) == 1, old
    code = code.replace(old, new)
code += '\n'
exec(compile(code, str(recipe_path)+'#current-frontier', 'exec'), globals())

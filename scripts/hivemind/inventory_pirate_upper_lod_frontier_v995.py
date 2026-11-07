"""Route the remaining upper LOD images against the current source pointer."""
from pathlib import Path

recipe_path = Path(__file__).with_name('inventory_pirate_upper_lod_frontier_v960.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    "'pirate-upper-lod-frontier-v960'": "'pirate-upper-lod-frontier-v995'",
    'pirate-source-lod-hierarchy-v959/RESULT.v1.json': 'pirate-source-lod-hierarchy-v994/RESULT.v1.json',
}.items():
    assert code.count(old) == 1, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#fourth-lot-current-frontier', 'exec'), globals())

"""Capture the final 19 currently eligible upper source LOD cards."""
from pathlib import Path

recipe_path = Path(__file__).with_name('run_pirate_upper_lod_images_v951.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-v951': 'pirate-upper-lod-images-v996',
    'pirate-upper-lod-frontier-v950': 'pirate-upper-lod-frontier-v995',
    'L08.IMAGE.PIRATE.UPPER_LOD.01': 'L08.IMAGE.PIRATE.UPPER_LOD.04',
    '[Path(__file__), ownership_path, frontier_path':
        "[recipe_path, Path(__file__), S/'inventory_pirate_upper_lod_frontier_v995.py', "
        "S/'inventory_pirate_upper_lod_frontier_v960.py', S/'inventory_pirate_upper_lod_frontier_v950.py', "
        "S/'audit_pirate_source_lod_hierarchy_v994.py', S/'audit_pirate_source_lod_hierarchy_v959.py', "
        'ownership_path, frontier_path',
}.items():
    assert code.count(old) == 1, old
    code = code.replace(old, new)
# All numeric batch constants in this recipe specify the admission bound and
# receipt counts; the native helper remains unchanged and keeps its guards.
assert code.count('24') == 6 and code.count('48') == 1
code = code.replace('24', '19').replace('48', '38')
exec(compile(code, str(recipe_path)+'#final-eligible-upper-lod-lot', 'exec'), globals())

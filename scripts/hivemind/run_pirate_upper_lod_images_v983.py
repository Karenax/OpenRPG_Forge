"""Capture the next unillustrated source LOD lot from the refreshed frontier."""
from pathlib import Path

recipe_path = Path(__file__).with_name('run_pirate_upper_lod_images_v951.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-v951': 'pirate-upper-lod-images-v983',
    'pirate-upper-lod-frontier-v950': 'pirate-upper-lod-frontier-v982',
    'L08.IMAGE.PIRATE.UPPER_LOD.01': 'L08.IMAGE.PIRATE.UPPER_LOD.03',
    '[Path(__file__), ownership_path, frontier_path':
        "[recipe_path, Path(__file__), S/'inventory_pirate_upper_lod_frontier_v982.py', "
        "S/'inventory_pirate_upper_lod_frontier_v960.py', S/'inventory_pirate_upper_lod_frontier_v950.py', "
        "S/'audit_pirate_source_lod_hierarchy_v981.py', S/'audit_pirate_source_lod_hierarchy_v959.py', "
        'ownership_path, frontier_path',
}.items():
    assert code.count(old) == 1, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#third-current-source-lod-lot', 'exec'), globals())

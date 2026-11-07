"""Start the second missing-upper-LOD lot from the hierarchy-aware frontier."""
from pathlib import Path

parent = Path(__file__).with_name('run_pirate_upper_lod_images_v951.py')
code = parent.read_text('utf-8')
for before, after in {
    'pirate-upper-lod-images-v951': 'pirate-upper-lod-images-v961',
    'pirate-upper-lod-frontier-v950': 'pirate-upper-lod-frontier-v960',
    'L08.IMAGE.PIRATE.UPPER_LOD.01': 'L08.IMAGE.PIRATE.UPPER_LOD.02',
    '[Path(__file__), ownership_path, frontier_path': '[parent,Path(__file__), ownership_path, frontier_path',
}.items():
    assert before in code
    code = code.replace(before, after)
exec(compile(code, str(parent)+'#second-hierarchy-aware-lot', 'exec'), globals())

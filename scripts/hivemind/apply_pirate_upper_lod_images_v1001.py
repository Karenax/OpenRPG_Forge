"""Index 19 reviewed source image cards with the existing guarded transaction."""
from pathlib import Path

recipe_path = Path(__file__).with_name('apply_pirate_upper_lod_images_v955.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-index-v955': 'pirate-upper-lod-images-index-v1001',
    'pirate-upper-lod-images-reviewed-v954': 'pirate-upper-lod-images-reviewed-v1000',
    'count==22': 'count==19',
    '[parent,Path(__file__).resolve(),HELPER,REVIEW]': '[parent,recipe_path,Path(__file__).resolve(),HELPER,REVIEW]',
}.items():
    assert old in code, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#fourth-reviewed-upper-lod-lot', 'exec'), globals())

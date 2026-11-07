"""Install the second reviewed upper-LOD image lot with the same private transaction."""
from pathlib import Path

recipe_path = Path(__file__).with_name('apply_pirate_upper_lod_images_v955.py')
code = recipe_path.read_text('utf-8')
for before, after in {
    'pirate-upper-lod-images-index-v955': 'pirate-upper-lod-images-index-v965',
    'pirate-upper-lod-images-reviewed-v954': 'pirate-upper-lod-images-reviewed-v964',
    'count==22': 'count==24',
    '[parent,Path(__file__).resolve(),HELPER,REVIEW]': '[parent,recipe_path,Path(__file__).resolve(),HELPER,REVIEW]',
}.items():
    assert before in code
    code = code.replace(before, after)
exec(compile(code, str(recipe_path)+'#second-reviewed-upper-lod-lot', 'exec'), globals())

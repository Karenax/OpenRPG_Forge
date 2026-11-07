"""Index the third visually reviewed source lot with the existing CAS transaction."""
from pathlib import Path

recipe_path = Path(__file__).with_name('apply_pirate_upper_lod_images_v965.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-index-v965': 'pirate-upper-lod-images-index-v988',
    'pirate-upper-lod-images-reviewed-v964': 'pirate-upper-lod-images-reviewed-v987',
    '[parent,recipe_path,Path(__file__).resolve(),HELPER,REVIEW]':
        '[parent,recipe_path,third_recipe_path,Path(__file__).resolve(),HELPER,REVIEW]',
}.items():
    assert old in code, old
    code = code.replace(old, new)
third_recipe_path = recipe_path
exec(compile(code, str(recipe_path)+'#third-reviewed-upper-lod-lot', 'exec'), globals())

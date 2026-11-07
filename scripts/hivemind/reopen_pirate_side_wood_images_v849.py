"""Decode the next four Wood01 LOD images after their own native comparison."""
from pathlib import Path

recipe_path = Path(__file__).with_name('reopen_pirate_held_wood_images_v830.py')
recipe = recipe_path.read_text('utf-8')
for before, after in {
    'pirate-wood-lod-migration-v819': 'pirate-wood-side-lod-migration-v845',
    'pirate-held-wood-lod-review-v820': 'pirate-side-wood-lod-review-v847',
    'pirate-held-wood-images-review-v830': 'pirate-side-wood-images-review-v849',
}.items():
    assert before in recipe
    recipe = recipe.replace(before, after)
exec(compile(recipe, str(recipe_path)+'#side-wood-four-variants', 'exec'), globals())

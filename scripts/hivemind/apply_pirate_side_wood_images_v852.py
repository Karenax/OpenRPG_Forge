"""Apply only the four reviewed Wood01 source thumbnails through the skill."""
from pathlib import Path

recipe_path = Path(__file__).with_name('apply_pirate_held_wood_images_v835.py')
recipe = recipe_path.read_text('utf-8')
recipe = recipe.replace('pirate-held-wood-images-index-v835', 'pirate-side-wood-images-index-v852')
recipe = recipe.replace('pirate-held-wood-images-seal-v834', 'pirate-side-wood-images-seal-v851')
exec(compile(recipe, str(recipe_path)+'#four-side-panel-source-cards', 'exec'), globals())

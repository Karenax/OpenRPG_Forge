"""Seal the four side-panel cards only after review, apply and native read PASS."""
from pathlib import Path

recipe_path = Path(__file__).with_name('seal_pirate_held_wood_delivery_v837.py')
recipe = recipe_path.read_text('utf-8')
replacements = {
    'pirate-held-wood-images-delivery-v837': 'pirate-side-wood-images-delivery-v854',
    'pirate-held-wood-images-index-v835': 'pirate-side-wood-images-index-v852',
    'pirate-held-wood-images-native-reader-v836': 'pirate-side-wood-images-native-reader-v853',
    'pirate-held-wood-images-seal-v834': 'pirate-side-wood-images-seal-v851',
    'before==570 and before-after==4 and after==566': 'before==566 and before-after==4 and after==562',
    '570 -> 566': '566 -> 562',
    'v819 a v837': 'v845 a v854',
}
for before, after in replacements.items():
    assert before in recipe, before
    recipe = recipe.replace(before, after)
exec(compile(recipe, str(recipe_path)+'#side-panel-delivery', 'exec'), globals())

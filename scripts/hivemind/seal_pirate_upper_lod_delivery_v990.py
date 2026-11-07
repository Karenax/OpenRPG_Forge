"""Seal the third delivered source image lot against the current reader and hashes."""
from pathlib import Path

third_delivery_recipe = Path(__file__).with_name('seal_pirate_upper_lod_delivery_v967.py')
code = third_delivery_recipe.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-delivery-v967': 'pirate-upper-lod-images-delivery-v990',
    'pirate-upper-lod-images-index-v965': 'pirate-upper-lod-images-index-v988',
    'pirate-upper-lod-images-native-reader-v966': 'pirate-upper-lod-images-native-reader-v989',
    'pirate-upper-lod-images-reviewed-v964': 'pirate-upper-lod-images-reviewed-v987',
    'pirate-upper-lod-images-v961': 'pirate-upper-lod-images-v983',
    'for c in catalog) == 469': 'for c in catalog) == 445',
    'missing_primary_images_before=493, missing_primary_images_after=469':
        'missing_primary_images_before=469, missing_primary_images_after=445',
    'pins[str(parent)] = sha(parent)':
        'pins[str(parent)] = sha(parent)\\npins[str(third_delivery_recipe)] = sha(third_delivery_recipe)',
}.items():
    assert old in code, old
    code = code.replace(old, new)
exec(compile(code, str(third_delivery_recipe)+'#third-source-delivery', 'exec'), globals())

"""Seal fourth-lot delivery without conflating images and faithful conversion."""
from pathlib import Path

recipe_path = Path(__file__).with_name('seal_pirate_upper_lod_delivery_v957.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-delivery-v957': 'pirate-upper-lod-images-delivery-v1003',
    'pirate-upper-lod-images-index-v955': 'pirate-upper-lod-images-index-v1001',
    'pirate-upper-lod-images-native-reader-v956': 'pirate-upper-lod-images-native-reader-v1002',
    'pirate-upper-lod-images-reviewed-v954': 'pirate-upper-lod-images-reviewed-v1000',
    'pirate-upper-lod-images-v951': 'pirate-upper-lod-images-v996',
    "== review['cards_verified'] == 22": "== review['cards_verified'] == 19",
    "== review['images_verified'] == 44": "== review['images_verified'] == 38",
    "len(run['refusals']) == 2": "len(run['refusals']) == 0",
    "for c in catalog) == 493": "for c in catalog) == 426",
    '22_upper_lod_source_previews_delivered': '19_upper_lod_source_previews_delivered',
    'cards_delivered=22, images_delivered=44': 'cards_delivered=19, images_delivered=38',
    'missing_primary_images_before=515, missing_primary_images_after=493': 'missing_primary_images_before=445, missing_primary_images_after=426',
    'pins[str(Path(__file__).resolve())]': 'pins[str(recipe_path)] = sha(recipe_path)\npins[str(Path(__file__).resolve())]',
}.items():
    assert old in code, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#fourth-lot-source-and-reader-delivery', 'exec'), globals())

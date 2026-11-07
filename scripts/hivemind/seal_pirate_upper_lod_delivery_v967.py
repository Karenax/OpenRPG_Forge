"""Seal second-lot delivery without claiming full conversion or GUI qualification."""
from pathlib import Path

parent = Path(__file__).with_name('seal_pirate_upper_lod_delivery_v957.py')
code = parent.read_text('utf-8')
for before, after in {
    'pirate-upper-lod-images-delivery-v957': 'pirate-upper-lod-images-delivery-v967',
    'pirate-upper-lod-images-index-v955': 'pirate-upper-lod-images-index-v965',
    'pirate-upper-lod-images-native-reader-v956': 'pirate-upper-lod-images-native-reader-v966',
    'pirate-upper-lod-images-reviewed-v954': 'pirate-upper-lod-images-reviewed-v964',
    'pirate-upper-lod-images-v951': 'pirate-upper-lod-images-v961',
    "== review['cards_verified'] == 22": "== review['cards_verified'] == 24",
    "== review['images_verified'] == 44": "== review['images_verified'] == 48",
    "len(run['refusals']) == 2": "len(run['refusals']) == 0",
    "for c in catalog) == 493": "for c in catalog) == 469",
    '22_upper_lod_source_previews_delivered': '24_upper_lod_source_previews_delivered',
    'cards_delivered=22, images_delivered=44': 'cards_delivered=24, images_delivered=48',
    'missing_primary_images_before=515, missing_primary_images_after=493': 'missing_primary_images_before=493, missing_primary_images_after=469',
    'pins[str(Path(__file__).resolve())]': 'pins[str(parent)] = sha(parent)\npins[str(Path(__file__).resolve())]',
}.items():
    assert before in code, before
    code = code.replace(before, after)
exec(compile(code, str(parent)+'#second-lot-source-and-reader-delivery', 'exec'), globals())

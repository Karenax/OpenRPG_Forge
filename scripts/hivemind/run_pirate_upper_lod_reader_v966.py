"""Verify second-lot search, metadata, licences and image bytes in the product reader."""
from pathlib import Path

parent_recipe = Path(__file__).with_name('run_pirate_upper_lod_reader_v956.py')
code = parent_recipe.read_text('utf-8')
for before, after in {
    'pirate-upper-lod-images-native-reader-v956': 'pirate-upper-lod-images-native-reader-v966',
    'pirate-upper-lod-images-index-v955': 'pirate-upper-lod-images-index-v965',
    "index['cards_updated']==22": "index['cards_updated']==24",
}.items():
    assert before in code
    code = code.replace(before, after)
exec(compile(code, str(parent_recipe)+'#24-second-lot-current-reader', 'exec'), globals())

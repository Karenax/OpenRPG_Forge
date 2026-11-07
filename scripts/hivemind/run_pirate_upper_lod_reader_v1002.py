"""Verify fourth-lot search, metadata, licences and image bytes in Rust."""
from pathlib import Path

recipe_path = Path(__file__).with_name('run_pirate_upper_lod_reader_v956.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-native-reader-v956': 'pirate-upper-lod-images-native-reader-v1002',
    'pirate-upper-lod-images-index-v955': 'pirate-upper-lod-images-index-v1001',
    "index['cards_updated']==22": "index['cards_updated']==19",
}.items():
    assert old in code, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#19-fourth-lot-current-reader', 'exec'), globals())

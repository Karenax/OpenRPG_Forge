"""Verify the third source image lot through the current native product reader."""
from pathlib import Path

third_reader_recipe = Path(__file__).with_name('run_pirate_upper_lod_reader_v966.py')
code = third_reader_recipe.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-native-reader-v966': 'pirate-upper-lod-images-native-reader-v989',
    'pirate-upper-lod-images-index-v965': 'pirate-upper-lod-images-index-v988',
}.items():
    assert old in code, old
    code = code.replace(old, new)
exec(compile(code, str(third_reader_recipe)+'#third-current-reader', 'exec'), globals())

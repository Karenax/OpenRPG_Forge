from pathlib import Path
parent=Path(__file__).with_name('run_pirate_lod_thumbnail_batch03_native_reader_v373.py')
code=parent.read_text("utf-8")
assert 'pirate-lod-thumbnail-batch03-native-reader-v373' in code
code=code.replace('pirate-lod-thumbnail-batch03-native-reader-v373','pirate-wood-lod-images-native-reader-v597')
assert 'pirate-lod-thumbnail-batch03-index-v372' in code
code=code.replace('pirate-lod-thumbnail-batch03-index-v372','pirate-wood-lod-images-index-v596')
assert "0<index['cards_updated']<=16" in code
code=code.replace("0<index['cards_updated']<=16","0<index['cards_updated']<=24")
exec(compile(code,str(parent)+"#wood-lod-image-lot", "exec"),globals())

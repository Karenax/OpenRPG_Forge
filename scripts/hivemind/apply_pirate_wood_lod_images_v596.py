from pathlib import Path
parent=Path(__file__).with_name('apply_pirate_lod_thumbnail_batch03_v372.py')
code=parent.read_text("utf-8")
assert 'pirate-lod-thumbnail-batch03-index-v372' in code
code=code.replace('pirate-lod-thumbnail-batch03-index-v372','pirate-wood-lod-images-index-v596')
assert 'pirate-lod-batch03-combined-review-v371' in code
code=code.replace('pirate-lod-batch03-combined-review-v371','pirate-wood-lod-images-sealed-v595')
assert '0<count<=16' in code
code=code.replace('0<count<=16','0<count<=24')
exec(compile(code,str(parent)+"#wood-lod-image-lot", "exec"),globals())

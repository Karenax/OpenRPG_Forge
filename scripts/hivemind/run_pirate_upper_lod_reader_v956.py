"""Verify the new upper-LOD thumbnails with the current product library reader."""
from pathlib import Path

recipe_path = Path(__file__).with_name('run_pirate_side_wood_native_reader_v853.py')
recipe = recipe_path.read_text('utf-8')
anchor = "recipe = recipe.replace(anchor, adapt + '\\n' + anchor)"
assert recipe.count(anchor) == 1
extra = """
code=code.replace('pirate-side-wood-images-native-reader-v853','pirate-upper-lod-images-native-reader-v956')
code=code.replace('pirate-side-wood-images-index-v852','pirate-upper-lod-images-index-v955')
assert "0<index['cards_updated']<=4" in code
code=code.replace("0<index['cards_updated']<=4", "index['cards_updated']==22")
"""
recipe = recipe.replace(anchor, 'adapt += '+repr(extra)+'\n'+anchor)
exec(compile(recipe, str(recipe_path)+'#22-upper-source-lods-current-reader', 'exec'), globals())

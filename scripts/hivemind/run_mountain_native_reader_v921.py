"""Verify Mountain searches, metadata and installed images in the product reader."""
from pathlib import Path
recipe_path=Path(__file__).with_name('run_pirate_side_wood_native_reader_v853.py')
recipe=recipe_path.read_text('utf-8')
anchor="recipe = recipe.replace(anchor, adapt + '\\n' + anchor)"
assert recipe.count(anchor)==1
adapt="""
code=code.replace('pirate-side-wood-images-native-reader-v853','mountain-reviewed-images-native-reader-v921')
code=code.replace('pirate-side-wood-images-index-v852','mountain-reviewed-images-index-v920')
code=code.replace("0<index['cards_updated']<=4", "0<index['cards_updated']<=44")
code=code.replace('hivemind.pirate-island','hivemind.mountain-ancient-mountain')
"""
recipe=recipe.replace(anchor,'adapt += '+repr(adapt)+'\n'+anchor)
exec(compile(recipe,str(recipe_path)+'#44-mountain-current-native-cards','exec'),globals())

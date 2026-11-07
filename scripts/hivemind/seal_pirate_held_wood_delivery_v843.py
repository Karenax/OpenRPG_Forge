from pathlib import Path
recipe = Path(__file__).with_name('seal_pirate_held_wood_delivery_v840.py')
code = recipe.read_text('utf-8').replace('pirate-held-wood-images-delivery-v840', 'pirate-held-wood-images-delivery-v843').replace('pirate-held-wood-images-native-reader-v838', 'pirate-held-wood-images-native-reader-v842').replace('v819 a v840', 'v819 a v843')
exec(compile(code, str(recipe)+'#non-shadowed-reader-delivery', 'exec'), globals())

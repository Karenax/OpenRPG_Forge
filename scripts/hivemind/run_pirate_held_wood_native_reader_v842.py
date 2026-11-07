"""Probe the delivery while avoiding a legacy local variable collision in pin collection."""
from pathlib import Path
recipe_836 = Path(__file__).with_name('run_pirate_held_wood_native_reader_v836.py')
recipe_838 = Path(__file__).with_name('run_pirate_held_wood_native_reader_v838.py')
code = recipe_838.read_text('utf-8').replace('pirate-held-wood-images-native-reader-v838', 'pirate-held-wood-images-native-reader-v842')
code = code.replace('inputs=[original,parent,Path(__file__).resolve(),INDEX,', 'inputs=[recipe_836,recipe_838,parent,Path(__file__).resolve(),INDEX,')
exec(compile(code, str(recipe_838)+'#non-shadowed-recipe-pins', 'exec'), globals())

"""Verify installed side-panel cards through the unchanged native library reader."""
from pathlib import Path

recipe_path = Path(__file__).with_name('run_pirate_held_wood_native_reader_v836.py')
recipe = recipe_path.read_text('utf-8')
anchor = "exec(compile(code, str(parent)+'#four-delivered-wood-lod-cards', 'exec'), globals())"
assert recipe.count(anchor) == 1
adapt = '''
code = code.replace('pirate-held-wood-images-native-reader-v836', 'pirate-side-wood-images-native-reader-v853')
code = code.replace('pirate-held-wood-images-index-v835', 'pirate-side-wood-images-index-v852')
before_digest = 'let digest=format!("{:x}",Sha256::digest(&bytes));'
after_digest = 'let digest=Sha256::digest(&bytes).iter().map(|byte|format!("{byte:02x}")).collect::<String>();'
assert before_digest in code
code = code.replace(before_digest, after_digest)
code = code.replace('asset-library-all-entries-native-audit-v2/target', 'firepit-static-glb-product-check-v2/target')
code = code.replace('inputs=[parent,Path(__file__).resolve(),INDEX,', 'inputs=[recipe_path,parent,Path(__file__).resolve(),INDEX,')
'''
recipe = recipe.replace(anchor, adapt + '\n' + anchor)
exec(compile(recipe, str(recipe_path)+'#current-sha-and-owned-reader', 'exec'), globals())

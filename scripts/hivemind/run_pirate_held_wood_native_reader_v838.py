"""Retain the legacy SHA formatting failure and probe with the current SHA array API."""
from pathlib import Path
original = Path(__file__).with_name('run_pirate_held_wood_native_reader_v836.py')
code = original.read_text('utf-8').replace('pirate-held-wood-images-native-reader-v836', 'pirate-held-wood-images-native-reader-v838')
anchor = "exec(compile(code, str(parent)+'#four-delivered-wood-lod-cards', 'exec'), globals())"
injection = '''before_digest='let digest=format!("{:x}",Sha256::digest(&bytes));'
after_digest='let digest=Sha256::digest(&bytes).iter().map(|byte|format!("{byte:02x}")).collect::<String>();'
assert before_digest in code
code=code.replace(before_digest,after_digest)
code=code.replace('asset-library-all-entries-native-audit-v2/target','firepit-static-glb-product-check-v2/target')
code=code.replace('inputs=[parent,Path(__file__).resolve(),INDEX,','inputs=[original,parent,Path(__file__).resolve(),INDEX,')
'''+anchor
assert anchor in code
code = code.replace(anchor, injection)
exec(compile(code, str(original)+'#current-sha-array-formatting', 'exec'), globals())

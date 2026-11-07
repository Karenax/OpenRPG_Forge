"""Reopen all third-lot PNGs and verify requested meshes against LOD0 controls."""
from pathlib import Path

recipe_path = Path(__file__).with_name('reopen_pirate_upper_lod_images_v963.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-v961': 'pirate-upper-lod-images-v983',
    'pirate-upper-lod-images-guard-v962': 'pirate-upper-lod-images-guard-v985',
    'pirate-upper-lod-images-review-v963': 'pirate-upper-lod-images-review-v986',
    "==len(prep['cases'])==39": "==len(prep['cases'])==37",
    'assert len(rows)==24 and len(controls)==15': 'assert len(rows)==24 and len(controls)==13',
    '[parent,Path(__file__),guardpath': '[parent,recipe_path,Path(__file__),guardpath',
}.items():
    assert old in code, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#third-lot-identities-and-images', 'exec'), globals())

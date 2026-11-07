"""Reopen the fourth lot's 38 PNGs and compare with independent LOD0 meshes."""
from pathlib import Path

recipe_path = Path(__file__).with_name('reopen_pirate_upper_lod_images_v963.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-v961': 'pirate-upper-lod-images-v996',
    'pirate-upper-lod-images-guard-v962': 'pirate-upper-lod-images-guard-v998',
    'pirate-upper-lod-images-review-v963': 'pirate-upper-lod-images-review-v999',
    "==len(prep['cases'])==39": "==len(prep['cases'])==29",
    'assert len(rows)==24 and len(controls)==15': 'assert len(rows)==19 and len(controls)==10',
    'cards_ready_for_review=24': 'cards_ready_for_review=19',
    'dict(cards=24,views=48': 'dict(cards=19,views=38',
    'twenty_four_upper_lod_variants_reopened': 'nineteen_upper_lod_variants_reopened',
    '[parent,Path(__file__),guardpath': '[parent,recipe_path,Path(__file__),guardpath',
}.items():
    assert code.count(old) == 1, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#fourth-lot-identities-and-images', 'exec'), globals())

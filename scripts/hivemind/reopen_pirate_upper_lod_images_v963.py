"""Verify all requested source meshes and images before second-lot visual review."""
from pathlib import Path

parent = Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch03_v370.py')
code = parent.read_text('utf-8')
for before, after in {
    'pirate-lod-thumbnail-batch03-v368': 'pirate-upper-lod-images-v961',
    'pirate-lod-thumbnail-batch03-guard-v369': 'pirate-upper-lod-images-guard-v962',
    'pirate-lod-thumbnail-batch03-review-v370': 'pirate-upper-lod-images-review-v963',
    "len(guard['admitted_ids'])==len(batch['cases'])==24": "len(guard['admitted_ids'])==len(batch['cases'])==len(prep['cases'])==39",
    'assert len(rows)==16 and len(controls)==8': 'assert len(rows)==24 and len(controls)==15',
    'cards_ready_for_review=16': 'cards_ready_for_review=24',
    'dict(cards=16,views=32': 'dict(cards=24,views=48',
    'sixteen_lod_variants_reopened': 'twenty_four_upper_lod_variants_reopened',
    'for p in [Path(__file__),guardpath': 'for p in [parent,Path(__file__),guardpath',
}.items():
    assert before in code
    code = code.replace(before, after)
exec(compile(code, str(parent)+'#second-upper-lod-identities-and-images', 'exec'), globals())

"""Reopen the upper-LOD receipts and all PNGs before visual admission."""
from pathlib import Path

parent = Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch03_v370.py')
code = parent.read_text('utf-8')
replacements = {
    'pirate-lod-thumbnail-batch03-v368': 'pirate-upper-lod-images-v951',
    'pirate-lod-thumbnail-batch03-guard-v369': 'pirate-upper-lod-images-guard-v952',
    'pirate-lod-thumbnail-batch03-review-v370': 'pirate-upper-lod-images-review-v953',
    "assert len(guard['admitted_ids'])==len(batch['cases'])==24 and not guard['cases_refused_source_parity']": "assert len(batch['cases'])==len(prep['cases'])==39 and len(guard['admitted_ids'])==37 and len(guard['cases_refused_source_parity'])==2",
    "assert case['id']==request['id'] and case['captured']": "assert case['id']==request['id']\n    if not case['captured']:\n        assert request['index_candidate'] and case['id'] in {r['id'] for r in guard['cases_refused_source_parity']}\n        continue\n    assert case['id'] in guard['admitted_ids']",
    'assert len(rows)==16 and len(controls)==8': 'assert len(rows)==22 and len(controls)==15',
    'cards_ready_for_review=16,cards_refused=0': 'cards_ready_for_review=22,cards_refused=2',
    'refusals=[],contact_sheets': "refusals=guard['cases_refused_source_parity'],contact_sheets",
    'dict(cards=16,views=32': 'dict(cards=22,views=44',
    'sixteen_lod_variants_reopened': 'twenty_two_upper_lod_variants_reopened',
    'for p in [Path(__file__),guardpath': 'for p in [parent,Path(__file__),guardpath',
}
for before, after in replacements.items():
    assert before in code, before
    code = code.replace(before, after)
exec(compile(code, str(parent)+'#upper-lod-identities-and-images', 'exec'), globals())

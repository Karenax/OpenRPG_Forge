"""Decode the four candidate LOD images after exact native material comparisons."""
from pathlib import Path
parent = Path(__file__).with_name('reopen_pirate_wood_lod_images_v594.py')
code = parent.read_text('utf-8').replace('pirate-wood-lod-migration-v591', 'pirate-wood-lod-migration-v819').replace('pirate-wood-lod-images-review-v594', 'pirate-held-wood-images-review-v830').replace('pirate-wood-lod-migration-review-v592', 'pirate-held-wood-lod-review-v820')
replacements = {
    "proof['groups_verified']==12": "proof['groups_verified']==2",
    "proof['lod_cases_verified']==36": "proof['lod_cases_verified']==6",
    "len(rows)==24 and len(controls)==12": "len(rows)==4 and len(controls)==2",
    "[Path(__file__),proofpath": "[parent,Path(__file__),proofpath",
    "range(0,len(rows),3)": "range(0,len(rows),2)",
    "rows[start:start+3]": "rows[start:start+2]",
    "twenty_four_wood_lod_variants_reopened_visual_review_pending": "four_retained_refusal_wood_lod_variants_reopened_visual_review_pending",
    "cards_ready_for_review=24": "cards_ready_for_review=4",
    "cards=24,views=48": "cards=4,views=8",
}
for before, after in replacements.items():
    assert before in code, before
    code = code.replace(before, after)
exec(compile(code, str(parent)+'#four-retained-refusal-variants', 'exec'), globals())

"""Reopen only guard-admitted captures; retain all palm material refusals."""
from pathlib import Path
parent = Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch03_v370.py')
code = parent.read_text('utf-8')
for old, new in [
    ('pirate-lod-thumbnail-batch03-v368', 'pirate-lod-thumbnail-batch06-v513'),
    ('pirate-lod-thumbnail-batch03-review-v370', 'pirate-lod-thumbnail-batch06-admitted-review-v517'),
    ('pirate-lod-thumbnail-batch03-guard-v369', 'pirate-lod-thumbnail-batch06-guard-v514'),
]:
    assert old in code
    code = code.replace(old, new)
old = "assert len(guard['admitted_ids'])==len(batch['cases'])==24 and not guard['cases_refused_source_parity']"
new = """assert len(batch['cases'])==24
admitted=set(guard['admitted_ids'])
refused={r['id'] for r in guard['cases_refused_source_parity']}
assert len(admitted)==15 and len(refused)==9 and not admitted & refused
assert admitted | refused == {r['id'] for r in prep['cases']}
assert all(r['id'] in admitted for r in prep['cases'] if not r['index_candidate'] and any(c['prefab_guid']==r['prefab_guid'] and c['index_candidate'] and c['id'] in admitted for c in prep['cases']))
refused_variants=[r for r in guard['cases_refused_source_parity'] if r['id'] in {c['id'] for c in prep['cases'] if c['index_candidate']}]
assert len(refused_variants)==6"""
assert code.count(old)==1
code=code.replace(old,new)
old="    assert case['id']==request['id'] and case['captured']"
assert code.count(old)==1
code=code.replace(old,old+"\n    if request['id'] not in admitted:continue")
code=code.replace('assert len(rows)==16 and len(controls)==8', 'assert len(rows)==10 and len(controls)==5')
code=code.replace("for p in [Path(__file__),guardpath", "for p in [parent,Path(__file__),guardpath")
code=code.replace("status='sixteen_lod_variants_reopened_visual_review_pending',cards_ready_for_review=16,cards_refused=0,rows=rows,refusals=[]", "status='ten_admitted_lod_variants_reopened_six_palm_variants_refused',cards_ready_for_review=10,cards_refused=6,rows=rows,refusals=refused_variants")
code=code.replace('dict(cards=16,views=32,lod_comparisons=comparisons)', 'dict(cards=10,views=20,refused_variants=6,lod_comparisons=comparisons)')
exec(compile(code,str(parent)+'#guard-admitted-subset-with-retained-refusals','exec'),globals())

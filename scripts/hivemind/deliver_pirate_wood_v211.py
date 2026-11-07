"""Seal the four inspected wood facades after exact native migration comparison."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');REVIEW=W/'pirate-wood-migration-review-v210'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not (REVIEW/'RESULT.v1.json').exists()
candidate=read(REVIEW/'CANDIDATES.v1.json');assert candidate['cards_ready_for_review']==4 and candidate['cards_refused']==0
for p,h in candidate['input_pins'].items():assert sha(p)==h,p
assert all(p['migration_exact'] and p['repeat_exact'] for r in candidate['rows'] for p in r['comparisons'])
notes={
 'SM_ShackFront4m_01':'Doorway and window openings fully visible in both views, weathered blue-grey paint and wood grain discernible; no clipping or error material.',
 'SM_ShackFront4m_02':'Solid timber panel with diagonal brace fully visible in both views, weathered blue-grey paint and wood grain discernible; no clipping or error material.',
 'SM_ShackFront6m_03':'Wide solid timber panel fully visible in both views, vertical supports and weathered blue-grey paint discernible; no clipping or error material.',
 'SM_ShackFront6m_05':'Wide facade and both window openings fully visible in both views, timber braces and weathered blue-grey paint discernible; no clipping or error material.'}
assert {Path(r['prefab_path']).stem for r in candidate['rows']}==set(notes)
base=S/'apply_pirate_thumbnail_batch_0113_0120_v177.py'
candidate['input_pins'].update({str(p):sha(p) for p in [Path(__file__),base,REVIEW/'CANDIDATES.v1.json']})
candidate.update(status='four_wood_facades_individually_inspected_and_exact_native_migration_verified',cards_verified=4,images_verified=8,
    agent_review=True,human_review=False,visual_notes=[dict(id=r['id'],views=[0,1],note=notes[Path(r['prefab_path']).stem]) for r in candidate['rows']],
    finished_utc=datetime.now(timezone.utc).isoformat())
(REVIEW/'RESULT.v1.json').write_text(json.dumps(candidate,indent=2),'utf-8')
text=base.read_text('utf-8-sig')
assert text.count('pirate-source-thumbnail-batch-0113-0120-index-v177')==1 and text.count('pirate-source-thumbnail-batch-0113-0120-review-v176')==2
text=text.replace('pirate-source-thumbnail-batch-0113-0120-index-v177','pirate-wood-migration-index-v211').replace('pirate-source-thumbnail-batch-0113-0120-review-v176','pirate-wood-migration-review-v210')
snapshot=REVIEW/'TRANSACTION-HELPER.v211.py';snapshot.write_text(text,'utf-8')
exec(compile(text,str(snapshot),'exec'),{'__name__':'__main__','__file__':str(snapshot)})

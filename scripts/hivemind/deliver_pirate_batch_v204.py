"""Seal four individually inspected source cards and apply the existing transaction."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');REVIEW=W/'pirate-source-thumbnail-batch-0121-0128-review-v203'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not (REVIEW/'RESULT.v1.json').exists()
candidate=read(REVIEW/'CANDIDATES.v1.json');assert candidate['cards_ready_for_review']==4 and candidate['cards_refused']==4
for p,h in candidate['input_pins'].items():assert sha(p)==h,p
notes={
    'SM_SeedDebris_01a':'Both low flat patches fully visible, light seed/debris surface distinguishable on dark background; no crop, pink error material or highlight clipping.',
    'SM_SeedDebris_01b':'Both low flat patches fully visible, clustered rounded blue-grey seeds discernible; no crop, pink error material or highlight clipping.',
    'SM_SeedDebris_01c':'Both low flat patches fully visible, elongated brown debris surface discernible; no crop, pink error material or highlight clipping.',
    'SM_ShackFront6m_01':'Front and reverse wall visible, doorway clear, timber boards and diagonal blue-grey trim discernible; no crop, pink error material or highlight clipping.'}
assert {Path(r['prefab_path']).stem for r in candidate['rows']}==set(notes)
base=S/'apply_pirate_thumbnail_batch_0113_0120_v177.py'
candidate['input_pins'].update({str(p):sha(p) for p in [Path(__file__),base,REVIEW/'CANDIDATES.v1.json']})
candidate.update(status='four_source_cards_individually_inspected_for_thumbnail_delivery',cards_verified=4,images_verified=8,
    agent_review=True,human_review=False,visual_notes=[dict(id=r['id'],views=[0,1],note=notes[Path(r['prefab_path']).stem]) for r in candidate['rows']],
    finished_utc=datetime.now(timezone.utc).isoformat())
(REVIEW/'RESULT.v1.json').write_text(json.dumps(candidate,indent=2),'utf-8')
text=base.read_text('utf-8-sig')
assert text.count('pirate-source-thumbnail-batch-0113-0120-index-v177')==1 and text.count('pirate-source-thumbnail-batch-0113-0120-review-v176')==2
text=text.replace('pirate-source-thumbnail-batch-0113-0120-index-v177','pirate-source-thumbnail-batch-0121-0128-index-v204').replace('pirate-source-thumbnail-batch-0113-0120-review-v176','pirate-source-thumbnail-batch-0121-0128-review-v203')
snapshot=REVIEW/'TRANSACTION-HELPER.v204.py';snapshot.write_text(text,'utf-8')
exec(compile(text,str(snapshot),'exec'),{'__name__':'__main__','__file__':str(snapshot)})

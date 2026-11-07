"""Seal the inspected rock preview and reuse the existing immutable transaction."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
REVIEW=W/'pirate-material-migration-review-v192'
INDEX=W/'pirate-material-migration-index-v193'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
candidate=read(REVIEW/'CANDIDATES.v1.json')
assert not (REVIEW/'RESULT.v1.json').exists() and not INDEX.exists()
assert candidate['cards_ready_for_review']==1 and candidate['cards_refused']==1
assert candidate['rows'][0]['id']=='hivemind.pirate-island.prefab-sm-rockstonefoliage-01a-cafc200c4640a834880dcb2aa14fd108'
assert all(p['migration_exact'] and p['repeat_exact'] for p in candidate['rows'][0]['comparisons'])
for p,h in candidate['input_pins'].items():assert sha(p)==h,p
base=S/'apply_pirate_thumbnail_batch_0113_0120_v177.py'
candidate['input_pins'].update({str(p):sha(p) for p in [Path(__file__),base,REVIEW/'CANDIDATES.v1.json']})
candidate.update(status='one_pirate_rock_preview_individually_inspected_and_native_comparison_verified',
    cards_verified=1,images_verified=2,agent_review=True,human_review=False,
    visual_notes=[dict(id=candidate['rows'][0]['id'],views=[0,1],
        note='Both rock views inspected on the EV8 contact sheet. Entire silhouette visible, grey-brown rock texture and surface relief discernible; no pink error material, uniform image or highlight clipping. Exact raw comparison covers these two thumbnail views only.')],
    finished_utc=datetime.now(timezone.utc).isoformat())
(REVIEW/'RESULT.v1.json').write_text(json.dumps(candidate,indent=2),'utf-8')
text=base.read_text('utf-8-sig')
assert text.count('pirate-source-thumbnail-batch-0113-0120-index-v177')==1
assert text.count('pirate-source-thumbnail-batch-0113-0120-review-v176')==2
text=text.replace('pirate-source-thumbnail-batch-0113-0120-index-v177','pirate-material-migration-index-v193')
text=text.replace('pirate-source-thumbnail-batch-0113-0120-review-v176','pirate-material-migration-review-v192')
snapshot=REVIEW/'TRANSACTION-HELPER.v193.py';snapshot.write_text(text,'utf-8')
exec(compile(text,str(snapshot),'exec'),{'__name__':'__main__','__file__':str(snapshot)})

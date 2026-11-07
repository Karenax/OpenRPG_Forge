"""Seal the inspected stable wall preview; preserve the already delivered rock."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
REVIEW=W/'pirate-material-migration-review-v197';INDEX=W/'pirate-wall-migration-index-v198'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
candidate=read(REVIEW/'CANDIDATES.v1.json')
assert not (REVIEW/'RESULT.v1.json').exists() and not INDEX.exists()
assert candidate['cards_ready_for_review']==2 and candidate['cards_refused']==0
wall='hivemind.pirate-island.prefab-sm-rockstonewall-01b-98712cb0fb71afe449c365d848640822'
candidate['already_delivered_ids']=[r['id'] for r in candidate['rows'] if r['id']!=wall]
candidate['rows']=[r for r in candidate['rows'] if r['id']==wall]
assert len(candidate['rows'])==1
assert all(p['migration_exact'] and p['repeat_exact'] for p in candidate['rows'][0]['comparisons'])
for p,h in candidate['input_pins'].items():assert sha(p)==h,p
base=S/'apply_pirate_thumbnail_batch_0113_0120_v177.py'
candidate['input_pins'].update({str(p):sha(p) for p in [Path(__file__),base,REVIEW/'CANDIDATES.v1.json']})
candidate.update(status='one_pirate_wall_preview_individually_inspected_and_stable_native_comparison_verified',
    cards_verified=1,images_verified=2,agent_review=True,human_review=False,
    visual_notes=[dict(id=wall,views=[0,1],note='Both EV8 wall views inspected. Complete irregular wall silhouette, grey-brown rock textures, separate stones and relief visible. No clipping, pink error material or uniform image. Extra fixture warmup followed by exact current/prior/current raw comparison in both views; the earlier unstable first capture remains retained.')],
    finished_utc=datetime.now(timezone.utc).isoformat())
(REVIEW/'RESULT.v1.json').write_text(json.dumps(candidate,indent=2),'utf-8')
text=base.read_text('utf-8-sig')
assert text.count('pirate-source-thumbnail-batch-0113-0120-index-v177')==1
assert text.count('pirate-source-thumbnail-batch-0113-0120-review-v176')==2
text=text.replace('pirate-source-thumbnail-batch-0113-0120-index-v177','pirate-wall-migration-index-v198')
text=text.replace('pirate-source-thumbnail-batch-0113-0120-review-v176','pirate-material-migration-review-v197')
snapshot=REVIEW/'TRANSACTION-HELPER.v198.py';snapshot.write_text(text,'utf-8')
exec(compile(text,str(snapshot),'exec'),{'__name__':'__main__','__file__':str(snapshot)})

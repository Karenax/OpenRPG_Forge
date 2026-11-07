"""Index sixteen individually reviewed source views with actual presentation EV."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S=Path(__file__).parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'house-source-thumbnail-batch-0033-0040-final-review-v1'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
candidate=json.loads((ROOT/'CANDIDATES.v1.json').read_text(encoding='utf-8'))
observations=[
 'Two complete views of a shallow terracotta dish with raised rim and visible worn surface and cracks.',
 'Two complete views of a flatter terracotta dish variant with a low rim and visible surface marks; source identity retained.',
 'Two complete views of a rake with a long grey handle and red-brown curved metal teeth; full silhouette visible.',
 'Two complete views of a large dark rock with brown mottling, fissures and blue highlights. Dark source appearance retained; fixed presentation EV4, no material retouch.',
 'Two complete views of a dark rounded rock with layered fissures and blue reflections. Dark source appearance retained; fixed presentation EV4.',
 'Two complete views of a small angular dark rock with diagonal fissures and blue highlights; fixed presentation EV4.',
 'Two complete views of the source rock variant named red, with a dark rough surface and blue highlights. Source variant name preserved; no claim that red coloration is visible.',
 'Two complete views of a spade with a long grey handle and a flat red-brown blade; full silhouette visible.',
]
assert len(candidate['rows'])==len(observations)==8
for p,h in candidate['input_pins'].items():assert sha(p)==h,p
for row,observation in zip(candidate['rows'],observations):
 row.update(observed=observation,agent_review=True,human_review=False)
pins=dict(candidate['input_pins']);pins[str(Path(__file__))]=sha(Path(__file__))
review=dict(status='eight_house_source_card_thumbnail_pairs_reopened_and_agent_reviewed',finished_utc=datetime.now(timezone.utc).isoformat(),sub_batch='L08.IMAGE.HOUSE.06',cards_verified=8,images_verified=16,cards_refused=0,rows=candidate['rows'],input_pins=pins,source_inputs_unchanged=True,presentation_only=True,original_version_parity=False,agent_review=True,human_review=False,images_installed_in_vault=False,forge_editor_qualified=False,full_import=False,prior_dark_and_clipped_views_retained=True,presentation_ev_by_card=[8,8,8,4,4,4,4,8])
path=ROOT/'RESULT.v1.json';assert not path.exists();path.write_text(json.dumps(review,indent=2),encoding='utf-8')
for old,new in [
 ('apply_house_images_0025_0032_v1.py','apply_house_images_0033_0040_v1.py'),
 ('run_house_images_0025_0032_native_v1.py','run_house_images_0033_0040_native_v1.py'),
 ('seal_house_images_0025_0032_v1.py','seal_house_images_0033_0040_v1.py'),
 ('record_house_registry_epoch_0025_0032_v1.py','record_house_registry_epoch_0033_0040_v1.py'),
]:
 target=S/new;assert not target.exists()
 q=(S/old).read_text(encoding='utf-8')
 q=q.replace('house-source-thumbnail-batch-0025-0032','house-source-thumbnail-batch-0033-0040').replace('L08.IMAGE.HOUSE.05','L08.IMAGE.HOUSE.06')
 q=q.replace('house-source-thumbnail-batch-0033-0040-review-v1','house-source-thumbnail-batch-0033-0040-final-review-v1')
 if 'registry_epoch' in old:
  q=q.replace('house-source-thumbnail-batch-0017-0024-delivery','house-source-thumbnail-batch-0025-0032-delivery').replace('house-registry-epoch-0025-0032','house-registry-epoch-0033-0040')
 compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
 subprocess.run([sys.executable,'-B',str(target)],check=True)

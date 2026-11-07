"""Index sixteen individually reviewed source views with actual presentation EV."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S=Path(__file__).parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'house-source-thumbnail-batch-0041-0048-review-v1'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
candidate=json.loads((ROOT/'CANDIDATES.v1.json').read_text(encoding='utf-8'))
observations=[
 "Two complete views of a red-brown and blue worn metal swing frame with angled uprights, top bar and side braces; seat is not part of this source prefab.",
 "Two views of a pair of separate red-brown curved hooks. Both hooks are visible; their original spacing and bounds are retained, so each occupies a small part of the source view.",
 "Two complete views of a flat rectangular grey wooden swing seat with visible grain and thickness.",
 "Two complete views of a light grey rectangular hollow trough with a thick rim and visible worn surface.",
 "Two complete views of an orange-brown watering pot with blue-green worn patches, handle, long spout and sprinkler head.",
 "Two complete views of a tall narrow grey wooden board with visible grain and rectangular section; source variant 01 retained.",
 "Two complete views of a grey wooden board with grain and rectangular section; source variant 02 retained.",
 "Two complete views of a narrow grey wooden board with grain and rectangular section; source variant 03 retained."
]
assert len(candidate['rows'])==len(observations)==8
for p,h in candidate['input_pins'].items():assert sha(p)==h,p
for row,observation in zip(candidate['rows'],observations):
 row.update(observed=observation,agent_review=True,human_review=False)
pins=dict(candidate['input_pins']);pins[str(Path(__file__))]=sha(Path(__file__))
review=dict(status='eight_house_source_card_thumbnail_pairs_reopened_and_agent_reviewed',finished_utc=datetime.now(timezone.utc).isoformat(),sub_batch='L08.IMAGE.HOUSE.07',cards_verified=8,images_verified=16,cards_refused=0,rows=candidate['rows'],input_pins=pins,source_inputs_unchanged=True,presentation_only=True,original_version_parity=False,agent_review=True,human_review=False,images_installed_in_vault=False,forge_editor_qualified=False,full_import=False,prior_dark_and_clipped_views_retained=False,presentation_ev_by_card=[8]*8)
path=ROOT/'RESULT.v1.json';assert not path.exists();path.write_text(json.dumps(review,indent=2),encoding='utf-8')
for old,new in [
 ('apply_house_images_0033_0040_v1.py','apply_house_images_0041_0048_v1.py'),
 ('run_house_images_0033_0040_native_v1.py','run_house_images_0041_0048_native_v1.py'),
 ('seal_house_images_0033_0040_v1.py','seal_house_images_0041_0048_v1.py'),
 ('record_house_registry_epoch_0033_0040_v1.py','record_house_registry_epoch_0041_0048_v1.py'),
]:
 target=S/new;assert not target.exists()
 q=(S/old).read_text(encoding='utf-8')
 q=q.replace('house-source-thumbnail-batch-0033-0040','house-source-thumbnail-batch-0041-0048').replace('L08.IMAGE.HOUSE.06','L08.IMAGE.HOUSE.07')
 q=q.replace('house-source-thumbnail-batch-0041-0048-review-v1','house-source-thumbnail-batch-0041-0048-final-review-v1')
 if 'registry_epoch' in old:
  q=q.replace('house-source-thumbnail-batch-0025-0032-delivery','house-source-thumbnail-batch-0033-0040-delivery').replace('house-registry-epoch-0033-0040','house-registry-epoch-0041-0048')
 q=q.replace('house-source-thumbnail-batch-0041-0048-final-review-v1','house-source-thumbnail-batch-0041-0048-review-v1')
 compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
 subprocess.run([sys.executable,'-B',str(target)],check=True)

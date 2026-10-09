"""Record the inspected native candidates and close their limited thumbnail scope."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'house-source-thumbnail-batch-0001-0008-v1';RETRY=W/'house-source-thumbnail-batch-0001-0003-exposure-v3';ROOT=W/'house-source-thumbnail-batch-0001-0008-review-v2'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,v):
 with Path(p).open('x',encoding='utf-8') as f:json.dump(v,f,indent=2,ensure_ascii=False,allow_nan=False)
def retain_review(p,v):
 if p.exists():assert read(p)==v
 else:write(p,v)
def main():
 assert not ROOT.exists();ROOT.mkdir();pins={str(Path(__file__).resolve()):sha(__file__)}
 for root in [CAP,RETRY]:
  run=read(root/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['source_inputs_unchanged']
  for p,d in read(root/'PREPARED.v1.json')['input_pins'].items():assert sha(p)==d,p;pins[p]=d
  for name in ['PREPARED.v1.json','RUN-RESULT.v1.json','NATIVE-RESULT.v1.json']:pins[str(root/name)]=sha(root/name)
 first=read(CAP/'NATIVE-RESULT.v1.json')['cases'];retry=read(RETRY/'NATIVE-RESULT.v1.json')['cases'];assert len(first)==8 and len(retry)==3
 rejected=[]
 for c in first[:3]:
  n=read(c['receipt']);rejected.append(dict(id=c['id'],native_receipt=c['receipt'],native_receipt_sha256=sha(c['receipt']),images=n['images'],reason='Both EV8 views visually inspected and rejected as too dark to identify the surface.'))
 retain_review(CAP/'VISUAL-REVIEW.v1.json',dict(status='five_cards_accepted_three_dark_cards_rejected',agent_review=True,human_review=False,cards_accepted=5,cards_rejected=3,rejected=rejected,full_import=False))
 observations=['Native grey-blue reflective cliff surface, complete silhouette and fissures visible in both EV1 views. Source shader/material appearance is preserved; physical realism or Forge shader parity is not asserted.']*3+['Complete cloud mesh with distinct cloud surface and transparent outline visible in both views.','Complete narrow green cypress, foliage and short trunk visible in both views.','Complete fence with pointed slats, grey wood grain and reverse supports visible.','Fence variant with missing slats, grey wood grain and reverse supports visible.','Fence variant with rounded end posts, grey wood grain and reverse supports visible.']
 rows=[]
 for i,c in enumerate(first):
  selected=retry[i] if i<3 else c;assert selected['captured'] and selected['id']==c['id'];n=read(selected['receipt']);assert n['id']==c['id'] and n['source_object_unchanged'] and n['presentation_only'] and n['preview_scene_isolated'] and n['gpu']!='Null';assert n['presentation_fixed_ev']==(1 if i<3 else 8) and n['presentation_tonemapping']=='Neutral'
  assert n['compiled_passes'] and all(p['compiled'] for p in n['compiled_passes']) and len(n['images'])==2
  pins[selected['receipt']]=sha(selected['receipt'])
  for image in n['images']:
   p=Path(image['path']);assert p.resolve().parent==(Path(selected['receipt']).parent/'images').resolve();data=p.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n' and data[12:16]==b'IHDR' and struct.unpack('>II',data[16:24])==(512,512);assert sha(p)==image['sha256'];assert image['clipped_rgb_pixels']==0 and image['linear_max_channel']<=1;pins[str(p)]=image['sha256']
  rows.append(dict(id=c['id'],native_receipt=selected['receipt'],native_receipt_sha256=sha(selected['receipt']),images=n['images'],observed=observations[i],agent_review=True,human_review=False))
 result=dict(status='eight_house_source_card_thumbnail_pairs_reopened_and_agent_reviewed',finished_utc=datetime.now(timezone.utc).isoformat(),sub_batch='L08.IMAGE.HOUSE.02',cards_verified=8,images_verified=16,rows=rows,input_pins=pins,previous_dark_views_retained=True,source_inputs_unchanged=True,presentation_only=True,original_version_parity=False,agent_review=True,human_review=False,images_installed_in_vault=False,forge_editor_qualified=False,full_import=False)
 write(ROOT/'RESULT.v1.json',result);print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','rows']}))
if __name__=='__main__':main()

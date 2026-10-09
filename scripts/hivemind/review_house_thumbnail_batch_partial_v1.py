"""Close five usable pairs; retain three cliff refusals without promoting them."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'house-source-thumbnail-batch-0001-0008-v1';ROOT=W/'house-source-thumbnail-batch-0001-0008-review-v3'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists();ROOT.mkdir();pins={str(Path(__file__).resolve()):sha(__file__)};prep=read(CAP/'PREPARED.v1.json');run=read(CAP/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['source_inputs_unchanged']
 for p,d in prep['input_pins'].items():assert sha(p)==d,p;pins[p]=d
 for name in ['PREPARED.v1.json','RUN-RESULT.v1.json','NATIVE-RESULT.v1.json','VISUAL-REVIEW.v1.json']:pins[str(CAP/name)]=sha(CAP/name)
 cases=read(CAP/'NATIVE-RESULT.v1.json')['cases'];assert len(cases)==8;rows=[];refusals=[]
 observations=['Cloud mesh, distinct cloud surface and transparent outline visible in both views.','Complete narrow green cypress, foliage and short trunk visible in both views.','Fence with pointed slats, grey wood grain and reverse supports visible.','Fence variant with missing slats, grey wood grain and reverse supports visible.','Fence variant with rounded end posts, grey wood grain and reverse supports visible.']
 for i,c in enumerate(cases):
  if i<3:
   attempts=[]
   for version in [2,3,4]:
    root=W/f'house-source-thumbnail-batch-0001-0003-exposure-v{version}';receipt=root/f'case-{i:02d}/NATIVE-RESULT.v1.json';n=read(receipt);assert n['id']==c['id'];pins[str(receipt)]=sha(receipt);attempts.append(dict(native_receipt=str(receipt),sha256=sha(receipt),ev=n['presentation_fixed_ev'],clipped_rgb_pixels=[r['clipped_rgb_pixels'] for r in n['images']],max_channel=[r['linear_max_channel'] for r in n['images']]))
   refusals.append(dict(id=c['id'],reason='EV8 and EV4 views are too dark for useful library identification; EV0/EV1 views are legible but have clipped highlights. All attempts retained; no cliff image indexed.',attempts=attempts));continue
  assert c['captured'];n=read(c['receipt']);assert n['id']==c['id'] and n['source_object_unchanged'] and n['presentation_only'] and n['preview_scene_isolated'];assert n['compiled_passes'] and all(p['compiled'] for p in n['compiled_passes']) and len(n['images'])==2;pins[c['receipt']]=sha(c['receipt'])
  for image in n['images']:
   p=Path(image['path']);assert p.resolve().parent==(Path(c['receipt']).parent/'images').resolve();data=p.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n' and data[12:16]==b'IHDR' and struct.unpack('>II',data[16:24])==(512,512);assert sha(p)==image['sha256'] and image['clipped_rgb_pixels']==0 and image['linear_max_channel']<=1;pins[str(p)]=image['sha256']
  rows.append(dict(id=c['id'],native_receipt=c['receipt'],native_receipt_sha256=sha(c['receipt']),images=n['images'],observed=observations[i-3],agent_review=True,human_review=False))
 result=dict(status='five_house_source_card_pairs_verified_three_cliff_pairs_refused',finished_utc=datetime.now(timezone.utc).isoformat(),sub_batch='L08.IMAGE.HOUSE.02',expected_cards=8,cards_verified=5,images_verified=10,cards_refused=3,refusals=refusals,rows=rows,input_pins=pins,source_inputs_unchanged=True,presentation_only=True,original_version_parity=False,agent_review=True,human_review=False,images_installed_in_vault=False,forge_editor_qualified=False,full_import=False)
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2,allow_nan=False),'utf-8');print(json.dumps(dict(cards_verified=5,images_verified=10,cards_refused=3)))
if __name__=='__main__':main()

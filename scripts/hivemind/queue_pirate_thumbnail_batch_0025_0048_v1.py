"""Capture the next unique LOD0 cards using the qualified private SDK bench."""
from pathlib import Path
from datetime import datetime,timezone
import sys,json,subprocess
import queue_house_thumbnail_batch_v1 as capture
W,V=capture.W,capture.V
ROOT=W/'pirate-source-thumbnail-batch-0025-0048-v1'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
MIGRATION=W/'pirate-private-material-migrations-reopen-v2/RESULT.v1.json'
PRIOR=W/'pirate-source-thumbnail-batch-0001-0024-v2/PREPARED.v1.json'
DELIVERY=W/'pirate-source-thumbnail-batch-0001-0024-delivery-reopen-v1/RESULT.v1.json'
def prepare():
 capture.no_unity();assert not ROOT.exists()
 migration=capture.read(MIGRATION);assert migration['source_inputs_unchanged']
 delivery=capture.read(DELIVERY);assert delivery['cards_updated']==22 and delivery['vault_delta_rehashed']
 pins=dict(migration['input_pins'])
 for p,h in pins.items():assert capture.sha(p)==h,p
 registry=capture.read(V/'metadata/asset-source-support-registry.v1.json');catalog_path=V/registry['path'];assert capture.sha(catalog_path)==registry['sha256']
 previous={c['id'] for c in capture.read(PRIOR)['cases']}
 selected=sorted([a for a in capture.read(catalog_path)['assets'] if a.get('pack_id')=='hivemind.pirate-island' and not a.get('preview') and a['id'] not in previous and a.get('original_lod_level')==0 and '/HDRP' in a.get('source_asset_path','') and a['source_asset_path'].endswith('.prefab')],key=lambda a:a['id'])[:24]
 assert len(selected)==24 and len({a['source_asset_path'] for a in selected})==24
 ROOT.mkdir();cases=[]
 for i,a in enumerate(selected):
  assert (PROJECT/a['source_asset_path']).is_file()
  for field,digest in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
   source=V/a[field];assert capture.sha(source)==a[digest];pins[str(source)]=a[digest]
  folder=ROOT/f'case-{i:02d}';folder.mkdir();cases.append(dict(id=a['id'],prefab_path=a['source_asset_path'],prefab_guid=a['source_guid'],output=str(folder/'NATIVE-RESULT.v1.json')))
 request=ROOT/'REQUEST.v1.json';capture.write(request,dict(cases=cases,output=str(ROOT/'NATIVE-RESULT.v1.json')))
 for p in [Path(__file__).resolve(),MIGRATION,PRIOR,DELIVERY,catalog_path,request]:pins[str(p)]=capture.sha(p)
 capture.write(ROOT/'PREPARED.v1.json',dict(sub_batch='L08.IMAGE.PIRATE.02',expected_cards=24,expected_images=48,cases=cases,input_pins=pins,catalog_revision=registry['revision'],private_sdk_migration_receipt=str(MIGRATION),private_bench_reused=True,original_lod_level=0,source_version_parity_qualified=False,presentation_only=True,source_assets_executed=False,prior_refused_cards_not_retried=True,full_import=False))
 with (ROOT/'controller.log').open('xb') as log:
  child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 capture.write(ROOT/'JOB.v1.json',dict(controller_pid=child.pid,created_utc=datetime.now(timezone.utc).isoformat()))
 print(json.dumps(dict(controller_pid=child.pid,lot='L08.IMAGE.PIRATE.02',expected_cards=24,expected_images=48,private_bench_reused=True,root=str(ROOT))))
if __name__=='__main__':
 if len(sys.argv)==1:prepare()
 else:capture.ROOT=ROOT;capture.PROJECT=PROJECT;capture.run()

"""Capture the next unique LOD0 cards using the qualified private SDK bench."""
from pathlib import Path
from datetime import datetime,timezone
import sys,json,subprocess
import queue_house_thumbnail_batch_v1 as capture
W,V=capture.W,capture.V
ROOT=W/'pirate-source-thumbnail-batch-0105-0112-v1'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
MIGRATION=W/'pirate-private-material-migrations-reopen-v5/RESULT.v1.json'
PRIOR=W/'pirate-source-thumbnail-batch-0001-0024-v2/PREPARED.v1.json'
DELIVERY=W/'pirate-source-thumbnail-batch-0097-0104-delivery-reopen-v2/RESULT.v1.json'
def prepare():
 capture.no_unity();assert not ROOT.exists()
 migration=capture.read(MIGRATION);assert migration['source_inputs_unchanged']
 delivery=capture.read(DELIVERY);assert delivery['cards_updated']==7 and delivery['vault_delta_rehashed']
 pins=dict(migration['input_pins'])
 for p,h in pins.items():assert capture.sha(p)==h,p
 registry=capture.read(V/'metadata/asset-source-support-registry.v1.json');catalog_path=V/registry['path'];assert capture.sha(catalog_path)==registry['sha256']
 previous={c['id'] for previous_root in ['pirate-source-thumbnail-batch-0001-0024-v2','pirate-source-thumbnail-batch-0025-0048-v1','pirate-source-thumbnail-batch-0049-0072-v1','pirate-source-thumbnail-batch-0073-0096-v1','pirate-source-thumbnail-batch-0097-0104-v1'] for c in capture.read(W/previous_root/'PREPARED.v1.json')['cases']}
 selected=sorted([a for a in capture.read(catalog_path)['assets'] if a.get('pack_id')=='hivemind.pirate-island' and not a.get('preview') and a['id'] not in previous and a.get('original_lod_level')==0 and '/HDRP' in a.get('source_asset_path','') and a['source_asset_path'].endswith('.prefab')],key=lambda a:a['id'])[:8]
 assert len(selected)==8 and len({a['source_asset_path'] for a in selected})==8
 ROOT.mkdir();cases=[]
 epoch=ROOT/'private-input-epoch';epoch.mkdir();snapshots=[]
 for name,h in list(pins.items()):
  source=Path(name)
  if source.is_relative_to(PROJECT) and source.suffix in {'.mat','.asset','.json','.cs','.shadergraph'}:
   target=epoch/source.relative_to(PROJECT);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(source.read_bytes());assert capture.sha(target)==h
   pins[str(target)]=h;snapshots.append(dict(live_path=name,snapshot=str(target),sha256=h))
 capture.write(ROOT/'PRIVATE-EPOCH.v1.json',dict(private_input_snapshots=snapshots,full_import=False))
 pins[str(ROOT/'PRIVATE-EPOCH.v1.json')]=capture.sha(ROOT/'PRIVATE-EPOCH.v1.json')
 for i,a in enumerate(selected):
  assert (PROJECT/a['source_asset_path']).is_file()
  for field,digest in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
   source=V/a[field];assert capture.sha(source)==a[digest];pins[str(source)]=a[digest]
  folder=ROOT/f'case-{i:02d}';folder.mkdir();cases.append(dict(id=a['id'],prefab_path=a['source_asset_path'],prefab_guid=a['source_guid'],output=str(folder/'NATIVE-RESULT.v1.json')))
 request=ROOT/'REQUEST.v1.json';capture.write(request,dict(cases=cases,output=str(ROOT/'NATIVE-RESULT.v1.json')))
 for p in [Path(__file__).resolve(),MIGRATION,PRIOR,DELIVERY,catalog_path,request]:pins[str(p)]=capture.sha(p)
 capture.write(ROOT/'PREPARED.v1.json',dict(sub_batch='L08.IMAGE.PIRATE.06',expected_cards=8,expected_images=16,cases=cases,input_pins=pins,catalog_revision=registry['revision'],private_sdk_migration_receipt=str(MIGRATION),private_bench_reused=True,original_lod_level=0,source_version_parity_qualified=False,presentation_only=True,source_assets_executed=False,prior_refused_cards_not_retried=True,full_import=False))
 print(json.dumps(dict(lot='L08.IMAGE.PIRATE.06',state='prepared_capture_not_started',expected_cards=8,expected_images=16,root=str(ROOT))))
if __name__=='__main__':
 if len(sys.argv)==1:prepare()
 else:
  assert sys.argv[1]=='run'
  active=subprocess.check_output([capture.PS,'-NoProfile','-Command',"@(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -eq 16336 -and $_.CommandLine -like '*run_browser_source_glb_v114.py*' }).Count"],text=True).strip()
  assert active=='0','Wait for actual Web GLB controller terminal result before native GPU capture'
  capture.ROOT=ROOT;capture.PROJECT=PROJECT;capture.run()

"""Freeze eight fresh Pirate source cards; rendering and review remain pending."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'pirate-source-thumbnail-batch-0113-0120-v163';PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not O.exists()
 auditpath=W/'pirate-private-import-change-audit-v143/RESULT.v1.json';guardpath=W/'pirate-source-thumbnail-batch-0105-0112-source-guard-v1/RESULT.v1.json';deliverypath=W/'pirate-source-thumbnail-batch-0105-0112-delivery-reopen-v1/RESULT.v1.json'
 audit=read(auditpath);guard=read(guardpath);delivery=read(deliverypath);assert audit['original_sources_unchanged'] and len(guard['admitted_ids'])==8 and delivery['cards_updated']==8 and delivery['vault_delta_rehashed']
 pins=dict(audit['input_pins'])
 for p,h in pins.items():assert sha(p)==h,p
 pointer=read(V/'metadata/asset-source-support-registry.v1.json');catalogpath=V/pointer['path'];assert sha(catalogpath)==pointer['sha256'];catalog=read(catalogpath)
 previous=set()
 for name in ['pirate-source-thumbnail-batch-0001-0024-v2','pirate-source-thumbnail-batch-0025-0048-v1','pirate-source-thumbnail-batch-0049-0072-v1','pirate-source-thumbnail-batch-0073-0096-v1','pirate-source-thumbnail-batch-0097-0104-v1','pirate-source-thumbnail-batch-0105-0112-v1']:
  p=W/name/'PREPARED.v1.json';previous.update(c['id'] for c in read(p)['cases']);pins[str(p)]=sha(p)
 eligible=sorted([a for a in catalog['assets'] if a.get('pack_id')=='hivemind.pirate-island' and not a.get('preview') and a['id'] not in previous and a.get('original_lod_level')==0 and '/HDRP' in a.get('source_asset_path','') and a['source_asset_path'].endswith('.prefab')],key=lambda a:a['id']);selected=eligible[:8];assert len(selected)==8 and len({a['source_asset_path'] for a in selected})==8
 O.mkdir();epoch=O/'private-input-epoch';epoch.mkdir();snapshots=[]
 for name,h in list(pins.items()):
  p=Path(name)
  if p.is_relative_to(PROJECT) and p.suffix in {'.mat','.asset','.json','.cs','.shadergraph'}:
   copy=epoch/p.relative_to(PROJECT);copy.parent.mkdir(parents=True,exist_ok=True);copy.write_bytes(p.read_bytes());assert sha(copy)==h;pins[str(copy)]=h;snapshots.append(dict(live_path=name,snapshot=str(copy),sha256=h))
 private=O/'PRIVATE-EPOCH.v1.json';private.write_text(json.dumps(dict(private_input_snapshots=snapshots,full_import=False),indent=2),'utf-8');pins[str(private)]=sha(private)
 cases=[]
 for i,a in enumerate(selected):
  live=PROJECT/a['source_asset_path'];assert live.is_file();pins[str(live)]=sha(live)
  for field,hfield in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
   p=V/a[field];assert sha(p)==a[hfield];pins[str(p)]=sha(p)
  folder=O/f'case-{i:02d}';folder.mkdir();cases.append(dict(id=a['id'],prefab_path=a['source_asset_path'],prefab_guid=a['source_guid'],output=str(folder/'NATIVE-RESULT.v1.json')))
 request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
 for p in [Path(__file__),auditpath,guardpath,deliverypath,catalogpath,request]:pins[str(p)]=sha(p)
 prep=dict(sub_batch='L08.IMAGE.PIRATE.07',prepared_utc=datetime.now(timezone.utc).isoformat(),expected_cards=8,expected_images=16,cases=cases,input_pins=pins,catalog_revision=pointer['revision'],private_sdk_migration_receipt=str(guardpath),private_bench_reused=True,original_lod_level=0,source_version_parity_qualified=False,presentation_only=True,source_assets_executed=False,prior_refused_cards_not_retried=True,capture_started=False,full_import=False)
 (O/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8');(O/'SELECTION.v1.json').write_text(json.dumps(dict(eligible_remaining_before_selection=len(eligible),selected_cards=selected),indent=2),'utf-8');print(json.dumps(dict(lot=prep['sub_batch'],selected=8,images_expected=16,eligible=len(eligible),capture_started=False,names=[Path(a['source_asset_path']).stem for a in selected])))
if __name__=='__main__':main()

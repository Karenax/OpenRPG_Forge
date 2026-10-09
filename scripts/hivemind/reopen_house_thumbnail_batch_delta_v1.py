"""Independently reconcile the exact five-card transaction and its controlled Vault delta."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');INDEX=W/'house-source-thumbnail-batch-0001-0008-index-v1';READER=W/'house-source-thumbnail-batch-0001-0008-native-reader-v1';ROOT=W/'house-source-thumbnail-batch-0001-0008-delta-reopen-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists();ROOT.mkdir();receipt=read(INDEX/'RESULT.v1.json');plan=read(INDEX/'PREPARED.v1.json')['plan'];delta=receipt['controlled_vault_delta'];assert receipt['cards_updated']==5 and receipt['cards_refused']==3
 prior=read(INDEX/'prior-registry.json');assert sha(INDEX/'prior-registry.json')==delta['prior_registry_sha256'];assert sha(V/prior['path'])==prior['sha256'];old=read(V/prior['path'])
 current=read(V/'metadata/asset-source-support-registry.v1.json');assert sha(V/'metadata/asset-source-support-registry.v1.json')==delta['new_registry_sha256'];assert current['revision']==receipt['new_catalog_revision'] and sha(V/current['path'])==current['sha256'];new=read(V/current['path'])
 old_map={a['id']:a for a in old['assets']};new_map={a['id']:a for a in new['assets']};ids={r['id'] for r in receipt['cards']};assert set(old_map)==set(new_map) and len(new_map)==12516 and {k for k in old_map if old_map[k]!=new_map[k]}==ids
 for asset_id in ids:
  before=old_map[asset_id];after=new_map[asset_id];assert not before.get('preview') and after.get('preview');assert after['readiness']==before['readiness'] and after['license']==before['license']
  for key in before:
   if key.startswith('source'):assert before[key]==after[key]
 for p,d in delta['new_files'].items():assert Path(p).resolve().is_relative_to(V.resolve()) and sha(p)==d,p
 assert len(delta['new_files'])==12 and sha(V/'metadata/asset-import-registry.v1.json')==plan['expected_asset_registry_sha256']==delta['model_registry_sha256']
 for p,d in receipt['preview_pins'].items():
  data=Path(p).read_bytes();assert sha(p)==d and data[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',data[16:24])==(512,512)
 native=read(READER/'NATIVE-RESULT.v1.json');run=read(READER/'RESULT.v1.json');assert run['actual_exit_code']==0 and sha(READER/'NATIVE-RESULT.v1.json')==run['native_result_sha256'] and native['cards_verified']==5 and {r['id'] for r in native['rows']}==ids
 for p,d in run['input_pins'].items():assert sha(p)==d,p
 baseline=read(W/'indexed-payload-integrity-independent-reopen-v3/RESULT.v1.json');bench=read(W/'house-bench-native-thumbnail-index-v1/RESULT.v1.json');assert baseline['counts']['preview_absent']==948 and bench['cards_updated']==1
 remaining=948-bench['cards_updated']-len(ids);assert remaining==942
 pins={str(p):sha(p) for p in [Path(__file__).resolve(),INDEX/'RESULT.v1.json',INDEX/'PREPARED.v1.json',READER/'RESULT.v1.json',READER/'NATIVE-RESULT.v1.json',V/current['path'],V/prior['path'],V/'metadata/asset-source-support-registry.v1.json',V/'metadata/asset-import-registry.v1.json',W/'indexed-payload-integrity-independent-reopen-v3/RESULT.v1.json',W/'house-bench-native-thumbnail-index-v1/RESULT.v1.json']}
 result=dict(status='five_card_index_and_twelve_new_vault_files_independently_reconciled',finished_utc=datetime.now(timezone.utc).isoformat(),cards_verified=5,other_source_cards_unchanged=12511,images_verified=10,new_vault_files_verified=12,source_pointer_change_verified=True,model_registry_unchanged=True,missing_primary_images_derived_from_closed_baseline_and_controlled_deltas=remaining,input_pins=pins,full_vault_rescan=False,editor_visual_verified=False,full_import=False)
 (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()

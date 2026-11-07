"""Index only the reviewed one source cards using the skill's immutable transaction."""
from pathlib import Path
from datetime import datetime,timezone
from types import SimpleNamespace
import copy,hashlib,importlib.util,json,shutil
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');ROOT=W/'pirate-lod-thumbnail-batch02-index-v365';REVIEW=W/'pirate-lod-batch02-combined-review-v364/RESULT.v1.json'
HELPER=Path('C:/Users/karen/.codex/skills/openrpg-import-assets/scripts/source_support_import.py')
REVIEW=W/'pirate-lod-batch02-combined-review-v364/RESULT.v1.json'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,v):
 with Path(p).open('x',encoding='utf-8') as f:json.dump(v,f,indent=2,ensure_ascii=False,allow_nan=False)
def repair(text,skill):
 try:skill.validate_text_encoding(text,'updated-card-text');return text
 except ValueError:pass
 for encoding in ['cp1252','latin1']:
  try:
   result=text.encode(encoding).decode('utf-8');skill.validate_text_encoding(result,'repaired-card-text');return result
  except (UnicodeError,ValueError):pass
 raise ValueError('Cannot reversibly repair the existing card text')
def main():
 assert not ROOT.exists();review=read(REVIEW);count=review['cards_verified'];assert 0<count<=16 and review['images_verified']==2*count and review['agent_review'] and not review['human_review'];ROOT.mkdir()
 for p,d in review['input_pins'].items():assert sha(p)==d,p
 spec=importlib.util.spec_from_file_location('private_source_transaction',HELPER);skill=importlib.util.module_from_spec(spec);spec.loader.exec_module(skill)
 state=skill.load_state(V);catalog=copy.deepcopy(state['catalog']);old={a['id']:a for a in state['catalog']['assets']};new={a['id']:a for a in catalog['assets']};ids={row['id'] for row in review['rows']};assert len(ids)==count
 created=[];changes=[];cards=[];preview_pins={};prior_transactions=set((V/'metadata/asset-source-support-transactions').glob('*.json'))
 for row in review['rows']:
  entry=new[row['id']];before=old[row['id']];native=read(row['native_receipt']);assert sha(row['native_receipt'])==row['native_receipt_sha256']
  assert entry['source_guid']==native['prefab_guid'] and entry['pack_id']=='hivemind.pirate-island' and 'preview' not in entry and entry['original_lod_level']==native['requested_lod']
  views=[]
  for image in row['images']:
   source=Path(image['path']);assert sha(source)==image['sha256'];relative=f"previews/native-source/hivemind-pirate-island/{image['sha256']}.png";target=V/relative;target.parent.mkdir(parents=True,exist_ok=True)
   if target.exists():assert sha(target)==image['sha256']
   else:
    with source.open('rb') as src,target.open('xb') as dst:shutil.copyfileobj(src,dst)
    created.append(str(target))
   assert sha(target)==image['sha256'];preview_pins[str(target)]=image['sha256'];views.append(dict(path=relative,sha256=image['sha256'],view=image['view'],width=512,height=512))
  for field in ['names','descriptions']:
   for language,text in entry[field].items():
    repaired=repair(text,skill)
    if repaired!=text:changes.append(dict(id=entry['id'],field=field,language=language,before=text,after=repaired));entry[field][language]=repaired
  entry.update(preview=views[0]['path'],preview_sha256=views[0]['sha256'],preview_views=views,preview_origin='unity-native-source-capture',preview_forge_render=False,preview_status='unity-native-thumbnail-agent-reviewed-openrpg-render-unverified',preview_visual_review='agent-reviewed',preview_capture=dict(unity_version=native['unity_version'],fixed_ev=native['presentation_fixed_ev'],tonemapping=native['presentation_tonemapping'],original_unity_version_parity=False,private_sdk_material_migrations=True,migration_receipt_sha256=sha(review['private_import_migration_receipt']),human_review=False,native_receipt_sha256=row['native_receipt_sha256'],review_receipt_sha256=sha(REVIEW),requested_original_lod=native['requested_lod'],lod_selection_verified=native['requested_lod_selection_verified'],selected_lod_mesh_identities=native['selected_mesh_identities'],selected_lod_vertex_counts=native['selected_mesh_vertex_counts'],source_empty_lod_slots=native['source_empty_lod_slots']))
  entry['tags']=[t for t in entry['tags'] if t!='preview-unavailable']+['unity-native-thumbnail']
  entry['descriptions']['fr']+=f" Vignette de cette source capturée dans Unity 6000.5.8f1, exposition EV{native['presentation_fixed_ev']:g} et tonemapping Neutral, revue agent après migration HDRP dans une copie privée ; rendu OpenRPG non vérifié."
  entry['descriptions']['en']+=f" Thumbnail captured in Unity 6000.5.8f1, EV{native['presentation_fixed_ev']:g} exposure and Neutral tonemapping, agent reviewed after HDRP migration in a private copy; OpenRPG rendering remains unverified."
  allowed={'names','descriptions','tags'}|{k for k in entry if k.startswith('preview')};assert {k:v for k,v in entry.items() if k not in allowed}=={k:v for k,v in before.items() if k not in allowed}
  cards.append(dict(id=entry['id'],primary_sha256=views[0]['sha256'],source_name=Path(entry['source_asset_path']).stem,views=views))
 assert set(old)==set(new) and {k for k in old if old[k]!=new[k]}==ids
 validation=skill.validate_catalog(catalog,V,state['packs'],state['model_ids'],ids)
 write(ROOT/'prior-entries.json',[old[k] for k in sorted(ids)]);(ROOT/'prior-registry.json').write_bytes(state['support_registry_raw']);write(ROOT/'TEXT-REPAIRS.v1.json',changes)
 stage=ROOT/'stage';stage.mkdir();raw=skill.canonical_json(catalog);digest=skill.sha_bytes(raw);(stage/'source-support.v1.json').write_bytes(raw)
 plan=dict(schema='openrpg.source-support-stage.v1',expected_asset_registry_sha256=skill.sha_bytes(state['asset_registry_raw']),expected_support_registry_sha256=skill.sha_bytes(state['support_registry_raw']),expected_active_catalog_sha256=skill.sha_bytes(state['catalog_raw']),candidate_source=str(REVIEW),candidate_source_sha256=sha(REVIEW),candidate_ids=sorted(ids),counts=dict(existing=len(old),updated=count,union=len(new)),catalog_sha256=digest,validation=validation,update_scope='reviewed_existing_source_card_thumbnails_and_reversible_text_encoding_repairs')
 (stage/'plan.json').write_bytes(skill.canonical_json(plan));write(ROOT/'PREPARED.v1.json',dict(plan=plan,created_preview_files=created,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),HELPER,REVIEW]},full_import=False))
 skill.cmd_apply(SimpleNamespace(vault=str(V),stage=str(stage)));final=skill.load_state(V);assert final['support_registry']['revision']==digest and final['catalog']==catalog and skill.sha_bytes(final['asset_registry_raw'])==plan['expected_asset_registry_sha256']
 checked=skill.validate_catalog(final['catalog'],V,final['packs'],final['model_ids'],ids)
 transactions=set((V/'metadata/asset-source-support-transactions').glob('*.json'))-prior_transactions;assert len(transactions)==1
 delta_files=[*[Path(p) for p in created],V/final['support_registry']['path'],*transactions];delta={str(p):sha(p) for p in delta_files};registry_path=V/'metadata/asset-source-support-registry.v1.json'
 result=dict(status='reviewed_pirate_source_cards_transactionally_indexed',finished_utc=datetime.now(timezone.utc).isoformat(),cards_updated=count,images_installed=2*count,cards_refused=review['cards_refused'],source_cards=len(new),cards=cards,new_catalog_revision=digest,validation=checked,other_cards_unchanged=True,source_license_and_readiness_unchanged=True,text_encoding_repairs=len(changes),preview_pins=preview_pins,controlled_vault_delta=dict(new_files=delta,modified_registry=str(registry_path),prior_registry_sha256=plan['expected_support_registry_sha256'],new_registry_sha256=sha(registry_path),model_registry_sha256=sha(V/'metadata/asset-import-registry.v1.json'),full_vault_rescan=False),agent_review=True,human_review=False,editor_visual_verified=False,full_import=False)
 write(ROOT/'RESULT.v1.json',result);print(json.dumps(dict(cards_updated=count,images_installed=2*count,missing_primary_images=sum(not a.get('preview') for a in final['catalog']['assets']),new_catalog_revision=digest)))
if __name__=='__main__':main()

"""Update one existing source card through the skill's immutable apply mechanism."""
from pathlib import Path
from datetime import datetime,timezone
from types import SimpleNamespace
import copy,hashlib,importlib.util,json,shutil
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');ROOT=W/'house-bench-native-thumbnail-index-v1'
CAP=W/'unity-library-source-thumbnail-house-bench-v8';REOPEN=W/'house-source-thumbnail-reopen-v1/RESULT.v1.json'
HELPER=Path('C:/Users/karen/.codex/skills/openrpg-import-assets/scripts/source_support_import.py')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def write(p,value):
 with Path(p).open('x',encoding='utf-8') as f:json.dump(value,f,indent=2,ensure_ascii=False,allow_nan=False)
def main():
 assert not ROOT.exists();ROOT.mkdir();assert read(REOPEN)['images_verified']==2
 spec=importlib.util.spec_from_file_location('source_support_transactional_import',HELPER);skill=importlib.util.module_from_spec(spec);spec.loader.exec_module(skill)
 native=read(CAP/'NATIVE-RESULT.v1.json');review=read(CAP/'VISUAL-REVIEW.v1.json')
 assert review['status']=='source_thumbnail_candidates_accepted_by_agent_for_indexing' and not review['human_review']
 state=skill.load_state(V);catalog=copy.deepcopy(state['catalog']);matches=[a for a in catalog['assets'] if a['id']==native['id']];assert len(matches)==1;entry=matches[0];before=copy.deepcopy(entry)
 assert entry['source_guid']==native['prefab_guid'] and entry['pack_id']=='hivemind.house-on-a-hill'
 assert entry['source_asset_path']=='Assets/Hivemind/HouseOnaHill/HDRP/Art/Prefabs/SM_Bench_01.prefab'
 assert entry['source_archive_sha256']=='d476a7102427cb5ae7bc8ab8c828c4dbc9815ba94650d9b3b829205ca5f02958'
 assert 'preview' not in entry
 views=[];created=[]
 for image in native['images']:
  source=Path(image['path']);assert sha(source)==image['sha256'];relative=f"previews/native-source/hivemind-house-on-a-hill/{image['sha256']}.png";target=V/relative
  target.parent.mkdir(parents=True,exist_ok=True)
  if target.exists():assert sha(target)==image['sha256']
  else:
   with source.open('rb') as src,target.open('xb') as dst:shutil.copyfileobj(src,dst)
   created.append(str(target))
  assert sha(target)==image['sha256'];views.append(dict(path=relative,sha256=image['sha256'],view=image['view'],width=512,height=512))
 entry.update(preview=views[0]['path'],preview_sha256=views[0]['sha256'],preview_views=views,preview_origin='unity-native-source-capture',preview_forge_render=False,preview_status='unity-native-thumbnail-agent-reviewed-openrpg-render-unverified',preview_visual_review='agent-reviewed',preview_capture=dict(unity_version=native['unity_version'],fixed_ev=native['presentation_fixed_ev'],tonemapping=native['presentation_tonemapping'],original_unity_version_parity=False,human_review=False,native_receipt_sha256=sha(CAP/'NATIVE-RESULT.v1.json'),review_receipt_sha256=sha(CAP/'VISUAL-REVIEW.v1.json')))
 entry['tags']=[tag for tag in entry['tags'] if tag!='preview-unavailable']+['unity-native-thumbnail']
 entry['descriptions']['fr']+=' Vignette de cette source capturée dans Unity 6000.5.8f1, exposition EV8 et tonemapping Neutral, revue agent ; rendu OpenRPG non vérifié.'
 entry['descriptions']['en']+=' Thumbnail of this source captured in Unity 6000.5.8f1, EV8 exposure and Neutral tonemapping, agent reviewed; OpenRPG rendering remains unverified.'
 assert entry['readiness']==before['readiness'] and entry['license']==before['license'] and entry['source_sha256']==before['source_sha256']
 old_map={a['id']:a for a in state['catalog']['assets']};new_map={a['id']:a for a in catalog['assets']};assert set(old_map)==set(new_map)
 assert [key for key in old_map if old_map[key]!=new_map[key]]==[entry['id']]
 validation=skill.validate_catalog(catalog,V,state['packs'],state['model_ids'],{entry['id']})
 (ROOT/'prior-registry.json').write_bytes(state['support_registry_raw']);write(ROOT/'prior-entry.json',before)
 stage=ROOT/'stage';stage.mkdir();raw=skill.canonical_json(catalog);digest=skill.sha_bytes(raw);(stage/'source-support.v1.json').write_bytes(raw)
 plan=dict(schema='openrpg.source-support-stage.v1',expected_asset_registry_sha256=skill.sha_bytes(state['asset_registry_raw']),expected_support_registry_sha256=skill.sha_bytes(state['support_registry_raw']),expected_active_catalog_sha256=skill.sha_bytes(state['catalog_raw']),candidate_source=str(REOPEN),candidate_source_sha256=sha(REOPEN),candidate_ids=[entry['id']],counts=dict(existing=len(old_map),updated=1,union=len(new_map)),catalog_sha256=digest,validation=validation,update_scope='one_existing_source_card_thumbnail_only')
 (stage/'plan.json').write_bytes(skill.canonical_json(plan));write(ROOT/'PREPARED.v1.json',dict(plan=plan,created_preview_files=created,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),HELPER,REOPEN,CAP/'NATIVE-RESULT.v1.json',CAP/'VISUAL-REVIEW.v1.json']},full_import=False))
 skill.cmd_apply(SimpleNamespace(vault=str(V),stage=str(stage)))
 final=skill.load_state(V);assert final['support_registry']['revision']==digest and final['catalog']==catalog
 checked=skill.validate_catalog(final['catalog'],V,final['packs'],final['model_ids'],{entry['id']})
 assert skill.sha_bytes(final['asset_registry_raw'])==plan['expected_asset_registry_sha256']
 write(ROOT/'RESULT.v1.json',dict(status='one_house_source_thumbnail_transactionally_indexed_and_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),updated_asset_id=entry['id'],source_cards=len(new_map),cards_updated=1,images_installed=2,primary_preview=views[0],new_catalog_revision=digest,validation=checked,other_cards_unchanged=True,source_license_and_readiness_unchanged=True,native_capture_agent_review=True,human_review=False,editor_visual_verified=False,full_import=False))
 print(json.dumps(dict(cards_updated=1,images_installed=2,new_catalog_revision=digest,editor_visual_verified=False)))
if __name__=='__main__':main()

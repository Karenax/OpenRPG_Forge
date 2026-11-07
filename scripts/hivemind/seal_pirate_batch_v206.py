"""Seal the four-card delivery and keep the four material refusals actionable."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault')
O=W/'pirate-source-thumbnail-batch-0121-0128-delivery-v206'
I=W/'pirate-source-thumbnail-batch-0121-0128-index-v204';N=W/'pirate-source-thumbnail-batch-0121-0128-native-reader-v205'
G=W/'pirate-source-thumbnail-batch-0121-0128-guard-v202/RESULT.v1.json'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
index=read(I/'RESULT.v1.json');native=read(N/'RESULT.v1.json');guard=read(G)
assert index['cards_updated']==4 and index['images_installed']==8
assert native['actual_exit_code']==0 and native['cards_verified']==4
assert native['status']=='reviewed_current_native_source_cards_search_filter_details_images_passed'
assert sha(N/'NATIVE-RESULT.v1.json')==native['native_result_sha256']
pins=guard['input_pins']|native['input_pins']|index['preview_pins']|index['controlled_vault_delta']['new_files']
for p,h in pins.items():assert sha(p)==h,p
pointer=read(V/'metadata/asset-source-support-registry.v1.json');prior=read(I/'prior-registry.json')
assert pointer['revision']==index['new_catalog_revision']
assert sha(V/pointer['path'])==pointer['sha256'] and sha(V/prior['path'])==prior['sha256']
old={a['id']:a for a in read(V/prior['path'])['assets']};current={a['id']:a for a in read(V/pointer['path'])['assets']}
ids={c['id'] for c in index['cards']};assert len(ids)==4 and set(old)==set(current)
assert {id for id in old if old[id]!=current[id]}==ids
for id in ids:
    a,b=old[id],current[id];allowed={'names','descriptions','tags'}|{k for k in b if k.startswith('preview')}
    assert {k:v for k,v in a.items() if k not in allowed}=={k:v for k,v in b.items() if k not in allowed}
    assert not a.get('preview') and len(b['preview_views'])==2
    for field,hfield in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
        path=V/b[field];assert sha(path)==b[hfield];pins[str(path)]=b[hfield]
refused={r['id'] for r in guard['cases_refused_source_parity']};assert len(refused)==4
assert all(current[id]==old[id] and not current[id].get('preview') for id in refused)
models=V/'metadata/asset-import-registry.v1.json';assert sha(models)==index['controlled_vault_delta']['model_registry_sha256']
missing=sum(not a.get('preview') for a in current.values());assert missing==793 and len(current)==12517
for p in [Path(__file__),I/'RESULT.v1.json',I/'prior-registry.json',N/'RESULT.v1.json',N/'NATIVE-RESULT.v1.json',G,models,V/'metadata/asset-source-support-registry.v1.json',V/pointer['path']]:pins[str(p)]=sha(p)
result=dict(status='four_new_source_cards_eight_images_delivery_and_current_reader_reopened',
    cards_delivered=4,images_delivered=8,cards_refused=4,source_primary_images_missing=missing,
    ids=sorted(ids),refused_ids=sorted(refused),current_catalog_revision=pointer['revision'],
    source_license_readiness_and_other_cards_preserved=True,current_native_reader_verified=True,
    actual_studio_gui_verified=False,full_vault_rescan=False,full_import=False,input_pins=pins,
    gui_tool_observation='node_repl initialization twice failed; windows sandbox failed: helper_unknown_error: setup refresh had errors',
    finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Pirate.08 — quatre fiches / huit images livrées, quatre façades en attente

**Lot v201–v206 : 8 objets capturés, 4 admis et livrés, 4 refus conservés.** Trois variantes SeedDebris et la façade ShackFront6m_01 ont leurs deux vues PNG inspectées, sans clipping ni matériau d'erreur. Transaction v204 et lecteur courant du produit v205 PASS : recherche avec filtres, détails, licence, SHA et statut source non plaçable. v206 réouvre le catalogue courant, les huit images et les deltas Vault ; seules ces quatre fiches changent, sources/licences/readiness conservées.

**793 fiches source restent sans image primaire** ; 26 736 IDs dans la bibliothèque, dont 12 517 sources conservées. Pirate cumulé livré : 109 fiches / 218 images. Les quatre façades restantes utilisent M_Wood_Planks_02, migré par Unity pendant ce lot : quatre propriétés ajoutées et masques d'écriture stencil 8→9 / 14→15 / 40→41. Leur capture et leurs sources sont conservées ; il reste à vérifier les defaults natifs et comparer les effets du matériau avant/après dans la recette HDRP source.

La tentative de contrôle graphique Studio a échoué deux fois avant accès à une fenêtre : node_repl / sandbox `helper_unknown_error: setup refresh had errors`. Aucun parcours GUI n'est donc déclaré validé. Le lecteur natif fonctionne. Cela ne bloque pas la conversion locale ; la vérification de la page réelle Studio reste une exigence ouverte.

Prochaine action : qualifier la migration du matériau de bois et les quatre façades retenues, puis poursuivre les autres conversions/vignettes et la fidélité source/Forge. Ne pas retraiter les éléments livrés.

[Lot réouvert](../pirate-source-thumbnail-batch-0121-0128-delivery-v206/RESULT.v1.json) · [Dépendances et diff du bois](../pirate-source-thumbnail-batch-0121-0128-guard-v202/RESULT.v1.json) · [Lecteur produit](../pirate-source-thumbnail-batch-0121-0128-native-reader-v205/RESULT.v1.json).

'''
for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
    path=W/'SUIVI_LOTS_HIVEMIND'/name;text=path.read_text('utf-8-sig');(O/(name+'.before')).write_text(text,'utf-8');path.write_text(note+text,'utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))

"""Reopen the four facade deliveries, keeping source conversion and GUI gates open."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'pirate-wood-migration-delivery-v213'
I=W/'pirate-wood-migration-index-v211';N=W/'pirate-wood-migration-native-reader-v212';R=W/'pirate-wood-migration-review-v210/RESULT.v1.json'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
index=read(I/'RESULT.v1.json');native=read(N/'RESULT.v1.json');review=read(R)
assert index['cards_updated']==4 and index['images_installed']==8 and index['cards_refused']==0
assert native['actual_exit_code']==0 and native['cards_verified']==4 and native['reader_logic_unchanged']
assert native['status']=='reviewed_current_native_source_cards_search_filter_details_images_passed'
assert sha(N/'NATIVE-RESULT.v1.json')==native['native_result_sha256']
assert review['agent_review'] and not review['human_review'] and review['cards_refused']==0
assert all(p['migration_exact'] and p['repeat_exact'] for r in review['rows'] for p in r['comparisons'])
pins=review['input_pins']|native['input_pins']|index['preview_pins']|index['controlled_vault_delta']['new_files']
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
        p=V/b[field];assert sha(p)==b[hfield];pins[str(p)]=b[hfield]
models=V/'metadata/asset-import-registry.v1.json';assert sha(models)==index['controlled_vault_delta']['model_registry_sha256']
missing=sum(not a.get('preview') for a in current.values());assert missing==789 and len(current)==12517
for p in [Path(__file__),I/'RESULT.v1.json',I/'prior-registry.json',N/'RESULT.v1.json',N/'NATIVE-RESULT.v1.json',R,models,V/'metadata/asset-source-support-registry.v1.json',V/pointer['path']]:pins[str(p)]=sha(p)
result=dict(status='four_wood_facades_eight_images_and_current_native_reader_verified',cards_delivered=4,images_delivered=8,
    refused_cards_resolved=4,source_primary_images_missing=missing,ids=sorted(ids),current_catalog_revision=pointer['revision'],
    other_cards_sources_licenses_and_readiness_preserved=True,current_native_reader_verified=True,
    actual_studio_gui_verified=False,full_vault_rescan=False,full_import=False,input_pins=pins,
    finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Pirate.08 — quatre façades supplémentaires livrées, lot de huit terminé

**4 façades / 8 images supplémentaires validées et livrées sur F:** ShackFront4m_01, ShackFront4m_02, ShackFront6m_03 et ShackFront6m_05. v207/v208 : quatre defaults natifs exacts, diff du matériau entièrement réversible, textures/mots-clés/autres valeurs sérialisées inchangés. v209/v210 : copie en mémoire avec anciens masques stencil, chauffe préalable, huit vues RGBA16F exactes avant/après et entre répétitions, PNG lisibles inspectés. Les anciens refus sont conservés ; les quatre cas sont résolus pour leurs vignettes.

v211 transaction, v212 lecteur actuel et v213 réouverture indépendante PASS : recherche filtrée, fiches, licences, images, sources conservées et statut non plaçable. Seules ces quatre fiches changent. **Lot Pirate.08 total : 8 fiches / 16 images livrées** avec la tranche précédente. Pirate cumulé : 113 fiches / 226 images. **789 fiches source restent sans image primaire** sur 12 517 sources conservées ; la bibliothèque garde 26 736 IDs.

Les vignettes Unity ne qualifient pas les GLB, le placement ou le runtime Forge. La page réelle Studio reste à vérifier après résolution de l'échec du moteur de contrôle graphique Windows ; aucune preuve GUI nouvelle. Prochaine tranche : autres éléments sans image et conversion/fidélité source/Forge, en réutilisant les recettes vérifiées et sans refaire les livraisons acquises.

[Livraison réouverte](../pirate-wood-migration-delivery-v213/RESULT.v1.json) · [Defaults et diff](../pirate-wood-defaults-reopen-v208/RESULT.v1.json) · [Comparaison des quatre façades](../pirate-material-migration-render-v209/RUN-RESULT.v1.json) · [Lecteur produit](../pirate-wood-migration-native-reader-v212/RESULT.v1.json).

'''
for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
    path=W/'SUIVI_LOTS_HIVEMIND'/name;text=path.read_text('utf-8-sig');(O/(name+'.before')).write_text(text,'utf-8');path.write_text(note+text,'utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))

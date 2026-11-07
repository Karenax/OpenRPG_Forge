"""Reopen both controlled deliveries against the current catalog and product reader."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'pirate-material-migration-delivery-reopen-v200'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
index_roots=[W/'pirate-material-migration-index-v193',W/'pirate-wall-migration-index-v198']
reader_roots=[W/'pirate-material-migration-native-reader-v194',W/'pirate-wall-migration-native-reader-v199']
indexes=[read(p/'RESULT.v1.json') for p in index_roots]
readers=[read(p/'RESULT.v1.json') for p in reader_roots]
pins={};ids=set()
for root,index,reader_root,reader in zip(index_roots,indexes,reader_roots,readers):
    assert index['cards_updated']==1 and index['images_installed']==2 and index['other_cards_unchanged']
    assert reader['actual_exit_code']==0 and reader['status']=='reviewed_current_native_source_cards_search_filter_details_images_passed'
    assert reader['cards_verified']==1 and reader['reader_logic_unchanged']
    ids.add(index['cards'][0]['id'])
    for p,h in index['preview_pins'].items():assert sha(p)==h,p;pins[p]=h
    for p,h in index['controlled_vault_delta']['new_files'].items():assert sha(p)==h,p;pins[p]=h
    for p,h in reader['input_pins'].items():assert sha(p)==h,p;pins[p]=h
    assert sha(reader_root/'NATIVE-RESULT.v1.json')==reader['native_result_sha256']
    for p in [root/'RESULT.v1.json',root/'prior-registry.json',reader_root/'RESULT.v1.json',reader_root/'NATIVE-RESULT.v1.json']:
        pins[str(p)]=sha(p)
assert len(ids)==2
prior=read(index_roots[0]/'prior-registry.json');middle=read(index_roots[1]/'prior-registry.json')
assert middle['revision']==indexes[0]['new_catalog_revision']
pointer=read(V/'metadata/asset-source-support-registry.v1.json')
assert pointer['revision']==indexes[1]['new_catalog_revision']
assert sha(V/pointer['path'])==pointer['sha256']
assert sha(V/prior['path'])==prior['sha256']
old={a['id']:a for a in read(V/prior['path'])['assets']}
current={a['id']:a for a in read(V/pointer['path'])['assets']}
assert set(old)==set(current) and len(current)==12517
assert {k for k in old if old[k]!=current[k]}==ids
for id in ids:
    a,b=old[id],current[id];allowed={'names','descriptions','tags'}|{k for k in b if k.startswith('preview')}
    assert {k:v for k,v in a.items() if k not in allowed}=={k:v for k,v in b.items() if k not in allowed}
    assert not a.get('preview') and len(b['preview_views'])==2
    for field,h in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
        path=V/b[field];assert sha(path)==b[h];pins[str(path)]=b[h]
    for image in b['preview_views']:
        path=V/image['path'];assert sha(path)==image['sha256'];pins[str(path)]=image['sha256']
models=V/'metadata/asset-import-registry.v1.json'
assert sha(models)==indexes[0]['controlled_vault_delta']['model_registry_sha256']==indexes[1]['controlled_vault_delta']['model_registry_sha256']
missing=sum(not a.get('preview') for a in current.values());assert missing==797
for p in [Path(__file__),V/'metadata/asset-source-support-registry.v1.json',V/pointer['path'],V/prior['path'],models]:pins[str(p)]=sha(p)
O.mkdir()
result=dict(status='two_pirate_cards_four_images_current_catalog_and_native_reader_verified',cards_delivered=2,
    images_delivered=4,source_cards=len(current),source_primary_images_missing=missing,current_catalog_revision=pointer['revision'],
    ids=sorted(ids),other_cards_unchanged=True,sources_licenses_readiness_preserved=True,
    current_native_library_verified=True,gui_studio_verified=False,full_vault_rescan=False,full_import=False,
    input_pins=pins,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Pirate.07 — deux refus résolus et quatre images livrées

**2 fiches supplémentaires / 4 images livrées sur F:** rocher SM_RockStoneFoliage_01a et mur SM_RockStoneWall_01b. Diagnostics et anciens refus conservés. Les dix propriétés ajoutées correspondent aux defaults source. Comparaison GPU HDRP EV8 avec copies en mémoire rétablissant les anciens masques stencil et la texture du ShaderGraph : pixels RGBA16F exacts sur les deux vues. Une capture préalable de chauffe résout la première image transitoire du mur ; critères exacts et sources inchangés.

Transactions v193/v198 puis lecteurs natifs actuels v194/v199 PASS : recherche texte + filtres pack/source_support, licence, détails, SHA des PNG et refus d'accès comme modèle plaçable. Réouverture indépendante v200 : seules ces deux fiches changent, sources/licences/readiness conservées, quatre images et deltas Vault rehashés. **797 fiches source restent sans image primaire** ; bibliothèque inchangée à 26 736 IDs. Pirate livré cumulé : 105 fiches / 210 images. Les anciens refus restent dans l'historique, les deux présents sont désormais résolus.

Cette livraison qualifie uniquement les vignettes source, pas la conversion runtime de ces objets. GUI Studio, placement, effets et fidélité complète source/Forge restent à vérifier. Prochaine tranche : nouveaux éléments sans vignette et validation de la page réelle de bibliothèque dans Studio. Ne pas refaire les lots validés.

[Livraison réouverte](../pirate-material-migration-delivery-reopen-v200/RESULT.v1.json) · [Comparaison GPU stabilisée](../pirate-material-migration-render-v196/RUN-RESULT.v1.json) · [Recherche native du rocher](../pirate-material-migration-native-reader-v194/RESULT.v1.json) · [Recherche native du mur](../pirate-wall-migration-native-reader-v199/RESULT.v1.json).

'''
for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
    path=W/'SUIVI_LOTS_HIVEMIND'/name;text=path.read_text('utf-8-sig')
    (O/(name+'.before')).write_text(text,'utf-8');path.write_text(note+text,'utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))

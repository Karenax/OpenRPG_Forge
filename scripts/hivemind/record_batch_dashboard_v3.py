"""Refresh the readable lot dashboard from terminal receipts, preserving history."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json

W=Path('F:/OpenRPG_ImportWork'); T=W/'SUIVI_LOTS_HIVEMIND'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,text):
    tmp=p.with_suffix(p.suffix+'.tmp');tmp.write_text(text,'utf-8');tmp.replace(p)
def main():
    now=datetime.now(timezone.utc).isoformat()
    storage=W/'vault-current-final-review-v4/RESULT.v1.json'
    independent=W/'vault-final-integrity-independent-reopen-v4/RESULT.v1.json'
    compile=W/'studio-current-source-wasm-check-v9/RUN-RESULT.v1.json'
    if not compile.exists():compile=W/'studio-current-source-wasm-check-v8/RUN-RESULT.v1.json'
    native=W/'studio-source-state-native-v1/RUN-RESULT.v1.json'
    compile_status=read(compile)['status']
    current=read(native);passed=current['status']=='seven_actual_state_tests_and_both_preserved_scenes_passed'
    closure=W/'studio-source-state-install-v1/RESULT.v2.json'
    closed=closure.exists() and read(closure)['status']=='installed_scene_state_adapter_native_and_full_web_compile_reopened'
    active='L01.STUDIO.RENDU_WEB' if closed else 'L01.STUDIO.ETATS_SOURCE.01'
    stage='étape technique vérifiée' if passed else 'contrôle natif en cours' if 'refusal' not in current['status'] else 'refus documenté, diagnostic à poursuivre'
    rows=[
      ('L00','Sources, licences et stockage sur F','Contrôle de stockage fermé','132 961 fichiers, 603 276 106 799 octets ; 206/206 lots SHA restants réconciliés. Maintenir les contrôles à chaque nouvelle livraison.'),
      ('L01','Chargement et scènes dans Studio',stage,'Rocks/FirePit : états de 95 nœuds, 63 renderers, 14 groupes LOD conservés. Finir pilotage LOD, contexte de rendu, aperçu et placement source_graph ; recette GPU/éditeur/runtime.'),
      ('L02','Dungeon','Captures intermédiaires vérifiées','42 captures HDRP ; matériaux, rendu et assemblage des prefabs complets restant à qualifier.'),
      ('L03','Conversions statiques par pack','Étapes géométriques vérifiées','Townsmith : 893 géométries et 8 poses ; 6 bindings à résoudre. Villa : 207 dérivés admis ; 19 scènes supplémentaires à traiter. Puis chaque prefab complet et les autres packs.'),
      ('L04','Matériaux, textures et feuillage','En cours','Rocks/FirePit : dérivés statiques et données source préservés. Terminer rendu, passes, ombres, LOD et autres matériaux/packs.'),
      ('L05','Rigs et animations','Intermédiaire conservé','Medieval : poursuivre matériaux, déformation GPU, clips et recette. Puis les autres rigs/animations.'),
      ('L06','Effets','En cours','FirePit : deux effets, référence native 112 observations ; simulation autonome et rendu restant. Blood : 85 prefabs/139 graphes et nouveaux sous-lots ; Dust : références temporelles, comportement Forge restant.'),
      ('L07','Sprites, terrains et autres formats','À poursuivre','Dépendances, conversion adaptée au type, rendu et usage dans le produit.'),
      ('L08','Bibliothèque, images et recherche','Contrôles de données vérifiés, recette globale ouverte','26 735 fiches rapprochées ; 25 787 références image et 948 absences explicites. Produire les images pertinentes manquantes et vérifier recherche/filtres/métadonnées, aperçu, édition, sauvegarde/réouverture.'),
      ('L09','Réconciliation finale','Non terminé','Rapprocher les IDs des 48 packs/variantes avec les livraisons et refus précis ; qualification finale Studio/runtime et provenance.')]
    plan='''# Hivemind — suivi courant par lots

Actualisé : '''+now+'''. Ce tableau remplace les actions anciennes des journaux ; leurs preuves restent conservées.

**Bibliothèque : `F:\\OpenRPG_AssetVault`**. **Conversions, caches et preuves : `F:\\OpenRPG_ImportWork`**.

Périmètre : **48 packs/variantes**. Le registre déclare **14 219 modèles et 12 516 sources support** ; ces compteurs ne sont pas un total d’assets entièrement livrés. L’objectif reste incomplet.

| Lot | Travail | État vérifié | Prochaine étape / portée |
|---|---|---|---|
'''
    plan+=''.join('| '+' | '.join(row)+' |\n' for row in rows)
    plan+='''
## Sous-lot actif : L01.STUDIO.ETATS_SOURCE.01

Périmètre fixé : les deux scènes Rocks et FirePit. Appliquer les états actifs des nœuds, enabled/force-off des renderers, identités originales et flags d’ombres ; conserver les 14 groupes LOD. La racine reste cachée jusqu’à admission du rendu et du pilotage LOD.

'''
    plan+=f'**État : {stage}**. Tests natifs : {current.get("actual_tests_passed",0)}/7 validés ; état exact `{current["status"]}`. Frontend WASM courant : `{compile_status}`.\n\n'
    plan+='''[Compilation](../studio-current-source-wasm-check-v8/RUN-RESULT.v1.json) · [Tests natifs](../studio-source-state-native-v1/RUN-RESULT.v1.json) · [Dérivés et contrôles](../rocks-firepit-renderer-state-glb-v1/INDEPENDENT-REOPEN.v1.json).

Ces preuves ne qualifient pas le GPU, la sélection LOD, les particules ni la disponibilité visible dans Studio.

## Ordre des prochains sous-lots

1. **L01.STUDIO.ETATS_SOURCE.01** : terminer les tests des états sur les deux scènes ; enregistrer le reçu terminal.
2. **L01.STUDIO.RENDU_WEB** : contextes de rendu et pilotage LOD qualifiés, montage aperçu/placement par le host commun, puis recette GPU/éditeur. Conserver les refus précis sans utiliser de remplacement silencieux.
3. **L03.TOWNSMITH.BINDINGS** et **L03.VILLA.SCENES** : six bindings, puis dix-neuf scènes restantes ; assemblage et vérification par prefab.
4. **L02/L04** : matériaux, passes et rendu des prefabs ; les éléments géométriques déjà validés ne sont pas reconvertis.
5. **L05/L06/L07** : rigs/animations, effets FirePit/Blood/Dust, sprites et terrains, par groupes d’IDs explicites.
6. **L08** après chaque livraison : image propre à l’asset, fiche, recherche/filtres, aperçu, placement, édition et réouverture ; runtime applicable.
7. **L09** : réconciliation finale de tous les IDs et preuves.

## Règle de suivi

Chaque sous-lot précise ses IDs, attendu/vérifié, sorties sur F, reçu et prochaine étape. Étapes distinctes : conversion → vérification du dérivé → comportement matériau/animation/effet → index/image → recette Studio/runtime. Aucun passage d’étape technique ne compte comme une livraison complète.

Après chaque résultat terminal, ce tableau et ETAT.v1.json sont actualisés et JOURNAL.md reçoit une entrée. Les refus et anciens reçus sont conservés ; aucun nouveau contrôle d’un lot fermé sans motif consigné.

[Périmètre et inventaire par pack](DECOUPAGE_OPERATIONNEL.md) · [Clôture stockage](../vault-current-final-review-v4/RESULT.v1.json) · [Réouverture indépendante](../vault-final-integrity-independent-reopen-v4/RESULT.v1.json) · [Historique](JOURNAL.md).
'''
    if 'v9' in str(compile):
        plan+='\n**Contrôle Web actualisé v9** : [exécution actuelle](../studio-current-source-wasm-check-v9/RUN-RESULT.v1.json). La compilation v8 avait réussi avec son snapshot intact ; la clôture actuelle a refusé deux fichiers d’exemples Tidewater modifiés depuis. Le refus est conservé dans [le reçu de clôture](../studio-source-state-install-v1/CLOSURE-RUN.v1.json). Les sept tests natifs restent valides ; aucune source concurrente n’est restaurée ou effacée.\n'
    if closed:
        plan=plan.replace('## Sous-lot actif : L01.STUDIO.ETATS_SOURCE.01','## Sous-lot vérifié : L01.STUDIO.ETATS_SOURCE.01')
        plan=plan.replace('1. **L01.STUDIO.ETATS_SOURCE.01** : terminer les tests des états sur les deux scènes ; enregistrer le reçu terminal.','1. **L01.STUDIO.ETATS_SOURCE.01 fermé** : 7/7 tests et frontend actuel réouverts ; [reçu de clôture v2](../studio-source-state-install-v1/RESULT.v2.json).')
        plan=plan.replace('2. **L01.STUDIO.RENDU_WEB** :','2. **L01.STUDIO.RENDU_WEB actif** :')
        plan+='\n**Clôture de l’étape états réouverte** : sept tests réussis, trois modules produit installés intacts, 1 030 entrées du frontend actuel rehachées. Le refus précédent reste conservé. GPU/éditeur/runtime et calcul LOD non qualifiés. [Reçu terminal](../studio-source-state-install-v1/RESULT.v2.json).\n'
    thumbnail=W/'unity-library-source-thumbnail-house-bench-v2/RUN-RESULT.v1.json'
    thumbnail_job=W/'unity-library-source-thumbnail-house-bench-v2/JOB.v1.json'
    if thumbnail_job.exists():
        thumb=read(thumbnail) if thumbnail.exists() else dict(status='preparing_pinned_source_inputs_before_unity_launch')
        plan+='\n## L08.IMAGE_SOURCE.HOUSE_BENCH.01\n\nUne fiche source sans image pertinente : SM_Bench_01, House On A Hill. 742 fichiers du manifeste contrôlés ; 473 fichiers de données copiés dans un projet privé sur F. Aucun script acheté exécuté. Capture prévue : deux vues de 512 × 512 ; inspection visuelle et indexation restant à faire. SDK privé HDRP 17.5.0 / Unity 6000.5.8f1 explicitement conservé dans la preuve ; aucune qualification de conversion ou du rendu Forge.\n\n'
        plan+=f'État enregistré : `{thumb["status"]}` ; images candidates capturées : {thumb.get("images_captured",0)}/2 ; images installées : 0. [Exécution](../unity-library-source-thumbnail-house-bench-v2/RUN-RESULT.v1.json) · [Sources, exclusions et paramètres](../unity-library-source-thumbnail-house-bench-v2/PREPARED.v1.json) · [Refus de préparation conservé](../unity-library-source-thumbnail-house-bench-v1/RUN-RESULT.v1.json).\n'
    house_run=W/'house-native-prefab-hierarchy-v1/RUN-RESULT.v1.json'
    if house_run.exists():
        house=read(house_run)
        plan+='\n## L03.HOUSE.HIERARCHY.01\n\n50 prefabs sources, dont un effet : snapshot des poses, nœuds, slots originaux, références mesh, LOD, colliders et scripts manquants. Réutilisation du helper natif déjà contrôlé contre Unity installé. La file attend le contrôleur des images observé vivant avec son identité de création ; aucun second Unity concurrent. Les 50 racines sources ne sont pas 50 assets livrés, et le snapshot ne convertit pas les shaders ou effets.\n\n'
        plan+=f'État enregistré : `{house["status"]}` ; captures : {house.get("captured_prefab_roots",0)}/50. [Exécution](../house-native-prefab-hierarchy-v1/RUN-RESULT.v1.json) · [Liste exacte et sources épinglées](../house-native-prefab-hierarchy-v1/PREPARED.v1.json).\n'
    write(T/'TABLEAU_DE_BORD_LOTS.md',plan)
    doc=(T/'DECOUPAGE_OPERATIONNEL.md').read_text('utf-8-sig')
    lines=doc.splitlines()
    replacements={r[0]:r[3] for r in rows}
    for i,line in enumerate(lines):
        if line.startswith('| L'):
            cells=line.split('|'); ident=cells[1].strip()
            if ident in replacements and len(cells)==6:cells[4]=' '+replacements[ident]+' ';lines[i]='|'.join(cells)
    if '<!-- CURRENT-DASHBOARD-LINK -->' not in doc:
        lines[1:1]=['','<!-- CURRENT-DASHBOARD-LINK -->','**État courant et ordre actif : [TABLEAU_DE_BORD_LOTS.md](TABLEAU_DE_BORD_LOTS.md).** Les actions historiques ci-dessous restent datées.','']
    doc='\n'.join(lines)+'\n'
    start=doc.index('## Ordre de reprise')
    end=doc.index('## Périmètre par pack',start)
    doc=doc[:start]+'''## Ordre de reprise

Le sous-lot actif est **L01.STUDIO.ETATS_SOURCE.01**, suivi du raccordement du rendu commun. L'ordre courant, les compteurs et les reçus sont dans [TABLEAU_DE_BORD_LOTS.md](TABLEAU_DE_BORD_LOTS.md).

Les conversions avancent ensuite par prefab et par groupes d'IDs : bindings Townsmith et scènes Villa restantes, Dungeon/matériaux, rigs/animations, effets, sprites/terrains. Bibliothèque et recette suivent chaque livraison. Les géométries et le contrôle de stockage déjà fermés ne sont pas refaits sans raison consignée.

'''+doc[end:]
    doc=doc.replace('Plan JSON actualisé : `DECOUPAGE_OPERATIONNEL.v2.json`','Plan JSON courant : `DECOUPAGE_OPERATIONNEL.v3.json`')
    write(T/'DECOUPAGE_OPERATIONNEL.md',doc)
    for name in ['LOTS_ACTIFS.md','AVANCEMENT_PAR_LOTS.md']:
        path=T/name; older=path.read_text('utf-8-sig')
        marker='<!-- CURRENT-DASHBOARD-V3 -->'
        if marker not in older:
            write(path,marker+'\n**Suivi courant : [TABLEAU_DE_BORD_LOTS.md](TABLEAU_DE_BORD_LOTS.md).** Les blocs ci-dessous gardent leurs dates et leurs preuves historiques.\n\n'+older)
    state=read(T/'ETAT.v1.json');state['updated_utc']=now;state['goal_complete']=False
    state['current_execution']=dict(active_sub_batch=active,status=current['status'],state_sub_batch_closed=closed,native_tests_passed=current.get('actual_tests_passed',0),expected_native_tests=7,whole_vault_storage_qualified=True,assets_fully_delivered_count_not_inferred=True,gpu_qualified=False,editor_global_qualified=False,full_import=False,receipt=str(closure if closed else native),dashboard=str(T/'TABLEAU_DE_BORD_LOTS.md'))
    state['current_execution']['frontend_compile']=dict(status=compile_status,receipt=str(compile),gpu_qualified=False)
    if thumbnail_job.exists():state['current_execution']['source_thumbnail_sub_batch']=dict(id='L08.IMAGE_SOURCE.HOUSE_BENCH.01',status=thumb['status'],candidate_images_captured=thumb.get('images_captured',0),expected_candidate_images=2,images_installed=0,receipt=str(thumbnail))
    if house_run.exists():state['current_execution']['house_hierarchy_sub_batch']=dict(id='L03.HOUSE.HIERARCHY.01',status=house['status'],captured_prefab_roots=house.get('captured_prefab_roots',0),expected_prefab_roots=50,geometry_converted=False,receipt=str(house_run))
    for b in state.get('batches',[]):
        if b['id']=='L00':b.update(status='whole_current_vault_storage_integrity_qualified',receipt=str(storage),independent_receipt=str(independent))
        if b['id']=='L01':b.update(status=current['status'],current_receipt=str(native))
    write(T/'ETAT.v1.json',json.dumps(state,indent=2,ensure_ascii=False)+'\n')
    write(T/'DECOUPAGE_OPERATIONNEL.v3.json',json.dumps(dict(updated_utc=now,goal_complete=False,pack_variants=48,active_sub_batch=state['current_execution'],lots=[dict(id=r[0],work=r[1],status=r[2],next=r[3]) for r in rows],delivery_stages=['conversion','verification_du_derive','comportement','index_et_image','recette_studio_runtime']),indent=2,ensure_ascii=False)+'\n')
    journal=(T/'JOURNAL.md').read_text('utf-8-sig')
    entry=f'\n## {now} — L01.STUDIO.ETATS_SOURCE.01\n\nDécoupage et tableau courant actualisés. {stage} ; tests {current.get("actual_tests_passed",0)}/7, frontend courant : `{compile_status}`. Périmètre : 95 nœuds, 63 renderers, 14 groupes LOD conservés ; GPU/éditeur/runtime non qualifiés. [Reçu](../studio-source-state-native-v1/RUN-RESULT.v1.json).\n'
    if current['status'] not in journal.split('<!-- DASHBOARD-V3 -->')[-1]:
        write(T/'JOURNAL.md',journal+'\n<!-- DASHBOARD-V3 -->\n'+entry+'\nÉtat exact : `'+current['status']+'`.\n')
    proof=dict(updated_utc=now,current_status=current['status'],goal_complete=False,receipt_pins={str(p):sha(p) for p in [storage,independent,compile,native]},tracking_pins={str(T/n):sha(T/n) for n in ['TABLEAU_DE_BORD_LOTS.md','DECOUPAGE_OPERATIONNEL.md','ETAT.v1.json','JOURNAL.md']})
    write(T/'TABLEAU_COURANT_PREUVES.v3.json',json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(status=current['status'],dashboard=str(T/'TABLEAU_DE_BORD_LOTS.md'))))

if __name__=='__main__':main()

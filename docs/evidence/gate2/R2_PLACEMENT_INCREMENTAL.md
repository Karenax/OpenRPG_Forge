# R2-050 — reconstruction des placements

## Projection incrémentale des bâtiments — 22 septembre 2026

Le remplacement de projection conserve maintenant, pour chaque partie dont
l'identité sémantique et les ressources restent valides, la même entité ECS,
le même mesh et le même matériau lors d'une retouche de pose. Le composant
`Transform` est mis à jour sur place. Le collider Rapier n'est réinséré que si
l'échelle change ; translation et rotation seules conservent son handle. Une
partie dont le collider est absent ou corrompu reste reconstruite isolément,
sans élargir la réparation au lot complet.

Les gardes preview/accepted, époque courante et préparation Rapier restent
requises. Le test moteur exige la synchronisation `FixedUpdate` avant READY,
vérifie les huit identités stables, la conservation des handles lors d'une
pose, la réparation ciblée d'une seule partie et une retouche géométrique sans
recréation ECS. SHA-256 du fichier vérifié :
`71882E97B2C034E96CABDF0F835F74F6453E8EC451671AEE709646178BCC6608`.

Validations locales sur cache Cargo E: : format PASS, Clippy bibliothèque/tests
strict PASS et `cargo test -p forge_engine --lib authoring_buildings --locked`
PASS, 5/5, lors de la validation indépendante principale. Il s'agit de code et
de tests CPU/Rapier, sans nouveau candidat Studio, preuve graphique, mesure de
performance ou revue humaine. R2-050 et Gate 2 restent donc ouverts.

## Premier parcours graphique — 15 septembre 2026

Complément de relecture native : mode `summary` ajouté à l'exemple existant
`forge_project/examples/inspect_zone_archive.rs`, sans écriture. Le décodeur
Forge rejoue les deux archives v5 (source et essai sauvegardé) avec succès.
Comparaison des placements décodés : identiques exactement après Undo/Save,
y=3 m, même ID/asset/hash/rotation/échelle/verrou. Journal 11 → 13 reçus,
can_redo=true après relecture, tête finale
c0f7d63485a9fe0033a9982dbe3860d5fab771aad317d6fc85aa1a752c6b9e2e.
Hash fichier 329C26DB inchangé après inspection. Compilation exemple 0,76 s,
Clippy strict exemple PASS (0,39 s). Ce complément remplace l'incertitude de
relecture des valeurs mentionnée ci-dessous, mais ne prouve pas le geste Redo
ni l'identité ECS. Aucun nouveau lancement Studio.

Candidat 1551BFCA sur copie `incremental-placements-ui-20260915` de
`scene-index-assets-ui-20260914`, sans rebuild. Cette scène contient **un**
placement (pas deux) : arche 63a0e640-ab20-53be-b3d7-7007135019e3.
Index → sélection → cadrage acquis. Y saisi 3 → 4 m, X/Z conservés ; Preview
montre l'arche élevée, Apply la conserve, Undo la repose au sol. Le diagnostic
indique 3 primitives Rapier synchronisées, aucune en attente. Save après Undo
retourne « Session synchronisée ». Captures placement-preview.jpg,
placement-undone.jpg et placement-saved.jpg dans la copie isolée.

Archive sauvegardée : 329C26DB31A56F5D849E478D7D768CB466E49E17AF6FC342E1993DCE523B0D01.
Format historique v5 (migration + commandes, pas de champ head) : aucune
relecture JSON directe de head ni nouvelle réouverture GUI ne prouve les valeurs.
Les valeurs et l'annulation sont ici constatées dans le Studio, pas déduites
du champ absent. Aucun test de deux objets ou d'identité ECS dans ce parcours.

Temps d'appel natif affichés : Preview 314,7 ms, Apply 395,5 ms, Undo 291,1 ms,
Save 288,1 ms ; transport/traitement seulement, projection 3D exclue. Pas une
mesure avant/après ni un gain revendiqué. Redo, abandon, remplacement d'asset,
plusieurs objets, identité ECS, erreurs et déplacement physique restent dus.

Essai fermé après Save. PID 29048 et WebView 28932/32044/33148/19244/1188/33960
absents au contrôle final. Aucun push ; R2-050 et Gate 2 ouverts.

## Implémentation initiale — 15 septembre 2026

Complément indexation : les racines existantes sont indexées par EntityId une
fois par mise à jour ; les références d'assets utiles sont regroupées en ensemble.
Les recherches répétées dans toute la collection sont retirées. Avant intégration,
comparaison CPU isolée de 4096 recherches : linéaire 3,7051 ms, indexée 495,6 µs
construction comprise, mêmes indices retrouvés. Test opt-in
`compare_linear_and_indexed_lookup` PASS ; mesure unique, pas benchmark de scène,
pas garantie de gain sur petites collections ni de FPS. Clippy WASM strict PASS
(7,04 s) après intégration. Aucun nouveau candidat graphique pour ce complément.

Candidat regroupé construit : 1551BFCAF4A4AAC57A1690B1265C2B8D9E1C08433B5EF2A688396904A1C60DB2,
`E:/OpenRPG_Forge/.gate2-evidence/incremental-placements-studio-20260915`.
Preuve build-proof.json BUILD_ONLY, runtime_verified=false, gate_qualified=false.
Web terminé code 0 (Rust 59,46 s), packaging terminé code 0 ; kit gouverné
40A55F7570003E9A4944BF04118512B722B8CCEC94F3BDAA9D0F75079DD67976 inchangé.
Premier appel Trunk refusé avant build pour NO_COLOR=1, corrigé en true.
Aucun runtime lancé ; processus de compilation terminés. Réutiliser ce candidat
pour les scénarios restants, sans compter sa construction comme preuve de rendu.

Contrôle ciblé ajouté : `authoring_placement_reuse` teste la décision réellement
utilisée par le retrait des racines : identité inchangée, transformation/nom/verrou,
retour aux valeurs précédentes, changement de hash, changement d'AssetId, changement
d'ID, retrait et recherche dans une collection triée. Test PASS (compilation
10,65 s, exécution < 0,01 s) ; Clippy Web/WASM strict PASS (7,06 s).
La recherche de réutilisation utilise l'index trié par ID au lieu de parcourir
toute la collection pour chaque racine. Ce test couvre la décision, pas l'exécution
des commandes ECS ni la synchronisation Rapier : ces preuves restent dues.

Le code Studio conserve désormais les racines de même ID et référence AssetId/hash.
Transformations et métadonnées sont mises à jour sur place ; les autres racines
ne sont pas réécrites. Le cadrage du seul objet transformé est invalidé, puis
recalculé après propagation. Les primitives de collision existantes sont conservées,
avec la barrière Rapier/source exacte toujours requise avant Jouer.

Les fichiers vérifiés restent résidents tant qu'ils sont référencés. La préparation
ne recharge ni catalogue ni GLB pour une collection composée de références déjà
résidentes. Les nouvelles références sont vérifiées, les références retirées libérées,
et le budget de 256 Mio porte sur tous les fichiers distincts désirés, réutilisés inclus.
Les noms de fichiers mémoire incluent désormais ID ET hash de l'asset.

READY est retiré dès une demande, puis rétabli seulement pour l'époque courante
qualifiée. Une réponse tardive est ignorée. Une référence résidente disparue refuse
la mise à jour avant mutation de la projection. Après échec, un nouvel examen de
la même collection peut réessayer. Les sources canoniques ne sont jamais modifiées.

Clippy WASM strict acquis après factorisation du retrait des objets obsolètes ;
pas encore de preuve comportementale d'identités ECS ni de geste Studio pour ce
lot. Le candidat 284B775A ne contient pas ce code. Les scénarios ci-dessous restent
à exécuter ; aucun gain FPS/latence ni clôture R2-050 annoncé.

## Diagnostic du code courant — 15 septembre 2026

État avant ce lot : la reconstruction partielle des placements n'était pas implémentée.
Dans `apps/forge_studio/src/authoring_assets/placements.rs`, `show` compare toute
la collection DESIRED, change l'époque, demande Clear, puis `prepare` relit le
catalogue et vérifie les fichiers de tous les assets distincts de la collection.
`update` appelle `Projection::clear` pour chaque requête, détruit toutes les
racines et retire leurs fichiers mémoire, puis `spawn` recrée toute la collection.
Un changement de transform, de nom ou de verrou suffit donc à emprunter ce chemin.
Ce constat est une inspection du flux, pas une mesure de durée ou de FPS.

## Prochain lot fonctionnel

Conserver les racines des placements inchangés et mettre à jour sur place les
transformations/métadonnées lorsque l'identité et la référence AssetId/hash sont
inchangées. Chargement et remplacement des seules références ajoutées/modifiées ;
retrait des seules racines supprimées. Les fichiers mémoire doivent rester vivants
tant qu'une racine conservée les utilise. Aucun cache canonique supplémentaire.

La sélection, le cadrage et les gizmos doivent suivre la transformation courante.
Une proposition abandonnée doit restaurer les valeurs acceptées, y compris si
un chargement asynchrone se termine tard. Un échec ne doit pas annoncer une
collection partiellement chargée comme prête.

Le contrôle existant `forge_engine::manual_placements_ready` vérifie déjà la
collection exacte, les transformations racines, l'appartenance des primitives
et leur synchronisation Rapier. Le conserver : ne pas remplacer cette barrière
par un simple drapeau visuel. Après une transformation, attendre la propagation
et la synchronisation physique avant Jouer.

Preuves attendues sur ce lot : identité ECS des objets inchangés conservée,
absence de rechargement GLB pour une transformation seule, Undo/Redo/abandon,
remplacement d'asset, retrait, échec et réponse tardive, concordance des collisions.
Un contrôle graphique groupé doit confirmer déplacement et cadrage. Aucun gain
de performance ni clôture R2-050 n'est acquis par ce diagnostic.

## Qualification partielle du candidat v9b — 23 septembre 2026

Le replay isolé `projection-incremental-v9b-20260923-v8` passe 4/4 cas : une
transformation physique d'un placement conserve les identités ECS/mesh/matériau
et handles Rapier des 71 autres ; le déplacement d'un bâtiment puis sa retouche
de porte conservent les huit identités de parties ; Jouer/Retour après
réconciliation concordante réussit, source inchangée. SHA du candidat et limites
dans `GATE2_CLOSURE_WORK_20260923.md`. Il s'agit d'un diagnostic DOM/runtime,
non d'une mesure de frame GPU. Le refus délibéré d'une projection périmée par
l'interface n'est pas observé dans ce replay ; R2-050 reste ouvert. Le test
`forge_project playtest` (3/3) confirme séparément le refus du hash de tête
périmé avant StartPlaytest. Le test moteur ciblé
`manual_projection_rejects_preview_stale_backend_and_wrong_source` passe 1/1
sur Windows : source divergente, racine non appliquée, enfant propagé périmé
et backend Rapier non synchronisé restent non prêts ; le replay Studio du
refus délibéré est encore dû.

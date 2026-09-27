# R1 — Couverture de prairie à budget High redistribué

7 septembre 2026, branche Rust locale, Gate 1 ouverte.

## Implémentation

La recette de végétation et sa fixture passent de quatre à huit tentatives de touffe
par cellule. Le champ de densité zoné existant (32 m, multiplicateurs 0,15 à 1,6),
les seeds, IDs, poses, filtres de pente/matériau/altitude et exclusions ne changent pas.
Il ne s'agit ni d'une retouche manuelle de la carte de preuve, ni d'un nouvel algorithme
de scatter : les règles génériques existantes sont réutilisées par la recette.

Les profils embarqués High/Ultra passent de 64 à 32 brins par touffe. Low reste à 16,
Medium à 32. Aucun plafond de sécurité relevé : huit tentatives/cellule au maximum,
131 072 tentatives globales et 1 048 576 brins développés au maximum. Le pire cas
de la recette à 32 brins reste 4 096 cellules × 8 tentatives × 32 brins = 1 048 576.
Low/Medium peuvent donc rendre davantage de brins qu'avant ; aucun coût inchangé
sur ces profils n'est revendiqué. High redistribue son budget, pas la qualité de tous
les profils à coût constant.

Les documents sauvegardés ne sont pas réécrits. Recompiler un WorldSketch avec cette
nouvelle recette produit une végétation dérivée différente et un nouveau hash ; les
catalogues épinglés à l'ancien hash doivent être explicitement réassociés et validés.
La conservation d'identités générées n'est pas une preuve de retouche humaine Blender
ou de régénération depuis le Studio.

## Preuves de génération et garde-fous

Seize tests végétation monde PASS. Nouveau test : tous les anciens placements sont
retrouvés à l'identique après densification, y compris les touffes ; autres couches
inchangées, environ deux fois plus de touffes. Tests d'exclusion, déterminisme,
génération par chunk et sous-ensembles de densité conservés.

Sur le WorldSketch de preuve : 9 606 → 19 138 touffes ; arbres 636, arbustes 1 099,
fougères 1 167 inchangés. High : 614 784 → 612 416 brins, toujours 64 lots d'herbe,
pas une entité ECS par touffe/brin. Ce n'est pas une mesure de surface couverte.

Les snapshots de hash/comptage des fixtures ont échoué comme attendu au changement
de recette, puis ont été actualisés aux résultats déterministes observés. Le premier
passage workspace a également exposé le total historique 13 977 dans le test de
métriques ; nouveau total de cette fixture 24 645. Aucune limite ou assertion de
préservation n'a été retirée pour faire passer ces contrôles. Le contrôle des profils
a ensuite signalé le hash et le multiplicateur High historiques (64 brins au lieu de
32) ; leurs valeurs attendues ont été actualisées au réglage décrit ci-dessus.

Le premier lancement natif a refusé le catalogue périmé (`phase=prototype_catalog`,
`StaleVegetation`). Échec archivé, contrôle inchangé. La CLI courante a ensuite compilé
le WorldSketch exact dans `compiled/`, vérifiant les références sources et produisant
le hash végétation `e88b4d96b97c6d39ad181d761b95bb6715cb2ec4e1094bf9ccf58bb68c41858b`.
Un catalogue candidat séparé utilise ce hash ; bindings, kit, GLB, pivot et échelle
des arbres sont identiques. Sa validation runtime passe. Aucun ancien catalogue modifié.

## Rendu natif

Racine des preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-meadow-coverage-20260907/`.
`candidate/clear.png` ouverte et inspectée, caméra `human_bridge`, 1920×1080, High clair,
kit gouverné et forêt par lots. Comparaison visuelle avec
`r1-ground-relief-20260907/smoothed/clear.png`, mêmes entrées monde/kit/preview et shader sol.
Plus de touffes visibles, mais sol nu encore important : ne pas déclarer une prairie
dense/naturelle acceptée. La géométrie des touffes conserve le préfixe des 32 brins.

Run terminé sans erreur runtime, 116/116 pipelines prêts, kit 20/20 et 36 colliders.
RX 7800 XT, 239 échantillons après chauffe 180, capture 300, sortie 420, sans VSync :
59,24 FPS moyens, p95 18,0691 ms, 1 % low 27,06 FPS. Ce smoke avec capture ne passe
pas les seuils Gate 1 ; ni amélioration ni stabilité de performance ne sont établies.
Les avertissements préexistants `@invariant`/Equal et profils partiels restent visibles.

CI distante NOT RUN, pas de publication Git, pas de contenu Cadle.

## Validation logicielle finale

`cargo test --workspace --no-fail-fast` : **580 PASS, 0 FAIL, 1 timing préexistant
ignoré**, total calculé depuis `candidate/workspace-pass.log`. Les premières sorties
restent archivées. Le dernier hash périmé concernait le manifeste Studio, désormais
aligné sur la fixture réelle. Clippy strict workspace/all-targets/all-features et
fmt PASS. Aucun test n'a été désactivé et les plafonds sont inchangés.

Runtime capturé : `7b8a785198635e46e87f9eb1a35aab3c261bf193f46147b16e778baf0a40ebc0`.
PNG natif : `e704340faa78b64c925e31aac7e74ba11ff4706af53a888cf00f3379482600d7`.
Hashes fichier des nouvelles fixtures (distincts des hashes JSON canoniques) :
végétation `df0dc4e7a7ea6d86d66ac0e5c52cedeeb74ac6ac8d523371764315501b944389`,
qualité `d9572dbfcf51ed885967f90bcd8b7f4113d0f8c56787192cdadf3473e4ad2c06`.

Clippy strict Studio WASM et build Trunk PASS. Avertissement futur préexistant
`proc-macro-error2 2.0.1` conservé. Bundle Web candidat :
`d9a0c48e8178f85689f4d68bf7ea319603cbdff0154bde54a79fcf92701762f2`.

## Studio Web vérifié

Chrome, port 1449, même caméra candidate `human_bridge`, horloge fixe avancée à 0,250 s
pour initialiser les colliders. PNG `candidate/forge-viewport-1788788560765.png` reçu,
archivé et inspecté (1408×741). Diagnostic `forge-diagnostic-1788788573177.json` :
19 138 touffes, 612 416 brins, 64 lots, kit 20/20 et 36 colliders prêts. Hash végétation
égal à la sortie CLI ; plan de qualité `c5de95845814552512a8744c9243e497f8941eba0bba799bedde6a3289f46d0a`.
Console error vide au relevé. Copies des téléchargements vérifiées par SHA-256.
Le rendu montre plus de touffes, pas une prairie dense acceptée. Capture/JSON non
atomiques, aucune preuve de performance Web, pluie Web, Tauri ou exploration humaine.

Après les validations, le chemin partagé `debug/forge_runtime.exe` a été reconstruit
et porte `ff3262f1178a6464c0404630ca96329c37f5978ba552a85d386f4138da974500`.
Le contrôle de pins l'a signalé ; les sources restent identiques. La capture native
reste attribuée au binaire 7b8a… ci-dessus, pas au nouvel exécutable. Pas de nouveau
lancement natif ni de qualification de ce nouveau hash revendiqués.

# R1 — Dégagements de végétation dérivés du WorldSketch

7 septembre 2026, branche Rust locale ; Gate 1 ouverte.

## Défaut et correction

Le compilateur reprenait les deux exclusions de la fixture autonome : cercle de rayon
14 m centré sur (0, −208) et corridor vertical de demi-largeur 5 m entre (0, −224)
et (0, 224). Il ajoutait ensuite les trois corridors réellement authorés. Or le point
de départ du WorldSketch courant est (15, −225), rayon d'influence 12 m. Le dégagement
de départ ne suivait donc pas la carte ; une bande vide sans chemin authoré persistait.
Ce défaut ne suffit pas à expliquer toutes les zones nues de la vue `human_bridge`.

`compile_world_sketch` remplace maintenant les exclusions de fixture par les cercles
des landmarks `PlayerSpawn`, utilisant leurs position et rayon d'influence authorés,
puis ajoute les corridors des splines comme auparavant. L'identité d'un cercle dépend
de l'ID du landmark, pas de sa position. La fixture autonome garde ses exclusions :
aucun changement de ses données ni de la recette de densité du lot précédent.

La correction concerne chaque WorldSketch compilé, pas des placements faits à la main
dans cette carte. Les documents source, masques, terrain, chemins, shaders et assets
ne sont pas retouchés. Les ensembles générés changent normalement aux dégagements ;
la promesse du lot précédent « autres couches inchangées » ne s'étend pas à ce lot.
Elle ne prouve pas non plus une édition ou sauvegarde depuis le Studio.

## Vérification logicielle

Test nouveau reproduit rouge : cinq exclusions au lieu des quatre attendues. Après
correction, le test vérifie le cercle (15, −225, 12), déplace le landmark à (130, 100)
avec rayon 6, et vérifie le nouveau cercle, son ID stable, le terrain et les corridors
inchangés. Les tests existants de scatter/exclusion et de cycles qualité sont conservés.

Première collecte complète : anciens nombres de référence d'arbres/primitives et hash
de la fixture pré-zonage signalés. Valeurs attendues actualisées aux sorties nouvelles
(650 arbres, 22 341 placements), sans supprimer les assertions ni relever les budgets.
Le baseline pré-zonage du test utilise les dégagements authorés courants, pas ceux de
l'ancienne fixture. Résultat complet : **581 PASS / 0 FAIL / 1 timing ignoré** dans
`final/tests.log`.

Clippy a ensuite refusé la longueur du compilateur (114/100 lignes). La création des
cercles a été extraite telle quelle dans `spawn_vegetation_exclusions`, sans exception
de lint. Les tests du monde sont rejoués après cette extraction ; le passage workspace
ci-dessus appartient à la version avant extraction, sémantiquement identique.

Après extraction : **109 tests monde PASS**, Clippy strict workspace/all-targets/
all-features PASS, Clippy strict Studio WASM PASS, Trunk PASS. Avertissement futur
préexistant `proc-macro-error2 2.0.1` conservé. Le compilateur reste sous la limite de
taille sans `allow`. Nouveau bundle Web :
`e46d59b89f39a1f4d607cfe10e40a1eaad6cf3f9a0ad47ca86c79fa20fe2f5bc`.

## Artefacts et portée

Preuves locales : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-authored-clearings-20260907/`.
La CLI compilée a traité le WorldSketch candidat exact dans `compiled/`. Inspection :
un cercle authoré et trois corridors, aucun corridor de fixture. Comparaison avec la
végétation du lot prairie : seuls les champs d'exclusion changent. Nouveau hash canonique
végétation `1741c578f12b915c4e913b026b591efa9f68fb662c911dda604ebc1e725d057f` ; terrain
carvé toujours `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.

Un catalogue candidat séparé est associé au hash actuel, avec les mêmes bindings et
assets que le lot précédent. Les anciens catalogues restent intacts et périmés pour
cette nouvelle végétation, sans désactivation du contrôle de hash.

Comptages de génération courants : 650 arbres, 1 107 arbustes, 1 189 fougères,
19 395 touffes, soit 22 341 placements. Les paramètres de densité sont inchangés ;
ces différences viennent des zones libérées ou dégagées conformément à la carte.
High développe donc 620 640 brins, toujours sous le plafond existant.

Trois anciens onglets de preuve créés par l'agent (ports 1446, 1447, 1448) ont été
fermés ; fichiers et rapports conservés. Aucune amélioration de performance n'en est
déduite. CI distante NOT RUN, aucune publication Git, aucun asset Cadle.

L'ancien candidat prairie (port 1449) a ensuite été fermé avant le nouveau lancement
natif. Les onglets personnels n'ont pas été touchés. Les différences de sessions
ouvertes excluent une attribution comparative de performance à ce correctif.

## Exécution native

`RunNative.ps1` reconstruit le runtime puis copie l'exécutable dans `native/` avant
le lancement, pour que les builds suivants ne remplacent plus le binaire de preuve.
SHA-256 : `2d7e6d2f2ed6ec491f5cf5687e78710b2200ca4657f9c6cb76f5d83e9186ca8b`.
Capture `native/clear.png` ouverte et inspectée, SHA-256
`1d6bc8b1fec37075aa8b3f13c1f98caa547d4c840b227131c5a9c0ad4b231bd0`.

High clair, caméra `human_bridge`, 1920×1080, kit gouverné et forêt par lots. Code 0,
aucune ERROR runtime, 116/116 pipelines prêts, kit 20/20 et 36 colliders. Les pins
de l'exécutable et des entrées sont vérifiés après exécution. Capture demandée à partir
de 300, effectivement émise à 381 après huit observations stables ; retour GPU à 384.
Ne pas attribuer cette image à la frame 300.

Comptages conformes à la CLI : 650 arbres, 19 395 touffes. La composition change dans
les anciennes zones exclues ; le sol proche reste largement nu. Cette correction de
source ne vaut pas une acceptation artistique, une exploration humaine ou un test
visuel du déplacement du spawn (ce dernier est couvert par le test de génération).

Smoke RX 7800 XT, sans VSync, 239 échantillons après chauffe 180, sortie 420 :
69,92 FPS moyens, p95 17,161 ms, 1 % low 43,55 FPS. Capture incluse, sessions navigateur
différentes : pas de gain de performance attribué, seuils Gate 1 toujours non démontrés.
Les avertissements préexistants de profils partiels et `@invariant`/Equal sont conservés.

## Exécution Web

Le même bundle a été ouvert dans Chrome, vue `human_bridge`, kit gouverné et forêt
par lots. Après un pas virtuel de 250 ms : kit 20/20, 36 colliders prêts,
650 arbres, 19 395 touffes et 620 640 brins ; hash végétation `1741c578…d057f`.
Capture `extracted/forge-viewport-1788789933943.png` (1408×797) inspectée et
diagnostic `extracted/forge-diagnostic-1788789947311.json` archivés ; copies
comparées aux téléchargements par SHA-256. Aucune erreur console observée.

La capture montre encore une couverture clairsemée. Les dimensions diffèrent des
preuves précédentes : ni comparaison pixel à pixel, ni parité native/Web déduite.
Pas de pluie, exploration, Tauri ou benchmark qualifié par ce contrôle.
Le diagnostic conserve `gate_passed=false` et `human_acceptance=false`.

## Diagnostic de couverture suivant — pas une correction validée

Inspection du mesh courant : les racines des brins sont distribuées dans des disques
de rayon au plus `0.8 * patch.scale`. L'échelle maximale authorée vaut 1,25 ; avec
19 395 touffes, la somme des disques maximaux vaut 60 931,19 m² pour une carte de
262 144 m², soit **23,24 % au plus**. Les recouvrements, lobes et échelles inférieures
réduisent cette union. Ce chiffre borne l'enveloppe des racines, pas la couverture
de pixels, les pointes inclinées, ni la fraction des seules prairies éligibles.

Conséquence : le doublement des touffes ne démontre pas un tapis continu. Une simple
hausse du rayon à nombre de brins constant disperse les mêmes brins et ne démontre
pas davantage une prairie dense. La prochaine correction doit traiter ensemble
couverture de proximité, densité par zone et budget, sans relever aveuglément les
plafonds ni supprimer les exclusions authorées. Avant de changer ces paramètres,
isoler la contribution des corridors et filtres de matériau dans le premier plan.
La caméra `human_bridge` est à (-35, 5.4, -205), hors du cercle de départ (15, -225,
rayon 12) : ne pas attribuer le sol nu sous cette caméra à la clairière de spawn.

Ce diagnostic utilise les sources actuelles et le candidat déjà capturé ; aucun
nouveau build, gain de performance ou changement de rendu n'est revendiqué.

### Isolation régionale exécutée

L'exemple réutilisable `crates/forge_world/examples/inspect_grass_region.rs` lit
terrain et végétation sans les écrire et génère cinq variantes en mémoire. Exécution
sur `compiled/terrain-carved.v2.json` et `compiled/vegetation.v1.json`, hashes ci-dessus :

| Rectangle X/Z, mètres (bornes hautes exclues) | Authoré | Sans exclusions | Sans filtre matériau | Acceptation pleine | Les trois |
| --- | ---: | ---: | ---: | ---: | ---: |
| [-55,-15) / [-205,-165) | 70 | 112 | 70 | 129 | 200 |
| [-45,-25) / [-205,-185) | 13 | 32 | 13 | 23 | 51 |

Le filtre matériau n'enlève aucun centre supplémentaire dans ces deux comparaisons.
Les exclusions et la densité ont un effet réel ; les différences ne s'additionnent
pas car les rejets se recouvrent. Le terrain, pentes, altitudes, espacements et IDs
de candidats restent identiques. Pas d'ablation persistée dans la carte.

Le premier plan proche contient seulement 13 centres sur 400 m². Cela localise le
problème au-delà d'un compteur global de 19 395 touffes. Ne pas supprimer les corridors
pour masquer ce résultat : ils représentent des chemins authorés. La suite doit
traiter leur lisibilité au sol et la couverture des prairies adjacentes, pas relancer
une hausse générale des tentatives ou assouplir le filtre matériau déjà innocenté ici.

Exemple compilé et exécuté sur les deux régions ; test des bornes invalides PASS,
Clippy strict de l'exemple PASS, formatage et `git diff --check` PASS. Aucune modification
du moteur de rendu ; pas de nouveau total workspace ou de validation visuelle déduit.

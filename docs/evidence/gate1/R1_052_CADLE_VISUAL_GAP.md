# R1-052 — comparaison visuelle exécutée, écart encore important

2026-09-06. **Comparaison descriptive partielle, pas de PASS R1-052 ou Gate 1.**
Ce lot n'ajoute aucun code moteur ni asset. Il confronte le runtime actuel à la référence épinglée
pour sortir de la succession d'audits locaux sans verdict visuel d'ensemble.

## Exécution contrôlée

Cadle canonique : `scripts/Get-Cadle.ps1 -VerifyOnly` PASS avant/après, commit détaché
`ea0da62549980816a6a27e08b75808b1f56d70e7`, arbre propre. Clone dérivé existant :
`artifacts/cadle-gate-c/qualification-script-c`, même commit, seule différence suivie
`package-lock.json`, hash `85f9b294da3d67fc698631b4b9903ad9a33214044573db82ed86e4b09d2f1c1f`.
Node 22.22.3/npm 10.9.8. Pas de réinstallation ou modification du clone canonique.

Harness et scripts relus avant exécution ; dossiers de sortie et mutex absents avant lancement,
aucun ancien résultat effacé. Serveur Vite limité à 127.0.0.1:14640, arrêté à la fin après
vérification de son PID/commande. Contrôle de l'arbre servi actif, jamais contourné.

```text
node tools/inspect.mjs --name r1-ab-forest-20260906 --w 1920 --h 1080 --q high --seed 1337
  --script tools/scripts/fin/forest.json --url http://127.0.0.1:14640/
node tools/inspect.mjs --name r1-ab-vale-20260906 --w 1920 --h 1080 --q high --seed 1337
  --script tools/scripts/fin/vale.json --url http://127.0.0.1:14640/
```

Six PNG 1920×1080 inspectés (face/côté/sol, forêt et prairie), zéro erreur globale ou de step,
personnage vivant. RX 7800 XT réelle via ANGLE/D3D11, High, seed 1337, heure 13.
Scripts de qualification existants : attente initiale 31 s et stabilisation après téléportation.
Les médias et rapports bruts Cadle restent dans les deux dossiers `tools/out/r1-ab-*-20260906`
du clone ignoré ; aucune copie dans les sources ou dans les sorties Forge.

Forge : binaire du lot collisions d'arbres, SHA-256
`56f92b1c434b46148186954bf69ca02adc5c33b93d8caaf31e4d3221dea5de55`.
Deux lancements séquentiels avec `--world-sketch-visual-valley`, kit gouverné, `--weather clear`,
`--quality high --no-vsync`, capture 600, warmup métriques 240, sortie 1200.
Caméras existantes `human_bridge` et `hero_reference_match` ; seul `camera_id` change dans une
copie dérivée d'aperçu pour la première. Aucune pose du WorldSketch ou placement retouché.
Deux PNG Full HD inspectés, sorties 0, aucun ERROR, kit prêt 20/20 instances et 36/36 colliders.
Terrain `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208` commun aux deux.

## Constat visuel de l'agent — ne vaut pas acceptation humaine

| Critère observé | Référence | Forge actuel | Travail nécessaire |
|---|---|---|---|
| Couverture végétale proche | Prairie dense, sous-bois stratifié avec zones de sol nu | Quelques touffes isolées au premier plan, grandes surfaces vertes nues | Couverture par zones naturelles et vraies touffes, pas seulement une multiplication globale du scatter |
| Arbres | Troncs texturés, branches et canopée formant des masses | Majorité de petits conifères procéduraux très lisibles comme primitives ; quelques modèles du kit isolés | Raccord gouverné du rendu des arbres au scatter, échelles cohérentes et continuité proche/loin |
| Matières | Sol et feuillages détaillés, variations perceptibles près du joueur | Grain de sol faible, palette uniforme, silhouettes anguleuses | Détail PBR et variations de surface cohérentes avec les biomes |
| Composition | Relief lointain et masses forestières structurent l'espace | Horizon peu structuré, vallée ouverte, rivière très régulière ; pont lisible | Composition originale à améliorer sans reprendre le layout, les landmarks ou l'identité Cadle |
| Artefacts | Grandes cartes de feuillage visibles et transitions de silhouettes imparfaites | Pointillés de végétation très visibles, bandes régulières sur l'eau | Corriger les défauts Forge ; ne pas prendre ceux de la référence pour une cible |

**La parité visuelle n'est pas démontrée ; les images montrent encore un écart important.**
Les 399 tests du lot précédent qualifient des comportements, pas cet objectif artistique. Le kit
chargé et les 636 collisions d'arbres ne rendent pas à eux seuls la vallée convaincante.

## Coût : observations conservées, comparaison non qualifiée

Forge natif Vulkan, driver annoncé 26.8.1, 959 échantillons par vue :

| Vue | Moyenne ms | p95 ms | 1 % low FPS | Frames >33,3 ms |
|---|---:|---:|---:|---:|
| humaine | 10,228 | 11,060 | 39,67 | 2 |
| héroïque | 10,164 | 11,062 | 63,88 | 1 |

Ces fenêtres incluent la capture et ne constituent pas un benchmark. Aucune mesure de cadence
Cadle n'a été demandée par ces scripts visuels (`stats=[]`). Ne pas comparer ces nombres aux
chiffres du 30 août comme s'ils étaient contemporains. Aucun classement de performance déduit.

Même GPU physique et résolution ne suffisent pas : backends, scène, superficie (Cadle environ
2 km² contre Forge 512×512 m), lumière, FOV et charge diffèrent. Le High global Forge reste
`full_preset_applied=false` (eau/environnement partiels). Les applications de l'utilisateur en
arrière-plan n'ont pas été arrêtées. Restent les mesures comparables par système, le midrange,
les douze vues Forge normalisées actuelles, la revue humaine et le rapport final de gate.

## Preuves conservées

Forge : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-ab-20260906`.

| Artefact | SHA-256 |
|---|---|
| Cadle forêt `report.json` | `bc96e75429437da05da790c9ddc64b84cd5274d637ac116c11a88883b6452888` |
| Cadle prairie `report.json` | `aa81edb61bf371536295f9dc52b958d39b9e7ce5fb48613b375325e694d89cd9` |
| `forge-human.png` | `9778ce7458789c347f9bb59a4005f2b6a532b24acce893ce226127a0005a169e` |
| `forge-human.json` | `423a435e38f39d8590d5a5fc36a690fe7d5cb664e1f7c65877fd3e04d1b9e70f` |
| `forge-hero.png` | `c1860714214caebfc35d86952322fdfdd0ab17050703aef37bc87955e9853a8e` |
| `forge-hero.json` | `2ff7bdb607b08c4eb821214068c5c345b91c9c3066e7af2e6988a9783566ef60` |

Prochaine priorité : représentation végétale à l'échelle de la scène, en conservant les sources,
placements authorés, collisions et budgets de qualité. Ne pas relancer un audit CPU sans anomalie
liée à ce travail. Cela reste du développement Gate 1, pas une autorisation de Gate 2.

## Première correction exécutée : touffes courbées et fondu terminal

Suite locale du 2026-09-06, après les captures A/B ci-dessus. Implémentation dans
`crates/forge_engine/src/vegetation.rs` : distribution irrégulière lobée dans l'empreinte existante,
lames courbées et effilées (7 sommets, 3 triangles contre 4/2), largeur 4,5–10 cm et hauteur
22–65 cm avant l'échelle du placement. Aucun asset externe ou shader ajouté. Les racines restent
projetées sur les triangles du terrain, les deux sections de chaque lame se rejoignent exactement.

Les budgets de qualité passent de 12/20/32/48 à **16/32/64/64 brins par touffe**. Le profil de
présentation canonique sans override garde son budget existant ; le doublement concerne High.
Le plafond de 1 048 576 brins n'est pas augmenté. Golden du profil révision 1 :
`c64ffddf6ae0adcff1f85a4e63ad5d5ebe40d6bdf70f42bc3ef4c35c52b6f28d` ; hash adapté à la révision
3 du WorldSketch observé au runtime : `561577b6b0d029fe7272307748b3acff80e2cd8e26c95b43c6090e5ddb9f203a`.

La première capture (`human.png`) montrait encore des touffes trouées : le fondu d'un lot entier
s'étalait de 24 à 72 m en High. Le fondu final est maintenant borné à huit mètres au maximum,
soit **64–72 m en High**, sans étendre la portée. Les plages arbres/HLOD sont inchangées ; leurs
pointillés ne sont pas déclarés résolus. `human-final.png` montre les touffes proches pleines,
mais toujours de grandes zones nues : **amélioration locale retenue, couverture globale non acquise**.

Vérifications finales : `cargo test --workspace --locked -q` **400 PASS**, Clippy workspace
tous targets/features avec warnings en erreurs PASS, check moteur WebGPU/WASM PASS,
build natif PASS, formatage et diff ciblé PASS. Le nouveau test borne le fondu sans allonger
la visibilité ; les tests de géométrie conservent leur tolérance d'ancrage terrain. Leurs strides
ont été adaptés aux sept sommets (un ancien stride de quatre lisait les pointes comme des racines),
et le golden/budget High a été mis à jour pour la modification voulue. Aucun check supprimé.

Smoke natif final : mêmes caméra, terrain, GPU, résolution, météo et paramètres de collecte que
`forge-human` ci-dessus, sortie 0 sans ERROR. 7433 placements, 636 arbres, 3666 projections,
64 lots d'herbe et kit prêt 20 instances/36 colliders conservés. High : 289 984 brins,
885 776 triangles uniques végétation contre 305 808 auparavant ; ce compteur décrit les meshes,
pas les triangles réellement dessinés. 959 échantillons, moyenne 10,048 ms, p95 10,829 ms,
1 % low 65,41 FPS, un frame >33,3 ms. C'est une fenêtre avec capture, pas un gain de performance
qualifié, ni une mesure milieu de gamme. Le rendu Chrome de cette correction reste à exécuter.

Artefacts conservés dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-curved-grass-20260906` :

| Artefact final | SHA-256 |
|---|---|
| `forge_runtime.exe` | `189a8d173c8d81e79be40b5681b4dd265a40b7238899171f4756eb774e535599` |
| `human-final.png` | `84aa06765d21accfc6d9ef62c6d3a6fa05cd2ab83b46259031bbabd9a33c7a1b` |
| `human-final.json` | `b7b7861554a17b07a3285a38d1a4b9445d0ac18174c21483b3d8986b437ca4b8` |
| `tests-final.log` | `beef209ff04a393bdc8170eb0d5f0a14cdf2d6131aa9698a7c030da3d6cd70d9` |

Les fichiers `human.*` et `tests.log` sont des étapes intermédiaires, pas la preuve finale.
R1-049, R1-052 et Gate 1 restent ouverts. Restent notamment la couverture par zones naturelles,
les arbres à l'échelle de la scène, les matières, les transitions lointaines et la revue humaine.

## Deuxième correction exécutée : densité spatiale par zones

Lot local du 2026-09-06, sans publication Git. `VegetationLayer.density_pattern` est un champ
optionnel du document existant, sans nouvelle représentation canonique ni dépendance. Son absence
conserve le scatter uniforme et n'ajoute aucune clé à la sérialisation. Sa validation borne la taille
de zone à 4–512 m et les multiplicateurs ordonnés à 0–2, tous finis. Un bruit de valeur lissé,
déterministe en coordonnées monde et selon la seed/couche, module la probabilité des candidats.
Le renderer de densité qualité utilise cette même probabilité ; une zone nulle reste vide à Ultra.

Seule la recette d'herbe change : probabilité 0,32 → 0,8 ; zones de 32 m, multiplicateurs
0,15–1,6. Le nombre de tentatives reste 40 960, dont 16 384 pour l'herbe, et le plafond de
1 048 576 brins n'est pas augmenté. High reste à 64 brins par touffe et 64 lots d'herbe.
Le document végétation et son cache changent volontairement ; ne pas les déclarer intacts :

- fixture canonique : `ac3335d560385e548d4c05e9c8d9211a400da36ef601ab6d60942a3ef7fb9bdc` ;
- cache de la fixture : `c086cd80d34a831f03672ed06c885f8acdb745ba131f09d92f69e00984be8b97` ;
- végétation WorldSketch rev3 : `49819e19e9baa077d040ee1d1f64474d084d86e200b3eef940e4e7ab8c084f30`.

Le WorldSketch et le preview authoré restent byte-identiques aux empreintes précédentes
(`5b208895…`, `bde50c49…`), et le terrain runtime garde `18bd933d…`. Les tests reconstruisent
l'ancien document (`83b3c8c0…`) sans pattern, comparent toutes les poses non-herbe exactement,
les poses des candidats survivants et toutes les exclusions, puis rejouent la génération.
Les goldens de fixture et les comptes attendus ont été ajustés à la nouvelle recette explicite,
sans retirer les contrôles de filtrage, d'espacement, d'identité ou de bornes.

### Vérifié

- Workspace : **404 tests PASS** (4 nouveaux tests : contrat/round-trip, champ spatial,
  préservation des poses/exclusions, zone nulle malgré la densité qualité).
- Clippy workspace tous targets/features strict PASS ; check moteur WASM/WebGPU PASS ;
  build runtime PASS ; formatage et `git diff --check` PASS.
- Capture native Full HD High, clair, caméra `human_bridge`, RX 7800 XT/Vulkan, sortie 0,
  zéro ERROR ; avertissements wgpu `@invariant` présents, non corrigés par ce lot.
- **9 606 touffes contre 4 531**, soit 12 508 placements au total. Toujours 636 arbres,
  1 099 arbustes, 1 167 fougères et 3 666 entités visuelles. 636 colliders de troncs matérialisés ;
  kit prêt avec 20 instances, 36 colliders, aucune scène échouée.
- High : 614 784 brins, 1 860 176 triangles de meshes végétation uniques. Ce n'est pas
  le nombre de triangles effectivement dessinés. La caméra observe encore deux lots d'herbe.

Commande de capture : même invocation que la correction précédente, avec le binaire et les
sorties sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-zoned-grass-20260906`, `RUST_LOG=info`,
`--world-sketch-visual-valley`, le build kit `r1-063-authored-preservation-20260905-b/rebuild-1`,
le preview `r1-ab-20260906/preview-human.json`, `--weather clear --quality high --no-vsync`,
capture frame 600, warmup 240 et arrêt 1200. Les fichiers sont `human.png`, `human.json`,
`human.log`. La capture a été ouverte et inspectée par l'agent.

Fenêtre native avec capture : 959 échantillons, moyenne 10,079 ms, p95 10,953 ms,
1 % low 41,07 FPS, deux frames >33,3 ms, maximum 48,333 ms. **Pas un benchmark comparatif** :
ne pas déduire une amélioration ou une régression qualifiée de ces seules fenêtres avec capture.

### Limites et suite

La distribution par zones est implémentée et reproductible ; la capture montre davantage de
touffes, mais toujours de grandes plages découvertes. **La couverture naturelle n'est pas acquise**.
L'empreinte des touffes, la portée par lots et la représentation des arbres restent des contraintes
visuelles distinctes. Ne pas les masquer en augmentant simplement tous les budgets. Le rendu Chrome
de ce lot n'a pas été exécuté ; le check WASM ne le remplace pas. Aucune retouche manuelle ou
régénération via l'interface Studio n'est revendiquée. Aucune ressource Cadle copiée.
Gate 1/R1-049/R1-052 restent ouverts, CI distante NOT RUN et revue humaine toujours requise.

| Artefact de ce lot | SHA-256 |
|---|---|
| `forge_runtime.exe` | `a3ef12db8d3d178686aacbba46e05ec226270e48666589feb1035d2f2f529278` |
| `human.png` | `8e8a27ca9e71b9b02c15256cb255db6108143edd7c461fc9b4d4df27fd61b60b` |
| `human.json` | `ec9d27e86df4ba7cea19da646960045ec5bc84eda898ddbd3cbf8d5346a64a7a` |
| `human.log` | `0d29f8564cb6f8ae0e70b24645f4ee51c22f6f68f9e282187b5e242f697301f6` |
| `tests.log` | `e0a8c49184fde54995fd9aef42014b5416edc8eb6a4de7a83235d4b15d9f3c36` |
| `clippy.log` | `7db229656d7976a1d09f681d8bc1b3babfc3540b48e860f2249484a079f64527` |
| `wasm.log` | `e10020301634b876e2fabb81e7a63d1d72e73f445393f34022f510f0abc6d4bd` |
| `build.log` | `c7f41ebbfb469a15861fef76cfd11e85a06686666f5e4879dba0c80d61956ee9` |

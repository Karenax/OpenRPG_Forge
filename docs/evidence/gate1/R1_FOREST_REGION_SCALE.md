# R1-050 — Densité forestière régionale et échelle adulte

8 septembre 2026, branche Rust, base `f786a38aca2023f08d708687f99a47d14f34e6d4`.
Travail local non publié, changements préexistants conservés. Gate 1 ouverte.

## Demande et implémentation

Le propriétaire refuse le candidat précédent : arbres trop petits et espacés,
aspect de plaine. Cible : arbres adultes 5–10 fois plus hauts que le personnage.

- Le personnage reste à 1,80 m. Aucun changement de sa capsule ou du spawn.
- `VegetationDensityMask` est un champ sérialisé optionnel, grille carrée 2–257
  sommets par axe, échantillons u8 et validation stricte. Le scatter l'interpole
  bilinéairement et conserve filtres de pente, matériaux, exclusions et distance.
- `WorldSketchForestScatter`, optionnel, règle acceptation, essais, espacement et
  échelle. Le compilateur combine les régions Forest par maximum puis soustrait
  Buildable. Sans cette option, comportement historique conservé.
- La vallée opte pour probabilité 0,85, huit essais/cellule, espacement minimal
  4 m, échelles 1,6–3,15 et variation spatiale. Les prototypes gouvernés font
  5,65 m à échelle unité : hauteur résultante 9,04–17,7975 m, rapport 5,02–9,89.
- Résultat déterministe : 1 662 arbres/colliders, 23 353 placements de végétation.
  La forêt occupe sa région existante ; les autres zones restent des prairies.
- Aucun GLB, asset Cadle, placement du kit ou terrain sculpté modifié. Les pins
  WorldSketch/végétation du nouveau kit candidat sont actualisés explicitement.

Il s'agit de règles partagées pour les prochaines générations, pas d'un décor
ajouté à la main. Ce lot ne crée pas d'outil Studio d'édition du masque, ni de
nouvelle promesse d'undo/régénération conservant toutes les retouches : ces
parcours doivent conserver leurs propres preuves. Les poses générées changent
intentionnellement avec la recette ; les placements authorés ne sont pas modifiés.

## Vérifications réellement exécutées

### Parcours réel jusqu'au sous-bois — 8 septembre

Harness natif `--walk-proof ticks:axis_x:axis_y` ajouté dans l'application Runtime,
pas dans le domaine ni le renderer. Il réutilise `PendingPlayerControlCommand` et
le contrôleur Rapier, attend kit prêt et 120 ticks au sol, applique une marche
bornée puis 120 ticks neutres pour freinage. Aucun accès mutable à la pose joueur,
aucune navigation inventée, pas de téléportation ou de modification des sources.
Paramètres bornés et conflits de probes refusés. 78 tests Runtime et un test
d'intégration PASS ; Clippy Runtime tous targets strict PASS, build natif PASS,
format/diff PASS. Ce changement ne touche ni les contrats partagés ni le WASM.

Racine externe `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-walk-proof-20260908`.
Binaire `1F0A83D63C697ABB5ED832AF044EC4B40A59DF44FB4AEBC3DDCBC0AF78BB8D4E` ;
kit BLEND du candidat forestier inchangé, High natif, RX 7800 XT/Vulkan, Full HD.

- `Replay.ps1`, 1400 ticks, axe -0,5:1 : marche réelle vers l'est, fin
  (60,5365 ; -3,1721 ; -117,9229), au sol et vitesse horizontale nulle. Capture
  acquise, mais hors forêt : ne pas la présenter comme destination atteinte.
- `Replay-west.ps1` par défaut, 1400 ticks, axe 1:0,22 : fin près de
  (-100,125 ; 0,5972 ; -207,4993), au sol ; deadline 90 s avant capture frame 2400,
  processus 36720 arrêté par le garde-fou. Échec de capture conservé, pas effacé.
- `-Label west2 -CaptureFrame 1800 -ExitFrame 1900` : même trajet terminé,
  capture et sortie 0. La capsule est masquée par l'eau : `grounded` signifie
  contact terrain, pas berge sèche. Cela ne prouve pas une panne des collisions.
- `-Label west3 -WalkTicks 1800 -CaptureFrame 1700 -ExitFrame 1800` : trajet
  prolongé jusqu'à (-132,4952 ; 2,5993 ; -203,5164), grounded=true, vitesse nulle,
  événement de fin avant capture, sortie 0. Image inspectée : capsule visible
  dans le sous-bois, troncs adultes proches, canopée au-dessus du cadre. La forêt
  n'est plus vue uniquement depuis la prairie. Pas de mesure photographique exacte
  du rapport 5–10 (cimes hors champ), ni d'acceptation artistique humaine.

**Nouvelle limite prioritaire révélée par ce parcours** : l'overlay glissant
(240 images, non benchmark) de `west3.png` affiche moyenne 75,30 ms, p95 76,73 ms,
1 % low 12,4 FPS, instantané 13,4 FPS ; 10 257 524 triangles visibles CPU annoncés.
Sous-bois très sombre. `west2.png` était vers 16 FPS au bord de l'eau. Les mesures
40 FPS depuis la caméra héroïque ne caractérisent pas ce coût de proximité.
Le test alpha historique sans gain depuis cette autre caméra n'écarte pas un coût
du feuillage en sous-bois : une comparaison à ce nouvel emplacement serait une
charge différente, pas une répétition du benchmark héroïque inchangé. Ne pas
promouvoir MASK sans mesurer, ni réduire arbres/personnage pour masquer le problème.

| Capture | SHA-256 |
| --- | --- |
| walk.png (est, hors forêt) | `B9A9A6CD6C023280C754A1961D35D0C1FF27160FE5D5B415DA18E7FA70349555` |
| west2.png (lit de rivière) | `F91AC1464D3420A2449CC634AFB744B1F2B15B9C9CD897B544431218FD514155` |
| west3.png (sous-bois) | `30A7AAF31A734787960E64A88993B257F23D2B33CE919935277A7F3FA2E8820A` |

PIDs 28372, 36720, 27096, 10816 terminés ; aucun runtime de ce chemin restant.
Aucun navigateur lancé. Ce parcours qualifie un replay d'entrées natif et une
observation locale, pas le clavier physique, le Studio ou la Gate 1 entière.

### Complément du 8 septembre — personnage effectivement instancié

Inspection de `apps/forge_runtime/src/main.rs` : le profil joueur n'est créé
qu'avec `player_camera.is_enabled()` ou le probe de traversée. `Replay-human.ps1`
n'activait aucun de ces chemins. Son absence de personnage n'était donc pas
seulement un problème de cadrage : cette exécution n'en instancie pas.

Essai séparé, même binaire et kit, ajout explicite de `--player-camera`, sans
caméra fixe ni changement de spawn, carte, géométrie ou taille. Capture native
1920 × 1080 à la frame 400, sortie 0 à 600, `capture_verified` reçu. Le PNG montre
la capsule bleue à son spawn dans la prairie, forêt distante sur la droite.
**Cette image ne prouve toujours pas le rapport apparent 5–10 à distance égale** :
il faut rejoindre la lisière avec le personnage. Ne pas déplacer les arbres
ou réduire la capsule pour fabriquer cette preuve, ni répéter ce cadrage inchangé.

Preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-player-20260908`,
script `Capture.ps1`, journaux et `player.png`, SHA-256
`01450977B1AD0EB87B2618DA79E362E3FBE9910442C539DE8679FDB6E11E10A8`.
PID 16484 terminé, aucun runtime restant. L'overlay instantané de cette caméra
ne vaut pas benchmark et ne se compare pas directement aux essais sans personnage
depuis la caméra du kit. Aucun passage Gate 1 ni acceptation visuelle déduit.

- `cargo test --workspace --all-features --offline --locked -q` : **619 PASS,
  zéro échec, un timing CPU déjà ignoré**. Nouveaux tests : validation/roundtrip
  du masque, interpolation/zéro, forêt déterministe avec hauteurs et exclusions.
  Les attentes de comptage du candidat sont mises à jour, pas désactivées.
- `cargo clippy --workspace --all-targets --all-features --offline --locked --
  -D warnings` : PASS. `cargo fmt` et vérification du diff : PASS.
- CLI réel : compilation du WorldSketch et contrôles de sources PASS.
- Build natif + CLI PASS. Exécution native Vulkan High avec kit et forêt :
  sortie 0, capture 1920 × 1080 inspectée, aucun événement ERROR, 1 662 arbres
  et colliders réellement chargés. Vue d'ensemble : canopée dense sur le côté
  forestier de la rivière, prairie maintenue ailleurs. Ce n'est pas une capture
  comparative à hauteur humaine avec personnage visible.
- **Performance non qualifiée** : capture native dev optimisé, RX 7800 XT,
  599 échantillons après 300 frames de préchauffage ; moyenne 25,145 ms
  (39,77 FPS), p95 27,377 ms, 1 % low 31,87 FPS. Sous l'objectif 60 FPS.
  Pas de gain A/B ni de qualification GPU cible déduits de cet essai.
- Processus natif PID 45772 terminé, zéro instance restante de cet exécutable.

## Artefacts reproductibles locaux

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-scale-density-20260908`.
`Replay.ps1` conserve les paramètres, la limite de durée et le nettoyage ciblé.
`compiled/` contient les documents dérivés ; `forge-kit/` contient la copie
gouvernée repinnée, sans changement des six GLB ou du rapport de construction.

| Entrée / sortie | SHA-256 |
| --- | --- |
| WorldSketch canonique | `a92895b40f0c12454cede9f9ce5797cc7df217c4d152335c26687bee36463558` |
| Végétation compilée | `89ceeea8569c8940c863ca8682808baa50c3a9c0af5e2aa34b366667e02ac2d8` |
| Terrain sculpté inchangé | `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208` |
| tests.log | `166F56482C9232DE8EE80B0E961DAF06AF1DD9718F2DBAC10380B16250B2E0E4` |
| forge_runtime.exe | `16011A7DE419835D8AAD93B03B97104DAA5A0A662629FF8A6F6763D7A89F954C` |
| forest-native.png | `D9BAF0D47A8F685203E26D5E1C0EA6523848E70CCCD12FF88D57B5098117CF7B` |
| forest-native.json | `234F44E873481BF1A93201A96B210B8989A22ABBD5B72C6B5095890BD49EDCBD` |
| forge-kit/preview.json | `5FB5521E7A34888D166EA23D515AA4C2287E5B645637A3BC7A111738B5D7DEF4` |
| forge-kit/prototype-catalog.json | `38B4BCAC0C82C07D932973F7369D8C12030CFCCABC652EB786F24D7CC625633A` |

## Studio et statut

WASM release construit avec Trunk/Binaryen 131, deux workers, offline/locked,
41 137 942 octets. Le kit repinné est copié et hash-vérifié. Les vues de diagnostic
candidates reçoivent le même champ forestier pour éviter un document périmé ;
leurs caméras ne sont pas exercées dans ce smoke.

`scripts/Build-StudioCandidate.ps1` assemble le même frontend dans le shell Tauri
debug, sans publication. Sortie fraîche :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-tauri-forest-scale-density-20260908`.
`build-proof.json` épingle toutes les entrées et reste honnêtement BUILD_ONLY ;
les preuves d'exécution distinctes sont décrites ici.

Ouverture réelle avec le skill Computer Use, une seule instance et une seule
initialisation du monde. Après chargement, kit 20/20 avec 36 colliders, forêt
1 662 arbres / 19 944 primitives LOD, hashes alignés. Le viewport montre le
personnage au spawn existant dans la prairie et la forêt dense en arrière-plan.
Ce cadrage ne prouve pas un rapport de taille à distance égale ; la hauteur est
contrôlée par les sources et tests, une comparaison proche reste à présenter.
Le premier affichage sombre se stabilise ensuite : pas de qualification de
préchauffage ni de continuité exhaustive déduite.

Exports UI réellement reçus puis copiés sans modification : diagnostic
`forge-diagnostic-1788871185110.json`, capture `forge-viewport-1788871195910.png`.
PNG **1408 × 966 inspecté**, cible JSON `tauri_studio`, végétation hash `89ceee…`,
WorldSketch `a92895…`, terrain `18bd93…`, 23 353 placements dont 1 662 arbres.
Capture GPU reçue dans l'UI ; le ledger de démarrage garde sa limite
`GPU presentation not_observed` et ne doit pas être réétiqueté par ce rapport.
Capture et JSON ne sont pas atomiques, `human_acceptance=false`, `gate_passed=false`.
120 frames après 60 warmup : 32,60 FPS, moyenne 30,676 ms, p95 37,30 ms ;
série courte de démarrage, pas benchmark qualifié ni comparaison native/Web.

| Artefact | SHA-256 |
| --- | --- |
| WASM | `1B5D3E11F95AF92116CAD191DA1073236BF8F72C4E73D33CEACAE99B1C120074` |
| forge-studio-candidate.exe | `E9139A109345AE42EBB01DDBDEF0663BFE212C41F6FEBC3D7ACEA0BBD8811DBA` |
| studio-diagnostic.json | `5A8FD47C65A9ED2645A7668FC863AA1FB6BC9F5051CB24E0777AE39EA8EFD02F` |
| studio-viewport.png | `FCFF96B3799EC9FF9A581117EF193BD86CBB772CE441A4F0AA442A37DF40688B` |
| studio-loaded.jpg (premier éclairage sombre) | `ED916672DB69915D67292D3EB3045A1CE8390BAEEE8E3848EDCC620572B342C8` |

Fermeture Alt+F4 puis contrôle par chemin exact : **zéro processus candidat**,
zéro WebView2 associé à ce dossier ; session Node de pilotage réinitialisée.
Pas de navigateur ou runtime conservé pour ce lot, aucun processus tiers tué.

Acceptation artistique du nouveau candidat non donnée, Gate 1 non validée.
Performance cible, rendu à hauteur comparable, finition et autres critères
globaux restent dus. CI distante NOT RUN sous ADR-0006. Aucune Gate 2 commencée.

## Complément de cadrage — même candidat, 8 septembre

La continuation suivante reprend le binaire natif épinglé ci-dessus, sans build
ou modification des sources, avec `--fixed-proof-view human_bridge`. La caméra
authorée est à `[-35, 5.4, -205]`, cible `[-40, 11.4, -90]`, FOV vertical 62°.
Ce sont des coordonnées absolues : le nom `human_bridge` ne constitue pas une
mesure indépendante de hauteur des yeux au-dessus du terrain.

`Replay-human.ps1` dans la racine de preuve conserve les arguments : kit identique,
High, capture à partir de frame 400, sortie bornée à 600, deadline 60 s et
nettoyage du seul processus lancé. Sortie 0, PNG reçu et inspecté en 1920 × 1080,
pas d'erreur runtime ; PID 16976 terminé et absence par chemin exact vérifiée.

`forest-human.png` SHA-256 :
`2E025810C4CFD5D9D57C3B71A30E26BAFBDAB04ACE5298867B1C45B82B3F8EA0`.
Journaux : `human-stdout.log`, `human-stderr.log`.
Les événements sont sur stderr (stdout vide) : caméra stable, temps virtuel/fixe
à zéro, demande frame 400 après 113 pipelines prêts, readback frame 403,
capture vérifiée et sortie 600 sans ERROR. SHA-256 de `human-stderr.log` :
`B63D10AD67E9D321C1987ECFF16BCA4DCE973BBC8B19342AB5CB3ECFCD691C71`.

Observation : arbres adultes dominant le pont, lisière dense derrière celui-ci,
prairie conservée de l'autre côté. Cette vue basse permet de juger le changement
de composition par rapport aux anciens arbres clairsemés ; elle ne montre pas
un personnage placé au pied d'un arbre. Ce dernier repère reste absent. Les
timings de l'overlay figé ne sont pas un benchmark. Aucun nouveau test global,
aucune qualification artistique ou passage de gate par cette seule capture.

## 9 septembre — portée lointaine, pas une nouvelle densification

La vue globale Studio courante révèle une disparition des arbres au loin.
Le profil High borne `cull_distance_m` à 384 m ; `VegetationLodRanges` applique
cette borne aux lots et `hlod_placement_dither` rejette chaque racine au-delà.
Les objets du kit ont un chemin de visibilité indépendant. Le candidat modifie
uniquement les portées végétation des presets : High 384→640 m, Ultra 512→768 m.
Low/Medium, densités, transitions proche/moyenne/HLOD et budgets d'herbe restent
identiques. La présentation de base du document végétation conserve 384 m.
Il ne s'agit pas d'une adaptation automatique à l'étendue de toute future carte :
les distances restent des paramètres explicites du profil partagé.

Le test golden et le test de correspondance High/base ont d'abord échoué comme
attendu (journal conservé `tests.log`). L'empreinte attendue a ensuite été actualisée,
et la différence de portée est assertée explicitement (base 384, High 640), sans
retirer les autres invariants. 16 tests profil PASS et build natif PASS.
Hash canonique du profil candidat :
`91f44b501b80fef267d1f7af69db1b0c056e1679c09d43ebabb4be175007c03b`.

Preuves externes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-distance-20260909`.
Deux smokes séquentiels, même machine RX 7800 XT, même kit
`r1-atlas-cropped-runtime-kit-20260909`, caméra de preview inchangée, High végétation,
eau optique, forêt en lots. Arguments : `--world-sketch-visual-valley`, les trois
entrées kit build/preview/catalog, `--prototype-forest-batches --vegetation-quality high
--water-depth-preview --water-reflection-preview --metrics <json>
--metrics-warmup-frames 180 --exit-after-frames 600`. Aucune compilation en parallèle
de ces mesures. Sorties 0 ; 420 frames mesurées chacune, pas une qualification cible.

| Relevé court | Avant 384 m | Candidat 640 m |
| --- | ---: | ---: |
| FPS moyens | 66,87 | 61,57 |
| p95 ms | 16,51 | 17,94 |
| 1 % low FPS | 51,44 | 43,29 |
| Triangles visibles CPU | 1 520 122 | 1 744 504 |
| Lots HLOD visibles CPU | 149 | 186 |

Coût observé : environ 7,9 % de FPS moyens en moins. Une paire courte ne caractérise
pas la variance ; ne pas revendiquer gain, 60 FPS qualifiés ou seuils Gate 1 passés.
Nombre de meshes/triangles alloués identique, 1662 arbres et 23353 placements dans les
deux diagnostics. Hashes terrain/végétation, compteurs placements et projection
comparés identiques ; le profil de qualité, lui, change volontairement.

Capture supplémentaire `--fixed-proof-view valley_overview --capture overview.png
--capture-frame 180 --exit-after-frames 300` (sans métriques) : sortie 0, PNG Full HD
ouvert/inspecté. La forêt est présente jusqu'au fond de la rive droite au lieu d'une
coupure ; la prairie de gauche reste une prairie. Bord du monde et style restent
non résolus. Aucun placement, asset, shader ni collision ajouté par ce candidat.

- runtime candidat : `8DC9B54E86093D2C211906A0B9EDDE74CF98C70CDDEF9444FD105FC0ABD24567`.
- `overview.png` : `D8D2ECAEFCF60AAF5080D13F6C43300409D17C20999256D190669DC9986B74F9`.
- `before-metrics.json` : `6B1AD658E094920B72A04D5D0BC9D9981E34E51B0FC2DC47F67C430DC0E7C8A2`.
- `after-metrics.json` : `98D22D8C5AC0B672FA1439580A353B833F876B01EB69BC0E449105BA33845B73`.

Tous les essais sont terminés, absence de runtime vérifiée. Candidat local non qualifié :
intégration Studio des nouvelles portées, Ultra visuel et qualification performance
restent dus. Les paquets Studio précédents sont conservés. Gate 1 reste ouverte.

Contrôles transversaux terminés pour ce candidat : 678 tests workspace PASS,
un timing ignoré, Clippy workspace/all-targets/all-features avec `-D warnings`
PASS, check frontend WASM/Web PASS, format et diff-check PASS. Commandes Cargo
`--locked -j2`, caches E: comme le lot terrain ; journaux `workspace-tests.log`,
`clippy.log`, `wasm.log`. Warning amont future-compat `proc-macro-error2` conservé.
Zéro entrée ERROR dans les trois journaux graphiques, sorties 0 vérifiées.
Pas de build/lancement Studio de ces nouvelles portées revendiqué.

### Intégration Studio des portées — 9 septembre

Limite précédente levée pour High : Trunk et paquet Tauri construits puis lancés
via Computer Use. Paquet externe `r1-forest-distance-studio-20260909`, frontend
`r1-forest-distance-frontend-20260909`, sous `.gate1-evidence-runtime` sur E:.
EXE `CD9C24AE4B7A5A6BE44447CD23CA9B63B1CE4562E3509A8DD5EB84ACC7E274E7` ;
WASM `CBF87C996A34ECB2A9259C748399615AD3BCC66AA4D08646FFA109ED91C1BE92`.
Pins kit identiques au paquet terrain précédent, six sources de scène identiques
aux exports `r1-ground-chroma-studio-20260909`. Le hash de politique végétation
change volontairement : `2cb96a12bb7a788695678b12b2a5b9af354c2980c69ac3f46c79967518c1bbc4`.

High demandé/projeté, 23353 placements, 1662 arbres, kit 20/20 et 36 colliders.
Vue `valley_overview`, clair, temps fixe 0,250 s, IBL 1,0. Warmup final 87 pipelines,
zéro pending/failed, stable 8 ; pic d'erreurs transitoires 18 conservé. PNG reçu et
inspecté : forêt de rive droite présente au fond comme sur le candidat natif,
sol corrigé conservé. Export 1408×966, pas Full HD ni benchmark ; Ultra non essayé.

Fichiers copiés depuis Downloads vers le paquet, hashes vérifiés, originaux conservés :

- `forge-diagnostic-1788972911011.json` :
  `50B5516588BEFD093B1447CE41FACA168867B4087F8AEAFC9708E30FEE16887D`.
- `forge-viewport-1788972927305.png` :
  `7ADB8D847532D2D2F1284B2D388BB5B9E2D2207F2CCADEF2636A326C3B1FBE98`.

PID 28748 fermé par Alt+F4 ; absence du processus, enfants directs et WebViews
portant le chemin du candidat vérifiée. Aucun code modifié pendant ce packaging,
678 tests précédents conservés sans rejeu. Coût natif, performances cibles,
limite du terrain et acceptation artistique restent ouverts. Aucun passage de gate.

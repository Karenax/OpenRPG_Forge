# R1-048 — Courte vidéo de traversée du pont

Date : 2026-09-05. Branche `Rust`, arbre de développement modifié préservé, aucune publication.
R1-048 livré techniquement ; R1-065 et Gate 1 restent ouverts. CI distante **NOT RUN** (ADR-0006).

## Implémenté

Le runtime natif accepte `--bridge-crossing-probe --bridge-frames <dossier neuf>`.
Il réutilise le vrai contrôleur Forge, les commandes typées et les vérifications Rapier du
probe existant. Seul le mode vidéo oriente la caméra vers le pont et adapte l'axe de commande
à cette orientation ; le parcours n'est ni une animation de capsule ni une caméra téléportée.

Le temps simulé avance de 1/30 seconde par image rendue. Après 120 images de préchauffage
et la disponibilité physique du kit, les captures Bevy sont numérotées consécutivement.
La sortie attend tous les callbacks ; les erreurs de conversion/écriture Bevy font échouer
le verdict final via le garde ERROR indépendant. Maximum : 600 images, watchdog réel 180 s.
Le probe sans capture conserve son horloge réelle et son watchdog de 30 s.
Un dossier existant est refusé ; aucun PNG précédent n'est remplacé.

`scripts/Capture-BridgeVideo.py` réutilise les helpers de hash/manifest du collecteur de vues,
exige les quatre entrées et FFmpeg explicites, puis vérifie sorties, événements, terrain,
kit physique, PNG 1920 × 1080 et continuité de la séquence. FFmpeg 8.1.1 installé localement
encode en H.264/yuv420p ; FFprobe décode et compte toutes les images du MP4. Ce binaire de
développement n'est pas ajouté au runtime ni distribué. Les manifests incluent commandes,
hashes de chaque image et entrées, métadonnées vidéo et verdict ; un échec reste `incomplete`.

## Réellement vérifié

Deux exécutions finales sur AMD Radeon RX 7800 XT :

| Observation | Clair | Pluie |
| --- | ---: | ---: |
| Images PNG et images MP4 décodées | 271 | 271 |
| Dimensions | 1920 × 1080 | 1920 × 1080 |
| Durée / cadence imposée | 9,033333 s / 30 Hz | 9,033333 s / 30 Hz |
| Distance parcourue | 42,527519 m | 42,527519 m |
| Observations de traversée au sol | 518 / 518 | 518 / 518 |
| Contacts avec le collider du tablier | 434 | 434 |
| Instances du kit / colliders enregistrés | 20 / 36 | 20 / 36 |
| Bindings terrain, joueur et pont préservés | oui | oui |
| Code de sortie / ERROR observée | 0 / aucune | 0 / aucune |

Sorties externes conservées, chacune avec `frames/`, `bridge.mp4`, `runtime.jsonl`, `manifest.json` :

- `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bridge-video-final-20260905-clear`
- `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bridge-video-final-20260905-rain`

SHA-256 runtime : `8e8bac5ee7cc27e634b48c44d84f83cd373c292b4b39325629d400e813826f28`.
WorldSketch révision 3, octets inchangés :
`5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1`.
Terrain canonique : `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.

| Sortie | SHA-256 |
| --- | --- |
| MP4 clair | `07cfb57be7949b9c55d7538fa6142b54c536a532a91ac563e5c8d025da5f5d0b` |
| MP4 pluie | `834cfac5384bb70241ebd3acc6036e19c7a16f4b0908972279a4023bf3ae5546` |
| Manifest clair | `4565ce8e22efe2e9ab1e494bfa4ac4e1f8a9095aa7b9c8c74c7f1addd841ec3c` |
| Manifest pluie | `958d824b426db0b3aa062f64777442f029ffc1c12ffa78fcfa07757fc81541af` |

Commandes : reprendre `runtime_command` et `encode_command` des manifests avec un nouveau
dossier, ou lancer le collecteur avec `--runtime`, `--world-sketch`, `--kit-build`, `--preview`,
`--ffmpeg`, `--weather clear|rain`, `--output`. Les chemins et hashes exacts de ces entrées
sont consignés dans chaque manifest. Les quatre sources et FFmpeg sont rehashés après collecte.

Tests ciblés : 53 tests runtime passent (52 unitaires + 1 intégration), Clippy runtime
toutes cibles avec warnings refusés passe, `cargo fmt --all -- --check` passe.
Les nouveaux tests couvrent options incompatibles, dossier existant refusé, disponibilité du kit,
attente des callbacks et bornes. `python scripts/Test-BridgeVideo.py` passe : un cas synthétique
valide et dix corruptions refusées. Ces fixtures de headers ne remplacent pas le décodage réel
des deux vidéos. Les 324 tests workspace antérieurs ne sont pas revendiqués comme rejoués ici.

Régression native sans `--bridge-frames` rejouée ensuite, sans kit : fin à
20:32:33.980882 UTC, code 0, aucune ERROR retournée ; 42,528206 m, 518/518 ticks au sol,
435 contacts tablier et bindings préservés. Cette exécution garde le cadrage et l'horloge
historiques ; les deux modes diffèrent par l'horloge rendue et l'orientation de commande,
et aucune identité flottante bit à bit entre eux n'est revendiquée.

## Inspection et limites

Inspection d'images de début/milieu/fin de la première collecte, puis du début clair et du
milieu/fin pluie des sorties finales. Traversée cadrée, capsule visible, kit présent, pluie et
surface mouillée visibles. Le bandeau final distingue explicitement la capture hors temps réel.
L'overlay « 30 FPS / 33,33 ms » reflète l'horloge imposée, **pas le débit réel du GPU**.

Les arbres procéduraux coniques, le mélange de silhouettes du kit, les raccords du sol et
les stries de l'eau restent prototypaux. Ce lot n'a pas refait les matériaux ni la composition.
L'inspection échantillonnée ne qualifie pas scintillement, ghosting TAA, continuité perceptuelle
complète, trajectoire manuelle, navigateur, navigation ou collisions de chaque asset du kit.
Une première sortie sans bandeau reste conservée sous `r1-bridge-video-20260905-clear` ;
seules les deux sorties `final` ci-dessus sont les livrables retenus.

Les douze vues normalisées et l'acceptation artistique humaine explicite restent requises.
Aucun passage Gate 1, début Gate 2, modification de source authorée ou asset Cadle.

## Rejeu avec terrain morphé et contraintes de berges — 2026-09-06

Le collecteur accepte désormais `--quality` et `--forest-catalog`, ajoute les options
runtime correspondantes et épingle le catalogue parmi les entrées rehashées après collecte.
Le manifeste distingue ces demandes de la vérification de leur projection complète :
`quality_and_forest_projection_verified: false`. Il ne dispose pas de la télémétrie
complète des LOD pendant chaque image. Le test synthétique du collecteur passe toujours.

Build runtime courant PASS (47,40 s), puis parcours clair High avec le kit/forêt candidat
`r1-foliage-coherent-runtime-20260906-b`. Sortie 0, aucune ERROR, contrôles du collecteur
PASS : 271 PNG et images MP4 décodées, 1920×1080, 9,033333 s à 30 Hz imposés. Traversée
42,527519 m, 518/518 ticks au sol, 434 contacts tablier, bindings terrain/joueur/pont préservés.
Les entrées sont inchangées après collecte ; le hash terrain canonique reste celui ci-dessus.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-movement-20260906-clear`.
MP4 SHA-256 : `59e772ad53fb79eceb89d8bc494e5d178da1b57aac0548059da47fe13700ee4b`.
Les hashes runtime/catalogue/kit et de chaque PNG sont consignés dans le manifeste.

Images 0, 135 et 270 inspectées : vraie traversée cadrée, forêt affichée (636 arbres /
7632 primitives selon overlay), départ et arrivée au sol. Un trait sombre proche au sol
au départ et les stries/bruit de l'eau restent visibles. L'inspection échantillonnée ne
prouve pas l'absence de popping ou de scintillement sur toute la séquence, ni le passage
de chaque seuil LOD. Pas de nouvelle qualification pluie/Web, performance ou art humaine.
Le processus runtime et l'encodage sont terminés ; Gate 1 reste ouverte, CI NOT RUN.

### Vérification rétrospective des projections enregistrées

Le collecteur vérifie maintenant les événements `terrain_lod_projected` lorsque le profil
est demandé : présence, terrain épinglé, tier demandé et allocation non vide ; un événement
contradictoire fait échouer la collecte. Pour la forêt demandée, il exige un événement de
projection gouvernée avec arbres/primitives non nuls et comptages procéduraux explicitement
non valides. Ces observations sont séparées dans `render_projection_observations` ; le champ
de qualification complète reste faux. Pas de preuve GPU image par image ni du catalogue
complet par la seule présence d'arbres.

Rejeu du vérificateur sur la vidéo claire existante, sans runtime ni réencodage : PASS,
271 hashes PNG identiques au manifeste, journal et MP4 inchangés, preview rehashé conforme.
Terrain High observé : 64 chunks, 192 meshes, 149 789 sommets, 274 416 triangles ; forêt :
636 arbres, 7632 primitives. Manifeste historique non réécrit, SHA-256
`81e1d7b8ee81e16ac64d66d45e8fb2738b6db8b774d14d492797c45c63281ef0`.
Cette vidéo précède la modification des normales d'eau par fragment ; aucune requalification
du shader plus récent n'est déduite de ce rejeu.

`Test-BridgeVideo.py` : 2 tests PASS (fixtures valides, 10 corruptions historiques et
9 cas de projection absente/incohérente refusés). Diff check PASS. Pas de build Rust requis
pour ce changement de collecteur Python ; les tests workspace précédents ne sont pas rejoués.

## Vidéo du candidat regroupé rev.4 — 7 septembre

Le collecteur accepte désormais `--forest-mode individual|batches` (défaut individuel
inchangé). `batches` sans catalogue est refusé avant création de sortie ; la commande
utilise `--prototype-forest-batches`. Le validateur exige un événement de création
de lots non vides ; un simple événement de réutilisation ne suffit pas. Le manifeste
enregistre le mode demandé et l'observation CPU séparément de la qualification.

`python scripts/Test-BridgeVideo.py` : **3 tests PASS**, dont les deux anciens tests,
les choix de commande et les refus absent/vide/réutilisation seule/contradiction.
Pas de code Rust modifié dans ce lot ; le runtime a été reconstruit depuis le workspace
courant, avec les derniers correctifs de cache (build offline/locked, sortie 0).
Le total workspace précédent reste 611 PASS/1 ignoré, non rejoué pour le collecteur.

Collecte réelle :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-batched-bridge-video-20260907/`.
Commande exacte et pins dans `manifest.json` : WorldSketch de caméras
`r1-camera-set-20260906-c`, kit/catalogue rev.4 de `r1-conifer-sprays-20260907/runtime`,
High, clair, mode batches. Aucun aperçu nuageux opt-in demandé dans cette vidéo.

- Runtime sortie 0, aucune ERROR, sources épinglées inchangées pendant la collecte.
- 271 PNG et 271 images MP4 décodées, 1920×1080, durée 9,033333 s à 30 Hz imposés.
- 650 arbres/7800 primitives de catalogue, **508 lots lointains créés** observés.
- Terrain High projeté, kit 20/20 et 36 colliders prêts.
- Trajet 42,527519 m ; 518/518 ticks au sol, 434 contacts tablier ; bindings
  terrain/joueur/pont préservés. Pas de qualification du fournisseur de navigation.

PNG 0, 135 et 270 ouverts et inspectés : trajet cadré, pont/forêt/terrain visibles,
départ et arrivée au sol. Grandes formes foliaires encore artificielles, démarcations
du sol et stries d'eau visibles. Inspection échantillonnée seulement : pas de revue
image par image, de preuve de passage de chaque seuil LOD, d'isolation du vent lointain,
de stabilité temporelle globale, de performance ou d'acceptation humaine. Le texte
« 30 FPS » est l'horloge imposée de capture, **pas la cadence réelle du produit**.
Cette vidéo ne ferme donc pas à elle seule les critères de transitions de forêt.

SHA-256 :

- runtime : `2c980998679866c33f5295a0f496df4a38eb20bc61edaa8cec9f70ae5f893355` ;
- MP4 : `e33926584a6475d95bd53655ff0bae1ad48e47d450770b0a186e16536e994fed` ;
- journal : `5f8e46f746ab49384eddec204ad8032a509726f7f008bb38c1778324ee685916` ;
- manifeste : `cecbaa6cdb73d3e4ddc6971eebd6b7f7abfe6b23c00c457995e317ff232c2ef2`.

Runtime et encodage terminés. Aucun nouveau shader/asset, aucune publication Git,
aucune gate promue ; Web/Tauri et cible GPU restent hors de cette preuve native.

## Trace de distances LOD — 7 septembre 2026

Le recorder natif journalise désormais la caméra CPU à chaque numéro de demande
PNG et, au départ, les racines/IDs des arbres issus du scatter courant et les
bandes du profil. Un seul échantillon par demande, pas de journalisation ajoutée
au runtime ordinaire. Pas de modification des LOD, assets, shaders ou parcours.
Ces données ne prouvent pas l'appariement atomique GPU ni la visibilité écran.

Premier test lancé sans feature qualification : **0 test exécuté**, non retenu.
Rejeu `cargo clippy -p forge_engine --features qualification --all-targets --locked
--offline -- -D warnings` PASS ; tests recorder avec cette feature : **3 PASS**.
Build runtime, fmt et diff PASS. Pas de nouvelle suite workspace ou preuve Web.

Collecte réelle sous `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bridge-lod-trace-20260907/` :
271 PNG Full HD, MP4 9,033 s hors temps réel, traversée et arrêt contrôlés PASS.
Image 0135 inspectée, forêt/pont visibles ; pas d'inspection de chaque transition.
`scripts/Analyze-BridgeLodTrace.ps1` vérifie hash journal, unicité du relevé des
racines, séquence caméra complète, coordonnées finies et IDs uniques, puis calcule
les distances caméra/racine. Il refuse la vidéo précédente dépourvue de trace
(`Trace coverage mismatch`, contrôle négatif exécuté PASS).

Résultat `lod-distance-audit.json` : 271 échantillons, 650 racines ; 20 arbres
ont des distances observées de part et d'autre de la bande 64–72 m, 40 de part et
d'autre de 172–180 m. Ce sont les bandes calculées par le profil, pas les valeurs
nominales 24 m/72 m supposées équivalentes. Une racine derrière la caméra compte
aussi : aucune qualification de visibilité, fondu GPU, silhouette ou vent.
La prochaine analyse doit identifier les arbres visibles parmi ces IDs, sans
refaire une vidéo identique non instrumentée ni cocher la continuité LOD.

SHA-256 :

- runtime : `9d20c62409ba891c4d3f7c3ce801a89e7e0ce03b0166cab72749e4357353b6ec` ;
- vidéo : `584d322c5730ac04fb16c983090a32bd3e5e9223a79f49103d56d0f6d0bb088c` ;
- journal : `9a0e81308db207fb4a0b7df384436c1bc29a962d05011d058fa10a390df95010` ;
- manifeste : `7859a3c3825725b03faeb800a706e7d58711df8bf92625a70168d79c6167cddb` ;
- audit : `2bfb92a2a6eb014eefadb65b1fe23981095b080a8385a6fe4c633101d40be660`.

### Localisation écran des racines

Le recorder ajoute la matrice `clip_from_world` de la caméra active (ordre colonnes)
à chaque demande PNG. L'analyse optionnelle `-RequireScreenProjection` refuse une
matrice absente/non finie et calcule la projection des racines pendant la bande.
Les échantillons début/milieu/fin sont fournis avec coordonnées normalisées dans
l'image ; le nombre total d'échantillons dans le volume de vue est conservé.
Cette projection de point ne teste ni occlusion, ni couronne, ni visibilité GPU.

Build/Clippy qualification/fmt PASS ; nouvelle collecte native terminée, 271 images
Full HD, sources épinglées inchangées. Analyse réelle PASS et ancienne trace sans
matrice refusée (`Camera projection missing`). Pas de nouveau total de tests Rust.
Répertoire `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bridge-lod-screen-20260907/`.

Parmi les 20/40 arbres couvrant les bandes proche/lointaine, **9/25** ont au moins
un échantillon de racine dans le volume de vue pendant la bande. Cela ne signifie
pas qu'ils restent tous visibles pendant sa totalité. Six images inspectées :

- arbre proche `6575f5cb-ffba-5887-88bf-dcb307f9e5b5`, frames 37/61/85,
  distances 71,965/68,025/64,092 m ; racine vers x=58,4–59,4 %, y=34,5–34,8 % ;
- arbre lointain `c127193b-01db-5cde-88fe-12c24f654691`, frames 49/76/102,
  distances 179,942/175,935/172,100 m ; racine vers x=75,1–76,6 %, y=31,2 %.

Des silhouettes restent présentes aux emplacements ciblés. Le candidat lointain
est petit et voisin d'autres silhouettes : l'inspection ne suffit pas à établir
une continuité fine ni l'absence de saut entre les images non consultées. Les
pixels comprennent vent et mouvement caméra. Aucun seuil artistique abaissé,
aucune qualification forêt/vent/performance ni gate promue.

Pins SHA-256 : runtime `a40938c3142bb1ce822b4386112c28aa8f7fcfe1f073682fcad95fce4b44a680`,
vidéo `c467539fdf60f97687a9150f1bc3cac1a107c28cf5ba8e51cbdf821898e2490d`,
journal `13031941041192414ee446953cf7eaaf6966ba1bca12f770434c3e95fe5e6219`,
manifeste `ccb72fa58e8f33bc00c1c5376ac8be7727a4cfefeeb8dce25253a079f54ca647`,
`lod-screen-audit.json` `6fbed7f2f2b11f14c488326c3a40a92892a0775858215714ab8acdc508d3d946`.

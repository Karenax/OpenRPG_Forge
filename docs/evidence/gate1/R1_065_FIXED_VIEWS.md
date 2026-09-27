# R1-065 — Première collecte normalisée, défaut visuel révélé

## Actualisation Studio — 9 septembre 2026, vue globale non acceptée

Paquet `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-overview-current-studio-20260909` :
mêmes pins frontend/kit que `r1-profile-reapply-studio-20260909`, seule la caméra
`valley_overview` est sélectionnée. WASM SHA-256
`7E0D9A2B6A68F2D9FA58DE1911E0EBC7F456B5A1408BA69D8EC29F1C3068D57F`.
Caméra canonique [0,190,-250] vers [0,10,30], FOV 54°, High/clair.
Après un pas fixe : 0,250 s, kit 20/20, 36 colliders, 1662 arbres.
Export réel 1408×966 (pas Full HD), PNG inspecté : limite du terrain visible,
sol fortement tacheté et végétation lointaine peu lisible. La cause de cette
dernière observation n'est pas isolée ; aucune absence d'arbres authorés déduite.

- `forge-diagnostic-1788970350758.json` :
  `CFB719518F1907F26868EFE594CBF92975BA862B478EBB8B5C4FB1B9C8534218`.
- `forge-viewport-1788970371296.png` :
  `D160B74EB07D14AC6AD1CC7855CE35C88AE0E3FD713BC0F206BF08F52B433A24`.

Les deux fichiers sont conservés dans ce dossier et leurs hashes revérifiés.
Warmup final : 87 pipelines, zéro pending/failed ; cela n'efface pas les erreurs
transitoires de préparation. Instance PID 30788 fermée après export.
**R1-047, R1-065 et Gate 1 restent ouverts.** Cette actualisation ne remplace
ni la série finale cohérente ni l'acceptation humaine ni la performance cible.
Ne pas répéter cette capture inchangée : poursuivre les corrections visuelles.

2026-09-05, Rust local non publié. **R1-065 et Gate 1 restent ouverts.**

## Implémenté et exécuté

`scripts/Capture-WorldSketch.py` collecte les quatre caméras existantes du WorldSketch en clair
et sous pluie. Il ne modifie ni WorldSketch, ni placements, ni sources Blender. Chaque aperçu
dérivé change seulement `camera_id`. Le pont automatique reste actif. Le dossier de sortie doit
être neuf ; aucune preuve existante n'est écrasée. Les entrées sont hashées avant/après.

Exécution depuis la racine Rust :

```powershell
python scripts/Capture-WorldSketch.py `
  --runtime C:/Users/karen/AppData/Local/Temp/openrpg-forge-r1-064-target/debug/forge_runtime.exe `
  --world-sketch examples/visual_valley/world-sketch.v1.json `
  --kit-build E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json `
  --preview examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json `
  --output E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-065-fixed-views-20260905
```

Huit lancements terminés code 0, huit PNG **1920 × 1080**, huit journaux JSONL et métriques.
Capture frame 240, warmup 120, sortie frame 360, VSync demandé, RX 7800 XT/Vulkan.
Le manifeste externe contient commandes complètes, caméras, hashes des sources et sorties,
GPU et métriques. Son SHA-256 octets est
`866f90e68322142ebd40ea62b408ccdc93074eaf0eef9b25f70bc5ee130df28b`.
Runtime : `a7ecca1cc67683bf5d1ab236244cdabc9a3a56bde77ccea91650dd08eca7d854`.

Le contrôle initial vérifie arrêt réussi, absence d'ERROR, chargement du kit et projection du
pont, PNG Full HD, terrain et météo. Le vérificateur a ensuite été renforcé pour vérifier aussi
la caméra réellement déclarée dans les métriques, les pins du kit, 20/20 instances et les colliders
prêts/configurés (36/36 ici). **Les huit sorties ont été revalidées avec ces contrôles renforcés**,
sans relancer les captures ni modifier leur manifeste.

`python scripts/Test-WorldSketchCapture.py <dossier-sortie>` : deux tests PASS, couvrant les huit
captures réelles et sept corruptions refusées : ERROR, caméra, terrain, météo, colliders,
dimensions PNG, absence d'arrêt réussi. `git diff --check` PASS. Aucun code Rust n'a changé
dans ce lot ; ses suites ne sont pas présentées comme rejouées.

## Inspection des huit images

| Caméra | Clair | Pluie | Observation et limite |
| --- | --- | --- | --- |
| hero_reference_match | inspectée | inspectée | Pont intégré, rivière dégagée ; berges très régulières, bandes visibles sur l'eau, terrain peu détaillé |
| human_bridge | inspectée | inspectée | Pont visible ; groupes de végétation apparemment suspendus dans le ciel, défaut à diagnostiquer en priorité |
| keep_landmark | inspectée | inspectée | Arche et murs chargés, mais pas une forteresse achevée ; terrain nu et végétation prototypale |
| valley_overview | inspectée | inspectée | Vue globale lisible mais clairsemée, limite carrée du terrain visible, rivière très régulière |

Les huit PNG originaux ont été ouverts et inspectés dans la conversation. Ils restent dans le
cache externe, pas publiés ni ajoutés au dépôt. Le HUD présente encore des caractères incorrects.
Le nom `human_height` de la caméra n'est pas une preuve de sa hauteur réelle au-dessus du terrain
sculpté ; celle-ci doit être mesurée. La cause des éléments suspendus n'est pas encore établie.
Les métriques courtes vont de 109,70 à 121,19 FPS moyens et de 8,88 à 9,90 ms p95 :
ce sont des smokes associés aux captures, **pas un benchmark qualifiant sur GPU milieu de gamme**.

## Verdict et prochaine correction

Collecte technique réussie, **qualification visuelle non obtenue**. Huit captures ne sont ni douze
vues distinctes, ni un parcours vidéo, ni une acceptation humaine. La priorité suivante est de
diagnostiquer les éléments suspendus dans `human_bridge`, corriger leur projection si le défaut
est confirmé, puis rejouer cette vue ciblée. Les vues complémentaires et la vidéo restent prévues.
La conservation des verrous et retouches, la qualification Cadle et les transitions de gate ne
sont pas redéfinies par ce lot. Aucun asset Cadle, commit ou push.

Suite : `R1_065_HUMAN_CAMERA_HEIGHT.md` établit que la caméra était sous le terrain,
et non que les racines d'herbe flottaient. Ces huit captures restent la preuve historique
révision 2 ; elles ne sont pas écrasées par les deux vues corrigées révision 3.

## Recollecte cohérente du 6 septembre 2026

Huit nouvelles captures natives 1920×1080, quatre caméras canoniques × clair/pluie,
ont été produites et toutes ouvertes pour inspection. Aucune modification de scène.
Dossier externe : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-current-fixed-views-20260906`.
Manifeste SHA-256 : `95b251adde505e96f33b4040971c57410760458b7ac5ed622f5e1554b506dcd1`.
Binaire SHA-256 : `5328bd6b4a4b886224f14f01c89cc2ad31b9c02e9bcf9ec21b8de917a281a06f`.

Le script de collecte accepte désormais `--quality high --forest-catalog <catalogue>`.
Entrées : WorldSketch canonique révision 3, kit/preview/catalogue du dossier externe
`r1-foliage-coherent-runtime-20260906-b`. Le manifeste conserve chemins, hashes, commandes,
caméras et hashes de chaque PNG/journal/métrique/preview. Les entrées principales sont
contrôlées inchangées en fin de série ; les assets candidats sont liés à l'audit de provenance
`R1_053_PROVENANCE_CHAIN.md`, sans nouvelle promotion.

Vérifié : huit sorties 0 sans événement ERROR, arrêt réussi, dimensions PNG, caméra et
hashes documentaires concordants, kit chargé/colliders prêts, budgets disponibles High
projetés et forêt candidate présente. Les métriques indiquent 636 arbres/7 632 primitives
LOD ; les comptages géométriques procéduraux ne sont pas qualifiés pour cette projection.
High reste partiel, ce contrôle ne prétend pas `full_preset_applied=true`.
Trois tests Python PASS : huit preuves réelles et douze corruptions refusées, dont cinq
nouveaux cas qualité/forêt. Aucun code Rust modifié ni suite Rust rejouée dans ce lot.

Inspection actuelle : caméra basse correctement au-dessus du terrain, pont et forêt
chargés ; trames ponctuées visibles à distance, sol encore peu naturel, rivière régulière
avec bandes, reflets de terrain très forts sous pluie, architecture toujours prototypale.
La vue globale révèle aussi les limites du terrain. Ces réserves ne sont pas des erreurs
de chargement et restent ouvertes malgré le succès des contrôles automatiques.

**Huit vues actuelles, pas douze.** Le WorldSketch contient quatre caméras seulement ;
les quatre vues complémentaires restent à définir et produire. Aucune ancienne correction
n'est additionnée à cette série. Pas de benchmark qualifiant, de validation artistique humaine,
de parité Tauri ni de passage de gate. CI NOT RUN ; aucun push ou asset Cadle.

## Instantané de caméras et premier cadrage complémentaire — 6 septembre

`scripts/Prepare-CaptureCameraSet.py` prépare une copie externe du WorldSketch avec
deux caméras supplémentaires proposées dans
`examples/visual_valley/references/gate1-additional-cameras.json`. Ce sont des cadrages
de preuve provisoires, pas une nouvelle recette canonique ni une retouche du module.
Le CLI épinglé compile la source et sa copie : terrain, terrain sculpté, végétation,
masques, carving, audit de traversée et placements du pont sont comparés intégralement.
Seuls les pins de filiation WorldSketch et audit du pont peuvent différer, après contrôle
explicite contre leurs rapports de compilation. Aucun asset copié ou réexporté.
Le preview dérivé change seulement son pin WorldSketch ; placements et manifeste du kit
restent identiques. Les entrées sont contrôlées inchangées avant/après.

Première exécution conservée en échec dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-camera-set-20260906` : le contrôle initial
ignorait la propagation du hash de l'audit vers le document du pont. Diff inspecté :
seuls les deux pins changent, aucun placement. Le contrôle corrigé vérifie ces pins
au lieu de supprimer indistinctement les hashes.

Préparation réussie dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-camera-set-20260906-b`.
`camera-set-evidence.json` SHA-256 :
`3daff70195f8b361b4c3572dc544bee89c9deafd0aa635422b18de5fb76ea97a`.
WorldSketch dérivé canonique :
`6b0f32cf6520014656e763255a023a5c59f2f944def64450d8b9e3a6deab855e`.
Six caméras préparées, **pas douze captures qualifiées**.

Tests exécutés : `Test-CaptureCameraSet.py <dossier-préparation>` : quatre PASS,
dont compilations réelles et corruptions géométrie/filiation refusées.
`Test-WorldSketchCapture.py` sur `r1-current-fixed-views-20260906` : quatre PASS.
La couverture est maintenant dérivée des caméras de la source épinglée, pas du nombre
codé en dur huit : chaque pose doit figurer exactement une fois par météo.
Cinq nouveaux cas refusés : absence, doublon, renommage, pose différente, météo différente.
Ces contrôles s'appliquent aussi aux futures recettes ; aucune suite Rust rejouée.

Essai natif `water_detail`, High partiel, forêt candidate, capture 900/sortie 1800 :
deux lancements terminés code 0, contrôles de collecte PASS, deux PNG 1080p inspectés.
Dossier : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-camera-water-framing-20260906`.
Manifeste SHA-256 :
`3cbfceff79cd0c3133b855d204582256a4142dee7da0beae751efa6a6fbcd192`.
**Cadrage rejeté pour le détail de l'eau** : pont centré et eau réduite à une bande,
donc insuffisant pour observer les optiques. Sol et reflets pluie restent très visibles.
La caméra `forest_detail` n'a pas encore été rendue. Prochaine action : réorienter
le cadrage de preuve, sans modifier la géométrie, puis collecter une série complète
cohérente après inspection des deux nouveaux cadrages. Ne pas additionner cet essai
aux huit vues précédentes. Aucune acceptation humaine ni passage de gate.

## Série de douze produite et inspectée — 6 septembre

Le cadrage eau a été déplacé vers un tronçon sans pont : position `[-90, 10, -220]`,
cible `[-105, 1.5, -200]`, FOV vertical 40°. La caméra forêt conserve la position
`[-10, 18, 90]`, cible `[-65, 12, 115]`, FOV 38°. Seul le fichier des caméras de preuve
a changé ; le WorldSketch canonique reste inchangé, SHA octets
`5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1`.

Une nouvelle préparation, puis une **collecte complète des six poses × clair/pluie**
ont réellement été exécutées. Aucun assemblage avec les essais précédents.

- Instantané : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-camera-set-20260906-c`.
- Preuve de préparation SHA : `f0b0dce338e34a7cdc2e32cf5b739538d4e93a8ef24b566910765b259386c965`.
- WorldSketch dérivé canonique : `2c2b8c5892fadfdd44635cf9dbe8611776cbe5c63b831e5a85bd4c81999905dd`.
- Captures : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-twelve-views-20260906`.
- Manifeste SHA : `889bd5510064ca52d72a9a98c31467338e0641cee0f0e02d0119b720379ca98c`.
- Runtime SHA : `f03282cf6df610909a27b7d0ba1d04308b030e9d0c4bc580adb44b5621959442`.

Reproduction depuis la racine Rust, avec dossiers de sortie neufs :

```powershell
$fixture = 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-coherent-runtime-20260906-b'
$snapshot = 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-camera-set-20260906-c'
$captures = 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-twelve-views-20260906'
$binaries = 'E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906/debug'
python scripts/Prepare-CaptureCameraSet.py --cli "$binaries/forge_cli.exe" `
  --world-sketch examples/visual_valley/world-sketch.v1.json `
  --preview "$fixture/runtime-preview.v1.json" `
  --additional-cameras examples/visual_valley/references/gate1-additional-cameras.json `
  --source-root . --output $snapshot
python scripts/Capture-WorldSketch.py --runtime "$binaries/forge_runtime.exe" `
  --world-sketch "$snapshot/world-sketch.v1.json" --preview "$snapshot/runtime-preview.v1.json" `
  --kit-build "$fixture/asset-kit-build.v1.json" --forest-catalog "$fixture/prototype-catalog.v1.json" `
  --quality high --capture-frame 900 --exit-after-frames 1800 --output $captures
python scripts/Test-CaptureCameraSet.py $snapshot
python scripts/Test-WorldSketchCapture.py $captures
```

Vérifié : douze sorties 0, douze PNG 1920×1080, zéro ERROR, arrêts réussis, kit/forêt
chargés, pins et budgets disponibles concordants. Quatre tests préparation et quatre
tests collecte PASS ; le vérificateur compare aussi les SHA des quatre artefacts de
chaque run au manifeste. Le même vérificateur passe sur les huit vues historiques du
6 septembre. Pas de changement Rust, pas de rebuild ou de suite Rust revendiquée.

Les douze images ont été ouvertes individuellement et inspectées :

| Pose, clair et pluie | Observation |
| --- | --- |
| hero_reference_match | Pont et rivière lisibles ; eau périodique, berges très régulières, bord du monde visible |
| human_bridge | Caméra au-dessus du sol ; végétation par touffes, sol encore peu naturel, reflets pluie excessifs |
| keep_landmark | Arche et murs visibles, architecture prototypale ; forts reflets sur les collines mouillées |
| valley_overview | Répartition globale lisible ; limite du terrain et motifs de matériaux visibles |
| water_detail | Eau occupant environ la moitié de l'image, reflets et rives inspectables ; vagues régulières et mince bande verte au premier plan |
| forest_detail | Troncs et couronnes à plusieurs distances visibles ; silhouettes en losange sur les pentes lointaines, origine non isolée ; aspect très brillant sous pluie |

Les deux nouveaux cadrages sont exploitables pour observer les défauts, **pas une
acceptation artistique**. Le HUD diagnostique masque une partie haute gauche des images.
Ce sont six poses différentes sous deux météos, pas douze poses indépendantes.
Les mesures associées sont des smokes RX 7800 XT/Vulkan, pas le benchmark GPU de référence :
59,38–83,92 FPS moyens, p95 12,69–17,87 ms, 1 % low 43,94–57,73 FPS.
La caméra humaine échoue même aux seuils numériques p95/1 % low sur ce relevé local ;
ne pas transformer la collecte réussie en qualification de performance.

**La collecte de douze est constituée ; R1-065 et Gate 1 restent ouverts.** High complet,
qualité artistique, comparaison Cadle, vidéo de qualification cohérente, validations
humaines et parité Tauri ne sont pas établis par cette série. La suite utile porte sur
les défauts visuels/règles de matériaux et les capacités de qualité manquantes, pas sur
la répétition de cette collecte inchangée ou les micro-audits CPU terminés.
Sources et assets intacts ; CI NOT RUN, aucun commit/push.

# R1-063 — candidat de feuillage, 2026-09-06

## Implémentation isolée

`scripts/blender/Build-TreeFoliageCandidate.py` construit un candidat original à partir du kit
existant, sans changer le générateur principal, sa recette ou les assets du runtime.
Deux fois plus de feuilles utilisent deux triangles chacune, contre six auparavant ; le quad
est légèrement vrillé. Les textures alpha/PBR, les branches et les collisions viennent du kit
original. Les positions des feuilles sont dérivées de ses extrémités de branches et de sa seed.

Le script exige une nouvelle sortie et vérifie les hashes du manifeste et du blend. Il refuse
un arbre déclaré retouché dans la source auteur : cette étude ne doit pas écraser une correction.
Cette branche de refus est implémentée mais n'a pas encore été exercée avec une fixture négative.

## Preuve exécutée

Blender **4.5.10 LTS**, code **0**. Les six géométries de feuillage sont générées deux fois dans
la même session et comparées par hash des sommets, indices, indices matériaux et UV.
Budgets décroissants, présence des UV et collisions contrôlés par le validateur existant.
Les deux GLB sont chacun exportés deux fois depuis la même scène : octets identiques.
Cela ne vaut pas qualification de reconstructions indépendantes ni réimport GLB.

| Asset / LOD | Triangles avant → candidat | Sommets avant → candidat |
|---|---:|---:|
| Chêne 0 | 48 718 → 34 158 | 53 573 → 60 853 |
| Chêne 1 | 13 314 → 9 534 | 14 283 → 16 173 |
| Chêne 2 | 1 546 → 1 186 | 1 513 → 1 693 |
| Pin 0 | 62 944 → 46 908 | 63 770 → 71 788 |
| Pin 1 | 13 866 → 10 702 | 13 385 → 14 967 |
| Pin 2 | 3 142 → 2 592 | 2 733 → 3 008 |

Les sommets augmentent et les surfaces alpha se recouvrent davantage : **aucun gain de performance
GPU n'est prouvé**. L'exporteur prévient que plusieurs nœuds image partagent une texture et que
le premier sampler sera utilisé ; la parité des matériaux après réimport reste à vérifier.

Quatre rendus FHD sont produits, angles 0°/90° pour chaque arbre. Les vues 0° de la première
exécution et les vues 90° du rejeu final ont été inspectées : feuillage plus fourni, mais grosses
branches, cimes coupées dans la géométrie du tronc et structure trop régulière restent visibles.
Les LOD1/2 ont des contrôles géométriques, pas une acceptation visuelle.

Les hashes du manifeste, du rapport source, du blend et des six GLB sources restent inchangés.
L'essai initial a échoué avant création de sortie sur une mauvaise clé de lecture du manifeste ;
la lecture utilise maintenant `generator.input_manifest_sha256`. Le rejeu final inclut les gardes
de source auteur et les doubles exports.

Commande depuis la racine Rust :

```powershell
& 'C:/Program Files/Blender Foundation/Blender 4.5/blender.exe' --background --disable-autoexec --python-exit-code 1 --python scripts/blender/Build-TreeFoliageCandidate.py -- --base-build 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1' --manifest examples/visual_valley/asset-kits/r1-063/asset-kit.v1.json --output-dir 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-candidate-20260906-b'
```

Rapport externe `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-candidate-20260906-b/candidate-proof.json`,
SHA-256 `6cd9531f028ef246a791812209e58f5ed0c09010a56741c72d80b0d6ddef5af7`.
Il contient les hashes du script, du worker, des sources et des GLB.

## Verdict

**Candidat DCC plus dense, non promu**. Il ne change pas encore la forêt du Studio et ne clôt
ni R1-063 ni Gate 1. Avant intégration : corriger/qualifier la silhouette et les LOD, contrôler
le réimport et mesurer le coût réel. Les retouches existantes, les validations humaines et les
contraintes de gate restent préservées. Pas de dépendance ajoutée, d'asset Cadle ou de publication.
Les tests Rust ne sont pas rejoués pour cette étude Python isolée ; `git diff --check` passe.

## Complément : réimport et protection des retouches

`Test-TreeFoliageCandidate.py` a été exécuté avec Blender 4.5.10, code **0**, sans régénérer
les images ni modifier les sources. Le validateur existant `validate_glb_import` est réutilisé :
les deux GLB retrouvent exactement les noms de meshes attendus, les comptes triangulaires des
trois LOD, les UV et les objets de collision ; chaque matériau de rendu expose au moins trois
nœuds d'images PBR. Cela vérifie le contrat importé, pas le comportement Rapier.

La comparaison des matériaux GLB résout leurs indices de textures vers les hashes des images
embarquées et les samplers. Les signatures normalisées sont identiques aux GLB sources,
y compris facteurs PBR et paramètres alpha. L'avertissement de sampler de l'exporteur n'a donc
pas introduit de différence par rapport aux GLB du kit initial. La parité visuelle entre le
matériau Blender et Bevy n'est pas démontrée par cette comparaison.

Le garde de source est maintenant isolé dans `validate_candidate_source`, toujours appelé avant
création de sortie ou ouverture du blend. Il lève des erreurs explicites plutôt que des assertions
pour les conflits. Test positif : le kit actuel avec rocher retouché est accepté. **Quatre cas
négatifs passent** : chêne déclaré retouché, pin déclaré retouché, hash de manifeste changé,
hash de blend changé. Ces cas utilisent des copies en mémoire du rapport, jamais les originaux.
Ils remplacent la limitation « branche non exercée » indiquée dans le premier bilan ci-dessus.

Sortie : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-reimport-20260906/reimport-proof.json`.
Le rapport lie le hash du candidat précédent, du script de test et du garde courant ; la génération
géométrique n'a pas changé. Commande :

```powershell
& 'C:/Program Files/Blender Foundation/Blender 4.5/blender.exe' --background --disable-autoexec --python-exit-code 1 --python scripts/blender/Test-TreeFoliageCandidate.py -- --candidate-dir 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-candidate-20260906-b' --base-build 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1' --manifest examples/visual_valley/asset-kits/r1-063/asset-kit.v1.json --output-dir 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-reimport-20260906'
```

Le prochain contrôle pertinent est le rendu du candidat dans le runtime, avec mesure du coût et
inspection des LOD. Le réimport ne constitue ni promotion, ni validation artistique, ni passage
de gate. Aucun nouveau test Rust ou benchmark n'est revendiqué.

## Complément : observation native du candidat

`scripts/Prepare-FoliageRuntimeCandidate.py` assemble une fixture externe après vérification
des hashes du candidat et de la preuve de réimport. Les deux arbres candidats remplacent
uniquement leurs homologues dans cette fixture ; les quatre autres GLB restent byte-identiques,
y compris le rocher corrigé. Les 20 placements ne changent pas. L'identité distincte
`visual-valley-r1-063-foliage-candidate`, révision 1, évite de présenter cette variante comme
le kit initial. Le rapport minimal lie les preuves amont et ne revendique pas un rebuild DCC complet.
Le kit canonique, ses fichiers et le Studio restent inchangés.

Build natif courant `cargo build -p forge_runtime --locked` : **PASS**, 46,85 s, avec le target
externe usuel, `CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`. Exécutable SHA-256 :
`49ae3bf213b56cb6bb9fce1b77eec72c0b9ba0ab3e9fc6a8d58a5efb140ae23f`.

Run code **0**, RX 7800 XT / Vulkan / 1920×1080, caméra `hero_reference_match` :

- 20/20 scènes chargées, zéro échec ;
- 28/28/28 nœuds LOD configurés ; 36 colliders Rapier prêts, zéro échec ;
- 94/94 pipelines prêts, zéro pending/failed ; capture émise frame 300, callback frame 303 ;
- capture inspectée : arbres candidats présents, feuillage visible et ombres présentes ;
  grand écart de taille et de détail avec le scatter procédural, silhouette encore artificielle ;
- 240 échantillons après warmup 180 : moyenne **9,717 ms**, p95 **10,329 ms**, 1 % low
  **45,52 FPS**. Observation courte sans baseline jumelée, **pas un benchmark ni un gain prouvé**.

Les warnings Vulkan de registre/couche AMD et shader `@invariant` restent présents dans le log.
La configuration de trois LOD n'est pas une inspection de leurs transitions en mouvement.
Le runtime s'est fermé à l'issue des 420 frames ; aucun processus de ce run ne reste actif.

Dossier externe `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-runtime-20260906/` :

- `hero.png` SHA-256 `edeeaa8cf4b3e28fa74fab39133d8e336b35e870ae14e2ee5ef33c44a84f2aed` ;
- `metrics.json` SHA-256 `ad89d73957dbbb3929c6158fd5750579d745384dad9f4676483584388f75e943` ;
- `runtime.log`, `asset-kit-build.v1.json`, `runtime-preview.v1.json`, six GLB.

La commande runtime utilise `--world-sketch-visual-valley`, les deux JSON de cette fixture,
`--capture <hero.png> --capture-frame 300 --metrics <metrics.json> --metrics-warmup-frames 180
--exit-after-frames 420`. C'est une intégration technique native du candidat, pas sa promotion,
une validation WebGPU ou un passage de Gate 1. La cohérence de la forêt reste à traiter.

## Complément : Studio WebGPU, même fixture

Le candidat a ensuite été servi sur `http://127.0.0.1:14648/` dans une copie externe du build
Studio précédemment qualifié pour l'affichage des métriques. Aucun code Rust ne change dans ces
lots DCC ; le WASM réutilisé est byte-identique : SHA-256
`cd8c75763e117c07fc33a7bbff2ff4a8aaa59be69c794659f279b5a1439aba76`.
Les quatre fichiers Web et les six GLB copiés ont été comparés par SHA-256.
Le transport et les contrôles de provenance/hashes du produit sont utilisés sans modification.

Chrome, onglet 201243223, URL `/?forge_scene=world-sketch-visual-valley&forge_kit=governed` :

- statut du kit **20/20 instances, 36 colliders, prêt** ;
- monde initialisé **une fois**, hashes de scène alignés et caméra 1/1 ;
- profil High observé, eau/environnement toujours partiels ;
- capture navigateur inspectée : viewport **1408×741**, arbres du kit présents au milieu
  du scatter procédural, capsule joueur et herbe visibles ;
- relevé `dev.logs` limité aux 25 warnings/erreurs : liste vide. Ce n'est pas une collecte
  exhaustive des événements GPU ni une preuve universelle d'absence d'erreur.

La capture est conservée dans la conversation, pas sous forme de PNG sur disque. La caméra joueur
Web diffère de la vue héroïque native : aucune parité pixel à pixel ou qualification Full HD
du viewport n'est revendiquée. Le bandeau « présentation GPU non observée » reste présent ;
l'observation visuelle est distincte de cette télémétrie produit non qualifiée.

L'onglet a été fermé et le serveur local arrêté après contrôle de son PID et de sa ligne de
commande. Le candidat fonctionne donc en natif et dans ce smoke Studio WebGPU ; sa promotion,
son coût en forêt dense, ses transitions LOD et sa cohérence artistique restent non qualifiés.
Gate 1 demeure ouverte, sans publication.

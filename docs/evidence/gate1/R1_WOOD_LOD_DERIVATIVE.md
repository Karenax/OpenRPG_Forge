# Gate 1 — dérivé du bois distant, 7 septembre 2026

Statut : candidat Blender exporté/réimporté, lancé en natif et dans Chrome, **non promu**.
Suite du surcoût mesuré dans `R1_FOREST_VARIANT_SELECTION.md`.

## Implémentation

`Build-TreeFoliageCandidate.py --canopy-profile coherent --wood-profile reduced`
simplifie des copies du bois LOD1/LOD2 avec Decimate COLLAPSE (0,35/0,25), sous
Blender 4.5.10 LTS. Aucun nouveau code moteur, shader ou dépendance ; génération
hors ligne seulement. Le défaut reste `source`. Recette, blend, GLB originaux et
arbre proche intacts. Les arbres déclarés authorés restent refusés.

## Mesures et contrôles exécutés

| Asset | Bois LOD1 avant/après | Bois LOD2 avant/après | Total LOD0 / LOD1 / LOD2 candidat |
|---|---:|---:|---:|
| Chêne | 1 974 / 690 | 466 / 116 | 34 158 / 8 152 / 662 |
| Pin | 4 374 / 1 530 | 1 492 / 372 | 46 908 / 7 438 / 1 216 |

Le total lointain passe de 1 012 à 662 triangles pour le chêne (-34,6 %) et de
2 336 à 1 216 pour le pin (-47,9 %). **Pas de gain FPS déduit**.

Génération : répétitions des meshes et doubles exports identiques, budgets/UV,
comptages strictement décroissants et hashes des sources PASS. Huit images
produites ; les quatre images source/réduction du LOD2 ont été inspectées.
Silhouette générale conservée sur ces angles, bois moins détaillé ; grandes
cartes de feuilles et aspect artificiel toujours visibles. Ni revue humaine,
ni transition LOD en mouvement, ni conservation de silhouette sous tous les angles.

Réimport : deux GLB PASS, noms/LOD/UV/collisions et matériaux/images/samplers
identiques à la base. Quatre refus de source PASS (arbres authorés, manifeste et
blend périmés). Contrôle supplémentaire contre les GLB du candidat cohérent du
6 septembre : géométrie, indices/material indices, UV et transformations identiques
pour le LOD0, tout le feuillage et les collisions après réimport. Ce contrôle
n'affirme pas l'identité des normales du bois simplifié, ni la qualité de ses UV.
Pas de rebuild indépendant ; répétitions dans une même scène seulement.

## Reproduction et preuves

Lancer Blender avec `--background --disable-autoexec --python-exit-code 1 --python`
et le worker, puis `--` et les arguments suivants :

- `--base-build E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1`
- `--manifest examples/visual_valley/asset-kits/r1-063/asset-kit.v1.json`
- `--canopy-profile coherent --wood-profile reduced`
- `--output-dir <nouvelle-sortie>` (sortie existante refusée).

Test : `Test-TreeFoliageCandidate.py`, même base/manifeste, `--candidate-dir` vers
la sortie, `--reference-candidate E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-coherent-20260906`
et `--output-dir <nouvelle-preuve>`.

Root local : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-wood-lods-20260907/`.

- `candidate/candidate-proof.json` : `5aa839c7ed1a5303750942c4aa9b73347a8914f98236a782227ae6878a565af3`.
- `reimport-reference/reimport-proof.json` : `2e33604535871385038f9e5ee71fa60c54577245916f8b3c9271814fae789775`.
- GLB chêne : `be9abd360ef86af4a08fc97573ba7b107998657da228926b47508cd406ba63ee`.
- GLB pin : `83143f13bd9e9eca1d461603e597efd793932f341c208c5cf3286cf6a1ba2077`.

## Assemblage et capture native

`Assemble.py` dans le root local appelle l'assembleur du dépôt. Fixture séparée
`runtime/`, même identité de kit candidat, nouvelle révision 3. Égalité du preview
précédent sauf révision vérifiée : vingt poses, caméra et sources inchangées.
Catalogue mixte v2 : seuls révision et hashes des GLB changent, pivots/hauteurs et
bindings conservés. Quatre GLB non arborés identiques, dont le rocher authoré.
Sources épinglées avant/après. Aucun déploiement Studio ni promotion.

`Run.ps1 -Mode capture` utilise le binaire immuable du lot variantes, SHA-256
`69aae8b071684e9e311ccfb5f81bb1847ce8de2a3ed2cf9ef23d1000f9305c0b`.
Ce n'est pas un nouveau build du workspace. WorldSketch et caméra human_bridge
identiques, clear, High 1920×1080, RX 7800 XT/Vulkan, sans VSync.
Sortie 0 sans ERROR/panic observé ; kit 20/20, 36 colliders prêts, 650 arbres et
7 800 primitives LOD. Capture frame 300, callback 303, 116/116 pipelines prêts,
218 observations stables. PNG inspecté : vallée et deux espèces visibles,
conifère proche sombre et couverture artificielle toujours présents.

Capture incluse dans ce smoke : 239 échantillons, 55,64 FPS moyens, p95 19,7911 ms,
1 % low 23,79 FPS ; **ce n'est pas la comparaison de performance**.
Hash terrain `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`
et végétation `1741c578f12b915c4e913b026b591efa9f68fb662c911dda604ebc1e725d057f`
conservés. Contrôle des fichiers d'entrée et des douze GLB avant/après PASS.

- `assembly-proof.json` : `f0c65749212c8a763c1e65d6447fcd8d0b251267d04a2c7a4e2a4a1d60bef9bd`.
- `capture/reduced.png` : `ec4bc887e2023d9ffcb53fb3d82636e574f85298cad4c920f495628ea8e4bb90`.
- `capture/reduced.json`, `capture/reduced.log`, `capture/pins.json` conservés.

## Comparaison native : PARTIAL_INVALID

`Run.ps1 -Mode comparison` a exécuté A1/B1/B2/A2, A = catalogue mixte bois source,
B = même catalogue mixte avec dérivés réduits. Même binaire et configuration de
scène/caméra, High demandé, sans VSync, 420 frames de chauffe puis 1 199 échantillons,
aucune capture ni compilation simultanée. Les quatre processus se sont terminés
avec code 0, sans ERROR/panic observé, fichiers épinglés inchangés.

**L'audit final a échoué sur la résolution d'A1** : initialement 1920×1080,
finalement 2560×1369, trois événements de redimensionnement. Cause non attribuée.
La paire A1/B1 est invalide, et n'est pas utilisée pour calculer un gain.
Les logs et JSON restent intacts ; l'audit conserve `PARTIAL_INVALID`.
Le lanceur contrôle désormais la résolution après chaque passage et interrompt
la séquence si elle diffère, au lieu d'attendre l'audit final.

| Run | Résolution finale | FPS moyens | p95 ms | 1 % low FPS |
|---|---|---:|---:|---:|
| A1 — exclu | 2560×1369 | 53,84 | 20,2043 | 23,47 |
| B1 — paire exclue | 1920×1080 | 55,23 | 23,6656 | 28,31 |
| B2 | 1920×1080 | 59,82 | 18,2840 | 42,19 |
| A2 | 1920×1080 | 57,29 | 19,7442 | 36,94 |

Paire B2/A2 restante : +4,42 % de FPS moyens pour le dérivé. Un seul couple,
poste non isolé, GPU de développement seulement : **signal exploratoire**, pas
qualification de performance ni preuve statistique. Tous les seuils finaux
restent non atteints. Les deux passages ont deux événements de resize, aucune
différence entre dimensions initiales/finales ; aucune trace continue de caméra
ou d'activité externe n'a été enregistrée.

Sur B2/A2 : primitives opaques GPU observées 488 311 / 871 503 ; allocations
backend 1 024 123 132 / 1 112 464 772 octets (~84,25 Mio en moins), réservations
identiques 1 451 170 148. Échantillons mémoire à 25,05/25,06 s, pas de VRAM résidente.
Comptes de triangles visibles CPU 1 173 126 / 1 541 140 : observations finales,
pas un inventaire constant de primitives réellement dessinées pendant tout le run.
Sources de qualité/placements identiques, 650 arbres, kit 20/20 et 36 colliders
prêts ; `full_preset_applied=false` et blocker végétation restent déclarés.

- `comparison/{a1,b1,b2,a2}.{json,log}`, `pins.json` et `summary.json` conservés.
- `summary.json` : `d84d1f4ec1b9be660b6a798cf4b1983a80bca46b5ccea067151651bee7810a6a`.
- `audit.json` : `bb5de2eafb75c6e9bc8e09dd93e0b367f5e8df6e4a32691bffd6d87285cb2e0b`.
- `ValidateRuns.py` décrit l'exclusion, sans assouplir la résolution requise.

## Suite et limites

### Rejeu Web du dérivé

Bundle existant réutilisé sans compilation : WASM SHA-256
`684a4d6a87a935409e39cb4106c0c5dce6407458b20cc5ac5e8bb48ef0ff0d0d`.
`PrepareWeb.py` copie les six GLB vérifiés dans une nouvelle sortie `web-b/`.
Le premier assemblage `web/` est conservé comme échec : aperçu natif refusé car
son WorldSketch de caméras (`2c2b…`) diffère du document embarqué Web (`42fe…`).
Correction de préparation seulement : ancien aperçu Web vérifié, révision 2 → 3 ;
comparaison des champs prouvant que seuls caméra/hash WorldSketch/révision diffèrent
de l'aperçu natif, jamais les poses. Aucun contrôle de source désactivé dans Forge.
Les vues candidates restent chargées par le chemin de caméra déjà prévu.

Chrome, port local 1455 : kit 20/20 et 36 colliders prêts, 650 arbres/7 800 primitives.
High → Low → High exécuté, trois JSON et deux PNG exportés et archivés par
`AuditWeb.py` dans `web-exports/`, copies vérifiées par SHA-256. Les deux PNG ont
été inspectés : scène visible, pin proche sombre et ciel provisoire inchangés.
Console error vide au relevé final. Un premier clic d'avance temporelle a expiré ;
état relu à 0 s avant retry, puis les trois exports restent à 0,250 s.

| Observation | High initial | Low | High restauré |
|---|---:|---:|---:|
| Entités mesh | 8 132 | 6 997 | 8 132 |
| Meshes uniques | 654 | 654 | 654 |
| Triangles uniques | 2 590 826 | 1 181 970 | 2 590 826 |
| Triangles instanciés, tous LOD | 34 218 818 | 32 691 850 | 34 218 818 |
| Meshes non comptables | 0 | 0 | 0 |
| Triangles visibles CPU | 1 006 278 | 657 982 | 1 224 076 |
| Dimensions | 2048×1270 | 2048×1101 | 2048×1101 |

**PARTIAL_VIEWPORT_CHANGED** : cinq compteurs de géométrie restaurés, mais le
viewport a changé de hauteur. Ne pas présenter les images comme paire optique
identique, ni revendiquer six compteurs restaurés. Les sources terrain/végétation
restent identiques. Les placements projetés passent 22 341 → 11 439 → 22 341
(réduction Low des fougères/arbustes/herbes, arbres toujours 650).
Le script d'audit a été corrigé pour lire explicitement UTF-8 et distinguer cette
densité Low attendue de la restauration High ; aucun export modifié ou nouveau
passage UI après ces corrections. Son verdict reste partiel pour le viewport.

`web-exports/audit.json` SHA-256
`e2cc60c213b42d43a389bc43a6507903228a1fa5a6a6dfd0403dded7b7e58179` contient
les cinq hashes des exports et les dimensions. Déploiement/sources recontrôlés
inchangés à la fin. PNG High initial `forge-viewport-1788798076617.png`, final
`forge-viewport-1788798134383.png`. Pas de benchmark Web, qualification Tauri,
transition fluide ou acceptation artistique. `gate_passed=false` conservé.

Conserver le dérivé comme candidat moins chargé, sans promotion. Qualité du
feuillage sombre et transitions fluides restent dues ; ne pas répéter une série native identique
avant de garantir sa résolution. Rust/WASM inchangés : pas de rebuild Cargo pour ces scripts.
Gate 1 ouverte, Gate 2 non commencée, CI distante NOT RUN, aucune publication.

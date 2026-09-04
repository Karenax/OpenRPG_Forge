# Gate 1 — densification visible de la végétation

Date : **4 septembre 2026**. Correctif ciblé après le retour du propriétaire :
« c'est ok, mais je trouve que visuellement ca manque beaucoup de végétation ».
Cet accord sur la base avec réserve de densité n'est pas une acceptation de toutes les routes de
Gate 1, ni une revue humaine des nouvelles captures.

Candidat natif et Browser : **`8b3e3e6ccd77f33e7784306891c715c31a908cfb`**, arbre
`b277558f1e037d9894e4698ae3dcf61c7afccd3c`. Avant natif : `df6d350b`, présenté dans
[Berges et arbres](BANKS_AND_TREE_HANDOFFS.md) ; recette précédente figée par `8fb042c`.

## Changement et coût assumé

Les quatre probabilités de placement passent à 0,20 / 0,36 / 0,42 / 0,80. Le rayon des touffes
d'herbe passe de **1,55 à 2,5 m** avant échelle. Aucun changement de seed, grille, nombre de
tentatives, espacement, filtre, exclusion, profil de qualité, shader, asset ou dépendance.

| Couche canonique | Avant | Après |
|---|---:|---:|
| Arbres | 704 | 1 239 |
| Arbustes | 1 228 | 2 520 |
| Fougères | 1 282 | 2 959 |
| Touffes d'herbe | 5 068 | 12 593 |
| Total des placements | 8 282 | 19 311 |
| Brins d'herbe High | 324 352 | 805 952 |

La géométrie d'un brin reste à **7 sommets / 3 triangles**. L'herbe High compte donc 2 417 856
triangles au lieu de 973 056 : coût géométrique accru, pas une optimisation annoncée. Les 64 batches
d'herbe et 64 HLOD sont conservés ; les projections totales passent de 4 046 à 8 085 entités,
sans entité par brin. Les plafonds de génération/projection restent inchangés.

La sélection CC0 existante produit **285 arbres, 68 arbustes, 40 fougères, 43 rochers et 17 détails**,
soit **453/512** instances, 9 modèles et les mêmes 27 fichiers gouvernés. Les rochers/détails
augmentent car leur sélection existante dépend des placements d'herbe. Aucun asset Cadle ajouté.

La fixture initiale garde la révision partagée 1 ; le nouveau hash distingue son contenu :
`664df33ffe5ea42ae8e6dc9b30e496abe62f054583f483f8019f9abb6f8d00f7`.
Cache généré : `a12f488543df697325eb5df77df803ef6cf633b78fdcf017b5d2d3f4e7e263b0`.
Tous les anciens placements de touffes d'herbe restent présents. De nouveaux candidats espacés peuvent
remplacer d'anciens arbres/arbustes/fougères ; les instances survivantes et IDs de chunks restent bit-identiques.
Les paramètres de collision ne changent pas, mais davantage d'arbres peuvent activer des colliders.

Les exclusions portent sur les centres des touffes, pas chaque brin. À l'échelle maximale 1,25,
le rayon atteint 3,125 m : le cœur sans racines du chemin reste large de **3,75 m**, celui du spawn
garde **10,875 m de rayon**. Cela ne garantit pas l'enveloppe des feuilles animées.

## Validation exécutée

- `cargo fmt --all -- --check` : PASS.
- `cargo test --workspace --all-targets --locked --offline -- --quiet` : **382/382 PASS**,
  dont moteur 155, monde 54, runtime 76 et Studio 35. Régression contre la recette précédente,
  identité des survivants, espacement, projections terrain des racines et plafonds contrôlés.
- `cargo clippy --workspace --all-targets --locked --offline -- -D warnings` : PASS.
- Check moteur WASM et Clippy strict Studio WASM `--no-default-features --features web` : PASS.
  Avis tiers de compatibilité future `proc-macro-error2 2.0.1` conservé, dépendances inchangées.
- Build natif Release verrouillé/offline : **3 min 15 s**, puis `Test-Gate1VisualProof.ps1`
  High / `-NoVsync -SkipVideo` : **29 PNG 1920×1080**, 12 diagnostics de passes, contrôleur/Rapier
  exercé, stdout/stderr vides. Les 29 tailles/hashes et les cinq poses fixes avant/après sont vérifiés.
- Trunk Release verrouillé/offline, puis `Invoke-Gate1BrowserWeatherMatrix.ps1`, Edge
  **152.0.4191.62** : **Low/High PASS automatisé**, 6 checkpoints, 12 PNG, 4/4 bundles exacts.
  Chaque page : 0 erreur / 0 warning, réseau 0 échec. Canvas 1408×966, pas comparaison pixel à pixel
  avec le natif. Les 28 artefacts du manifeste sont revérifiés par taille et SHA-256.
- Les trois cycles même instance natif High / Browser Low / Browser High enregistrent wetness
  **0 → 0,78 → 0**, deux matériaux terrain et uniforms conformes. Les champs de preuve GPU précise
  et de revue humaine restent faux. Aucun PresentMon, benchmark cible ou nouvel audit de cadence.

Les messages hôte Edge `LaunchUpdateCmdElevatedAndWait` (80070002) et
`GPU state invalid after WaitForGetOffsetInRange` apparaissent aussi dans ce lot en fin de tier.
La console de page propre ne prouve pas leur absence ni leur cause. Les processus du lot sont arrêtés.

## Captures et observations

| Vue | Avant | Après |
|---|---|---|
| Hauteur humaine | [PNG](native/df6d350b0bca572d34c790eb14ec99963fd7f8d3/banks-trees/human-clear.png) | [PNG](native/8b3e3e6ccd77f33e7784306891c715c31a908cfb/vegetation-cover/human-clear.png) |
| Lointaine | [Original local](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-high-release-20260904/captures/distant-clear.png) | [PNG](native/8b3e3e6ccd77f33e7784306891c715c31a908cfb/vegetation-cover/distant-clear.png) |

Deux inspections d'agent concordent : davantage d'arbres et de sous-bois, herbe moins discontinue,
gain visible près de la caméra et sur les versants. Cadrages natifs identiques, animation non figée.
La pluie native, Low clair et le cycle High Browser ont également été inspectés.

Limites : rendu encore stylisé et anguleux ; lointain simplifié ; contraste CC0/procédural davantage
visible. L'herbe High conserve son extinction à 45–51 m du centre de batch, sans HLOD herbe. Dans la
vue eau, un nouvel arbre central masque une partie du chenal ; son raccord à la berge est peu lisible.
L'image ne suffit pas à affirmer que son pied est dans l'eau. Aucune qualification finale de placement
riparien, de silhouette ou de supériorité Grassworks/Cadle n'est déduite de ce lot.

## Identités des preuves

Natif : `E:\OpenRPG_Forge\.gate1-tools\evidence\vegetation-cover-8b3e3e6-high-release-20260904`.

- [Validation native](vegetation-cover-8b3e3e6.native-validation.json), original SHA-256
  `8799ae5b9afbc236f10a646f0dc7b88aa23c60592cfb638e5b544c0e53a7fdd6`.
- Ledger original : `f6e80743516d03b487cbc43160418c37efde68c4f4ff35e3a13083a945eae061`.
- Binaire conservé : `E:\OpenRPG_Forge\.gate1-tools\binary-controls\vegetation-cover-20260904\forge_runtime-8b3e3e6.exe`,
  SHA-256 `e6eee48fac7d6c4f281a3e2ac2c2e1a2d10b66fe2cbf9a98a015b4b08b02be9a`.

Racine Browser : `E:\OpenRPG_Forge\.gate1-tools\evidence\vegetation-cover-8b3e3e6-browser-20260904\8b3e3e6ccd77-20260904T114405363Z`.
Les six originaux canvas sont dans `low/` et `high/` de cette racine de run :
`1-initial_clear-canvas.png`, `2-rain-canvas.png`, `3-restored_clear-canvas.png`.

- [Manifeste Browser](vegetation-cover-8b3e3e6.browser-manifest.json), original SHA-256
  `29e11dc40b2774c0fd5801d6dec4b221ea181cd7b43ba21c18bb9c6e6f8989b0`.
- Ledger original : `8d926f0d672217cd3ddf6a81aa764a8da4f63b1172f21e918c76d01ff7b65b04`.

Les JSON suivis sont des copies sémantiques ; leurs chemins relatifs restent ancrés dans ces racines
externes. Les deux PNG suivis sont des copies sans transformation, hashes présents dans le reçu natif.

**Gate 1 reste à 43/55, Gate 2 non commencée.** Retour humain partiel consigné, aucun reçu de revue
formelle inventé. Le paquet humain v1 et les preuves antérieures restent intacts et historiques.
CI distante : statut canonique **NOT RUN — billing**, sous ADR-0006 ; aucun PASS distant revendiqué.

# R1 — Intégration de la liaison d'éclairage, 8 septembre 2026

## Correction générique, pas retouche de carte

`forge_engine::environment::quality::probe::publish` ne comparait que l'identité
de la texture spéculaire. Une nouvelle intensité, rotation, texture diffuse ou
valeur `affects_lightmapped_mesh_diffuse` pouvait donc ne pas atteindre la caméra.
La comparaison porte maintenant sur les cinq champs. Une liaison inchangée ne
réécrit pas le composant caméra. Aucun gain de ciel, shader, matériau, personnage
ou placement n'est modifié par ce correctif.

Le test `same_specular_handle_still_updates_each_lighting_binding_field` a échoué
avant correction : intensité caméra 0 contre source 2. Ce sont des valeurs de
fixture synthétique, **pas une mesure de la scène**. Il passe après correction et
vérifie aussi l'absence de réécriture à paramètres inchangés. Le premier appel avec
un nom court et `--exact` sélectionnait zéro test ; il n'est pas une preuve.

## Contrôles

- `cargo test --workspace --quiet` : 630 PASS, un test de timing ignoré, aucun échec.
- Premier Clippy strict : échec `float_cmp` dans l'assertion du nouveau test.
  Assertion corrigée en comparaison bit-à-bit, sans dérogation de lint.
- Test ciblé rejoué après cette correction : un PASS, 263 filtrés.
- `cargo clippy --workspace --all-targets --all-features -- -D warnings` : PASS.
- `cargo check -p forge_engine --no-default-features --features web --target
  wasm32-unknown-unknown` : PASS. Ce check n'est pas un lancement WebView.
- `cargo build -p forge_runtime` : PASS ; capture détaillée ci-dessous.

Cache de build sur E:, `CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`, deux jobs.
Branche Rust, base `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre de travail
modifié conservé : ce contrôle local ne qualifie pas une révision propre publiable.

Le harnais natif existant exporte désormais `sky_ibl_binding_snapshot` au moment
de sa requête : identités des deux textures, intensité, rotation et indicateur de
diffuse lightmappée de la sonde filtrée et des caméras actives. Il s'agit d'une
observation CPU ; les pixels de la carte diffuse filtrée ne sont pas lus.

## Lancement natif réellement vérifié

Racine locale `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-sky-binding-integration-20260908`.
`Capture.ps1 -ExpectedBinaryHash 7C4EB10B3877D6B5FA600C838C355A696BBD2910014776D79BECF0C04E4BEFDC`
réutilise le kit BLEND épinglé et le profil expérimental 2048 / trois cascades /
128 m / contact conservé, tous les autres domaines High. Marche native normale,
1800 ticks ; capture frame 1700 après fin de marche, sortie frame 1800.
Windows, RX 7800 XT/Vulkan, Full HD, binaire dev et VSync par défaut.

Une seule caméra active : les cinq champs sont égaux à ceux de la sonde filtrée.
Intensité 1,0, rotation [0,0,0,1], diffuse index 45 génération 0, spéculaire index
46 génération 0, indicateur lightmapped-diffuse true. Requête frame 900 et réponse
GPU brute unique de 3 145 728 octets. Ces pixels ne sont pas réanalysés : le nouveau
contrôle porte sur les liaisons, pas une nouvelle étude de la texture brute.

Position finale (-132,4952 ; 2,5991087 ; -203,51756). PNG inspecté : grands troncs
rapprochés, capsule visible, canopée dense, **troncs toujours presque noirs**.
La synchronisation est correcte sur ce run ; ne pas répéter ce contrôle inchangé
comme piste du défaut sombre. Filtrage diffus et matériaux restent non établis.
Overlay 22,2 FPS, moyenne glissante 45,26 ms, p95 46,98 ms, 1 % low 19,5 FPS :
ce n'est pas un benchmark avant/après ni l'objectif de performance atteint.
PID 8672 terminé, sortie 0, zéro ERROR, capture vérifiée. Absence du processus
confirmée après essai, aucun navigateur ouvert. Format et diff-check PASS.

| Artefact | SHA-256 |
| --- | --- |
| forge_runtime.exe | `7C4EB10B3877D6B5FA600C838C355A696BBD2910014776D79BECF0C04E4BEFDC` |
| candidate.png | `6B1AC0D5DCC85018BB3442BDB6D3615BA4AE5FF66740A3FC49385786CC191154` |
| candidate.metrics.json | `4C6C26CDAC910CCFBF2F89E5DAD862BF2A35F6B9583852F5DE84F289AB1E84D8` |
| stderr.log | `E0882BAC6E6EB941DC4331F750B8DB9B0A840A1791D6F92393E4ACFB0CEFE998` |

## Limites de qualification

Ce défaut de synchronisation est reproduit et corrigé, mais sa responsabilité
dans les troncs presque noirs n'est pas établie. La forêt de 9,04 à 17,80 m et ses
1 662 arbres restent issus des règles génériques précédentes, pas d'une retouche
ponctuelle. Le profil d'ombres expérimental 2048 / trois cascades n'est pas promu
au profil canonique. Aucun asset Cadle, aucune publication Git, aucune validation
humaine déduite. Gate 1 reste ouverte ; Gate 2 ne commence pas.

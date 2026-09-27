# R1-064 — admission d'un kit autonome avant chargement

2026-09-06, Rust local modifié, non publié. Gate 1 ouverte.

## Implémenté

Le runtime vérifie désormais, après le hash et avant Bevy, l'en-tête GLB v2, la longueur
déclarée, le premier chunk JSON (alignement, bornage à 16 Mio), la version glTF et l'absence
de clés `uri` dans le JSON. Une texture externe n'est pas autorisée par le seul hash du GLB.
Les ressources doivent être incorporées au candidat ; même les data URI sont refusées.
Le JSON est lu sur le même descripteur que le hash, après rewind, avec allocation bornée.

Le contrôle pur réside dans `forge_assets` et compile en WASM ; aucun Bevy, filesystem ou
réseau dans ce module. Il prépare un contrôle commun pour le Studio, mais le transport et
le chargement du kit externe dans le Studio ne sont pas encore implémentés.

Cette restriction s'applique au candidat gouverné R1-064, pas à un futur importeur glTF général.
Ce n'est pas une validation complète du conteneur BIN, des accessors, des extensions ou des
collisions. Une modification du fichier entre précontrôle et ouverture par Bevy n'est pas
empêchée par ce correctif ; un cache immuable reste nécessaire pour éliminer cette course.

## Réellement vérifié

- Tests workspace PASS, zéro échec/ignoré, dont 16 assets et 66 runtime. Ancien total narratif
  495 retiré comme non fiable ; voir la rectification et le nouveau journal dans R1_054_NO_UPSTREAM.md.
- Cas ajoutés : JSON embarqué accepté ; URI relative, traversal, HTTPS, data et extension
  refusées ; en-tête tronqué/longueur invalide/allocation excessive refusés.
- Test d'intégration : GLB à hash correct contenant une texture HTTPS refusé avant rendu.
- Clippy workspace tous targets/features strict PASS, check `forge_assets` WASM PASS,
  build runtime PASS, formatage et diff-check PASS.
- Six GLB réels du candidat corrigé chargés après le nouveau précontrôle : 20 instances,
  36 colliders prêts, zéro échec, toutes dépendances chargées ; arrêt réussi code 0 sans ERROR.

Entrées : rapport `r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json`
dans le cache externe, preview `examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json`.
Commande : `forge_runtime --world-sketch-visual-valley --asset-kit-build <rapport>
--asset-kit-preview <preview> --player-camera --metrics <preuve>/metrics.json
--metrics-warmup-frames 120 --exit-after-frames 360`.

Preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-self-contained-20260906`.
Métriques SHA-256 `8f971e9c7088be45556b18b7bb4524d3dd3f23a4ddabe7434c67e635862074f4` ;
journal `9bca730d3676bb355a4aa91680ab65b59b2820c8c7bda5b868c8511ffb5a1383`.

Pas de nouveau rendu ou de revue artistique nécessaires pour ce contrôle d'admission ;
aucune acceptation artistique déduite. CI distante NOT RUN selon ADR-0006.

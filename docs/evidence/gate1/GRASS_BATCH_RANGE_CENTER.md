# Végétation — centre de disparition cohérent sur relief

Date : **2026-09-04**. Correctif `d2c5e52`, candidat exécuté
`99a724ad105c07d013cc831b893352f816ed3692`, arbre
`89c29cb7bf20ac77aa4ad4f6bca35cbac81fe10a`, branche `Rust`.

## Correction et frontière

Le CPU et le preprocessing Bevy utilisent le centre AABB des batches grass ; le dither du shader
Forge reçoit leur origine. Cette origine restait au centre du chunk à Y=0, donc pouvait diverger
sur le relief. Le mesh est maintenant recentré sur son AABB et sa translation compensée avant
upload. Aucun shader, document canonique, asset, nombre de brins ou budget n'est changé.

Trois tests couvrent les positions monde à 0,0001 m près, les attributs/indices/extents conservés,
les distances concordantes sur terrain élevé et le vrai chemin de préparation de tous les batches.
La revue indépendante du code et du chemin Bevy 0.19.1 n'a trouvé aucun bloquant.

Ce défaut est distinct de la forme des grands brins droits : leur géométrie authored reste inchangée.
Ce lot ne prétend ni densifier la vallée, ni qualifier son esthétique, ni améliorer ses performances.

## Validation locale

| Commande | Résultat |
|---|---|
| `cargo fmt --all -- --check` | PASS |
| `cargo clippy --workspace --all-targets --locked --offline -- -D warnings` | PASS |
| `cargo test --workspace --all-targets --locked --offline` | **369/369 PASS**, dont 144 moteur et 76 runtime |
| `cargo check -p forge_engine --target wasm32-unknown-unknown --no-default-features --features web --locked --offline` | PASS ; pas un nouveau lancement Browser |
| `FORGE_SOURCE_COMMIT=<candidat>` puis `cargo build --release -p forge_runtime --locked --offline` | PASS, 3 min 23 s |
| `Test-Gate1VisualProof.ps1 -Quality high -NoVsync -SkipVideo` sur ce Release | **PASS automatisé frames-only**, 29 PNG 1920×1080, 12 diagnostics de passes, logs vides |

La première extension d'un test dépassait la limite Clippy de 100 lignes. Les assertions nouvelles
ont été isolées dans un test dédié, sans suppression de lint ; le tableau décrit le rejeu final.

Le run High Vulkan/RX 7800 XT conserve le même processus pour sec → pluie → sec, les endpoints
wetness exacts 0 → 0,78 → 0, le contrôleur gameplay et ses 24 checkpoints. Les 29 tailles/hashes des
PNG et le hash du ledger ont été revérifiés après acquisition. Aucune vidéo ni mesure PresentMon.

## Preuves conservées

- Racine externe : `E:\OpenRPG_Forge\.gate1-tools\evidence\grass-center-99a724a-high-release-20260904`.
- [Reçu automatisé versionné](grass-center-99a724a.validation.json), copie sémantiquement identique : ses chemins relatifs désignent
  cette racine externe, où restent le ledger complet et les 29 captures.
- Reçu externe SHA-256 : `dbf51c554a68fd9ffa485f0d967ec9da2b955d603dbe6321f41db2b3df91ad88`.
- Ledger SHA-256 : `0625640886d51d60d3c7fa5e05de84fdefa46c8a113435bf8346c8558b16a398`.
- Release SHA-256 : `01e55458034e9efbf982883ddd1ff06d29a140c11efd6a8dfba9a6df6e4c3f9d`.
- Exécutable conservé : `E:\OpenRPG_Forge\.gate1-tools\binary-controls\grass-center-20260904\forge_runtime-after-99a724a.exe`.
- Deux PNG copiés à l'identique dans Git :
  [vue humaine](native/99a724ad105c07d013cc831b893352f816ed3692/grass-center/human-clear.png),
  [vue lointaine](native/99a724ad105c07d013cc831b893352f816ed3692/grass-center/distant-clear.png).

Le premier run **Dev reste FAIL au validateur**, malgré ses 29 captures terminées : quatre lignes
WARN Vulkan concernent une lecture de registre et un doublon `VK_LAYER_AMD_switchable_graphics`.
Il reste dans `E:\OpenRPG_Forge\.gate1-tools\evidence\grass-center-99a724a-high-dev-20260904` ;
stderr SHA-256 `4d8e145562ad6869b2211eab96dff306044cb58ab14871bf712b58bed09fc44a`.
Le run Release est distinct : aucun filtre de log, changement du validateur ou réparation du pilote.

## Inspection et suite

L'agent a inspecté les vues humaine/lointaine, l'eau sous pluie et le retour clair du Release.
Terrain, végétation, rivière et changement météo restent visibles ; pas de nouvelle corruption
manifeste sur ces vues. Cela ne mesure pas le gain du correctif sur une traversée dédiée du seuil LOD.
L'overlay retardé ne doit pas être interprété comme le compteur exact de la frame PNG.

La vallée demeure rudimentaire : brins isolés très droits, grandes surfaces peu détaillées et berges
polygonales. Les revues humaines et comparaisons de Gate 1 restent dues. La prochaine amélioration
utile porte sur le rendu visible, pas sur un nouvel audit de micro-performance ou de pixel debug.
**Gate 1 reste ouverte à 43/55 ; Gate 2 n'est pas démarrée.** Aucun asset Cadle n'est copié.

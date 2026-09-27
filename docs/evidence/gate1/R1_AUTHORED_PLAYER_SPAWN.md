# Gate 1 — Départ joueur issu du WorldSketch

2026-09-05, branche Rust locale, sans publication. Gate 1 reste ouverte.

## Implémenté

Le runtime natif avec caméra joueur et le chargeur Studio WorldSketch utilisent désormais
le landmark `PlayerSpawn` (X=15, Z=-225), au lieu du départ historique X=16, Z=8.
`forge_world::world_sketch_player_spawn` calcule le centre de capsule depuis les triangles
du terrain sculpté, les dimensions du profil joueur et une marge de 2 cm. Les documents
source et leurs hashes ne changent pas. Le calcul reste moteur-neutre.

Un nombre de départs différent de un ou une identité/révision de terrain incompatible
produit une erreur typée. Le mode historique conserve son départ ; les probes spécialisés
conservent leurs overrides explicites. Ce raccord n'ajoute pas d'outil d'édition Gate 2.

## Réellement vérifié

- 320 tests workspace passent : assets 14, CLI 7, core 11, moteur 104, projet 45,
  runtime 47, Studio 25, monde 67.
- Le nouveau test moteur emploie le contrôleur réel et les heightfields Rapier pendant
  60 ticks : joueur au sol, collisions observées, absence de glissement, déplacement
  inférieur à 10 cm et requête caméra réussie.
- Tests du calcul : coordonnées du landmark, hauteur triangulée, rejet de terrain de
  mauvaise révision et de départ absent. Test Studio : mêmes coordonnées dérivées et
  absence d'override dans la vallée historique.
- Clippy workspace/all-targets/all-features strict et Clippy Studio WASM/Web strict passent.
  Le champ réservé au chargement Web est maintenant effectivement lu par le test Studio ;
  aucun avertissement n'a été désactivé. Studio 25 tests rejoués après correction des
  comparaisons flottantes en comparaisons exactes de bits.
- Build runtime natif réussi. Exécution réelle Vulkan/RX 7800 XT à 1920 × 1080 :
  capture inspectée, arrêt borné réussi, code 0 et zéro ERROR dans le journal.
- Bundle Studio Web construit avec Trunk, code 0, dans le sous-dossier `dist` de la
  preuve externe. L'avertissement de compatibilité future `proc-macro-error2` reste
  celui d'une dépendance, pas une erreur projet. Ce bundle n'a pas été lancé dans Chrome.

Commande, depuis la racine Rust, avec l'exécutable du target externe :

```text
forge_runtime --world-sketch-visual-valley --player-camera
  --capture <preuve>/player.png --capture-frame 240
  --metrics <preuve>/metrics.json --metrics-warmup-frames 120 --exit-after-frames 360
```

Preuve externe conservée dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-authored-player-spawn-20260905/`.
Arrêt enregistré à `2026-09-05T19:28:48.484368Z`.

| Fichier | SHA-256 |
| --- | --- |
| forge_runtime.exe | `3bc703ae2e102c8f665a8213bed4496f8516cc5a006b6cab004fcbea532a9ed4` |
| player.png | `a2b68534eca03c1c171ad08943b7e379e5bdc5316abe0d29329bb0efb9acab5b` |
| metrics.json | `8920368ea8731007ac4cf327134bc93570b5890c77d24fd9307b8fe115f08d66` |

240 échantillons après 120 frames de chauffe : 110,23 FPS moyens, p95 9,6323 ms,
1 % low 49,31 FPS, un hitch à 40,3652 ms. C'est un smoke court, pas une qualification
de performance ou une comparaison avant/après.

## Limites et suite prévue

La capsule est visible sur le terrain ; la caméra initiale regarde encore vers le bord
sud de la carte. Le raccord du départ est vérifié, pas un cadrage final de la vallée.
Cette exécution ne charge pas le kit Blender gouverné et ne qualifie pas une carte entière.
La compilation WASM ne vaut pas nouvelle exécution navigateur de ce changement.
Revue artistique, parcours vidéo, qualification humaine et Gate 1 restent ouverts.
CI distante toujours NOT RUN sous ADR-0006 ; aucun asset Cadle ni publication Git.

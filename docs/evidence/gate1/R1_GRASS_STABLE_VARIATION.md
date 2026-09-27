# Herbe : variations liées aux identités — 7 septembre 2026

## Défaut et correction

`build_grass_chunk_mesh` utilisait le rang de la touffe dans son batch pour choisir
rayon, angle, largeur, hauteur et inclinaison. Enlever ou réordonner une touffe modifiait
donc les brins de ses voisines, malgré leurs IDs et placements canoniques inchangés.
Le chemin de qualité construit précisément des batches à partir d'une densité filtrée.

La représentation compacte conserve maintenant un `variation_seed: u64`, calculé à
partir des 16 octets du UUID canonique de l'instance. Le mélange et le PRNG utilisent
des entiers de largeur explicite ; la position et le rang de batch ne servent pas de seed.
Les autres appels du bruit (branches/feuilles partagées) conservent leurs valeurs d'entrée.
Ce seed n'est pas une identité, un hash cryptographique ou une garantie d'unicité.

Les règles de dimensions, implantation des touffes, densité, nombres de brins/triangles,
projections sur le terrain, colliders et documents restent inchangées. Les positions
dérivées des brins changent une fois par rapport à l'ancien algorithme : ne pas recycler
les anciens PNG comme preuve de ce binaire. Pas d'ajout de texture, shader ou dépendance.

Cette correction rend les changements de qualité et les reconstructions moins instables ;
elle ne livre ni undo, ni sauvegarde, ni conservation de retouches dans Studio (Gate 2).
Elle ne réduit pas encore la largeur des brins et ne qualifie pas l'aspect naturel de l'herbe.

## Preuves automatisées

- RED réel : le nouveau test échoue sur l'ancien code avec
  `unaffected patch A changed after reorder` ; `red.log` conservé.
- GREEN : géométrie des touffes A/B inchangée bit à bit après permutation, suppression
  de A et réduction du budget de 12 à 6 brins (préfixe conservé).
- Second test sur des instances issues du générateur réel : même seed après changement
  de position, rotation, échelle et origine de chunk ; changement après remplacement d'ID.
- Les contrôles existants d'ancrage des racines sur terrain sculpté et des budgets passent.
- **561 tests workspace PASS, 1 test de timing préexistant ignoré** ; Clippy workspace
  all-targets/all-features strict PASS, check Studio WASM/Web PASS, fmt/diff check PASS.

Ce sont des assertions sur la géométrie CPU, pas une mesure des pixels à chaque transition.
Le check WASM compile le même calcul, sans constituer un nouveau replay navigateur.

Racine de preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-grass-stable-variation-20260907`.
Cargo : cache `terrain-water-20260906`, incrémental désactivé, `RUSTFLAGS=-C debuginfo=0`,
TEMP/TMP `terrain-water-temp-20260906`, sous `E:/OpenRPG_Forge/.gate1-build-cache`.

| Artefact | SHA-256 |
|---|---|
| workspace.log | 2F22674694EC3B11DB8F7035911B1726959A10A821796320A0D9E9344EDA0156 |
| clippy.log | CD3772DB98F000C038EDFDC24837149A5A516FCAAF456BC7741B925A98528CED |
| wasm.log | 8D8C959EA1AD9BB0D42DA4724430E0825A98AE4F04EAD25E398C60B4D4FE02C4 |

Branche Rust, base `f786a38aca2023f08d708687f99a47d14f34e6d4`, worktree modifié préservé.
Gate 1 ouverte, aucune publication ni copie d'asset Cadle. CI distante NOT RUN ;
le warning futur `proc-macro-error2 v2.0.1` reste visible.

## Lancement natif final

Build natif PASS, puis `Replay.ps1` : WorldSketch embarqué, caméra joueur, High,
420 frames, capture demandée à 300, métriques après 180. Windows/RX 7800 XT/Vulkan,
1920 × 1080, forêt procédurale sans kit externe. **Exit 0, zéro ERROR**, 87/87 pipelines
prêts, readback reçu frame 303. Image ouverte et inspectée : herbe présente et ancrée,
pas de géométrie manifestement explosée. Les brins restent larges et les touffes très
lisibles comme primitives ; ce lot ne revendique pas une amélioration artistique.

Terrain conservé : `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.
Smoke de 240 échantillons : moyenne 9,345 ms / 107,01 FPS, p95 10,211 ms,
1 % low 49,46 FPS, un hitch >33,3 ms. Pas de référence A/B contemporaine : aucun
gain de performance, absence de régression globale ou verdict GPU cible déduit.

| Artefact natif | SHA-256 |
|---|---|
| forge_runtime.exe | EB4103A7C8A67BBF5627ADC6026537888B5F432EBFD1C3790DCAB9AA86667AF4 |
| native.png | 6C8F0ECC62D6B7904E5E3A4342041F7204B2B7CAAF7B437CD1097EBF7B349340 |
| native-metrics.json | 7EEB6807BD58370C597BC8625B11A916AC4FCE5CBDF3DD6900D9A9FF192CC09C |

Suite visuelle pertinente : finesse/courbure et couverture des touffes, en conservant
désormais ces seeds stables. Pas de répétition du même test ni d'audit CPU supplémentaire.

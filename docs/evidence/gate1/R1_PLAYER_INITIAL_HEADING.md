# Gate 1 — Orientation initiale du playtest WorldSketch

2026-09-05, branche Rust locale, sans publication. Suite de `R1_AUTHORED_PLAYER_SPAWN.md`.

## Implémenté

Les chargeurs natif et Studio orientent désormais la caméra joueur WorldSketch vers le
centre XZ de la zone, qui est l'origine dans le contrat actuel. Le départ authoré reste
X=15, Z=-225 ; ni le WorldSketch, ni les profils, ni le terrain, ni les poses du kit
ne changent. Cette convention de présentation n'est pas une nouvelle donnée persistante.

L'adapter joueur accepte une cible initiale optionnelle. Il en dérive uniquement le yaw ;
l'inclinaison du profil, le contrôle libre et la caméra collision-aware existante restent
en place. Une cible verticale garde le heading par défaut ; une direction non finie est
refusée à la construction du plugin. Aucun nouvel asset ou dépendance.

La scène historique et les probes explicites conservent leur orientation précédente.
Le moteur ne dépend d'aucun nom de landmark ou slug de module.

## Vérifications exécutées

- 105 tests moteur passent, dont le test des directions initiales (centre, est, cible
  verticale) et le test physique WorldSketch après 60 ticks : au sol, absence de glissement,
  requête caméra réussie et direction alignée sur le centre (produit scalaire > 0,999).
- Premier passage du test WorldSketch échoué : la cible n'avait pas encore été raccordée
  à cette fixture. Fixture corrigée puis suite entière rejouée ; assertion conservée.
- Clippy workspace/all-targets/all-features strict, Clippy Studio WASM/Web strict,
  build runtime natif, bundle Trunk, fmt et diff-check réussis.
- La suite workspace complète précédente reste la preuve de 320 tests du lot départ ;
  elle n'est pas annoncée comme rejouée intégralement pour ce lot adapter.
- Capture native inspectée à 1920 × 1080 : joueur sur le terrain, vallée visible devant
  lui au lieu du bord sud. Arrêt borné code 0, zéro ERROR, à
  `2026-09-05T19:36:30.972189Z`.
- Probe du pont rejoué avec le nouvel exécutable, sans JSON de pont : 42,5282 m parcourus,
  518/518 ticks au sol, 435 contacts tablier, bindings préservés. Succès à
  `2026-09-05T19:36:42.440713Z`, arrêt code 0, zéro ERROR.
- Chrome réel sur `http://127.0.0.1:14625/?forge_scene=world-sketch-visual-valley`,
  onglet de test 201243121 : capture affichée et inspectée, même cadrage vers la vallée,
  canvas 1408 × 797 (pas Full HD), monde initialisé une fois, caméra 1/1,
  `parity_status=source_manifest_matched`, six hashes sources attendus et observés identiques.
  Les hashes terrain/joueur concordent aussi avec ceux du probe natif de ce lot.
  Journal navigateur warn/error vide lors du relevé ; serveur : seul favicon absent (404).
  Onglet et serveur de test fermés après contrôle. Capture Chrome conservée dans la
  conversation, pas annoncée comme PNG local versionné.

Le ledger Web observe le CPU/ECS et reste `gpu_presentation=not_observed` : la capture
réelle est une preuve visuelle séparée, pas un changement de ce verdict instrumental.

## Artefacts reproductibles

Dossier externe : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-player-heading-20260905/`.

```text
forge_runtime --world-sketch-visual-valley --player-camera
  --capture <preuve>/player.png --capture-frame 240
  --metrics <preuve>/metrics.json --metrics-warmup-frames 120 --exit-after-frames 360
forge_runtime --world-sketch-visual-valley --bridge-crossing-probe
trunk build --config apps/forge_studio/Trunk.toml --dist <preuve>/dist
```

| Artefact | SHA-256 |
| --- | --- |
| runtime natif | `24a03b5d5c5b124f534cc80efd1d251da047e775012923729a92ee549695f1c0` |
| player.png | `0813aac8d06043b1724b9fb5307b77f0e44584273811835851b736939e83d3fa` |
| metrics.json | `a4d2ea1c6e797b1dda57d7da6cd0fcd07251b8a80238036cf3a05f5643e9c4ec` |
| dist/forge_studio_bg.wasm | `8bb81522c27f5f53eecb879d9bc59cd93460c8f6710f9eea1569c308f5943309` |
| WorldSketch source inchangée | `5b2088952cad01732a53b371462247cf386bb25141c39ff97a60fb14bd2e5fc1` |

Smoke natif court : 239 échantillons, 114,24 FPS moyens, p95 9,4156 ms, 1 % low
41,98 FPS, un hitch de 48,6512 ms. Le 1 % low de ce run est sous le seuil Gate 1 :
ce run n'est donc pas une qualification de performance. Pas de boucle de micro-optimisation
engagée à partir d'une capture isolée. Le screenshot Chrome initial est également en phase
de chauffe et ne constitue pas un benchmark.

## Limites

Cette preuve ferme le défaut de direction initiale, pas R1-065 ni Gate 1. Les arbres et
le sol restent visuellement des prototypes ; aucun kit Blender externe n'est chargé dans
ces vues joueur. Douze vues normalisées, vidéo et acceptation artistique restent ouvertes.
Traversée Chrome non qualifiée. CI distante NOT RUN sous ADR-0006 ; aucune Gate 2 démarrée.

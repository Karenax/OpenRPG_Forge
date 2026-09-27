# Accès aux scènes depuis le Studio — 7 septembre 2026

## Fonction implémentée

Le menu supérieur **Scènes de preuve** ouvre les trois scènes existantes : vallée WorldSketch,
vallée de référence, scène technique Gate 0. Il n'est plus nécessaire de saisir leur query à
la main. La destination est relative à la page courante, sans origine externe ni nouvelle
permission Tauri. Le choix courant porte `aria-current=page` ; liens et bouton de déploiement
ont un focus visible. Le navigateur gère la navigation, sans nouveau système de routage.

Le menu avertit avant l'action : rechargement complet et réinitialisation des aperçus, sans
modification de projet. Seul `forge_scene` est retenu : caméra fixe, météo de démarrage,
parcours automatique et options kit/forêt ne sont pas transportés involontairement.
Pas d'ouverture de projet, édition, sauvegarde ou remplacement à chaud du monde Gate 2.
La scène par défaut reste Gate 0 ; aucune modification de carte ou d'asset.

## Vérifications réellement exécutées

- `cargo test -p forge_studio --all-features` : **40 PASS** (37 bibliothèque + 3 shell),
  dont le nouveau test des trois destinations locales et de leur parsing explicite.
- Clippy Studio natif all-targets/all-features, puis Studio WASM/Web avec `-D warnings` : PASS.
- `trunk build --config apps/forge_studio/Trunk.toml --dist <proof-root>/web` : PASS, **dev**.
- `cargo fmt --all --check`, `git diff --check` : PASS. Pas de nouveau passage workspace complet.
- Navigateur intégré, bundle servi sur `http://127.0.0.1:1429/` : parcours par clics
  Gate 0 → WorldSketch → vallée de référence → Gate 0. Identité attendue à chaque étape,
  monde initialisé une fois par page, hashes/caméra 1/1 alignés dans les vallées.
- Ouverture de WorldSketch avec `forge_view=human_bridge&forge_weather=rain` : mode fixe,
  pluie et humidité CPU 0,78 observés. Clic du menu vers WorldSketch : query réduite à
  `forge_scene=world-sketch-visual-valley`, clair, mode fixe absent, aide d'exploration revenue,
  un démarrage et parité alignée. Ceci vérifie le reset, pas les gestes manuels du joueur.
- Une capture du menu ouvert inspectée : trois libellés lisibles, scène courante différenciée,
  avertissement visible, panneau devant le canvas. Image conservée dans la trace de conversation,
  pas de PNG local revendiqué. Canvas 768×606 ; ce n'est pas une qualification Full HD/performance.
- Aucun log console de niveau error retourné pour la page finale. Onglet de preuve fermé.

Deux appels initiaux au contrôleur d'interface ont utilisé une forme de clic incorrecte,
sans navigation ; reprise après lecture de la signature documentée. Aucun échec produit caché.

## Artefacts et limites

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-scene-navigation-20260907`.
Cache Cargo : `E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906`, incrémental désactivé,
`RUSTFLAGS=-C debuginfo=0`. Warning futur `proc-macro-error2 v2.0.1` conservé.

| Artefact | SHA-256 |
|---|---|
| tests.log | E890A7CAF947890EB8E80DFD62EF1B6EFB1F77BAFC6C7D76295DFDC615E3927C |
| clippy.log | 0801C354BB0ED8E5739BE29156B79DFE15918C45EC1552C8B3B8411400504F43 |
| clippy-web.log | 210012A5659024C76885B881E9241862421D3D120BEF52AB12559E03A1289E0B |
| web-build.log | 677EFF9AF8C98BB7E17BE0A4C9681D6CE5937CCFDDC4DD7AA736E05C0DDFD83B |
| web/forge_studio_bg.wasm | 0915DD8D8BDEADB72BE84FCB9E70C228C51AAC8E629A5214ACE48B51FB682D54 |

La navigation utilise le frontend partagé mais **n'a pas été exécutée dans Tauri/WebView2**.
Les options kit/forêt ne sont pas des nouvelles destinations du menu. Aucune qualification
artistique, optique ou validation humaine implicite. Gate 1 reste ouverte et CI distante NOT RUN.
Branche Rust, changements préexistants préservés, aucune publication ou copie d'asset Cadle.

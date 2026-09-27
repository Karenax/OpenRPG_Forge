# R1-067 — scénario de traversée partagé, natif et Studio Web

2026-09-06, Rust local modifié, sans publication. Gate 1 ouverte.

## Implémenté

Le probe complet et son enregistrement natif ont été déplacés de l'exécutable vers
`forge_engine`, derrière la feature explicite `qualification`. Le runtime active cette feature
pour sa commande existante. Les features moteur `native`/`web` seules n'activent pas le harness.
Pas de second contrôleur, de copie du scénario ou de changement des seuils : commandes joueur,
contacts Rapier, progression, arrêt et conservation des bindings restent les mêmes.

L'horloge utilise l'abstraction `bevy::platform::time::Instant`, portable en WASM, équivalente à
`std::time::Instant` sur la cible native. Les sources terrain/WorldSketch/kit restent inchangées.

## Vérifié

- 395 tests workspace PASS (total corrigé ; voir R1_054_NO_UPSTREAM.md), dont les cinq tests du probe maintenant situés dans le moteur
  (151 tests engine, 61 runtime ; transfert des tests, pas cinq tests nouvellement inventés).
- Clippy workspace strict PASS après ajout de `must_use` à une méthode devenue publique.
- `cargo check -p forge_engine --no-default-features --features web,qualification
  --target wasm32-unknown-unknown --locked` PASS.
- Build runtime, formatage et diff-check PASS.
- Traversée native réelle avec le kit gouverné : sortie 0, événement completed réussi,
  518/518 ticks de traversée au sol, 435 contacts avec le tablier, progression 42,528 m,
  surélévation maximale 3,90455 m, terrain/joueur/pont conservés, arrêt final au sol.
  Aucun ERROR ; journal de fermeture success=true et runtime_errors_observed=false.

Commande : `forge_runtime --world-sketch-visual-valley --asset-kit-build <rapport>
--asset-kit-preview <preview> --bridge-crossing-probe`.
Rapport : cache `r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json` ;
preview : `examples/visual_valley/asset-kits/r1-063/runtime-preview-authored.v1.json`.

Preuve : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-shared-bridge-probe-20260906/runtime.log`.
SHA-256 journal `51824e7d4902baab6b002b232e26ab98b68920e3015f39612edf74274588f514` ;
runtime `6f0789cbb133ad448789ac7914895fb318ad1e747ce9720531e94f9a2cd186eb`.

## Raccord Studio implémenté et vérifié le 6 septembre

Le Studio active la feature moteur `qualification` et accepte explicitement
`?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_probe=bridge`.
Il réutilise le même scénario, les mêmes commandes séquencées et contrôles Rapier. Le contrôleur
interactif est désactivé pour cette exécution ; recharger sans `forge_probe` restitue le mode habituel.
La caméra regarde le pont, le joueur commence trois mètres avant celui-ci. Le scénario attend le
kit prêt, conserve son watchdog de 30 secondes et affiche un verdict dérivé dans
`studio-bridge-probe-status`. Il laisse la scène ouverte et soumet un arrêt neutre à son terme,
y compris en échec ; le mode natif conserve son AppExit. Aucun document source n'est modifié.

- 395 tests workspace PASS, Clippy workspace strict et Clippy WASM Studio PASS ; builds natif et
  Trunk PASS. L'assertion de configuration teste aussi le mode viewport et son axe caméra.
- Chrome/WebGPU, kit 20/20 instances et 36 colliders prêt, monde initialisé une seule fois :
  `completed`, 518/518 ticks au sol, 434 contacts tablier, progression 42,528 m.
  Console warn/error vide au relevé. Build final rejoué avec le même résultat et aide corrigée.
  Capture de conversation inspectée : joueur arrêté après traversée, terrain et kit rendus ;
  viewport 1408 × 741, pas une qualification Full HD.
- Paramètre `forge_probe` répété et demande sur la vallée non WorldSketch : refus explicite
  observé dans Chrome, zéro initialisation du monde dans les deux cas.
- Régression native avec le même kit : sortie 0, 518/518 ticks au sol, 435 contacts, progression
  42,5282059 m, bindings terrain/joueur/pont conservés et arrêt final au sol. Aucun ERROR.
  Deux avertissements wgpu sur `@invariant`/comparaison de profondeur Equal restent visibles ;
  ils ne sont pas assimilés à un audit graphique réussi.

Preuve native : cache `r1-native-crossing-embedded-adapter-20260906/runtime.log`, SHA-256
`24c310161698b9de2a99e70836022d9a7a6d9cd1e484663f418f49d1dd8a2537`.
Exécutable : `33b4601c79ef90855c2d28cea25c8193b1fc80a5e05fe2250e916bcda03b0232`.
Premier build Web : cache `r1-studio-crossing-20260906/dist`, WASM
`2f6f110573de265a430136156611a08f996910f687b3edd00468c7876ac817da`.
Build final avec aide indiquant les commandes désactivées :
`r1-studio-crossing-20260906-final/dist`, WASM
`0d4cc18e1197b8f5c71510ee22e53114f6d9e864143632c356c59f4e877c15c1`.

## Limites et travaux restants

Le parcours automatique ne vérifie ni les touches maintenues ni la capture souris ni une traversée
interactive humaine. Le fournisseur de navigation reste non sélectionné. Les chronométrages
instantanés affichés ne constituent pas un benchmark : compilation native concomitante au premier
parcours, résolution et conditions non normalisées. Pas de nouvelle vidéo ni de PNG durable.
La revue humaine, Tauri et le passage de gate restent ouverts. La CI distante est NOT RUN sous
ADR-0006 ; l'arbre modifié n'est pas une qualification finale propre. L'avertissement de compatibilité
future `proc-macro-error2 2.0.1` reste présent dans les builds Web.

## Rejeu intégré avec forêt gouvernée et végétation corrigée

2026-09-06 : même scénario, désormais avec `--prototype-forest-preview` et catalogue/kit candidat
cohérent révision 2. Le runtime courant inclut arbustes ajourés, fougères découpées et litière au sol.
Aucun nouveau code de probe ni modification des seuils. La forêt est projetée avant le kit prêt
dans le journal natif : 636 arbres/7 632 primitives, kit 20 instances/36 colliders.

Natif : sortie 0, success=true, 518/518 ticks au sol, 435 contacts tablier, progression
42,5282059 m ; bindings terrain/joueur/pont conservés et arrêt final au sol. Avertissement wgpu
`@invariant`/Equal présent, `runtime_errors_observed=false`. Le champ de fermeture
`automated_run=false` ne remplace pas le verdict explicite du probe automatisé.

Journal `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-crossing-20260906/native.log`, SHA-256
`4eec750824942c57d2c0eb8f18eeefbb51ffb49b66225e78128ef2f1a1e74eee`.
Exécutable SHA-256 `ac8e0f0dfdb5b7839bac814653e1b47e3a67c1676ca26d1e672b54a4381b9e4d`.

Studio Chrome : bundle `r1-vegetation-ground-web-20260906/web-dist` (hash dans
`R1_STUDIO_FOREST_WIRING.md`), URL avec `forge_forest=preview&forge_probe=bridge`.
Premier plan demandé pendant crossing ; completed observé, 518/518 ticks au sol, 434 contacts,
42,528 m, forêt et kit prêts, monde initialisé une fois. Capture inline d'arrivée inspectée,
logs erreur/warn collectés vides. Canvas final 2048×1101 ; ni capture normalisée archivée ni
benchmark de performance. Les 120 frames de démarrage (29,08 ms moyens, p95 38,30 ms) ne prouvent
pas le budget cible ni une comparaison contrôlée avec le natif.

**Défaut visuel repéré à l'arrivée :** aplats triangulaires brun-vert au sol, près de la sortie
du pont. Cause non établie dans ce lot (ne pas l'attribuer sans diagnostic au shader ou aux LOD).
Le prochain diagnostic doit distinguer poids de matériaux/chemin, géométrie superposée et LOD.
Le succès de collision ne qualifie pas ces aplats, la végétation naturelle, les gestes humains,
Tauri ou Gate 1. Aucune publication Git ni nouvelle gate.

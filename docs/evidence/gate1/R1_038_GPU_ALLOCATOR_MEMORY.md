# R1-038 — Mémoire de l'allocateur GPU

2026-09-06, Rust local. Implémentation commune au diagnostic moteur, sans modification de
carte, asset, preset ou renderer. Gate 1 ouverte ; aucun push.

## Contrat implémenté

`GpuDiagnostic.memory` ajoute un relevé optionnel aux rapports runtime existants. Avant le
premier relevé : null. Après appel backend : frame, temps écoulé, portée, statut et totaux
optionnels. `allocator=null` signifie rapport indisponible, pas zéro octet utilisé.

`gpu_memory.rs` appelle l'API publique `RenderDevice::wgpu_device().generate_allocator_report()`
au plus une fois toutes les cinq secondes. Aucun nouveau crate, backend, unsafe, interrogation
OS ou compteur par entité. Seuls les totaux sont conservés ; noms et listes d'allocations sont
jetés après lecture. Les chiffres distinguent :

- octets alloués aux allocations vivantes de cet allocateur ;
- octets réservés dans ses blocs, y compris l'espace non alloué ;
- nombres d'allocations et de blocs.

Ce n'est **ni la VRAM résidente, ni la capacité de la carte, ni toute la mémoire GPU du processus**.
Le rapport n'identifie pas les types de tas ; ne pas traiter automatiquement tous ses octets
comme de la VRAM dédiée. Mémoire hors allocateur, overhead pilote et autres processus non mesurés.
Le timestamp permet de distinguer l'âge du relevé de la frame de sortie du rapport.

Sources locales épinglées consultées : wgpu 29.0.4 `src/api/device.rs`, wgpu-types 29.0.4
`src/counters.rs`, wgpu-hal 29.0.4 `src/vulkan/device.rs`, backend WebGPU wgpu 29.0.4
`src/backend/webgpu.rs`. Vulkan fournit les totaux de gpu-allocator ; le backend navigateur
retourne None dans cette version. Compilation Web vérifiée, comportement navigateur non rejoué.

## Vérifié

- Deux tests : sérialisation disponible/indisponible avec fraîcheur et portée explicites ;
  première lecture puis intervalle minimal de cinq secondes, sans répétition à temps égal.
- `cargo test --workspace --all-features --locked -q` : **458 PASS, 1 timing ignoré**,
  dont 192 moteur PASS / 1 ignoré. Aucun test retiré.
- `cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` : PASS.
- `cargo check -p forge_studio --no-default-features --features web --target wasm32-unknown-unknown
  --locked` : PASS ; avertissement futur préexistant proc-macro-error2 2.0.1.

Lancement natif de la scène WorldSketch, qualité High demandée, météo clear, caméra héroïque,
kit candidat et forêt. Sortie 900 frames, warm-up métriques 120, pas de nouvelle capture :
aucun changement visuel dans ce lot. Code 0, zéro événement ERROR,
`runtime_errors_observed=false`, kit 20/20 instances, dépendances chargées, 36 colliders kit.

Relevé Vulkan RX 7800 XT, pilote AMD 26.8.1 :

| Champ | Valeur observée |
| --- | ---: |
| frame du relevé | 731 |
| temps du relevé | 15,0114616 s |
| alloué | 888 353 628 octets (847,20 Mio) |
| réservé | 1 182 734 692 octets (1 127,94 Mio) |
| allocations | 585 |
| blocs | 6 |

Vérification du JSON : statut available, alloué strictement positif, réservé ≥ alloué,
frame après warm-up, kit complet et aucun ERROR. Chiffres ponctuels, pas pic mémoire ni budget
validé sur GPU cible. Coût de collecte non benchmarké ; aucune optimisation revendiquée.

## Reproduction et preuves

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-gpu-allocator-20260906`.
Fixture : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-coherent-runtime-20260906-b`.
Runtime : `E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906/debug/forge_runtime.exe`.

```text
forge_runtime --world-sketch-visual-valley --quality high
  --asset-kit-build <fixture>/asset-kit-build.v1.json
  --asset-kit-preview <fixture>/runtime-preview.v1.json
  --prepare-vegetation-catalog <fixture>/prototype-catalog.v1.json
  --prototype-forest-preview --metrics <racine>/metrics.json
  --metrics-warmup-frames 120 --exit-after-frames 900
```

SHA-256 runtime : `f03282cf6df610909a27b7d0ba1d04308b030e9d0c4bc580adb44b5621959442`.
`metrics.json` : `9834439c3b0b2917a8c24aca7e11219516d1a17da8a31b073d816ec24dd75109`.
`runtime.jsonl` : `70bf791d03606bae72abd64560f27fc69207c9d8cbec2c3ee12a109efd0ce47c`.
Les métriques épinglent les sources WorldSketch/terrain et le manifeste du kit.

R1-038 reste partiel : pas d'affichage dédié Studio, de mesure de résidence réelle, de qualification
sur matériel cible ni de couverture de tous les backends. Profils complets et Gate 1 non acquittés.
CI distante NOT RUN sous ADR-0006. Processus terminés, aucun asset Cadle importé.

## Raccord Studio et export vérifié — 2026-09-06

Un composant de présentation lit le diagnostic moteur existant et expose son dernier relevé
dans l'inspecteur et dans `observations.gpu_memory` de l'export JSON. Aucun second compteur
ni mutation de projet. États distincts : attente initiale, backend indisponible, totaux disponibles.
L'affichage numérique emploie une conversion entière en Mio à deux décimales tronquées,
testée jusqu'à u64::MAX ; octets exacts conservés dans le JSON.

`cargo test -p forge_studio --all-features --locked` : **33 PASS** (30 bibliothèque, 3 shell).
Clippy Studio natif/all-targets/all-features puis wasm32/web strict PASS. Trunk build debug
réussi dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-gpu-memory-20260906/dist`.
WASM inspecté : `e914338690a95be7a03ba067159b41dbc538ad50d4b24501e97d1ba4ca2ed70c`.
Un import wildcard remplacé ensuite par trois imports explicites pour Clippy Web ; aucun
changement de comportement après ce build. Pas de nouveau verdict workspace complet.

Échecs intermédiaires conservés : cache ACL Tauri référençant l'ancien target C: supprimé ;
617 artefacts Tauri régénérables (82,7 Mio) retirés uniquement du target E: vérifié, puis
reconstruction et tests desktop réussis. Valeur attendue du test 847,19 corrigée en 847,20,
après vérification du quotient exact 847,2000389 ; aucune assertion de contrat retirée.

Chrome, route `?forge_scene=world-sketch-visual-valley`, kit externe non demandé : capture inline
inspectée, libellé « Mémoire GPU : mesure indisponible sur ce backend » lisible, aucune valeur
native recyclée. Viewport rendu 2048×1101 ; une initialisation du monde. Le ledger DOM observé
à frame 3026 / 86,057 s indique backend_report_unavailable, allocator null et portée explicite.
Zéro message console error dans le relevé. Ceci ne qualifie ni Tauri en exécution ni la scène
finale avec kit ; aucune nouvelle revue artistique.

Le bouton d'export a été utilisé ; le fichier transmis ensuite par l'utilisateur a été lu et
validé : `C:/Users/karen/Downloads/forge-diagnostic-1788712975153.json`.
SHA-256 `3d414be69b4aa5add6bf64d821b17106615f2c42adcd602c7958dc082a44840a`.
Export du 2026-09-06T16:42:55.151Z : mémoire frame 586 / 40,9645 s, allocator null,
backend_report_unavailable ; gate_passed=false et human_acceptance=false. L'écart de frame avec
la lecture DOM ultérieure est attendu. **Réception et contenu du JSON vérifiés**, pas seulement
demande de téléchargement. Le PNG inline n'a pas été archivé en fichier local.

La réserve d'affichage/export Studio est levée sur ce cas Web indisponible ; les nombres
disponibles restent prouvés nativement et par test de formatage, pas dans le navigateur.
R1-038 et Gate 1 restent ouverts pour les limites de mesure/qualification ci-dessus.

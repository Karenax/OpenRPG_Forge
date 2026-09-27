# Gate 1 — Préparation et délai du parcours de pont

8 septembre 2026, Rust local, aucune publication, Gate 1 ouverte.

## Échec observé

Le candidat Studio du raccord d'eau utilise le même WASM sous l'URL explicite
`forge_probe=bridge`. Nouvelle option du script `Build-StudioCandidate.ps1` :
`-BridgeProbe`, sans modification du lancement interactif par défaut, des pins,
du moteur ou de la carte. Trois cas valides et quatre refus du script de
qualification d'entrées PASS. Tauri compilé en 19,04 s, sans rebuild WASM.

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-shore-studio-bridge-20260908`.
Lancement PID 3284, capture UI `timeout.jpg` : **failed**, 28/28 ticks au sol,
zéro contact tablier, 1,855 m parcouru. Motif : watchdog réel 30 secondes.
Le personnage est au début du pont, pas à l'arrivée ; aucun PASS de traversée.
Fenêtre fermée et PID absent au contrôle après essai.

Inspection du code : `BridgeCrossingProbeState::new` initialise `started_at`
avant chargement/préparation. Le watchdog utilisait ce même instant pour
WarmingUp, Crossing et Stopping. L'attente initiale était donc débitée du
budget réel de marche. Les systèmes de déplacement attendent notamment le kit
prêt, mais ce timer ne l'attendait pas.

## Correction implémentée et vérifiée

Préparation bornée à 120 secondes (budget déjà utilisé par les gardes de
capture/chargement). Quand les bindings Rapier sont stables, le joueur au sol
et le warmup fixe satisfait, démarrage du budget Crossing+Stopping de 30 secondes.
Un seul changement d'origine, aucun renouvellement pendant marche ou arrêt.
Les maxima de ticks, exigences de sol/contact, distances et invariants sont
inchangés. Le mode enregistrement conserve son propre garde existant.

Le nouveau test couvre préparation froide, timeout de préparation, 29/30 secondes
de mouvement, partage du budget avec l'arrêt et neutralité après completion.
Pas de sleep réel dans le test ; instants injectés aux décisions.

Racine de rejeu : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bridge-deadline-20260908`.
`Verify.ps1` : workspace, Clippy strict, check WASM et build natif.
`BuildStudio.ps1` : frontend neuf puis candidat au parcours explicite.
Le prochain verdict dépend de leurs résultats et du parcours réel, pas de la
seule modification du chronomètre. CI distante NOT RUN sous ADR-0006.

## Résultats natifs

637 tests workspace PASS / un timing préexistant ignoré ; Clippy strict workspace
tous targets/features PASS, check Studio WASM PASS, build natif PASS (2 min 31 s).
Le warning futur `proc-macro-error2 2.0.1` reste visible. Aucun contrôle de
licence/CI distante prétendu rejoué pour ce changement sans dépendance.

Natif SHA-256 `D3E2EE6A5D99645399AE3518C4E55FCB59DE6F6269E3DD86D8CEACC38F0A530F`.
`RunCrossing.ps1` lancé avec kit/forêt gouvernés complets, ni capture ni mesure
FPS combinées au parcours. Événement `bridge_crossing_probe_completed` à
17:57:07 UTC : 42,5282059 m pour 42,4999886 m requis, 518/518 ticks au sol,
435 contacts tablier, zéro perte de sol, état final au sol et trois bindings
préservés. Sortie 0 sans ERROR, PID 936 terminé. Le harness générique conserve
`automated_run=false` : c'est le verdict spécifique du parcours qui fait preuve.

Terrain `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208` ;
pont `39b798e798530e0dee56a0185112918d58f9e9753e0ccfc3cfa57115600cdd43`.
La scène et le correctif d'eau sont inchangés ; aucune preuve artistique ajoutée
par ce parcours natif.

## Résultat WebView

Frontend et coque compilés avec succès. WASM SHA-256
`8989262392B5790AE3E7DA12533CE6EB6BD827FB0EC819D79AAA3CCB9DCBE6F8`,
EXE `6E95843F64083B784417B61553C235D7D3F654E7206DE18ECED0A0961B9C1F20`.
Parcours réel terminé : 518/518 ticks au sol, 434 contacts tablier, 42,528 m.
Kit 20/20 et forêt 1 662 arbres observés. Exports UI conservés dans la racine
de rejeu : `studio-diagnostic.json` SHA-256
`954F095D3257A3123113CD5710F59CE2A257EC20697E0C827B5417B253943FF8` ;
`studio-arrival.png` SHA-256
`5DE442B44FBAA69CAA3BCDB1F55A057E9D9C2D11A232C0D3E7A383D3A389503F`.
La caméra finale regarde la forêt : ce PNG ne prouve pas le raccord de rive.
Session PID 39884 fermée et absence vérifiée ; propriétaire PID 39624 préservé.
Premières 120 frames : moyenne 41,79 ms / p95 47,40 ms, démarrage avec autre
Studio ouvert, pas un benchmark. Gate 1 reste ouverte.

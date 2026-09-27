# Gate 1 — Rejeu WebGPU des correctifs de végétation

2026-09-06, branche Rust, aucun changement de code, aucune publication. Les correctifs
de touffes, de densité par zones et de repères LOD ont été compilés puis lancés réellement
dans Chrome, au-delà du check Rust/WASM précédent.

## Build et entrées

`trunk build --config Trunk.toml --dist
E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-web-20260906/dist`, depuis
`apps/forge_studio`, sortie 0. Cache Cargo existant `openrpg-forge-r1-064-target`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`, `NO_COLOR=true`.
L'avertissement de compatibilité future `proc-macro-error2 2.0.1` reste présent.

Le build kit et les six GLB déjà gouvernés ont été déployés dans le nouveau dossier,
avec contrôle SHA-256 de chaque GLB avant copie. Preview authoré rev3 inchangé.
Aucun fichier Cadle, aucune bibliothèque d'assets nouvelle, aucun changement de source.
Serveur de preuve uniquement sur `127.0.0.1:14641`, sans exposition réseau publique.

URL : `http://127.0.0.1:14641/?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_probe=bridge`.

## Observations réelles

Le canvas observé mesure **1408 × 797**, donc pas une qualification du viewport Full HD.
Le Studio initialise un seul monde ; les diagnostics affichent les hashes alignés, une caméra
active sur une attendue, eau/météo observées et le kit prêt à **20/20 instances et 36 colliders**.
Le profil High est projeté pour les budgets disponibles ; `full_preset_applied=false`,
eau/environnement toujours partiels. La capture affichée et inspectée dans l'outil montre le
personnage sur le pont, les arbres et l'herbe rendus : ce n'est pas seulement une preuve CPU.
Le diagnostic produit « présentation GPU non observée » reste inchangé : cette capture est
une preuve externe, pas une raison de falsifier le ledger du produit.

Premier parcours : **échec conservé**, watchdog réel de 30 secondes dépassé à 352 ticks
tous au sol, 311 contacts, 28,823 m. La cadence glissante proche de 1 Hz visible lors de la
mise au premier plan suggère une limitation d'arrière-plan ; l'état initial de visibilité
n'ayant pas été relevé, ce n'est pas une causalité formellement démontrée.

Après `Page.bringToFront`, visibilité `visible` et focus `true` vérifiés, la même URL a été
rechargée sans cache. Aucun délai ou critère n'a été modifié. Le rejeu termine avec :

- **518/518 ticks au sol, 434 contacts avec le pont, 42,528 m parcourus** ;
- kit toujours prêt, hashes alignés, une seule caméra ;
- aucune entrée `Runtime.exceptionThrown`/`Log.entryAdded` dans la fenêtre CDP 45→88,
  sans pagination ni troncature. Cela n'est pas un audit exhaustif de tous les logs navigateur.

L'archivage de la capture finale a échoué : `native pipe closed before response`, puis
`Debugger unattached` lors de la récupération. Aucun PNG local n'a été obtenu ; ne pas
revendiquer une capture finale durable. L'onglet de preuve n'a pas pu être fermé par l'outil.
Le serveur local est arrêté séparément après vérification de son PID et de sa ligne de commande.

## Artefacts et verdict

Dossier `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-web-20260906`.
`observations.json` est explicitement une transcription des résultats CDP par l'agent,
pas un export signé du runtime. Le WASM et son bootstrap sont conservés pour reproduction.

| Artefact | SHA-256 |
|---|---|
| `dist/forge_studio_bg.wasm` | `c722a1166a87b60d67b422165d4cdd984644e127c163b25eb7041e47febeb6c6` |
| `dist/forge_studio.js` | `0edc28407fbef69e26451fa96b799202e410a6d602331db7dbe020b16b9f00dd` |
| `build.log` | `42fd7ff0fc20014bcd2cc6428dae5494adc716b820d4820ed64f254a1fe5e684` |

**Rendu navigateur observé et parcours automatique rejoué avec succès**, mais pas de benchmark,
de paire temporelle du vent, de capture finale archivée ni d'acceptation humaine. Les défauts
artistiques, la comparaison normalisée et les autres exigences de Gate 1 restent ouverts.
Cette preuve ne clôture pas R1-014 ni Gate 1 ; CI distante NOT RUN sous ADR-0006.

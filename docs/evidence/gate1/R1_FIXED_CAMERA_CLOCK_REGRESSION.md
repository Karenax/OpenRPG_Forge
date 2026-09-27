# Vue fixe et horloge pas à pas — 7 septembre 2026

Gate 1 reste ouverte. Ce correctif fiabilise une observation du rendu ; il ne
qualifie ni le vent lointain, ni les performances, ni la qualité artistique.

## Défaut reproduit

Le premier candidat Web autorise un pas de 250 ms puis refuse le suivant : le
suivi joueur exécuté dans `FixedUpdate` écrase la caméra de preuve. Le temps
virtuel initialement gelé masquait ce conflit. Le contrôle strict de pose/FOV
détecte correctement la dérive et n'est pas assoupli.

Le nouveau test `fixed_proof_clock_preserves_camera_during_player_ticks`, avec
le vrai plugin joueur et Rapier, échoue avant correction : la caméra authorée
(-65, 42, -245) est remplacée par (0, 0.8422692, 4.4016643).

## Correctif et preuves

La présence de `FixedProofViewStatus` réserve la caméra à la qualification et
empêche uniquement le système de suivi de s'exécuter. La simulation joueur
continue. Sans ce mode, le système normal conserve son fonctionnement.
Le garde-fou est compilé seulement avec la feature `qualification`.

17 tests ciblés moteur/joueur PASS après correction. Le test vérifie deux pas,
500 ms de temps virtuel, pose/FOV/near/far inchangés, ticks joueur effectivement
exécutés, puis reprise du suivi dans le schedule physique après retrait de
l'exclusivité diagnostique. Il ne constitue pas une preuve de présentation GPU.

Logs locaux sous
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907` :
`proof-camera-red.log` (échec attendu avant correctif),
`proof-camera-green.log` (17 PASS).

Validation élargie : 575 tests workspace PASS / 1 timing ignoré, Clippy strict
workspace all-targets/all-features et Studio WASM PASS. Logs
`proof-camera-workspace.log`, `proof-camera-clippy.log`, `proof-camera-wasm.log`.
Le warning de compatibilité future `proc-macro-error2` reste présent.
Le premier lancement via Windows PowerShell a traité stderr Cargo comme une
erreur de script ; aucun verdict test déduit. Relance avec PowerShell 7 après
confirmation de l'absence de processus Cargo/rustc, sans jobs concurrents.

Build Web PASS (`proof-camera-build.log`, sortie 0), puis rejeu Chrome effectué.
Bundle `web-proof-camera`, WASM SHA-256
`184EE180958152563336B557F805D5AEA1FDDF9FBAC70B880AC76C7CF307476F`.
Sur `valley_overview` candidate (FOV 10°, données copiées du candidat précédent),
le statut passe successivement de 0.000/0 à 0.250/1 puis 0.500/2 sans refus.
Le hash des vues reste `d71f7c2a43c5ea068241effd88793d6ff8cfce3715a52bbabbb0f2729ad32656`.
Aucune erreur dans les logs navigateur consultés. Deux PNG 1408×741 ouverts et
inspectés : cadrage conservé, pas de saut vers le joueur. Cette paire ne suffit
pas à attribuer les variations du feuillage au seul vent plutôt qu'au dithering/TAA.
Le vent lointain reste non qualifié.

Artefacts dans `proof-camera-after/` : captures `forge-viewport-1788781976220.png`
et `forge-viewport-1788782014352.png`, diagnostic `forge-diagnostic-1788782024572.json`.
Les images de l'ancien candidat ne sont pas réutilisables comme validation du
correctif. Diagnostic du refus et captures précédentes conservés dans
`proof-camera-before/`, dont `forge-diagnostic-1788781788006.json`.

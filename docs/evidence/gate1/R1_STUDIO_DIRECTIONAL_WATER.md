# R1 — eau directionnelle : intégration Studio et trois instants

9 septembre 2026, branche Rust. Gate 1 ouverte. Pas de publication.

## Construction réellement exécutée

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-directional-water-20260909/`.
`BuildStudio.ps1` réutilise le protocole du candidat de lit minéral, dans une
nouvelle destination. Les anciens bundles et changements du worktree sont préservés.

- Tests workspace : 648 PASS, zéro échec, un ignoré.
- Clippy Studio WASM strict PASS ; Trunk release et packaging Tauri PASS.
- Avertissement de compatibilité future `proc-macro-error2 v2.0.1` conservé dans
  le log ; il ne constitue pas une erreur de cette compilation.
- EXE : `DA0EF2F81988B1201AD2F8C01F8A2620C1D1829D57DB181E4C061E3DEF6E14F0`.
- WASM : `4CCBA1991C7BEE91DF2697E92AFD76A040E5B67AC1A3129390F72AD13F9B2CDE`.

Les logs complets et `candidate/build-proof.json` sont conservés. Ce dernier
reste un rapport BUILD_ONLY ; les preuves d'exécution sont les exports ci-dessous.

## Exécution du Studio

Candidat ouvert et contrôlé par l'interface Windows, entrée `water_close`,
kit gouverné, forêt en batches, `forge_water=depth-preview`. Aucun shader ou
paramètre artistique modifié pendant l'essai. Exports natifs de l'interface,
pas des captures substituées. Canvas 1408 × 966, caméra et carte inchangées.

À chaque instant, diagnostic JSON puis PNG exportés séparément : paire non
atomique, temps virtuel figé entre actions. Le bouton existant « Avancer
l'animation de 250 ms » a été actionné deux fois ; le nouvel état a été
observé avant chaque export. Préparation : 74 pipelines, zéro pending et
zéro failed final, pic de 18 erreurs transitoires pendant le préchauffage.

| Temps | Diagnostic JSON | Viewport PNG | Phase CPU (radians) |
| --- | --- | --- | --- |
| 0 s | forge-diagnostic-1788910533803.json | forge-viewport-1788910548081.png | 2.115455380293542 |
| 0,25 s | forge-diagnostic-1788910586695.json | forge-viewport-1788910601954.png | 2.32108690760866 |
| 0,50 s | forge-diagnostic-1788910642162.json | forge-viewport-1788910654236.png | 2.52671843492379 |

Hashes SHA256, dans le même ordre JSON / PNG :

- 0 s : `CE86A0B73592D6D80BC08705EA912BBE9EE8E5E623E31543849A64DDFEF32C86` /
  `DD2DBCC91E94A86927358D54506B89893DD0D0BB81D99C1AA5D62F709D829C28`.
- 0,25 s : `660C2C8A0FD89FBEC4B7E7E4993C19E872E33DBA095A100BE2F535AA7FBA63EA` /
  `4EE263DA40E7E70C709FAC0392F9DA5CE4D544CDA5756C7936E68C1A853363AC`.
- 0,50 s : `5AB66D36C09BB2101BBDE220AA7F3450AB386E1030CA59B185EB8848977E65AA` /
  `64E9A49462A44ED07AE62095BE99FAFB9584CA5C42670244B13FB50BC9988D66`.

Originaux dans Downloads, copies vérifiables dans la racine locale de preuve.
Les trois PNG ont été inspectés. Les rides et leurs nuances changent sous le
pont ; caméra/pont restent en place. Le fond brun dominant et les stries
régulières persistent. Ce court échantillon confirme une variation visuelle
dans le Studio, pas une animation fluide à cadence réelle, une direction
physique mesurée du courant ou une acceptation artistique.

Les trois diagnostics observent le candidat optique, 70 995 triangles eau/berges
et un état `prepared` sans erreur finale. Les profils d'eau restent désactivés
explicitement : aucune qualification de R1-023. Le chargement de colliders du
kit affiché à zéro dans cette vue figée n'est pas une preuve de playtest ; cette
séance ne qualifie pas les collisions de forêt.

## Cadrage complémentaire natif

`curve-world-sketch.json` ne change que les caméras de la copie de preuve,
comparaison sémantique hors caméras PASS, sans réécriture des seeds. SHA256
`B34F45FBD453ACDD21088C29CFAAD7D31D3BEC0C2AA61A16385E1D56C14620AE`.
`CaptureCurve.ps1` utilise l'EXE métrique précédemment épinglé, sans kit gouverné.
Vue `water_curve` : position [-18,14,-105], cible [-36,2.6,-112], FOV 58°.
Vue technique surélevée, pas preuve à hauteur humaine.

`curve.png` : `43BCA280C89E9EF7520E46A61997DCC317E95995100CC5E0926C42BBD34F1BB4`.
Capture acquise, log sans ERROR, sortie zéro. Inspection : plus grande portion
d'eau visible, toujours très lisse et striée. Ce cadrage ne prouve pas tous les
virages ni le mouvement. Pas de benchmark pendant la compilation du Studio.

## Nettoyage et verdict

PID natif 5996 terminé. Studio 19560 et enfant 38836 terminés ; ancienne session
propriétaire conservée. Aucun navigateur ouvert, aucun processus de compilation
laissé par ce lot.

Intégration WebGPU des corrections métriques confirmée. Rendu artistique,
fluidité continue, profils et comparaison Cadle restent non qualifiés.
R1-022/023 et Gate 1 restent ouverts. Ne pas refaire ces trois exports pour
annoncer un progrès supplémentaire sans changement pertinent ou preuve nouvelle.

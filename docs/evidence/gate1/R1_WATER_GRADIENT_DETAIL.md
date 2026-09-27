# R1 — normales de détail à gradients continus

9 septembre 2026, branche `Rust`, worktree existant préservé. Gate 1 ouverte.

## Implémenté

Le helper original `water_flow_detail.wgsl` remplace la dérivée du bruit de
valeurs par un bruit à gradients, interpolé avec une fonction quintique.
Les pentes ne sont plus forcées à zéro aux coins de la grille. La deuxième
échelle est tournée et sa dérivée ramenée dans le repère mondial par la
transposée de cette rotation. Advection, amplitudes, fréquences, filtrage par
empreinte écran et matériau optique restent inchangés. Aucune donnée de carte,
géométrie, texture tierce ou dépendance ajoutée.

SHA256 du helper :
`7279D2A84F8F1DC57549E8AE1BAE7549E9E9DFD497D7EE0E3873A6CEBA9BD09A`.

## Preuve native

Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-gradient-detail-20260909/`.
`Capture.ps1` fournit la commande reproductible et le nettoyage du PID possédé.
Même entrée WorldSketch avec caméra `water_close`, phase figée, capture à la
frame 300 et sortie à 420 que le témoin de `R1_WATER_OPTICS_ISOLATION.md`.
Scène isolée sans kit gouverné : ce n'est pas la qualification de la forêt.

- Témoin : `../r1-water-isolation-20260909/baseline.png`, SHA256
  `8FC071F03D5DB63401EC9101448E035D65B814B43BE134FD30142FDCD5C92879`.
- EXE candidat : `forge_runtime.exe`, SHA256
  `36E30279273207EC8BDFA4CA66874BBCF194F7E5A5A0243B69A02F31EE6F10E6`.
- Capture candidate : `after.png`, SHA256
  `CB379527EA162AA2E05B411B73289CB8395F27198B2290AAC97709D06FD4C58C`.
- Log `after.stderr.log` : `capture_verified`, sortie 0, aucune erreur runtime.
  PID 17208 terminé puis absence confirmée par inventaire des processus.

Les deux images ont été inspectées : les motifs larges et anguleux visibles
sous le pont deviennent des rides plus continues. Le lit brun et la zone
sombre sous le pont persistent. Ce gain limité justifie de conserver le
candidat technique, pas de qualifier le réalisme de l'eau. Une image fixe ne
prouve ni la continuité temporelle ni une orientation crédible du courant.

Cadence glissante affichée, non benchmark : témoin moyenne 10,37 ms / p95
16,54 ms / 1 % low 52,3 FPS ; candidat 10,23 ms / 17,11 ms / 49,0 FPS.
Ces relevés courts ne démontrent ni amélioration ni équivalence de performance.
Géométrie eau/berges affichée inchangée : 70 995 triangles.

## Vérifications et suite

36 tests ciblés `forge_engine water::` PASS, dont validation Naga du vrai
helper pour consommateurs compute et fragment. Build natif, Clippy moteur
all-targets/all-features avec warnings en erreurs et `cargo fmt --check` PASS.
Cela ne constitue pas une vérification numérique GPU de la dérivée.

Pas de reconstruction Studio à ce stade, pas de qualification WebGPU,
d'animation, de profils ou d'acceptation artistique. Aucun passage de gate,
commit ou publication. Les sources restent distinctes des anciens bundles.

Prochaine correction : faire correspondre l'orientation des rides au courant,
pas seulement déplacer du bruit isotrope. La référence conceptuelle
[Directional Flow](https://catlikecoding.com/unity/tutorials/flow/directional-flow/)
distingue ces deux effets ; elle n'est pas une preuve de leur implémentation
dans Forge. Ne pas reproduire une série de modifications de macro-vagues.

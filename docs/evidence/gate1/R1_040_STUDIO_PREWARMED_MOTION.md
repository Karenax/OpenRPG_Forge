# R1-040 — cycle Studio après préchauffage explicite

11 septembre 2026. **Préparation acquise ; cycle mobile FAILED.**
L'essai à froid précédent reste conservé, non remplacé par un PASS.

## Changement implémenté

Mode explicite `forge_quality_motion=prewarmed`, construit avec
`Build-StudioCandidate.ps1 -BridgeProbe -PrewarmedQualityMotionProbe`.
Le mode froid `forge_quality_motion=1` reste disponible et inchangé.
Le parcours attend le service produit `ProfileWarmup` : quatre niveaux puis
retour au niveau initial. Préparation absente, incomplète, failed ou cancelled
ne libère pas le départ. Après départ, les contrôles, collisions, longueur et
watchdogs du parcours sont inchangés. Aucun allongement de marche réalisé ici.
La préparation est exportée séparément des trois traces en mouvement.
Ce mode de preuve n'est pas activé lors d'une ouverture normale du Studio.

Test de départ renforcé : état absent/idle/warming/failed/cancelled/ready
incomplet refusés, ready 5/5 admis avec rendu prêt ; après départ aucune
suspension pour un changement de readiness. Test des deadlines existant conservé.
Deux combinaisons CLI invalides rejetées, entrée préchauffée validée en
`ValidateOnly` sans créer de sortie ni prétendre avoir exécuté l'ancien frontend.

Clippy strict workspace/all-targets/all-features PASS (25,60 s), Web PASS
(24,28 s), 704 tests PASS / deux sondes CPU ignorées. fmt et diff-check PASS.
Frontend release construit en 2 min 29 s puis bundle Tauri, avertissement futur
proc-macro-error2 conservé. 281 fichiers épinglés et revérifiés inchangés.

## Preuves exactes et reproduction

Racine externe :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-prewarmed-motion-20260911/`.
`Validate.ps1`, `Build.ps1`, `Run.ps1`, journaux natif/Web/tests/build,
`source-inputs.json`, `studio/build-proof.json`, `cleanup.json`.
HEAD Rust `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre modifié : pas une
révision propre publiée. Aucune carte, identité source ou asset retouché.

| Artefact | SHA-256 |
| --- | --- |
| EXE | `8ae897d54ecb4b04f3d3f5069d38aac6078915cb8c7af43cb87fecf1f5927da9` |
| WASM | `bfaeb17fb0afffd45db94dc478d562a96e47ed816435cc20a51a49088d48101f` |
| studio-motion.json | `3650ee72343205000415eae39ae0c694ee892287f729e57347692dcf8b513263` |
| studio-motion.png | `9466c7e9673ac2eae488354ab1d35b62972cbcd303769be3d5a4e20299ca73db` |

Exports réels par les boutons du Studio avec la compétence Windows :
`forge-diagnostic-1789113747621.json`, `forge-viewport-1789113757080.png`.
PNG 1408×966 relu : personnage, route, forêt et ciel affichés ; tramage encore
visible sur certains troncs, eau hors champ. Pas de nouvelle revue artistique.

## Résultat du cycle, pas simple résultat de build

Préparation **Ready 5/5 en 67,6749 s**, sans erreur : Low à 45,5962 s,
Medium 49,7664 s, High 56,0068 s, Ultra 61,9143 s, High 67,6749 s ;
129 puis 131 puis 134 pipelines. Ces temps incluent l'initialisation à froid.

Après préparation, High acquitté pendant la marche au tick 1817 et Low au tick
2062. Traversée completed, 518/518 ticks grounded, 434 contacts, 42,529579 m,
aucune erreur. Retour High prêt trop tard pour huit observations mobiles :
le cycle atteint son watchdog et conserve `FAILED: délai ou parcours`.
L'attente après la fin du parcours était celle de la sonde, pas un mouvement
encore actif ; une terminaison négative immédiate serait une amélioration du
protocole, pas une validation du cycle.

| Étape | Première observation (s) | CPU prêt (s) | GPU prêt (s) | Frames mobiles / observées | Pic d'intervalle (ms) |
| --- | ---: | ---: | ---: | ---: | ---: |
| High initial | 0 | 0 | 0 | 8 / 19 | 61,0 |
| Low | 0,8993 | 4,5942 | 4,9445 | 87 / 87 | 435,6 |
| High retour | 5,1551 | 9,4963 | 10,0595 | 73 / 3001 | 562,1 |

Origine : fin de préparation. Les 3001 frames comprennent l'attente à l'arrêt,
donc ne pas en déduire une moyenne ou un p95 de déplacement.

- Low : maximum préparation végétale 195,5 ms ; commit 7,1 ms ; forêt
  inclusive 320,2 ms, dont lots lointains 5,2 ms.
- High retour : préparation végétale 183,5 ms ; commit 7,3 ms ; forêt
  inclusive 321,0 ms, dont lots lointains 5,8 ms.
- Les pics 435,6/562,1 ms suivent bien les reprojections forestières.
- Les pauses de 5,9–7,8 s du cycle froid ne se reproduisent pas dans cette
  fenêtre après préparation. Une acquisition ne démontre ni leur élimination
  générale ni leur attribution exclusive à la compilation GPU.

## Décision et prochaine correction ciblée

Le préchauffage prépare effectivement les variantes, mais ne rend pas les
changements instantanés ou fluides. R1-040 et Gate 1 restent ouverts. Pas de
nouvelle dérogation, pas de Gate 2 ni de publication.

La trace justifie de traiter la préparation végétale (~0,19 s) et la forêt
inclusive (~0,32 s), pas de découper davantage le commit ~7 ms. Inspection du
projecteur : `prototype_projection::prepare` recalcule `MeshAabb::compute_aabb`
pour chaque primitive de chaque arbre, alors que beaucoup partagent les mêmes
meshes. Prochaine optimisation proposée : mémorisation **locale à cet appel**
des bornes par mesh, afin de conserver l'invalidation entre appels et les
retouches. Elle n'est pas implémentée ni mesurée par ce lot. Ne pas prétendre
que ces calculs expliquent déjà les 320 ms entières. Rejouer seulement après
correction/test pertinent ; ne pas répéter ce protocole inchangé.

Sortie normale 0 ; huit processus fermés, zéro survivant : 34304, 34268, 20156,
35496, 29512, 25824, 31552, 16660. Aucun navigateur utilisateur touché. Toutes
les modifications antérieures préservées, aucun asset Cadle introduit.

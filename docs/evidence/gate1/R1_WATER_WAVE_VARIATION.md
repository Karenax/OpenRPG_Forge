# Gate 1 — Variation spatiale des crêtes de rivière

8 septembre 2026, branche Rust ; candidat REJETÉ, changement de shader retiré,
aucune publication. Ne pas rejouer cette variante inchangée.

## Intention et implémentation candidate

Après correction du raccord de rive, le PNG Studio montre encore de longues
rides parallèles. Le shader partagé module spatialement les phases des deux
ondes avec une sinusoïde lente oblique au courant. Pondérations 0,55 / 0,45
au lieu de 0,72 / 0,28 ; somme 1 et amplitude du profil inchangée. Le gradient
inclut la dérivée de modulation, dans la fonction déjà partagée par sommets,
normales par fragment et positions précédentes du motion prepass.

Modulation indépendante du temps : période temporelle TAU conservée, aucune
horloge/seed/recette ou coordonnée spécifique à la vallée ajoutée. Pas de
texture ni dépendance nouvelle, mêmes triangles et passes. Coût supplémentaire :
une paire sin/cos par évaluation et arithmétique ; aucune promesse de coût GPU
identique ni de gain de performance. Ce n'est pas de l'hydrodynamique et les
crénelures de rive ne sont pas déclarées corrigées par ce changement.

Vérification mathématique indépendante en double précision (pas exécution WGSL) :
8 584 échantillons, amplitude 0,12, maximum observé 0,1199935862 ; erreur maximale
gradient analytique/différences centrales 4,45e-10 ; wrap TAU 2,53e-15. La borne
globale vient de |sin| <= 1 et des poids positifs de somme 1, pas du seul échantillon.

## Référence réelle avant modification

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-wave-variation-20260908`.
Premier essai `before` refusé : la caméra candidate change le hash WorldSketch
exigé par le kit natif, sortie 2, PID 35264 terminé ; journal conservé. Aucun
garde-fou assoupli. Le protocole utilise ensuite la caméra canonique
`hero_reference_match`, temps figé depuis Startup, même kit/forêt et profil.

`before-fixed.png` acquis et inspecté, sortie 0 sans ERROR, capture vérifiée,
PID 27072 terminé. PNG SHA-256
`6CCC5BB65CA7CC72EFFC6D4509A7374E3F25FA3D5372F3143E8A4E09F9F1EBBA` ;
EXE `D3E2EE6A5D99645399AE3518C4E55FCB59DE6F6269E3DD86D8CEACC38F0A530F`.
Vue complète de vallée : rides régulières visibles dans la rivière. Overlay
22,13 ms moyen / p95 28,76 ms / 1 % low 31,8 FPS, pas un benchmark.

`Verify.ps1` lancé : tests workspace, Clippy strict, check WASM et build natif
journalisés. L'ancien Studio ne contient pas le changement de vagues.

## Résultat et décision

Suite workspace 637 PASS / un timing préexistant ignoré, Clippy strict PASS,
check WASM PASS, build natif PASS
(2 min 16 s). Capture après, même caméra canonique et temps figé : sortie 0,
aucun ERROR, capture vérifiée, PID 26392 terminé. PNG `after-fixed.png` SHA-256
`418A9917DFCC0F8309097F2398A67D8005BD4D64C55B49917F9ABD01A5013E12` ;
EXE expérimental `D72CB7C622D68A3624B8957398FBFAFE2CEC5402EE8996B57D45AC900E2A6E31`.
Overlay 21,81 ms moyen / p95 27,75 ms / 1 % low 32,5 FPS ; pas un benchmark,
aucun gain revendiqué. Le changement de readiness statique du tour précédent
est aussi dans ce nouvel EXE, mais ne change pas le shader de la référence.

Inspection : crêtes courbées, mais toujours fortement périodiques et aspect de
tôle ondulée persistant. Aucun bénéfice artistique suffisant pour garder ce
surcoût. Variante rejetée, seul le patch WGSL de ce lot retiré avec patch inverse ;
travail préexistant préservé. Les binaires et PNG expérimentaux restent des preuves
locales rejetées, pas un candidat à promouvoir. Aucun rebuild Studio de cette
variante à faire. Prochaine piste : réponse des petites rides/reflets et échelle
des pentes, plutôt qu'une nouvelle modulation des mêmes grandes sinusoïdes.
Ni amélioration artistique ni Gate 1 revendiquées.

Shader restauré SHA-256
`12A4A84EF36945D124A2027B38EC01A770D98293F38414B1874347F6F4C9273E`.
Diff check PASS, trois PIDs d'essai absents après contrôle. Aucun processus de
capture de ce lot n'est laissé ouvert ; les captures rejetées ne sont pas supprimées.

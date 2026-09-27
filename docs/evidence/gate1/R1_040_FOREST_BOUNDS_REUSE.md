# R1-040 — bornes forestières réutilisées pendant la préparation

11 septembre 2026. **Optimisation implémentée, testée et gain CPU local mesuré.**
Gate 1 et qualification du cycle mobile restent ouvertes.

## Modification et garanties

`vegetation/prototype_projection/bounds.rs` calcule une AABB par mesh partagé,
au lieu de rescanner ses sommets pour chaque primitive de chaque arbre.
Le cache est local à `prepare` et emprunte les assets immuablement. Aucun cache
persistant ni heuristique d'invalidation : les retouches sont prises en compte
à la préparation suivante ; un mesh absent/sans positions reste une erreur.
Placements, meshes, marges de déformation, matériaux, LOD et collisions inchangés.
Correction générique du projecteur, pas retouche particulière de Visual Valley.

17 tests ciblés PASS, dont deux nouveaux (16620 lectures partagées avec bornes
exactes ; modification puis géométrie absente au passage suivant). Les tests
existants de changement de profil, réimport, récupération et collisions passent.
Clippy strict workspace/all-targets/all-features et Web PASS ; 706 tests globaux
PASS / deux sondes ignorées ; fmt et diff-check PASS. Aucun contrôle désactivé.

## Comparaison réelle avant/après

Baseline : `R1_040_STUDIO_PREWARMED_MOTION.md`. Même machine, viewport 1408×966,
URL préchauffée, kit, forêt batches, eau/nuages, longueur et limites du parcours.
Identités source des enregistrements et protocole JSON comparés : identiques.
Une paire de sessions, pas une série statistique de benchmarks FPS.

| Mesure (ms) | Avant Low | Après Low | Avant retour High | Après retour High |
| --- | ---: | ---: | ---: | ---: |
| Maximum CPU forêt inclusive | 320,2 | 24,8 | 321,0 | 24,0 |
| Dont lots lointains | 5,2 | 5,2 | 5,8 | 5,3 |
| Maximum préparation végétale | 195,5 | 175,8 | 183,5 | 183,4 |
| Maximum intervalle de frame de l'étape | 435,6 | 216,6 | 562,1 | 268,8 |

Environ **92 % de coût CPU forestier en moins dans cette paire**, sans modification
des données de carte. Ce n'est pas un gain de 92 % de FPS ni une qualification GPU.
L'intervalle associé à la projection Low passe à 139,7 ms ; son plus grand pic
restant, 216,6 ms, suit maintenant la préparation végétale de 175,8 ms.
High conserve 268,8 ms malgré forêt 24 ms : les phases tracées ne couvrent pas
tout le travail Bevy/navigateur/GPU, donc ne pas attribuer arbitrairement le reste.

Préparation Ready 5/5 en 62,2016 s, 134 pipelines à la fin. High et Low acquittés
en marche (ticks 1811/2043). Traversée completed, 518/518 ticks grounded,
434 contacts, 42,529579 m, aucune erreur. Retour High GPU prêt à 9,151 s après
préparation, contre 10,0595 s avant, mais encore trop tard pour huit observations
mobiles : **FAILED: délai ou parcours**, pas de fermeture de R1-040.
Les milliers de frames finales comprennent l'attente à l'arrêt ; pas de p95
de mouvement ou performance cible déduit de ce relevé.

Suite ciblée : préparation végétale avant les yields (~176–183 ms) et latence
du traitement par chunks. Le commit végétal reste ~7–8 ms. Pas de nouvelle
retouche artistique, prolongation silencieuse du parcours ou répétition identique.

## Preuves et nettoyage

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-bounds-20260911/`.
Scripts `Validate.ps1`, `WorkspaceTests.ps1`, `Build.ps1`, `Run.ps1`, journaux,
pins et `cleanup.json` conservés. Frontend release 2 min 25 s, shell 27,04 s.
Avertissement futur proc-macro-error2 conservé. 282 fichiers épinglés puis vérifiés.
HEAD Rust `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre modifié, non publié.

| Artefact | SHA-256 |
| --- | --- |
| EXE | `4633589323bd6e25c7c48e6cabc4b71b2e0c2d390dc685b0b3ee112193ff4593` |
| WASM | `41073cd89347f0e360a1e71c2e26a570290cd15b65834e9c0f43fa5158c77ba0` |
| studio-motion.json | `b31976a85ec64d55c388f4ac589fff0f1fe478064c68b8ed4bf30e347cef4529` |
| studio-motion.png | `5e4229f9af33af749641163d63b2e830de8e06d5a6fac7958ab7ff53f764b24a` |

Exports Windows réels : `forge-diagnostic-1789114855561.json` et
`forge-viewport-1789114864989.png`. PNG relu : forêt, route, personnage et ciel
présents, tramage des troncs encore visible, eau hors champ. Pas de qualification
artistique nouvelle. La compétence Windows a servi aux exports et à la fermeture.

Sortie normale 0, neuf processus possédés terminés : 17764, 33384, 27740, 30404,
31472, 30588, 28716, 35396, 4880. Aucun navigateur utilisateur fermé.
Pas de suppression, de commit/push, d'asset Cadle ou de nouvelle gate.

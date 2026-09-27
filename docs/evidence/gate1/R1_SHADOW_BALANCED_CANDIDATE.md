# R1 — Candidat d'ombres 2048 / trois cascades, 8 septembre 2026

Actualisation 9 septembre : comparaison **en marche vivante** désormais acquise,
quatre passages ABBA sur le même EXE/kit, trajectoires contrôlées. Dernier tiers
forestier ~32 → 40 FPS, encore sous la cible ; voir
`R1_LIVE_WALK_SHADOW_COMPARISON.md`. Le refus stationnaire ci-dessous reste valide,
le nouveau protocole ne l'assouplit pas. Pas de promotion artistique ou de gate.

## Actualisation — comparaison vivante refusée le 9 septembre

Le candidat 2048 / trois cascades existe mais n'est toujours pas intégré comme
preset livré. La preuve historique ci-dessous reste une isolation figée et une
capture, pas une comparaison du jeu vivant. Pas de nouveau réglage tenté.

Une paire actuelle a été préparée avec le même EXE
`3ED00CED70A722941ADECEFD95508A7E866125E4760B0D27706FCC44D8E48E10`,
le kit colorimétrique courant (1662 arbres), les sept domaines High demandés
séparément, VSync demandée désactivée et le parcours `1800:1:0.22`.
`--walk-proof-measure` devait mesurer 300 frames après 120 frames de stabilisation,
simulation vivante, sans capture. Diff récursif des profils contrôlé : seules
`presets.high.shadows.map_resolution` (4096 → 2048) et `cascade_count` (4 → 3)
diffèrent. Aucun profil source réécrit.

**Le témoin a échoué**, sortie 1 : `walk_measurement_moved`, 255 échantillons,
origine (-132,49286 ; 2,5967288 ; -203,52524), pose observée
(-132,4842 ; 2,591207 ; -203,54242), vitesse horizontale commandée/rapportée nulle.
Le déplacement dépasse la tolérance stationnaire de 0,02 m ; le garde est resté
inchangé. Parcours terminé avant mesure et personnage initialement au sol.
Pas de rapport complet, pas de moyenne extrapolée des 255 frames, pas de gain
calculé. La variante équilibrée **n'a pas été exécutée** après cet échec.

Racine locale :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-shadow-live-pair-20260909/`.
`Run.ps1`, `baseline.stderr.jsonl`, `baseline.stdout.log` conservés.
PID 32348 arrêté normalement après refus ; absence du processus et de ses
descendants vérifiée. Aucun navigateur ou build lancé.

Conséquence : ce protocole stationnaire vivant n'est pas exploitable sur ce point
de terrain sans violer son invariant. Ne pas déplacer artificiellement le joueur,
modifier la physique, élargir la tolérance, sélectionner un essai chanceux ou
relancer les anciennes isolations figées pour les présenter comme une mesure live.
Une future comparaison du jeu vivant doit mesurer un intervalle et des commandes
comparables en déclarant les poses réelles, ou un emplacement stable justifié.
Ce refus ne prouve ni une régression du contrôleur ni un gain/perte d'ombres.
Le coût perceptible du parcours précédent demeure ; les preuves antérieures
motivent le candidat, pas sa promotion ni la clôture Gate 1.

## Résultat

Candidat externe construit et **capturé après parcours réel** dans le sous-bois.
Atlas 2048, trois cascades, portée 128 m et contact shadows conservés, autres
domaines High. Les observations effectives du ledger concordent : un soleil,
bornes 24 / 55,425625 / 128 m, contact shadows présents sur lumière et caméra,
16 pas. Profil hash typé `a79aceb7b8f68afa5d9376ceb1c1ffff10ef26c40ccb14a1619d66db824d23e3`.

PNG inspecté avec le témoin historique `r1-walk-proof-20260908/west3.png` : même
composition de sous-bois, capsule bleue devant de grands troncs rapprochés,
canopée présente, ombres de contact et ombres portées lisibles au sol. Les contours
d'ombre diffèrent ; ne pas affirmer une parité pixel ou l'absence de perte de détail.
Les troncs et la canopée restent trop sombres ; cette dette artistique n'est pas
corrigée par le budget. Aucun jugement humain acquis.

Overlay de la capture : 21,8 FPS instantanés ; fenêtre glissante 240 frames,
moyenne 46,15 ms, p95 47,79 ms, 1 % low 19,5 FPS. Témoin historique : 13,4 FPS
instantanés, moyenne 75,30 ms, p95 76,73 ms, 1 % low 12,4. **Pas un benchmark A/B** :
binaires différents, animations vivantes, phase temporelle non normalisée, fenêtres
glissantes ; le ledger global contient déplacement et capture. Ne pas utiliser sa
moyenne globale comme mesure stationnaire ou qualification 60 FPS. VSync demandée
active pour la capture, contrairement aux mesures statiques précédentes.

Le candidat **n'est pas le preset livré** : aucun changement du profil canonique,
de la carte ou du Studio. La prochaine intégration doit préserver cette distinction,
vérifier le chemin partagé et ne pas remplacer l'acceptation visuelle/performance
de Gate 1 par un budget plus facile à rendre. Aucun nouveau test des paramètres
écartés n'est justifié sans changement de charge ou d'hypothèse.

## Deux derniers contrôles de paramètres

Même binaire `D01FB456…` que `R1_UNDERSTORY_CPU_PHASES.md`, mêmes 300 frames
statiques Full HD et mêmes sources. Racine `r1-shadow-resolution-20260908`.
Le contrôle JSON des profils normalise 128 et 128.0 comme le même nombre ; après
restauration du seul paramètre testé, aucune autre différence sémantique.

| Variante, contre le témoin 13,000 FPS | FPS moyens | p95 ms | 1 % low FPS |
| --- | ---: | ---: | ---: |
| Contact shadows seuls désactivés, 4096 / 4 cascades / 128 m | 13,285 | 76,867 | 12,541 |
| Trois cascades seules, 4096 / 128 m / contact conservé | 15,716 | 65,686 | 14,722 |

Projection effective validée dans chaque rapport. Contact désactivé sur lumière
et caméra, nombre de pas absent ; trois cascades 24 / 55,425625 / 128 m dans le
second. PIDs 8172 et 38228 terminés, sorties 0, aucun ERROR Runtime. Différences
de pose < 5 mm ; autres limites de ces fenêtres exploratoires inchangées.
Décision : conserver les contact shadows, leur suppression n'explique pas le coût
majeur ; résolution et nombre de cascades sont les leviers retenus pour le candidat.
La projection de végétation réserve déjà les ombres au LOD proche (`NotShadowCaster`
sur les autres), vérifié dans le code ; aucun correctif redondant ajouté.

| Rapport | SHA-256 |
| --- | --- |
| contact-off.json | `A41FA4D1A981712BDE196BB396F0D725B0EAB9B01AE3BF1A79DD5B8EA169F679` |
| cascades-3.json | `CA0BC622D7BEE79993533E4BD0EBA24894FDDA1AC2DA4B687323D49980B38AF2` |

## Production et vérification du candidat

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-shadow-candidate-20260908`.
`Capture.ps1` vérifie les pins du binaire, kit et profil source, génère un profil
externe neuf en ne changeant que résolution et nombre de cascades, refuse les
sorties existantes et borne son processus à 110 s. `--walk-proof 1800:1:0.22`,
capture frame 1700, sortie frame 1800. Marche terminée avant `capture_verified`,
position de fin (-132,4948 ; 2,5985713 ; -203,51906), pas de téléportation.

Harnais natif étendu pour autoriser le profil externe en capture de parcours
bornée, et pas seulement en mesure. Mesure et capture simultanées restent refusées.
**82 tests Runtime + une intégration PASS**, Clippy all-targets strict, build natif,
fmt et diff-check PASS. Pas de rebuild des domaines partagés ni du Studio pour ce
changement de validation native. PID 9152 terminé, sortie 0 et absence de processus
depuis la racine vérifiée ; capture/ledger/profil conservés, aucune publication.

| Artefact | SHA-256 |
| --- | --- |
| forge_runtime.exe | `17F8ED4117B09E495F471B6D997CEE2A512E5B71152D58A7E6D546C581A6C3C6` |
| candidate.png | `8FE44F580D8CD322056D6BAAB44C1F70BD4717EF8E5B64D6990D6CF99B7437CA` |
| candidate.metrics.json | `2E496D3E3EE175A2AFF87C9382AEC247610D7F89A19C8C51EFECFBD1BBCBBF61` |
| candidate.profile.json | `484DC144D28EA6B133DBFCC7C2B5F2368B2A93A89DB7A19B1F9B6658327169EA` |

Gate 1 ouverte : candidat non qualifié artistiquement, trop lent pour la cible,
pas de parité Studio ou de qualification RTX 3060/RX 6600 déduite.

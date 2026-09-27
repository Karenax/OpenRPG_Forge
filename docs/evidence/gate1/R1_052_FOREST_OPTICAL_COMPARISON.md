# R1-052 — comparaison forestière descriptive, FOV aligné

11 septembre 2026. Acquisition réalisée ; qualification comparative **PARTIAL**.
Ni amélioration du rendu, ni passage de Gate 1, ni publication.

## Protocole et identité

Cadle canonique `ea0da62549980816a6a27e08b75808b1f56d70e7`, arbre
`f0d52e434454fe302052d22eafde203bc361cc1f`, vérifié intact avant/après.
Clone jetable `artifacts/cadle-gate-c/qualification-script-c` : seul changement
suivi `package-lock.json`, overlay SHA-256
`85f9b294da3d67fc698631b4b9903ad9a33214044573db82ed86e4b09d2f1c1f`.
Harnais upstream relu, contrôle du serveur conservé, sortie neuve bornée.
Aucun fichier source ni asset Cadle modifié ou copié dans Forge.

Scripts et suivi des processus :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-cadle-fov60-20260911/`.
Les résultats et l'image restent dans le clone ignoré :
`tools/out/r1-forest-vfov60-20260911/`.

- Rapport SHA-256 : `2eb520ee5b24b46bdd5b18f89f093f133eab074e9c6a41bffbb68ecfb7474169`.
- PNG SHA-256 : `f6371589da8da30a4c45e538eada81139d94b1c3ac69e98d456dda23f2a3a716`.
- Forge : acquisition native vivante de `R1_IDLE_SLOPE_STABILITY.md`, runtime
  `206b647eae5aa286b47156bef2ace00c2833f1b44918c6ebc79a80e3e4440484`.
  Capture séparée inspectée à nouveau, pas de relancement natif inutile.

Réglage Cadle en mémoire uniquement : FOV horizontal de base 91,49284452°,
calculé par `2 atan(tan(60° / 2) × 16/9)`. La caméra applique effectivement
60,004166° verticaux ; tolérance contrôlée 0,02°, ratio 16/9.
Pitch demandé -0,2094395161 rad ; position au sol (63,5 ; 27,53 ; -768,3),
caméra à Y 29,1797. Simulation non suspendue, personnage idle grounded,
ennemis passifs et invulnérabilité d'inspection, heure 13 fixe. Après 31 s
initiales et 5 s de stabilisation, deux fenêtres de 10 s puis une seule capture.

## Coûts observés — pas un ratio de vitesse entre moteurs

Même RX 7800 XT, 1920×1080 et label High. Cadle utilise ANGLE/D3D11 ;
Forge utilise Vulkan. Les budgets effectifs, scènes et caméras ne sont pas
équivalents : première personne Cadle, troisième personne Forge (bras 4,5 m),
2 km² contre 512×512 m, effets et contenu différents.

| Observation | Forge natif antérieur | Cadle fenêtre 1 | Cadle fenêtre 2 |
| --- | ---: | ---: | ---: |
| FPS moyens | 17,8119 | 108,8 | 108,6 |
| Frame moyenne ms | 56,1424 | 9,19 | 9,21 |
| p95 ms | 57,554 | 41,8 | 39,0 |
| CPU moyen ms | phases séparées, pas total comparable | 8,94 | 8,94 |
| GPU moyen ms | non acquis simultanément | 9,16 | 9,13 |
| Draws dernière frame Cadle | non comparable | 278 | 192 |
| Triangles dernière frame Cadle | non comparable | 4 911 746 | 3 415 666 |

Forge : 300 frames sur 16,8427 s, 1 % low 16,0782 FPS. Cadle : tampon des
600 dernières frames, pas toutes les frames des dix secondes. Les séries brutes
lues juste après chaque statistique contiennent 600 valeurs positives ; leurs
1 % low dérivés sont environ 15,08 et 14,55 FPS (1000 / moyenne des six frames
les plus lentes). Lecture non atomique avec les statistiques arrondies du tableau :
ne pas fusionner ces séries en une fenêtre unique ni conclure à la fluidité.
Les compteurs de géométrie sont des instantanés, pas des moyennes de fenêtre.

## Inspection visuelle et décision bornée

Les deux captures montrent maintenant l'intérieur d'une forêt, avec un FOV
vertical proche. Cadle présente un couvert bas plus continu, davantage de détail
sur les troncs et une lumière volumétrique plus structurée. Forge conserve des
troncs anguleux, un sol plus découvert et un sous-bois plus simple. La différence
ne provenait donc pas uniquement de l'ancienne comparaison intérieur/lisière.
Ce constat est descriptif : hauteur, orientation locale, éclairage et géométrie
ne sont pas identiques. Il ne constitue pas une revue humaine ou une obligation
de retoucher immédiatement la carte déjà acceptée par le propriétaire.

**Résultat utile acquis** : optique documentée, coût contemporain Cadle et coût
Forge vivant disponibles, comparaison intérieure inspectée. **Non acquis** :
benchmark à budgets équivalents, supériorité visuelle, jugement humain final.
R1-052 reste ouvert sur ces exigences ; l'acquisition n'est plus entièrement
NOT RUN. Ne pas relancer ces mêmes fenêtres sans changement de protocole utile.
Les dérogations performance et GPU Studio restent inchangées.

Sortie 0, zéro erreur globale ou de step, huit PID possédés fermés : 27352,
18508, 6028, 32632, 6196, 31932, 29124, 34852. `cleanup.json` indique aucun
survivant ; port 14640 libéré. Aucun build produit ni onglet utilisateur touché.

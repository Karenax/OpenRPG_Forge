# Tidewater — correctif007 et investigation du sol, 8 octobre2026

Le bouton **Quitter le jeu** est ajouté au menu Échap, sous Reprendre. Il envoie AppExit::Success. La preuve finale menu002 appuie sur le bouton visible et laisse cette action fermer le vrai processus natif (code0), sans demander sa fermeture au pilote. Réglages graphiques, navigation Échap et Reprendre restent couverts par le même replay.

**Le sol noir ou disparu signalé reste ouvert. Aucune correction de rendu du terrain n'est affirmée.** La disparition n'a pas été observée dans les scénarios ci-dessous. Les diagnostics ne changent le départ, les réglages ou l'heure que sous des variables explicites de preuve ; le launcher les retire au lancement normal.

## Construction et installation

Le candidat final est example004, SHA256 `287d9eec8991a6bc3e14a069fafa1e440b246dc4ee10d60e5ae80b328415bb1e`. Le moteur compilé006 et les dépendances du SDK fermé ont été réutilisés avec vérification SHA256, sans remplacer les travaux concurrents de Studio. Les fichiers concernés sont comparés à leur baseline avant copie vers les sources principales. Le launcher reste à son SHA256006 `5c8e7c919893d28f4e40a0ca536b74316d32848d73b88b31653962e33b372e5b`.

Rustfmt passe ; Clippy example-check002 retourne0, avec23 diagnostics préexistants dans d'autres fichiers et aucun sur les fichiers modifiés. Le contrôle n'est pas une qualification de tout le workspace ou de la CI. La copie de secours de l'exécutable006 et des préférences se trouve dans backups. L'installation et les hashes réels sont consignés dans deployment.json.

## Investigation native

Les copies de graphics.json et display.json correspondent au profil utilisateur : heure09:30, flou et TAA désactivés, contraste1.21, exposition+1EV. Les profils réels restent inchangés. Première séquence en caméra de smoke1920×1080 ; suivantes avec `--walk`, première personne et plein écran2560×1440, mêmes paquets et shaders que le launcher.

001 : déplacement et rotation en caméra smoke. 002 : première personne. 003 : rotations rapides, fortes inclinaisons et captures consécutives. 004 : mêmes mouvements avec heures08:00/12:00/18:00/09:30. 005 : activation puis désactivation TAA, FXAA, adaptation, occlusion ambiante, brume et capture de réfraction en cours de déplacement. 006 : départ diagnostique dans la végétation intérieure (-60,-190), fortes inclinaisons et mouvements.

| Séquence | Images caméra mobile | Captures natives | Maillage / matériau absent côté GPU |
| --- | --- | --- | --- |
| 001 | 221 | 30 | 0 / 0 |
| 002 | 275 | 30 | 0 / 0 |
| 003 | 221 | 82 | 0 / 0 |
| 004 | 221 | 82 | 0 / 0 |
| 005 | 221 | 82 | 0 / 0 |
| 006 | 275 | 82 | 0 / 0 |

Le tampon GPU de lumière garde une seule identité dans les séquences004–006. L'hypothèse d'une recréation répétée n'est donc pas soutenue par ces mesures. La présence du maillage et du matériau préparés ne prouve pas, à elle seule, qu'aucun pixel ne clignote. Des captures sélectionnées et des planches de contact ont été inspectées ; les statistiques de pixels sont des aides de diagnostic, pas un verdict automatique d'acceptation du sol. Le `pass` des receipts terrain signifie que l'exercice natif a terminé avec mouvement ; il ne clôture pas le bug rapporté.

Chaque processus de replay a terminé, sans dépassement des protections de mémoire possédée et sans laisser de descendant actif. Aucun seuil de RAM disponible n'est réintroduit. Les journaux ne contiennent pas d'erreur de rendu ; les avertissements GPU readback de canal fermé sont émis après les dernières captures lors de la fermeture. Pics mesurés dans diagnostic-summary.json.

## Suite nécessaire pour le sol

Il manque un scénario qui reproduit la panne : endroit de Tidewater, mouvement ou changement de réglage immédiatement précédent, et idéalement une image du défaut. Sans cela, modifier les shaders ou le streaming constituerait une correction hypothétique. La demande de précision est laissée à l'utilisateur ; le bouton Quitter est livré séparément et testé.

La validation humaine de Gate3 précédemment donnée concerne la version jouée à ce moment-là. Aucun nouveau verdict humain, résultat CI ou clôture Gate3 n'est déduit ici. Aucun commit ni push n'a été effectué.

Dossier local complet : `E:\OpenRPG_Forge\.tidewater-integration\launcher-repair-20261008-007`.

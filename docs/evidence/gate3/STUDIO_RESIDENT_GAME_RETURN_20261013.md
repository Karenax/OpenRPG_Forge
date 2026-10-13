# Studio — retour du jeu sur la carte conservée, 13 octobre 2026

Demande propriétaire : garder la carte chargée pendant le jeu natif séparé,
puis retrouver l'éditeur sans tout recharger. Le propriétaire confirme en
parallèle que `E:/OpenRPG_Forge/Lancer-Editeur.cmd` fonctionne et demande de ne
pas le modifier. Ce lanceur et le profil local ne sont pas modifiés.

Les sources communes remplacent les deux navigations `location.replace` du
parcours jeu/éditeur par un état de pause en mémoire. Le navigateur conserve
les callbacks de rendu sans les exécuter pendant la partie ; il reprend ces
mêmes callbacks quand la pause est levée. Le monde Bevy, le canvas, la caméra,
les ressources GPU et la session auteur restent présents. La mémoire de ces
ressources n'est donc pas libérée pendant le jeu. Les outils auteur sont
inertes pendant la pause. La fermeture normale du jeu réactive la carte ;
une erreur du processus est affichée avec un bouton de retour.

Les huit tests JavaScript passent : contrat RAF, cadence actif/repos,
chargement, conservation/reprise des callbacks pendant la partie et annulation
pendant la pause. Le frontend release optimisé et le build natif passent avec
la même empreinte des 2549 entrées avant et après compilation :
`a5ba7b3c2680c88ddb075fdfa0d5bc02702138dbd35affaf29702c651252ccd5`.
L'EXE courant a pour SHA-256
`4dd80264dbcfd11a590f831b14fe040fce6b67b2d310cc988b0ef38555aa242e`.
Le lanceur canonique inchangé ouvre cet EXE (PID 17708, naissance
`2026-10-13T11:45:11.4905040Z`) depuis un autre dossier de travail ; le module
Tidewater est disponible. Le bouton lance le runtime natif commun (PID 37176,
parent 17708) sur l'archive de la carte actuelle. Le panneau de pause indique
que la carte et la caméra restent chargées ; aucun changement de page n'est
effectué. Le propriétaire ferme le jeu puis confirme « oui c'est bon » pour
le retour. L'observation UI finale est interrompue par son appui sur Échap ;
aucune nouvelle action de contrôle d'application n'est effectuée ensuite.
Le verdict de retour repose sur cette confirmation propriétaire, le panneau
de pause observé, les tests de conservation des callbacks et les sources.
Les retouches récentes du propriétaire (32 objets observés) sont
préservées ; les anciennes preuves à 31 objets restent historiques.

L'erreur initiale de syntaxe d'attribut est corrigée ; son log reste conservé.
Le premier assemblage natif refuse le remplacement de l'EXE encore ouvert :
la confirmation de fermeture n'avait pas abouti. L'automatisation refuse ensuite
« Quitter sans enregistrer » par crainte de perte de brouillons. La fermeture
par « Enregistrer et quitter » aboutit ; les 88 fichiers auteur, le profil et
le lanceur sont identiques aux hashes précédents. Le build repris passe.
Les refus et échecs initiaux ne sont pas réécrits en succès. Les avertissements
WASM historiques subsistent ; aucun Clippy WASM global PASS n'est revendiqué.

Le contrôle final confirme les 88 fichiers auteur, le lanceur et le profil
inchangés, ainsi que les 2549 entrées de compilation identiques au build.
Cette acceptation concerne le retour jeu/éditeur, pas la revue humaine P05
d'une partie RPG complète. Le propriétaire autorise ensuite la mise à jour
documentaire, le commit et le push du lot Studio sur `Rust` ; voir
[le périmètre de publication](STUDIO_PUBLICATION_20261013.md).

Sources : `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`.
Preuves : `E:/OpenRPG_Forge/.gate3-evidence/20261013/studio-resident-game-launch-001`.
Gate 3 reste ouverte ; le runtime natif reste une promenade et la partie RPG
complète n'est pas qualifiée par ce changement.

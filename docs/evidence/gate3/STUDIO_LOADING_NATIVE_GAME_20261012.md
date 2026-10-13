# Studio — chargement, mémoire et jeu séparé, actualisé le 13 octobre 2026

Ce rapport conserve les mesures du Studio avant la demande de garder la carte
chargée pendant le jeu. Le [retour sans rechargement](STUDIO_RESIDENT_GAME_RETURN_20261013.md)
identifie l'EXE commun suivant et ses validations ; les mesures ci-dessous ne
sont pas transférées automatiquement à ce nouvel EXE.

Le chantier reste dans `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`. Le seul Studio
commun demeure `E:/OpenRPG_Forge/.gate3-ntfs-cargo-20261002/release/forge-studio.exe`,
accessible par `E:/OpenRPG_Forge/Lancer-Editeur.cmd`. Aucun autre checkout ou
frontend actif n'est créé. Les preuves sont dans
`E:/OpenRPG_Forge/.gate3-evidence/20261012/studio-loading-native-game-001`.

Identité du Studio lors des mesures ci-dessous : SHA-256
`253d82dcd371535d6e40fbce924683fa9566be47790660e31c0ac0d29054a4c7` ;
empreinte des 2549 entrées de compilation
`82931937aaddff9d6a235289f1d591b0c0315d7a977b5580bac5997b57040b04`.

## Comportement

Le chargement de carte montre une icône animée et sa phase. Il se termine après
la projection de la carte, les collisions et les placements demandés ; une
erreur de préparation ferme l'attente et conserve le message d'erreur.
Le parcours de sélection reste inchangé : une retouche prévisualisée doit être
appliquée ou abandonnée avant de sélectionner un autre objet. La barre d'actions
explique cette attente. Il n'y a ni application ni sauvegarde implicite.

La vue d'édition utilise la lumière acquise et une eau temporaire simple. Elle
ne calcule pas le bootstrap atmosphère/nuages ni les programmes animés
d'environnement. Ces calculs restent dans le runtime natif existant. Les
géométries, textures originales et collisions auteur sont conservées. Les
recettes et hashes déjà validés sont réutilisés entre images plutôt que recalculés
à chaque image. Une fin de travail GPU réveille la prochaine image de Bevy ;
les contrôles HTML continuent de fonctionner pendant cette attente.

Les matériaux de poissons et leurs récepteurs de lumière sont modifiés
uniquement lorsque leurs valeurs changent. La vue d'édition fige leur horloge,
en conservant les changements de LOD lors des déplacements de caméra ; le jeu
natif conserve son animation. Le navigateur groupe les callbacks
`requestAnimationFrame` avec un intervalle minimal de 1/60 seconde uniquement
sur la page d'édition. Les fences et la file WebGPU gardent leurs sémantiques.
La scène fixe est recalculée au plus deux fois par seconde au repos. Toute
interaction clavier, pointeur, molette ou contrôle HTML rétablit immédiatement
la cadence active pour deux secondes ; l'icône de préparation de carte maintient
aussi cette cadence. Le chargement et les mouvements de caméra ne sont donc
pas soumis au rythme de repos. Les animations CSS de l'interface restent actives.

« Lancer le jeu » appelle uniquement une commande native typée avec le hash de
la carte actuelle. Les chemins et arguments du processus proviennent du profil
local, jamais du navigateur. Le backend exporte une archive immuable comprenant
les retouches appliquées non sauvegardées, les sources vérifiées et le départ
auteur. Le runtime vérifie l'archive et ses blobs sans écrire dans le workspace.
Une seule fenêtre de jeu peut être ouverte. Le Studio recharge son interface
avec le canvas en pause pour libérer le contexte GPU ; « Revenir à la carte »
devient disponible après fermeture du jeu. Le test intégré reste retiré.

## Vérifications et limites

Le runtime natif `tidewater_plant_probe.exe` est compilé en release depuis le
checkout commun, SHA-256
`06185bae6fa9b49bea9a75eb6f1787617150113eaffcb88c97511b1221fc359f`.
Les 257 tests natifs Studio passent, 4 restent ignorés, ainsi que Clippy natif
strict. Les 14 tests du binaire repassent après raccordement des deux commandes
au manifeste de droits. Le test d'export vérifie une carte modifiée non
sauvegardée et l'absence de mutation de la session ou des fichiers auteur.

La compilation WASM passe. Le Clippy WASM strict échoue sur les avertissements
historiques ; le contrôle qui isole trois catégories d'avertissements ordinaires
signale encore 58 erreurs Clippy dans des composants existants. Les 8 erreurs
initialement trouvées dans les nouveaux composants sont corrigées. Ce contrôle
n'est pas présenté comme un Clippy WASM global PASS.

Le test moteur vérifie l'absence d'événement de modification sur une vue figée,
les modifications nécessaires après déplacement de caméra et l'horloge du jeu
natif : PASS. Clippy moteur natif strict, bibliothèque et tests : PASS.
Les six tests JavaScript vérifient le groupement des callbacks, l'annulation,
les erreurs, l'absence de modification des pages hors édition, la reprise après
interaction et le maintien de la cadence pendant le chargement : PASS.

La compilation Rust WASM et l'optimisation du frontend sont terminées, mais
la commande Trunk initiale échoue sur son hook PowerShell. Le hook exact est
ensuite exécuté avec le bon PATH ; les 3111 assets générés sont vérifiés par
hash avant promotion dans le seul `dist` commun. Le dernier changement
JavaScript conserve ce WASM optimisé à l'identique. Le build natif Studio passe
avec la même empreinte de sources avant et après compilation. Le premier échec
Trunk reste conservé et n'est pas déclaré PASS.

La première itération confirme l'icône et le bouton mais ne corrige pas la
mémoire : la consommation GPU monte de 3,6 à 10,2 Go pendant la préparation,
après une baseline à 14,3 Go dédiés. Voir `FIRST_ITERATION.md`. L'itération qui
fige les matériaux ramène la RAM à environ 3,7 Go, mais laisse environ 8,7 Go GPU
et 140 mises à jour/s. Une tentative de retard de promise GPU ne limite pas cette
cadence et est remplacée par le groupement des demandes d'images du navigateur.
Ces échecs restent conservés dans les traces d'itération.

L'essai à cadence constante (PID 2564, naissance `2026-10-13T08:40:16.4919922Z`) ouvre
Tidewater avec l'icône de chargement, puis affiche les 31 objets. Les premiers
relevés Windows de l'ensemble Studio/WebView donnent 3,17 puis 3,08 Go de RAM,
1,93 puis 2,26 Go GPU dédiés, puis 3,11 Go RAM et 2,61 Go GPU après navigation.
Après plus de cinq minutes, la mémoire GPU atteint 4,51 Go : ce relevé ne prouve
pas une stabilisation. Voir `FOURTH_ITERATION.md`. La cadence observée est proche
de 48 mises à jour/s, la file GPU revient à zéro,
et les matériaux inchangés ne produisent plus de modifications répétées.
Il s'agit de mesures locales sur cette carte et cette session ; les tailles
d'allocations demandées et la résidence physique Windows sont distinctes.

Le Studio actuel (PID 13244, naissance `2026-10-13T08:54:25.1748482Z`) ajoute
le rythme de repos. L'icône de chargement et les 31 objets sont observés. La vue
fixe revient à 2,0 mises à jour/s ; après un clic réel de navigation, la fenêtre
de mesure qui couvre la reprise affiche 34,3 mises à jour/s, puis revient au
repos. Les premiers relevés donnent 3,02 puis 3,06 Go RAM et 1,60 puis 1,92 Go
GPU dédiés. Le dernier relevé donne 3,05 Go RAM et 1,94 Go GPU dédiés.
Les 69 fichiers auteur sont inchangés ; l'empreinte des 2549 entrées de
compilation est identique au build. Les relevés complets et leurs dates sont
dans `FINAL_RESULTS_20261013.json`.
Ces relevés prouvent l'amélioration sur ce parcours court ; ils ne qualifient
pas la résidence GPU après plusieurs heures d'édition continue. Le Studio est
laissé ouvert pour le propriétaire sur la même carte, sans retouche de test.

Le lancement réel par le bouton a été observé dans une fenêtre native séparée
sur le même raccord backend. Le runtime natif actuel passe ensuite son parcours
automatique `--walk-smoke`, sur une copie de données de l'archive exacte exportée
par le bouton : exit0, terrain et sources vérifiés, végétation complète et backend
stable. Le parcours mesure 68,37 images de boucle/s en moyenne et 32,01 au
percentile bas de 1 %, sur 241 intervalles après démarrage à 1920×1080. Ce sont
des intervalles de boucle, sans timestamps GPU ni qualification générale.
Preuves : `native-smoke-20261013/game.walk.json` et `game.png`.

Les compteurs de mises à jour ne sont pas des FPS présentés ; les tailles des
assets CPU ne sont pas la résidence GPU Windows. Le runtime natif reste une
promenade. Dialogue, quête, combat et sauvegarde RPG ne sont pas raccordés par
ce chantier. Aucune acceptation humaine P05, CI distante ou clôture Gate 3 n'est
déduite des builds et tests : **Gate 3 reste ouverte**.

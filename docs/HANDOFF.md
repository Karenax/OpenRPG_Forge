# Reprendre OpenRPG Forge hors Codex

**Gate 9 planifiée, non ouverte — 7 novembre.** Tidewater doit être un jeu créé
avec Studio, reposant sur des capacités génériques de l'éditeur et du moteur,
reproductibles dans d'autres jeux. L'inventaire et les critères R9-01 à R9-06
figurent dans la [roadmap](planning/MIGRATION_ROADMAP.md#gate-9--tidewater-reproductible-par-les-capacités-communes).
Cette décision ne lance pas la gate et ne change pas les verdicts de Gate 7.

<a id="gate-7-etat-courant"></a>

## Gate 7 — état courant au 18 novembre 2026

**Gate 7 est clôturée localement le 18 novembre 2026 par décision du propriétaire.**
Le périmètre Windows s’appuie sur les validations datées et le candidat 063.
Linux et le rejeu du 063 sur Windows propre sont reportés. Le Hub a été retiré
au lot 048. Le [rapport de clôture](evidence/gate7/GATE7_CLOSURE_20261118.md)
rapproche les dix critères et les preuves conservées.

Le [candidat Windows 062](#gate-7-windows-062) contient le moteur du comparatif
Tidewater à 1080p qui est passé de 18 à 3 intervalles de plus de 33,33 ms.
Sur l’hôte, le Studio mis à jour exporte Le Grand Plateau. Le jeu installé
relit la sauvegarde du 061 et la conserve à la désinstallation. Les trois
paquets ont passé la relecture des licences. La preuve Windows propre
concerne le 060.

Après le 062, le [tri de navigation sur le travailleur](#gate-7-tidewater-navigation-tri-worker)
fait passer de trois à zéro le nombre d’intervalles de plus de 33,33 ms
sur le trajet de référence à 1080p. Le [kit Windows 063](#gate-7-windows-063)
embarque le binaire exact du comparatif. Sur l’hôte, le Studio 063 exporte
Le Grand Plateau, et le jeu installé reprend puis préserve la sauvegarde
061 à la désinstallation. La preuve sur Windows propre couvre le 060.

La relecture Windows vérifie 60 fichiers. Pour le 049 privé, elle retrouve
les 43 pièces épinglées de construction, de maintenance et de machine
propre. Pour le 059, elle relie le Setup, les notices, l'export du Grand
Plateau et les deux sessions joueur par SHA-256. Les invités auteur et
joueur partagent les mêmes trois fichiers exportés. Les deux sessions
quittent avec la projection prête. La sauvegarde conserve la même
empreinte après la reprise puis la désinstallation graphique.
Le 059 est qualifié pour ce parcours avec WebView2 Fixed Version géré
dans les invités.
Les mises à jour et retours arrière du 049 restent rattachés au 049.
Le Studio 059 reste privé et le jeu du Grand Plateau est préparé pour
redistribution. Le [Setup Studio 060](#gate-7-setup-studio-060) contient
le premier paquet auteur Windows revu pour redistribution locale, avec
WebView2 et le CRT en prérequis externes. Le [rejeu exact 060](#gate-7-studio-060-fixed-windows-propre)
valide l’installation et l’ouverture du Studio sous Windows propre avec
WebView2 Fixed Version géré. Le [rejeu auteur 060](#gate-7-studio-060-export-grand-plateau)
produit ensuite l’export redistribuable du Grand Plateau. Le
[rejeu joueur 060](#gate-7-joueur-060-grand-plateau) installe cet export,
démarre et reprend une partie, puis retire le jeu en conservant la
sauvegarde. Le [Setup 060 en mode standard](#gate-7-studio-060-evergreen-ltsc)
s’installe, vérifie son paquet et ouvre le Studio sur Windows 11 LTSC
sans SDK avec Evergreen enregistré. Le [rejeu auteur sur l’hôte](#gate-7-studio-060-evergreen-hote)
produit sous Evergreen les quatre fichiers exacts du parcours auteur
Fixed Version. La parité de performances Tidewater reste ouverte.
Le [kit joueur Windows 061](#gate-7-windows-061-kit) regroupe le moteur
mesuré sur Tidewater, le launcher construit sur les mêmes sources et
les notices vérifiées pour ces fichiers. Le [Studio 061 installé sur
l’hôte](#gate-7-studio-061-export-grand-plateau) exporte Le Grand Plateau
avec ce kit exact. Le [parcours joueur 061](#gate-7-joueur-061-grand-plateau)
installe cet export dans un profil distinct, sauvegarde et reprend via
son launcher, puis conserve la sauvegarde à la désinstallation native.
La [revue des licences Windows 061](#gate-7-windows-061-licences)
relie les notices et sources au Setup et à l’export exact du Grand
Plateau.

Reçu `windows-grand-plateau-reviewed-20261116/R7-windows-049-059-evidence-reconciliation-20261117.json`,
SHA-256 `196c9b7a01fd432467ba1a3f3d164ccd6420d441b3ae2a3787a3528570270899`.
Cette relecture ajoute un reçu de 10 531 octets à la campagne et
réutilise les preuves existantes sans relancer d'essai.

- **R7-07, R7-09 et R7-10 sous Windows.** Le candidat privé 049 couvre le
  Studio installé et l'export de Tidewater et du Grand Plateau dans un
  Sandbox hors ligne sans SDK. Un invité joueur séparé a installé, lancé et
  repris les deux jeux sans Studio. Les cycles de mise à jour, restauration
  et désinstallation du Studio et de Tidewater préservent les données
  contrôlées. Le bilan Windows 049 rapproche 43 pièces. Le candidat 050
  confirme le parcours complet sur Le Grand Plateau dans deux invités
  hors ligne. Le Setup 052 avec WebView2 externe est assemblé et vérifié.
  Son essai sur Windows propre couvre le refus d'un prérequis inutilisable.
  Sur l'hôte, le 052 couvre l'export depuis le Studio installé, puis
  l'installation, le lancement, la sauvegarde et la reprise via les interfaces
  du jeu. Le Setup privé 056 est assemblé et relu. Les CLI du 055 et du 056
  ont effectué une installation, une mise à jour et un retour au 055 dans
  un dossier isolé sur l'hôte. Les essais hors ligne et connecté
  ont reproduit la divergence entre registre et disque dans le Sandbox.
  Le [Setup 057](#gate-7-setup-windows-057) est assemblé et son paquet
  Studio a passé l'installation par CLI dans un dossier isolé sur l'hôte.
  Ce Studio a importé le module de débris scannés et produit le jeu
  **Le camp exporté**. Le jeu installé séparément a lancé et repris la sauvegarde
  depuis son propre launcher. Le [058](#gate-7-setup-windows-058) couvre
  l’export privé de Tidewater solaire sur l’hôte, puis le lancement et la
  reprise depuis le launcher livré. Les paquets 057 et 058 passent aussi
  la mise à jour, le retour au 057 et la désinstallation par CLI dans
  une installation hôte isolée. Le précontrôle du 058 sur Windows
  Sandbox sans SDK échoue sur WebView2. L’installation du Setup 060 sur
  Windows propre est couverte par le reçu LTSC ci-dessus.
- **R7-07, R7-09 et R7-10 sous Linux.** Le build privé 0837 et les essais en
  conteneur Debian 12 sans SDK couvrent l'ouverture de la fenêtre Studio
  et, avec le kit 0837, le lancement de diagnostic et la sauvegarde du
  Grand Plateau. Le launcher corrigé du 005 a
  repris la sauvegarde du Grand Plateau sur Ubuntu sans SDK, avec une
  autorité X11 non standard. Le retour au 005 a conservé les données.
  Le runtime du 005 a atteint l'état prêt pendant cette session, puis l'a
  perdu sur la dernière image observée. Sur Ubuntu sans SDK, le 006 a
  repris la sauvegarde du Grand Plateau avec projection et simulation
  prêtes, puis le retour au 005 a préservé les sauvegardes des deux jeux.
  Le SDK 006 a validé les tests du Studio et s'est arrêté au Clippy strict,
  avant de produire le paquet Studio. Le 007 a passé 204 tests du monde,
  1 524 tests moteur, 7 tests du workspace, 91 tests du runtime et le
  Clippy strict du monde et du moteur. Seize tests étaient ignorés.
  Le Studio privé 008 a été construit et installé par CLI sur Ubuntu
  24.04.5 sans SDK. La mise à jour du 0837 vers le 008 puis le retour
  au 0837 conservent les quatre fichiers du projet de qualification.
  L'interface de l'installation 008 s'affiche et ouvre la carte
  sauvegardée dans l'invité Ubuntu. Le Studio 008 importe et exporte
  Le Grand Plateau. Le jeu exporté est installé par CLI dans un profil
  joueur distinct, où son launcher démarre, sauvegarde et reprend une
  partie. Après le retrait du Studio 008, les fichiers auteur et la
  sauvegarde sont intacts. Le launcher reprend la partie avec simulation
  et projection prêtes à la fermeture. La vue 3D du Studio sous Xvfb
  reste à qualifier. Un invité Ubuntu séparé sans Studio ni SDK a
  exécuté l'installateur livré avec le jeu exporté, puis sauvegardé et
  repris une nouvelle partie.
- **R7-08.** Le service d'export natif a produit les crédits de la
  référence Tidewater. Le Studio privé 050 s'installe et démarre en
  Sandbox Windows hors ligne sans SDK. Ses quatre index installés
  correspondent aux empreintes attendues et l'audit ne relève aucune
  identité Cargo sans texte candidat. Le module original du Grand Plateau
  a passé le réexport en mode redistribution dans le Studio 050 sur l'hôte.
  Le Grand Plateau 052 installé conserve le texte CC0 d'origine, les
  sources et notices Symphonia et les quatre index complets de notices.
  Le 055 vérifié utilise le CRT MSVC de Windows et exclut du frontend
  les références locales `forge-imports` et `forge-kit`.
  Le 057 conserve les quatre index de notices et les dix archives sources
  vérifiés dans le Setup exact et dans les deux installations hôte.
  Le CLI du 057 accepte le module CC0 du Grand Plateau et refuse le
  contrôle privé. Le Studio et le jeu exporté restent en portée privée.
  Sur le 058, les quatre index et dix archives sources concordent dans
  les deux rôles installés. Son CLI accepte également le module CC0 du
  Grand Plateau et refuse la publication de Tidewater solaire.
  La [relecture des jeux livrés](#gate-7-inventaires-licences-livrees)
  rapproche les inventaires Windows 058 et Linux 008 et le module issu
  de l’export Linux. Le Studio 059 assemble le jeu redistribuable du
  Grand Plateau avec le kit Windows revu. Le même export est reproduit
  dans un Windows sans SDK avec WebView2 Fixed Version géré. L'installateur
  du jeu et la reprise passent dans un second invité sans Studio. Les
  droits des contenus privés et la détection Evergreen de l'invité
  restent ouverts. Voir le [rejeu 059](#gate-7-studio-059-windows-propre).
- **Performance du moteur commun.** Sur le même trajet Tidewater au
  profil Moyen à 1280 × 800, les kits 053, 054 et 055 mesurent 16,58,
  16,07 et 15,59 ms en moyenne. Le p95 passe de 27,40 à 19,32 puis
  17,75 ms. Le kit 055 atteint la cible moyenne de 16,67 ms sur ce trajet.
  À 2560 × 1440, immobile en troisième personne, il mesure 13,25 ms en
  moyenne et 16,47 ms au p95 contre 20,86 et 22,23 ms lors du précédent relevé
  du moteur commun à la même pose.
  À 2560 × 1440, le 056 mesure 21,11 / 22,37 / 21,83 ms en alternant
  troisième, première et troisième personne sans processus concurrent
  détecté aux bornes. Le lecteur 012 avait mesuré 10,42 à 10,44 ms au
  même emplacement en première personne, avec sa télémétrie historique.
  Le rejeu suivant avec le runtime des débris scannés donne 12,45 / 13,47 /
  13,29 ms dans les trois poses à 1440p. Le démarrage commun sans bordure
  initialise la fenêtre à 2 560 × 1 440 dès l’image 0. Sur le même trajet,
  il mesure 12,89 ms en première personne contre 12,77 ms pour le témoin.
  Le [rejeu avec le terrain source et l’eau](#gate-7-tidewater-terrain-eau)
  termine le même trajet à 14,56 ms en première personne. La parité
  historique et les autres profils et résolutions restent ouverts. Le
  [contrôle visuel du village](#gate-7-tidewater-village-visuel) rapproche
  l’éclairage du 012 quand l’export déclare l’environnement dynamique
  et l’heure de référence, 17 h 45. Une déclaration du champ d’herbe
  rétablit les touffes. La teinte du sol et certains placements diffèrent
  encore. Le correctif de réfraction retire les plantes des cascades
  Bevy sur la pose appariée à 1440p au village. Le moteur commun mesure
  15,93 ms en moyenne, contre 16,24 ms avant le correctif et 10,56 ms
  sur le lecteur 012. Sur le module intégrant l'étal, les 83 poissons
  et Joe animé, les deux correctifs ramènent les entrées directionnelles
  Bevy de 9 426 à zéro. Avec la réfraction active, le dernier rejeu donne
  10,20 ms de GPU actif médian en présentation indépendante contre 8,16 ms
  pour le 012. Le p95 entre présentations est à 16,01 ms, contre 14,18 ms
  pour le 012 dans les mêmes conditions d'affichage. L'éclairage diffus et
  les reflets acquis retrouvent la teinte du sol et du chemin échantillonnés.
  Le [rejeu CDLOD](#gate-7-tidewater-cdlod-ownership) retrouve 14,03 ms
  entre présentations en moyenne et 16,14 ms au p95. Il n'établit aucun
  gain de cadence à cette pose.
  Le [runtime livré dans le kit 060](#gate-7-tidewater-runtime-060)
  relève 13,86 ms en moyenne et 15,69 ms au p95 sur le même scénario.
  Une [copie rapprochée du document 012](#gate-7-tidewater-scene-012-060)
  mesure 13,02 ms en moyenne et 17,27 ms au p95 avec ce même runtime.
  Le [rejeu du runtime courant](#gate-7-tidewater-runtime-courant) relève
  14,08 ms en moyenne et 17,03 ms au p95 dans les 113 intervalles
  consécutifs en présentation indépendante. La fenêtre complète mélange
  les modes et ne qualifie pas la parité.
  Voir le [rejeu des ombres du soleil](#gate-7-tidewater-ombres-soleil) et le
  [rejeu des shaders terrain](#gate-7-tidewater-shaders-terrain).
- **Débris scannés dans le module exporté.** Le rejeu du 15 novembre sur RX
  7800 XT avec Vulkan affiche les troncs témoins via le composant de rendu
  commun utilisé par Tidewater. Le rapport confirme 145 placements et 64
  formes de collision enregistrées. Le contact en jeu et la comparaison
  de performance sur un trajet apparié restent à vérifier. Voir le
  [plan Gate 7](planning/GATE7_IMPLEMENTATION_PLAN.md).

Voir le [plan](planning/GATE7_IMPLEMENTATION_PLAN.md) et la
[matrice d'acceptation](evidence/gate7/GATE7_PROGRESS_20261023.md#matrice-dacceptation).
Les reçus détaillés par candidat suivent.

### 15 novembre — candidat Windows 054

Le kit joueur 054 a été assemblé et vérifié. Les trois exécutables de
livraison contrôlés importent `vcruntime140.dll`. Reçu externe
`candidate-054-replay/R7-08-windows-external-crt-game-kit-binaries.json`,
SHA-256 `b4be34b823dd84d1e87a5288600e6837ac5fe377661b76f13813004405505a8b`.
Le compilateur a terminé le Studio natif 054, mais l'entrée
`apps/forge_studio/src/asset_library_native.rs` a changé pendant la compilation.
Le contrôle de provenance a refusé le binaire. Aucun Setup 054 n'a été produit.

Le frontend 054 contient 3 000 fichiers importés, huit textures de nuages
de la référence Tidewater et huit GLB dont le manifeste limite l'usage aux
preuves internes. Reçu `candidate-054-replay/R7-08-studio-054-local-asset-rights-inventory.json`,
SHA-256 `f3c6e4fd350e63a99c9c78eb4150da340b61a3624d37c1f48e414a9a7430143d`.
Les reçus se trouvent dans la campagne commune, sous
`studio-web-notice-corrections-20261110/windows-external-crt-20261114/`.
Les deux étapes de livraison `prepare-frontend` et `studio` acceptent
désormais `--without-local-assets` pour préparer un nouveau candidat dont
le frontend exclut ces références locales. Le hook et le validateur commun
ont contrôlé ce mode sur une copie temporaire des 13 autres fichiers du
frontend 054.

<a id="gate-7-tidewater-windows-055"></a>

### 15 novembre — trajet Tidewater sur les kits 053, 054 et 055

Après la fermeture du Studio de conversion, les deux trajets 053 et 054
ont terminé sur le même module Tidewater, en Vulkan FIFO sur la RX 7800 XT,
au profil Moyen à 1280 × 800. Le kit exact du Setup 055 a suivi sur le même
trajet. Les trois parcours ont la même empreinte fonctionnelle et des
coordonnées de départ et d'arrivée identiques aux bornes mesurées.

| Kit | Images prêtes mesurées | Moyenne | P95 | 1 % low |
| --- | ---: | ---: | ---: | ---: |
| 053 | 827 | 16,58 ms | 27,40 ms | 30,64 images/s |
| 054 | 852 | 16,07 ms | 19,32 ms | 42,93 images/s |
| 055 | 879 | 15,59 ms | 17,75 ms | 45,46 images/s |

Chaque kit a été mesuré une fois. Les différences entre les binaires
incluent plusieurs changements du moteur commun. L'analyse 053/054 a
repris les observations d'origine après un échec de suppression du cache
temporaire sur des chemins Windows longs. Le cache a ensuite été retiré.
Sous `studio-web-notice-corrections-20261110/windows-external-crt-20261114/`,
`candidate-054-replay/tidewater/G7-D6-tidewater-windows-053-054.json`
a pour SHA-256 `aeef3d2c9e452a07430177250524d289e3eafce9132adae45c9b3b12ced11f05`.
`candidate-055/tidewater/G7-D6-tidewater-055-kit-replay.json`
a pour SHA-256 `d745f626db4e220b909bbd2f8ce0bd7ec6ae149dc1fde9ef5dce3344f22db36f`.

Le scénario immobile du 055 à 2560 × 1440 termine avec la projection
vérifiée. Les 20 valeurs graphiques, la position, le recul de 4,5 m et le
champ vertical de 60° correspondent au précédent relevé du moteur commun
en troisième personne. Les quatre secondes centrales retenues comptent
301 images prêtes, à 13,25 ms en moyenne et 16,47 ms au p95. L'ancien
relevé fournit 20,86 ms et 22,23 ms sur 192 images prêtes, avec un
préchauffage différent. Le raccourci V n'a pas basculé le 055 en première
personne pendant cet essai. Le premier lancement à nombre d'images
borné a perdu l'état prêt du rendu environnemental à sa sortie et a
renvoyé le code 1. La parité avec le 012, mesuré en première personne
avec une autre télémétrie, reste à établir.
`candidate-055/stationary-1440-scenario/G7-D6-tidewater-055-stationary-1440p.json`
a pour SHA-256 `3be748200368b151d968e25b47e84a320997ca07ceee6b4fee0b01830d53e437`.

### 15 novembre — première personne à 1440p

L'étape `set_camera` du scénario commun utilise la commande caméra du
contrôleur et attend son acquittement ainsi que le mode demandé. Les 18
tests ciblés passent. Reçu `native-scenario-camera-command-20261115.log.receipt.json`
SHA-256 `247b61de92c0e60b9c285a2d60083a1855484f9b85722d65dafc411e7ae6b6e2`.
Le build Release de qualification est stable. Les trois exécutions sortent
avec la projection et la simulation prêtes. La résolution de 2560 × 1440,
les 20 réglages personnalisés, la position, le lacet, le tangage, le recul
nul et le champ vertical de 75° sont contrôlés sur les observations. La
fenêtre mesurée couvre les secondes 4 à 8 après la première image prête
à cette pose.

| Mode et destination des observations | Images prêtes | Moyenne | P95 |
| --- | ---: | ---: | ---: |
| FIFO, écritures sur F: | 150 | 26,53 ms | 24,45 ms |
| FIFO, écritures temporaires sur E: | 197 | 20,28 ms | 21,68 ms |
| Immediate, diagnostic avec écritures sur E: | 200 | 20,04 ms | 21,69 ms |

Deux intervalles de 505 et 355 ms apparaissent sur F:. Sur E:, le maximum
FIFO est de 23,15 ms. Une seule exécution par condition limite l'attribution
des pauses. La première analyse avait exigé un échantillon de présentation
dans la fenêtre chronométrée. Les échantillons qui l'encadrent et les
dimensions de chaque observation caméra ont permis de vérifier la
résolution et le mode sans relancer cet essai.

Avec FIFO sur E:, le nouveau relevé reste au-dessus de la cible de 16,67 ms.
Le précédent moteur commun à la même pose mesurait 21,45 ms, et les deux
phases du 012 mesuraient 10,42 à 10,44 ms avec une télémétrie historique
distincte. Le mode Immediate conserve un intervalle proche. La parité et la
cause de l'écart de cadence restent ouvertes.

La relecture des compteurs de phase situe l'écart dans
`render.prepare_views.windows`. Sur les deux dernières collectes en
régime stable, ce périmètre mesure 0,24 ms par appel en troisième personne
sur le 055, puis 6,74 ms en FIFO et 6,43 ms en Immediate en première
personne sur le build de qualification. Le périmètre inclut l'acquisition
de la surface par Bevy. La mesure en troisième personne provient d'une
autre exécution et les périmètres CPU peuvent se recouvrir. La cause de
l'attente reste à isoler.
`G7-D6-first-person-window-cpu-readback.json` a pour SHA-256
`cdf39eba78e9422ab5a55cdd86ecf2b2aa53b9a1a578908cc8d1cb0ed0934fa2`.

La relecture des deux exécutions précédentes à 2560 × 1440 permet aussi
une comparaison au sein de chaque session. Sur les 90 images prêtes avant
la transition et la fenêtre stable après la transition, la moyenne passe
de 12,76 à 20,28 ms en FIFO et de 12,63 à 20,04 ms en Immediate. La position
du personnage et les compteurs HLOD sélectionnés sont identiques dans
les fenêtres comparées. Le champ vertical et la génération de projection
changent avec la caméra. Les échantillons GPU asynchrones du rendu caméra
restent entre 7,63 et 8,49 ms, tous relevés confondus. Ils couvrent un
périmètre de rendu et leur âge est conservé dans le reçu. La relecture
confirme l'écart de cadence sur le même binaire et laisse ouverte la cause
précise de l'attente de la surface. Le reçu
`G7-D6-tidewater-camera-transition-readback.json` a pour SHA-256
`a4786d3e0d21e1160629beac43bc9ae1661ac67893e59e07278a1a72404dd841`.

Reçus sous `studio-web-notice-corrections-20261110/tidewater-performance/runtime-first-person-command-20261115/`.
`module-game-build-proof.json` a pour SHA-256
`0b823d2cd8c18b5342b1ae44579f74807e5c81e6448f52b73b5b2f823e74f482`.
Les rapports `G7-D6-tidewater-first-person-command-1440p.json` de `run/`,
`run-ssd/` et `run-immediate/` ont pour SHA-256, dans cet ordre,
`41f696e8004c9f74e040072a2d136527d16bfe1007d7c8ecf1b3527a0676058a`,
`e4542741a39d6354d56e21b807fd06be263f70acd2f93bf72a3793f3c6fd2bb3` et
`5e8c6f3325305d98fe6711179f54d89ce8b0f1f93c1b8fa37345165d769bca54`.

Le runtime Windows 056 exact a terminé le même scénario Tidewater sans
profilage CPU, à 2560 × 1440 sur la RX 7800 XT en Vulkan. FIFO est le
mode demandé. Les observations valident la résolution, les modes caméra,
la position et les 20 réglages du profil Moyen. Le conteneur SDK Linux 006
compilait durant la mesure, avec un plafond de trois CPU. Sur les 90 images
prêtes précédant la transition, la troisième personne donne 22,02 ms en
moyenne et 24,06 ms au p95. Les 178 images prêtes en première personne
donnent 22,47 ms et 24,33 ms. Le scénario se termine avec la projection et
la simulation prêtes.

Les inventaires du build de diagnostic précédent et du 056 diffèrent sur
`Cargo.lock`, le manifeste et l'historique des assets du Studio, ainsi que
deux scripts de livraison. Dans ce passage, le changement de caméra
modifie peu l'intervalle moyen. La cause du recul en troisième personne
et la parité avec le lecteur 012 restent à établir sur l'hôte au repos.
Reçu `run-056-unprofiled/G7-D6-tidewater-first-person-command-1440p.json`
dans le même dossier, SHA-256
`ed22469870e00effa3124d0590c0a59b470e4b8124bed12dfb8b5b8707d09613`.

Le rejeu du même binaire, sans compilation Rust ni invité Sandbox, a
terminé avec 21,03 ms de moyenne en troisième personne et 21,93 ms en
première personne. Le contrôle de sortie a détecté un processus Python
correspondant au filtre de conversion. Le résultat est conservé comme
diagnostic. La parité à charge comparable reste ouverte.
Reçu `run-056-no-sdk-load/G7-D6-tidewater-first-person-command-1440p.json`,
SHA-256 `bf129342bb5743f5d3a39d161a954982a5f7477a372880d19efd5e1bd8a40620`.

Un aller-retour de caméra a été exécuté avec le runtime 056 exact, le
même module, les mêmes réglages et les écritures temporaires sur E:.
Le contrôle de concurrence a passé au départ et à la sortie. Les
secondes 4 à 8 après chaque pose prête mesurent respectivement 21,11 ms,
22,37 ms et 21,83 ms en moyenne, et 23,03 ms, 24,40 ms et 23,71 ms au p95.
Les trois fenêtres conservent 121 942 triangles sélectionnés et valident
une projection prête à 2560 × 1440. Le temps CPU de préparation fenêtre,
qui inclut l'acquisition de la surface, reste entre 6,42 et 6,51 ms sur
les collectes stables. Dans cette session, l'écart avec la cadence
historique est déjà présent avant la première bascule et persiste au
retour. La cause de l'attente et la parité restent ouvertes.
Dans `run-056-camera-round-trip/`, les reçus
`G7-D6-tidewater-camera-round-trip.json` et
`G7-D6-tidewater-camera-round-trip-attribution.json` ont pour SHA-256
`11730c2a67b7878a85d7f0958063ddab76408f27cefea8b6bedde063e1563e09` et
`cac688192a9054d9a442c2629e3500329cf764cd37739c88a23afd4e22c5375b`.

La répétition à l'identique avec DirectX 12 utilise le même binaire 056
et la même Radeon RX 7800 XT. Les trois moyennes sont de 19,34 ms,
21,27 ms et 22,32 ms. Le temps CPU de préparation fenêtre descend à
0,037–0,039 ms, tandis que la phase `Render` passe de 8,84–9,80 ms en
Vulkan à 11,11–12,35 ms en DirectX 12. Le périmètre de soumission et
finalisation passe de 1,29–1,36 ms à 2,66–2,82 ms. Les périmètres CPU
peuvent se recouvrir. La parité avec la cadence du lecteur 012 reste
ouverte avec les deux backends.
Reçu `G7-D6-tidewater-vulkan-dx12-camera-round-trip.json`, SHA-256
`e617e8d2a75b04ec3839d47bd314b7b5824ef27f81efb1db5178d9595b52d50e`.

La relecture des fenêtres fixes en première personne rapproche les
phases de rendu et la boucle principale du 012 et du 056. La boucle
principale mesure 4,46–4,65 ms dans les traces du 012, puis 14,96 ms en
Vulkan et 15,42 ms en DirectX 12 sur le 056. La préparation du rendu
mesure 1,18–1,19 ms sur le 012, puis 4,07 et 6,96 ms. Les deux backends
du 056 regroupent 19 127 instances de rendu direct et 1 325 instances
Bevy d'ombre dans les relevés retenus. Les sources de chronométrage et
la charge diffèrent entre générations. Les durées de phases se recouvrent.
Reçu `G7-D6-tidewater-012-056-prepare-attribution.json`, SHA-256
`2e8c935dbcd597c40f709f12c1c76a9710ee19f714a16612d71b9abd7837e8f6`.

Le moteur commun réutilise désormais le découpage CPU du parcours de
marche. Le mode de profilage existant relève les opérations avant la
boucle fixe, la boucle fixe, la mise à jour, puis la création de scènes
et la post-mise à jour. Le temps hors de ces étapes inclut aussi la fin
de la boucle, le rendu, la présentation et les attentes du runner.
Le contrôle Cargo verrouillé et hors ligne compile le moteur, le
runtime et leurs cibles de test. Reçu
`tidewater-reuse-walk-cpu-phases-20261115.log.receipt.json`, SHA-256
`10045aecf734254c3837591ff2cd58a4c7a9288b7a694c3eca44f8a33c52fec2`.

Le build Release du runtime mesuré est stable, reçu
`runtime-main-schedule-20261115/module-game-build-proof.json`,
SHA-256 `2361862b656ae828679bfa0016b239066138028752f1aea7b5150991f573d8d4`.
Son manifeste diffère du 056 sur 35 fichiers. Le reçu
`source-delta-from-windows-056.json` épingle les deux inventaires,
SHA-256 `f38ad56aa5be7e0105a27cd458337781de74a6f63981c688265ae1bb1deb14cb`.

Au point fixe Tidewater en première personne à 1440p, la création de
scène et la post-mise à jour prennent 6,34 ms par appel, la boucle fixe
4,67 ms, la mise à jour 0,61 ms et les opérations avant la boucle fixe
0,43 ms. Le runtime termine avec simulation et projection prêtes et
20,73 ms d'intervalle moyen en première personne. Le contrôle de sortie
a détecté un processus de conversion. Le résultat situe les coûts dans
cette session et laisse la qualification de parité ouverte.
Dans `runtime-main-schedule-20261115/round-trip/`, le reçu de parcours
`G7-D6-tidewater-camera-round-trip.json` a pour SHA-256
`abd7badb294950fd7d300670900dfa380a5740abc5f0f82d67de5bff7b63faf5`.
La relecture `G7-D6-common-main-schedule-attribution.json` a pour
SHA-256 `32da3aa96118d657959421cd77f9d1d4d60ea97183eba2e43eb68892eab2d933`.

Le moteur commun fournit maintenant à Bevy une boîte englobante
conservative pour chaque plant qui dispose d'une enveloppe de
déformation admise. La boîte couvre les LOD et le mouvement déclaré,
arrondit vers l'extérieur et suit les coordonnées du rendu à
transformation identité. L'audit vérifie la boîte et le marqueur qui
empêche Bevy de la remplacer par celle du maillage prototype.
Les plantes sans boîte exploitable gardent leur exemption de filtrage.
Les 43 tests CPU ciblés passent et un test GPU est ignoré. Reçu
`tidewater-bounded-plant-culling-tests-20261115.log.receipt.json`,
SHA-256 `e60a5d78e520713bf3e08b2e0bbe0cb9f6c285800a97fef19c92a488f947ae1d`.
Le comparatif garde temporairement le binaire précédent épinglé sur E:
(`65a62bb5b7f70dc060ea355d07f3fb6c257591731037fb1ea1737a18c3f687cc`).

Le comparatif sur les deux builds stables à 1440p conserve le même
module, les mêmes réglages, la même empreinte fonctionnelle et
121 942 triangles dans la sélection de projection. Les compteurs
préparatoires du rendu direct passent de 19 114 à 10 768 instances en
troisième personne et d'environ 19 128 à 12 545 en première personne.
Les deux captures terminales montrent les mêmes plantes et les grandes
ombres au point examiné, avec des différences dans le feuillage animé.
Le premier essai s'était arrêté lors d'une commande caméra après
capture. Le rejeu a placé l'unique capture après la dernière mesure,
ce qui permet aux deux parcours de terminer. La capture redondante du
premier essai a été retirée après conservation du diagnostic.

La cadence dépend de la phase. Les moyennes passent de 12,60 à 20,79 ms
en troisième personne avant la bascule, de 13,22 à 13,48 ms en première
personne et de 14,10 à 13,20 ms au retour. Le contrôle de concurrence est
vide au lancement et à la sortie des deux essais. La charge préparée
baisse et cette paire n'établit aucun gain de cadence global.
Sous `runtime-bounded-plant-culling-20261115/terminal-capture/`, le reçu
de parcours `G7-D6-tidewater-bounded-plant-culling.json` a pour SHA-256
`d1740205669f0be68c728ef5250a6474515a1d5f55acfe1c2cdb2938805f8729`.
La relecture des compteurs et des captures
`G7-D6-tidewater-bounded-culling-workload.json` a pour SHA-256
`714b7e1b7fd27ffd06d53eca0a2a37ddb954d6402fa450a8b9c4cd7f95b2c889`.

La relecture des traces existantes précise la phase lente avant la
bascule. Sur 300 pas de simulation en troisième personne, la version
avec filtrage traite 232 images contre 399 sur la référence et passe
5,89 ms par pas fixe contre 3,76 ms. La phase CPU `Render` passe de
7,83 à 12,98 ms par appel, tandis que la préparation fenêtre reste à
0,22–0,24 ms. Au retour de la caméra, les deux phases `Render` sont à
8,86–8,93 ms et les deux cadences sont à 13,72–13,75 ms dans les
fenêtres de relecture. Les fenêtres reposent sur des compteurs cumulés
encadrés par les diagnostics GPU, avec caméra, projection et simulation
stables. Les périmètres se recouvrent et le coût par pas fixe reste à
attribuer. Reçu `G7-D6-tidewater-bounded-culling-cpu-gpu-readback.json`,
SHA-256 `43baf808496ac79422deebf7fbf475c666e18e0c141f9c5a7d7208d5d81acb1c`.
Sur les deux traces alignées par pas de simulation, la version avec
filtrage passe de 21,63 ms aux pas 731–807 à 13,54 ms aux pas 879–897.
La référence passe de 13,07 à 13,44 ms. Les deux fenêtres gardent la
caméra initiale et la projection prête. Le scénario attend 900 pas avant
de soumettre la commande caméra. Le retour à cette cadence précède donc
la bascule. Reçu `G7-D6-tidewater-before-first-camera-command.json`,
SHA-256 `b8b38e8fe93f319f51e1ee805a50e7da64ed5fe58495a6e2db4d1f531c2ee5d7`.
Dans le contrôleur commun, le pas fixe signalait à Bevy une modification
de la projection, de la pose de caméra et de la visibilité du personnage
même à valeurs égales. Les écritures suivent désormais les changements
réels. Le recalcul Bevy de la projection conserve ses déclencheurs pour
le champ de vision, la taille du viewport et les changements de cible.
Le test ciblé couvre la caméra stable et les bascules de mode avec
collision. Le gain de cadence reste à mesurer sur un binaire incluant
cette correction. Le conteneur Linux 008 utilise son snapshot antérieur.
La référence du prochain comparatif est l'exécutable avec filtrage déjà
mesuré. Sa copie de 119 217 664 octets sur E:, `forge-module-before-camera-change.exe`,
correspond au reçu du build, SHA-256
`63cd2049899a6f157495495441c494d7529a314288fcaf15c6b400e1f43a4355`.
Les deux écarts d'inventaire constatés avant le prochain build sont le
contrôleur et `asset_library_native.rs` du Studio. Le graphe Cargo du
runtime de module ne dépend pas de `forge_studio`. Le test ciblé passe
avec les contrôles de changement de projection et de visibilité. Reçu
`tidewater-stable-camera-changes-20261115.log.receipt.json`, SHA-256
`95eb452627a2c4174fa01147eb6e95af8e45d95b75f498cce3635ebb49a6c1a9`.
Le build Release démarre avec les deux écarts d'inventaire attendus.
Les deux versions peuvent reprendre le même scénario à 1440p avec
les mêmes réglages. `runtime-stable-camera-20261115/Replay-Stable-Camera.py`
réutilise le scénario et le script de mesure précédents en vérifiant
les empreintes des binaires et les deux écarts d'inventaire. Son mode de
validation du lanceur est passé sans démarrer les jeux.

Le build Release corrigé a terminé sur des entrées stables. Reçu
`runtime-stable-camera-20261115/module-game-build-proof.json`, SHA-256
`ddbeb6faa292259866ebee8de2b88b9f34c57824fcaf37ea77f76613085d355a`.
Dans la paire mesurée à 1440p, les moyennes avant/après sont
19,04 → 18,72 ms en troisième personne, 19,83 → 19,38 ms en première,
puis 20,45 → 19,72 ms au retour. Les deux versions terminent avec
projection et simulation prêtes, et comptent 121 942 triangles
dans la sélection. Le Linux 008 compilait ses tests du runtime aux
deux bornes de la paire. La cadence historique du 012 reste à atteindre.
Sous `runtime-stable-camera-20261115/terminal-capture/`, le reçu
`G7-D6-tidewater-stable-camera.json` a pour SHA-256
`850db1322080834bad1f5e8fd0b2afeb48a4c7adcafe7c7978428f3dcd42ca44`.

Le trajet stationnaire à 1440p a été rejoué en série sur le build avec
filtrage précédant la correction caméra et sur le binaire qui a validé
les débris scannés. Les deux exécutions conservent le même export
Tidewater, 20 réglages du profil Moyen, la RX 7800 XT avec Vulkan et
121 942 triangles sélectionnés. Le filtre de concurrence était vide aux
bornes. Dans les trois fenêtres prêtes, les moyennes sont de 12,31 / 13,23 /
13,47 ms sur la référence et de 12,45 / 13,47 / 13,29 ms sur le nouveau
binaire. Les p95 sont de 13,61 / 14,66 / 15,44 ms et de 13,93 / 15,44 /
14,78 ms respectivement. La fenêtre en première personne reste au-dessus
des 10,42–10,44 ms de la référence 012, mesurée avec sa télémétrie d'origine.
Ce rejeu utilise l'export Tidewater historique employé pour les mesures
précédentes. Le coût des placements scannés reste à mesurer avec le paquet
qui les contient. Les rapports `G7-D6-tidewater-camera-round-trip.json`
sont sous `runtime-scan-ready-20261115/before-camera-change/` et
`runtime-scan-ready-20261115/scan-ready/`. SHA-256 respectifs
`28a4157d91d65b4252120670ea8d5d5abb1b2f29e616f5d7445f8f83e21c31f0`
et `8efbfc9a6125f2053e49e42906b41645cdc980fa8b086e6d0191cd7fb45e71ce`.

La relecture des intervalles en première personne à l'arrêt rapproche
les captures sur RX 7800 XT, pilote 26.10.1, Vulkan FIFO et 1440p. Les
empreintes du module et du gameplay ainsi que les 20 réglages comparés
concordent. Sur 768 intervalles du lecteur 012 et 297 du moteur commun,
la médiane passe de 9,94 à 13,31 ms et le p95 de 14,68 à 15,44 ms.
L'écart moyen reste de 3,01 ms après retrait de 5 % de chaque extrémité.
Cinq intervalles du moteur commun dépassent 16,67 ms. Ces valeurs
mesurent la cadence de la boucle principale. Les protocoles de capture
diffèrent et l'équivalence du plan lointain reste à établir.

Trois relevés GPU récents du moteur commun donnent une médiane de
7,98 ms pour le graphe caméra, 2,75 ms pour la passe opaque et 0,81 ms
pour la préparation des lots de maillages (`bin_unpacking`). Les deux
phases stationnaires du 012 donnent respectivement 5,96–6,50 ms,
2,87 ms et 0,17 ms. Les relevés GPU sont asynchrones et ces mesures
ne s'additionnent pas. La préparation des lots de maillages reste un
écart à examiner pour R7-05.

Les reçus `first-person-cadence-comparison.json` et
`first-person-gpu-comparison.json` sont sous `runtime-scan-ready-20261115/`.
Leurs SHA-256 sont respectivement
`b2646d53f18bb5863216322a3d4d7b8e3e82e6ab21d06bdafb25b768df1a800f` et
`1ef3b8a1929f47e96ca0f97b89798af53698b63e1921125047d3303c8c258219`.

Sur chacun des deux binaires communs, le premier relevé des lots après
la mesure GPU dans la fenêtre retenue arrive moins d'une seconde après
cette mesure. Les compteurs concordent. Le rendu principal prépare
120 lots et 12 544 entrées d'instances sur deux couples vue-passe.
Les ombres préparent 389 lots et 1 304 entrées, dont 1 244 pour les
trois cascades directionnelles et 60 pour les six faces de la lumière
ponctuelle. Le coût GPU `bin_unpacking` relevé à proximité est de
0,809 et 0,817 ms. Les compteurs décrivent les listes préparées sur CPU.
La passe Bevy balaie les groupes par vue et phase, et calcule les groupes
de travail à partir du nombre d'instances. Le 012 ne conserve pas les
mêmes compteurs. L'attribution de l'écart GPU exige une mesure des groupes
effectivement exécutés avec un cadrage équivalent. Avant le transfert du
réglage, la caméra joueur commune initialisait son plan lointain à 1 600 m
et la sonde de marche Tidewater le fixait à 60 000 m au démarrage.
Reçu `runtime-scan-ready-20261115/first-person-prepared-mesh-bins.json`,
SHA-256 `5fe30e1db3dcee83abf31cf43e4fa5c5e16b00d722fc707dccab45d8f24fa037`.

Le contrôleur joueur commun initialise désormais le plan lointain à
60 000 m quand un jeu installe son terrain source admis, y compris avec
l'océan source. La sonde de marche utilise le même réglage du moteur.
Les autres scènes joueur utilisent le réglage local de 1 600 m.
Sous Windows, `cargo check` passe pour `forge-module` et la sonde
`tidewater_plant_probe`. Le test `scene_camera_range_is_applied_at_spawn`
confirme les deux distances au démarrage. Reçus à la racine de la campagne
`source-camera-horizon-check-20261115.log.receipt.json` et
`source-camera-horizon-startup-test-20261115.log.receipt.json`.
SHA-256 respectifs
`af8e7673d2f5844ef8bb72a3d9fe473f3e9de25ee67c79e22efa1c3078c3852a` et
`d28ebd70ae3f4acb224ea9aebc0eb0448df2ec872d97d9257024fb62e13e0a17`.

Au rejeu Release du même trajet Tidewater, les trois phases se terminent
sur RX 7800 XT, pilote 26.10.1, Vulkan FIFO et 2 560 × 1 440, avec le même
module et les mêmes réglages. En première personne, la moyenne et le p95
sont de 21,22 et 23,49 ms. Le binaire de référence à 1 600 m, conservé
avant compilation et rejoué dans la même session, donne 23,07 et
26,76 ms, contre 13,47 et 15,44 ms dans ses mesures archivées. Les volumes
des lots de maillages concordent sur les trois relevés. Sur les intervalles
CPU proches des fenêtres de mesure, `render.prepare_views.windows` passe
de 0,21 ms dans l'archive à 6,60 ms pour le nouveau binaire et à 6,35 ms
pour le contrôle de la même session. Cette hausse se reproduit avec
l'ancien exécutable. Les durées GPU sont asynchrones et l'expérience
n'isole ni la cause de l'attente côté fenêtre, ni l'effet du plan lointain.
La comparaison de rendu et la parité avec le 012 restent ouvertes.

Les reçus `horizon-build-readback.json` et
`R7-05-source-camera-horizon-matched-replay.json` sont sous
`tidewater-performance/runtime-source-camera-horizon-20261115/`.
SHA-256 respectifs
`e789a3c7bf3dfa07ebe1ee3bb55818f4dd3a079fb637024f818ddf62c509caba` et
`b25cb2dc742b3faafe7bfac908d58a024fa90951677e5314a9204134e835939b`.

Le lecteur Tidewater créait déjà sa fenêtre sans bordure dès le
démarrage. Le jeu exporté créait d'abord une fenêtre de 1 280 × 800 avant
de demander ce mode. Le module commun et la sonde Tidewater utilisent
maintenant le même choix de mode lors de la création native. La
confirmation par l'OS et le repli en mode fenêtré restent actifs. La
vérification `cargo check` passe pour `forge-module` et
`tidewater_plant_probe`.
Reçu `initial-display-mode-parity-check-20261115.log.receipt.json`,
SHA-256 `e9b59291ec8a754165253d3a5ddf0df3d8f9e0f64173c0de1e549838347f13ad`.

Le module Release a été recompilé en 19 min 36 s dans le cache partagé.
Les 3 509 entrées de compilation concordent avant et après le build.
Par rapport au témoin à 60 km, seuls `native_display.rs`, `scene.rs`
et `tidewater_plant_probe.rs` diffèrent. Le reçu Cargo a été relu
après une brève disparition de F:.
`runtime-initial-borderless-release-20261115.log.receipt.json`,
SHA-256 `aeab7fec714ed68baffc71be6c8054075cdeca3587e536722d9e5c05e37612cc`.

Les deux passages Release utilisent le même module, la même pose et les
mêmes réglages à 2 560 × 1 440, sur RX 7800 XT en Vulkan FIFO. Le témoin
initialise la fenêtre à 1 280 × 800 et reçoit la taille finale à l’image 1.
Le nouveau binaire initialise à 2 560 × 1 440. Les quatre notifications
de taille, y compris deux notifications intermédiaires, arrivent à l’image 0.
En première personne, la moyenne et le p95 sont de 12,89 et 14,73 ms,
contre 12,77 et 13,99 ms pour le témoin. Le changement de démarrage
n’améliore pas la cadence mesurée dans cette pose. Les deux mesures
restent au-dessus des 10,42–10,44 ms du lecteur historique 012.

Reçu `tidewater-performance/runtime-initial-borderless-20261115/R7-05-initial-borderless-comparison.json`,
SHA-256 `4463e41249c85f532c217472f7968be342d78eb414758144554ed21588c78d36`.
Le premier témoin a été écarté après détection d’une conversion active.
Ses reçus de lancement et de sortie sont conservés. Les traces écartées
sur F: (5 709 979 octets) et les copies temporaires sur E: (122 358 324
octets) ont été retirées après vérification du comparatif.

<a id="gate-7-tidewater-terrain-eau"></a>

### 16 novembre — Tidewater avec terrain source et eau dans le moteur commun

La relecture du manifeste de l’export 049 employé pour le comparatif
confirme l’absence de liaisons de terrain source et d’océan pour la zone.
Le lecteur 012 enregistrait des passes d’océan, d’embruns et de transmission.
Le comparatif précédent gardait donc une différence de charge de rendu.

Une variante privée de la même carte réunit 324 ressources terrain,
FFT, surface d’eau et ressac du paquet de qualification existant. Le
CLI de livraison valide le paquet avec `orpmod-inspect` et `module-check`. Les
empreintes de zone, d’archive, de gameplay et les onze sources de zone
sont inchangées. Le binaire Release et le scénario restent identiques
au comparatif sans bordure. Les inventaires GPU du terrain, des modèles,
des objets, de l’eau, des lèvres de vagues et des embruns sont achevés
et courants à la sortie normale. Le journal confirme les passes
d’océan et de transmission, absentes des relevés de l’export 049.

Sur RX 7800 XT, en 2 560 × 1 440 Vulkan FIFO et profil Moyen, les poses
et les 20 réglages concordent. Les moyennes des trois phases sont de
15,14 / 14,56 / 15,67 ms, contre 12,04 / 12,89 / 12,87 ms lors du
passage précédent. En première personne, le p95 passe de 14,73 à
15,29 ms. Les trois échantillons GPU récents donnent une médiane de
9,94 ms pour le graphe caméra et 0,79 ms pour la préparation des lots.
Les passages successifs changent le terrain et l’eau ensemble. Ils
ne mesurent ni le coût isolé de l’eau, ni la parité optique et de
cadence avec le lecteur 012.

Sous `tidewater-performance/runtime-initial-borderless-20261115/`, reçus
`tidewater-source-water-inputs.json` et `R7-05-tidewater-source-water-replay.json`,
SHA-256 respectifs
`6cf639013c1099c7e6d7ef398ac916858c73d6c93bc4db948a418c1c30404183` et
`78a003deeb7c02b87600b92999dfe27809d05a28960642da3f6b314746d83c96`.
Les 5 758 968 octets de traces dupliquées sur E: ont été retirés après
relecture sur F:. Le paquet de 298 084 920 octets et son cache installé
de 297 758 634 octets de payloads ont servi au contrôle visuel apparié
ci-dessous. Le paquet et son cache ont ensuite été retirés.

<a id="gate-7-tidewater-village-visuel"></a>

### 16 novembre — Vue du village et horloge solaire du module

La référence 012 exacte a été capturée par observation de sa fenêtre
pendant la seconde phase fixe. Les deux captures du moteur commun
proviennent du scénario de jeu, à la même position de caméra
[40, 5,31931, −115], en première personne à 2 560 × 1 440. Les
empreintes de zone, d’archive et de gameplay, les 20 valeurs
graphiques et la rotation concordent. Les clichés du moteur commun
terminent sur une projection prête et sans erreur.

L’export de référence 049 déclare un profil d’environnement acquis.
Son adaptateur `legacy_reference::environment_binding` produit ce profil
sans programmes ni horloge. Dans le test, l’ajout des programmes
déjà empaquetés et d’une horloge fixe à 17 h 45 produit 22 programmes
préparés, une publication HDR et l’heure GPU prête. La carte et le
binaire sont conservés. Le village retrouve l’ombre observée dans le
012 et la capsule retrouve sa couleur claire. Le sol reste plus chaud
et les herbes et arbustes diffèrent. Le cadrage ne montre pas l’océan.
La capture du 012 est un JPEG de fenêtre. Les deux autres sont des PNG
du jeu. L’exposition et les animations ne sont pas synchronisées pour
une comparaison pixel à pixel.

Sous `tidewater-performance/runtime-initial-borderless-20261115/`,
`R7-05-tidewater-visual-reference-comparison.json` lie les trois vues
et leurs reçus, SHA-256
`736c83108d50a01366ae6f5a50216e6f32e958565162bb72a7ee1041a4b9d15c`.
`tidewater-village-visual-review.jpg` les présente dans l’ordre 012,
export 049 avec terrain et eau, puis environnement dynamique à 17 h 45.
Le paquet solaire est une variante privée de qualification. Le
[parcours Studio](#gate-7-import-grands-modules) vérifie ces références
après réouverture et réexport du projet.

Le rapport de reprise du jeu 058 indique zéro cellule d’herbe publiée.
Le module contient 23 ressources d’herbe dans son profil importé, mais
son document végétal v1 ne demande aucun champ. Le moteur commun installe
ce champ sur déclaration v2, conformément à [ADR-0042](ADR/ADR-0042-explicit-imported-grass-fields.md).
La variante privée `tidewater-solar-grass.orpmod` ajoute la déclaration
avec l’empreinte du profil déjà présent. La régénération conserve les
blocs de plantes, le terrain et les corrections. La livraison relit les
onze sources de zone et les 1 568 payloads inchangés. La révision du
document et les empreintes de l’archive et du head avancent. Aucune
ressource d’herbe supplémentaire n’entre dans le paquet.
Reçu `tidewater-solar-grass-inputs.json` dans la même campagne, SHA-256
`8516d83ed0cd4296bbcd101a32b914d7209fc31b7f7d27583a6285bfac0bdb43`.

Les deux captures de qualification emploient le même exécutable, la même
caméra à 2 560 × 1 440, les mêmes réglages graphiques et l’horloge à
17 h 45. La variante publie 12, 36 et 94 cellules dans les trois niveaux.
Le rapport GPU confirme trois maillages, trois matériaux, onze images
et huit buffers prêts. Les touffes reviennent autour du chemin. Le sol
reste plus chaud que dans le 012 et plusieurs formes et placements
diffèrent encore. Le rejeu se termine sans erreur de projection.

`R7-05-tidewater-solar-grass-comparison.json`, SHA-256
`691f03f94b176a734b3b46649d2295aac31c1b7bd3ae7a8514c17fcfb7516fc5`.
`tidewater-village-grass-review.jpg`, SHA-256
`c815804f6b001208503aa480b38c1c638415d2cbb6187fce8a606ee4903407e6`.
Les huit copies de trace sur E: ont été retirées après relecture sur F:,
soit 6 604 759 octets. Le paquet privé de 298 718 101 octets et son
cache restent sur E: pour le prochain parcours Studio et le comparatif
de cadence apparié.

La relecture du manifeste d’entrées archivé du 012 confirme les mêmes
sept ressources de terrain dans le module d’essai, dont les shaders, la
carte de normales, le splat et la texture de détail. Le raster et les
paramètres d’échantillonnage concordent. La zone n’a pas de substitution
de matériau. Dans le code courant, l’adaptateur de présentation de la
référence met à l’échelle les harmoniques sphériques acquises, tandis que
le jeu de modules reçoit celles du graphe HDR natif. La part de cette
différence dans la teinte mesurée reste à établir sur le GPU.
Reçu `R7-05-tidewater-terrain-input-comparison.json`, SHA-256
`9a8dc07866b6c70bfb73477212dfb9ee0b01d6b7d121d8d115b98f6ac6d970e4`.
Le reçu relit aussi les intervalles des captures existantes. Les
sessions ont eu lieu à des moments différents et ces chiffres ne
permettent pas d’isoler le coût de l’herbe ou du graphe HDR.

Le moteur conserve le tampon source de 32 octets de l’éclairage direct
tant que ses valeurs restent identiques dans une même génération
d’environnement. Le rendu continue de publier ces valeurs et les
harmoniques dans le tampon de scène. Le `cargo check` natif Windows du
moteur passe. Reçu `source-environment-key-light-reuse-check-20261116.log.receipt.json`
à la racine de la campagne, SHA-256
`e09c11f6bee6cdf6207b1c6fd4e74f167fd8c6f021cd1c88ff46e28bb90d2356`.
Le gain de cadence reste à mesurer.

Le jeu de qualification réutilise la sonde de lecture GPU du tampon
d’éclairage de terrain (`FORGE_LIGHTING_BUFFER_READBACK=1`). Son rapport
consigne les 176 octets effectivement relus et la frame de réception
asynchrone. Le binaire archivé du 012, SHA-256
`1d4858213e74e64ead31332a65a633e7baa1c951645616137de04b3733271d27`,
ne contient pas cette option. La capture historique reste la référence
visuelle. Le rejeu `Replay-TidewaterLighting.py` vérifie la pose du module
à 17 h 45 et le résultat de la sonde après publication de l’environnement.
Le build Release passe. Reçu `tidewater-scene-lighting-readback-release-20261116.log.receipt.json`,
SHA-256 `e5fbaa212678561f0a0d619452d0027d222afe021be904b1aefdddc7a05f9fc9`.
Le premier rejeu s’est arrêté après deux notifications de retrait d’écran,
avant la publication de la lumière à 17 h 45. Le rejeu suivant termine à la
même pose et en 2 560 × 1 440. La lecture GPU reçoit les 176 octets à la
frame 496, avec projection prête et sortie normale. L’évaluation des
harmoniques selon une normale verticale donne RGB linéaire
`[0,2024 ; 0,1645 ; 0,1654]`. La capture acquise du témoin historique
`lighting-diagnostic-reference-004` donne `[0,1571 ; 0,2415 ; 0,4055]`.
Le témoin acquis provient d’une autre heure et d’une autre caméra. Cet
écart quantifie la couleur des données réellement lues, sans isoler le
coût du graphe ou la cause de tout l’écart visuel avec le 012.

`solar-grass-lighting-readback-retry/lighting-readback-assessment.json`,
SHA-256 `d2a8b5ff84c0fdd682b4fd089386a072ecc11bcb5d0963d06ddfbb62429770e6`.
Les copies de traces sur E: ont été retirées après relecture sur F:,
soit 6 752 498 octets. Le binaire antérieur reste disponible dans le
dossier temporaire du comparatif jusqu’à la mesure sans sonde ci-dessous.

Le même paquet solaire avec herbe a été mesuré à 2 560 × 1 440, en
première personne, avec projection prête et sans lecture du tampon GPU.
Les moyennes témoin/candidat/témoin de retour sont de 15,73 / 16,09 /
15,66 ms. Les p95 sont de 16,97 / 19,17 / 16,94 ms. Le premier passage
de retour a été écarté après détection d’un processus de conversion à sa
sortie. Les fonctions Cargo diffèrent entre le témoin et le candidat.
Cette comparaison ne montre pas de gain de cadence et ne permet pas
d’attribuer l’écart à la réutilisation du tampon seule.
`R7-05-solar-grass-build-cadence.json`, SHA-256
`127326ddd320acfce9cca1f9738d9c7e4dca2adf9f1759ae3e078a40e8936880`.
La mesure conservée sur F: a permis de retirer les traces dupliquées
sur E: (4 489 549 octets) et le témoin 950d4f8e devenu inutile
(120 231 424 octets). Le candidat de cette mesure sert de témoin suivant.

Le profil de cette session mesure 1,67 ms en moyenne dans l’observation
des familles graphiques du candidat, démarrage inclus. Cette observation
compare désormais les identifiants d’assets typés. La mise en texte sert
uniquement à signer une publication ou à vérifier sa reprise par le
préchargement. Les listes visibles et retenues gardent le même périmètre.
Le build Release passe avec les mêmes fonctions Cargo dans la paire.
Sur la même pose et le même paquet, le coût CPU moyen de l’inventaire
sur chaque session passe de 1,66 à 1,08 ms, puis revient à 1,66 ms avec
le témoin. Sur les ticks prêts retenus, les moyennes restent de
15,71 / 15,77 / 15,63 ms et les p95 de 17,03 / 17,01 / 17,24 ms.
L’amélioration est mesurée dans l’inventaire. La cadence globale reste
dans la dispersion des deux passages témoins. Le premier passage du
candidat a été écarté après détection d’une conversion à sa sortie.
Les copies des traces sur E: ont été retirées, soit 4 444 839 octets.

`R7-05-typed-inventory-cadence.json`, SHA-256
`796f384805fd87e3018c0dc4c605c8ce9309a82e00d22ed961a17376ac99fff1`.
Reçu `tidewater-typed-graphics-inventory-release-20261116-replay.log.receipt.json`,
SHA-256 `609cb4ad8c99fd47a53bd52861bec8e71bc4243e5a0ad6b114102e9a40e63b13`.

La relecture des traces du 012 et de ces trois passages vérifie la pose
fixe du village, la première personne, les 20 réglages graphiques et
la résolution de 2 560 × 1 440. Les médianes des intervalles enregistrés
sont de 9,57 à 9,98 ms sur les deux phases fixes du 012 et de 15,48 à
15,68 ms sur le moteur commun. Les p95 sont respectivement de 13,85 à
15,10 ms et de 17,01 à 17,24 ms. Entre 9 et 11,5 % des intervalles
du moteur commun dépassent 16,67 ms sur les fenêtres retenues. L'écart
apparaît donc aussi dans la cadence ordinaire, avec des révisions de
scène et une instrumentation distinctes. Cette relecture n'inclut ni
les présentations à l'écran ni les deux corrections d'environnement
ci-dessous.
Reçu `R7-05-tidewater-fixed-frame-interval-reanalysis-20261116.json`
à la racine de la campagne, SHA-256
`b53a4c22b97e50354ea4d3e9f827f12891d8354d0ed0254e5411be580d1233cf`.

Le moteur valide désormais les entrées d’environnement sans construire un
cycle initial à chaque soumission. Entre deux phases de rafraîchissement,
il avance le même contrôleur sans allouer les quatre uniformes de calcul.
Lorsque la lumière directe et sa destination GPU sont identiques, il
acquitte aussi la frame sans encoder de copie. Une nouvelle destination
reçoit la lumière courante et chaque rotation de l’environnement publie
toujours la cubemap et les harmoniques. Le contrôle de compilation Windows
passe. Le rejeu GPU a été différé pendant la conversion d’assets en cours.
Reçu `source-environment-idle-plan-check-20261116.log.receipt.json`
à la racine de la campagne, SHA-256
`bb7f6f51f0adc2555aa5720c7e0f55d014c4ef72c6ad25ab38ae21fe60502bcd`.

La publication solaire du moteur commun cible désormais le soleil du
terrain. Les autres lumières directionnelles gardent leur couleur,
leur intensité et leur transform. Une publication identique ne marque
plus le soleil comme modifié. Le test de régression exerce ces deux
cas, et les trois tests de publication solaire passent sur Windows.
La correction est postérieure au binaire de comparaison ci-dessous.
Reçu `source-solar-terrain-sun-ownership-20261116.log.receipt.json`,
SHA-256 `6958a515c20e3e1e90a34bbc656cef969403db2b53a7a52a82f17ef69894d9d5`.

Le build Release de qualification du jeu réussit avec les mêmes fonctions
Cargo que le témoin. Le candidat a pour SHA-256
`fb3513680c81dd6d207a20f71fb79c9028f2e8f03708ead05f9b86d07e24e2d7`.
Le témoin du relevé précédent est conservé avec le paquet solaire et ses
réglages pour le scénario témoin, candidat, témoin de retour. Le comparatif
`Compare-TidewaterEnvironmentIdle.py` réutilise les entrées épinglées
du dernier relevé. Aucun passage GPU n'a démarré pendant la conversion.
Reçu `tidewater-environment-idle-release-20261116.log.receipt.json`,
SHA-256 `17d21d754cdfc5fc1748cb78df9d6c3eea38c1144cd0e7ef6eef501560f7b104`.

Les deux premières tentatives du 16 novembre concernent le témoin seul.
La première s'arrête après deux retraits d'écran et avant la projection
prête. La seconde termine le scénario avec projection et soleil prêts,
et 12, 36 et 94 cellules d'herbe. Un nouveau lot de conversion est
détecté à la sortie des deux passages. Leurs timings sont écartés.
Les rapports des dossiers `solar-grass-environment-idle-before/`
et `solar-grass-environment-idle-retry-before/` ont respectivement pour
SHA-256 `c204b9938e49fe7aa5a954e07b60efa47e45b64c8293b4ff0e7e652efbe2836d`
et `999af08c437c22714e23a5a10e7222f552e71a57152894b471579b5f54120253`.

Le troisième passage attend sept observations sans charge concurrente
et avec l'écran principal en 2 560 × 1 440. Le témoin termine le scénario
avec projection et soleil prêts. Le lot de conversion suivant, processus
24068, a démarré à 04:23:03, avant l'initialisation du rendu à 04:23:08
et sa fin à 04:23:20, heure locale. Ce passage est aussi écarté.
Le candidat Release reste à mesurer sur un créneau sans conversion.
Rapport `solar-grass-environment-idle-idle-before/module-runtime-report.json`,
SHA-256 `4f12e39e5c797940c88e881c426c66b6880fd43b36f4618a55a6d5f97a23ec6e`.
Les trois dossiers laissés sur E: représentent 2 353 736 octets. Les
rapports utiles sont conservés sur F:.

Après la fin de la conversion, les trois passages 1440p se terminent
avec projection, soleil et publication HDR prêts, sans charge concurrente
observée aux bornes. Aux mêmes pose et réglages, les moyennes du témoin,
du candidat et du témoin de retour sont de 15,80 / 15,57 / 15,76 ms.
Les p95 sont de 17,30 / 16,82 / 16,66 ms. Le petit écart de moyenne
est observé sur cette série et les p95 des deux témoins encadrent celui
du candidat. La parité avec le 012 reste ouverte.
Reçu `R7-05-environment-idle-cadence-conversion-complete.json`,
SHA-256 `d2b44e6ea39f1eff3b276cf0a5adda3ffd8fe8911e9b22011d308f6d8b2d99cd`.

Pour mesurer le 012 et le moteur commun avec la même source de temps,
PresentMon 2.6.0 a ouvert et fermé une session ETW locale sans élévation.
L'exécutable Intel signé correspond au SHA-256 de sa version publiée.
Ce contrôle portait sur un processus sans rendu. La capture des deux jeux
et le rapprochement des fenêtres de mesure restent à faire.
Reçu `presentmon-session-check-20261116.json` dans le dossier du comparatif,
SHA-256 `cdd77194a6e3711a74434e405ba7e75012abeb4a80ffff9de4831a2e98080064`.
[Documentation PresentMon](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md).

Le lecteur `scripts/gate7/frame_intervals.py` accepte maintenant le format
PresentMon 2.6 `--v1_metrics --qpc_time`. Il retient les intervalles
complets d'un seul processus et d'une seule chaîne de présentation dans
la fenêtre QPC fournie. Il conserve le mode et l'indicateur d'image
écartée. Les sept tests du lecteur passent, dont un cas synthétique
avec deux processus et un changement de chaîne.
[Colonnes CSV de PresentMon 2.6](https://github.com/GameTechDev/PresentMon/blob/v2.6.0/PresentMon/CsvOutput.cpp#L142-L175).

Le comparatif PresentMon du 16 novembre confirme l'écart de cadence
avec le même instrument. Les deux fenêtres de quatre secondes couvrent
la vue fixe du village en première personne, en 2 560 × 1 440, avec les
mêmes réglages, le même mode de présentation et `SyncInterval=1`.
Le 012 mesure 10,56 ms en moyenne et 14,18 ms au p95 sur 378
présentations. Le moteur commun mesure 15,75 ms et 17,02 ms sur 253
présentations. Aucune présentation écartée n'est relevée. La médiane
du temps GPU actif rapporté passe de 8,16 à 11,79 ms et le temps médian
dans l'appel de présentation de 0,09 à 0,11 ms. L'instrument mesure
des intervalles entre appels et ne permet pas d'attribuer le coût à
une passe. Les deux transports de scène restent distincts. Le binaire
commun porte l'optimisation du cycle d’environnement et précède la
correction du soleil du terrain.
Reçu `R7-05-tidewater-presentmon-pair-20261116.json` à la racine de la
campagne, SHA-256
`f1b94e65148c9559f80eae75df01a33a0c4a08300b7ab90adb84afa5892d7b52`.
Les CSV et reçus individuels sont dans `presentmon-reference-012/`
et `presentmon-common/` dans le dossier du comparatif.
[Définition des métriques PresentMon](https://github.com/GameTechDev/PresentMon/blob/v1.9.2/README.md#csv-columns).

La phase fixe du lecteur 012 prépare 83 poissons sur les 379 images
de sa trace. Le transport exact du comparatif 1440p ne déclare aucun
profil de poissons. Le moteur commun prend déjà en charge ces profils.
L'alignement de ce contenu doit passer par les données du module et
leurs droits existants. Reçu
`R7-05-tidewater-stationary-012-source-content-20261116.json` sous
`tidewater-performance/runtime-initial-borderless-20261115/`, SHA-256
`de4af26c61e3cec46078827d079cb376d1a7695caaecf0c1e68f8d8d514e0e03`.

La recette épinglée du 012 a permis de retrouver les deux profils. Les
498 fichiers des index ont été relus aux empreintes. Le catalogue du
village retient 78 poissons. Celui de l'étal retient cinq poissons
attachés à l'étal et écarte les 18 emplacements de réserve. Le reçu
conserve les 83 identités d'asset, les 32 scripts sources à exclure du
transport et les références exactes des données. Les 466 fichiers
restants totalisent 6 592 642 octets. Le module de qualification doit
encore reprendre les placements et l'attache de l'étal. Reçu
`R7-05-tidewater-reference-fish-inputs-20261116.json` dans le même dossier,
SHA-256 `b55ac78b9616ae8b54640df22db684946d0cd3d4602c7940c1269916cbf7e492`.

Le préparateur natif du moteur a projeté les 83 poses et leurs trois
niveaux de détail en CPU. L'éditeur de zone commun a écrit et relu les
78 placements du village dans une variante privée, avec leur profil
exact. Le CLI livré avec le 058 admet le transport. Les empreintes de
gameplay et les blobs source de zone restent identiques. L'attache des
cinq poissons de l'étal et le rendu en jeu restent à qualifier. Reçus
`R7-05-tidewater-reference-fish-poses-20261116.json`, SHA-256
`e2c88ce7d56a922addb2d62cbd1906056fc9c492264e4a3a72b79267e5f42a06`,
et `R7-05-tidewater-village-fish-transport-20261116.json`, SHA-256
`4b468b986abf06ffdc18b69992ae7ab64270fa566f82802fb434b64e7e483c9b`.
Le paquet de 302 009 399 octets sur E: sert au prochain essai. Trois
copies temporaires des archives de zone, dont les octets figurent déjà
dans les transports vérifiés, ont été retirées pour 9 374 160 octets.

Le module privé du village n'embarque aucun des quinze fichiers de
rendu de l'étal d'origine. Leurs 108 334 592 octets correspondent aux
empreintes de la recette du 012. La préparation de ces flux de maillage
et des textures en tableaux utilise désormais `source_surface::stall_assets`
dans le moteur commun. L'adaptateur Tidewater conserve l'identité et le
contrôle de son format historique. Le test transféré vérifie toujours
les mipmaps en espace linéaire et sRGB ainsi que l'isolation des couches.
La commande ciblée exécute 21 tests avec succès et en ignore deux qui
requièrent des entrées de qualification explicites. Le transport de
l'étal et le rattachement de ses cinq poissons restent à faire. Reçu
`shared-stall-assets-20261116-test.log.receipt.json` à la racine de la
campagne, SHA-256
`0bc2e0975cdff936339dd0f6e76e6dd0583769b5891b116f121d69ed655220f8`.

Le Studio appelle désormais le montage commun de l'étal. Le moteur
place le maillage, les cinq colliders et les poissons sous la racine
du placement. Le Studio y raccorde ses contrôles d'édition et le
personnage compagnon. Le contrôle natif vérifie qu'un refus des
ressources poisson ne laisse aucun maillage ni collider de l'étal.
Reçu `shared-stall-placement-20261116-test-replay.log.receipt.json`,
SHA-256 `8607c1045ee28992be780d539ec10c8fd5146ba8099909a2b28df05073f87c55`.
La vérification `forge_studio` pour `wasm32-unknown-unknown` avec
la fonctionnalité `web` passe. Reçu
`shared-stall-placement-20261116-studio-web.log.receipt.json`, SHA-256
`f112e3806891cdec0e71f6b739297027b17dac39a8c88fed18d32ef7cc25279b`.
Les exécutables temporaires qui ont produit les poses et la variante
du village ont été retirés après vérification des résultats, soit
32 451 584 octets. Leurs sources sont conservées pour la reprise.

La lecture des champs de maillage et de textures est aussi commune.
L'adaptateur Tidewater transmet la déclaration et les octets d'origine
au moteur. Le test refuse un contenu différent de son empreinte et
le contrôle des mipmaps passe. L'inventaire destiné à l'export rapproche
les quinze fichiers de l'étal, ses notices, le GLB de Joe et sa notice
Rocketbox, ainsi que les cinq identités des poissons attachés. Il
référence les fichiers existants. Reçus
`shared-stall-layout-20261116-test.log.receipt.json`, SHA-256
`85a3480456385cfa4e6a18b09bd705cf8d3d7d5ddd85ff1962e21c3441f16477`, et
`R7-05-08-tidewater-stand-runtime-inputs-20261116.json`, SHA-256
`e95b23617bedb5d7423c27893ef389f65c4cf5f3a8c030484a0560a74e6a028f`.

Le descripteur de l'étal et l'inventaire de ses ressources sont lus
par `forge_assets::source_stall_package`, sans charger Bevy. Le lecteur
Tidewater passe par cette lecture commune. Le moteur utilise les mêmes
contrôles des flux et textures pour construire son maillage et ses mipmaps.
Le contrôle Rust sur les données historiques relit le descripteur,
sept flux de maillage et sept textures pour 108 329 264 octets. Les tailles
et empreintes concordent avec l'inventaire du 012. Le contrôle refuse
un fichier répété et un chemin sortant du dossier source. Reçu
`R7-05-tidewater-stall-portable-layout-20261116.json`, SHA-256
`da0adf56ff2757ad2d96eaec10d481c731265779082cf5c21287375835ddefb5`.
La vérification Release Windows de `forge_tidewater --lib` passe,
reçu `tidewater-stall-portable-layout-check-20261116.log.receipt.json`,
SHA-256 `040509e2a78fb3e8558b8fb125782927e688867340a5873ce90125c62245e62e`.
Le contrôle d'exécution a refusé le retrait du dossier temporaire de la
sonde sur E: (2 385 331 octets).

L'API 16 du module déclare l'étal par zone, épingle son descripteur et
ses quatorze ressources et conserve les notices. Les poissons attachés
peuvent référencer un profil déjà admis dans la zone, avec leur pose
locale. Le réexport d'une origine conserve la déclaration et ses fichiers.
Un module de qualification privé a embarqué l'étal historique et un
placement canonique. L'essai a rouvert l'archive, retrouvé le placement
et relu chaque ressource depuis le dossier installé. Il a aussi refusé
la publication et un octet modifié dont l'empreinte externe avait été
mise à jour sans modifier le descripteur original. Reçu
`source-stall-private-archive-replay-20261116.log.receipt.json`, SHA-256
`5f2eec2187307169044a19ac5e5c4cde62d2f731eb4cc734929663def27bf188`.
La vérification Release Windows du binaire `forge-module` passe, reçu
`source-stall-installed-runtime-check-20261116.log.receipt.json`, SHA-256
`50391acddf0e5ab14852f6160f869753ae89af3714e413b5619c57e890e42b40`.
Cet essai d'archive portait sur l'étal seul. Le jeu charge désormais le
maillage, le matériau complet et les cinq colliders via le moteur commun.
Le même système de poissons gère les poissons du village et ceux qui
suivent la racine de l'étal, avec les distances historiques de détail et
d'ombre. Le lecteur de jeu dédié accepte cette projection. Les anciens
points d'entrée conservent leur refus. Le premier contrôle Release du
raccordement passe, reçu
`source-stall-render-host-check-replay-20261116.log.receipt.json`, SHA-256
`a1e3841848086fe8990f75569b863efcf2259544875f71d1e68b0716adf7995c`.

Le paquet privé de 414 350 769 octets contient les 78 poissons du village,
l'étal historique et ses cinq poissons attachés. La réouverture par le
lecteur natif retrouve les deux profils, les poses et les contenus de zone
et de gameplay d'origine. L'archive a pour SHA-256
`dd090b2e7d8cc66739a006eb65cf6d0fa2ae7a2f84f85a95b4320dcbd06326ca`.
Reçu `R7-05-tidewater-stall-private-candidate-20261116.json`, SHA-256
`9f5afe743da25e8eb6aafe88c4fbc14b63730d61b2804d4bd941b2d775bf82af`.
Le dossier déployé temporaire reprend ce transport et sert directement
au premier essai natif. Le rendu GPU, les collisions en jeu et le trajet
de performance restent à vérifier.
Le scénario repointe la même vue et la même fenêtre de mesure vers les
empreintes du nouveau module, avec une capture après la mesure. Fichier
`scenario-stall-cadence-20261116.json`, SHA-256
`ffcb4e9fd755e42b9ed0ec7b25bc7cbb046d5a07f093f99febaa777ff438229f`.
La relecture confirme les empreintes du module et du profil graphique
du comparatif. Le module charge l'heure solaire fixe de 17 h 45. Les
copies temporaires du scénario, du préparateur et de ses binaires ont
été retirées d'E: après contrôle des sources conservées et du transport,
soit 16 324 501 octets. Le dossier déployé sert au rejeu natif.
La construction Release sans cible explicite a rencontré deux identités
Serde dans le cache hôte. La reprise utilise la cible MSVC explicite et
les options déjà vérifiées, journal
`source-stall-tidewater-runtime-msvc-build-20261116.log`.

La construction MSVC a abouti, reçu
`source-stall-tidewater-runtime-msvc-build-20261116.log.receipt.json`, SHA-256
`8d6ff3e04d12a6dc128477cb1e7b730d3cf280a370342e49a65312caffa81a2d`.
Le premier lancement du module privé s'est arrêté avant la mesure sur
le refus du matériau de l'étal par l'inventaire graphique. Reçu
`stall-preview-graphics-refusal-20261116/module-runtime-report.json`,
SHA-256 `fe97b01ea52001ad21978b138b8b6d8f7686f73b4707e42d779d0d8ebb62a33b`.
Le correctif enregistre les maillages de l'étal et ses ressources dans
l'inventaire GPU. Il vérifie leur propriétaire et attend leur publication.
Le contrôle de projection rapproche aussi le nombre de poissons attachés.

La variante privée de 423 972 906 octets ajoute le modèle de Joe et sa
notice Rocketbox. Elle épingle la scène 0, le clip `idle_neutral_01` et
la pose relative d'origine. Le moteur projette le modèle sous l'étal et
attend le démarrage de l'animation. La zone et son gameplay gardent les
mêmes empreintes. La variante a pour SHA-256
`9ad11e41192b742ab8f9cfb511a2e90a055341563fff6916e16b05de30e23d6c`.
Reçu de préparation `R7-05-tidewater-stall-actor-input-20261116.json`,
SHA-256 `b477494c65bb98b743ea2aa547646e6134425315664a2eab98bf4ed5b501f54b`.
La vérification Release du moteur et de `forge-module` passe, reçu
`source-stall-animated-inventory-check-replay-20261116.log.receipt.json`,
SHA-256 `9f6afb5e1c06ee7b1e8f6542f0d72a0253f3357e7f3cdbeacac06edf8b0cc80b`.
La construction de la variante corrigée utilise le cache commun, journal
`source-stall-animated-inventory-runtime-build-20261116.log`.
Le lecteur natif admet l'archive exacte et relit les deux profils,
les cinq attaches et l'animation déclarée. Reçu
`R7-05-tidewater-stall-actor-native-admission-20261116.json`, SHA-256
`2dec214281ce640f35132d369140e3395e5ecfa9ce4cc45ad694e0eb9627bb1d`.
Les temporaires du vérificateur ont été retirés sur E: après contrôle
des sources conservées et du reçu, soit 12 119 427 octets. La projection
en jeu et la mesure 1440p de cette variante restent à vérifier.
Le binaire avec l'inventaire de l'étal a été construit, reçu
`source-stall-animated-inventory-runtime-build-20261116.log.receipt.json`,
SHA-256 `8b67d27dd6a7eccf180f0eba129f9692379c7e0a9f437258193a6d4f29bb1437`.
Le rejeu avec Joe dépasse le refus de matériau précédent. Il s'arrête
avant la pose de mesure lorsque le collecteur source refuse le maillage
skinné dans sa passe d'ombres rigides. Reçu
`stall-actor-graphics-refusal-20261116/module-runtime-report.json`,
SHA-256 `cb12a3a707dfd1baf5e29a66ba955774ee7501cdffe8e820529e944d25167964`.
Le correctif conserve le maillage de Joe dans la passe Bevy qui prend en
charge la déformation du squelette. Le collecteur source garde son refus
des maillages skinnés sans propriétaire déclaré. La construction du
correctif est suivie dans `source-stall-native-skin-shadow-runtime-build-20261116.log`.
Le rejeu réutilise le cache validé de l'archive et ouvre un profil de
sauvegarde neuf.

Le binaire corrigeant les ombres skinnées a terminé sa construction,
reçu `source-stall-native-skin-shadow-runtime-build-20261116.log.receipt.json`,
SHA-256 `19f3b4480fe3972c897c113784caccc45cae35bfc0f761e341246a4dcb2bd62f`.
Le rejeu a terminé les 420 ticks à la pose de mesure et produit la
capture 1440p. L'inventaire GPU de l'étal et les cinq colliders étaient
prêts. Le contrôle de sortie a relevé 22 objets statiques pour 23 attendus.
Les 407 primitives de l'inventaire excluaient le maillage de l'étal ;
le témoin attendait 412 primitives en comptant ses cinq colliders comme
cinq maillages. Le correctif enregistre le maillage en détail conservé
dans le HLOD commun et vérifie séparément les cinq colliders. Reçus sous
`stall-actor-static-coverage-20261116/` : rapport natif SHA-256
`8432d6c59fc39acd9defbe30faba1339bb5246b2e6f686c41d0d91114a30c605`,
capture SHA-256 `5a5aa1501f28af1b3408b9aa37a55ed5ee0359262251de17a30b52160633dbea`.
La vérification Release passe, reçu
`source-stall-static-hlod-runtime-check-20261116.log.receipt.json`, SHA-256
`5bfd84401a385d97c85792aba35d0846e2c7c00152ddfc75c8fc3bc3b1595a66`.
Le script de rejeu conserve les bornes QPC de PresentMon même en cas de
refus au contrôle de sortie.

Le rejeu suivant sort normalement avec projection prête, cinq colliders
sur l'étal et couverture HLOD complète de 23 objets et 408 primitives.
Le rapport attend les 78 poissons du village, les cinq attaches et le
démarrage de l'animation de Joe. Les 43 observations de caméra concordent
avec la pose et la projection 1440p de Tidewater 012. La construction
Release est épinglée par le reçu
`source-stall-static-hlod-runtime-build-20261116.log.receipt.json`, SHA-256
`f229d8173fcb42b55bd5dda411ccde46214cf0546adb3a7bd9ef4a8a8198c70c`.

Sur les quatre secondes observées, les 168 intervalles PresentMon du
moteur commun donnent 23,74 ms en moyenne et 32,28 ms au p95, contre
10,56 ms et 14,18 ms pour le 012. Les deux mesurent `SyncInterval=1`.
La présentation du moteur commun se partage entre 79 `Composed: Flip`
(médiane 31,24 ms) et 89 `Hardware Composed: Independent Flip` (16,63 ms).
Sur ce dernier mode, seul mode du 012, le temps GPU actif médian atteint
13,02 ms pour le moteur commun et 8,16 ms pour le 012. Reçus sous
`stall-actor-presentmon-20261116/` : `comparison.json`, SHA-256
`ae1676849e743093395c162df89bb930c1b8fef3214cc264cbf82bdada1807fd`,
`present-modes.json`, SHA-256
`fc58248152cc8d60023056f9b064568c50a51ef50debfb05951cf80c7af2de07`,
et `module-runtime-report.json`, SHA-256
`7a69f4e4f2cc54676f82d53177ac35bb9f463320eec67fd41cf86495816986bd`.
La parité de performance reste ouverte.

Le journal compte 9 426 entrées dans les trois cascades directionnelles
de Bevy et signale un récepteur hors des familles reconnues pour les ombres
source. Les matériaux de l'étal et des poissons n'étaient pas reconnus
par ce contrôle alors que leurs shaders lisent l'éclairage source. Le
correctif les reconnaît sans changer la règle pour les récepteurs natifs.
La reconstruction s'est terminée, reçu
`source-stall-fish-sun-routing-runtime-build-20261116.log.receipt.json`,
SHA-256 `430c12140e0b6185ddd8f69178ad75825d197a8c4cc15fa1d261ab5649e4b430`.

Le rejeu corrigé sort normalement, avec la même pose 1440p, le même
module et les cinq colliders de l'étal. Le journal affiche `receiver=None`
et 1 060 entrées directionnelles Bevy, soit 8 366 de moins. Sur 185
intervalles, le temps de présentation descend de 23,74 à 21,53 ms en
moyenne et de 32,28 à 27,67 ms au p95. Les deux modes de présentation
restent présents. En `Hardware Composed: Independent Flip`, la médiane
GPU active reste à 13,20 ms, contre 13,02 ms avant correction et 8,16 ms
sur le 012. Les deux relevés asynchrones du nouveau rejeu situent la
passe opaque à 3,99–4,00 ms et le graphe caméra à 10,49–11,76 ms.
Ils orientent le diagnostic sans isoler le coût des objets ou du terrain.
Reçus sous `stall-actor-source-sun-20261116/` : `comparison.json`,
SHA-256 `082e93b84c5f6c6585edf1c9e3d7d8651abe04338f68c756673d154688f557e8`,
et `module-runtime-report.json`, SHA-256
`9f24ae54ff762ffff236f6dac1354d1f74ebaa1c672402a14ac9b216feae3743`.
La parité de cadence et de coût GPU reste ouverte.

Le moteur commun écarte désormais toute la liste de casters directionnels
Bevy du soleil de scène lorsque le contrôle d'admission confirme que les
récepteurs visibles sont couverts par l'éclairage source. Les 1 060 entrées
restantes du rejeu précédent sont visées par ce changement. Les cascades
gardent leur identité pour que le cache de rendu retire les anciennes
entrées. Un matériau natif ou inconnu, un soleil volumétrique ou une
indisponibilité du graphe source conserve le chemin de repli. Les ombres
ponctuelles et spot suivent leur routage habituel. Le rejeu GPU du correctif
reste à faire. La comparaison ciblée avec le paquet sans Joe a été
reportée lorsque Unity est passé actif avant le lancement.

Le test ciblé de routage passe sous Windows. Reçu
`source-sun-unused-casters-regression-20261116.log.receipt.json`, SHA-256
`6292816a2d0b676ff7565fc918b5e4b64a99d70048d15a9b9a6a7072b1ecaa4a`.
Le journal de reconstruction du lecteur Release est
`source-sun-unused-casters-runtime-build-20261116.log`.

<a id="gate-7-tidewater-ombres-soleil"></a>

Le lecteur Release termine le rejeu à 1440p avec le même paquet, les mêmes
réglages et la pose historique. Les 42 observations de caméra dans la
fenêtre sont prêtes et concordent avec la pose attendue. La projection
confirme les cinq colliders de l'étal et les ombres source prêtes, avec
393 casters opaques et cinq lots de végétation. Les listes directionnelles
Bevy passent de 1 060 entrées à zéro. Les six faces d'ombre ponctuelle
conservent leurs 55 entrées et leurs 38 lots.

Sur 206 présentations dans la fenêtre de quatre secondes, sans abandon,
la moyenne passe de 21,53 à 19,31 ms et le p95 de 27,67 à 23,29 ms.
En `Hardware Composed: Independent Flip`, la médiane GPU passe de
13,20 à 11,15 ms, contre 8,16 ms pour le 012. En `Composed: Flip`,
elle passe de 14,19 à 12,30 ms. Les deux modes sont présents dans les
rejeux communs et leurs proportions diffèrent. Les deux relevés
asynchrones situent `bin_unpacking` à 0,23 ms, contre 0,83–0,84 ms
avant ce changement. La passe opaque reste autour de 4 ms. La capture
présente encore les capsules bleues visibles dans le témoin commun.
La parité avec le lecteur 012 reste ouverte.

Quatorze fichiers vérifiés par SHA-256 sous
`stall-actor-unused-sun-casters-20261116/` (7 225 701 octets), dont
`comparison.json` (`b11486c5b73549492305fa47329a56308b53dea443424094d4d6535a2bfc0f53`)
et `module-runtime-report.json`
(`a30c80ecc71eda54b3fd2757edfff9084d3ea89588582549b2a4b348d9510ef4`).
Reçu `source-sun-unused-casters-runtime-build-20261116.log.receipt.json`,
SHA-256 `185798f606f68f42f65add400f773afa129e86a3f06264c636b6861f41eaae5e`.
Les copies et réglages temporaires de ce rejeu ont été retirés d'E:
(7 211 373 octets). Le script de rejeu et le cache d'extraction commun
restent disponibles pour la comparaison suivante.

Le diagnostic de réfraction réutilise le même binaire et le même module.
Il passe uniquement `water_refraction_capture` de 1 à 0 dans les réglages
du rejeu. Les couples vue-passe de rendu direct passent de trois à deux
et les entrées préparées de 12 720 à 12 654. Les compteurs d'ombres
concordent. En présentation indépendante, le GPU actif médian passe de
11,15 à 10,46 ms, sur 84 puis 114 présentations. La passe opaque conserve
un relevé de 4,03–4,13 ms. Bevy réutilise le même chemin de diagnostic
pour ses passes opaques, et les relevés asynchrones ne donnent pas une
décomposition par vue. La réfraction reste active pour la comparaison
de qualification : le reçu `reference-012/camera.perf-proof.json`
confirme sa disponibilité dans les 2 303 relevés historiques.

Reçus sous `stall-actor-refraction-isolation-20261116/` :
`comparison.json` (`a6e0ea48bbb2d56aef3889513edaeab23a9782743a9934245f568b8d156cf9d3`)
et `module-runtime-report.json`
(`7d412e8f5f7cab8ab9e3320b40855f10acd867338d59f10faa4440969f9cd78e`).
Treize fichiers vérifiés sur F: occupent 1 079 946 octets. Les copies
d'E: et les sorties temporaires de diagnostic retirées occupaient
7 240 720 octets.

Le shader commun des surfaces vérifie désormais la présence de lumières
ponctuelles ou spot dans la cellule Bevy avant de préparer leurs
paramètres de réflexion. Le rejeu Release conserve la réfraction, les
cinq colliders, les 55 entrées ponctuelles et les mêmes réglages. Les
42 observations de caméra concordent. En présentation indépendante,
la médiane GPU est de 10,80 ms sur 126 présentations contre 11,15 ms
sur 84 pour le témoin. En mode composé, elle est de 12,15 ms contre
12,30 ms. La passe opaque varie entre 3,96 et 4,47 ms sur les deux
relevés asynchrones. Ce rejeu ne confirme pas un gain propre à la passe
opaque et ne qualifie pas la parité avec le lecteur 012.

Reçus sous `stall-actor-native-light-clusters-20261116/` :
`comparison.json` (`a82564b163ba4a2b83ebb32296f3a8199b81dd0051a1298575d60ae4526c5f65`)
et `module-runtime-report.json`
(`0fffbdf59b71818148d433c8b1adb40ddcb7474cb9250de10cc1e6b8744c46c4`).
Reçu `source-empty-native-light-clusters-runtime-build-20261116.log.receipt.json`,
SHA-256 `6b9e737060ae61f3d1066744f7c7892a5f3e8d3f8721d6dd7cf572cda2c6f878`.
Onze fichiers ont été vérifiés sur F: (1 055 057 octets) et 7 198 182
octets de copies et de sorties temporaires ont été retirés d'E:.

La relecture des quatre CSV PresentMon reprend le décodeur de qualification et
vérifie les empreintes, le PID, la chaîne de présentation et la fenêtre
QPC de chaque mesure. Pour le dernier rejeu avec réfraction, 103 des
105 intervalles au-dessus de 16,67 ms sont en `Composed: Flip`. En
`Hardware Composed: Independent Flip`, deux intervalles sur 126 dépassent
ce seuil et le p95 est de 15,86 ms, contre 13,87 ms pour le 012
avec zéro dépassement sur 378 intervalles. Les deux modes décrivent
l'affichage observé. La comparaison en mode indépendant conserve un
écart GPU et les révisions de scène diffèrent.
Reçu `R7-05-present-mode-cadence-readback-20261116.json`, SHA-256
`f0d5b65760a3ddf64241e57ff68e9329788dd45e95d64673daac1fe159d6968a`.

La relecture du profil 012 et du rapport commun confirme l'égalité
exacte des 20 réglages graphiques, dont la réfraction activée. Les CSV
recalculés à 1440p en présentation indépendante, `SyncInterval=1`,
donnent 8,16 ms de GPU actif médian sur 378 présentations pour le 012
et 10,80 ms sur 126 présentations pour le moteur commun. Les p95 des
intervalles de présentation sont de 13,87 et 15,86 ms.

La première phase du profil historique relève une médiane de 2,87 ms
pour l'opaque. Les deux relevés asynchrones du jeu commun donnent
3,96 et 4,47 ms. Le diagnostic sans réfraction relève encore 4,03 et
4,13 ms. Ces relevés ne sont pas appariés aux lignes PresentMon ni
étiquetés par vue. Le compteur commun de 313 récepteurs terrain égale
les 169 cellules et 144 tuiles HLOD de la partition. Le code masque
les projections de la partition qui appartiennent au CDLOD et le
compteur générique comptabilise aussi les matériaux masqués. Le
CDLOD rapporte 66 tuiles actives. Le prochain rapprochement doit
identifier les soumissions opaques par vue et par propriétaire
sur la même scène.
Reçu `R7-05-tidewater-opaque-graphics-readback-20261116.json`,
SHA-256 `352c9752721537c1bdbc60b9b47fe24ebd83b12a5737bedfbd55c91207e443a8`.

Le profilage commun publie maintenant `runtime_camera_mesh_bins`.
Il rattache les lots directs opaques et alpha-testés, ainsi que ceux
de la prépasse, à leur caméra extraite et à son image. La caméra de
réfraction est identifiée par son marqueur moteur existant. Les vues
fenêtrées et les autres vues hors écran ont aussi leur propre groupe.
Ce relevé décrit le travail préparé avant le passage GPU. Le contrôle
Cargo du moteur et de `forge-module` passe.
Reçu `tidewater-camera-mesh-bin-module-check-20261116.log.receipt.json`,
SHA-256 `cb7fbee90f7375da0ffba72db569fc887ace1c1cd5ff75c4aec6c71df413ea0b`.

Le rejeu Release retrouve 43 observations de la pose de référence en
1440p. Sur deux images prêtes, les compteurs par caméra recomposent
exactement le total direct Bevy. À l'image 532, la fenêtre prépare
124 lots et 12 654 instances et la réfraction un lot et 66 instances.
Les deux caméras totalisent 125 lots et 12 720 instances. Le diagnostic
attribue ces entrées à chaque vue. Le coût GPU de chaque vue reste à
mesurer. En présentation indépendante, le moteur commun relève
11,06 ms de GPU actif médian sur 93 présentations, contre 8,16 ms pour
le 012. Les révisions de scène diffèrent toujours. Le rejeu confirme
la fermeture normale et ses cinq colliders d'étal.

Reçu `stall-actor-camera-bins-20261116/camera-mesh-bin-readback.json`,
SHA-256 `0f5f4e82eee169ade225d5c33be24680cd2e29e71370d9bd7594aa9b3423c9e6`.
Les dix fichiers de preuve ont été vérifiés sur F:. Le dossier de
rejeu temporaire de 7 260 103 octets a été retiré sur E:.

Le moteur commun réserve désormais la conversion des matériaux terrain
génériques aux surfaces qui en ont besoin. Quand le CDLOD admis assure
le rendu avec les ombres source, les cellules physiques qu'il possède
gardent leur matériau d'origine. Une conversion déjà appliquée au
démarrage est retirée pour ces seules cellules. Le rapport d'éclairage
compte les tuiles CDLOD actives dans les récepteurs terrain et garde le
repli générique quand le propriétaire CDLOD est absent.
Le contrôle Cargo de `forge-module` passe.
Reçu `tidewater-cdlod-hidden-terrain-receivers-module-check-20261116.log.receipt.json`,
SHA-256 `d6fc6e29615a69a7a6e25da590f836f94b4e46c47f4b80f8dd9d47e642793227`.

Le rejeu Release confirme le passage de 313 récepteurs terrain
comptabilisés aux 66 tuiles CDLOD actives. Les 169 cellules résidentes,
les 144 tuiles HLOD, les 404 récepteurs de modèles et les cinq
colliders de l'étal concordent. La projection et la partition physique
sont prêtes. Les 43 observations de caméra retrouvent la pose de
référence à 1440p avec les mêmes réglages graphiques. Le GPU actif
médian en présentation indépendante est de 10,99 ms sur 87
présentations, contre 11,06 ms sur 93 pour le rejeu précédent et
8,16 ms pour le lecteur 012. Le coût GPU par vue reste à isoler.

Reçu `stall-actor-cdlod-hidden-receivers-20261116/cdlod-hidden-receivers-readback.json`,
SHA-256 `8abfff980163d31c7494372ed00cb7d03c8fdf9556dd524664bee23d04e004f1`.
Les dix fichiers de preuve ont été relus sur F:. Le dossier de
rejeu temporaire de 7 262 676 octets a été retiré sur E:.

Un essai du shader commun a écarté les lumières ponctuelles et spot
dont l'atténuation de portée Bevy était exactement nulle au fragment,
avant les ombres et le calcul PBR. Le contrôle Cargo de `forge-module`
et la composition WGSL ciblée avec Bevy 0.19.1 ont passé.
Reçu `tidewater-native-light-range-module-check-20261116.log.receipt.json`,
SHA-256 `46c5503fe3e28696e0717975be263e86705833cddf4924fd754d2f5283eb3350`.

Le rejeu Release à 1440p a retrouvé les 43 observations de la pose de
référence et s'est terminé avec la projection prête. Les six faces
d'ombre ponctuelle et les inventaires du terrain et des modèles
concordent avec le témoin. La médiane de GPU actif en présentation
indépendante est de 11,08 ms pour l'essai et 10,99 ms pour le témoin.
Les relevés asynchrones de la passe opaque restent autour de 3,96 ms.
La garde expérimentale a été retirée du checkout après cet essai.
Reçu `stall-actor-native-light-range-20261116/native-light-range-readback.json`,
SHA-256 `c7bd3e76ce0d00ea614267b789d8783343e792b85c26d39c1304d4bcf639b2e2`.
Les dix fichiers de preuve ont été relus sur F:. Le dossier de
rejeu temporaire de 7 251 899 octets a été retiré sur E:.

<a id="gate-7-tidewater-opaque-par-camera"></a>

Le journal du rejeu CDLOD contient trois relevés GPU. Le premier est
vide et les deux suivants donnent 3,958 et 3,956 ms pour le pass opaque,
sous `render/main_opaque_pass_3d/elapsed_gpu`. Ce chemin commun ne
permet pas de rattacher la mesure à l'une des deux caméras. À titre de
diagnostic, sans caméra de réfraction, les deux relevés renseignés
donnent 4,028 et 4,125 ms pour le pass opaque. Le moteur
peut désormais ajouter le rôle et l'identifiant de la caméra au chemin
du pass Bevy existant avec `FORGE_RUNTIME_CPU_PROFILE=1` et
`FORGE_RUNTIME_GPU_VIEW_PROFILE=1`. Dans ce mode de diagnostic, le
rendu 3D par caméra s'exécute sur un seul thread pour que Bevy rattache le
pass à la bonne caméra. Les mesures de cadence de référence gardent
le profilage par vue désactivé. Le contrôle Cargo de `forge-module`
passe.
Reçu `tidewater-opaque-view-profile-20261116-verified-module-check.log.receipt.json`,
SHA-256 `fd828e1eac0040689aea3bf80fbb5f58acd6fec12c1d09449c332916943afc8a`.
Le build Release a terminé. Le lecteur de qualification a pour SHA-256
`228288b9f55a23b69c69c36cb5b5d0812e47768daf8c1e40aea75309fb1d09fa`.
Reçu `tidewater-opaque-view-profile-20261116-release.log.receipt.json`,
SHA-256 `5e6a955a425f9ed68092e1c1fd3c3d1ac9b9449a4da78336b5fbd546c2ef6a19`.

Le rejeu du module avec Joe a retrouvé les 43 observations de caméra à
la pose 1440p et s'est terminé avec la projection prête et les cinq
colliders de l'étal. Les deux relevés renseignés donnent de 3,78 à
3,91 ms pour le pass opaque de la caméra principale et de 0,071 à
0,073 ms pour celui de la réfraction. L'âge des mesures va de 12,89
à 13,55 ms. Le coût opaque mesuré se concentre dans la vue principale
sur cette scène. Le profilage modifie l'ordonnancement du rendu, et
la capture mélange deux modes de présentation. Elle ne
qualifie pas la parité avec le lecteur 012.

Reçus sous `stall-actor-opaque-view-profile-20261116/` :
`opaque-view-readback.json` (SHA-256
`c878fb9150d576c9861cc050b55f444f353df7e2ad2b284fbe9af61c5baf2cd5`)
et `evidence-inventory.json` (SHA-256
`f24ec7ad6370bc3080e1f4bc198db09b6c1e8c2881127eb1ab795eb2cf2359ba`).
Les douze pièces vérifiées sur F: totalisent 1 116 608 octets. Les
16 fichiers temporaires du rejeu sur E: occupent 7 215 057 octets.
Le retrait du dossier a été refusé par la politique d'exécution.

Le rejeu suivant réutilise ce même binaire et les mêmes réglages à la
même pose, avec le module antérieur à l'ajout de Joe. La relecture des
deux archives retrouve les mêmes charges utiles communes. Le manifeste
diffère seulement pour le personnage animé et sa notice. La projection
est prête et les cinq colliders restent présents.

Avec Joe, les deux relevés opaques de la vue principale donnent 3,78 et
3,91 ms. Sans Joe, ils donnent 3,93 et 3,89 ms. La réfraction reste
autour de 0,07 ms dans les deux cas. Retirer Joe ne rapproche pas la
passe principale de la médiane historique de 2,87 ms. Ces quatre
relevés par vue proviennent de deux exécutions distinctes et gardent
leur portée de diagnostic.

Reçus sous `stall-opaque-without-actor-20261116/` :
`opaque-actor-comparison.json` (SHA-256
`35c6a499f5eea2d324e7af4829881a7c09825a1f1d73539c8c6efdb98e94c67d`)
et `evidence-inventory.json` (SHA-256
`3175c5a028168efd52f5fb98eb19c7405ac9289e38f01d5b3233e9735166c566`).
Les dix pièces vérifiées sur F: occupent 1 103 489 octets. Les
2 073 fichiers du dossier de rejeu, dont le cache du module sans Joe,
restent sur E: après refus du retrait par la politique d'exécution.
Leur volume est de 421 612 444 octets.

L'essai du filtre d'ombres source précompose ses trois disques de
Vogel et applique une rotation par disque. Les boucles conservent leurs
8, 12 et 5 échantillons, leurs rayons et leurs phases. La simulation
des calculs en `f32` sur 4 097 phases par disque
mesure un écart de position maximal de `1,968e-6` sur le disque unité.
Naga valide les fragments dérivés pour l'éclairage des surfaces, le
terrain et les objets. Le contrôle Cargo de `forge-module` passe.
Reçu `R7-05-tidewater-vogel-offsets-check-20261116.json`, SHA-256
`97ac9567ada5cee64f8647dafa9f64c40c3d56d6fbda07b86cbb40a433bd5795`.

Le candidat Release retrouve 43 observations de la même pose, la
projection prête et les cinq colliders de l'étal. Les relevés de la
passe opaque principale donnent 3,86 et 3,90 ms contre 3,78 et 3,91 ms
pour le témoin. En présentation indépendante, la baisse de la médiane
GPU de 10,81 à 10,74 ms s'accompagne d'une hausse de l'intervalle de
présentation de 14,03 à 14,32 ms, sur 104 puis 52 présentations. Les
captures retrouvent la même scène, à des instants différents de ses
animations. Le rejeu ne confirme aucun gain, et le filtre expérimental
a été retiré du checkout. Le binaire Release du cache reste celui de
l'essai jusqu'à sa prochaine reconstruction.

Reçus sous `stall-actor-vogel-offsets-20261116/` :
`vogel-comparison.json` (SHA-256
`1ccee48ffaff95df8c7832a27a90b7d6b640f05bf2434d4f262fa47f71d9a3a9`)
et `evidence-inventory.json` (SHA-256
`fbe3096789c065a4d935629949fffcad80d29d24453290b327dee3941bb602f6`).
Les treize pièces vérifiées sur F: totalisent 1 981 646 octets.

La relecture du 17 novembre rapproche les deux échantillons du profilage
par vue avec les anciens relevés. Sur ces deux échantillons, le graphe de
caméra varie de 2,120 ms et sa passe opaque principale de 0,128 ms. La
préparation des lots relevée est à 0,222–0,232 ms, contre 0,166–0,167 ms
dans le 012 et 0,876 ms dans une ancienne trace du moteur commun. Les
mesures sont asynchrones et les captures utilisent des révisions
différentes. La durée restante du graphe n'est pas attribuée.
Reçu `tidewater-performance/R7-05-camera-scope-cross-readback-20261117.json`,
SHA-256 `53d3ba19329f08231bc82a0d8b5f78b90dddb0ef06814299cd5444237547e8f3`.

Le rapprochement des libellés historiques `tidewater_*` avec les passes
`source_*` du moteur commun exploite les mêmes captures Vulkan. Parmi
les six passes ajoutées au rapprochement, les ombres d'objets présentent
le plus grand écart défavorable. Avec Joe, le moteur commun mesure
0,704 et 0,717 ms, pour des médianes historiques de 0,442 et 0,415 ms.
Sans Joe, il mesure 0,552 et 0,715 ms. Le rattrapage du coût des ombres
requiert de rapprocher les émetteurs et les cadences de cascades sur le
même trajet. Les différences de charge et l'âge des relevés excluent une
attribution de l'écart GPU global à ces seules passes. Aucune mesure
isolée des embruns n'est disponible dans ces deux traces communes.
Reçu `tidewater-performance/R7-05-renamed-source-gpu-scope-readback-20261117.json`,
SHA-256 `4a3fa78dbc6035bbdd0e0de129dbc749680bff31e1387fb569fc297664ef83a8`.
La relecture ajoute 6 613 octets à la campagne et réutilise les mesures
existantes.

Les rapports des deux rejeux affichent 399 et 401 soumissions avec
respectivement `[399, 200, 101]` et `[401, 201, 101]` exécutions par
cascade. La cadence 1/2/4 fonctionne. Les deux scènes conservent 393
émetteurs opaques et cinq lots végétaux. Le moteur commun calcule
désormais les bornes des maillages opaques à l'admission et sélectionne
les émetteurs à partir des quatre plans latéraux de chaque cascade.
Les objets au bord de la cascade sont conservés. Le moteur réutilise
le bundle GPU quand sa liste d'émetteurs est identique.
Les trois tests ciblés des émetteurs opaques passent sous Windows.
Reçu `source-opaque-cascade-culling-20261117.log.receipt.json`,
SHA-256 `c0c8ff5648ad50de785921335d7c7c20e6b5d5b3728d10a5be5688d8d5404226`.
La compilation Release de `forge-module` réussit en 22 min 01 s sur le
cache partagé. SHA-256 du binaire
`d96443085191a679ca4c93bda2c26e141c32bf5b2d2799eed27ee6903c3faf17`
et du reçu Release
`facc287f933f3f72535dd3a289405adffa6c85b54d123aeb953b71885c1add99`.
Le paquet Tidewater et le scénario précédent ont été relus par
SHA-256. Reçu de préparation
`tidewater-performance/runtime-initial-borderless-20261115/R7-05-source-opaque-cascade-culling-release-preflight-20261117.json`,
SHA-256 `074d655786c55792ef8182cca21715b7b0fffda896dd58f0377946c8a5ce4058`.
Le pilote temporaire `Replay-OpaqueCulling-20261117.py` réutilise le
rejeu PresentMon existant. Son contrôle d'entrée retrouve le paquet,
les réglages, le scénario et le cache du rejeu. Le témoin du 16 novembre
utilise le même affichage sans bordure et conserve l'ordonnanceur
standard du rendu. SHA-256 du pilote
`15e6469a29cff19a9ae2440001e94901ab96cad31a0e25790d7b4aebc961a63d`.

Le jeu exporté se ferme avec la projection prête et les cinq colliders
de l'étal. Les 43 observations de caméra correspondent à la pose 1440p.
Le rapport conserve 393 émetteurs opaques canoniques et la cadence des
cascades, `[503, 252, 127]`. La capture montre le village et les ombres
des éléments visibles à cette pose.

En présentation indépendante à `SyncInterval=1`, le GPU actif médian
est de 10,68 ms sur 273 images, contre 10,99 ms sur les 87 images
indépendantes du témoin du 16 novembre. Les deux relevés de la passe
d'ombres des objets donnent 0,55 et 0,69 ms, contre 0,72 et 0,87 ms.
Le p95 du temps entre présentations passe de 17,84 à 17,51 ms.
La moyenne passe de 13,98 à 14,58 ms. Les deux candidats utilisent des
binaires différents. Le rejeu indique une baisse du temps GPU observé,
avec un temps moyen entre présentations plus élevé. Le lecteur 012
reste à 8,16 ms de GPU actif médian et la parité reste ouverte.
Reçu `stall-actor-opaque-culling-20261117/opaque-culling-readback.json`,
SHA-256 `c02afc64e47a80411369692a833b0f38e4d4c9ca016e166eb8abcf0d693c3b67`.
Les 13 pièces conservées sur F: totalisent 6 910 123 octets.
Les copies et sorties dérivées retirées sur E: totalisent 7 326 785 octets.

La relecture des fenêtres PresentMon existantes à la même pose et en
présentation indépendante compte 28 intervalles au-delà de 16,67 ms
sur 273 pour le moteur commun et zéro sur 378 pour le lecteur 012.
Sur ces 28 intervalles, la médiane de `msGPUActive` est de 11,74 ms,
contre 10,60 ms pour les 245 autres. La médiane dans l'appel Present
passe de 0,107 à 0,125 ms. Le p95 du GPU actif est de 12,12 ms sur
l'ensemble du rejeu commun et de 9,43 ms dans le 012. Ces colonnes
se recouvrent. Elles situent l'écart entre les mesures et ne suffisent
pas à l'attribuer à une passe ou à la boucle CPU.
Reçu `stall-actor-opaque-culling-20261117/present-tail-readback.json`,
SHA-256 `2b08c09cffdfe881511fd64cdbf2fabf03bf4717ea5a3642343386456346d51f`.

La relecture de l'archive du lecteur 012 vérifie l'exécutable, les
en-têtes des paquets et le document auteur. Le module du dernier rejeu
référence les mêmes 822 fichiers de surfaces d'objets et les 16 fichiers
de l'étal par empreinte. Le terrain, les corrections de relief et de
peinture, les couches et les blocs d'instances végétales correspondent.
Les 21 placements d'origine conservent leurs modèles et leurs poses en
X/Z. Quinze ont une altitude différente, jusqu'à 0,519 m. Le document
actuel déclare en plus les 78 poissons du village, l'étal et le champ
d'herbe. Le lecteur historique chargeait déjà les poissons par son
chemin dédié. Les entrées simulées d'océan et d'environnement diffèrent
dans les paquets. Le rapprochement réduit les différences à examiner
en gardant ouvertes la résidence, les soumissions GPU et la parité
de rendu. Reçu `R7-05-tidewater-012-scene-input-reconciliation-20261117.json`,
SHA-256 `0d6678d37fa28f67f90a2453d58c65c53aaee8d1481ddf8a37dc1f5e212d9804`.
La campagne conserve ce reçu de 4 084 octets.

La relecture des deux paquets ramène les 250 écarts d’empreinte de
l’océan à 246 paquets de paramètres et de trames d’amorçage FFT
(116 608 octets) et quatre index. Les neuf shaders FFT concordent.
Les deux versions partagent la taille 256 et les quatre cascades.
Tous les fichiers de ressac et de surface hors index ont une empreinte
présente dans le paquet du 012. Le module courant amorce l’océan avec
124 trames acquises avant sa continuation. Ces données circonscrivent
le changement de l’amorçage et laissent le coût en jeu à mesurer.
Reçu `R7-05-tidewater-ocean-input-closure-20261117.json` (3 147 octets),
SHA-256 `241edca31b5e3d601d8a289a980d8ec9c8be69bad4623c6998da2004e84fc75a`.

Le moteur commun et la sonde de référence réutilisent maintenant les
18 vues de mip FFT tant que les identités des deux textures GPU
concordent. Un changement de texture recrée les vues. L’ordre et les
dimensions des dispatchs restent identiques. Le contrôle Cargo Windows
couvre `forge-module` et `tidewater_ocean_spectrum_probe` en une exécution.
Reçu `source-ocean-retained-mip-views-20261117.log.receipt.json`,
SHA-256 `3e1ac97289f3bff5dde63c42a9cfda3caf542a76a12d9b51fa9610fa730266a1`.
Le journal et le reçu occupent 1 464 octets sur F:. Aucun rejeu lancé.

<a id="gate-7-tidewater-runtime-060"></a>

Le runtime exact du kit Windows 060, avec les vues FFT mises en cache,
a rejoué le scénario sans nouveau build. Le module, les réglages et
la pose à 1440p concordent avec le candidat CDLOD précédent. Les
42 observations de caméra sont prêtes et les listes d’ombres sont
identiques. En présentation indépendante à `SyncInterval=1`, les
284 puis 287 présentations donnent 14,03 puis 13,86 ms en moyenne,
16,14 puis 15,69 ms au p95 et 10,47 puis 10,22 ms de GPU actif médian.
Le lecteur 012 reste à 10,56 ms en moyenne et 14,18 ms au p95 sur sa
scène historique. Les deux exécutions du moteur commun utilisent des
binaires différents. La mesure n’isole pas le coût des vues FFT.

Sous `windows-reviewed-studio-20261117-060/tidewater-runtime-060/`,
reçu `runtime-060-tidewater-readback.json`, SHA-256
`fc1825158782c907d1cfab767758bb3e33f8ce7d1d43c03530fcbd809f1afaef`.
Les 12 pièces vérifiées occupent 6 769 729 octets sur F:. Les
sorties temporaires ont été retirées après relecture
(7 285 949 octets sur E:).

<a id="gate-7-tidewater-scene-012-060"></a>

Une copie de diagnostic du module a été rapprochée du document auteur
archivé du 012 par les commandes natives d’édition de zone. La pièce
absente de la référence a été retirée et 15 hauteurs ont été restaurées.
Les 21 placements communs ont alors les mêmes modèles et transformations.
Les 2 029 autres fichiers du paquet, dont les données de jeu, gardent
leurs empreintes. Le module de qualification reste intact.

Le même runtime 060 rejoue la pose à 1440p avec les mêmes réglages.
Les 43 observations de caméra sont prêtes. Le rapport retrouve les
83 poissons, les cinq colliders de l’étal et l’animation de Joe.
La copie ne présente aucun groupe d’ombres ponctuelles dans le journal.
Les 50 instances de trois cascades Bevy comptées sur la copie sont
antérieures à l’activation du routage du soleil commun. Les échantillons
suivants comptent zéro candidat directionnel sur les deux scènes.

| Scène | Intervalle moyen | Intervalle p95 | GPU actif médian |
| --- | ---: | ---: | ---: |
| Module complet, runtime 060 | 13,86 ms | 15,69 ms | 10,22 ms |
| Copie rapprochée, même runtime 060 | 13,02 ms | 17,27 ms | 8,93 ms |
| Lecteur historique 012 | 10,56 ms | 14,18 ms | 8,16 ms |

Les intervalles au-delà de 16,67 ms passent de 2 sur 287 à 25 sur 306.
Sur la copie, 22 des 25 se concentrent dans les deux premières secondes
de la fenêtre mesurée. Le temps médian passé dans l’appel Present est de
0,11 ms sur ces lignes. Le GPU actif médian sur la ligne qui précède
chaque intervalle long est de 7,38 ms. Les durées décrivent des étapes
distinctes de la présentation et n’attribuent pas la cause de l’écart.
La parité reste ouverte.

Sous `windows-reviewed-studio-20261117-060/tidewater-012-aligned-060/`,
reçu `aligned-012-readback.json`, SHA-256
`709e457d0ccb0cd3144a2fdc7402a8f790e50d2fe95a91d4e7d370fdd720798b`.
Les 15 pièces vérifiées occupent 6 844 885 octets sur F:, dont les deux
pilotes de reproduction. Les 867 325 814 octets temporaires ont été
retirés sur E: après relecture.

La variante rapprochée a été reproduite avec ces mêmes pilotes. Seul
l’outil de préparation a été compilé, en réutilisant les bibliothèques
Rust du cache commun et les dépendances hôte et cible. Le paquet obtenu
retrouve le SHA-256 `96e726e4939187df502b663c0c47c38ef837c0357444342c4b28d6d56242cb6f`
et le reçu de préparation exact. Les 15 hauteurs sont restaurées et
2 029 fichiers sont conservés à l’octet près. Le CLI 060 accepte le
paquet à une zone avec `orpmod-inspect`. Les deux binaires à mesurer
sont présents aux empreintes du lecteur 012 et du moteur
`0f867669aed89d607dc791bb781ef7cd8ec481d679541e3fbb9f816151b96496`.
Les huit fichiers de reprise occupent 437 404 328 octets sur E: dans
`.build/tmp/gate7-tidewater-012-aligned/`. Aucun nouveau passage n’a été
lancé sur cette copie, et le verdict de parité reste ouvert.

<a id="gate-7-tidewater-paire-sans-profilage"></a>

Une paire ordonnée a ensuite exécuté le 012 et le moteur commun sur la
variante rapprochée, sans les options de profilage CPU, GPU par caméra
et intervalles internes. Le 012 conserve son scénario natif de caméra.
La pose correspond sur les 42 relevés du moteur commun, avec projection
prête. Les 50 et 47 observations de fenêtre retrouvent chaque jeu au
premier plan sur le même écran à 2 560 × 1 440 et 144 Hz.

Entre 1 et 4 secondes dans les fenêtres de mesure, les deux traces sont
en mode composé avec `SyncInterval=1`. De 012 au moteur commun, la
moyenne entre appels `Present` passe de 20,14 à 17,96 ms, le p95
d’affichage de 32,79 à 31,84 ms et le GPU actif médian de 12,80 à
11,33 ms. Une puis deux présentations sont écartées. Le moteur commun
avait aussi une tranche de présentation indépendante avant la fenêtre
retenue. Les modes et la scène doivent être considérés pour toute autre
comparaison. Cette paire ne qualifie pas la parité sur un trajet répété.

Sous `windows-reviewed-studio-20261117-060/tidewater-012-aligned-unprofiled-pair-20261117/`,
reçu `result.json`, SHA-256
`2498859d70f412c2a389219da6c19dcae0908498203571658a4722cf2d5eadad`.
Les 25 pièces vérifiées occupent 12 941 996 octets sur F:. Windows ne
liste plus de session de collecte Gate 7. Le contrôle d’exécution a
refusé le retrait des 36 799 674 octets de la session de travail 012 sur E:.
Le cache du moteur commun de 421 349 033 octets reste disponible dans
le dossier de travail pour la reprise de la comparaison.

Le parcours historique de navigation du 012 passe en sprint par cinq
points de la plage à la végétation intérieure. Le scénario commun accepte
maintenant `sprint: true` dans `move_to` et transmet l’entrée au contrôleur
existant. L’option est désactivée dans les anciens scénarios. La copie
`scenario-012-inland-sprint.json` reprend les cinq coordonnées et la
tolérance de 0,7 m, avec une préparation bornée et une sortie normale.
SHA-256 `5dfbfaf2ef70159e1329e73adbee2ecfe13d4d3e74b855b1591d44d008e35a7f`.
Le fichier de 1 395 octets reste dans le dossier de travail E: du paquet
rapproché. Le trajet et sa cadence restent à observer après assemblage.

La vérification Release Windows de `forge_engine` a terminé avec le
code 0. Reçu `scenario-move-to-sprint-check-20261117.log.receipt.json`,
SHA-256 `48d48278b9a5f50c4db055465bab8a5d169a451efbd1dd20fd1e9bd4d3ac0f80`.
Le journal et le reçu occupent 1 227 octets sur F:. Aucun jeu lancé.

Le build du runtime Windows Release a terminé en 13 min 29 s sur le
cache commun. Les 1 745 entrées recensées ont conservé leurs empreintes.
Le binaire `forge-module.exe` a pour SHA-256
`8f7ef947314f16683dbb20e9a036284e5af8e503cd0c2e4ce0c8084c6d7adc3c`.
Le suivi du build est dans `tidewater-inland-sprint-20261117/runtime-build.json`
sous `windows-reviewed-studio-20261117-060/`.

Le lecteur 012 atteint les cinq points en mode navigation. Sa preuve
fonctionnelle passe et son contrôle de cadence échoue, avec code de
sortie 1. Son mode de test rend à 1 920 × 1 080, confirmé par le fichier
de capture. Le moteur commun exécute les cinq étapes `move_to` en sprint
à 2 560 × 1 440. Il termine avec la projection prête et un code de
sortie 0. PresentMon enregistre les deux trajets du premier au cinquième
point sans perte ETW signalée. Les résolutions diffèrent et le mode
de présentation change pendant le passage du moteur commun. Le trajet
est fonctionnel côté moteur. La parité de cadence reste à établir.

Sous `windows-reviewed-studio-20261117-060/tidewater-inland-sprint-20261117/`,
reçu `result.json`, SHA-256
`f802ce6fe7ee68b0a8bb3d4640526d929b12729e7180bd343b97d15c8448e778`.
Les 27 pièces vérifiées occupent 5 486 881 octets sur F:. Deux lancements
ont produit cette preuve. Le cache du module a été réutilisé.

Le journal du runtime confirme l’empreinte du scénario sprint dans
`plan_hash` aux cinq étapes terminées. Le champ `scenario_sha256` du
reçu de lancement reprenait l’empreinte de l’ancienne pose fixe. Le
pilote de travail inscrit désormais l’empreinte du scénario réellement
lancé. Le contrôle statique des substitutions passe. Les reçus datés
préservent leur forme d’origine.

Le 18 novembre, `forge-module` accepte les trois options de
redimensionnement du pilote de preuve natif. Il les transmet au même
`RuntimeAutomation` que le runtime de preuve. La compilation Release
Windows est terminée, avec les 1 745 entrées inventoriées stables. Le
binaire a pour SHA-256
`efb81350b878abcf83c3e22903eedae102ee4cfa0cfd2bc12875fab878c42098`.

Un seul passage sur le moteur commun a confirmé le redimensionnement
en 1 920 × 1 080 dans le journal Bevy, puis les cinq étapes du sprint
avec la projection prête, et un code de sortie 0. Les vingt réglages
et le mode `Composed: Flip` avec `SyncInterval=1` correspondent à la
capture historique du 012. Le processus du jeu était en arrière-plan
pendant les 305 relevés du trajet. Ce passage valide le redimensionnement
et le trajet. Le budget de cadence et la parité restent à établir avec
le jeu au premier plan pendant toute la fenêtre mesurée.

Sous `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-20261118/`,
reçu `result.json`, SHA-256
`9484e49ec3ed11fb7501729b5ecc719f525d4c3b3789187e8874f6818b4a4ff7`.
Les pièces relues occupent 4 071 278 octets sur F:. La capture du 012 a
été réutilisée.

<a id="gate-7-tidewater-sprint-1080p-premier-plan"></a>

Un passage reprend ce même binaire avec une préparation allongée avant
la mesure et les mêmes cinq points du sprint. Les 314 relevés du moteur
commun et les 278 du lecteur 012 archivé trouvent le jeu au premier
plan. Les deux fenêtres ont une zone client de 1 920 × 1 080 sur
l’écran 144 Hz, les vingt mêmes réglages et `Composed: Flip` à
`SyncInterval=1`. Les deux trajets atteignent les cinq points. La
projection du jeu exporté est prête à la sortie.

| Indicateur entre les points 1 et 5 | Lecteur 012 | Moteur commun |
| --- | ---: | ---: |
| Intervalles entre présentations | 1 326 | 630 |
| Moyenne entre présentations | 16,88 ms | 41,53 ms |
| P95 entre présentations | 19,08 ms | 121,30 ms |
| P95 entre changements d’image affichée | 20,89 ms | 118,04 ms |
| GPU actif médian | 11,30 ms | 11,12 ms |

Le moteur commun dépasse 16,67 ms en moyenne et le comparatif ne
valide pas la parité. Les 142 intervalles d’au moins 50 ms ont un p95
de 0,14 ms dans l’appel `Present` et de 14,15 ms de GPU actif. Les
observations ne montrent aucune préparation HLOD en attente pendant le
trajet. La trace indique de longues pauses entre présentations. Elle ne
permet pas d’en attribuer la cause à un système du moteur ou de Windows.

Sous `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-foreground-20261118/`,
reçu `result.json`, SHA-256
`2d0a8c7596c5cb47fc84f06131dd5c97767da08e73b4b4b6b0d09065368a38cd`.
Les 17 pièces relues et le reçu occupent 5 386 153 octets sur F:. Le
lecteur 012 a été repris depuis son reçu existant.

Le profilage CPU déjà intégré a été activé sur un seul passage du même
binaire, même scénario et même fenêtre au premier plan. Sur vingt
secondes entre le premier et le cinquième point, les compteurs relèvent
1 034 exécutions de chacun des horaires principal et de rendu, pour
445 présentations ETW. Les durées moyennes par appel atteignent 17,03 ms
pour l’horaire principal, 12,93 ms pour celui de rendu et 4,49 ms pour
`render.prepare_views.windows`. La boucle fixe occupe 8,75 ms par appel
de l’horaire principal. Elle inclut les contrôles de végétation
`plants.early_guard` à 2,47 ms et `plants.render_ready` à 0,95 ms par
appel, ce dernier exécuté deux fois par pas fixe. Les périmètres se
recouvrent. L’écart entre exécutions et présentations subsiste pendant
le profilage et sa cause reste à établir.

Le moteur commun réutilise désormais le même reçu d’inventaire du
renderer pour la préparation de la végétation, la vérification des
surfaces et l’admission initiale de la simulation dans un même contrôle.
Les contrôles de génération, de contexte et d’image publiée sont
maintenus. La vérification Cargo Release du runtime Windows passe. Le
gain de cadence de cette correction reste à mesurer.

Reçu `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-cpu-20261118/result.json`,
SHA-256 `9717d3ab5a991ae64ab17450d5b7b42fafe131d1892e3fdb9827b20a2b118dd3`.
Les pièces relues et le reçu occupent 5 718 060 octets sur F:.
Le reçu `tidewater-reuse-plant-inventory-20261118.log.receipt.json` a
pour SHA-256 `5029bdcfb8779022d8382f1df927f30f21a1150ecbac6ea6e5cded76aa9f25a5`.

Le moteur réutilise aussi le numéro de l’image GPU achevée obtenu
avec le reçu courant. La végétation le compare à son image de
publication, les ombres à leur image d’encodage, et l’admission de la
simulation vérifie en une seule lecture le propriétaire, la génération
et l’image de démarrage. Les vérifications de sources, de contexte et
d’époque GPU restent actives. Le `cargo check` Release Windows du
runtime passe. Le gain en jeu reste à mesurer. Reçu
`tidewater-reuse-completed-render-frame-windows-20261118.log.receipt.json`,
SHA-256 `9397fd72736e16edf94af1246e1b12a646a8c21ddb514a1e8e36bd96547fb1ed`.

<a id="gate-7-tidewater-trace-presentation"></a>

Le build Release Windows intègre la mutualisation des contrôles de
végétation et la trace optionnelle `forge::present`. Les 1 745 entrées
ont gardé leurs empreintes pendant la compilation. Le binaire a pour
SHA-256 `61c1437b9c35cf5d2f2b11eb1dbd3e8a7bb51830e2aa176f7fb8c370cae97e0b`.

Un seul passage instrumenté du sprint 1080p se termine avec cinq points
atteints et la projection prête. Les 306 relevés trouvent le jeu au
premier plan sur l’écran 144 Hz. Pour les 1 352 images observées, la
texture de la fenêtre principale est présente avant le système de rendu
Bevy, puis consommée par sa demande de présentation à wgpu. Les
intervalles entre ces demandes atteignent 18,92 ms en moyenne et
28,60 ms au p95. La trace Windows alignée sur la même fenêtre livre
611 lignes DXGI, soit 610 intervalles complets, en `Composed: Flip`.
L’écart apparaît entre la demande de présentation par Bevy et les
événements attribués par PresentMon. L’expérience ne distingue pas
les images écartées en aval des événements omis par le collecteur.
[Documentation PresentMon 2.6](https://github.com/GameTechDev/PresentMon/blob/v2.6.0/README-ConsoleApplication.md).

Sous `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-present-trace-20261118/`,
reçu `result.json`, SHA-256
`50d67d1652517215696363f1165c48728cb30ed4a800eb2b6411da0e4cdcac4f`.
Les 17 pièces relues et le reçu occupent 6 249 206 octets sur F:.
La cadence d’affichage et la parité restent à établir.

Le même exécutable Release, forcé sur Direct3D 12 pour un seul essai,
détecte la Radeon RX 7800 XT. Le premier appel de présentation wgpu
arrive après 131,87 s, au-delà de la fenêtre de collecte. Deux appels
sont relevés avant l’arrêt sur délai, sans point du trajet atteint.
Ce démarrage incomplet ne fournit aucune mesure comparable au sprint
Vulkan. Reçu `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-dx12-20261118/result.json`,
SHA-256 `afd347f02adc3dc77c7cd9b9afdb399d3687c07ddb751acb4cb2bf02bc999ae4`.

Un essai de lecture par DXGI Desktop Duplication utilise le même
exécutable Vulkan et le même trajet. Le premier lancement s’arrête sur
le contrôle du premier plan avant la mesure. Le second termine le sprint
avec la projection prête et 310 contrôles du premier plan à 1080p.
PresentMon relève 605 événements `Composed: Flip` sur 25,93 s
(42,80 ms en moyenne entre appels comptés). Le collecteur indépendant
s’initialise sur le bon écran, mais n’émet aucun échantillon exploitable.
La collecte ne conserve aucun pixel. Le comptage PresentMon est
reproduit, et la cadence réelle de l’image reste à établir.
[Horodatage DXGI Desktop Duplication](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/ns-dxgi1_2-dxgi_outdupl_frame_info).

Reçu `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-desktop-20261118/result.json`,
SHA-256 `da22cd1e73c5742a0ba163e01b16d34fdc3d7c135013ab7db38353b9afacd10d`.
Les pièces relues et le reçu occupent 1 790 018 octets sur F:.

<a id="gate-7-tidewater-reutilisation-image-gpu"></a>

Le sprint 1080p après la réutilisation de l’image GPU se termine avec
les cinq points atteints, projection prête et 308 contrôles du premier
plan sur l’écran 144 Hz. Les 1 745 entrées du build Release ont gardé
leurs empreintes. Les cinq fichiers de cette correction sont les
seules différences avec les entrées du binaire instrumenté précédent.

Sur 25,79 s, wgpu et PresentMon donnent chacun 1 401 événements, soit
1 400 intervalles, en `Composed: Flip` sans présentation écartée.
Une fois les horloges rapprochées, la trace wgpu suit l’événement DXGI
par 0,10 à 0,41 ms dans l’ordre chronologique. La divergence de
comptage précédente ne se reproduit pas sur ce passage. L’écart moyen
entre demandes wgpu passe de 18,92 à 18,41 ms et le coût moyen d’un
appel de la boucle fixe de 8,46 à 8,28 ms. Ces écarts viennent d’un seul
rejeu instrumenté et ne suffisent pas à isoler la cause de la
divergence précédente.

Le moteur commun relève 18,41 ms entre présentations et 27,91 ms
au p95 entre changements d’image affichée. Le relevé 012 sans
profilage donne respectivement 16,88 et 20,89 ms. Le budget de 16,67 ms
et la parité en conditions de production restent à qualifier.

Reçu `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-render-frame-reuse-20261118/result.json`,
SHA-256 `a6a43ec13f0e60d20c87dbd986e4d001de39f7bb1575ce5f21e7736e93abcd17`.
Les pièces relues et le reçu occupent 6 508 417 octets sur F:.

<a id="gate-7-tidewater-comparatif-sans-profilage"></a>

Le même binaire Release a repris le sprint une fois sans profilage.
Le module, le scénario, les réglages, la zone client 1080p et
l’écran 144 Hz correspondent au comparatif 012 déjà archivé. Le
jeu atteint les cinq points avec la projection prête et reste
au premier plan aux 311 contrôles. PresentMon relève 1 431 intervalles
en `Composed: Flip` avec `SyncInterval=1`, sans présentation écartée.

| Indicateur | Lecteur 012 | Moteur commun |
| --- | ---: | ---: |
| Moyenne entre présentations | 16,88 ms | 18,14 ms |
| P95 entre présentations | 19,08 ms | 28,04 ms |
| P95 entre changements d’image affichée | 20,89 ms | 27,80 ms |
| GPU actif médian | 11,30 ms | 10,68 ms |

Le moteur commun dépasse le budget moyen de 16,67 ms et reste à
1,26 ms du lecteur 012 en moyenne. Parmi les 89 intervalles de plus
de 25 ms, le temps médian dans `Present` vaut 0,12 ms et le GPU actif
médian 11,17 ms. Ces mesures laissent la cause des à-coups ouverte.
Le passage de 41,53 à 18,14 ms dans les relevés PresentMon doit être
lu avec la divergence de comptage observée sur l’ancien binaire.

Reçu `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-render-frame-reuse-unprofiled-20261118/result.json`,
SHA-256 `6de39f7c06b52e22eb46d854f7f149805d07dfc3ae7262bf487a0cce36baaf40`.
Les pièces relues et le reçu occupent 5 407 595 octets sur F:.

Sur les vingt secondes du sprint instrumenté, la sélection des plantes
prend 15,80 ms en moyenne sur les 16 changements de partition.
La sélection commune vérifie désormais l’enveloppe du modèle une fois
par couche et rejette les identifiants en double à partir du tri
canonique. Le rejet reste global, y compris entre deux couches, et la
projection continue de vérifier chaque pose et les limites d’admission.
Les neuf tests Windows Release de `forge_world::imported_vegetation::streaming`
passent. Le gain en jeu reste à mesurer.
Reçu `tidewater-world-plant-partition-validation-20261118.log.receipt.json`,
SHA-256 `4259ec97fab0ebed504c94eab739b31203e946cd2cc6d773e4bf91d97265bb64`.

L’admission au rendu prenait en moyenne 5,33 ms sur ces mêmes 16
changements. Elle parcourt maintenant les plantes vérifiées et les
sources préparées dans leur ordre commun, en vérifiant chaque identité.
L’entrée publique accepte toujours les sélections fournies dans un autre
ordre et revérifie chaque source. L’herbe, l’environnement et les
espèces importées n’acquièrent plus un jeton lorsque le seul besoin est
de vérifier l’achèvement GPU de leur image de publication. Cette
lecture conserve les mêmes contrôles de sources, d’identité, de
génération et de contexte. Le `cargo check` Release Windows du runtime
passe. Le gain sur le sprint reste à mesurer.
Reçu `tidewater-ordered-admission-scoped-receipt-20261118.log.receipt.json`,
SHA-256 `4786383e627d1f94c89841ea8e5b514e8bd77d3b44f29c5e0bf56cbaa1f17108`.

<a id="gate-7-tidewater-admission-plantes-comparatif"></a>

Les deux corrections d’admission et la lecture des reçus GPU scoped ont
été regroupées dans un seul binaire Release. Les 1 745 entrées ont
gardé leurs empreintes. Sept fichiers diffèrent du binaire précédent.
Le sprint sans profilage reprend les cinq points à 1080p, avec les mêmes
réglages, l’écran 144 Hz et `Composed: Flip` à `SyncInterval=1`.
Les 309 contrôles trouvent le jeu au premier plan. Les cinq points
sont atteints et la projection est prête.

| Indicateur | Tidewater 012 | Moteur commun précédent | Moteur commun actuel |
| --- | ---: | ---: | ---: |
| Moyenne entre présentations | 16,88 ms | 18,14 ms | 18,27 ms |
| P95 entre présentations | 19,08 ms | 28,04 ms | 27,74 ms |
| P95 entre changements d’image affichée | 20,89 ms | 27,80 ms | 27,90 ms |
| GPU actif médian | 11,30 ms | 10,68 ms | 10,66 ms |

Les mesures du moteur commun restent proches des précédentes sur ce
passage. Le budget moyen de 16,67 ms et la parité restent ouverts.
Reçu `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-plant-admission-unprofiled-20261118/result.json`,
SHA-256 `6210830de665e3886e59f23384d45548e2159d539b7ccea4ce4abd9c92e9c2e3`.
Les pièces relues et le reçu occupent 5 620 027 octets sur F:.

<a id="gate-7-tidewater-couverture-jalons"></a>

Le runtime commun vérifie maintenant les familles de matériaux par
archétype. Les cas qui exigent un propriétaire restent vérifiés par
entité, comme l’exception des miroirs de réfraction. La comparaison
utilise le mode de qualification qui ne consigne que les jalons du
scénario, le démarrage et les commandes. Sur la fenêtre du sprint,
l’ancien relevé écrivait 148 observations détaillées, soit environ
2,2 Mo. Le nouveau journal confirme l’absence de ces observations.

Les 1 745 entrées ont gardé leurs empreintes pendant la compilation
Release. Cinq fichiers diffèrent du binaire précédent. Le même sprint
1080p, sans profilage, atteint ses cinq points avec projection prête
et 321 contrôles du premier plan. PresentMon relève 1 432 intervalles
sans présentation écartée. Le moteur commun donne 18,07 ms en moyenne
et 27,78 ms au p95 d’affichage, contre 18,27 et 27,90 ms au précédent
passage, et 16,88 et 20,89 ms pour le lecteur 012. Les deux changements
sont présents dans ce seul passage. Le budget et la parité restent
ouverts.

Reçu `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-coverage-markers-unprofiled-20261118/result.json`,
SHA-256 `b237592d2aac8bf0d29685df0c1fd95627b5f2a61f260d92d685f861a1245d3c`.
Les pièces relues et le reçu occupent 1 444 131 octets sur F:.

Le moteur prépare les emprises par plante et leur ordre canonique après
la première admission du snapshot. Pour chaque nouvelle partition de la
même grille, il réutilise ces emprises et valide les cellules résidentes,
le budget et les propriétaires HLOD courants. Les sources de collision
gardent leurs identités, leur ordre et leurs profils. Une autre grille
reprend le calcul complet. Le cache reste lié à la population, au
terrain et aux sources immuables qui l’ont produit.

Les dix tests Windows Release du streaming de végétation passent,
dont la comparaison de la sélection directe et de la sélection avec
cache sur deux partitions et le refus d’une couverture HLOD périmée.
Le `cargo check` du runtime Release Windows passe. Le gain en jeu
reste à mesurer.

Reçus `tidewater-snapshot-footprints-world-20261118.log.receipt.json` et
`tidewater-snapshot-footprints-runtime-20261118.log.receipt.json`.
SHA-256 respectifs
`c8719ee6570d244f32a4009f72510031a7013a84d38fa7e3e45070659168d15b`
et `e67d41c36b54fe5a2037986b6c245f0731fe3b08c3237847aafb21ee4fea77be`.

L’audit partagé de la végétation filtre les retraits de composants
selon le marqueur de la famille contrôlée. Les retraits du marqueur,
les modifications détectées et les messages arrivés à expiration
peuvent toujours déclencher la vérification complète. Les cas où elle
est déjà nécessaire évitent un parcours préalable des composants
modifiés. Trois cas passent avec le code de l’audit lié à Bevy ECS,
et le `cargo check` Release Windows du runtime passe. L’effet sur la
cadence du jeu reste à mesurer.

Reçus `tidewater-scoped-plant-audit-probe-20261118.json`
(SHA-256 `3b32c4c04bff6fc379c5acabb481319250edad4de0caf4c831d51e372dbbd9c0`)
et `tidewater-scoped-plant-audit-runtime-corrected-20261118.log.receipt.json`
(SHA-256 `e12ad2ccbe53c1d6bbe833baa77c9090f298b4a1b57fbe19161f73d51933aba8`).

<a id="gate-7-tidewater-emprises-audit-comparatif"></a>

Un build Release regroupe le cache d’emprises et le filtrage des
retraits. Les 1 745 entrées gardent leurs empreintes entre les deux
manifestes du build. Neuf fichiers diffèrent du binaire précédent.
Le comparatif reprend le sprint 1080p sans profilage, journal aux jalons
et écran 144 Hz, avec cinq points atteints et projection prête.
Les identités et les effectifs canoniques de végétation et de collision
concordent avec le relevé précédent. Les 317 contrôles trouvent le jeu
au premier plan. Les 1 454 intervalles sont en `Composed: Flip` à
`SyncInterval=1`, sans présentation écartée.

| Indicateur | Tidewater 012 | Moteur commun précédent | Moteur commun actuel |
| --- | ---: | ---: | ---: |
| Moyenne entre présentations | 16,88 ms | 18,07 ms | 17,56 ms |
| P95 entre présentations | 19,08 ms | 26,92 ms | 23,53 ms |
| P95 entre changements d’image affichée | 20,89 ms | 27,78 ms | 26,66 ms |
| GPU actif médian | 11,30 ms | 10,70 ms | 10,65 ms |

Le passage mesure les deux changements ensemble. Le budget moyen de
16,67 ms et la parité Tidewater restent ouverts.
Reçu `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-footprint-audit-unprofiled-20261118/result.json`,
SHA-256 `3165147b783749eff2a112769cd8c5a1ab78350ad283197e9b4359fd27e61580`.
Les pièces relues et le reçu occupent 1 419 669 octets sur F:.

Le contrôle GPU du module prend désormais le maximum des trois numéros
d’image exigés par la demande, la publication et l’application du
profil. Il vérifie la disponibilité de chaque famille une fois dans ce
passage. Le reçu du terrain sert aussi aux contrôles suivants du terrain
et des surfaces. La végétation vérifie son inventaire principal sans
créer un jeton supplémentaire. Le dernier sprint avait huit familles
actives. Le `cargo check` Release Windows du runtime passe. Le coût en
jeu de ce changement reste à mesurer.

Reçu `tidewater-graphics-scope-completion-20261118.log.receipt.json`,
SHA-256 `5c93b539cce96a063f328458e3cfc0d7da3bbb169901bd094d8a013847407a88`.

<a id="gate-7-tidewater-inventaires-partages"></a>

Les demandes, les reçus GPU et les jetons partagent désormais les mêmes
listes de ressources immuables. Pendant l’extraction, le moteur relève
la caméra une fois pour l’inventaire principal et les familles actives.
Chaque famille vérifie toujours la présence de ses sources. Une nouvelle
génération enregistre ses propres ressources et conserve les contrôles
de propriétaire, d’identité et d’achèvement GPU.

Le build Release regroupe les deux corrections d’inventaire. Les 1 745
entrées gardent leurs empreintes pendant la compilation, avec cinq
fichiers modifiés depuis le binaire précédent. Le sprint 1080p reprend
le même export, les mêmes réglages et l’écran 144 Hz. Le jeu reste au
premier plan aux 316 contrôles et atteint ses cinq points avec la
projection prête. Les effectifs et identités de la végétation et des
collisions concordent avec le relevé précédent. PresentMon relève 1 526
intervalles en `Composed: Flip` à `SyncInterval=1`, sans image écartée.

| Indicateur | Tidewater 012 | Moteur commun précédent | Inventaires partagés |
| --- | ---: | ---: | ---: |
| Moyenne entre présentations | 16,88 ms | 17,56 ms | 16,68 ms |
| P95 entre présentations | 19,08 ms | 23,53 ms | 19,45 ms |
| P95 entre changements d’image affichée | 20,89 ms | 26,66 ms | 21,80 ms |
| GPU actif médian | 11,30 ms | 10,65 ms | 10,49 ms |

La moyenne du moteur commun atteint 16,6755 ms sur ce passage, à
0,0055 ms du seuil de 16,67 ms. Le p95 d’affichage reste 0,91 ms derrière
le lecteur 012. Le budget et la parité restent ouverts sur le périmètre
complet. Binaire `5fac7bfe78f9d6a1b84e3350e169aee47d2e5bd656509e60887b89c1cf941cf3`.

Reçu `windows-reviewed-studio-20261117-060/tidewater-sprint-1080p-shared-inventory-unprofiled-20261118/result.json`,
SHA-256 `b9b9f9dc875557103739e7361fb1e5cf6ec38e9c8ecc508fc08649a98c6d6a45`.
Les 19 pièces relues occupent 1 439 734 octets sur F:. Le contrôle
d’exécution a refusé le retrait des 1 213 728 octets temporaires sur E:.

<a id="gate-7-tidewater-a-coups-061"></a>

La relecture des traces 1080p épinglées compare le moteur du kit 061 et
le lecteur 012. En 061, le p99 d’affichage atteint 33,34 ms, contre
26,41 ms en 012. Au-delà de 33,33 ms, on compte 16 intervalles sur
1 526 en 061 et un sur 1 326 en 012. Le p95 et la moyenne du comparatif
restent identiques à la première relecture.

Le trajet entre `(0, -90)` et `(-20, -110)` concentre huit de ces
intervalles en 061 et un en 012 après retrait de 750 ms à chaque
extrémité. Le contrôle écarte l’approche immédiate des points. Le 061
freine près des points dans son pilote de parcours et le 012 continue
en sprint jusqu’à leur tolérance. Les deux captures traversent les
mêmes repères à des durées différentes. La cause des pics reste à isoler
sur ce tronçon à partir des chronologies de simulation et de rendu.

Reçu `windows-unified-20261118-061/tidewater-061-tail-readback.json`,
SHA-256 `b50d3fb22baa186f8881753c01971124b780ce1c0f00748f21f6fcf70d71d5a8`.
La relecture ajoute 6 408 octets sur F: et réutilise les deux captures.

<a id="gate-7-tidewater-061-trace-troncon"></a>

Un passage instrumenté du binaire exact 061 atteint les cinq repères et
termine avec la projection prête. Les 316 contrôles confirment la fenêtre
1080p au premier plan à 144 Hz. Le rapprochement associe les 1 493
présentations wgpu et DXGI une à une à 0,10–0,37 ms près, en `Composed:
Flip` à `SyncInterval=1`, sans présentation écartée.

Au cœur du tronçon `(0, -90)` → `(-20, -110)`, les sept intervalles
d’affichage au-delà de 33,33 ms correspondent à des intervalles entre
présentations du jeu de plus de 25 ms. Le GPU actif médian de ces sept
images est de 10,64 ms. Le temps moyen par image dans la boucle de
simulation à pas fixe passe de 5,89 à 7,21 ms sur la fenêtre de cinq
secondes qui couvre ce tronçon, puis revient à 6,15 ms. Sur les mêmes
fenêtres, le temps dans l’ordonnanceur de rendu reste proche de 13 ms. La
boucle fixe exécute un peu plus de pas par image sur le tronçon et le
temps moyen par pas passe de 6,11 à 6,78 ms.

Les mesures CPU sont agrégées sur cinq secondes. Quatre des sept pics
précèdent ou suivent une publication de végétation à une image près.
Le système à l’origine des pics reste à identifier dans la simulation.
Les mesures de cadence pour le verdict
restent celles du passage sans instrumentation.

Reçu `windows-unified-20261118-061/tidewater-061-segment-trace/result.json`,
SHA-256 `9cc8d33e5b46ad88975b1b32d9cdf925caa1694363dbc13acd0e60f211ea0cad`.
Les 15 pièces relues occupent 2 100 668 octets sur F:.

<a id="gate-7-tidewater-colliders-navigation"></a>

Le moteur commun recherche désormais les colliders en attente dans les
archétypes qui portent un composant `Collider`. Les deux phases de
navigation du pas fixe évitent ainsi de passer sur les entités purement
visuelles. Les colliders en attente et ceux déjà installés dans Rapier
restent contrôlés, y compris les entités que les filtres Bevy masquent
par défaut.

Dans un monde Bevy synthétique contenant 19 359 entités visuelles,
269 colliders installés et cinq colliders en attente admissibles, chaque
parcours compte cinq cas. La durée médiane de la recherche passe de
0,404 à 0,002 ms par appel.

La compilation Windows optimisée réussit. Sur le trajet du 061 à
1080p, le moteur corrigé atteint les cinq repères, confirme la
projection et termine sans erreur de simulation. PresentMon ne produit
aucun échantillon, y compris après reprise de la collecte sur la sortie
standard. Le gain en jeu et l'effet sur les à-coups restent indéterminés.
Le 061 reste le dernier comparatif de cadence exploitable.

Reçu `windows-unified-20261118-061/tidewater-navigation-collider-index/result.json`,
SHA-256 `52807486fe347ef2971e679eaf72590916669885a2c0063d1a765108c1f6f672`.
Les onze pièces relues occupent 36 195 octets sur F:.

<a id="gate-7-tidewater-cadence-moteur-navigation"></a>

Le compteur du moteur enregistre les intervalles entre deux images de
la boucle principale, avec la même instrumentation sur le 061 et le
moteur corrigé. La fenêtre du 061 se ferme après le troisième repère.
Le comparatif se limite aux trois premiers repères, parcourus dans les
deux essais en 1080p à 144 Hz. La moyenne passe de 15,02 à 14,60 ms
et le p95 de 23,34 à 20,89 ms. Le p99 passe de 37,11 à 37,30 ms
et le nombre d’intervalles au-delà de 33,33 ms passe de 10 à 12.

Sur la partie centrale entre `(0, -90)` et `(-20, -110)`, le p95
passe de 29,56 à 24,11 ms, avec 8 puis 10 intervalles au-delà de
33,33 ms. Les deux essais comptent 29 images où la simulation est
signalée non prête sur cette portion. La mesure relève une amélioration
du p95 côté moteur et des pics qui persistent. La collecte Windows sans
filtre capte le 061, mais aucune présentation du processus corrigé.
Le p95 de la boucle principale ne suffit pas à établir la cadence affichée.

Reçu `windows-unified-20261118-061/tidewater-navigation-frame-intervals/result.json`,
SHA-256 `7dbfefc9f08680adfa50ccc7c572d89169f4a59b85193d597ff199f0cc4da816`.
Le reçu et l’archive des 29 pièces vérifiées occupent 502 419 octets sur F:.

Sept des huit pics du 061 au milieu de ce tronçon retrouvent un pic du
moteur corrigé à un pas de simulation près. Les pas 1608, 1610 et 1613
coïncident exactement. Ce rapprochement cible les événements communs de
simulation à examiner. Reçu `spike-ticks.json` dans le même dossier,
SHA-256 `35553d8764c02812ac203000f5294aa31afdb035474a9831d9f09e691b65ae07`
(3 580 octets).

<a id="gate-7-tidewater-admission-terrain"></a>

Aux pas 1608, 1610 et 1613, les deux relevés observent le même changement
de disponibilité du terrain. Les 45 tuiles actives, leur cache et les
normales restent stables entre les deux images de chaque changement.
Ces relevés ne distinguent pas encore la synchronisation physique de
l'admission graphique.

Les deux contrôles du terrain dans le pas fixe réutilisent désormais
la validation budgétaire quand la demande de cellules correspond
exactement à celle déjà admise. Une demande inédite, une préparation
en cours, un changement de source ou une erreur repassent par le
planificateur. L'intégrité des cellules et les colliders Rapier
sont toujours contrôlés. Le `cargo check --release` Windows du runtime
`forge-module` réussit. Le gain de ce changement reste à mesurer en jeu.

Reçu `terrain-admission-readback.json` dans le dossier du comparatif,
SHA-256 `6b731c4523691f7abb8290d04a6808e4c287213245c36a83a0f7da02961e941f`.

<a id="gate-7-tidewater-hlod-empreinte"></a>

Le HLOD distant réutilise sa disposition lorsque les clés des cellules
couvertes sont identiques. Les changements de détail gardent les
contrôles des sources, des maillages et des budgets. Le build Release
Windows ne diffère du binaire du comparatif précédent que par la
réutilisation du plan admis et de la disposition HLOD.

Le rejeu atteint les cinq repères à 1080p, avec 316 observations au
premier plan dans chacun des deux relevés. Côté moteur, les intervalles
au-delà de 33,33 ms passent de 18 à 15. Le p99 passe de 33,56 à
32,03 ms, tandis que la moyenne passe de 14,95 à 16,93 ms et le p95
de 19,49 à 19,71 ms.
PresentMon attribue 710 présentations au jeu pendant les 1 508
intervalles moteur mesurés sur le trajet. Le passage précédent ne
disposait d'aucun échantillon Windows attribué au jeu. La cadence
d'affichage et la parité restent ouvertes.

Reçu `windows-unified-20261118-061/tidewater-shared-terrain-layout/result.json`,
SHA-256 `26d13f3724f042099b5f4da50a0681ca246916ee119a2519f654bd7f98ddaa54`.
Le reçu et l'archive vérifiée occupent 475 105 octets sur F:.

<a id="gate-7-tidewater-hlod-presentations"></a>

Un passage instrumenté du même binaire rapproche les horodatages wgpu
et DXGI sur le trajet complet à 1080p. Les 700 entrées DXGI attribuées
au jeu correspondent chacune à une des 1 521 demandes wgpu, à
0,11–0,26 ms près. Chacun des 109 intervalles DXGI d'au moins 50 ms
contient des demandes wgpu intermédiaires. La trace Windows ne permet
donc pas de mesurer la cadence d'affichage de ce passage.

Avec la même instrumentation, le 061 donne une moyenne de 17,05 ms
entre demandes wgpu et un p95 de 21,10 ms. Le moteur avec réutilisation
du terrain donne 16,99 et 19,69 ms. Chacun compte 17 intervalles au-delà
de 33,33 ms. Dans les fenêtres de cinq secondes qui couvrent la même
portion du trajet, le temps par image dans la boucle à pas fixe passe
de 7,21 à 6,11 ms. La préparation de la fenêtre du rendu passe de
4,66 à 5,32 ms. Ces fenêtres regroupent plusieurs étapes du moteur
et ne permettent pas d'expliquer chaque pic.

Reçu `windows-unified-20261118-061/tidewater-terrain-wgpu-dxgi-alignment/result.json`,
SHA-256 `3fc3204ad4c38a6b20defab9b3b81483195bdd7313813af99fc4f6f337ea90af`.
Le reçu et l'archive vérifiée occupent 191 699 octets sur F:.

Lorsqu'un changement de couverture exige une nouvelle disposition HLOD,
le partitionnement consulte maintenant la plage de colonnes occupée par
la tuile. Les quatorze dispositions comparées en calcul isolé sont
identiques, y compris leurs raccords, et le calcul médian passe de
96,86 à 65,40 µs. Les onze tests HLOD existants passent en Release Windows.
Le gain par image en jeu reste à mesurer.

Reçu `windows-unified-20261118-061/tidewater-hlod-column-lookup/result.json`,
SHA-256 `a9b1cc649042b9db3faf189eec881384dcc6aacfb6ac965a8e1eddeab406c106`.
Le reçu et les sources du calcul isolé occupent 7 854 octets sur F:.

<a id="gate-7-tidewater-cadence-compositeur"></a>

Windows Graphics Capture horodate les changements d'une zone de 128 ×
72 pixels de la seule fenêtre Tidewater, sans écrire les pixels. Le
réglage par défaut limite l'échantillonnage à un intervalle minimal
de 16 ms sur ce poste. Passer à 1 ms permet de relever les intervalles
de 13,89 ms sur l'écran à 144 Hz.
[Horodatage des images du compositeur](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframe.systemrelativetime).

Le lecteur 012 et le moteur commun `f4ecc42f…` passent les cinq
repères à 1080p avec les mêmes vingt réglages. Une perte de premier plan
du 012 au premier repère conduit à retirer 750 ms à chaque extrémité
dans les deux relevés. Sur les parties comparables, la moyenne est de
18,83 ms pour le 012 et de 17,25 ms pour le moteur commun. Le p95 est
de 20,83 ms pour les deux. Le p99 passe de 27,78 à 34,72 ms et le nombre
d'intervalles au-delà de 33,33 ms passe de 1 à 20. Neuf sont observés
à l'intérieur du tronçon `(0, -90)` → `(-20, -110)`, puis six sur le
tronçon suivant. La parité de cadence reste ouverte.

Reçu `windows-unified-20261118-061/tidewater-compositor-capture-pair/result.json`,
SHA-256 `60b41ee37c42c693af3d38f92ff69c9a7454231e2f2a6ca29072e41a6e9ef23f`.
Le reçu et l'archive vérifiée de 35 pièces occupent 285 837 octets sur F:.

<a id="gate-7-tidewater-phases-images-longues"></a>

Dans un passage instrumenté du même binaire, le compositeur reçoit
1 489 images modifiées sur le trajet. Les onze intervalles de plus de
33,33 ms sur la partie intérieure des deux tronçons ont chacun une
demande wgpu tardive 24 à 30 ms plus tôt. Le rapprochement de ces
horodatages est rétrospectif. Pour les envois associés, l'ordonnanceur
de rendu prend 8,39 à 12,05 ms et sa phase de préparation des vues
0,65 à 0,78 ms. Le délai se manifeste avant l'envoi du moteur au
compositeur. La part de la simulation et des attentes entre images
reste à départager.

Le résumé du scénario 012 mentionne `AutoVsync`. Le relevé direct de sa
fenêtre dans l'essai caméra 1440p indique `Fifo`, mode également demandé
par le moteur commun. La Radeon annonce la prise en charge de
`FifoRelaxed`. Le mode négocié pendant les deux passages à 1080p
reste inconnu. La catégorie DXGI `Composed: Flip` décrit le chemin
d'affichage Windows.

Sous `windows-unified-20261118-061/tidewater-compositor-stage-trace/`,
`result.json`, SHA-256
`b2f1c374fe4061ec0ec5a3ff323afe43ea7031899272cb3817e6af6353611991`,
et `present-mode-difference.json`, SHA-256
`0200ae8657c44455a99bdf4d93162ba19a4705129c38690f5dc3010648f3f8be`.

Le rejeu instrumenté `eb6463c9`, qui inclut aussi le calcul HLOD par
plages de colonnes, donne 17,36 ms en moyenne et 17 intervalles de
plus de 33,33 ms, contre 18,83 ms et un seul intervalle pour le 012.
Le p95 reste à 20,83 ms et le p99 atteint 34,72 ms pour le moteur commun
contre 27,78 ms pour le 012. Ce passage ne permet pas d'isoler le gain
du calcul HLOD. Sur les deux tronçons les plus touchés, 13 longs
intervalles du compositeur sont associés par horodatage à un retard
wgpu, suivi d'une image principale lente. L'intervalle médian de la
fin de `PostUpdate` au début de `First` est de 1,98 ms pour ces images.
Les durées mesurées après `First` décrivent la nouvelle image. Le
rapprochement ne désigne pas un système moteur responsable du retard.

Reçu `windows-unified-20261118-061/tidewater-slow-main-boundaries/result.json`,
SHA-256 `194b488f664519a834657be40b11bd64770e35b3c5ee2b31e7373108172a2ade`.

<a id="gate-7-tidewater-plan-cellules-partage"></a>

Les onze longs intervalles du premier passage instrumenté et les treize
du second suivent chacun une reprise de simulation sur ces tronçons.
Le moteur commun
réutilise maintenant un seul plan entre les deux gardes physiques et
la mise à jour du terrain pour une demande identique. Une transition
déjà en préparation conserve sa suspension sans recalculer le plan.
Seule une admission réussie permet de réutiliser son verdict budgétaire.
Les gardes continuent de vérifier les colliders actifs avant d'avancer.

Le build Release Windows réussit et le rejeu complet à 1080p atteint
les cinq repères avec la projection prête. Le p99 du compositeur reste
à 34,72 ms. Le nombre d'intervalles de plus de 33,33 ms passe de 17 à
18, contre un pour le 012. La mutualisation du plan ne résout pas les
à-coups sur ce passage. Le premier départ en arrière-plan a été écarté.

Reçu `windows-unified-20261118-061/tidewater-shared-cell-plan/result.json`,
SHA-256 `8587ac7173c2ff3599df2833d04bd3e5fcbfe6cc2bf96bbc4cd958a813d52407`.

<a id="gate-7-tidewater-navigation-empreinte-worker"></a>

Un passage d'échantillonnage CPU du même binaire atteint les cinq
repères. Parmi 1 676 relevés des compteurs d'instruction du processus,
un échantillon du fil principal pendant une image lente après reprise
se trouve dans `forge_navigation::source_hash`. Le début de la fonction
dans le PDB correspond à son entrée dans la table d'exception du binaire.
Ce passage instrumenté ne mesure ni la durée totale de cette fonction
ni son effet sur la cadence.

Le travailleur natif de navigation calcule désormais l'empreinte des
tuiles non vides avant leur génération. Les buffers de géométrie lui
sont transférés sans copie. Le moteur relit le reçu des colliders et
n'enregistre l'empreinte que pour la demande encore active. Le format
de l'empreinte et le calcul des demandes vides restent identiques.

Sous `windows-unified-20261118-061/tidewater-simulation-cpu-samples/`,
`result.json`, SHA-256
`da9bde4976d20bf3e0b508341b11db5a6b3a7ff49bafdf96a16e37a083cfded0`,
et `checked-symbol.json`, SHA-256
`e192cbce4b36c385d453c51c091c17cb6685336d91edd75156713529375f6c21`.

Le binaire Release Windows atteint les cinq repères avec la projection
prête. Le comparatif du compositeur utilise le même trajet à 1080p,
l'écran 144 Hz, les vingt mêmes réglages et le même retrait de 750 ms
aux deux extrémités. L'échantillonneur CPU est désactivé.

| Mesure sur le trajet | Tidewater 012 | Moteur avant | Moteur après |
| --- | ---: | ---: | ---: |
| Intervalle moyen | 18,83 ms | 17,30 ms | 17,08 ms |
| P95 | 20,83 ms | 20,83 ms | 20,83 ms |
| P99 | 27,78 ms | 34,72 ms | 27,78 ms |
| Intervalles au-delà de 33,33 ms | 1 | 18 | 3 |

Les deux tronçons les plus touchés passent de 9 à 2 et de 5 à 1
intervalles de plus de 33,33 ms. Ce résultat porte sur le trajet et le
profil mesurés. Les autres profils et le rendu restent à qualifier
selon leurs critères. Reçu
`windows-unified-20261118-061/tidewater-navigation-worker-hash/result.json`,
SHA-256 `9b7f025b00dc32def80ad23d8ad6016a623ddd5973f06e598969915e27395352`.
La relecture de la capture situe les trois intervalles résiduels à 4,74 s,
4,90 s et 12,99 s après le premier repère. Ils durent chacun 34,72 ms.
Les trois suivent une reprise de simulation et un intervalle de 26,22 à
30,87 ms entre deux demandes de présentation du moteur. Le rapprochement
situe le retard en amont du compositeur sans isoler le système en cause.

<a id="gate-7-tidewater-navigation-tri-worker"></a>

Le travailleur natif du moteur commun assure maintenant le tri canonique
des triangles avant le calcul de l’empreinte. Les indices séquentiels, le
comparateur et le format d’empreinte sont conservés. Deux entrées du moteur
changent par rapport au comparatif précédent dans le périmètre du runtime.

Le build Release Windows réussit. Un premier passage est écarté au contrôle
du premier plan. Le passage retenu atteint les cinq repères à 1080p sur le
même écran à 144 Hz et avec les mêmes vingt réglages. Sa projection est prête.

| Mesure entre changements d’image | Tidewater 012 | Moteur 062 | Tri sur le travailleur |
| --- | ---: | ---: | ---: |
| Moyenne | 18,83 ms | 17,08 ms | 17,27 ms |
| P95 | 20,83 ms | 20,83 ms | 20,83 ms |
| P99 | 27,78 ms | 27,78 ms | 20,83 ms |
| Intervalles au-delà de 33,33 ms | 1 | 3 | 0 |

La mesure porte sur un passage dans les conditions du comparatif à 1080p.
Le runtime a pour SHA-256
`0c9f542a6830e86bdda0c8b6128ebf05838d3a6bc1462973db7a647f61785e6f`.
Le reçu `windows-unified-20261118-061/tidewater-navigation-worker-order/result.json`
a pour SHA-256 `8a0942f9e1ab24eac71b6e6a91753d0e8b9495e16e6d8097cc035355e9dff5cc`.
Les 18 pièces vérifiées occupent 577 588 octets en archive sur F:. Les
copies archivées et les fichiers du départ invalidé ont libéré 9 312 113
octets sur E:.

La relecture des trois CSV PresentMon vérifie les empreintes, une seule
chaîne de présentation par trace, `Independent Flip`, `SyncInterval=1`
et zéro présentation écartée. Sur les deux dernières secondes, le GPU
actif médian est de 8,20 ms pour le 012, 10,06 ms pour le module complet
et 8,63 ms pour la copie rapprochée. Sur cette même sous-fenêtre, le p95
entre changements d’image affichée vaut 13,91 ms pour le 012, 20,85 ms
pour le module complet et 20,85 ms pour la copie. Le nombre d’intervalles
d’affichage au-delà de 16,67 ms est respectivement de 0 sur 187, 25 sur
144 et 17 sur 153. Les intervalles entre appels `Present` dépassent ce
seuil 0, 1 et 3 fois. Le prochain comparatif doit relever les deux
cadences ensemble et vérifier le mode de présentation et la fréquence
du moniteur. La réduction de charge sur la copie laisse la parité ouverte.

Reçu `display-cadence-readback.json` dans le même dossier, SHA-256
`bb2229cd7bb31070935b3dc9a1874e55ebf212dd6d2479af340b754222d87553`.
La relecture conserve 5 472 octets sur F: et réutilise les trois captures.

Le reçu `presentation-tail-readback.json` dans le même dossier ajoute
5 670 octets et date les groupes d’ombres des deux journaux. SHA-256
`1e2d65e49ade1a59742a2db76dcecbcb0d30d51c4c687d2f89ed6be82144d4d6`.
Le diagnostic moteur associe maintenant les groupes préparés au numéro
d’image et publie un total par type de lumière, y compris les zéros.
La vérification Cargo Release de `forge_engine` passe sous Windows.
Reçu `shadow-mesh-frame-snapshot-20261117.log.receipt.json` à la racine
de la campagne, SHA-256
`5febaf825bfe20c11bd391a4f8ea34e49acfcff70117d3d77927b3b74c1bad4d`.
Le reçu et le journal occupent 17 872 octets. Aucun rejeu lancé.

Le chemin nuages partagé réutilise la liaison du sampler entre les
interpolations temporelles d’un même pipeline. Il prépare les deux groupes
de liaisons de la trace volumétrique uniquement quand cette passe est
encodée. Le panorama réutilise les entrées communes dans le même tampon
de préparation. Les dispatchs, les dimensions, les slots d’historique et
les shaders sont identiques. Ce changement retire du travail CPU de
préparation. Son effet sur la cadence reste à mesurer en jeu.

La vérification Cargo Release de `forge_engine` passe sous Windows.
Reçu `source-clouds-retained-sampler-20261117.log.receipt.json` à la racine
de la campagne, SHA-256
`1384f78a99fa4e819f8d04e5cbd9c7df2c45ebffd93972ce5fabb7e788358710`.
Le reçu et le journal occupent 898 octets. Aucun rejeu lancé.

<a id="gate-7-tidewater-runtime-courant"></a>

Le build Release Windows du runtime courant termine en 16 min 32 s sur
le cache commun. Les empreintes de ses entrées compilées sont stables.
L’exécutable a pour SHA-256
`88ab6a40350e6180e3b993377cc9143c7508de19af13052005369731b39a73de`.
Un seul rejeu conserve le module, la pose et les réglages 1440p du 060.
Les 43 observations sont prêtes, avec 83 poissons, cinq colliders et la
projection terminée. Les snapshots des images 288 et 542 indiquent zéro
instance directionnelle Bevy et 55 instances ponctuelles.

La fenêtre complète compte 224 intervalles, 17,77 ms en moyenne et
22,40 ms au p95. Elle mélange deux modes. Après retrait de l’intervalle
qui chevauche la transition, les 113 intervalles en présentation
indépendante donnent 14,08 ms de moyenne, 17,03 ms au p95 et 11,05 ms
de GPU actif médian. Le 060 complet donnait 13,86 ms, 15,69 ms et
10,22 ms sur 287 intervalles indépendants. Les 110 intervalles du
nouveau rejeu en mode composé donnent 21,60 ms de moyenne. Cette
fenêtre ne démontre aucun gain et ne permet pas d’attribuer l’écart à
la préparation des nuages ou de conclure à la parité du lecteur 012.

Sous `windows-reviewed-studio-20261117-060/tidewater-current-build-20261117/`,
reçu `result.json`, SHA-256
`776dec54af511e0169381e0fa0ac42866a1c0b7e7d04dc6fa9f81b85856d89ba`.
Les 17 pièces vérifiées occupent 9 310 696 octets sur F:. Les
11 442 350 octets temporaires ont été retirés sur E: après relecture.

Un seul rejeu de confirmation réutilise le même exécutable du cache,
la même pose et les mêmes réglages. Les deux rejouements du runtime
courant basculent en présentation composée après 2,62 puis 2,35 s.
Le 060 reste en présentation indépendante sur sa fenêtre complète.
Sur la sous-fenêtre commune de 1 à 2,25 s, toutes les présentations sont
indépendantes. Le 060 donne 14,09 ms de moyenne, 15,88 ms au p95 et
10,44 ms de GPU actif médian sur 87 intervalles. Le runtime courant
donne 13,78 / 15,67 / 11,14 ms sur 89 intervalles au premier passage
et 13,90 / 16,17 / 11,18 ms sur 88 intervalles au second.

Les cartes d’entrées des deux builds diffèrent uniquement sur
`source_clouds/trace.rs` et `render_timing.rs`. Les exécutions du 060 et
du runtime courant utilisent des chemins d’exécutable différents.
Le relevé confirme la bascule et l’écart de temps GPU dans la sous-fenêtre.
Il ne détermine pas leur cause et ne qualifie pas la parité.

Sous `windows-reviewed-studio-20261117-060/tidewater-current-confirmation-20261117/`,
reçu `result.json`, SHA-256
`f0ff5bedbbfa4ccc35cea4d6940f86dd7215f2aa9caf5675c6ee095d12385c3e`.
Les 11 pièces relues occupent 1 104 309 octets sur F:. Les 7 148 374
octets temporaires ont été retirés sur E:.

Le 17 novembre, un comparatif réutilise les deux binaires existants au même
chemin, avec le même répertoire de travail, la même scène et les mêmes
réglages. Le 060, auparavant indépendant sur toute la fenêtre mesurée,
passe en présentation composée avant la fenêtre retenue. Le runtime
courant bascule en cours de fenêtre. Sur les intervalles composés, le 060
donne 21,25 ms en moyenne, 22,31 ms au p95 et 12,21 ms de GPU actif médian
sur 187 intervalles. Le courant donne 21,30 / 22,62 / 12,00 ms sur 131
intervalles composés, et 13,75 / 15,66 / 11,18 ms sur 86 intervalles
indépendants. Le courant bascule donc aussi à chemin commun. La cause de
la bascule et la parité avec Tidewater historique restent à établir.

Sous `windows-reviewed-studio-20261117-060/tidewater-same-launch-path-20261117/`,
reçu `result.json`, SHA-256
`be637a1daf55ff026620e498446fd2c6626c059a1cbbfeaee7df6cab3a5b8545`.
Les 18 fichiers vérifiés occupent 803 062 octets sur F:. La copie temporaire
et les fichiers de travail sur E: ont été retirés, soit 135 582 022 octets.

Le profil archivé du lecteur 012 et les quinze observations de fenêtre
relevées dans cinq journaux du moteur commun indiquent le même mode Bevy
`Fifo` à 2 560 × 1 440. Le lecteur 012 force les mises à jour Winit
continues pendant la capture. Le runtime conserve le réglage Bevy des
jeux, continu lorsque la fenêtre a le focus. Les quinze observations
indiquent le focus, y compris dans les deux journaux au chemin commun.
La comparaison suivante doit établir le focus pendant toute la fenêtre
QPC et relever le mode de l’OS ainsi que la fréquence du moniteur.

Reçu `window-readback.json` dans le même dossier, SHA-256
`51d0789000c210bb12170ebf3bedde39e448e5bdccbe1e47dfb3f0c789f6343e`.
Ce reçu de 1 857 octets réutilise les journaux déjà conservés sur F:.

Un comparatif 012 / runtime courant a réutilisé les deux exécutables
existants à la pose du village. Les 42 et 40 observations Win32, espacées
d’au plus 108 ms pendant la mesure, trouvent à chaque fois le jeu au
premier plan sur le même écran principal 2 560 × 1 440 à 144 Hz. Le
lecteur 012 passe du mode composé au mode indépendant à 2,03 s dans la
fenêtre. Le runtime courant reste en mode composé. Le changement de mode
se produit aussi dans le lecteur historique, avec le même écran et le
focus observé. La comparaison des fenêtres complètes reste inexploitable
pour établir la parité.

Sur les deux premières secondes, les deux jeux sont en présentation
composée avec `SyncInterval=1`. De 012 au runtime courant, la moyenne
entre appels `Present` passe de 20,10 à 21,11 ms et le GPU actif médian
de 12,70 à 12,04 ms. Le p95 entre changements d’image affichée vaut
29,13 puis 27,86 ms. Le 012 compte une présentation écartée sur cette
sous-fenêtre. Ces chiffres portent sur 99 et 94 intervalles, avec les
transports propres aux deux jeux. La parité reste ouverte.

Sous `windows-reviewed-studio-20261117-060/tidewater-historic-monitor-20261117/`,
reçu `result.json`, SHA-256
`f803e8558d5917bfec01463d12f52f1f1ad36aae73a8728c75294dcdf338bb07`.
Les 18 fichiers vérifiés occupent 2 347 591 octets sur F:. Les copies
de travail retirées sur E: représentent 271 669 782 octets.

Dans le moteur commun, le vertex shader CDLOD réutilise désormais la
hauteur mesurée pour le morphing quand sa position finale X/Z est
identique bit pour bit. Une position déplacée par le morphing ou par le
clipping relit la hauteur à la nouvelle position. Le Studio et le jeu
exporté partagent la préparation du shader, y compris l’adaptation du
raster et les corrections de relief. Les variantes historiques qui
diffèrent du programme reconnu conservent leur code. Le programme LOD
du paquet Tidewater de qualification correspond au programme moteur par
SHA-256 `a8e3e7558e0980702838bcad508ab61f46e399dcf1176e067b5cc5a9405b95a5`.

Le test ciblé Windows valide avec Naga le raster de référence, le raster
rectangulaire avec élévation corrigée et la conservation d’une variante
historique. Reçu `source-terrain-lod-height-reuse-debug-20261117.log.receipt.json`,
SHA-256 `20a2826b0db545d98d54c712514685af6503db4e9215a506610dd12e78bb0c21`.
Le journal et le reçu occupent 1 336 octets sur F:. Les traces de la
compilation Release interrompue ont été retirées (1 264 octets).

Le build Windows Release a produit `forge-module.exe` de SHA-256
`0f867669aed89d607dc791bb781ef7cd8ec481d679541e3fbb9f816151b96496`.
Les inventaires avant et après build concordent. Trois fichiers de
préparation du terrain le distinguent du binaire témoin. Le comparatif
réutilise le même chemin de lancement, le même paquet et la même pose
1440p. Une première collecte s’interrompt lors d’un changement de handle
d’écran. Le pilote révisé consigne ce cas et les deux passages terminent
avec le jeu au premier plan à chaque relevé, sur le même écran à 144 Hz.

Sur la fenêtre commune d’environ quatre secondes en présentation
indépendante à `SyncInterval=1`, 279 puis 287 intervalles, le GPU actif
médian passe de 10,66 à 10,33 ms. Le p95 entre appels `Present` passe de
17,37 à 15,76 ms. Le p95 entre changements d’image affichée reste à
20,86 ms. Les deux passages terminent avec la projection prête et zéro
présentation écartée. Ce comparatif porte sur deux versions du moteur
commun et laisse la parité avec le lecteur 012 ouverte.

Sous `windows-reviewed-studio-20261117-060/tidewater-lod-height-reuse-20261117/`,
reçu `result.json`, SHA-256
`8aa26e453ef74d1b32f508de0039369fc598f0b3c56117dc3ebc6e303225f4cf`.
Les 22 fichiers vérifiés occupent 2 267 266 octets sur F:. Les copies et
sorties de travail retirées occupaient 382 741 277 octets sur E:.

La relecture des captures existantes aligne les 1,75 dernières secondes
en présentation indépendante avec `SyncInterval=1`, sans présentation
écartée dans la fenêtre retenue. Le p95 d’affichage est de 20,84 ms pour
le 012, 20,85 ms pour la scène rapprochée du 060 et 20,86 ms pour le
moteur après la correction CDLOD. La moyenne entre appels `Present` vaut
respectivement 11,20, 12,98 et 13,70 ms. Le GPU actif médian vaut 9,02,
8,59 et 10,24 ms. Le p95 d’affichage de ces fenêtres est proche de trois
rafraîchissements à 144 Hz. La cadence des appels et le coût GPU restent
distincts. Les scènes et les conditions de profilage diffèrent entre
le lecteur historique et le moteur commun. La parité reste ouverte.

Dans le même dossier, `historical-independent-flip-tail-readback.json`,
SHA-256 `9bcc30693a23092f7d559ac450ec57190b8c90c2a0f099303223eb33de8f38a8`.
Le reçu ajoute 2 990 octets sur F: et réutilise quatre captures.

<a id="gate-7-tidewater-pose-sans-profilage"></a>

Les deux passages ci-dessus avaient `FORGE_RUNTIME_CPU_PROFILE=1` et
`FORGE_GAME_FRAME_TIMES=1`. Un passage supplémentaire a réutilisé le même
binaire Release, le même module, les mêmes réglages et la même pose à 1440p
avec ces deux options désactivées. Les 42 relevés de caméra correspondent
à la pose attendue. Le jeu termine avec la projection et l’éclairage prêts
sur RX 7800 XT avec Vulkan. PresentMon n’a produit aucun CSV. La cadence
sans profilage et la parité avec le lecteur 012 restent à établir.

Sous `windows-reviewed-studio-20261117-060/tidewater-without-cpu-profile-20261117/`,
reçu `result.json`, SHA-256
`5ddc5a3c74c6842b9699baf05fdeea19ba923e152f2dcccfb89f9c266289a42e`.
Les neuf fichiers vérifiés occupent 869 026 octets sur F:. Le dossier de
travail temporaire de ce passage a été retiré de E:.

Le passage de diagnostic suivant conserve le binaire, le module et la pose.
PresentMon vise le PID du jeu et termine sa collecte au bout de 26 secondes.
Son journal confirme le démarrage et l’arrêt de l’enregistrement, signale
157 701 événements ETW perdus et ne produit aucun CSV. Le jeu termine
avec la projection prête. La collecte a donc échoué même en laissant
PresentMon s’arrêter seul. Aucune mesure de cadence n’est disponible.

Sous `windows-reviewed-studio-20261117-060/tidewater-timed-presentmon-20261117/`,
reçu `result.json`, SHA-256
`34ab602bf8fe1b70c403b70d5d63f63ded8f70c54a3175f71e9d9d3efda4581b`.
Les sept fichiers vérifiés occupent 10 432 octets sur F:. Le retrait des
127 732 050 octets temporaires sur E: a été bloqué par la politique
d’exécution. Ces fichiers ne sont plus nécessaires au diagnostic.

Windows listait encore `ForgeGate7-13404-31336` avec le fournisseur
graphique DxgKrnl. Les deux PID et le collecteur avaient quitté. La
session seule a été arrêtée et son absence vérifiée. Un passage du même
binaire et du même module sans les deux options de profilage a ensuite
produit un CSV, sans perte ETW signalée ni session Gate 7 restante. Les
42 relevés de caméra correspondent à la pose et la projection est prête.

Sur quatre secondes à 1440p, écran principal à 144 Hz au départ et à la
fin, la capture contient 236 intervalles et zéro présentation écartée.
Elle mélange présentation composée et indépendante à `SyncInterval=1`.
La moyenne entre appels `Present` vaut 16,93 ms, le p95 d’affichage vaut
27,52 ms et le GPU actif médian 11,44 ms. L’ordre des événements établit
que la collecte a repris après nettoyage. La cause des pertes antérieures
et la parité historique restent ouvertes.

La relecture des [captures du 012 et du runtime courant](#gate-7-tidewater-runtime-courant)
et du nouveau CSV situe des transitions opposées. Le 012 commence en
présentation composée et passe en présentation indépendante à 2,03 s.
Le moteur commun sans profilage commence en présentation indépendante et
passe en présentation composée à 1,90 s. Dans les deux fenêtres de quatre
secondes, la présentation composée coïncide uniquement lors de la courte
transition, sur environ 0,13 s. Aucune sous-fenêtre stable en présentation
indépendante ne coïncide. Les moyennes globales ne permettent donc pas
d’évaluer l’écart entre les deux moteurs à mode de présentation égal.

Sous `windows-reviewed-studio-20261117-060/tidewater-unprofiled-after-stale-trace-20261117/`,
reçu `result.json`, SHA-256
`82b5aff27f2b66a2ff5b76e398e822a41ffa2f51253685260c90d74794e74777`.
Les 14 pièces vérifiées occupent 1 007 861 octets sur F:. Le passage
réutilise le binaire existant et ajoute 6 672 391 octets au dossier de
travail temporaire sur E:.

Le paquet de ce passage a été rapproché de l’index archivé des ressources
du 012. Les 822 ressources d’objets et les 16 de l’étal retrouvent leur
SHA-256 dans l’index. Les écarts de végétation, d’environnement, de
poissons et de surfaces de terrain portent uniquement sur des fichiers
JSON. Dans l’océan, les 246 binaires sans empreinte correspondante
contiennent des images ou des paramètres d’amorçage. Le moteur lit ces captures avant le passage
à l’horloge live. Le rapprochement borne les écarts de fichiers à
examiner. Il laisse à vérifier l’état de l’océan, les placements auteur
et l’exercice des mêmes chemins de rendu pendant la mesure.
[Lecture des images d’amorçage](../crates/forge_engine/src/source_ocean.rs#L476).

Dans le même dossier, `historical-scene-resource-readback.json`,
SHA-256 `6d15cdb7898517ddef735bbfaeea31c1eb1748b39a67b7af6cbc1e8c95605f66`.
Le reçu ajoute 2 649 octets sur F:. Aucune copie du paquet et aucun
nouveau lancement.

<a id="gate-7-tidewater-piece-et-ombres-ponctuelles"></a>

Les documents auteur exacts du 012 et de l'export aux reflets acquis
diffèrent aussi sur les bâtiments procéduraux. Le document du lecteur
012 n'en contient aucun. L'export ajoute la pièce de 8 × 6 m à
X = 46 m et Z = −145 m. Le chemin commun des pièces crée une lumière
intérieure ponctuelle avec ombres. À la pose à 1440p du comparatif,
le rapport confirme une pièce et la trace relève six faces d'ombre,
38 lots et 55 instances cumulées avant rendu. Aucun temps GPU isolé
n'est disponible pour ce travail. Il s'ajoute aux différences de
géométrie et de médias déjà relevées.

La relecture rapproche aussi les mesures asynchrones. Le graphe GPU
vaut 6,50 ms en médiane dans la phase immobile du 012 et donne deux
relevés de 8,76 et 10,48 ms dans le moteur commun. L'opaque donne
2,87 ms en médiane contre deux relevés de 3,29 et 3,95 ms. Les durées
imbriquées ne sont pas additives. Le prochain comparatif visant à
attribuer l'écart au moteur doit tenir compte des charges supplémentaires
de l'export, dont cette pièce et ses ombres.

Reçu `R7-05-tidewater-012-procedural-room-readback-20261117.json`
dans le dossier du comparatif, SHA-256
`984f29ef1fecae9d4758cf0aed5216058eea6cd6521b86b634f72c761363fda6`.
Les 4 303 octets du reçu réutilisent les traces existantes.

<a id="gate-7-tidewater-comparatif-piece"></a>

Un rejeu de diagnostic retire uniquement la pièce dans une copie du
module, via la commande de l'éditeur. L'archive de zone et le
manifeste sont les deux seules entrées de transport modifiées.
Les 2 029 autres entrées et le gameplay restent aux mêmes empreintes.
La première admission a refusé l'empreinte du document de végétation,
dont la révision suit celle de la zone. Le codec commun a recalculé
l'empreinte et validé la zone avant la reprise. Aucun build du runtime.

Le témoin avec pièce et la copie sans pièce réutilisent le même
binaire, les mêmes réglages et la même pose à 1440p. La projection
est prête, avec l'étal, ses cinq colliders, les 83 poissons et Joe.
Les médianes RGB des trois zones échantillonnées sont identiques.
En présentation indépendante avec `SyncInterval=1`, sur 280 puis
310 images, le GPU actif médian passe de 10,57 à 8,82 ms, la moyenne
entre présentations de 14,26 à 12,86 ms et le p95 de 16,76 à 16,91 ms.
Le lecteur 012 relevait 8,16 ms de GPU actif et 14,18 ms au p95.

Les six faces ponctuelles disparaissent dans la copie. La trace
relève aussi trois cascades directionnelles Bevy et 50 instances
sur le rejeu sans pièce, contre zéro sur le témoin avec pièce.
La variation porte sur la pièce et le travail de rendu associé.
La mesure ne donne pas un coût isolé des ombres ponctuelles et
ne qualifie pas la parité du jeu complet. Le module livré conserve
la pièce.

Reçu `stall-actor-room-control-20261117/room-control-readback.json`,
SHA-256 `cfa5c93e4cdceedb23f94206999b5e15c909854c2b0cd2920d437234eb3c1980`.
Les 19 pièces vérifiées occupent 7 289 752 octets sur F:. Le
transport dérivé, son cache et les sorties temporaires ont été
retirés après relecture, soit 886 520 227 octets sur E:.

Le mode de diagnostic existant, `FORGE_RUNTIME_CPU_PROFILE=1` et
`FORGE_RUNTIME_GPU_VIEW_PROFILE=1`, encadre maintenant la passe
d'ombre ponctuelle précoce de Bevy. La mesure porte le numéro de face
et l'identifiant rendu de la lumière. Le mode conserve son ordonnanceur
monothread de diagnostic. Le contrôle Cargo de `forge-module` passe.
Reçu `point-shadow-gpu-scope-check-20261117.log.receipt.json`,
SHA-256 `8db3ad921c1e19a2a3ef113a593bf0455ec6da2e986b0170acbf073ae71f4f5b`.
Le journal et le reçu occupent 1 096 octets.

Un build Release et un seul rejeu Windows avec le module complet
retrouvent les six faces de la lumière intérieure. Les 42 observations
de caméra concordent à 1440p et la projection est prête. La somme des
dernières valeurs GPU par face est de 0,153 et 0,132 ms sur les deux
relevés renseignés. L'opaque principal donne 3,98 et 3,39 ms, contre
0,070 et 0,062 ms pour l'opaque de réfraction. Les valeurs sont
asynchrones. Elles délimitent le coût direct des passes d'ombre et
écartent leur rendu seul comme explication de la baisse de 1,75 ms
observée en retirant la pièce. Le mode instrumenté ne qualifie pas
la cadence de production.

Build de 16 min 33 s, binaire SHA-256
`bd568e0f462edf173d94e9d1fcd9803792a204d0d9a140e2540ff7364d73f9b2`.
Reçu `stall-actor-point-shadow-profile-20261117/point-shadow-profile-readback.json`,
SHA-256 `a425727622cd09b896662fedf4f586dffd7e55b39cea208edbdb29f105d0de95`.
Les 16 pièces vérifiées occupent 7 198 843 octets. Les copies et
sorties temporaires ont été retirées d'E:, soit 7 188 391 octets.

<a id="gate-7-tidewater-cdlod-ownership"></a>

Le cache de tuiles CDLOD partagé par le terrain et l’eau conserve désormais
son index d’entités entre deux sélections. Il le met à jour lorsqu’une
tuile reçoit une entité, devient racine ou quitte le cache. La réparation
des liaisons de capture et de visibilité reste active et écrit uniquement
si la valeur diffère. Le dernier rapport du terrain Tidewater consigne
66 tuiles actives et 77 en cache. Les 16 tests Windows ciblés passent,
dont le changement de racine, le retour de caméra et l’éviction.
Reçu `source-cdlod-retained-ownership-20261117.log.receipt.json`,
SHA-256 `538c5ec02ccb31583ed8db44c31cd8908540fc0b0323bc7e9f3c4b3a07129647`.
Le journal et le reçu occupent 3 092 octets sur F:.

Le build Release Windows termine en 16 min 13 s. Un retrait d’écrans
interrompt le premier lancement avant la pose. Après confirmation
du retour des deux écrans, le rejeu atteint la projection prête et
confirme 43 observations de la pose à 1440p. Le paquet, le scénario,
les réglages et les listes d’ombres correspondent au témoin. Les deux
relevés comptent 284 présentations en mode indépendant à
`SyncInterval=1`. La moyenne entre présentations passe de 14,03 à
14,03 ms, le p95 de 16,01 à 16,14 ms et le GPU actif médian de
10,20 à 10,47 ms. Ce rejeu n’établit aucun gain de cadence. La parité
avec le lecteur 012 reste ouverte.

Reçu `stall-actor-cdlod-ownership-20261117/cdlod-ownership-readback.json`,
SHA-256 `2792207767a0f037f92dabd830cc2c561e2961e630fe28c137f1653e7f5c2ecb`.
Le reçu épingle le binaire et les pièces du rejeu. Les 12 pièces
vérifiées occupent 6 797 164 octets sur F:. Les sorties temporaires
ont été retirées après relecture (7 431 929 octets sur E:).

<a id="gate-7-tidewater-shaders-post-traitement"></a>

Le manifeste de sources du 012 archivé dans `lot-092f57a4fdf3.zip`
confirme par SHA-256 les fichiers historiques relus au commit
`4799ebb3dfa98795d97862f0fed3cd4f6a876887`. Les sept shaders de grain,
d'occlusion ambiante et de flare correspondent au checkout commun après
normalisation des fins de ligne. Les huit shaders de brume correspondent
après cette même normalisation et le renommage de leurs imports et du
drapeau `SOURCE_HDR`. Le chemin `Reference` du moteur commun reprend
ACES, les paramètres de gradation et le masque de mesure d'exposition
64 × 64. La courbe d'exposition partage les valeurs historiques et un
test existant compare les coefficients du profil par défaut. Ce contrôle
porte sur les sources et les réglages du chemin `Reference`. Éclairage
ambiant reçu, ressources liées, dimensions et activation effective des
passes restent propres aux deux exécutions comparées.

La collecte des émetteurs opaques du moteur commun emprunte désormais
les références ECS pendant le tri et les contrôles d'admission. Elle
clone uniquement les références de maillage des émetteurs retenus et
emprunte les références de matériaux. Les filtres de visibilité et
d'opacité, l'ordre des émetteurs et les données transmises au GPU sont
conservés. Le `cargo check` Windows de `forge-module` avec `module-game`
passe. Reçu `source-caster-borrowed-candidates-module-game-check-20261117.log.receipt.json`,
SHA-256 `8a6e805a6a9a47aed8952c81e40ad0a681fa2e9eafb744381fda13969326f57c`.
Le journal et le reçu occupent 1 368 octets. Le gain CPU reste à mesurer.

La sélection de végétation du moteur commun retrouve les plantes admises
par indice de groupe et d'instance, comme le lecteur 012. Le manifeste
du 012 confirme les deux fichiers historiques de sélection et de résidence
au commit cité plus haut. L'admission commune conserve ses contrôles de
source, d'identité et de doublons. La sélection maintient le chevauchement
des niveaux, les compteurs et l'ordre de publication. Le test Windows
ciblé couvre une sélection proche, éloignée et de retour. Reçu
`indexed-source-plant-admission-test-20261117.log.receipt.json`, SHA-256
`640abdee8a27808406773b397326eaf72e23e729ce3b22232b2e0b4f70c38cbb`.
Le journal et le reçu occupent 1 375 octets. Le gain CPU sur le trajet
reste à mesurer.

<a id="gate-7-tidewater-corrections-cpu"></a>

Un seul rejeu Windows Release combine les corrections de collecte des
ombres et d'indexation des plantes. Le paquet, le scénario et les réglages
concordent avec le témoin précédent. Les 43 observations retrouvent
la pose de référence en 1440p. La partie se termine avec la projection
prête, 18 870 entités de végétation, les cinq colliders de l'étal et les
55 entrées d'ombre ponctuelle. Les deux captures montrent les mêmes
capsules de jeu, le terrain éclairé et la végétation animée à cette pose.

Sur la fenêtre de quatre secondes en présentation indépendante et
`SyncInterval=1`, les échantillons passent de 273 à 281. La moyenne entre
présentations passe de 14,58 à 14,22 ms, le p95 de 17,51 à 16,27 ms et
le nombre d'intervalles au-delà de 16,67 ms de 28 à 8. Le GPU actif
médian passe de 10,68 à 10,53 ms et son p95 de 12,12 à 12,49 ms.
Les deux plans de végétation mesurés passent de 1,79 et 2,81 ms à
1,30 et 1,64 ms. Les durées reflètent une exécution de chaque binaire.
Le lecteur historique reste à 8,16 ms de GPU actif médian et 14,18 ms
au p95 entre présentations. La parité reste ouverte.

Reçu `stall-actor-consolidated-cpu-20261117/consolidated-cpu-readback.json`,
SHA-256 `cf634b96ceb0818357efdac1321e0d6657993edc450c595eadc7040832d3927d`.
Le build Release dure 16 min 56 s, binaire
`4aaeb64e06d4d4c10ba62bbd1068fda47958c68ca7cece69bd762ff7fb1f4feb`.
Les 15 pièces relues occupent 7 303 235 octets sur F:. Les sorties
temporaires et copies de mesure sur E: ont été retirées (7 300 345 octets).

Le document de gameplay des deux archives de zone, vérifiées contre les
empreintes du rejeu 012 et du paquet commun, est identique. Il déclare six
acteurs et douze objets. Le module commun ne déclare aucune liaison de
modèle de gameplay. Les capsules sont ses marqueurs de gameplay sans
éclairage. La grande capsule bleue figure aussi dans la capture du 012.
Le GLB animé de Joe relève de l'étal et constitue un autre élément.

Un échantillonnage des deux captures existantes à 1440p, tous les sept
pixels, donne les médianes RGB suivantes, du 012 vers le moteur commun.

| Zone (rectangle, borne haute exclue) | Pixels | 012 | Commun |
| --- | ---: | --- | --- |
| Capsule, x 1197–1357, y 700–958 | 851 | 119 / 198 / 240 | 120 / 199 / 241 |
| Sol, x 850–1151, y 1160–1339 | 1118 | 29 / 37 / 31 | 46 / 40 / 19 |
| Chemin, x 1740–1938, y 1265–1389 | 522 | 36 / 43 / 34 | 57 / 47 / 21 |

Les valeurs proviennent du JPG historique et du PNG commun. Le contrôle
situé montre l'écart sur les surfaces éclairées. Il ne sépare pas
l'éclairage direct, les harmoniques ambiantes et les ombres, et ne
justifie pas de modifier les acteurs pour le comparatif de cadence.

Le fichier d'éclairage acquis `village-lighting.json` a la même empreinte
dans le paquet d'assets archivé du 012 et dans le module commun du rejeu
(`ed8049867e2e64b5438e1d62bbc788b4303a3af571fb8a1f736df2c2e99c7f7b`).
Le module embarque donc déjà les neuf coefficients RGB de référence.
L'heure acquise déclarée, 972 minutes, correspond au démarrage à 16 h 12
du calcul historique. La pose qualifiée passe ensuite à 17 h 45.
Le lecteur 012 conserve les coefficients acquis et les multiplie par le
rapport d'irradiance entre ces deux heures, canal par canal. Le moteur
commun publie la lumière directe de cette même horloge, puis le graphe
HDR remplace les coefficients diffus du tampon partagé à chaque rotation
de l'environnement. Ce choix intervient après la lecture du paquet. Un
rapprochement du rendu doit réutiliser les coefficients déjà embarqués et
le calcul partagé `source_sky::publication::scaled_reference` pour un
module qui choisit ce comportement, tout en conservant le graphe HDR
dans le chemin prévu pour les modules à éclairage ambiant dynamique.

Le moteur commun accepte ce choix dans `solar_clock.diffuse_lighting`
avec la valeur `captured_irradiance` (API module 17). Il mémorise la
première irradiance à l'heure acquise et publie ensuite les coefficients
ajustés dans le tampon de scène. La cubemap HDR continue de suivre le
graphe d'environnement. Les modules sans ce réglage gardent la
publication diffuse GPU. L'export depuis un module d'origine garde le
réglage et sélectionne l'API minimale correspondante.

Le `cargo check` Windows de `forge-module` passe, reçu
`tidewater-captured-diffuse-module-check-20261117.log.receipt.json`, SHA-256
`bcb59456416e32294f9a50518149b278e486288a8a69fcefebd7f9a80cc31d35`.
Le test du contrat d'horloge refuse le choix sous API 6 et vérifie sa
sérialisation et la validation de zone sous API 17. Reçu
`tidewater-captured-diffuse-contract-test-20261117.log.receipt.json`, SHA-256
`1566059ae3998a1d65556d83715066c8be643eb143ad358f5b912a76ab988cad`.
Les quatre fichiers de compilation et de test occupent 2 748 octets.

Le paquet privé sur E: sous `tidewater-captured-diffuse-20261117/`
conserve tous les fichiers du rejeu consolidé par SHA-256. L'écart du
manifeste se limite à l'API 17 et à ce choix d'éclairage. SHA-256 du
paquet `0a899473273b9e1b1813c3b7260e2644f13e3ee291844b046af9ff9c0a7db86b`.
Le paquet de 423 972 947 octets sert de référence pour ce réglage.

<a id="gate-7-tidewater-eclairage-acquis"></a>

Le rejeu unique en Release charge le paquet API 17 à 17 h 45, avec
42 observations prêtes à la pose de référence en 1440p. Il conserve
les 83 poissons, les cinq colliders de l'étal et le rapport d'ombres du
témoin commun. L'échantillonnage visuel reprend les mêmes rectangles.
La médiane RGB du sol passe de `46 / 40 / 19` à `32 / 34 / 19` avec
le choix acquis, contre `29 / 37 / 31` pour le 012. Celle du chemin
passe de `57 / 47 / 21` à `39 / 40 / 22`, contre `36 / 43 / 34`.
Les valeurs convergent, avec un écart résiduel sur le bleu.

Sur 283 présentations indépendantes, contre 281 pour le témoin commun
avec HDR diffus, le GPU actif médian passe de 10,53 à 10,31 ms. La moyenne
entre présentations passe de 14,22 à 14,09 ms et le p95 de 16,27 à 16,73 ms.
Le lecteur 012 reste à 8,16 ms de GPU actif médian et 14,18 ms au p95
entre présentations. Le rejeu confirme l'effet visuel à cette pose.
Les deux binaires et les révisions des scènes historiques diffèrent.
La parité globale reste ouverte.

Reçu `stall-actor-captured-diffuse-20261117/captured-diffuse-readback.json`,
SHA-256 `5c2a2f8aea67a2168a76f503102879a4ed3b26b56db9ea804a62fd5501e0136b`.
Build Release en 18 min 02 s, SHA-256 du binaire
`1b1116ba364f2401c99c3399008a290bab44f4668fd0b9e341e12c3cbed4e8aa`.
Les 16 pièces relues occupent 7 104 618 octets sur F:. Les traces
temporaires sur E: ont été retirées (7 095 835 octets).

Les deux relevés GPU asynchrones de ce rejeu donnent 3,39–4,02 ms pour
l'opaque, contre une médiane de 2,87 ms sur le 012. Les nuages du moteur
commun donnent 0,57–0,66 ms dans ces relevés, pour des médianes historiques
de 1,18–1,19 ms. Les durées ne s'additionnent pas et ne localisent pas à
elles seules le coût des reflets dans l'opaque.

Le paquet 012 et le paquet commun contiennent la même cubemap acquise
(`environment-cube.bin`, SHA-256
`fa25fd9b1f326b6c8af6940218699e9a1ae86b78fb9262e014046cee00bacc31`).
Le code historique du village lie cette cubemap et la publication solaire
retrouvée dans les sources archivées ajuste les coefficients diffus.
Le mode `captured_irradiance` du moteur commun accepte désormais un
environnement sans programmes HDR. Il conserve alors la cubemap acquise
et attend la première publication diffuse à l'heure demandée avant de
valider la scène. Le soleil et la continuation des nuages restent actifs.
Les modules qui fournissent le graphe continuent d'utiliser ses reflets.

Le contrôle de compilation Windows passe, reçu
`tidewater-captured-reflections-module-check-20261117.log.receipt.json`,
SHA-256 `7b8fef0226f40ee9bf89f900a15d1dbbece771da27cf0262b16de93171e15379`.
Le test ciblé de l'horloge vérifie la lecture du mode acquis sans graphe et
conserve le refus pour une horloge standard sans graphe. Reçu
`tidewater-captured-reflections-contract-test-20261117.log.receipt.json`,
SHA-256 `e1d1f8cb84b1a6dea1205947ea8cd8423515f11b47b5bc0845cbe2c599119be2`.
Les journaux et reçus totalisent 2 756 octets.

Le candidat privé `tidewater-captured-reflections-20261117/` sur E:
conserve les ressources restantes à l'identique par SHA-256. La variante
retire les 28 entrées réservées au graphe HDR, soit 555 748 octets de
ressources et notice propres à ce graphe. Le paquet de 423 405 491 octets
(`ec369d8858028d2636359a6108e27e59d06050a87054d5cc3c0a546e5e446c93`)
sert à vérifier l'admission native, la capture et la cadence à la même pose.

<a id="gate-7-tidewater-reflets-acquis"></a>

Le rejeu Release en 1440p charge le candidat et retrouve les 42 observations
de la pose à 17 h 45 avec la projection prête. Le rapport confirme zéro
programme HDR préparé, la publication solaire complète, 438 soumissions
de continuation des nuages et la complétion GPU après l'image requise.
Les 83 poissons, les cinq colliders de l'étal et les listes d'ombres
retrouvent le témoin commun avec reflets dynamiques.

Le sol échantillonné passe de `32 / 34 / 19` à `29 / 38 / 32`, contre
`29 / 37 / 31` dans le 012. Le chemin passe de `39 / 40 / 22` à
`36 / 43 / 34`, égal au 012 pour le rectangle retenu. La grande capsule
reste proche dans les trois captures. Les placements, la végétation en
mouvement et le codage JPG/PNG limitent ce verdict aux zones mesurées.

Le GPU actif médian passe de 10,31 à 10,36 ms sur 283 puis 275 images en
présentation indépendante. La moyenne entre présentations passe de 14,09
à 14,50 ms et le p95 de 16,73 à 17,25 ms. La trace n'établit aucun gain
de cadence et la parité de performance avec le 012 reste ouverte.
Reçu `stall-actor-captured-reflections-20261117/captured-reflections-readback.json`,
SHA-256 `69c04ce31cb83ca82beb185befe2f55100edebcacc854f5fe248fa1e6a9b036d`.
Build Release en 17 min 42 s, binaire
`000e13f5c5ae577f0b96bfc45a939cedcad9b325fbee4e6af355a3fdeffa11d8`.
Les 16 pièces relues occupent 7 178 865 octets sur F:. Les traces
temporaires sur E: ont été retirées (7 167 708 octets).

Les deux relevés asynchrones de ce candidat donnent 3,96 et 3,94 ms
pour la passe opaque. Les points de mesure du lecteur 012 donnent une
médiane de 2,87 ms. Le document du paquet vérifié déclare une élévation
de base nulle, sans corrections de hauteur ni surcharges de matériau.
Le matériau terrain CDLOD utilise désormais dans ce cas l'évaluateur
de hauteur du shader source directement, sans interroger la texture des
retouches à chaque sommet. Les terrains sculptés et les élévations de
base conservent l'évaluateur corrigé. Les pipelines d'ombre et de calcul
conservent leurs fonctions et leurs dispositions de bindings.

Les deux tests ciblés Windows passent, dont le programme corrigé sur
une grille rectangulaire et la conservation du shader source en terrain
neutre. Reçu `source-uncorrected-height-material-test-20261117.log.receipt.json`,
SHA-256 `e058a4ba5b1e38ba4b49c7698694f2a13c83e0d49887daf69d113456605b4af7`.
Le journal et le reçu occupent 1 627 octets. L'effet en jeu reste à mesurer.

Le rapport du rejeu aux reflets acquis confirme l'absence de producteur
de lumières locales source pour ce module. La préparation du shader
terrain de `forge-module` omet leur calcul par pixel quand le module n'a
aucun producteur admis, et garde la variante complète quand il en possède.
Le choix est fixé à l'installation du terrain et conservé lors des
remplacements de document. Le soleil, les ombres ponctuelles Bevy et
l'éclairage sous-marin conservent leurs chemins.

Les trois tests Windows de préparation de la scène terrain passent. Ils
vérifient notamment la présence de la fonction locale dans la variante
éclairée et son absence dans l'autre variante. Reçu
`source-terrain-local-light-variants-20261117.log.receipt.json`, SHA-256
`d98a73da66bf35cfdef01cbac856093c8aa5e3430042a100b746c08e5ee0ce40`.
Le journal et le reçu occupent 1 543 octets. Les deux changements du
shader terrain doivent partager le prochain build et le même rejeu natif.

<a id="gate-7-tidewater-shaders-terrain"></a>

Un seul build Release et un seul rejeu Windows exercent les deux
changements du terrain. Le paquet aux reflets acquis, le scénario et
les réglages ont les mêmes empreintes que le témoin. Les 42 observations
de la caméra à 1440p, la projection prête, l'identité d'environnement et
les listes d'ombres concordent. Les médianes RGB de la capsule, du sol et
du chemin sont identiques sur les rectangles des deux captures.

Sur les fenêtres de quatre secondes en présentation indépendante,
275 puis 284 images, la moyenne entre présentations passe de 14,50 à
14,03 ms, le p95 de 17,25 à 16,01 ms et le GPU actif médian de 10,36 à
10,20 ms. Les deux relevés GPU asynchrones de l'opaque donnent 3,95 et
3,29 ms, contre 3,96 et 3,94 ms au passage témoin. Ils ne sont pas
appariés par image et n'isolent pas l'effet de chaque changement.
Le lecteur 012 reste à 8,16 ms de GPU actif médian et 14,18 ms au p95
entre présentations. La parité de performance reste ouverte.

Reçu `stall-actor-terrain-shaders-20261117/terrain-shaders-readback.json`,
SHA-256 `2332229ab24f990a9c26625005d139f73959b931831d7ab8b0548fed14ea2da6`.
Build Release en 17 min 04 s, binaire
`ca69e62ac185cc95876f7cb5494d4ce701c07ab8ad732672cee8eb34f0363838`.
Les 15 pièces relues occupent 7 213 552 octets sur F:. Les copies et
sorties temporaires du rejeu sur E: ont été retirées (7 208 223 octets).

<a id="gate-7-tidewater-terrain-peinture-fixe"></a>

Le manifeste archivé du lecteur 012 confirme l'empreinte du fichier
`tidewater_object_shadow/receiver.rs` au commit
`4799ebb3dfa98795d97862f0fed3cd4f6a876887`. Le lecteur utilisait déjà
la même sélection des paramètres d'ombres par `currentShadowParams`.
Les programmes de terrain et de CDLOD acquis correspondent aussi aux
programmes intégrés au moteur commun par SHA-256. Les deux rapports
d'exécution relèvent 66 tuiles de terrain à la pose fixe. Cette égalité
ne renseigne ni le nombre de soumissions ni leur coût.

La variante expérimentale réutilise le programme de terrain source sans
le code de peinture en direct quand le document ne contient aucune peinture.
Elle garde la variante peinte pour les cartes retouchées. Les six tests
ciblés de préparation terrain et le contrôle Cargo de `forge-module` passent.
Reçus `source-terrain-fixed-paint-retry-20261117.log.receipt.json`, SHA-256
`cf986590e56d469ca20d5d11b4b35f1f8ab31917fe01d55350b4c39db515fdad`, et
`source-terrain-fixed-paint-runtime-check-20261117.log.receipt.json`, SHA-256
`4262e7da347ea67feb652845f5c9ec4dc33fd44f8617a26248d492da8a536ea6`.

Un seul rejeu Release confronte la variante au témoin sur le même paquet,
la même caméra et les mêmes réglages. Les 42 observations de caméra sont
prêtes, les ombres conservent les mêmes comptes et les trois zones
échantillonnées conservent les mêmes médianes RGB. En présentation
indépendante à `SyncInterval=1`, le GPU actif médian passe de 10,20 à
10,57 ms, la moyenne entre présentations de 14,03 à 14,26 ms et le p95
de 16,01 à 16,76 ms. Les deux mesures asynchrones de l'opaque donnent
3,62 et 3,22 ms. L'unique comparaison n'établit aucun gain de cadence.
La variante est retirée du checkout et les sources des variantes de
production modifiées ont été retrouvées à leurs empreintes du témoin.
La parité avec le lecteur 012 reste ouverte.

Le build Release du rejeu a pris 16 min 51 s. SHA-256 du binaire
`2a4cac054a260cb322f259d433ba2f12ab3efbc06a58683011a49c750670dc43`.
Reçu `stall-actor-fixed-paint-20261117/fixed-paint-readback.json`, SHA-256
`63a09c145d7453b53befe34e60fd2aa989422fc78aefc7350a1dc6392b2dca33`.
Le diff reproductible `fixed-paint-trial.patch` s'applique au checkout restauré,
SHA-256 `c3cce88420571b70a7f6b03515604ffc50f7182793c8295c0b4424bad0a415e3`.
Les 16 pièces conservées dans ce dossier totalisent 7 194 248 octets.
Les copies et sorties temporaires de ce rejeu sur E: ont été retirées
après vérification (7 177 364 octets).

<a id="gate-7-tidewater-plein-ecran-exclusif"></a>

Le plein écran exclusif déclaré par le jeu est désormais demandé sous
Windows après la création de la fenêtre et la détection de l'écran
principal. Le moteur commun reprend la demande de plein écran Bevy
utilisée par Tidewater avec le mode vidéo courant de l'écran.
L'absence de mode compatible ou le refus de la fenêtre déclenche le
retour fenêtré. La bascule F11 garde son usage fenêtré/sans bordure.
Les trois tests ciblés Windows passent. Reçu
`native-exclusive-display-regression-20261116.log.receipt.json`, SHA-256
`570384ff14327487317de16db0b5f09d67abf09a25fbf7997b6055f4afb83f49`.

Le rejeu Release conserve le même module, les mêmes réglages et la
réfraction du témoin. La fenêtre signale la demande plein écran appliquée,
les 43 observations de caméra concordent en 1440p, et les 257
présentations utilisent `Hardware: Legacy Flip` avec `SyncInterval=1`.
La moyenne est de 15,49 ms et le p95 de 18,87 ms, contre 17,37 ms et
22,57 ms dans le témoin sans bordure qui alterne deux modes. Le nombre
d'intervalles au-dessus de 16,67 ms passe de 105 sur 229 à 52 sur 257.
La médiane GPU active en mode exclusif est de 14,56 ms. Le témoin
sans bordure relevait 10,80 ms dans sa tranche de présentation
indépendante. Ces valeurs portent sur des modes de présentation distincts.
Le lecteur 012 reste la référence de comparaison en mode indépendant.

Reçus sous `stall-actor-native-exclusive-display-20261116/` :
`comparison.json` (`23732642d6b8c8044f9b7f0f32fd37b0f443a76402e3509b7610975c18c93b59`)
et `module-runtime-report.json`
(`ba71c8b02a4b36649749b415cf66f51d171ad30e06ba8ea97f12e8db8f3a08f0`).
Reçu `native-exclusive-display-runtime-build-20261116.log.receipt.json`,
SHA-256 `7a90f23ec831b15f9e8c8266e035162cdf136c2bf630d565f47ac3ad3fdb4be5`.
Douze fichiers ont été vérifiés sur F: (1 120 181 octets) et 7 280 996
octets de copies et de sorties temporaires ont été retirés d'E:.

<a id="gate-7-tidewater-licences-du-rejeu"></a>

La relecture R7-08 du module exact des mesures vérifie ses 2 059 fichiers
et les 23 chemins de licence déclarés, dont la notice Rocketbox de Joe.
Le transport, le module, son origine et le texte des modifications
déclarent un usage privé. Le catalogue associe les quatre dérivés
Quaternius à un texte CC0-1.0 et maintient pour chacun une déclaration
`redistribution: forbidden`. Cette relecture consigne le périmètre de
qualification de la scène. Reçu
`R7-08-tidewater-stall-actor-rights-readback-20261116.json`, SHA-256
`9ecb4fd73f53dd44dff4014f0f9b7ca26c68e37755e011c5aed90e45400b208e`.

Le module aux reflets acquis utilisé pour le dernier comparatif a été
relu à son empreinte exacte. Ses 2 031 entrées déclarent 22 chemins de
licence, tous relus par SHA-256. Le transport, le document courant et
son origine déclarent toujours `private_local_only`. Les quatre
dérivés Quaternius conservent leurs déclarations précédentes. Le
bruit de tramage des nuages a toujours les mêmes octets que le
fichier amont épinglé. Les métadonnées du volume désignent également
le même fichier source. Le choix des reflets acquis ne change donc
pas la réserve sur les textures de nuages. La variante aux textures
générées reste une preuve distincte.
Reçu `R7-08-tidewater-captured-reflections-rights-20261117.json`
dans le dossier du comparatif, SHA-256
`5a4b1139772bfecf62feb59458f27351dafd1d2b290b14a6f939dbd8472e0225`.
Ce rapprochement ajoute 6 195 octets et réutilise les modules existants.

La relecture du même module relie par SHA-256 les quatre modèles actifs
et les six versions historiques aux recettes des modèles Quaternius du
049. Le pack source est déclaré CC0. Le catalogue courant garde les quatre
modèles sous `redistribution: forbidden`, sans preuve attachée pour les
retouches. Les six versions citées par la notice historique restent
couvertes par la qualification locale privée. Tout futur export
redistribuable doit établir les droits sur ces retouches et sur le contenu
auteur de l’origine privée. [Licence du pack](https://quaternius.com/packs/medievalvillagemegakit.html).

L’étal relie ses ressources à la notice Poly Haven et à l’atlas des
enseignes rasterisé. Aucun fichier de police autonome n’a été trouvé par
extension dans ce transport. Le modèle animé de Joe correspond au dérivé
de la recette de conversion et à la notice MIT Rocketbox embarquée.
[Crédits des accessoires](https://raw.githubusercontent.com/dgreenheck/tidewater/1438b1abfcaee3267092b75573014f4d9b4a983c/public/models/props/CREDITS.md)
et [licence Rocketbox](https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/LICENSE.md).

Reçu `R7-08-tidewater-exact-model-and-stall-rights-20261117.json`,
SHA-256 `e262697ace81ed62beb44b8ac5c7bbfe9461f0c7703d500af43b0b24565ba545`.
Le reçu ajoute 8 051 octets sur F: et réutilise les transports existants,
sans fabrication, sans lancement et sans modification du catalogue.

Le module exact du sprint 1080p, `tidewater-room-control.orpmod`
(SHA-256 `96e726e4939187df502b663c0c47c38ef837c0357444342c4b28d6d56242cb6f`),
est refusé par `orpmod-inspect --publish` en raison de son statut privé.
Son origine déclare également `private_local_only`. Ses quatre modèles
`quaternius-townhouse.glb`, `quaternius-cottage.glb`,
`quaternius-inn.glb` et `quaternius-merchant.glb` sont déclarés
`redistribution: forbidden` sans justificatif attaché.

La variante aux textures générées a ensuite été appliquée au module
courant aux reflets acquis. Les huit textures reprennent les octets
du donneur 058. Le bruit et les sept niveaux du volume d'origine
sont absents aux empreintes du nouveau transport. Les deux index
d'environnement et le manifeste du module sont les seuls autres
fichiers modifiés. Les 2 020 autres entrées, le cube de reflets
acquis, la zone et le gameplay gardent leurs empreintes.

Un rejeu Windows sur le binaire Release disponible atteint la projection
prête à la même pose à 1440p, avec 556 soumissions de nuages.
Les médianes RGB des quatre rectangles échantillonnés sont identiques
à la capture avec les textures d'origine. Les binaires des deux
captures diffèrent. Le rejeu valide le chargement et le rendu à cette
pose. L'export par Studio du module courant reste à vérifier. Le
transport conserve la portée privée, l'origine du cube acquis et les
réserves sur les autres médias.

Reçu `stall-actor-generated-clouds-current-20261117/generated-clouds-current-readback.json`,
SHA-256 `541fec4a1a5e3cee42fbc9fe79e553e2137b64b8bb91b813920aec9c3039ce90`.
Les 17 pièces dont les deux pilotes reproductibles occupent
7 189 442 octets sur F:. Le module dérivé, son cache et les sorties
temporaires ont été retirés après relecture (853 984 490 octets sur E:).

Le cube acquis et les coefficients diffus provenaient également de
l'ancien ciel. Le lecteur amont calcule les reflets et l'irradiance
à partir de la radiance qui compose le panorama des nuages. Le
[script d'acquisition](../scripts/tidewater/export-environment.mjs)
accepte maintenant les sept mips et le bruit générés en entrée.
La capture au même point de vue utilise ces huit fichiers du 058
et la même révision amont. Elle produit un nouveau cube et neuf coefficients RGB d'irradiance.
[Source de la capture et du préfiltrage](https://raw.githubusercontent.com/dgreenheck/tidewater/1438b1abfcaee3267092b75573014f4d9b4a983c/src/sky/Environment.js).

Cette nouvelle variante remplace aussi le cube et les coefficients
diffus du paquet. Les 2 018 autres entrées et le gameplay gardent leurs
empreintes. Le même binaire que pour le rejeu avec les seules textures
générées charge le module, retrouve la pose à 1440p, termine avec
projection prête et enregistre 556 soumissions de nuages. Les médianes
RGB concordent sur les quatre rectangles échantillonnés. Le paquet
conserve son origine privée et les réserves sur les autres médias.
Le réexport depuis Studio reste à vérifier.

Reçu `stall-actor-generated-sky-20261117/generated-sky-readback.json`,
SHA-256 `e01feb64e01eeeef61be2e1274f7c610012df07b4d6e61141a0f345a9716aa88`.
La capture du cube et de l'irradiance est conservée dans
`captured-sky.zip`, dont chaque entrée a été relue par SHA-256.
Les 20 pièces durables occupent 7 860 031 octets sur F:. Après
vérification, le transport dérivé, son cache et les sorties
temporaires ont été retirés (856 235 995 octets sur E:).

Le 17 novembre, le service natif `build_module_with_history` utilisé
par Studio réexporte cette variante à partir du paquet admis. La relecture
retrouve les 267 ressources d’environnement octet pour octet, dont les
12 entrées du nouveau ciel. Les liaisons de la zone, du terrain, de l’eau,
de la végétation, de l’étal, de Joe et des poissons sont conservées.
Les 29 documents de provenance et de droits gardent leurs empreintes.
Le mode redistribuable refuse l’origine privée. Le contrôle réutilise la
bibliothèque Windows déjà compilée. Le parcours dans l’interface Studio
et les droits de redistribution de Tidewater restent ouverts.

Reçu `stall-actor-generated-sky-20261117/R7-08-generated-sky-native-reexport-20261117.json`,
SHA-256 `8ebb0283835521f05c423d8d97f7383b4eec2d2be2004e4b4837dc31ffc8e0ef`.
Le reçu et le pilote de reproduction vérifiés occupent 14 654 octets sur
F:. Les copies temporaires de préparation et de réexport ont été retirées
après relecture, soit 2 144 003 966 octets sur E:.

La relecture R7-08 régénère en mémoire les huit textures Forge et retrouve
exactement leurs empreintes dans le reçu de réexport natif. Aucune des
huit empreintes historiques ne correspond. Le volume compressé et le bruit
reproduisent les deux empreintes d’entrée de la capture du ciel. Les 38
entrées du ZIP sont vérifiées, dont la série de faces et les coefficients
d’irradiance. Le cube réassemblé correspond au cube du réexport et diffère
du cube historique. La provenance de ces substitutions est établie pour
cette variante. L’origine privée, les retouches des bâtiments Quaternius et
les droits des autres médias restent à traiter avant toute redistribution
de Tidewater. Le réexport en interface Studio reste ouvert.

Dans le même dossier, reçu `R7-08-generated-sky-source-readback-20261117.json`,
SHA-256 `58592cebed3d56beed39546781ff98ace32957ac9254da986b7202424003f864`.
Le reçu de 2 423 octets réutilise les pièces existantes. Aucune copie
de module ou sortie temporaire n’a été produite.

Les 2 058 fichiers du déploiement précédent correspondent à son transport.
Le contrôle automatique a refusé le retrait de ce dossier temporaire
sur E: (414 350 753 octets).
Le retrait ciblé a ensuite abouti sur
`.build/tmp/gate7-stall-candidate-20261116/stage`, après relecture SHA-256
des 2 057 fichiers déclarés et de leur manifeste de transport
`1337b0a22f2ae34c9944e242d535b53bb9f6247012d84c41b8ca6f1077291096`.
L'archive source vérifiée a pour SHA-256
`dd090b2e7d8cc66739a006eb65cf6d0fa2ae7a2f84f85a95b4320dcbd06326ca`.
Le contrôle des entrées du rejeu avec Joe passe avec son propre paquet
et son cache.

Les journaux du moteur commun comptent 8 366 entrées de végétation
importée sur 9 422 dans les trois cascades directionnelles de Bevy
pour une vue du village. L'herbe utilise déjà le chemin d'éclairage
source, mais son matériau laissait la détection des récepteurs natifs
conserver ces entrées. Le moteur reconnaît désormais les matériaux
de végétation dont le chemin source est actif. Un matériau natif ou
non résolu conserve la liste de Bevy. Les trois tests ciblés passent,
dont la séparation des ombres directionnelles et locales. La mesure
GPU après correction reste à faire. Reçu
`tidewater-grass-source-sun-20261116-test.log.receipt.json` à la racine
de la campagne, SHA-256
`8517a4dc1cd1cae7042ce3fc882ff5dadef696aaf493bb047d5f034633a837a1`.
Le runtime de qualification compilé a pour SHA-256
`f46a56a761703296b35ffbb379944046a12b5eeab147dbde446e2547361e744d`.
Reçu `tidewater-grass-source-sun-20261116-release.log.receipt.json`,
SHA-256 `507e0fec25d9ac0f7833e87efeb942a6a339e996957b0d440f1eee73e2878ac9`.
Une fois la conversion arrêtée, le comparatif PresentMon au même point
mesure 15,59 ms en moyenne et 17,27 ms au p95 sur 256 présentations.
Le binaire précédent mesurait 15,75 et 17,02 ms. Les deux essais gardent
la pose, le viewport et les réglages, sans présentation écartée. La
médiane GPU active passe de 11,79 à 12,33 ms. Les deux journaux comptent
toujours 8 366 entrées de végétation source sur 9 422 candidats aux
cascades d'une caméra. La correction du matériau d'herbe est insuffisante.
Reçu `R7-05-tidewater-grass-source-sun-presentmon-20261116.json` sous
`tidewater-performance/runtime-initial-borderless-20261115/`, SHA-256
`78d7047e6178be293a713ba2cf83e459da8a729a71eff21ee4b41a0ca4a66e6e`.

Le profil courant utilise le terrain CDLOD et les trois projections
d'eau source. Le programme de terrain du module correspond aux octets
du programme moteur qui lit les ombres source. Le moteur reconnaît
cette installation CDLOD et les matériaux d'eau dédiés. Un programme
CDLOD différent conserve le repli Bevy. Le profilage consigne le
premier récepteur qui conserve les cascades lorsqu'il en reste un.
Les trois tests ciblés passent, dont le repli avec un terrain CDLOD
 sans programme moteur reconnu.
Reçu `tidewater-source-sun-receivers-20261116-test.log.receipt.json`,
SHA-256 `63c601142546c7b12caeb6ea14390119c5c57bd69a16bb30e3a66e41239cffe0`.
Le runtime Release portant cette correction a pour SHA-256
`b204380fc5b6d07c3be1f514db40a194c349bb5d94f7a1d1d0150755102d9ab3`.
Reçu `tidewater-source-sun-receivers-20261116-release.log.receipt.json`,
SHA-256 `64b80e3d162491f1856defd75fbf677feb5da4136f71f879ca3bcb68c85506c8`.

Le rejeu sur ce binaire mesure 16,16 ms en moyenne et 18,27 ms au p95
sur 247 présentations, aux mêmes réglages et à la même pose. La médiane
GPU active est de 12,42 ms. Aucune présentation n'a été écartée. Le
profilage identifie le terrain CDLOD comme premier récepteur conservant
les 8 366 entrées de végétation dans les cascades Bevy. La capture
contrôle les processus aux bornes. Le fil de conversion travaillait
sur la publication de fichiers pendant cette période. Reçu
`R7-05-tidewater-authored-cdlod-routing-observation-20261116.json`,
SHA-256 `87ff7080d263b0871b3b5cfb2c55c78cf1f2b3cde1c1e759dfe92e53d6f1b30c`.
Le terrain auteur installe le fragment par un autre chemin que le
chargeur initial. Il publie désormais aussi l'usage des ombres source
 lorsqu'il installe le programme moteur reconnu. Les trois tests de
 préparation et de publication du terrain auteur passent, dont la
 préservation de la configuration après un chargement refusé. Reçu
`tidewater-authored-cdlod-sun-20261116-test.log.receipt.json`, SHA-256
`14fc2dac32ea0b78bdabe649b0ab79261755cb46deb0b16d20358af035a21e35`.

 Le runtime Release `d5422e57b6f970e93f7821707467d266549f30a6efb7e4ae62a804ab946e5fa9`
 valide cette installation du CDLOD. À pose, module et réglages identiques,
 le rejeu mesure 16,24 ms en moyenne, 18,90 ms au p95 et 12,49 ms de GPU
 actif médian sur 245 présentations, sans présentation écartée. Les
 maillages dérivés de la réfraction deviennent les premiers récepteurs
 détectés et les cascades Bevy contiennent encore 8 366 entrées de plantes
 par caméra dans le relevé CPU. Les fragments de la réfraction conservent
 l'éclairage des matériaux source. Le moteur marque désormais ces seuls
 maillages dérivés `NotShadowReceiver`.
 Reçu `R7-05-tidewater-authored-cdlod-routing-comparison-20261116.json`,
 SHA-256 `3fc503cbb1b2be3e8fe35bc9d0b6d7394ce9ee2d9cdf36a3c4a4ca8347bb4d34`.
 Les huit fichiers de capture ont été vérifiés sur F:. Le dossier
 temporaire correspondant sur E: contient encore 1 272 467 octets de
 copies et de réglages reproductibles. Le retrait a été refusé par
 l'environnement d'exécution.

 Le runtime Release `4a16a4d6c41b10d578a850a6c26d073c5af4f427dd8256ac4b53bac1678d468e`
 rejoue la même pose, le même module et les mêmes réglages. Le relevé CPU
 ne trouve plus de plantes dans les cascades Bevy. Le rendu prépare une
 vue par cascade, avec 277, 380 et 399 instances, contre 1 507, 2 716 et
 5 199 précédemment. Les six faces de l'ombre ponctuelle gardent leurs
 nombres d'instances. Les ombres du programme source restent prêtes avec
 les mêmes cinq lots de plantes et 393 casters opaques.
 Sur 250 présentations en quatre secondes, sans présentation écartée,
 la moyenne passe de 16,24 à 15,93 ms, le p95 de 18,90 à 18,43 ms et la
 médiane de GPU actif de 12,49 à 11,60 ms. La parité avec le lecteur 012
 reste ouverte et les deux scènes ne transportent pas les mêmes poissons
 et étals. L'essai a contrôlé les processus aux bornes.
 Reçu `R7-05-tidewater-refraction-shadow-routing-comparison-20261116.json`,
 SHA-256 `3ecb2d22d2de8c6f8e020fc8b7ca86ab194ab6cbd8f9f3250115727abc0fc9ac`.
 Le reçu de compilation `tidewater-refraction-sun-20261116-release.log.receipt.json`
 a pour SHA-256 `d5a7c2472828858c9b9cddd923d72e7c79d64c1d6022d38fe680d9d157348ace`.
 Les huit fichiers de capture sont vérifiés sur F:. La copie temporaire
 et ses réglages occupent 1 272 609 octets sur E:.

Les fichiers d’essai remplacés sur E: ont été retirés après relecture
des preuves, soit 753 393 382 octets. Le paquet solaire (298 652 482
octets) et son cache (298 322 364 octets de payloads) restent
disponibles dans le sous-dossier temporaire `solar` pour ce parcours.

<a id="gate-7-import-grands-modules"></a>

### 16 novembre — Import Studio du paquet Tidewater solaire

Le producteur de workspace précédent refuse le paquet de 298 652 482
octets avant de créer le workspace. Le Studio 057 partage son plafond
d’origine de 256 Mio. Le lecteur du moteur avait admis ce même paquet.
Reçu `old-studio-origin-budget-rejection.json` sous
`tidewater-performance/runtime-initial-borderless-20261115/`, SHA-256
`6440330b547c26266614a6a455367bb495bb33d3eea02246334faa5fc7043ec0`.

Le sélecteur du Studio lit maintenant les modules par blocs de 8 Mio.
L’hôte borne l’archive à 512 Mio, exige des blocs consécutifs de la
fenêtre qui a lancé le transfert et admet l’archive complète avant
l’adoption du module. Le réexport applique le même budget à l’origine
conservée. Les contrôles Cargo passent pour le Studio natif et le frontend
WebAssembly. Reçus `tidewater-large-module-studio-native-check-20261116.log.receipt.json`
et `tidewater-large-module-studio-wasm-check-20261116.log.receipt.json`, SHA-256
`56ac02547d6c3bf979e539a8cd98cdad0cf09491343fae457c94d8ea050933c3` et
`d6b6f7061f8ece214445d5f09341762df06561d8e9c0b87b69aee2383fe6feca`.
Le frontend Release et le Studio natif ont été construits dans le cache
commun avec des entrées stables. Le Studio reconstruit a importé le paquet
réel depuis le sélecteur et sauvegardé le projet. Après fermeture normale
et relance, la carte Tidewater island se rouvre et affiche le littoral.
Le Studio a produit un nouveau module par « Exporter le module complet ».

Le CLI vérifie l’empreinte de l’origine conservée dans le workspace, le
statut privé, la même zone, les mêmes archives et le même gameplay. Les
liaisons et les empreintes des ressources de terrain, d’océan, de ressac,
d’environnement et de l’horloge à 17 h 45 concordent avec le paquet
importé. Le réexport de 299 684 362 octets a pour SHA-256
`b32a9a89c6d1e14cd3f42f3e12c9bbfad72add34e50a0215fa4474ad7cf25ca9`.
La préparation de l’export a généré 1 586 copies de fichiers. Leur
relecture par taille et SHA-256 dans l’archive a permis de retirer
299 350 001 octets. Le projet sauvegardé, l’origine et l’export restent
dans l’espace de qualification pour les contrôles de distribution.

Reçus sous `tidewater-performance/runtime-initial-borderless-20261115/large-module-studio-build-20261116/`.
`studio-build-proof.json`, SHA-256
`fc6de51ba2d96ef2c164bdc4355cd434c7b0ac9dafd7fe6ef0af3eece37e7948`.
`R7-04-tidewater-solar-studio-reexport.json`, SHA-256
`98ade15f157b058d0bf19392d6768d43c760dd7fb3647688f58e1e5960c29f5d`.
`studio-tidewater-reopened.jpg`, SHA-256
`605e6b738f9bb70e6a002aaf87b95f8cce4d6683b20731a5d03b7ca991f86186`.
Ce parcours concerne le Studio reconstruit sur l’hôte et le réexport
du module. Le jeu complet et le Setup de ce build restent à qualifier.

Le CLI de livraison relit les 18 chemins de licence déclarés par le
paquet Tidewater solaire, dont six textes aux empreintes distinctes.
Le contrôle de publication le refuse avec le statut privé attendu.
Reçu `R7-08-tidewater-solar-private-notices.json`, dans le même dossier,
SHA-256 `778a1d60e9c641fd92403cda054854e74bf1c23cada24938c117f1d1471a1b54`.

<a id="gate-7-setup-windows-058"></a>

### 16 novembre — Setup Windows 058 et export du jeu Tidewater solaire

Le CLI de livraison, le moteur joueur et le launcher Windows ont été
reconstruits dans le cache commun. Leurs trois reçus `BUILD_ONLY`
confirment la stabilité des entrées pendant la compilation et épinglent
le même manifeste source, SHA-256
`52322c8b68c5d83893b38fbe928d235813f669b32296011c8e74c8a8327aaf62`.
Les 3 511 entrées de compilation correspondent à celles du Studio qui
a importé et réexporté Tidewater solaire. Le graphe Windows exact couvre
727 identités dans cinq périmètres. L’audit a vérifié la réutilisation
des textes du lot 057, dont 65 compléments et 17 notices du code Forge.

Reçus sous `tidewater-performance/runtime-initial-borderless-20261115/large-module-studio-game-kit-20261116/`.
`delivery-cli-build-proof.json`, SHA-256
`2804a8f56942a7c7759e0e44e57d2c00c82be5c2a746baeffa202fb53e846bcd`.
`module-game-build-proof.json`, SHA-256
`226b2996ba42672716776356b9c8bbf1d84fc0d8f6efdd96c931d33b7b45fc25`.
`standalone-launcher-build-proof.json`, SHA-256
`b1984847c41a708cd5842088f827976cd09f709575088a688553ea3787221628`.
`notice-candidate.json`, SHA-256
`d15213846c4aecf50c23e64698d2e43b3db9ed64945364d2b1f4f33aee638d2b`.
Le frontend Release et le kit joueur privé ont été intégrés au Studio.
`prepare-frontend-build-proof.json`, SHA-256
`e5f1c8993c9a12b53b0a128e714f879d77d77d6bcc7fbbe9af4d1e8be502b20c`.
`game-kit-assembly-receipt.json`, SHA-256
`8dc6bd813a4547c2b44a6c4f6821c425dd6b376dda60fd01f53617195ba64343`.
Le Setup de 78 472 512 octets a pour SHA-256
`32ad639054b6782bc1d07461393240ddadc55486ba94b879c1b854169d2ac2da`.
La relecture du Setup exact vérifie les 425 membres du paquet Forge,
dont les dix archives sources, et les binaires du Studio et du kit.
`setup/R7-08-studio-058-setup-readback.json`, SHA-256
`cc899c60e66e024ad757d3bceb95f865ec565824c318aac066dfc34565f1bacd`.

Les PDB des quatre binaires Windows 058 correspondent aux identifiants
CodeView des exécutables livrés. Le contrôle confirme les imports
Visual C++ et ne relève aucun objet des archives statiques `libcmt.lib`,
`libvcruntime.lib` et `libucrt.lib`. Le Studio, son CLI et le launcher
contiennent chacun les six objets du chargeur statique WebView2 SDK.
Le moteur joueur n'en contient aucun. Le texte BSD 3-Clause vérifié
accompagne le Studio et le jeu installés. Les runtimes Evergreen et
Visual C++ sont des prérequis externes du Setup 058.
Reçu `R7-08-windows-external-crt-game-kit-binaries.json` dans le dossier
de build du 058, SHA-256
`8b6a0c3cfdd66802d8fa255fb9e3668a534486569aeb61f8a5c0eb3b09631999`.
Reçu `R7-08-windows-058-webview2-loader-linked-readback.json` à la racine
de la campagne, SHA-256
`4c73f87e0e4dd24045b7b7e12faac512b1831e3728d67dc512495d3deda60446`.

Le Setup 058 est rapproché du compilateur NSIS 3.12 et de son `COPYING`.
Les trois plugins x86 Unicode et le bitmap concordent aux empreintes.
Le journal confirme la compression zlib globale, et le désinstallateur
généré est épinglé dans le même reçu.
`R7-08-studio-058-nsis-attribution.json` à la racine de la campagne,
SHA-256 `9e00a272536cf3363988608e3c0e1290ab4fd94f4f2aceb7f979d9f610c80588`.

Pour les dépendances MPL‑2.0, les PDB des quatre exécutables natifs 058
retrouvent les neuf crates Symphonia et `option-ext`. Le Studio et le jeu
installés fournissent les dix archives exactes, leurs licences et les
notices. La préparation depuis les verrous Cargo reproduit les 25
fichiers livrés. Les quatre autres crates MPL‑2.0 de l'index conservateur
(`cssparser`, `cssparser-macros`, `dtoa-short`, `selectors`) ne figurent
dans aucun des PDB ni dans les graphes de dépendances normales hors
macros procédurales des cinq périmètres. Ces résultats portent sur les
enregistrements des linkers et les graphes Cargo. Reçu
`R7-08-windows-058-mpl-native-source-coverage.json` à la racine de la
campagne, SHA-256 `abe89a23cc94ce6ce64804df12199cdf1ea4321062f657b8bac6bd358345ee9a`.

La relecture des tables Tauri du Studio et du launcher 058 retrouve
respectivement 16 et 3 entrées. Le JavaScript, le CSS et les autres
fichiers non HTML correspondent aux empreintes de compilation. La
sérialisation de `index.html` préserve le contenu des scripts. Pour le
launcher, les entrées sont `desktop.js`, `index.html` et `styles.css`.
Reçus dans le dossier de build du 058
`R7-08-studio-058-embedded-web-assets.json`, SHA-256
`81c9775976ba84e9f381f05a5f64921589125019dbe70198edf7a652ec2c63c2`,
et `R7-08-launcher-058-embedded-web-assets.json`, SHA-256
`391482910d481018e34d56dff3bd88e9aa13791265d384627805d33fb47615d5`.

L’installation du Studio par CLI a vérifié le paquet dans un dossier
de qualification sur l’hôte. Depuis le Studio installé, le projet
Tidewater solaire a été sauvegardé avec Tidewater island comme carte
de départ et le profil Moyen. « Créer les paquets privés » a exporté
le launcher, le jeu et un module de 299 684 296 octets. Le CLI exact
vérifie les liaisons de terrain, d’océan et d’environnement, les
empreintes de la zone et le gameplay. Il refuse la publication du
module privé. `studio-058-host-launch.json`, SHA-256
`0d8c39212f361b87dc1869ffdb3ef164fb8099ae206c3d1ea5db0b633eb562a9`.
`R7-04-06-tidewater-058-private-export-install.json`, SHA-256
`7d5ead8653af5afd2bf7d22504be8fab3f7c89219786dd2d7fdde90045a90806`.

Le rapprochement R7-08 recalcule les 422 chemins de licence déclarés du
Studio 058 installé, les 421 du jeu et les 29 du module installé. Les
quatre index et les dix archives sources du kit concordent entre les deux
applications. Le CLI exact du Setup admet le module reproductible du
Grand Plateau CC0 en mode publication et refuse Tidewater solaire. Les
descripteurs vérifiés déclarent un usage privé pour le Studio et le jeu.
Reçu `setup/R7-08-windows-058-role-license-readback.json`, SHA-256
`6c8471474f1faf56c4cdb2110020370ac5fc40b0891d546ce18b0c049d1ac04b`.

La relecture des octets du module Tidewater solaire exporté par le 058
retrouve les huit textures de nuages du 049, soit 1 202 468 octets. Le
bruit et les sept niveaux du volume figurent chacun une fois dans le
transport. Le reçu lie chaque entrée à son équivalent généré par Forge
et vérifie les empreintes dans le transport, l'inventaire du module et
les références d'environnement. Reçu
`R7-08-tidewater-058-cloud-media-readback-20261116.json` à la racine de
la campagne, SHA-256
`be8ecd60fbc01039ab4055524cf01a39cd31cfbae47861b3f31cbd1df2be377f`.
La variante de qualification du 058 remplace les huit textures par
les octets générés et essayés sur le 049. Les deux manifestes
d'environnement et leurs références ont été mis à jour. Le CLI exact
du 058 admet le module privé et la relecture ne retrouve aucune des
huit empreintes de textures d'origine dans le transport. Le seul
nouveau paquet sur E: occupe 299 691 306 octets. Il sert à l'essai de
rendu et de réexport depuis le Studio. Le paquet exporté d'origine
reste disponible pour reproduire le parcours 058. La variante ne
qualifie pas les droits des autres médias. Reçu
`R7-08-tidewater-058-generated-cloud-variant-20261116.json`, SHA-256
`fae4a9c3a20f6b1927f455b636264130c5bb41d325036d910ff6843379dd8b25`.

Le contrôle natif du kit refuse désormais un manifeste qui omet un
fichier sous `licenses/` de sa liste déclarée. Le test conserve les
octets de la licence sur disque et retire uniquement sa déclaration.
Le jeu assemblé avec le kit complet reprend toutes les déclarations.
Les quatre tests `game_builder` passent sur Windows. Le manifeste
épinglé du 058 et la relecture du jeu installé concordent sur les
421 chemins et leurs empreintes. La qualification des droits de
redistribution du jeu reste ouverte.
Reçu `R7-08-game-kit-license-inventory-20261116.json` à la racine de
la campagne, SHA-256
`cbb0952ac57d24689f21c7f62f709745ae0353d8c7756dc48117146c7a725612`.

<a id="gate-7-inventaires-licences-livrees"></a>

La relecture hors ligne des archives exactes du jeu **Le Grand Plateau**
exporté par le Studio Linux 008 recalcule chaque empreinte et retrouve
les 461 chemins déclarés par le kit joueur. L’installateur et le launcher
livré ont les mêmes octets. Le transport du module exporté déclare quatre
chemins, dont le texte CC0 de l’origine, et conserve le statut privé de
l’export. Le Studio 008 emballé déclare 462 chemins. Sur 405 chemins de
licence communs aux jeux Windows 058 et Linux 008, 401 sont identiques.
Les quatre différences portent sur les index générés pour chaque paquet.
Ce rapprochement couvre les deux distributions privées.
Reçu `R7-08-delivered-private-role-inventory-20261116.json` à la racine de
la campagne, SHA-256
`6fc38143468ad881b79dcbafd604ddf7ad5071d174b5f809516fd77589a2a987`.

La relecture des binaires exacts confirme la présence des 18 848 octets de
Fira Mono par défaut de Bevy dans le moteur Windows 058 livré au joueur
et dans sa copie du kit d'export du Studio, ainsi que dans le moteur du
jeu Linux 008. La notice OFL-1.1 est identique dans ces trois rôles et son
texte correspond à l'empreinte du blob amont déclarée dans l'index.
La recherche de ces mêmes octets dans les deux launchers ne donne aucune
correspondance. Reçu `R7-08-delivered-native-font-readback-20261116.json`,
SHA-256 `0f13db134ea0674b0b4629a0a46072a32d0397ee40a68df33b988d102f863d16`.

Le refus de redistribution du jeu complet intervient à trois étapes.
L'assemblage délivre un kit `private_local_only`, le Studio exporte les
modules intégrés avec `ModuleRights::PrivateLocalOnly` et le lecteur de
candidat requiert un paquet privé. Le contrôle `orpmod-inspect --publish`
du Grand Plateau s'applique au module source séparé. Le jeu exporté doit
porter une décision de redistribution de son kit exact et réutiliser
les contrôles de publication pour les modules réellement embarqués et
leurs visuels de présentation. Les trois garde-fous restent actifs.

L'admission d'un jeu autonome déclaré redistribuable contrôle désormais
le statut de chaque module livré et réutilise la validation de publication
du catalogue pour ses visuels. Le catalogue est relu à l'empreinte du
paquet. Les visuels et justificatifs doivent correspondre à l'inventaire
de présentation, avec leurs notices dans la liste des licences.
Le test de mise à jour refuse le module privé et conserve exactement
l'installation active. Le test de présentation refuse la même image
privée dans un paquet déclaré redistribuable et l'ouvre en usage privé.
Les cinq tests `game_builder` passent. Reçu
`game-distribution-pinned-catalogue-20261116.log.receipt.json`, SHA-256
`62754011a13404f7ee45b802acf4225cd2e845af5dd22676994674fb6e108ff5`.
Le Studio et son kit exportent toujours en portée privée.

Le service natif d'assemblage et d'installation accepte désormais un
kit `forge.delivery.game-kit.v2` redistribuable si le manifeste natif
épingle la revue `licenses/redistribution-review.json`. La revue lie
la plateforme, la révision source et l'empreinte de l'inventaire exact
du kit, hors fichier de revue. L'inventaire est encodé en UTF‑8, trié
par chemin, avec un chemin, une taille décimale et un SHA-256 séparés
par des tabulations sur chaque ligne terminée par LF. Le candidat
conserve la portée déclarée et contrôle ses modules en mode publication
avant assemblage et lors de la préparation de l'installation. Le format
privé v1 du kit et les candidats privés existants restent admis.

Le test installe un candidat redistribuable construit avec une fixture
originale et relit l'identité du jeu. Il refuse le kit sans revue, le
module privé et un changement de binaire malgré le nouvel inventaire
du manifeste. Les six tests `game_builder` passent. Reçu
`reviewed-game-kit-exact-inventory-rerun-20261116.log.receipt.json`,
SHA-256 `d8d9b6b3cc7f2643b8eb02973812a0e0588547688ff7dfea716bd677cd09586d`.
Le test sur fixture ne porte pas sur les droits d'un kit livré.

La recette Windows accepte désormais `--redistribution-review` avec son
SHA-256 explicite. Elle contrôle la revue sur le kit assemblé et l'inscrit
dans son inventaire de licences. La relecture, la préparation du manifeste
embarqué du Studio et la copie du kit adjacent conservent la portée et
revérifient les octets. Les recettes existantes produisent toujours un kit
privé par défaut. Les tests Python sur fixtures exécutent 71 cas dont un
ignoré. Reçu `R7-08-reviewed-game-kit-staging-tests-20261116.json`,
SHA-256 `333d7346c9f54bcc69980b5f33d73d85bfdedfbcc7c32df21da7be711bbb2fa1`.
Le Studio affiche le choix de redistribution lorsque le manifeste embarqué
déclare un kit v2 redistribuable. L'export reste privé par défaut, y compris
avec ce kit. Le service natif relit le kit, applique la portée explicitement
choisie et vérifie les droits des modules et des visuels sélectionnés.
Les deux tests natifs vérifient le refus avec un kit privé et les deux
portées avec un kit revu. Le frontend WASM et le binaire Studio passent
la vérification de compilation. Le contrôle statique UX donne zéro
avertissement. L'essai de l'interface avec un kit réel reste à faire.
Reçus `studio-game-export-scopes-20261116.log.receipt.json`,
`studio-game-export-scopes-web-20261116.log.receipt.json` et
`studio-game-export-scopes-desktop-20261116.log.receipt.json`, SHA-256
respectifs `f987e8da2a8760d51b3ada36c214a694a9be81cbf79d41be6961d41817d632cc`,
`0dd6939c6ecd42700ff24ce8b38b3008b7c6862bb457f1b4d1ab373af00ecb3b` et
`f97f90532074fc3e1be37ce34090be5a3f4dd23f231c02dbc7aaeffc7464273e`.

Le candidat Windows du Grand Plateau reprend le cache commun. Sous
`windows-grand-plateau-reviewed-20261116/` dans la campagne Gate 7, les
cinq graphes recalculés retrouvent 727 identités. Les 421 entrées de
licences, notices et sources préassemblées représentent 2 937 009 octets
et correspondent au kit 058. Le lot existant est réutilisé. Reçu
`R7-08-notice-reuse-before-kit-assembly.json`, SHA-256
`7fe298d89aed0160a539d08cb2a11b690f10ec5980656bf8cd7d9c8fba1a8ea7`.

Le moteur joueur Release du nouveau candidat a été construit sur
l'inventaire `5b2477b066fcb9bf6dd1461e3a04aab33cdc043ef6607cf967d64f7ae7523e4b`,
stable pendant le build. Le binaire relu a pour SHA-256
`1c5142231489c0b720ce21e0626b6f8f7646858787f379793d4230e292aca633`.
Le reçu `module-game-build-proof.json` a pour SHA-256
`697da032207e77915c693f7d01c4b8d3dec86e16c3b0e5be2b0a10622dd9c05c`.
Le CLI a terminé sur le même inventaire stable. Reçu
`delivery-cli-build-proof.json`, SHA-256
`1579b1a4efe93f5d2a67f8b6ef7a07bce56bfa845b53433ff011d4e9d611b44d`.
Le launcher a terminé sur le même inventaire. Ses trois ressources Web
embarquées concordent avec les entrées préparées. Reçu
`R7-08-launcher-embedded-web-assets.json`, SHA-256
`9028b0f53e485e9c8e4f0b7f6822f8868c456ec2bf6f8747cc186579957a2ee3`.
Le contrôle des PDB, du CRT externe, du chargeur statique WebView2, des
sources MPL et de Fira Mono porte sur les binaires exacts du kit. Reçu
`R7-08-kit-review-inputs.json`, SHA-256
`21c928bf2414cec3262f53d0803ff3b0fbb904278d9027f03e478a62bb286c1d`.

Le kit Windows à portée redistribuable a été assemblé et relu, avec sa
revue limitée aux deux binaires et à leurs notices et sources. Le kit
occupe 143 442 961 octets sur E: dans
`.build/tmp/gate7-windows-game-kit-grand-plateau-reviewed-20261116`.
Les 424 entrées et le manifeste v2 concordent. L'empreinte du manifeste
est `2949af28296d0f94dc2bddef772da561a1a70fd66112218d6ca0ae416c26d321`.
Le reçu `game-kit-assembly-receipt.json` relu sur F: a pour SHA-256
`b6b23ff7eef49a98f48a5fb732979a0e01f5ee4f48361f846c9155a80a00247a`.
La revue `redistribution-review.json` épingle l'inventaire des 423
fichiers avant ajout de la revue. Elle s'appuie sur
`R7-08-windows-game-kit-redistribution-review-evidence.json`, SHA-256
`1386997eb276a96f85e7febe1a3f85ac6ff8ec2fe42db0d7c48fc2dfd36a17a1`.
Le frontend Release et le Studio natif ont terminé sur le même
inventaire. Le Studio a embarqué le manifeste v2 et la relecture du
Setup 059 vérifie ses 426 membres Forge, dont la revue du kit et dix
archives sources. Le Setup de 78 509 287 octets a pour SHA-256
`eb5cbbdb5ed0083399ac5b493bd774d1494ea00fffb46424c26ae8c2aa49f716`.
L'empaquetage a nécessité un dossier E: plus court après une erreur de
longueur de chemin. Les binaires ont été réutilisés. Reçu
`setup/R7-08-studio-059-setup-readback.json`, SHA-256
`1fcc7118c0271c7ae8fbf56f7a1f6b5acc0522f60020d253f303332d1a5ce9e6`.

L'installation par CLI du Studio 059 sur l'hôte a vérifié la révision
active, le kit adjacent et sa revue dans un profil de qualification
isolé. Reçu `R7-09-studio-059-host-install.json`, SHA-256
`c35e7f7c212381ace00be84eb5ce77b2dd715fff0b32455dc6bae8dfd868ab20`.
Le Studio installé a pour SHA-256
`61c94a7ab4bd386ab4adc4e5cd89a267f1fcf345e5d8e6d02d3c2870d332a742`.
L'espace auteur du Grand Plateau réutilise la sauvegarde du contrôle
CC0 antérieur. La copie isolée a 15 fichiers et 8 525 455 octets.
Le CLI exact du 059 relit le module d'origine en mode publication.
Reçu `R7-08-grand-plateau-author-workspace-reuse.json`, SHA-256
`f05f5b1b395b88664898503bf895d7bbb9e755b5ca72db63d334574694d400e3`.
Le Studio 059 installé a sauvegardé la configuration avec la carte du
Grand Plateau comme départ et produit le jeu depuis « Créer les paquets
redistribuables ». L'inspection vérifie le manifeste du kit revu, les
425 fichiers Forge de l'application, l'installateur et le module exigé
par le jeu. Le module conserve les octets du terrain, de la zone et du
gameplay, les deux modèles, leur dépendance, les trois références visuelles
et les licences d'origine. L'export utilise la version 1.0.0 saisie par
défaut dans le Studio ; le module d'origine est en 1.0.1.
Reçu `R7-08-grand-plateau-059-studio-export-readback.json`, SHA-256
`ee34518c0c2cdc6b9053a13f83cf61f925daf2ea3672a0bfab4f81a1d813bf00`.

<a id="gate-7-licences-059"></a>

La relecture R7-08 des octets du 059 relie le Setup privé, le jeu Windows
du Grand Plateau et son module redistribuable. Les archives contiennent
respectivement 423, 422 et quatre chemins de licence et de provenance
déclarés. Le Studio et le paquet joueur conservent chacun les dix archives
sources de dépendances. Les octets de la revue du kit sont identiques dans
les deux rôles, et son empreinte couvre les 423 entrées examinées avant
l'ajout de la revue. `Installer.exe` correspond octet pour octet au
launcher revu. Le module conserve le texte CC0 d'origine et les trois
autres pièces déclarées. Le Setup conserve sa portée privée. Reçu
`R7-08-windows-059-exact-role-licences-20261116.json` sous
`windows-grand-plateau-reviewed-20261116/`, SHA-256
`acbbc614df291faed98a8d8d713b6af60c07f55787c9ab65e0f5fd74a65e61c3`.

La chaîne du 059 relie par SHA-256 le graphe Cargo, l'inventaire candidat
de 727 identités, l'assemblage et la revue du kit. L'inventaire candidat
dispose d'un texte pour chaque identité répertoriée. La revue joueur
couvre les 423 fichiers épinglés avant ajout de la revue. Le reçu du
jeu installé désigne exactement cette revue et son inventaire. Les
reçus du Studio et du jeu attestent que WebView2 Evergreen et Visual C++
sont des prérequis externes. Cette relecture porte sur les paquets
Windows 059 et leurs rôles déclarés.

Le contrôle de la table Web du binaire Studio 059 livré retrouve
exactement les 16 entrées attendues. Quinze correspondent octet pour
octet aux empreintes du build. Pour la page d'entrée sérialisée,
les six références locales et les trois empreintes d'intégrité des
ressources liées concordent. Les octets du HTML d'entrée avant
sérialisation ne sont plus disponibles dans le répertoire de build.
Le manifeste embarqué contient zéro import et zéro fichier de kit
local. La table a été lue dans le binaire issu du paquet exact du
Setup corrigé. Le paquet Studio garde le périmètre privé que fixe son
assembleur. Le kit joueur embarqué conserve sa revue propre au rôle jeu.
Reçu `R7-08-studio-059-embedded-web-assets.json`, SHA-256
`28224c5da3a582d55facc84565d99ebfeca9f93d86f8b61f588c3a6061c4435e`,
sous `windows-grand-plateau-reviewed-20261116/`. Le reçu et le
vérificateur occupent 16 940 octets sur F:. Aucun rebuild ni lancement
du Studio ou du jeu.

L’assembleur Windows accepte désormais une revue épinglée du paquet
Studio et du CLI qui l’accompagne. Ce chemin exige un Studio construit
sans assets locaux, un kit joueur revu et un audit sans identité Cargo
en réserve. Le Setup relit la revue embarquée et requiert WebView2 et
Visual C++ comme prérequis externes. La portée privée reste le défaut.
Les 20 tests ciblés d’assemblage et de recette Setup passent avec des
fixtures. Le 059 garde sa portée privée. Un paquet et un Setup issus
du prochain candidat réel restent à revoir et à qualifier.
Reçu `studio-windows-reviewed-assembly-tests-20261117.log` (125 octets),
SHA-256 `b429697ab8fb22348ac7e0c1ee2e5a0b0b5bb6e0392e93c9b825ea5434dc410b`.
Voir la [recette de revue](../scripts/delivery/README.md#cible-gate-7--studio-et-launcher-de-chaque-jeu).

Le candidat Windows 060 compile le CLI, le launcher et le runtime
commun sur le même inventaire de sources. Le contrôle des PDB et des
imports retrouve le CRT dynamique. La table embarquée du launcher
contient les trois mêmes ressources Web que le 059 à l’octet près.
Les 421 fichiers de notices et de sources gardent leurs empreintes.
Le kit joueur 060 de 424 fichiers a été assemblé avec la revue de ses
octets, puis relu par le lecteur de production. Le build du Studio
sans assets locaux utilise ce kit.
Sous `windows-reviewed-studio-20261117-060/`, reçu
`game-kit-assembly-receipt.json`, SHA-256
`020e975b95d0ff6cced9d466c0fa02f2bc85a812028c5b811fac9c13d442144f`.

<a id="gate-7-windows-061-kit"></a>

Le candidat Windows 061 reprend le moteur optimisé des inventaires de
rendu. Le moteur, le launcher et le CLI ont été contrôlés depuis le
même inventaire de sources stable, SHA-256
`cb82ba220e97abbfe3de9cf220c9bb67f693a3d4f2650897aa3c1d1f43ceda30`.
Le moteur du kit a le même SHA-256 que celui du [sprint Tidewater](#gate-7-tidewater-inventaires-partages).
Le cache Cargo reprend directement ce binaire. Le build du CLI retrouve
l’empreinte du 060. Le launcher recompilé conserve les trois fichiers web
embarqués attendus.

L’assemblage relit le graphe Cargo hors ligne, les imports PE, les objets
du chargeur et les 421 fichiers de notices et de sources du 060. Les
empreintes de ces 421 fichiers concordent. Le kit relu déclare sa portée
redistribuable avec les prérequis Microsoft externes. Sa révision couvre
ses 424 fichiers, dont les deux exécutables et le reçu de revue.
Le reçu d’assemblage est `windows-unified-20261118-061/game-kit-assembly-receipt.json`,
SHA-256 `c7aab79c0776f055badce776c31e347036c03045fce4297c0caba858b07f9c43`.
Le manifeste du kit a pour SHA-256
`f94e165cb13943697554ef98ed14eaeda20ac223fd595004c74701d1a8012f0e`.

<a id="gate-7-studio-061-export-grand-plateau"></a>

Le frontend et le Studio 061 ont été compilés depuis le même inventaire
de sources que le kit. Le Studio embarque le manifeste exact du kit. Le
Setup relu contient le paquet auteur et le CLI attendus, avec WebView2
et Visual C++ fournis séparément. `Forge-Studio-061.exe` occupe
78 929 927 octets et a pour SHA-256
`8797b90912aa23b752be818c7ba1de7a2e63113299ed1c1678f9e756257ad3cb`.
Sous `windows-unified-20261118-061/`, le reçu
`setup/R7-08-studio-061-setup-readback.json` a pour SHA-256
`4a79f40d8deae85ec1da90a96427d6d68bf83a089a7ff4bda8ee80f5d76d57ce`.
La recette `Assemble-Windows061.py` a pour SHA-256
`4d2d539499b4fa722610ef0c2275c459ae600df227596c11cc1a4761d3ec33ee`.

Le CLI du Setup 061 a réinstallé le Studio dans le dossier d’essai qui
avait servi à la désinstallation du 060. Le Studio a rouvert Le Grand
Plateau sous Evergreen `157.0.4341.60` signé et exporté le jeu avec les
conditions de redistribution déjà validées. Les 21 fichiers du précédent
workspace et de son export gardent leurs empreintes. La relecture
intégrale des 425 fichiers de `game.forgepack` confirme les 424 fichiers
du kit, dont le binaire mesuré sur Tidewater. Le module conserve les mêmes
octets que le 060 et passe `orpmod-inspect --publish`.

Les reçus `setup/R7-09-studio-061-host-reinstall.json` et
`setup/R7-061-grand-plateau-host-export.json` ont pour SHA-256 respectifs
`de2acfc29e7180863e311b56948f2e2179b3a8777e5a4e0dd7c474e8b391e26b` et
`c67524c7af1088c9a6f40cefa0c3a3b59a698d8d2d0812248b44314513b0bec8`.
Le parcours 061 couvre le Studio installé sur l’hôte et l’export réel.
Les preuves de machine propre restent rattachées au 060.

<a id="gate-7-windows-061-licences"></a>

R7-08 passe sur le périmètre revu du Studio Windows 061 et du Grand
Plateau exporté. Le contrôle hors ligne relit les octets des trois
paquets, vérifie les deux reçus de redistribution et retrouve les 424
chemins de licence du Studio, les 422 du jeu et les quatre du module.
Le CC0 du contenu original est présent dans le module. L’installateur
du jeu reprend le launcher revu à l’octet près. Le Setup contient le
paquet auteur et le CLI revus, avec Evergreen et Visual C++ externes.
Ce verdict couvre le candidat Windows local et le contenu déclaré
redistribuable. Tidewater reste privé. Le volet Linux reste reporté.

Reçu `windows-unified-20261118-061/R7-08-windows-061-exact-role-licences.json`,
SHA-256 `5e3f16f12ffd0bffe2a0419526a51fbfd2fc0b0adc8098ddfc22619cd340179d`.
Le reçu et son script de lecture occupent 14 723 octets sur F:.

<a id="gate-7-windows-062"></a>

Le kit Windows 062 regroupe 424 fichiers pour 144 539 665 octets. Le
runtime a le SHA-256 du [rejeu Tidewater validé](#gate-7-tidewater-navigation-empreinte-worker),
`2223109d24aa90d48af75ddfc07bacc06073413e54e802f7070631df599463e0`.
Le moteur, le launcher et le CLI ont été produits depuis l’inventaire
stable `91f27236e3ad9159b5552cf228b8ff6b74dca9d954a490792749882146b6a961`.
Les neuf fichiers modifiés depuis le 061 appartiennent au moteur et
au monde. La relecture retrouve les 421 entrées de notices et sources
du kit précédent et confirme les dépendances Microsoft externes. Le
kit est préparé pour redistribution dans le périmètre revu.

Reçu `windows-unified-20261118-062/game-kit-assembly-receipt.json`,
SHA-256 `35cca3e887510281f27233bffd5abc434eaf197cd1930f86ef86a6032500727f`.
Le manifeste du kit a pour SHA-256
`c4ea59a0a39a135eb7802b614044cac0a52b4cc5c77a2e50c1f28c6eaaaaf012`.

Le Setup 062 est dans `E:/OpenRPG_Forge/.build/qualification-data/gate7/w062/Forge-Studio-062.exe`.
Il contient le Studio, le CLI et le kit relus. Le mode classique a refusé
le dossier de qualification 061 créé par CLI, qui n’a pas son marqueur
de propriété. La commande de maintenance native `apply` a mis à jour
cette installation en conservant les 25 fichiers de l’espace de travail.
Depuis Evergreen, le Studio a produit l’export redistribuable du Grand
Plateau et le contrôle des 425 fichiers du jeu retrouve le runtime exact
du comparatif. Dans le module, seul `module_version` passe à `1.0.0-062`.

Le Setup classique 062 a aussi été installé sur l’hôte dans son dossier
standard libre, avec `/NOINTEGRATION` et un profil de qualification isolé.
Le Studio s’ouvre sur un atelier neuf. La désinstallation officielle retire
les binaires et conserve l’empreinte du fichier témoin. Le répertoire
`packages` vide et le fichier `installation.lock` vide restent sur place.
Les reçus sont sous `windows-unified-20261118-062/setup/classic-host-062/`.
`cleanup.json` a pour SHA-256
`5895c9a2e400ba082292dad3ba3911a038732700afe2c1e18aa30e83055ce694`.

Le jeu a été installé dans un dossier dédié. Son launcher autonome propose
« Reprendre » pour la sauvegarde 061. Le jeu affiche la scène et confirme
la reprise. La projection est prête à la sortie et le dernier état
sauvegardé conserve ses empreintes. La désinstallation native retire les
448 fichiers de l’installation et préserve la sauvegarde.

R7-08 passe pour les paquets exacts. La relecture compte 424 chemins de
licence dans le Studio, 422 dans le jeu et quatre dans le module. Le CC0
du contenu original et les notices tierces sont présents. WebView2 et le
runtime Visual C++ sont fournis séparément. Le parcours 062 est observé
sur l’hôte de développement. La preuve de machine propre couvre le 060
aux conditions documentées.

Sous `windows-unified-20261118-062/`, reçus SHA-256 vérifiés.

- `setup/R7-08-studio-062-setup-readback.json` `b98cc48773fa379a592d9c1dae6a153b2d4c4178b6b7a76bd6958f600f4cf6b5`
- `setup/R7-09-studio-062-host-update.json` `bb7c701992f3a389dc651690b00a3259d5cfa3b3431b08bcb8cfd55e579ddd9c`
- `setup/R7-062-grand-plateau-host-export.json` `efc1816761dc509accfdc0c71b71942bce7cece7134b670a1d0be7db6e5a47a8`
- `setup/player-host-062/result.json` `5ea41eb46beab089a5ca04c39ad761492839aed0a75a674169568d91a8364e55`
- `R7-08-windows-062-exact-role-licences.json` `e2614f50072f393a23d04f7dad132d8a3e559f7bba6a95c8e9f21e28e4914b9e`

<a id="gate-7-windows-063"></a>

Le kit Windows 063 assemble les 424 fichiers du runtime, du launcher et des
notices. Les 3 520 entrées compilées correspondent au 062 sauf les deux
fichiers du [tri de navigation](#gate-7-tidewater-navigation-tri-worker).
L’orchestrateur réutilise le runtime mesuré dans le cache Cargo. Son empreinte
est identique dans le reçu du comparatif, le reçu de build et le kit relu.
Les 421 fichiers d’information de licence et de sources livrées conservent
leur inventaire. La revue vérifie les imports PE et les sources liées du
runtime et du launcher ainsi que la présence de Fira Mono dans le runtime.

Reçu `windows-unified-20261118-063/game-kit-assembly-receipt.json`,
SHA-256 `0104e1095ea151016aa61e033cf30e8cd38e9673748ea5129bf046b821043952`.
Le manifeste du kit a pour SHA-256
`194594fe35bb3cdf67680f20e7f3c8f072f3b05bfdf28897fd3503c873b8ef80`.
Le Setup `E:/OpenRPG_Forge/.build/qualification-data/gate7/w063/Forge-Studio-063.exe`
a pour SHA-256 `eae1f3a4efc13310e26b4e9aea47ea12fef7d45f579f21c1eb090d08b803ee57`.
La maintenance native a appliqué le paquet exact du Setup à l’installation
isolée 062. Les 29 fichiers de l’espace de travail ont conservé leur
empreinte. Le Studio installé sous Evergreen a exporté Le Grand Plateau
`1.0.0-063`. Parmi les 425 fichiers de l’application, 424 correspondent
au kit exact. Le contenu du module conserve ses empreintes, avec la
version mise à jour dans `module.json`.
Le retour 063 → 062 → 063 par le CLI a vérifié les deux binaires actifs
et conservé les 33 fichiers du workspace après l’export. Le 063 est la
version active à la fin du cycle.

L’installation de qualification du jeu utilise le service natif de
l’installateur. Depuis `Jouer.exe`, le launcher propose « Reprendre » et
le jeu affiche la scène avec « Sauvegarde reprise ». Le rendu sur la
Radeon RX 7800 XT avec Vulkan et les rapports de sortie confirment le
bon état de la projection. La désinstallation retire 448 fichiers
et préserve l’empreinte de la sauvegarde 061. La relecture des licences
couvre le Studio, le jeu et le module, avec 424, 422 et quatre chemins
respectivement. Ces preuves concernent le 063 sur l’hôte de développement.

Reçus SHA-256 sous `windows-unified-20261118-063/`.

- `setup/R7-08-studio-063-setup-readback.json` `fbe23fd33c0330953ff1998111600bd4d7e7b886246623b24abe223798020e61`
- `setup/R7-09-studio-063-host-update.json` `3c058d6f895e10247ea0b1072d62fe7d6d1234e9ca9027716d426c112979c969`
- `setup/R7-09-studio-063-rollback-roundtrip.json` `9bdedcbc5e6455aac1fb91c99b7deadf6ad9648e94aa04fd97350a7b9d1d08dd`
- `setup/R7-063-grand-plateau-host-export.json` `bf1ede7941351bb869aba689dca9bf0ae81f4a031bfdec9af0879a41ff8fc57f`
- `setup/player-host-063/result.json` `e09909a8a1cd161ffe08b46ceca7918b55a0017845742e98b7c18be82d1d102b`
- `R7-08-windows-063-exact-role-licences.json` `118ea489535b8bc6ccf425649373b16a54930d9ce1c8eaa62a9811b071c426bb`

Pour R7-10, le propriétaire confirme qu’aucun autre PC Windows n’est
disponible. Le poste actuel contient le SDK Windows, Cargo, Rust et Node.
Le propriétaire reporte ce contrôle dans la [clôture locale de Gate 7](evidence/gate7/GATE7_CLOSURE_20261118.md).
Le Setup 063 et le module source du Grand Plateau peuvent être réutilisés
pour un seul parcours. L’origine intégrée au module exporté par le 063
confirme le paquet source suivant.
`E:/OpenRPG_Forge/.build/qualification-data/gate7/grand-plateau-cc0-20261114/grand-plateau-cc0.orpmod`,
SHA-256 `9ca7174c4b89055111dd85465fdb09be84b9e0b35e9a779200271f756539d071`.
Le Setup, le CLI, ce module et les deux prérequis externes ont été relus
par SHA-256. Les signatures des installateurs Visual C++ x64 et WebView2
Evergreen x64 présents sur E: sont valides.
Installer le Studio avec les prérequis déclarés,
exporter le jeu, retirer le Studio en conservant cet export, puis installer
le jeu et vérifier son lancement autonome et la reprise. Les preuves
sur Windows propre du 060 restent rattachées à leurs binaires.

<a id="gate-7-windows-062-quatre-profils"></a>

Une session du runtime exact du kit 062 applique Low, Medium, High et
Ultra dans le module privé du comparatif Tidewater. Chaque demande est
observée comme appliquée et persistée. Les captures natives à 1080p
montrent la côte, la végétation, l’eau et le ciel depuis la même position.
Le fichier de préférences enregistre Ultra. Le rejeu de persistance
après relance provient du 015.

| Profil | Intervalles moteur | Moyenne | P95 | Au-delà de 33,33 ms |
| --- | ---: | ---: | ---: | ---: |
| Low | 215 | 14,02 ms | 15,19 ms | 0 |
| Medium | 207 | 14,63 ms | 15,69 ms | 0 |
| High | 197 | 15,14 ms | 16,39 ms | 0 |
| Ultra | 200 | 15,02 ms | 16,26 ms | 0 |

Les séries retiennent les images de simulation et de projection prêtes
après préparation, au point `(20,38 m, −90,48 m)`. Elles mesurent les
intervalles du moteur sur cette pose fixe. Le comparatif de cadence
du compositeur en déplacement reste celui du [trajet 1080p](#gate-7-tidewater-navigation-empreinte-worker).

Reçu `windows-unified-20261118-062/tidewater-four-profiles/result.json`,
SHA-256 `d239f7693ebadb1a250a6f3383249cd674eb19e3aa484587cf8200650e465a0a`.
L’archive des seize pièces vérifiées occupe 11 795 207 octets sur F:.

<a id="gate-7-joueur-061-grand-plateau"></a>

L’export exact du Grand Plateau 061 a été installé sur l’hôte dans un
profil joueur distinct du Studio et sans sauvegarde initiale. Le pilote
utilise les mêmes services natifs que `Installer.exe`. `Jouer.exe` ouvre
le launcher installé. Deux processus jeu distincts exécutent « Jouer »,
puis « Reprendre » après une sauvegarde F5. Tous deux rendent la scène
sur la Radeon RX 7800 XT via Vulkan et se terminent avec la projection
prête. L’état sérialisé et le fichier de sauvegarde ont les mêmes
empreintes avant et après la reprise.

Le CLI a retiré l’installation isolée, y compris `Jouer.exe`, et la
sauvegarde a conservé son SHA-256 `5da16b87de701c8bd5e1395eaef7fe66ba28eb8b1027753e5d293df891f2676e`.
La désinstallation a retiré 448 fichiers et 169 298 484 octets sur E:.
Le profil de qualification conserve la sauvegarde témoin et les journaux
du parcours joueur. Ce résultat couvre le 061 sur le PC de développement.

Reçu `windows-unified-20261118-061/setup/player-host-061/result.json`,
SHA-256 `02ecd7f279a09225a42e5bdea8d3d07481efdfa56c7135cf7a477c5b6783adfe`.
Les sept pièces relues occupent 41 038 octets sur F:.

<a id="gate-7-setup-studio-060"></a>

Le Studio natif 060 et son frontend Release ont terminé sur le même
inventaire que le kit joueur. Quinze des 16 ressources de la table
embarquée concordent octet pour octet avec le frontend, y compris le
manifeste v2 du kit. La page d’entrée sérialisée par Tauri conserve
les références et empreintes d’intégrité. Le manifeste confirme zéro import
et zéro fichier de kit local. Le paquet Studio et le CLI correspondent
à la revue exacte. Le Setup de 78 905 232 octets contient ces mêmes
octets, l’index, le désinstallateur et les quatre composants NSIS connus.
Le contrôle de liaison retrouve le CRT dynamique et les objets du
chargeur SDK WebView2. WebView2 et Visual C++ restent des prérequis externes.

Reçu `windows-reviewed-studio-20261117-060/setup/R7-08-studio-060-setup-readback.json`,
SHA-256 `ceb9a58c4ef0bb7ace09b3695d43e11d1b7a7b038d40414e955157b30c207333`.
Le Setup `w060/Forge-Studio-060.exe` a pour SHA-256
`0617c8870e9a1071d7dd01478b35d7236c63f7d1bc44c5349066d6099c421e8a`.
Revue locale `R7-08-studio-060-redistribution-review-evidence.json`,
SHA-256 `cb79270f2e3c44f866670dd1d96e1625df6f5948b84f8c7e2dd215eece9cfbaa`.

Le pilote `setup/Test-Studio060Evergreen.ps1` épingle le Setup, son CLI
et le manifeste attendu après installation. Sur l’hôte, il détecte le
runtime Evergreen signé 157.0.4341.46 et le SDK. Le Setup n’a pas été
lancé. Hyper-V refuse toujours la lecture des invités à la session
courante. Le prochain parcours exige un Windows propre avec Evergreen
résolu par le chargeur. Reçu `setup/R7-10-evergreen-host-admission.json`,
SHA-256 `5355968cac4c71032428178e544a3bdae1d685d08755edab44f31c071f360cd0`.

Un Sandbox Windows 26100 neuf, hors réseau, sans SDK ni Studio,
retrouve l’inscription Evergreen `157.0.4341.42` et les fichiers
`157.0.4341.46`. L’installateur signé du Visual C++ externe ne termine
pas dans les quatre minutes allouées. L’invité n’atteint ni le contrôle
du chargeur 060 ni l’exécution du Setup. Il est arrêté après écriture
du reçu. Ce résultat confirme l’écart registre/disque de l’invité et
laisse ouvert le parcours 060 avec Evergreen sur Windows propre.
Sous `setup/sandbox-evergreen-060/`, reçu `result.json`,
SHA-256 `77ebc56b383c25a9d764a5b4a2e79936ed42557f731e6d071df78bf37a53d679`.
Le reçu et les pilotes occupent 7 046 octets sur F:. Les 12 667 octets
temporaires ont été retirés sur E:.

Pour le prochain passage, le reçu d’assemblage exact 060 fixe le minimum
Visual C++ à `14.44.35207.0`. Le Setup lit les versions de
`vcruntime140.dll` et `vcruntime140_1.dll` dans le répertoire système x64
avant d’extraire son CLI. L’ancien pilote Sandbox décide d’installer le
redistribuable à partir d’une clé de registre. Son délai expiré ne fournit
aucune lecture des deux DLL dans l’invité. Sur le poste de développement,
la lecture des deux fichiers signés donne `14.51.36247.0` et le reçu
d’admission confirme Evergreen `157.0.4341.46`. Le SDK présent exclut ce
poste du verdict machine propre.

<a id="gate-7-sandbox-dll-admission-060"></a>

Le contrôle direct sur un nouveau Sandbox Windows 26100 sans SDK ni Studio
relit les empreintes du Setup et du CLI 060. Les deux DLL x64
`vcruntime140.dll` et `vcruntime140_1.dll` sont absentes. La clé Visual C++
est aussi absente. Evergreen est inscrit en version `157.0.4341.42` pour
la machine, sans inscription utilisateur, avec le seul dossier exécutable
`157.0.4341.46`. Le pilote s’arrête au prérequis CRT avant de lancer le
chargeur Evergreen ou le Setup. Aucun prérequis n’est installé pendant
ce passage. La liste Sandbox confirme ensuite la fermeture de l’invité.

Sous `setup/sandbox-dll-admission-060/`, reçu `result.json`, SHA-256
`5959c5336000a54b03c788d2f5cd72dc8e63a4351edf439c2fcf10a0d47e0a1b`.
Le reçu et ses deux pilotes vérifiés occupent 8 182 octets sur F:.
Les 13 847 octets temporaires ont été retirés sur E:.

Le pilote `setup/Invoke-Studio060CleanWindows.ps1` réunit les étapes pour
un Windows propre distinct. Il vérifie le Setup, le CLI, les deux
composants Microsoft signés et `Test-Studio060Evergreen.ps1`. Sans option,
il observe les prérequis. Avec `-Provision`, il lance les installateurs
Microsoft uniquement si nécessaires, puis vérifie les DLL et la détection
Evergreen. Avec `-Install`, il attend l’admission positive avant d’exécuter
le Setup et de vérifier le paquet actif. Il refuse le poste de développement
avant toute installation. Aucun redémarrage n’est déclenché par le pilote.
[Distribution Microsoft WebView2 Evergreen](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

Pour exécuter ce passage ailleurs, mettre les deux scripts côte à côte et
indiquer les quatre binaires via `-Setup`, `-DeliveryCli`, `-CrtInstaller`
et `-EvergreenInstaller`. Les binaires existent sur E: dans `w060`,
`external-crt-cli-20261114` et `studio-050-webview-bootstrap-replay/evergreen-current`
sous `.build/qualification-data/gate7`. Le contrôle sur l’hôte reçoit
`ENVIRONMENT_NOT_CLEAN` après validation des cinq empreintes et des deux
signatures. Le parcours Studio → export → reprise reste à exécuter dans
l’autre invité. Les essais Fixed Version et de maintenance gardent leur
portée datée.

SHA-256 du pilote `e09486d7fb20ad9045a27429610e30142f04be8f87a76fc7c9eb10436e6b7430`.
Reçu `setup/R7-10-studio060-clean-windows-pilot-host.json`, SHA-256
`575d0c9ae2bc98cd58e7a97e5e3b79d0c399e83b413936307bd7a74cd92c4a91`.
Les deux fichiers vérifiés occupent 6 594 octets sur F:. Les binaires
et prérequis existants sont réutilisés sans duplication.

Le redémarrage d’un seul Windows Sandbox 26100 hors réseau confirme que
le décalage Evergreen persiste après un nouveau démarrage de l’invité.
L’identifiant Sandbox reste le même. Le nom de l’invité change, et les
événements Windows relient la demande de redémarrage au nouveau démarrage
du noyau. Avant et après, l’inscription machine indique `157.0.4341.42`
et le répertoire avec exécutable indique `157.0.4341.46`. Aucun des deux
installateurs n’a été lancé. L’hôte a conservé son heure de démarrage et
le Sandbox de diagnostic est arrêté.
[Redémarrage dans Windows Sandbox](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-overview).

Sous `setup/sandbox-evergreen-guest-reboot-060/`, reçu `result.json`,
SHA-256 `44a0730e2d5c729b003192086575b9402a1c14c56fffc83b1f6af86d7e65d287`.
Les six pièces vérifiées occupent 9 111 octets sur F:. Les 8 022 octets
temporaires ont été retirés d’E:. Le pilote du 060 peut être réutilisé sur
un Windows propre disposant d’un Evergreen fonctionnel.

Le nouvel invité Hyper-V `Forge-Gate7-Windows-LTSC` est créé et a
démarré sur le disque installé depuis le média Microsoft épinglé. Le
TPM virtuel et Secure Boot sont activés. PowerShell Direct a confirmé
Windows 11 Entreprise LTSC d’évaluation (26100), sans SDK ni outils de
développement détectés. Le déclencheur du premier logon n’a produit
aucun reçu du pilote. Le lancement dans la session interactive de cet
invité reste à qualifier dans la reprise Evergreen ci-dessous.
Le parcours Evergreen du Setup 060 reste ouvert. Le PC hôte conserve son
heure de démarrage.

Sous `setup/hyperv-ltsc-060/`, reçu `result.json`, SHA-256
`e8628a301c16ba41039e21f5bad41457d208a893fea2c0aaeb6bcaa8a2e028ee`.
Les six pièces vérifiées occupent 3 672 octets sur F:. Le disque invité
de 17 687 379 968 octets et le média de qualification de 326 121 472
octets sont conservés sur E: pour la reprise du même invité.

<a id="gate-7-studio-060-evergreen-ltsc"></a>

PowerShell Direct a lancé le pilote épinglé sur l’invité LTSC, dans le
profil local de qualification. L’invité ne contenait ni Studio ni SDK ni
outils de développement. L’installateur Visual C++ x64 a retourné 0.
Le chargeur WebView2 a résolu Evergreen `157.0.4341.60` depuis son
installation Microsoft enregistrée et signée. Le contrôle des redirections
WebView2 a été négatif. Le Setup 060 silencieux et la vérification du paquet
actif ont retourné 0. La révision installée est
`9713fd74f1d7d3499237ee4472780311a3a509605d1ba1169166b6482c9e331c`.

L’invité a ensuite terminé la configuration Windows. La console Hyper-V
ouverte avec élévation a montré le bureau Forge Qualification puis
l’écran de verrouillage du compte. Le premier contrôle de lancement
dans la session interactive a expiré sans reçu. Le 18 novembre, le bureau
LTSC déverrouillé est visible dans la console. Le contrôle graphique
n’atteint pas les commandes de la console élevée. Windows annule
l’élévation demandée pour le transfert du module et le lancement dans
la session invitée. PowerShell Direct refuse également la session hôte
standard pour manque d’autorisation. La relance élevée autorisée a
copié le Grand Plateau dans `C:\ForgeQualification\Evergreen060\LeGrandPlateau.orpmod`
et vérifié le SHA-256 `7b57af5a201e1a8bb816f4b3938df2b4280c822b67fae77b0c3d50d4adbdaab0`.
Le contrôle `Win32_ComputerSystem.UserName` a arrêté le lanceur avant
l’ouverture du Studio. Le pilote vérifie désormais le compte authentifié
dans l’invité et conserve la tâche limitée à une session interactive de ce
compte. Une tâche dédiée a évité le conflit avec la tâche précédente.
Le Studio est visible et affiche l’atelier sans carte ouverte. La tâche
n’a pas livré le reçu du processus WebView2 dans le délai imparti. Le
contrôle graphique n’ouvre pas le volet Projet et jeu dans la console
avec élévation. La console standard a reçu un refus d’accès à la VM.
Aucun nouveau Setup ni export n’a été exécuté.

Sous `setup/hyperv-ltsc-060/`, reçu `studio-060-evergreen-install.json`,
SHA-256 `e7ff4904ac776081ee52f46436a0d0d3517bbddd19d16992c0e717b6c059fb53`.
Le reçu relu occupe 9 967 octets sur F:.

<a id="gate-7-studio-060-evergreen-hote"></a>

Le 18 novembre, PowerShell Direct a identifié le processus WebView2
Evergreen Microsoft signé `157.0.4341.60`, enfant du Studio exact 060
dans l’invité LTSC sans SDK. La console standard a refusé l’accès malgré
la délégation limitée à cet invité. L’accès temporaire a été retiré et
son retrait vérifié. Les deux VM `Forge-Gate7-Windows` et
`Forge-Gate7-Windows-LTSC` ont été retirées de Hyper-V sur demande du
propriétaire. Leurs deux disques ont été retirés d’E:, soit
42 823 843 840 octets de fichiers VHDX. Les preuves sont conservées sur F:.

Sur le PC hôte, le paquet Studio 060 exact et les 15 fichiers auteur du
Grand Plateau ont été vérifiés dans un profil isolé. Le Studio a chargé
Evergreen Microsoft signé `157.0.4341.46`. La création des paquets
redistribuables depuis son interface a réussi. Les quatre fichiers ont
les mêmes SHA-256 que ceux produits sur Windows propre avec Fixed
Version. `orpmod-inspect --publish` accepte le module. Le parcours
auteur et joueur complet sans SDK reste couvert par le rejeu Fixed
Version. La preuve Evergreen associe le démarrage sur LTSC et l’export
sur l’hôte.

Reçu `setup/R7-10-studio-060-evergreen-host-export-20261118.json`,
SHA-256 `b734cc2516c4e9d7f39bc7a2cc09343d371c19be7f8c9f15d1ee13009a38166b`.
Le reçu relu occupe 3 056 octets sur F:.
L’instance Studio de qualification a été fermée. Le contrôle automatique
a refusé le nettoyage de sa copie isolée de 373 508 528 octets sur E:.
Le retrait ultérieur de ses fichiers d’installation par le CLI du
Studio est vérifié dans le [reçu de désinstallation](#gate-7-studio-060-desinstallation).

<a id="gate-7-studio-060-fixed-windows-propre"></a>

Le Setup exact 060 retourne 0 dans un nouveau Sandbox Windows 11 26100
hors réseau, sans Studio ni SDK avant installation. Le journal Microsoft
confirme l’installation Visual C++ x64 sans redémarrage. Les deux DLL
livrées par Microsoft sont en `14.52.37029.0`. Le CAB Fixed Version et son
exécutable correspondent aux empreintes de la version `157.0.4341.60`.
Le chargeur du CLI confirme cette version avec une stratégie limitée à
`forge-delivery.exe` et `forge-studio.exe` dans l’invité. L’inscription
Evergreen reste en `157.0.4341.42`.

Le CLI installé conserve son empreinte et vérifie la révision active
`9713fd74f1d7d3499237ee4472780311a3a509605d1ba1169166b6482c9e331c`.
Son lancement normal ouvre « OpenRPG Forge Studio » avec le processus
WebView2 Fixed Version attendu. Le premier pilote a été arrêté après
le contrôle WebView2 pendant l’écriture de son rapport. La reprise a
conservé le même invité et ses prérequis. Elle a exécuté le Setup une
seule fois. L’export, le parcours joueur associé au 060 et Evergreen
standard restent à valider.

Sous `setup/managed-fixed-060/`, reçu `result.json`, SHA-256
`d6c59fe5bf939e4b7109125101ed8a0e31c283ac3b5ca7a22bc64333bd0f1771`.
Les six pièces vérifiées occupent 20 174 octets sur F:. L’invité a été
fermé et les 353 449 612 octets temporaires ont été retirés sur E:.

La comparaison lit toutes les entrées des deux paquets Studio exacts.
Les 421 fichiers communs sont les notices et les sources du kit joueur,
identiques à l’octet près. Le Studio, le CLI, le launcher et le runtime
ont des empreintes différentes. Les autres écarts du paquet portent sur
la provenance et les reçus de redistribution. L’orchestration d’export,
la construction du jeu et l’installation conservent leurs sources directes,
mais `module_builder.rs` et d’autres dépendances ont changé. Les preuves
059 s’appliquent aux fichiers de notices communs et à son propre rejeu.

Les 15 fichiers auteur du Grand Plateau correspondent aux empreintes du
rejeu propre 059, soit 8 526 742 octets. Il s’agit de l’espace enregistré
pour l’export, après la configuration faite dans Studio. Les trois fichiers
de l’ancien export ont aussi été relus. Le prochain passage peut reprendre
ce même espace auteur, avec les anciens exports exclus de la copie de
qualification. Il reste à produire le nouvel export avec le Studio 060
et à installer, jouer, sauvegarder puis reprendre avec les binaires 060.

Dans le même dossier, reçu `R7-060-export-player-proof-delta.json`,
SHA-256 `bb92476b1527da6d380cbef1496d7c73f72487af5417da781620140d8a72c2cc`.
Le reçu ajoute 4 796 octets sur F:. Aucun paquet ni espace auteur n’est
dupliqué et aucun essai n’est relancé par ce rapprochement.

<a id="gate-7-studio-060-export-grand-plateau"></a>

Un Windows Sandbox 26100 neuf, sans SDK ni Studio, a installé le Setup
060 exact avec Visual C++ et WebView2 Fixed Version `157.0.4341.60` géré.
Le pilote a repris dans le même invité après le premier retour WebView2
inexploitable. L’installation a ensuite été exécutée une seule fois. Les
15 fichiers auteur ont été copiés et vérifiés dans le workspace de
l’invité. Les anciens exports sont restés hors de cette copie.

Dans le Studio 060 vérifié, le choix du projet « Le Grand Plateau » et
les conditions CC0 approuvées ont produit un export redistribuable. Le CLI
du Studio accepte le module avec `orpmod-inspect --publish`. Le module
est identique à l’octet près à celui du 059. L’installateur correspond au
launcher revu du kit 060 et le manifeste d’export épingle ce kit. Le
paquet jeu a pour SHA-256
`bc499a3ba7dc1ff2e17f8a509adf7030578b81cb7eebef1e5015d0c30f63c6f8`.
L’installation et la reprise par le joueur restent à exécuter dans un
invité séparé. Evergreen standard reste ouvert.

Sous `setup/managed-fixed-060/author-grand-plateau-060/`, reçu
`export-result.json`, SHA-256
`21b908ebcc3a09c24c823a621cc0218c2f19510123c483863ccfce3b1556afd9`.
Les dix pièces relues occupent 54 639 octets sur F:. L’invité auteur est
fermé. Les 58 186 octets temporaires ont été retirés sur E:. Les quatre
fichiers exacts de l’export (169 260 078 octets) et le CAB signé (310 954 844
octets) restent sur E: sous `.build/tmp/w060-grand-plateau-author/` pour
l’essai joueur, sans seconde copie.

<a id="gate-7-joueur-060-grand-plateau"></a>

Dans un second Sandbox Windows 26100 hors réseau, sans SDK ni Studio,
l’installateur de l’export 060 vérifie et installe Le Grand Plateau au
dossier proposé. La relecture vérifie l’application, le module, le
launcher, `Jouer.exe` et le runtime exact du kit. L’invité utilise Visual
C++ externe et WebView2 Fixed Version `157.0.4341.60` géré.

« Jouer », F5 puis « Reprendre » exécutent deux processus natifs distincts
sur la Radeon RX 7800 XT en Vulkan. Les deux rapports indiquent une
projection prête et une sortie réussie. La reprise conserve l’empreinte
de la sauvegarde et l’état de jeu. La fermeture du second processus a été
demandée par `taskkill` sans `/F` après le raccourci resté sans effet.
Le désinstallateur graphique retire l’installation, `Jouer.exe` et son
programme temporaire, avec la même empreinte de sauvegarde sur disque.
Ce passage qualifie le parcours auteur et joueur du candidat 060 dans
ces conditions. Evergreen standard reste à vérifier. Le cycle de
maintenance natif est documenté [ci-dessous](#gate-7-jeu-060-maintenance).

Sous `setup/managed-fixed-060/player-grand-plateau-060/`, reçu de la chaîne
`assessment.json`, SHA-256
`b8fbc526bcdfe2b0a07f0bdf809d1d25c5a425459a7c69f6a9b15fad2052e096`.
Les 17 pièces relues occupent 69 339 octets sur F:. L’invité est fermé.
Les 311 021 309 octets temporaires sur E: ont été retirés, dont le CAB.
L’export exact de 169 260 078 octets reste sur E: pour le contrôle Windows
standard, sans duplication.

<a id="gate-7-windows-060-licences-chaine"></a>

R7-08, périmètre Windows 060 et Grand Plateau. Les 422 fichiers de
licences, notices, sources et revue du paquet jeu correspondent à ceux
du kit embarqué dans le Studio 060. Le launcher et le runtime concordent
avec le kit revu. Le module vérifié conserve quatre chemins de licence,
dont le CC0 du terrain et des documents de gameplay originaux. Les
deux modèles correspondent à leurs empreintes déclarées. Les reçus
auteur et joueur portent sur ce même paquet. Ce résultat couvre le
périmètre exact Windows 060 avec Le Grand Plateau. Les droits du
contenu Tidewater privé gardent leur suivi propre.

Sous `setup/managed-fixed-060/`, reçu
`R7-08-windows060-exact-export-rights-readback.json`, SHA-256
`730b4fa330581b9dc518f89551a6255ddf9b0deb70a094a2cae7cf0729d4964b`.
Le rapprochement ajoute 1 701 octets sur F: et réutilise les binaires
et reçus existants.

<a id="gate-7-studio-060-maintenance"></a>

L’installation isolée sur l’hôte a utilisé les archives Studio exactes
059 et 060 et leurs CLI. Le CLI installe 059, applique le plan de mise à
jour 060 puis applique le plan de retour au 059. À chaque étape, `verify`
confirme la révision active, l’empreinte de l’exécutable installé et le
dossier utilisateur de qualification. Les 15 fichiers auteur du Grand
Plateau dans ce dossier conservent leurs empreintes aux trois étapes.
Le passage couvre les transactions Studio par CLI sur l’hôte. La preuve
Windows propre 060 couvre séparément l’installation et l’export.

Sous `setup/studio-maintenance-059-060/`, reçu `result.json`, SHA-256
`6b13c7d36750d2053b8e6148df1e4164cf937bd217de8191de5095ad9b057483`.
Les cinq pièces relues occupent 11 698 octets sur F:. Les 398 782 704
octets de l’installation, de la copie auteur et des fichiers temporaires
ont été retirés sur E:.

<a id="gate-7-studio-060-desinstallation"></a>

Le CLI exact 060 a désinstallé le Studio du profil isolé qui avait
effectué l’export Evergreen du Grand Plateau. La révision et l’exécutable
étaient vérifiés avant l’opération. Le CLI a retiré 428 fichiers, dont
l’état actif et l’exécutable. Les 21 fichiers du workspace de
qualification, dont les quatre fichiers de l’export, conservent leurs
SHA-256. Le retrait libère 195 720 765 octets sur E:. Seul le verrou
vide du service reste dans le dossier d’installation. Ce parcours
couvre la commande native du Studio sur l’hôte. Les étapes de
l’interface NSIS et du registre Windows relèvent des essais dédiés.

Reçu `setup/R7-09-studio-060-host-uninstall.json`, 6 386 octets sur F:,
SHA-256 `f4eff2ae5fd87df835ebd869834f2255cf159d23ce89fd34cfd43c47fa47c10f`.

<a id="gate-7-jeu-060-maintenance"></a>

Le cycle du Grand Plateau réutilise les deux exports exacts 059 et 060,
une copie de la sauvegarde native 059 et un profil isolé sur l’hôte.
Les services utilisés par l’installateur et le lanceur appliquent la mise
à jour vers 060 puis la restauration du 059. Les révisions actives et les
empreintes du moteur et du launcher correspondent à chaque version. Le
module reste identique et le lanceur reconnaît la même sauvegarde comme
reprenable aux trois étapes, avec l’empreinte
`6e0c9e15cb0887d10fa752ad54d07a3cfa2487ede7e9a64d3fc4ec290a56c73d`.
Le fichier d’entrée durable `Jouer.exe` reste à son empreinte 059, comme
prévu lorsque cette entrée vérifiée est déjà présente. Le passage couvre
les transactions natives et l’admission de la reprise. Les deux parties
réelles du parcours joueur 060 restent rattachées à leur reçu séparé.

Sous `setup/game-maintenance-059-060/`, reçu `result.json`, SHA-256
`c3c949fa2bd45ffb56a85f472d1625f7981f45cc57c06c42fa473a8a2f8004d8`.
Les trois pièces relues occupent 8 884 octets sur F:. Les 327 441 598
octets de l’installation isolée et de ses temporaires ont été retirés.

Le contrôle du Setup 059 relie son empreinte au compilateur NSIS 3.12,
au fichier `COPYING`, au journal de compression zlib et aux octets des
trois plugins et du bitmap. Il épingle le désinstallateur propre au 059
et retrouve la référence aux termes NSIS dans les notices du Studio.
[Licence NSIS](https://nsis.sourceforge.io/License).

Le binaire Studio livré correspond à son PDB par identifiant CodeView.
Le lien retrouve les six objets du chargeur statique WebView2 et le
paquet fournit son texte BSD 3-Clause. Les dix composants liés et
recensés dans l'inventaire MPL‑2.0 ont chacun une archive source lisible,
le texte de licence correspondant et un chemin indiqué dans la notice
livrée. Le contrôle des imports et des archives liées confirme le CRT
dynamique. [MPL 2.0, section 3.2](https://www.mozilla.org/en-US/MPL/2.0/#distribution-of-executable-form).

Reçus `R7-08-studio-059-setup-notices.json` et
`R7-08-windows-external-crt-studio.json`, SHA-256 respectifs
`155c50749fe119afa5733bbeb4fc25945f547d5a79c4a20ec66be8443887e895` et
`0e5b8d758af242bf8bbdb382402baeca7e4f60469deaf3b2ab71e835a62f8a74`.

L'installation du jeu par CLI sur l'hôte vérifie les deux paquets.
Le chemin de 261 caractères vers l'exécutable a provoqué l'erreur
Windows 3 au lancement. Le préfixe de chemin étendu a permis de démarrer
un processus qui s'est terminé sans fenêtre visible. La même révision
et les mêmes octets, installés sous `w059/player/game`, ont ouvert le
lanceur avec un profil joueur dédié. Reçu d'admission
`R7-09-grand-plateau-059-host-launch-admission.json`, SHA-256
`f493fda255974237a63bd38608b9f20d3c290244b0ff78124a5a3307802e9427`.
La désinstallation de l'essai long a retiré 446 fichiers, pour
147 851 013 octets mesurés avant le retrait. Reçu
`R7-09-grand-plateau-059-long-path-uninstall.json`, SHA-256
`09e2d11a17df864b84aec9e4948514e37b1b3fffd3d37bd6e99c9293aa23d44d`.

Depuis le lanceur graphique, « Jouer » ouvre la partie, F5 crée la
sauvegarde puis « Reprendre » relit les mêmes octets et le même état
dans un second processus. Les deux rapports attestent une projection
prête sur RX 7800 XT en Vulkan et une sortie normale. Reçu
`R7-06-grand-plateau-059-launch-save-resume.json`, SHA-256
`df6c732af5f654167211b0452bd789bfb30161a1088016998b3c45fddf7776a5`.
Le verdict de qualification de distribution reste ouvert sur le périmètre
complet de Gate 7.
`setup/clean-windows-059-inputs.json` épingle le Setup, le CLI, les
trois fichiers du jeu exporté et les deux prérequis x64 signés Microsoft,
revérifiés sur l'hôte avant les deux essais invités suivants.
SHA-256 `05bc5c3119dd1853c03baf4fc5b8040aa4a887ab56f8e0de6579e101c7a1f0ba`.

<a id="gate-7-studio-059-windows-propre"></a>

Le Setup NSIS 059 exact retourne 0 dans un Sandbox Windows 11
26100 sans SDK, sans Forge et hors réseau. Visual C++ x64 est
installé sans redémarrage, confirmé par le journal Microsoft après
perte du code de sortie dans le premier script. La version Fixed
157.0.4341.60 signée est fournie par la stratégie Microsoft
`BrowserExecutableFolder`, limitée aux exécutables CLI et Studio dans
l'invité. La valeur Evergreen obsolète de l'invité est alignée de
`157.0.4341.42` sur l'exécutable Microsoft présent `157.0.4341.46`
pour le contrôle de fichier NSIS. Le contrôle Evergreen standard
reste en échec sans la stratégie Fixed Version.
[Stratégies WebView2 Microsoft](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-webview-policies).

Le diagnostic suivant isole l'alignement du registre. Dans un invité
Windows 11 26100 sans SDK ni Forge, le CRT signé a terminé sans
redémarrage. Sans runtime Fixed ni stratégie de navigateur, le CLI exact
du 059 retourne le diagnostic WebView2 `0x80070002` avant puis après
alignement de `157.0.4341.42` sur l'exécutable Microsoft signé
`157.0.4341.46`. La capture ne contient pas le code de sortie du processus.
Le Setup n'a pas été exécuté dans cet invité. Le premier essai a expiré
pendant le CRT. La reprise du second invité avec le CLI Windows Sandbox
a vérifié le résultat du CRT avant le contrôle Evergreen. Les deux
invités sont fermés.
[Détection Evergreen](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution#detect-if-a-webview2-runtime-is-already-installed)
et [CLI Windows Sandbox](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-cli).
Reçu `setup/evergreen-registration-only/out/evergreen-registration-resumed.json`,
SHA-256 `1d68c956ff3122a0585a0201a57f1ab341e53e9bbb6d9d5ae1b898122d831a2f`.
Le diagnostic du CRT conserve une projection de 2 246 octets. Le
journal intermédiaire de 404 137 octets a été retiré après vérification.

Le rejeu Evergreen par compte standard est documenté sous
`setup/evergreen-standard-user-20261116/`. Dans un Sandbox 26100
hors réseau et sans SDK, le journal Microsoft confirme l'installation
Visual C++ avec résultat `0x0`. Le compte local standard a chargé
son propre profil et exécuté l'installateur Evergreen x64 signé,
sans stratégie Fixed Version.

Son journal Edge Update retourne `0x80040828` et indique une
installation machine déjà présente. La valeur machine reste
`157.0.4341.42`. Le compte standard ne reçoit aucune version
Evergreen dans HKCU. Le CLI exact du 059 retourne `0x80070002`
avant et après l'essai. Le journal donne le code Edge Update et le
script n'a pas capturé le code de sortie natif de l'installateur.
Le Setup Studio attend un prérequis détectable. L'invité a été fermé
après les contrôles, et 1 850 660 octets de traces intermédiaires
ont été retirés.
Reçu `R7-10-evergreen-standard-account-readback.json`, SHA-256
`90137b3424cac9d651fb93ac42340ec5cb2194ec734130f60a125a0b82b49c97`.
[Installation Evergreen par machine ou utilisateur](https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution#installing-the-runtime-as-per-machine-or-per-user).

<a id="gate-7-evergreen-maintenance"></a>

Dans un Sandbox Windows 26100 sans SDK, un contrôle en lecture seule a
retrouvé la version `157.0.4341.42` dans l'inscription machine et dans
l'entrée Programmes. Seule la version `157.0.4341.46` est présente sur
disque, avec le navigateur et son installateur signés par Microsoft.
L'entrée Programmes ne fournit ni `ModifyPath` ni `UninstallString`.
Le contrôle a fermé l'invité sans exécuter de réparation. La qualification
Evergreen du Setup requiert toujours un Windows propre dont le chargeur
détecte le runtime. [Réparation des applications Windows](https://support.microsoft.com/en-us/windows/apps/repair-apps-and-programs-in-windows).
Reçu `evergreen-repair-admission-20261116/R7-10-evergreen-repair-admission-readback.json`,
SHA-256 `e2fe7b1b1413d942bd68597633e0dd8fbf2618e097fe60c1bf1d292af209ed0e`.

Le 17 novembre, la détection du Setup est alignée sur le chargeur
WebView2 du CLI. Le Setup utilise son code de retour directement, ce qui
permet au chargeur de résoudre aussi une stratégie WebView2 par
application. En mode embarqué, il conserve la signature de l'installateur
Microsoft comme prérequis à son exécution et interroge de nouveau le
chargeur après celle-ci. Le contrôle NSIS ciblé compile les deux variantes.
Une sonde compilée avec la routine exacte et le CLI 059 résout la
version 157.0.4341.46 sur l'hôte. La sonde n'installe aucun produit.
Reçu `windows-grand-plateau-reviewed-20261116/R7-10-nsis-webview2-loader-probe-20261117.json`,
SHA-256 `717213911eef01fb751e45dceddbffecad4d472330b2cbdb9fcd24f73d568c74`.

Le Setup corrigé a ensuite été réassemblé, avec le même paquet Studio
privé et le même CLI 059. L'invité Windows Sandbox 26100 est hors
réseau, sans Studio ni SDK. Le journal Microsoft confirme l'installation
de Visual C++ sans redémarrage après la perte du code de sortie dans le
script d'observation. Evergreen retourne `0x80070002`. Une stratégie
par exécutable fournit WebView2 Fixed Version signé 157.0.4341.60.
Le Setup silencieux renvoie 0, puis le CLI installé vérifie le paquet
et renvoie 0. L'inscription Evergreen conserve la valeur
`157.0.4341.42` avant et après l'essai. La correction qualifie
l'installation avec le prérequis Fixed Version géré, sans correction
de l'inscription Evergreen. L'installation standard Evergreen reste ouverte.

Sous `windows-grand-plateau-reviewed-20261116/setup/webview-loader-recheck-20261117/`,
`installer-build.json` a pour SHA-256
`1e7efd6623702980f6f90a565d06850434770ae61f2a5a49a847c0d5bce20975`,
`setup-final.json` a pour SHA-256
`6ccaa1b4f5698f8233407b75f76eebdf505e9497666de5e310bb0b029ad5c81f`
et `installed-verification.json` a pour SHA-256
`9a7d9fb845a4dc30b20a8ffbe7cd888e776630cb4b75bc1cc78e81dd66878adc`.
L'invité a été arrêté. Les 13 pièces vérifiées sur F: totalisent
28 284 octets. Le CAB temporaire de 310 954 844 octets a été retiré
sur E:. Le Setup de 78 508 635 octets sert au contrôle Evergreen
restant sur un autre invité.

La relecture d'attribution du Setup corrigé vérifie ses huit fichiers
extraits. Le paquet Studio, le CLI, le désinstallateur et les quatre
composants NSIS de la distribution précédente conservent leurs
empreintes. Le Setup laisse WebView2 et Visual C++ en prérequis externes.
Les avis et les archives sources vérifiés dans le paquet Studio 059
s'appliquent donc à ce même paquet dans le Setup corrigé. Le Studio
garde sa portée privée.
Reçu `setup/webview-loader-recheck-20261117/R7-08-studio-059-corrected-setup-notices.json`
sous `windows-grand-plateau-reviewed-20261116/`, SHA-256
`9e1939412abd28ae4ad4405c17857a1df22080d2a08f6062403051eaa97325d6`.

<a id="gate-7-evergreen-standard-admission"></a>

Le pilote `Test-Studio059Evergreen.ps1` se trouve dans la campagne sous
`windows-grand-plateau-reviewed-20261116/setup/evergreen-standard-admission-20261117/`.
Il vérifie le Setup corrigé et le CLI exacts par SHA-256. Le contrôle
Evergreen compare la version retournée par le chargeur à l'inscription
machine ou utilisateur et à l'exécutable signé par Microsoft. Il refuse
une redirection du dossier WebView2. Sur l'hôte, la version Evergreen
`157.0.4341.46` concorde. Le SDK et les outils de développement sont
détectés et le Setup n'est pas lancé. Hyper-V refuse toujours l'accès
depuis cette session.

Sur un autre Windows x64 propre avec le CRT x64 disponible, lancer
`Test-Studio059Evergreen.ps1 -Setup <Setup corrigé> -DeliveryCli <CLI 059>`
avant le parcours. L'option `-Install` exécute le Setup silencieux
avec `/NOINTEGRATION` et vérifie le paquet installé. Cette option reste
à exercer dans l'invité. Le mode silencieux couvre l'installation et
la vérification des fichiers. Le lancement du Studio, l'export et le
parcours joueur conservent leurs propres preuves.

Le script et le reçu hôte occupent 6 336 octets sur F:. Aucun
installateur supplémentaire n'est copié. SHA-256 du script
`fe6382c36517e8f3b9aaf50f58f5abbf510b21cefd6720049f2c68f5a4f8f392`
et du reçu
`c0a1365237b47b9971e5c0f42f228edd237f967ed798bf2bd29e50f0ac8efce6`.
[Distribution d'Evergreen et détection du runtime](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

La reprise de la création de l'invité LTSC avec le script existant
a été annulée par Windows à l'élévation. Aucun dossier, disque ou
descripteur de cet invité n'a été créé. Le Setup 059 attend toujours
un Windows propre avec Evergreen utilisable. Le reçu de 456 octets
`hyperv-ltsc-create-admission-20261117.json` est conservé dans le
même dossier, SHA-256
`26ad48eb08f2e8e21e9d0c4be4a852fd214636366f5555410830b29d2fadf503`.

Le média Windows 11 Enterprise LTSC français a aussi été démarré dans
QEMU 8.2.2 sous Docker. Hyper-V refuse l'accès depuis la session courante
et Docker ne dispose pas de `/dev/kvm`. La sonde utilise TCG, le firmware
OVMF avec magasin de clés Microsoft et un TPM 2.0 émulé par swtpm.
L'invite de démarrage du DVD accepte la relance, puis le démarrage reste
sur le logo TianoCore avec l'indicateur animé. Deux relevés espacés
conservent les mêmes 569 001 984 octets lus et 277 833 opérations.
Aucune fenêtre d'installation Windows n'a été observée. La sonde
n'avait ni disque système ni connexion réseau. Le conteneur jetable a
été arrêté et son retrait vérifié.
[Prérequis des VM Windows 11](https://learn.microsoft.com/en-us/windows/whats-new/windows-11-requirements#virtual-machine-support).
Reçu `setup/clean-windows-tcg-tpm-20261116/boot-probe.json`,
SHA-256 `4c7a118d8790dd0df4480950213417f1101b7c1c585a313ef24b12afdd950789`.
Les six fichiers vérifiés sur F: totalisent 15 941 octets. Leurs
copies temporaires ont été retirées d'E:.

Le CLI installé vérifie le 059. La fenêtre du Studio a été observée
avec un processus enfant Fixed Version. L'attente de fin du CLI dans
le script initial a retenu son flux pendant l'ouverture du Studio ;
l'observation a été recueillie séparément. Le Studio a ensuite relu
les 15 fichiers du projet sauvegardé dans son dossier standard et
exporté Le Grand Plateau en mode redistribuable par son interface.
Le résultat inclut le même installateur, la même application et le
même module que l'export 059 de l'hôte, vérifiés octet pour octet
par SHA-256. Le Sandbox a été fermé après la relecture des reçus.
Reçus sous `setup/fixed-webview2-diagnostic/out/` :

- `08-setup.json`, SHA-256 `e6163f599bbc1ee6bab22f21346201966444d19b58bd9ac51d85fca0cc0e3ad3`
- `10-studio-observation.json`, SHA-256 `69032affb441a020dc9ac33b72ff87786327e7a4645c4238ab2bff49e438b5b4`
- `12-standard-author-workspace.json`, SHA-256 `83957295b9d36553f50e0668eb6d272e5b75ae4e6296690604125d2e2c1d41f4`
- `13-author-export.json`, SHA-256 `7c71f36107c7988b4f76757f8e3493c9995a19428af683275d74adb580a00a53`

Dans un second Sandbox Windows 11 26100 hors réseau sans SDK ni
Studio, l'installateur graphique du Grand Plateau 059 vérifie et
installe le jeu au dossier proposé. La préparation avait omis
`candidate.json` dans la première copie de test. La recopie du
manifeste exact rétablit l'écran d'installation. L'installateur
crée `Jouer.exe` et ouvre le lanceur. Le CLI vérifie les paquets
installés et les trois exécutables. La stratégie WebView2 Fixed
Version est limitée aux exécutables de livraison de cet invité.

« Jouer », F5 puis « Reprendre » exécutent deux processus natifs
distincts sur la Radeon RX 7800 XT en Vulkan. Les rapports des
deux sessions confirment une projection prête, des sorties normales
et les mêmes empreintes de sauvegarde et d'état. Le script de
relecture a été repris en UTF-8 après un refus du libellé accentué,
sans rejouer les sessions. Le désinstallateur graphique retire
l'installation, `Jouer.exe`, les binaires du jeu et son propre
programme temporaire. La sauvegarde conserve son empreinte.
Les deux invités jetables sont fermés. Le CAB temporaire de
310 954 844 octets a été retiré sur E: après conservation de sa
signature, de son empreinte et de son URL dans le reçu.

Reçus sous `setup/clean-player-059/out/` :

- `06-player-receipt.json`, SHA-256 `5bfc2e362d1497095a680dd664544ffd4d9336d4a9dc5d737d3d775ec0c773e3`
- `08-uninstall-receipt.json`, SHA-256 `812d4d897efdb42f09177e2022ada7ea0fa91095a69d9bc3c5eed4efe1cb2991`

Le rejeu du chemin long a reproduit l'erreur Windows 3 avec le même
lanceur, trois répertoires de travail différents et la politique système
de chemins longs désactivée. Le chemin étendu de l'exécutable réussit
les trois sondes natives `--verify-distributable`. Le code commun
construit désormais ce chemin à partir de l'exécutable déjà vérifié,
pour le lanceur et le moteur natif. Les tests de bibliothèque Windows
passent à 97 réussites et 3 ignorés.
Matrice `R7-09-grand-plateau-059-long-path-process-matrix.json`, SHA-256
`9169a6658ff17d2d4dffa72cf8280a2e1e174a13c2d0e2e40224805722681547`.

Avec le seul préfixe de chemin étendu, la fenêtre est restée en échec à
261 caractères. La désactivation expérimentale
des deux gestionnaires de glisser-déposer a dépassé le refus OLE dans la
sonde, puis le lanceur et la sonde ont quitté avec l'erreur FailFast 1653.
La modification expérimentale du lanceur a été retirée.
Reçu `R7-09-grand-plateau-059-long-webview-diagnostic.json`, SHA-256
`9c686a846bb89a6a170a1e403b8645bbd9519fe78ccbae171be9b87bed302010`.
Le rejeu a été désinstallé, avec retrait de 446 fichiers. Les sondes
temporaires sur E: ont aussi été retirées (42 283 759 octets). Le contrôle
d'exécution a refusé la suppression groupée de trois dossiers résiduels
contenant 3 393 octets de plans, verrous et journaux vides.

<a id="gate-7-windows-chemins-longs"></a>

Le service de lancement Windows cherche désormais un alias DOS du
dossier de l'exécutable quand le chemin atteint la limite Win32.
Il conserve le nom de l'exécutable et vérifie que les deux chemins
résolvent le fichier déjà admis. Sans alias utilisable, il garde le
chemin étendu. Les 18 tests ciblés du garde Windows passent. Le test de
lien symbolique nécessitant un privilège est ignoré.

Le CLI reconstruit a lancé le paquet exact du Grand Plateau 059 depuis
la même installation de rejeu. Le chemin physique mesure 261 unités
UTF-16 et le chemin passé à Windows 115. La fenêtre « Le Grand Plateau »
et le processus WebView2 ont été observés. L'installation de rejeu a
été retirée après la fermeture. Reçu
`R7-09-grand-plateau-059-long-path-short-alias.json`, SHA-256
`f2d29fb4d234112d3dcc559e105f8080cb41473bca2a13169e27d6de3f12bfb6`.
Le rejeu par `Jouer.exe` reconstruit confirme le même chemin physique de
261 unités et un alias de 115 unités. L'entrée du jeu et le lanceur
interne ont tous deux l'empreinte du build Release
`159e0cba711474a54794797415eda7a84ad53582c15561c9115f7f1553cecacc`.
Le processus enfant reçoit l'identifiant attendu du Grand Plateau.
La fenêtre du jeu et son processus WebView2 sont visibles sur l'hôte.
Le rejeu utilise un paquet privé assemblé pour cet essai avec le moteur
et le module du 059. Le paquet redistribuable déjà revu est conservé.
La désinstallation a retiré 447 fichiers et les 521 089 000 octets du
dossier de préparation temporaire ont été retirés sur E:.
Reçu `player-entry-long-path/R7-09-grand-plateau-rebuilt-jouer-long-path.json`,
SHA-256
`ff73bd18858884f3efecb3be7c380b4967f1b5f1ca32a6aa06eceeb7d296af63`.
La preuve couvre le démarrage graphique sur l'hôte avec un alias DOS
utilisable. Les volumes sans alias DOS restent à qualifier.

Le jeu a été installé par CLI dans une racine séparée du Studio fermé.
Le launcher a démarré une nouvelle partie, puis repris sa sauvegarde
dans un second processus. La première commande de sauvegarde a été
refusée pendant la stabilisation des objets physiques. La suivante a
réussi. Les deux rapports confirment une projection prête sur RX 7800 XT
en Vulkan et une sortie normale. Les empreintes de la sauvegarde et de
l’état repris concordent. Le CLI a utilisé la racine utilisateur par
défaut, avec un dossier de sauvegarde propre à l’identifiant du jeu de
qualification. `R7-06-tidewater-058-launch-save-resume.json`, SHA-256
`e99fac855a9b2b2c71974185fbb04065a34c14b7afbf3c5d7d16d9944fdf7bf4`.
L’essai du Setup par NSIS sur Windows propre reste ouvert.
`setup/clean-windows-058-inputs.json` épingle le Setup, le CLI et les deux
installateurs de prérequis. Leurs signatures Microsoft ont été revérifiées
sur l’hôte. SHA-256 du manifeste
`62507e64cbdf16a848fc247e70e84ea1c59bfa914304a463a3d1325f7347b1aa`.

Le 16 novembre, le Sandbox hors réseau et sans SDK a vérifié les empreintes
du Setup et de son CLI. L'installation Visual C++ x64 signée a dépassé
le délai de 180 secondes. Son journal a confirmé la fin de la même
installation avec le code 0 et sans redémarrage. Le contrôle WebView2
du CLI exact a renvoyé `0x80070002`. Le registre de l'invité désignait
`157.0.4341.42`, dont l'exécutable était absent. La version
`157.0.4341.46` était présente avec un exécutable signé Microsoft.
La correction du registre dans l'invité a reproduit le même refus du
chargeur. Le Setup NSIS n'a pas été exécuté. L'invité a été fermé.

Sous `clean-windows-058-sandbox/out/` dans la campagne commune,
`guest-result.json` (délai dépassé) a pour SHA-256
`51ac3a9664239290405a6965feaec538a74d7a69cca1dc2ab98e5709f8c2f99b`.
`guest-result-after-crt.json` (fin de Visual C++ et échec WebView2) a
pour SHA-256
`96fa58e4fd62afd67aaae978a7f2be6f94fd22cf2946d2e0c422aa9c6448fb86`.

Les trois anciens exports des journaux Hyper-V de préparation reprenaient
les mêmes 120 lignes de Setup et le même événement, enrichis à chaque ligne
de métadonnées PowerShell. La projection compacte de 19 440 octets a été
copiée et relue sur F: avant le retrait des quatre copies d'E:, soit
1 517 089 610 octets. Les dates des trois acquisitions et leurs empreintes
figurent dans le reçu de projection. Reçu de retrait
`R7-10-hyperv-offline-log-retirement-20261116.json`, SHA-256
`ed998844de9d274368d4944762eab79246bafe372bb49444932baa17a0a348c9`.

Le cycle natif R7-09 installe le 057, applique le 058, puis restaure le
057 dans une racine créée pour cet essai. Les révisions 1, 2 et 3 sont
vérifiées et les empreintes des binaires Studio et moteur du kit
correspondent à chaque paquet actif. Trois fichiers copiés du projet
Tidewater solaire, dont le document auteur et un objet du workspace,
gardent leurs empreintes après chaque opération et la désinstallation.
La désinstallation native a retiré 852 fichiers de l'installation.
Les trois témoins, plans et fichiers d'état du dossier d'essai occupent
2 947 846 octets. La réouverture du projet après restauration et le
Setup NSIS sur Windows propre restent à vérifier.
Reçu `R7-09-studio-057-058-native-lifecycle-20261116.json` à la racine
de la campagne, SHA-256
`563ef0a7dde8aadf4c5cf620b6b595963dd60d9e4f439dbb48fbc7c82e047fe4`.

L'ancien invité Hyper-V n'a pas terminé l'installation de Windows. Sa
dernière capture affiche un refus de configuration matérielle pendant
la préparation du système. Le nouveau mode `-Ltsc` de
[`Prepare-WindowsHyperV.ps1`](../scripts/gate7/Prepare-WindowsHyperV.ps1)
utilise une racine distincte et l'image LTSC 2024 pour une installation
interactive avec TPM virtuel et démarrage sécurisé. L'inspection en
lecture seule retrouve les commandes Hyper-V et le service en cours
d'exécution. Le SHA-256 de l'ISO correspond au dossier de qualification,
`e45e7037f7da4ae799bcc7d666d4debd06a9cfb856690073ecbbfbc8441213d5`.
Windows a annulé l'élévation avant la création de l'invité LTSC.
L'essai du Setup 058 sur cette configuration reste à faire.

<a id="gate-7-setup-windows-055"></a>

### 15 novembre — Setup Windows 055 assemblé et relu

Le frontend et le Studio natif 055 ont été compilés avec
`--without-local-assets` et un inventaire source stable. Le Setup de
77 758 449 octets a pour SHA-256
`111458f95a1b26182f498a812517faa152993a4f06e523e39e5ea7b709332728`.
La relecture recalcule les empreintes de 425 fichiers du paquet Forge,
dont les 10 archives sources, et vérifie les quatre index de notices.
Les exécutables du Studio et du kit utilisent le CRT externe.
L'audit a relu la table d'assets Tauri dans l'exécutable Studio du paquet.
Son adresse et ses 16 entrées concordent avec le code PE et le reçu
de compilation. Les octets de 15 ressources décompressées sont identiques
à leurs entrées. La sérialisation de `index.html` conserve les scripts
intégrés. La table complète exclut `forge-imports` et `forge-kit`.
La relecture complémentaire inventorie les ressources PE de l'installateur,
du Studio, du launcher, du CLI et du moteur. Les images de l'icône de
l'installateur correspondent à `modern-install.ico` de NSIS. Celles du
Studio correspondent à l'icône dérivée du SVG original Forge. Trois
scripts de Tauri 2.11.5 ont été retrouvés octet pour octet dans le Studio
et le launcher. Les notices MIT et Apache-2.0 de cette crate sont présentes
dans le paquet. La police Fira Mono exacte est présente dans le moteur
natif, avec sa notice OFL-1.1. La recherche de ses octets dans les deux
fichiers WASM est négative. L'examen des segments de données des deux
modules du 055 ne relève aucun conteneur littéral PNG, GLB2 ou SFNT
valide. Le reçu `setup/R7-08-studio-055-wasm-literal-media.json` a pour
SHA-256 `f55e7125378677d4bc1ae4995bee65032619aa0800f162f1fba2542a12137744`.
Les données transformées et les ressources éventuellement générées au
démarrage restent à examiner. Le contrôle du 056 ci-dessous couvre les deux
modules WASM de ce candidat.
Le CLI du Setup a vérifié WebView2 157.0.4341.46 et préparé un plan neuf
sur l'hôte avec Visual C++ 14.51.36247.00. Le même CLI a appliqué et vérifié
une installation isolée. Les quatre index de notices et 10 archives sources
installés correspondent à la relecture du Setup. L'installation temporaire
a été retirée après contrôle. Le parcours NSIS, l'ouverture du Studio et
l'export depuis Windows sans SDK restent à qualifier. La session courante reçoit
un refus d'accès à Hyper-V et le dernier invité Sandbox n'a pas chargé
son WebView2 Evergreen.

Sous `studio-web-notice-corrections-20261110/windows-external-crt-20261114/candidate-055/`
dans la campagne commune, la relecture `setup/R7-08-studio-055-setup-readback.json`
a pour SHA-256 `300b9aa70e87245775cbb1c948e6693cda70b131e28f5169ee7d5978a99b2b03`.
Le précontrôle `setup/R7-10-studio-055-host-preflight.json` a pour SHA-256
`9be8a4901f1b5a588933ce7115c117515e35ab1bd6428c9616e2a0826051339b`.
L'installation isolée `setup/R7-10-studio-055-isolated-host-cli-install.json`
a pour SHA-256 `eba64fb2505efd098cb542118537f7569a710c2674726c856ace077fdf804a50`.
La table web `setup/R7-08-studio-055-embedded-web-assets.json` a pour
SHA-256 `1022c3c1d3a67426d0f8aafa902520e6d3ea0ded8c2153d67feb186e64e0f9ab`.

### 15 novembre — icône de secours du launcher Windows

Le launcher du 055 embarque l'icône du projet launcher. Ses six images
correspondent au fichier suivi, dont la source graphique n'a pas été
retrouvée. Le build Windows du launcher autonome prend désormais l'icône
issue du SVG original de Forge Studio. Les icônes déclarées dans les
projets exportés restent prioritaires via la présentation du jeu.

Le premier build a été refusé après une modification parallèle du Studio.
Le rejeu Release sur sources stables a confirmé que les six images de son
exécutable correspondent à l'icône Forge. Les suites ciblées ont donné
55 succès et un test ignoré. Le frontend temporaire du premier essai
a été retiré après contrôle, soit 64 083 octets. Le Setup 056 intègre
ce changement.

Sous `studio-web-notice-corrections-20261110/windows-external-crt-20261114/launcher-icon-20261115/`,
`replay/standalone-launcher-build-proof.json` a pour SHA-256
`767ea9cd1964b3ee4f22d5a4cbcadc98f00679c6c0cda5f3cdb010f27ec78357`.
`R7-08-windows-native-resources-055-and-launcher.json` a pour SHA-256
`890ab30bbb70426ad94813f3f1738315c6fcfbd223861dfaf0da669a6cffd106`.

<a id="gate-7-setup-windows-057"></a>

### 15 novembre — Setup Windows 057 et parcours privé sur l'hôte

Le Setup privé de 78 434 115 octets a pour SHA-256
`dbd489bb13360e92af9e36ea9fb469f1cd26076439a16f5073caa1b9a79f6a0f`.
La relecture rapproche les 425 fichiers, les quatre index de notices et
les dix archives sources. Les quatre exécutables importent le CRT MSVC.
Le CLI et le moteur du kit correspondent aux binaires de la campagne
`windows-scanned-format-20261115`. Le moteur exact a terminé le trajet
du module privé API 15 sur RX 7800 XT avec projection prête, 145 placements
scannés et 64 formes de collision enregistrées.

Le CLI du 057 a installé le paquet Studio dans un dossier de qualification
sur l'hôte et relu les octets installés. Les empreintes du Studio et du
moteur concordent avec le Setup et le fichier utilisateur témoin est
intact. L'installation est conservée pour le parcours auteur. Le Setup
NSIS et le parcours export, lancement et reprise sur Windows propre
restent à qualifier.

Le Studio installé a importé le module privé `.orpmod` dans un espace
auteur isolé. Le projet **Le camp exporté** a été sauvegardé et exporté
avec ses deux cartes. La carte aux débris sert de point de départ et le
profil graphique est Moyen. La relecture confirme les révisions des deux
cartes, les 141 variantes de débris et les empreintes des ressources.

L'installation par CLI a vérifié le jeu et le module dans une racine
joueur séparée. Le launcher a lancé une nouvelle partie, puis repris la
sauvegarde après fermeture du premier processus. Les deux sessions ont
affiché la scène sur RX 7800 XT et se sont terminées normalement. Les
empreintes du fichier et de l'état de jeu concordent entre les deux
rapports, et celle du fichier a été recalculée sur disque.

Reçus sur F: sous `windows-scanned-format-20261115/source-replay-001/setup/`.
`R7-08-studio-057-setup-readback.json` a pour SHA-256
`3a4d354f77e0acddf7cf3554eb43389b76230abe30d5e71c962d18bb74f9b7a1`.
`R7-09-studio-057-isolated-host-cli-install.json` a pour SHA-256
`c3939696c0f75caf9e9aaeb048abeb782487ca706b6a31a8a5aff99eb48069da`.
`R7-07-studio-057-installed-ui-startup.json` a pour SHA-256
`9d646864fbc14b2fe95e70edcc58c8fee0efc5b67466a147374e4d6aa90f1652`.
`R7-04-06-studio-057-private-export.json` a pour SHA-256
`0a9c26400e374651c07780ea8ba82a14ba394cc0ffb72768ae2784d2bce5aea1`.
`R7-09-studio-057-exported-game-host-install.json` a pour SHA-256
`adff7ecb2df3ae4d7919e5aa19c7513fb43cbe40365694ac716c305f3186103f`.
`R7-06-studio-057-exported-game-launch-resume.json` a pour SHA-256
`288ba716bbcfa3dc1796a084bb8b0a709d3af723bdb190867b04424c9be6c6c9`.
`R7-08-studio-057-private-game-license-readback.json` atteste la relecture
des 421 fichiers déclarés dans les licences du jeu installé, dont dix
archives sources. Le CLI exact refuse la publication du module privé.
SHA-256 `84a3f135d16cd10c15a57c4e0378ca93e3804cdeb8280ba62bb91a61fa487aa2`.
Le rapprochement par rôle du 057 recalcule les empreintes des 422
chemins de licences du Studio installé et des 421 du jeu installé. Les
quatre index et les dix archives sources correspondent dans les deux
paquets. Les trois plugins x86 Unicode et le bitmap du Setup exact
correspondent au NSIS 3.12 installé, dont le compilateur et `COPYING`
sont épinglés. Le journal indique zlib en mode solide. WebView2 Evergreen
et Visual C++ sont des prérequis externes du Setup.

Le CLI exact du 057 accepte le module Grand Plateau 1.0.1 sous CC0-1.0
avec `orpmod-inspect --publish`, relit les deux textes déclarés et refuse
la variante privée. Les descripteurs du Studio et du jeu installé
déclarent `private_local_only`. Le reçu
`R7-08-studio-057-role-license-readback.json` a pour SHA-256
`461ccaed000c6b355584f3bdd3cc497d104a5b24515dd688b02ca8ddea7059b5`.

Le manifeste `clean-windows-057-inputs.json` épingle le Setup, le CLI
et les deux prérequis Microsoft signés pour le prochain invité.
SHA-256 `283b4ee929c40b969c32b4ffcc823e118eb189bd56956ee9262ab900743fdb70`.
Le contrôle en direct d'Hyper-V refuse l'accès à la VM de qualification.
QEMU 11.1.0 avec WHPX et `q35,smm=off` atteint l'UEFI et monte l'ISO
Windows 11 Enterprise LTSC française en lecture seule. Le démarrage
explicite de `EFI/BOOT/BOOTX64.EFI` affiche l'invite de démarrage du DVD.
Après validation, l'invité reste sur le logo TianoCore. Le journal
indique `WHPX: Unexpected VP exit code 4`. Cette version de QEMU refuse
aussi l'option `-tpmdev`. Le Setup 057 n'a pas démarré dans cet invité.
Les deux processus QEMU sont arrêtés et les 605 095 octets de fichiers
temporaires sur E: ont été retirés après relecture des captures sur F:.
[Configuration des VM Windows 11](https://learn.microsoft.com/en-us/windows/whats-new/windows-11-requirements#virtual-machine-support).

Sous `windows-scanned-format-20261115/clean-windows-qemu/`, les reçus
`windows-ltsc-whpx-media-boot.json` et
`windows-ltsc-whpx-explicit-efi-boot.json` ont pour SHA-256 respectifs
`5e3e31bbe9d736592ff52c1d4c86c512c0cc22909375343eb58e055515eb1064` et
`d2a95e0bb23a219eb6777873f6ebce8a7b1dce57b6a0b03af28e36cacf54106e`.

Le reçu du trajet `module-scanned-debris-20261115/native-057-route/assessment.json`
a pour SHA-256 `3018a0f7bcbc4508568bda1caeb21fff291c944bb504bf42e5c2d8a9ddc50b8b`.

<a id="gate-7-setup-windows-056"></a>

### 15 novembre — Setup Windows 056

Le Setup de 77 769 129 octets a pour SHA-256
`c32c396d9415be60be5574a4bbd5b5b6bc6f4435e0f09eab32a422ad1f649e8c`.
La relecture vérifie les 425 fichiers du paquet Forge, les 10 archives
sources et les six images de l'icône Forge dans le launcher livré. La
table Tauri du Studio embarque 16 ressources vérifiées, avec les
références locales exclues. Le Studio, le CLI, le moteur et le launcher
utilisent Visual C++ externe. Les notices de 727 identités ont été
revérifiées avec le nouveau manifeste du Studio. Le premier build a été
refusé après une modification parallèle de l'historique des assets.
Le rejeu a terminé avec un inventaire stable.

La relecture des sources d'application du Setup exact confirme les 25
fichiers embarqués sous `licenses/native-media/`, soit 581 703 octets.
Les neuf archives Symphonia et l'archive `option-ext` correspondent aux
deux verrous Cargo épinglés par le build. Leurs textes de licence et
notices proviennent des membres d'origine et figurent aux mêmes octets
dans le paquet. Le reçu compare la préparation reproductible, le kit
assemblé et les fichiers vérifiés du Setup privé.
Reçu `setup/R7-08-studio-056-application-sources-readback.json`, SHA-256
`bafa0495dd7eee0df3f90ff508150d0f6e8a5de4338815992aece68025ce6576`.

Le CLI livré dans le 056 accepte le module CC0 du Grand Plateau avec
`orpmod-inspect --publish` et refuse son équivalent privé. La commande
contrôle le module. L'export du jeu complet utilise toujours le kit privé.
Le même CLI refuse le nouveau paquet Tidewater au décodage du rôle
`scanned_debris`. Le test de rendu des débris utilise un build plus récent
du moteur. La qualification du Setup de ce parcours doit porter sur un
candidat qui comprend aussi le format ajouté.

Le Setup 056 a été rapproché du compilateur et du fichier `COPYING` de
NSIS 3.12. Les octets des trois plugins x86 Unicode et du bitmap `win.bmp`
correspondent aux membres inventoriés du Setup exact. Le journal confirme
la compression zlib en mode solide. Le désinstallateur généré est épinglé
dans le même reçu. `setup/R7-08-studio-056-nsis-attribution.json` a pour
SHA-256 `b94db3a4c03d51e06a8c5333aff4206d4972ea65987e6edfe65c3bc0668c977a`.
[Licences applicables selon NSIS](https://nsis.sourceforge.io/License).

Le paquet Forge du Setup 056 contient le texte BSD 3-Clause du chargeur
WebView2 SDK 1.0.3650.58 et la notice qui le référence. Le texte a été
rapproché du manifeste des sources du build, et la notice du reçu du kit
056. Les deux fichiers figurent dans les chemins de licence du
paquet. Le runtime Evergreen est un prérequis externe et le candidat
conserve sa portée privée.
`setup/R7-08-studio-056-webview2-sdk-notice-readback.json` a pour SHA-256
`84a6f0d2c910e2f400c6f152f10aaf895d6cd078d76e89b19599a95a77212478`.

La relecture du 056 retrouve les trois scripts Tauri 2.11.5 dans le
Studio et le launcher. Les scripts et les textes MIT et Apache-2.0
correspondent aux octets du paquet Cargo verrouillé. La police Fira
Mono du paquet `bevy_text` verrouillé se trouve dans le moteur livré.
Son texte OFL-1.1 est indexé dans les chemins de licence du paquet et
correspond au blob indiqué dans l'index d'assets. Le contrôle porte
sur ces ressources dans le Setup privé exact.
`setup/R7-08-studio-056-linked-notices.json` a pour SHA-256
`3edcc82327d80a7f0cdd7e23909f01561b1a68c1134576b559fb8a9900d00f0d`.

Les 15 112 segments de données WASM du Studio et les deux du worker de
navigation ne contiennent aucun conteneur littéral PNG, GLB2 ou SFNT valide
selon le contrôle exécuté. Les deux modules du 056 correspondent octet pour
octet à ceux du 055 après substitution de la seule empreinte du fournisseur
de navigation. Son calcul a été reproduit à partir des entrées de build.
Seul `Cargo.lock` a changé parmi ces entrées. L'analyse concerne les formats
reconnus et les différences entre ces deux candidats.

Sur l'hôte, les CLI ont installé le 055, appliqué le 056 puis restauré
le 055 dans une racine isolée. Le module de qualification Le Grand
Plateau conserve son SHA-256 après mise à jour et restauration.
La racine temporaire a été retirée après contrôle. Windows a annulé la
demande UAC avant le démarrage de la VM existante. Un autre essai dans
un Sandbox hors ligne sans Rust, .NET ni SDK Windows a vérifié le SHA-256
du 056 et celui du redistribuable Microsoft signé. L'installation de Visual
C++ a atteint le délai de 120 secondes avant le lancement du Setup. Le
parcours NSIS et l'ouverture du Studio sur Windows sans SDK restent à
qualifier. Le reçu `clean-sandbox/setup-056-clean-sandbox.json` a pour
SHA-256 `f9781a50883ec560d327193ce1df4b59ab7bf8e03aed634b54dd000a4aa33f27`.
Le rejeu préparé dans `windows-056-clean-replay-20261115/ReplayNext.wsb`
relit les deux DLL Visual C++ x64 après l'attente de l'installateur,
conserve le résultat du processus et son journal, puis exécute le Setup
si les deux DLL satisfont son seuil. La
session du premier essai a été arrêtée avec `wsb stop` après contrôle de
son identifiant et du SHA-256 de son reçu sur F:. Une nouvelle session
hors ligne a démarré avec `ReplayNext.wsb`.

Dans le nouvel invité, le journal Visual C++ indique un code 0 sans
redémarrage et les deux DLL x64 sont en version `14.52.37029.0`.
Le Setup 056 a démarré et a refusé le prérequis WebView2 après avoir
constaté l'absence de l'exécutable pour la version déclarée `157.0.4341.42`.
Le reçu d'étape `clean-sandbox/setup-056-clean-sandbox-stage.json` conserve
les journaux et les versions, SHA-256
`afe00925766597c4769b95dd0b36f0363db90696d5c34533a9164935e69843f2`.
La collecte du reçu final a été interrompue. La capture sérialisait les
métadonnées PowerShell des lignes de journal. La copie d'étape a été
ramenée de 110 365 030 à 4 328 octets en conservant les valeurs relevées
et l'empreinte de l'original. Le script lit désormais les journaux comme
du texte et enregistre une éventuelle erreur de collecte. Après la
sauvegarde du reçu, `wsb stop` a arrêté ce seul invité. L'installation
complète du 056 reste à vérifier avec un Evergreen fonctionnel.

Un nouvel invité hors ligne a exécuté `ReplayEvergreen.wsb`. Il a vérifié
l'installateur Evergreen x64 fourni par Microsoft. Le reçu
`clean-sandbox/setup-056-evergreen-source.json` identifie le paquet de
215 116 512 octets, sa provenance et sa signature. SHA-256 du reçu
`8beb5847fe7bd0f9d72475f77fcba742fc97c2db4a6a04d8d66360d58eb43aee`.
L'installateur a retourné `0x80040902`. Le journal Edge Update précise
que l'installateur n'a pas pu créer un répertoire temporaire, code interne
11. L'exécution sous SYSTEM dans le même invité, avec un répertoire
temporaire dédié et 82 517 655 552 octets libres, a retourné le même code.
Le registre désigne encore `157.0.4341.42` et le disque fournit uniquement
`157.0.4341.46`. Le Setup 056 n'a pas été relancé. L'invité a été fermé
après contrôle des trois reçus `setup-056-evergreen-provision.json`,
`setup-056-evergreen-diagnostic.json` et `setup-056-evergreen-system.json`.
L'invité Hyper-V `Forge-Gate7-Windows` reste inaccessible à cette session.
Le prérequis Evergreen signé est disponible sur E: pour un Windows propre
distinct du Sandbox testé.

Un invité Sandbox connecté a exécuté le bootstrapper Evergreen officiel,
dont la signature Microsoft a été vérifiée sur l'hôte. Il a retourné
`0x80040902`. Le registre machine déclare toujours `157.0.4341.42`,
l'unique exécutable trouvé reste en `157.0.4341.46` et l'inscription
utilisateur est absente. Le Setup 056 attend un Windows avec un runtime
Evergreen fonctionnel. L'invité a été fermé et les fichiers temporaires
d'essai ont été retirés, soit 1 899 104 octets.
Reçu `clean-sandbox/evergreen-online/setup-056-evergreen-online.json`,
SHA-256 `b7fc311cdfd165888ce6cd37ae2e5147464c10eeec4c9791081f3163598d720f`.
[Bootstrapper Evergreen](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution#online-only-deployment).

Le disque de qualification d'octobre a été présenté en lecture seule à
QEMU, avec un disque différentiel. L'accélération WHPX ne prend pas en
charge le mode SMM de cette configuration. Sous TCG, le disque démarre
jusqu'au message « Windows n'a pas pu terminer l'installation ». Aucun
bureau invité n'a été atteint et le Setup Forge n'a pas été rejoué.
QEMU a été arrêté et le disque différentiel de 246 284 288 octets retiré.
Le reçu `clean-windows-qemu/windows-boot-diagnostic.json` du candidat 056
contient l'empreinte de la capture. SHA-256 du reçu
`9ec32b0394c654612c589fd4c13b9e4af24efb83cbfc14da22ef1f2cecf59dbb`.
Le journal Panther relevé sur ce disque en octobre indique que Sysprep
échoue pendant `Specialize` en ouvrant le magasin système BCD
(`0xC0000098`). Le rejeu du Setup nécessite un Windows distinct qui a
terminé son installation. Le Setup 056, Visual C++ x64 et Evergreen
x64 du 15 novembre sont disponibles sur E:. Leurs trois SHA-256 et les
deux signatures Microsoft ont été relus. Reçu
`clean-windows-qemu/windows-056-clean-input-readback.json`, SHA-256
`750edce318a6862b9bdf4807c5c4ead2af7f225e4fbb4f1c9fd4cc9244195a`.

Dans un nouveau Sandbox sans SDK, les deux DLL Visual C++ compatibles
étaient présentes après installation. L'exécutable WebView2
`157.0.4341.46` a été vérifié par sa signature Microsoft. La valeur
`pv` a été corrigée dans le registre de cet invité jetable. Le chargeur
a gardé l'erreur `0x80070002` avant et après la correction. Le
précontrôle a arrêté le rejeu avant de lancer le Setup 056. Le Sandbox
a été arrêté après la relecture des deux reçus sur F:.
Reçus `clean-sandbox/repaired-registration/webview-registration.json`
et `clean-sandbox/repaired-registration/setup-056-clean-sandbox-replay.json`,
SHA-256 `76d171f82d6d3a81098cb01deaf678523d495a871cf91cf000adc21d9cf558c8`
et `48c88bb06cc1c7d68f4347c84223c619e6418a93d8fd2bf9f8eafd3ba8b75390`.

Reçus sous `studio-web-notice-corrections-20261110/windows-external-crt-20261114/candidate-056-replay/setup/`.
`R7-08-studio-056-setup-readback.json` a pour SHA-256
`9b2d961f0f16ea0283dc4c0736862d96cc16d78e1a3a835689333713fb340f71`.
`R7-08-studio-056-embedded-web-assets.json` a pour SHA-256
`f3b14c3006d912a35a38faee55e4700aa7c4fe3894942e812e89dca8944ad7e0`.
`R7-08-studio-056-wasm-literal-media.json` a pour SHA-256
`7d4ed7ab82f7abd6dd3854fd7639ac3a20ec3bc3158c5a13b6b67c141c8ae4a3`.
`R7-08-studio-056-wasm-navigation-fingerprint-delta.json` a pour SHA-256
`67eb5d2d6a72a2d248e03ee2e57eeaa1b811ea8ec1054805d038d7fa9063e4e0`.
`R7-09-studio-055-056-host-upgrade-rollback.json` a pour SHA-256
`cd4671a91ea61af68d4e53473bf0a7beeb6684dea6d6aeac1ec6d4f4cb201eeb`.

<a id="gate-7-rejeu-linux-20261115"></a>

### 15 novembre — Préparation du rejeu Linux

Le snapshot `linux-replay-20261115-001` contient 7 535 fichiers relus, avec
un SHA-256 de `9e525a718e43e48a8e256916ccaf965a9a641f9f9e8da5c51558ba24a0fbfcd0`.
Le contrôle du frontend a été aligné sur le périmètre du build commun,
qui exclut les scripts d'orchestration de la conversion et conserve le
lecteur `scripts/assets/vault_storage.py`. Il vérifie l'absence du bundle
local lorsque le build authentifié déclare cette variante. Le contrôle
refuse un hash différent du lecteur du Vault ou du verrou Cargo, ainsi
qu'un bundle absent lorsque le build le requiert.
Reçu `editor-launcher-linux-qualification-20261102-001/runs/linux-replay-20261115-001/frontend-056-preflight.json`,
SHA-256 `70891804e0a0e294f4fad92f9d23e5ff3826090418220af122da825fc78b0b16`.
Le frontend contient encore l'overlay du kit Windows. Le parcours export
Linux nécessite son kit propre et demeure à qualifier.
Le premier conteneur a passé 204 tests du monde. L'API Docker a renvoyé
une erreur HTTP 500 pendant le build moteur. Après redémarrage de Docker
Desktop, le conteneur est revenu arrêté avec le code 255. Le journal complet
des tests du monde et `interruption.json` sont conservés dans le même dossier.
Ce reçu a pour SHA-256
`db0ef54b937c7f3dfd9156921f54be99b8d64be04358916599af2aa04fbb8020`.

Le graphe des quatre rôles natifs Linux a été résolu hors ligne avec
les manifests et verrous du snapshot. Les textes candidats de ses 703
identités ont été relus et vérifiés à partir des index 013 et 056. Les
manifests des composants Forge correspondent aux notices originales.
Les deux résultats dans `runs/linux-replay-20261115-002/` sont
`linux-notice-scope-inventory.json` (SHA-256
`ca00b4d1edd3c113a59dfd4eb15d8a3961baf8abb06fb6fef37d8d4c1b19184e`)
et `linux-notice-text-availability.json` (SHA-256
`a2b26b90d5325e92851abc97c1044745db8fda6df314f50a1db19a8950c8dc74`).
Le bundle candidat combine ces quatre rôles natifs et la fermeture
WASM commune, soit 786 identités, 419 fichiers et 2 469 559 octets.
Les vérificateurs de préparation de la livraison et du lanceur Docker
ont accepté ses index et ses reçus. Le reçu candidat
`linux-kit-notice-candidate.json` a pour SHA-256
`6f653a2aaf83181ac0e93692eb85c369627cc713e4155ad17074ea56a01f787e`.
Le rapprochement avec le kit et les binaires Linux livrés reste à qualifier.

Le rejeu `linux-replay-20261115-002` a retrouvé l'erreur HTTP 500 de Docker
pendant le build moteur avec un seul job Cargo. Son reçu d'interruption
a pour SHA-256 `045104b4049b4766c2e0fa6ead388d1afddbaefd6f6b8453dba0ef0d4e7c7502`.
Les deux conteneurs arrêtés ont été retirés. La VM Docker existante a reçu
8 Gio configurés et signale 8 321 089 536 octets utilisables. Le rejeu
`linux-replay-20261115-003` utilise ce même SDK, trois CPU et deux jobs
Cargo, avec un target isolé requis pour le kit autonome. Le lanceur a
vérifié les entrées de notices et joint le frontend 056 au même snapshot.
Reçus `linux-kit-inputs.json` et `frontend-attached.json`, SHA-256
`48352b30b5e1db350bcb8e0f3a7e96f6bfb167a955f8eef361d7d984e536ab76` et
`6cfb2bafa353728b9dd8a80780d2f53a78180914bb990da80c60cd6c92644b83`.
Le rejeu a passé les tests du monde, du moteur et du workspace/launcher.
Il s'est arrêté sur deux diagnostics Clippy de `forge_project`, dans la
validation des chemins historiques `.glb` et dans `preview_with_audit`.
`assessment.json` (SHA-256
`605d5b4b515b2024ad634dd56fa6eb29c5dfcd1a95be103eb237aeb6197e842c`)
vérifie les journaux des étapes terminées. L'arrêt précède la construction
du runtime, du kit et du Studio.

Le contrôle ciblé suivant a reproduit le snapshot 003, appliqué les
fichiers corrigés et passé le même Clippy strict du monde et du moteur.
L'empreinte de chacun des 13 fichiers modifiés et celle du journal sont
dans `engine-clippy-corrected.json`, SHA-256
`a08d107a39bfdda4e7139a3c0e04493f5a9b7333508b903ea8559b6adfd9a498`.
Ce contrôle laisse le verdict du rejeu complet 003 à son périmètre d'origine.
Le runtime et le service de livraison passent aussi Clippy strict après
la correction du parcours d'admission des meshes. Reçu
`runtime-delivery-clippy-corrected.json`, SHA-256
`d56cb9dba8b92df9e2b0e054d6e7a594dbffcae01dd5388f0bb9097f9ad86c13`.
Le rejeu `linux-replay-20261115-004` capture ces corrections. Le snapshot
de 7 535 fichiers a été relu intégralement (SHA-256
`93dfbbaa88c3a0ad79fa8ad8fe2b8007753519e05327f424f0ed6a25974667dc`).
Les 14 fichiers de code corrigés et les deux documents Gate 7 constituent
les seuls écarts par rapport au snapshot du 003. Les 3 417 entrées de build
correspondent au frontend 057 et le bundle de notices existant a passé
le précontrôle sur ce snapshot. Les reçus de précontrôle, de build frontend
et de rattachement au conteneur ont respectivement pour SHA-256
`e85dafdbd7df948be03c79116e590490f196e484b5c040bdd654ba8486499cb9`,
`c0e47b1a512c6a7178563d2915de0ceaff33512205969882da185ee3331876b7` et
`0886ad36486234138eac8e9a209517cf9de9ffe49c619fed2bacf32c0c22c17c`.
Le rejeu Docker utilise la VM existante, trois CPU et deux jobs Cargo.
Le rejeu 004 a passé 204 tests du monde, 1 521 tests du moteur, sept tests
de workspace/launcher et 91 tests du runtime. Quatorze tests moteur et
deux tests du runtime restent ignorés. Clippy strict du monde et du moteur
a réussi dans ce rejeu. Le runtime et la livraison passent aussi Clippy
strict. La livraison ajoute 240 tests réussis et 14 tests ignorés. Le
conteneur poursuit la compilation release du runtime avant assemblage du
kit et du Studio.

L'invité Ubuntu 24.04.5 de qualification est accessible par QEMU et SSH
avec sa clé hôte connue. QEMU a confirmé en direct que le VHDX de référence
était ouvert en lecture seule et que les écritures passaient par le disque
différentiel. L'inventaire ne relève aucun SDK de développement ni partage
de fichiers. WebKitGTK, GTK, Vulkan, Xvfb et xdotool étaient déjà présents.
Ce parcours prépare la qualification du produit avec ces prérequis.
L'invité a été arrêté proprement pendant la compilation et son descripteur
de relance est conservé. Les reçus `ubuntu-qemu-guest-readiness.json` et
`ubuntu-qemu-guest-shutdown.json` ont respectivement pour SHA-256
`e3851d67490d2ef07fb90de93c6de251216871843072fe68f5d8b4350be9c4f4` et
`992c71b9bdb70ff36bc49cfcffa2597214969b3c63ba0a9f50792a14368377ac`.

L'invité Ubuntu a ensuite installé le Studio Linux 0837 depuis le paquet
vérifié avant et après transfert. `verify` confirme la révision installée.
Le lancement sous verrou a ouvert une fenêtre Studio sous Xvfb. Le PID,
le SHA-256 de l'exécutable et ses dépendances ELF correspondent au candidat.
Un fichier témoin dans le profil auteur servira au contrôle de conservation
lors de la mise à jour et du retour arrière. L'invité a été arrêté proprement
avec cet état dans le disque différentiel. Reçus
`ubuntu-0837-upgrade-baseline.json` (SHA-256
`b735a20b02ed9de39ddbb3ca346d293f955ff70f0014e1bac0f30d9bb36deaad`) et
`ubuntu-0837-baseline-shutdown.json` (SHA-256
`2aa4a087fa765b45db4c13cbe0bdb01f6b1eeae05cf810e2c8afced84d234690`).
Le proxy Docker a perdu la route vers le moteur Linux pendant la
compilation release. Après rétablissement du moteur, le conteneur 004
est revenu arrêté avec le code 255, sans reçu final ni binaire release.
Les huit étapes terminées ont été relues et leurs journaux correspondent
au point de reprise et au snapshot. Le reçu `interruption.json` du 004
a pour SHA-256
`425256c32f2ac4fe130cc3fec64f4954871872895ea5b435dbcdbaa06411af2a`.
La cause de la perte de route reste indéterminée.

La VM Docker existante a été portée à 16 Gio pour laisser de la marge
hors de la limite mémoire du conteneur. Le rejeu `linux-replay-20261115-005`
est lancé depuis le même snapshot 057 avec les mêmes notices et le même
frontend. Reçus `linux-kit-inputs.json` et `frontend-attached.json` du 005,
SHA-256 `53fc415b3d671a6ba0a9c63856740af5709bd481c6264b9263bdd964a8881414`
et `43591122807061d236a9fe6c6172f9bb2e6c4470e22d25745ea4de01aece1d98`.
Les tests du monde, du moteur et du workspace et le Clippy strict du
moteur ont réussi. Les tests runtime du module donnent 91 succès et deux
tests ignorés. Le journal `05-module-runtime-tests.log` a pour SHA-256
`d3d7d72dd667b8b69290d078fab9ad484d5561adbd50780dffc87cb5b6600ec0`.
Le Clippy strict du runtime a réussi. Le journal
`06-module-runtime-clippy.log` a pour SHA-256
`2c2643757776586b4fa2842b4d0e02e773f1d39ac42725691da186377b9f6776`.
La livraison totalise 240 tests réussis et 14 ignorés. Son Clippy strict
a réussi. Le runtime `forge-module` et le launcher autonome ont compilé
en release sous Linux x64, avec résolution des bibliothèques dans le
conteneur SDK. Les reçus `launcher-build-proof.json` et
`linux-core-artifacts.json` ont été copiés à l'identique sur F:, SHA-256
`5412a91696c848756888f284f47919317889c5f6b99bf18c68e3b51a9dca372b`
et `3cb1b11468fd982f30621e8d7555edc8292fd4cf32412fcae5b4e3104b0128ee`.
L'inventaire du kit lit 786 identités de paquets. Le kit privé a été
assemblé avec 463 fichiers et 187 206 326 octets, sans essai d'installation
ni de jeu à cette étape. Les reçus `linux-kit-notice-receipt.json` et
`linux-game-kit-assembly-receipt.json` sont sur F:, SHA-256
`6f653a2aaf83181ac0e93692eb85c369627cc713e4155ad17074ea56a01f787e`
et `463cb4f9217415e656637de8755074522e655728894aa555360e296477bd51d9`.
La relecture ELF du kit 005 rapproche les deux exécutables de leurs
empreintes d'assemblage. Le runtime déclare six bibliothèques directes,
le launcher quinze dont GTK 3 et WebKitGTK 4.1. Toutes sont résolues par le
SDK Debian et aucun des 463 chemins du kit ne porte leur nom. Les deux
binaires référencent des symboles jusqu'à `GLIBC_2.35`. Cette relecture
couvre les imports directs des binaires 005. Le reçu
`R7-08-linux-player-elf-boundary.json` a pour SHA-256
`befa09836608030e1288b1159dc0bbe249e1bb374ad8f85dc6e9e5bbcd692f69`.
Les contrôles Web du moteur et du Studio et les tests navigateur passent.
Sous Linux, les tests natifs du Studio ont ensuite échoué sur deux
assertions Tauri. La liste de test omettait les permissions Aurora déjà
déclarées pour la prévisualisation et l'import. La configuration locale
omettait la commande `run_aurora_gameplay` appelée par le panneau. La
correction raccorde la commande générée aux fenêtres locales `main` et
`atelier` et maintient le contrôle des refus pour une origine distante
et une fenêtre étrangère. Reçu terminal du 005 `receipt.json`, SHA-256
`d54d9e07891cd66707d6fe4bf220a57b0a1451393ab97e573fd75338fd77a87d` et
journal `13-studio-tests.log`, SHA-256
`80608fa4340ce4b0b882131cf931e6ab151db02b18a3ccb6750e06afde6a8cc2`.
Les tests du binaire Studio corrigé donnent 34 succès et un test ignoré.
Les deux tests ACL sont verts. Reçu ciblé
`acl-xauth-production-build.json`, SHA-256
`680338b9ff0486ac214db5dbfcb45d5d0c40f3b067346d07a0f5e40966b3fb74`.
Les journaux
`07-delivery-tests.log` et `09-module-runtime-build.log` ont pour SHA-256
`2758624efa96aada6f4e4ce50d0a06ab9c5ce6d9b5adc44dfcb9e6c67208daa1`
et `240f72ae4ae4aa2f45b119650917bb9d02aad4cc4bf6cd43b9b4a916cb74ee49`.

Le kit 005 et le module CC0 du Grand Plateau ont servi à assembler un
paquet privé avec le CLI de livraison et la configuration 049 épinglée.
Le reçu `clean-player-assembly-proof.json` a pour SHA-256
`55a5b07394e4ee26e7e22226b54b0d86ade951d14a8de5cb38723053e70cadb4`.
L'invité Ubuntu sans SDK a vérifié l'installation et les deux
exécutables. Sous Xvfb avec un fichier d'autorité non standard,
le runtime s'arrête sur `XOpenDisplayFailed` après un refus
d'authentification. Le plan de lancement transmettait `DISPLAY` et
omettait `XAUTHORITY`. Le checkout inclut maintenant `XAUTHORITY` dans
la liste d'environnement admise sous Linux. Ce changement est postérieur
au snapshot 057. Reçus `clean-player-x11-auth-failure.json` et
`clean-player-x11-auth-failure.log`, SHA-256
`07f680addc5f0567b64a49a12769460d32544926f58ed9e60e14176d5b706825`
et `c0cd7137dacb3b135cbf371679bc5e0b9bfd93d80a845047aae6c76e33acc307`.

Avec la même installation et le chemin X11 par défaut, le launcher 005
ouvre la fenêtre du Grand Plateau sous llvmpipe/Vulkan. F5 produit un
fichier `forge.game-save` avec le module et la carte attendus, SHA-256
`375ce6b24e4e97a445bbd00763e0a13b8010e7f4bc37715ec70aca7a136a84c0`.
Le bouton « Reprendre » du launcher installé démarre un second runtime.
Son rapport de sortie confirme « Sauvegarde reprise », avec les mêmes
empreintes de sauvegarde et d'état et sans refus de reprise. La fenêtre
a été fermée au tick 37 pour recueillir ce rapport. La simulation n'avait
pas encore déclaré son état prêt. Les reçus `clean-player-resume-proof.json`
et `clean-player-resume-runtime.json` ont pour SHA-256
`0340d5c38b3af71397c0820d37a900522fe943acd9a1b1d1c92d561db34f00eb`
et `281a221f95b0c3ffdbea9075137f95c08208156ad072820b60f3aabb97bc1d27`.
L'invité a été arrêté proprement après transfert, avec l'installation
et la sauvegarde conservées sur le disque différentiel.
Le test ciblé du service commun a vérifié la transmission de `XAUTHORITY`
avec un chemin non standard dans le conteneur Linux. Il a tourné sur une
copie du snapshot 057 avec les correctifs X11 et ACL seuls. Le journal
`delivery-xauth-test.log` a pour SHA-256
`655b7eedce93e37b22d090901ba8ce72d78b324647809780400028d0ed113b59`.
Le launcher corrigé a compilé en release et passé son contrôle
d'admission après réutilisation des trois fichiers du frontend autonome
du 005 à leurs empreintes. Le premier build de diagnostic avait omis
la configuration Tauri du frontend autonome et a été refusé. Le
paquet de mise à jour conserve le moteur, le module CC0 et les autres
fichiers. Seul le binaire du launcher change dans la charge utile.
L'exécutable refusé a été retiré après contrôle des deux empreintes,
soit 243 468 040 octets. Le reçu du build et ses journaux restent utiles.

Sur l'invité Ubuntu sans SDK, le service de livraison a activé ce paquet
sur l'installation 005. Avec un fichier `XAUTHORITY` hors du chemin
par défaut, le bouton « Reprendre » a démarré le runtime et chargé la
sauvegarde existante. Le retour au 005 a conservé les empreintes de la
sauvegarde et du fichier utilisateur témoin. L'invité a été arrêté
proprement. Reçus `xauth-upgrade-package.json` et
`xauth-upgrade-resume-rollback.json`, SHA-256
`a399b13f4c3b703d277cd87733568b56ac10cfbcbb521417b06c18aa81536fb5`
et `92697eb1c4b02ffecb1bbfcd749c2228e1a426cd03b730c599fb65c9768078bc`.
Le rapport de projection observe un premier état prêt à l'image 35
et le dernier à l'image 68. À la sortie, à l'image 69, le rapport ne
confirme plus la disponibilité de la simulation. Les projections des
bâtiments et placements manuels restent prêtes. Le relevé final compte
74 pipelines graphiques, sans échec ni attente. Le terrain reste
synchronisé et sans transition en attente, avec `physics_ready=false`.
Le contrôle du jeu prêt jusqu'à la fermeture reste ouvert.
`xauth-upgrade-projection.json`,
SHA-256 `ed5f1e53b9da01ccf11ecfb525e7d51a3617bd90faa656c790b9877b4c9a812e`.

Le kit 005 a aussi assemblé une application Linux des Trois Cairns à
partir du module et du manifeste de launcher exportés par le Studio
Windows 017. Le paquet est relu par le CLI commun. Sur l'invité Ubuntu
sans SDK, les deux racines installées sont vérifiées après l'installation
des Trois Cairns. Le manifeste du Grand Plateau, sa sauvegarde et le
fichier témoin du profil conservent leurs empreintes. Le manifeste des
Trois Cairns désigne son propre espace de sauvegarde. Reçus
`cairns-assembly-proof.json` et `cairns-install-coexistence.json` du 005,
SHA-256 respectivement
`d76d506128b627770d093b7205931f8ac46fe55bee631c978c5c4a8e04aa0476` et
`962a3a1622e13e353ef8813313f8d5fee130d95ce2b7b461f6dd3df5a2440471`.

Le jeu installé des Trois Cairns a ouvert sa fenêtre, puis F5 a créé une
sauvegarde correspondant aux identités du jeu, du module et de la carte.
Le premier script a cherché dans le profil du Grand Plateau. La relecture
a trouvé la sauvegarde dans le profil enregistré par l'installation des
Trois Cairns. SHA-256 du fichier de sauvegarde
`5a828ce948968f6b7e107fa21d376bac84aa9471f305fc2dec6b885ccc14e1ea`.
Le bouton « Reprendre » du launcher a chargé ce fichier, et le runtime
a terminé avec « Sauvegarde reprise » et un code de sortie réussi. Son
rapport final ne confirme pas l'état prêt de la simulation. Le manifeste
et les données du Grand Plateau restent identiques. Reçus
`cairns-save-readback.json` et `cairns-resume-and-isolation.json`, SHA-256
respectivement `85e3bab0d589156465047d2b916e9bddcfcd978b3f0de659dad6f92007c942c6`
et `08c5129bcb75cf31edcd3be1f6b263bce398388dc978bb738ef52367d3cf6ef2`.

Le CLI commun a désinstallé les Trois Cairns et retiré 483 fichiers.
L'installation du Grand Plateau et les deux sauvegardes ont été relues
avec les mêmes empreintes. Reçu `cairns-uninstall-retention.json`, SHA-256
`84abb9b400a4e7e0a4215b4d720c86daf54bde0ba488d71dc2e1e7dbb140f73e`.
L'invité est arrêté. Les entrées temporaires retirées totalisent
187 422 846 octets sur le système de fichiers hôte, 187 421 510 dans
l'invité et 187 472 774 dans Docker. Le disque invité conserve les deux
sauvegardes et le projet auteur de qualification.

Le target isolé devenu inutile du rejeu 003 a été retiré après contrôle
de son reçu et de son propriétaire, soit 7 298 299 519 octets. Les deux
fichiers de transfert 0837, soit 401 343 949 octets, restent sur E: à la
suite du refus de retrait par la politique d'exécution.

Dans l'invité Ubuntu, le Studio 0837 ouvrait une fenêtre vide avec le
profil CPU QEMU par défaut. Le noyau a signalé deux instructions invalides
au même décalage dans JavaScriptCore. L'instruction relevée utilise AVX,
absent des capacités annoncées au guest. Avec le profil QEMU `-cpu max`,
le guest annonce AVX et AVX2, et le même Studio affiche l'interface sous
Xvfb. Aucun réglage de composition WebKit supplémentaire n'est actif sur
ce lancement. Le descripteur de relance est `launch-cpu-max.json` dans
`.build/vm-qualification/linux-clean-qemu-replay-057/`.

Le parcours auteur a créé et sauvegardé **Plateau de qualification**,
son module **Village de qualification** et sa carte **Prairie de
qualification** dans le Studio 0837. Le workspace est à la révision 4 et
référence l'objet carte vérifié par SHA-256. La capture montre le statut
« Carte sauvegardée » et une vue 3D toujours en chargement après 73 s.
Le projet et le fichier témoin sont conservés sur le disque différentiel
pour la mise à jour et le retour arrière. L'invité a été arrêté proprement.
Reçus du 005 `ubuntu-0837-authored-workspace.json` (SHA-256
`cfc76580e4c6b3699fe5ad85850da059cfa83ef4d1e4f4a557b684e123a2108e`) et
`ubuntu-authored-workspace-shutdown.json` (SHA-256
`3141b6dc17d393bff337bcf4dc280a640473f9ae9ed4447144c6884d46f27d09`).

Dans ce même invité, deux sondes autonomes de WebKitGTK
`2.52.6-0ubuntu0.24.04.1` ont utilisé Xvfb, le rendu logiciel et une page
locale en contexte sécurisé. WebAssembly et WebGL2 répondent. Avec les
réglages par défaut, WebGPU est désactivé et `navigator.gpu` est absent.
Après activation de la fonctionnalité dans la seconde sonde,
`navigator.gpu` vaut `null` et aucun adaptateur ou périphérique WebGPU
n'est obtenu. La vue 3D du Studio reste à qualifier dans un environnement
Linux capable d'exposer un adaptateur WebGPU. Le Studio et les réglages
système n'ont pas été modifiés par ces sondes. L'invité a été arrêté
proprement. Reçus du 005 `ubuntu-webkit-gtk-capability-probe.json` (SHA-256
`3f4208657b80e2dcaa04c3efd02806d327ed27f177e5333f768a166a73867b72`) et
`ubuntu-webkit-gtk-webgpu-optin.json` (SHA-256
`49c25af74f16b1b408d8a1c8bf7c54eb65f7bdcbd29e3087481585f4da3fc708`).
Le [réglage de compilation amont 2.52.6](https://github.com/WebKit/WebKit/blob/webkitgtk-2.52.6/Source/cmake/WebKitFeatures.cmake#L231)
désactive WebGPU par défaut. Les sondes décrivent le paquet Ubuntu de
l'invité. Dans le checkout, l'éditeur vérifie désormais la présence de
WebGPU avant de préparer la projection et termine l'indicateur de
chargement avec un message si la fonctionnalité est absente. Cette
correction est postérieure au snapshot 057 du rejeu.
La vérification Cargo `wasm32-unknown-unknown` en release réussit. Reçu
`studio-authoring-webgpu-admission-20261115.log.receipt.json` à la racine
de la campagne, SHA-256
`7b3c4fd822d4b4daa30940f661953cdfb2298679a881df6bc2aa19bdb01172f4`.
L'affichage du nouveau message dans un Setup reconstruit reste à vérifier.

Le Studio Linux épingle Tauri 2.11.5 avec Wry, qui utilise WebKitGTK du
système. Dans les sources amont de WebKitGTK 2.52.6 et 2.54.2, le getter
`Navigator::gpu()` n'instancie son objet que sous
`HAVE(WEBGPU_IMPLEMENTATION)`. La définition de cette macro est réservée
à `PLATFORM(COCOA)` dans les deux tags. Cela explique le résultat `null`
observé après activation de la préférence dans la sonde Ubuntu.
[WebKitGTK 2.52.6, getter](https://github.com/WebKit/WebKit/blob/webkitgtk-2.52.6/Source/WebCore/page/Navigator.cpp#L343-L363) ·
[WebKitGTK 2.52.6, plateforme](https://github.com/WebKit/WebKit/blob/webkitgtk-2.52.6/Source/WTF/wtf/PlatformHave.h#L906-L912) ·
[WebKitGTK 2.54.2, getter](https://github.com/WebKit/WebKit/blob/webkitgtk-2.54.2/Source/WebCore/page/Navigator.cpp#L343-L363) ·
[WebKitGTK 2.54.2, plateforme](https://github.com/WebKit/WebKit/blob/webkitgtk-2.54.2/Source/WTF/wtf/PlatformHave.h#L937-L943) ·
[WebView Linux de Tauri](https://github.com/tauri-apps/tauri-docs/blob/v2/src/content/docs/reference/webview-versions.md#webkit-macos-ios--linux).

Pour qualifier la vue 3D Linux avec l'interface actuelle du Studio, il
faut une WebView Linux qui fournisse effectivement un adaptateur et un
périphérique WebGPU. Ajouter un GPU à un invité avec ces versions amont
de WebKitGTK ne lève pas cette restriction du port GTK. L'installation,
la sauvegarde et l'export du Studio peuvent être contrôlés indépendamment
sur le 006, avec un verdict explicite pour la vue 3D.

La publication officielle `tauri-runtime-cef` 3.0.0-alpha.7 contient une
cible Linux x64, dépend de Tauri 3 et utilise CEF 154.5.0 avec GTK 4.
Le manifeste Tauri 2.11.5 du candidat 006 n'expose pas de variante CEF.
Évaluer cette voie demande de vérifier un vrai périphérique WebGPU, puis
le protocole local de ressources, les capacités `main` et `atelier`,
la sauvegarde et l'export depuis le Studio installé. Le paquet devra
aussi inventorier la distribution CEF effectivement livrée. Ces points
restent à qualifier avec le frontend et les services communs du Studio.
[Publication officielle Tauri CEF](https://github.com/tauri-apps/tauri/releases/tag/tauri-runtime-cef-v3.0.0-alpha.7) ·
[Manifeste CEF](https://github.com/tauri-apps/tauri/blob/tauri-runtime-cef-v3.0.0-alpha.7/crates/tauri-runtime-cef/Cargo.toml#L26-L48).

Une sonde indépendante sous Chromium 156.0.8078.79 sur Debian 12 a
obtenu un périphérique WebGPU via SwiftShader avec les options de rendu
logiciel consignées dans le reçu. Sur une page locale en contexte
sécurisé, elle relit `42` après exécution d'un shader de calcul et le
pixel RGBA `[255, 0, 0, 255]` après rendu dans un canvas WebGPU. Le
conteneur SDK jetable est retiré. Cette mesure porte sur le navigateur
autonome. Le Studio installé, l'intégration CEF et l'accélération
matérielle restent à qualifier.

Reçu `linux-chromium-webgpu-capability-20261115/R7-07-linux-chromium-webgpu-capability.json`,
SHA-256 `9c7c3c73cc2c7035ab5090cc2728334628a7279a7566f45a733071a9e8e8f3a0`.
[WebGPU sous Chromium sans interface](https://developer.chrome.com/blog/supercharge-web-ai-testing).

La sonde indépendante Tauri 3.0.0-alpha.6 avec CEF 3.0.0-alpha.7 compile
sur Ubuntu 24.04 avec GTK 4.14.5. Le SDK Debian 12, qui fournit GTK 4.8.3,
s'arrête au contrôle de la version minimale 4.14. Le conteneur Ubuntu
refuse la création des espaces de noms Chromium même après configuration
du binaire `chrome-sandbox` livré. L'invité QEMU Ubuntu 24.04.5 sans SDK
exécute ce même binaire sous Xvfb, avec l'auxiliaire Chromium configuré
et sans option de désactivation du sandbox.

Les fenêtres `main` et `atelier` chargent l'origine locale sécurisée
`http://tauri.localhost`. Chacune reçoit un `ArrayBuffer` de 65 536
octets conforme à la réponse attendue de la commande Tauri. Aucun
périphérique WebGPU n'est obtenu. Le code amont interprète les arguments
sans valeur et sans préfixe `--` comme des arguments positionnels. La
sonde préparée pour le prochain build corrige ces options et échoue si
l'une des deux fenêtres n'obtient pas de périphérique WebGPU. Le Studio
installé reste à qualifier. L'invité est arrêté. Deux conteneurs de
rejeu devenus redondants ont été retirés (4 071 993 344 octets de couches
inscriptibles déclarés par Docker). Le conteneur de compilation et les
ressources dans l'invité sont conservés pour le rejeu corrigé.

Reçu `linux-studio-cef-contract-20261115/R7-07-cef-ubuntu-ipc-contract.json`,
SHA-256 `d01e6466ef494413a2182ad0feef0f90fdce0a3de13a6a88c2be7d846c2eec3d`.
[Traitement des arguments CEF](https://github.com/tauri-apps/tauri/blob/tauri-runtime-cef-v3.0.0-alpha.7/crates/tauri-runtime-cef/src/runtime.rs#L2571-L2590).

Avec les options corrigées, la sonde compilée sur Ubuntu 24.04 charge
`libcef.so` à côté de l'exécutable dans l'invité sans SDK. Les deux
fenêtres conservent la réponse IPC conforme et exposent `navigator.gpu`.
Le processus GPU de CEF s'arrête six fois avec le code 139 sous Xvfb et
SwiftShader. Les deux fenêtres indiquent `device=false` et `passed=false`,
alors que la commande retourne 0. Le journal noyau enregistre les erreurs
de segmentation et un refus AppArmor. Le rapprochement ne permet pas
d'attribuer une cause commune à ces événements. Ce résultat ne qualifie
ni la vue 3D du Studio ni un GPU matériel.

L'invité a été arrêté et le conteneur de compilation précédent a été
retiré (6 359 568 384 octets de couche inscriptible déclarés par Docker).
Le conteneur arrêté du build corrigé et les ressources du rejeu restent
disponibles pour le diagnostic. Reçu
`linux-studio-cef-contract-20261115/R7-07-cef-ubuntu-webgpu-options.json`,
SHA-256 `8fb099e7e19c8499feb62aee8dc00851cfcd45a68991b4c3b50a48e4ebf53fb8`.

La comparaison hors WHPX n'a pas pu exécuter la sonde. L'invité Ubuntu
s'arrête au démarrage du noyau en TCG avec une erreur de temporisateur
IO-APIC, en mode multithread et en mode monothread sans HPET. Sous WHPX,
l'option VirtIO Venus permet d'atteindre SSH et d'observer un nœud DRM
dans l'invité. Le noyau signale un délai dépassé sur les capacités du
périphérique. Le QEMU Windows local signale l'absence du serveur de rendu
dans virglrenderer et l'échec d'initialisation Venus. L'inventaire WSL
renvoie `REGDB_E_CLASSNOTREG`. Aucun de ces chemins n'a fourni un
adaptateur Vulkan qualifié. Le prochain essai CEF matériel demande un
hôte Linux avec un adaptateur pris en charge.

Après l'arrêt de QEMU via QMP, `qemu-img check` ne relève aucune erreur
sur le disque différentiel Ubuntu. Reçu
`linux-studio-cef-contract-20261115/R7-07-virtual-linux-gpu-admission.json`,
SHA-256 `a086c2ecf46288da59ebcd46e0407a16961826a2e69630be0a564a1ebb8c153c`.
[Prérequis de VirtIO GPU et Venus](https://www.qemu.org/docs/master/system/devices/virtio/virtio-gpu.html).

Le 17 novembre, une lecture dans un instantané jetable du même invité
confirme les empreintes de la sonde, de `libcef.so`, du chargeur Vulkan
et du pilote SwiftShader. En sélectionnant explicitement le manifeste
ICD livré, le chargeur confirme le choix de SwiftShader avant un nouvel
arrêt du processus GPU avec le code 139. Le journal filtré s'arrête
avant les résultats des deux fenêtres. La vue 3D reste à qualifier.
Reçus de la campagne `linux-studio-cef-contract-20261115` :
`R7-07-cef-guest-renderer-inputs-20261117.json` (SHA-256
`66eed64fd22316be3e7e665d4c8a293f9c33c7395171291341aeee9bb00990c3`) et
`R7-07-cef-guest-vulkan-selection-20261117.json` (SHA-256
`43fea5c856bd2b5673b3ef877f8b8114d81f713066e7faebcd320f8a7719598f`).

### 15 novembre — construction du candidat Linux 006

Le transport `gate7-linux-corrected-20261115-006.zip` a été relu en entier.
Il contient 7 535 fichiers du checkout commun et inclut les correctifs
`XAUTHORITY`, ACL Aurora et détection WebGPU. Son SHA-256 est
`d2ccd25604377f4089ac6f26f91f451d0115fd170dbb866c5d82c7b69cfdf38f`.
Les 48 entrées épinglées du graphe de licences et les 51 de l'audit
Studio correspondent au snapshot. Le runner a admis la réutilisation
du lot de notices après vérification des empreintes.

Le rapprochement des quatre index du lot de notices du 006 couvre les
786 identités Cargo inventoriées et vérifie 415 textes distincts par
SHA-256. L'index de base couvre 709 identités, les compléments 60 et
les notices du code original Forge couvrent les 17 identités restantes.
Les cinq graphes d'audit sont entièrement couverts : CLI 70, launcher 333,
runtime 477, Studio natif 450 et Studio WASM 597. Le reçu
`R7-08-linux-notice-index-coverage.json` a pour SHA-256
`9aaf0b855eea2336adf93c59ab7276e23c3dfc8879832bb1919a3447eebd7de0`.
La vérification des binaires finaux, des bibliothèques système et des
droits sur les contenus tiers reste ouverte.

Pour R7-08, les graphes Cargo Linux x64 du Studio et du launcher autonome
ont été relus avec les verrous et options du 006. `selectors 0.36.1`
est atteint via `dom_query` et les dépendances de construction et macros
de Tauri. Il est absent des graphes de dépendances normales sans macros,
qui comptent 364 identités pour le Studio et 263 pour le launcher. Son
texte MPL-2.0 reste présent et vérifié dans le lot de notices. Le reçu
`R7-08-selectors-linux-build-scope.json` a pour SHA-256
`103b19a76d7d83af30356e41b70bbdf2c60dd4e17518ad117a88a50ff93a9907`.
Ce contrôle du graphe laisse à vérifier les octets générés et liés dans
les binaires Linux 006.

Les huit journaux vérifiés du 004, le manifeste et le journal de la
compilation interrompue sont conservés dans `sdk-results/` du 004. Son
cache de compilation isolé et le conteneur arrêté ont été retirés, ce
qui a rendu 17 660 731 392 octets disponibles dans le volume Docker.
Reçu `isolated-target-retirement.json`, SHA-256
`819d83b40d7077b196926928c16f6d17c9cfd426c1161f241456769e716ebaea`.

Le cache isolé du rejeu 005 a été retiré après vérification des 13 journaux,
du manifeste source et des deux reçus de construction et d'assemblage sur F:.
Le runtime, le launcher de qualification et son correctif X11 restent
disponibles hors du cache. Le 006 poursuit sa compilation dans son propre
cache. Le volume Docker passe de 20 026 970 112 à 51 207 409 664 octets
libres. Reçu du 005 `isolated-target-retirement.json`, SHA-256
`7820ed15dd55aa41d10702a1d210a8637dbaa04526dedce37b6d83009b134d3f`.

Le conteneur `linux-replay-20261115-006-sdk` a démarré les contrôles
du snapshot, avec un plafond de 3 CPU, 12 Gio et deux jobs Cargo. Les
204 tests du monde passent. Le frontend release optimisé via Trunk a été
construit sur le cache Windows partagé, avec des sources stables pendant
la compilation. Le contrôle confirme l'absence de `forge-imports` et
`forge-kit` dans la distribution Web. Le conteneur a reçu le frontend
vérifié. Les reçus `prepare-frontend-build-proof.json` et
`frontend-attached.json` ont respectivement les SHA-256
`c43c2c43b0934801abea0751e1acab3bbefb9ae647313d4f72563a6c07c27245` et
`a813dfd763a22273ac8ce83828c12cbc9b6d60bec2f0bce9f3a5cfb5a41be8e3`.
Les tests du moteur donnent 1 521 succès et 14 tests ignorés. Les sept
tests du workspace et du launcher passent. Les deux journaux sont relus
dans `sdk-results/` du 006, SHA-256 pour `02-engine-tests.log` et
`03-workspace-launcher-tests.log`, respectivement
`7c8b33523a1663ff66aaa15ee49d3f381047b4cbfd2bb2194a17f376bbef2e02` et
`a193bc94706ac9b49522f378c2ecc4a7c74079675dda4322d83b116a0632ec0c`.
Le Clippy strict de `forge_world` et `forge_engine` passe avec
`-D warnings`. Le journal `04-engine-clippy.log` a pour SHA-256
`7ae17771b0e9984ae478d61ae9cd9cc0ba4c8c75518fa05cd2c26d06e9f7ece6`.
Les tests du runtime donnent 91 succès et deux tests ignorés. Son Clippy
strict passe. Journaux `05-module-runtime-tests.log` et
`06-module-runtime-clippy.log`, SHA-256 respectivement
`8718f8f8555d18c4e4833d2e624602e18a14e1d9934392c857fdad3ffcc956b0` et
`d942fe854fb976a7fb668ff90e6927842f4535917646b8957bd955159d1ebd20`.
La livraison totalise 240 tests réussis et 14 ignorés. Son Clippy strict
passe. Journaux `07-delivery-tests.log` et `08-delivery-clippy.log`,
SHA-256 respectivement
`c60f10cf537c72aaf1eb6b503b549714cf10a8143e71a57b45187afae8c36af9` et
`3f23e01d860b27fb1232897437d489acae6e235aeea53294497e1e5a22d79d57`.
Le runtime de livraison a compilé en release. La relecture de l'ELF x64
confirme les mêmes six bibliothèques directes que le 005, avec des symboles
jusqu'à `GLIBC_2.35`. Le binaire mesure 163 557 200 octets, SHA-256
`05ac1b9287a3289becc2af5cfc9839a76b086b7ee9b86cf27dd5580943f9a3d9`.
Journal `09-module-runtime-build.log`, SHA-256
`22329d3e0aea9e4e63916f49dcccfe858e370c8727fd0fa7d6512362a03c1da2`.
Reçu `R7-08-runtime-release-elf-006.json`, SHA-256
`9d62261264d92f8a97d8b37d3a608f62c5757aab8b67ce715e730c38106e35a7`.
Le launcher autonome a compilé et `--verify-distributable` confirme son
frontend autonome sans marqueur Aube. Les deux exécutables sont vérifiés
par SHA-256 et leurs bibliothèques se résolvent dans le SDK. Les 15 imports
directs du launcher et sa version de symbole maximale `GLIBC_2.35`
correspondent au 005. Les reçus `launcher-build-proof.json`,
`linux-core-artifacts.json` et `R7-08-launcher-release-elf-006.json` ont
respectivement les SHA-256
`ba7183284bebfcaf5d19c90159ad2d31aabe9ef780c80a2fba3e0d3cb4e079dd`,
`058d4026cae1e18e3da5e46d3faa114b53061c5a40a20f41d6e7a771e4f6b927` et
`a59867cba02663ee906ea6b369b1ee2e68b0efe8f4b9d9a8248c20a836330418`.
Le reçu de démarrage de la campagne consigne le manifeste
`54bb2d18a28ef3acb4ab86030c8a990db423e9c6ada26d58aa974837212b045e`.

Le contrôle WASM du Studio 006 et les huit tests Node de cadence du
viewport passent. Les journaux `11-studio-web-check.log` et
`12-studio-browser-tests.log` ont respectivement les SHA-256
`0c6d89ac94fe9e82f93cdb252aa85080b2ae19fdf1f50ab56842d342a7349ba0` et
`5cc012af1a7d6492600cbb7426c9c1aeea54bfe66639b4e17392578f48c34ef5`.
Les tests natifs du Studio donnent 454 succès et 34 tests ignorés.
Le contrôle des permissions locales Tauri passe. Le journal
`13-studio-tests.log` a pour SHA-256
`1723cb44c6803c05d1d1225371fb63cb35abc203b052ae80f9c139b8fa53571e`.
Le Clippy strict du 006 a échoué avec le code 101 sur les imports, les
conversions de tailles et les lints de structure de sept fichiers Rust.
Le journal `sdk-results/14-studio-clippy.log` a pour SHA-256
`ce19a46f6a52a168b0d309652932f0744c8bc00bebc366fd5ae74ed79f47fec9`.
Le reçu terminal `sdk-results/receipt.json` a pour SHA-256
`b51c14f6959d35bb7c12784076e233b8f48e1b5a767d27ca6c07a4787eb775fe`.
Le build Release du Studio et le paquet Studio n'ont pas été produits.
Les corrections ciblées sont dans le checkout commun. Le Clippy strict sur
toutes les cibles du Studio passe sous Windows après avoir également remonté
un import du chemin d'export natif. Le journal
`studio-006-corrected-windows-clippy-replay-20261115.log` a pour SHA-256
`1b423ebdf6dad4c3a33a6154fd5c6db8c8027f07c58ec4cb132dcd4d311677af`.
Son reçu de commande a pour SHA-256
`4297ae3219957732b1462aabf3ac3420214fe954acdc4f7771f2a9bf5312bed0`.
Après son arrêt automatique sur inactivité et un redémarrage expiré,
Docker Desktop a été relancé. Le serveur Linux 29.9.2 répond, le
conteneur 006 est accessible avec son code terminal 1 et le volume
`forge-gate7-linux-build` est disponible sans conteneur actif.
Reçu `docker-engine-recovered-20261115.json`, SHA-256
`2e67b03f1f2b5ecc24dda0f10b5b6a833a43622b7684eb18e5a8ea21d5083e7b`.
Le rejeu ciblé du Clippy Linux passe avec les huit corrections épinglées
sur le snapshot 006. Il réutilise le target du 006 et vérifie les fichiers
sources ainsi que le frontend avant et après la commande. Les résultats
historiques gardent leur verdict. Sous `studio-clippy-corrected/`,
`receipt.json` a pour SHA-256
`ba5423d34d271c4673415448a3ede61d37251aa24351c580f29a812fd148e98a`
et `studio-clippy.log` a pour SHA-256
`0dfb224c9c6f4ea19af61cae6411c90623401811317ce78fdf708c1b8ba42c8d`.
L'archive ZIP des huit entrées est relue et vérifiée, SHA-256
`8053f23f1c2a08dfb18bf4e2037935e3982ade79ad3424298ee2f6b05c670ff6`.
Le conteneur et les 394 388 octets de staging sur E: ont été retirés.
Le build Release et l'assemblage du Studio Linux restent ouverts.

Le candidat `linux-replay-20261115-007-sdk` est lancé dans la campagne
éditeur et launcher Linux avec deux jobs Cargo et trois CPU. Le ZIP
du checkout commun contient 7 541 entrées vérifiées par lecture intégrale,
SHA-256 `2af9997c8f356e71239ae299e027c37978ba5acc7394c5ed4853efdbbc71f37f`.
Le graphe de licences réutilisé concorde sur les 48 manifests et
verrous. Les entrées du kit privé `0.0.59-gate7.linux` sont publiées
dans le conteneur. `started.json` et `linux-kit-inputs.json` ont pour
SHA-256 `6f81583a2ad8a4925dd34f3c156cfe437cdc007a974e7757e83cbd97df8bb7c4`
et `de4504c4fec494bcd9ce1d773f6a7f81b69230d91295a5ac12e9059cb45af9e3`.
Le 007 passe 204 tests du monde, 1 524 tests moteur et 7 tests du
workspace (lancement et récupération). Quatorze tests moteur sont ignorés.
Le reçu `core-test-readback.json` rapproche les trois empreintes des
journaux du conteneur avec le snapshot et le reçu de lancement du 007.
SHA-256 `21b4d7bd03d02f9ac9fe489bc6c99a5b286bcf7ef97d3daeb8e72dc6802245cf`.
Le Clippy strict du monde et du moteur passe également sur le 007.
`04-engine-clippy.log` a pour SHA-256
`85600ba55c3aa126549d56e898c583312811e7dc0c48b58c71b0c58f0729b13b`.
Le frontend 007 sans assets locaux a compilé. Le contrôle final a
refusé son admission : `apps/forge_studio/src/asset_library_native.rs`
est la seule entrée du périmètre des sources du build qui a changé
pendant l'exécution. Reçu `frontend-build/prepare-frontend-result.json`,
SHA-256 `754f0be7200362419654eeea647bc9b8b8212debc19ee384ed0dfa5da221345a`.
L'arrêt du 007 à la fin du contrôle courant a été demandé par le runner
pour libérer le cache Linux, avec export des preuves avant le 008. Reçu
`frontend-cancelled.json`, SHA-256
`5e8e2395f3cf6cbc0e2c29293793cc0441ddb6b1206e0943d96ae240442f01a1`.

Le frontend 008 sans assets locaux est construit et admis sur les 3 417
entrées pertinentes du nouveau snapshot. Le ZIP source de 7 541 fichiers
a été relu intégralement, SHA-256
`2f224d1f39e3c8c5d3964e215f820ec09aead0814888399caf4a73b2bf799ec0`.
Les 48 manifests et verrous Cargo épinglés concordent avec le bundle de
notices candidat. Les 14 fichiers du frontend sont conservés pour le
rejeu 008 dans un ZIP de 25 286 101 octets, avec relecture intégrale des
empreintes. Sous `linux-replay-20261115-008-frontend/`, le reçu
`frontend-payload.json` a pour SHA-256
`a494d0ef98f99ec0ea1be31ed2eb52ab213512f948e46b555fe36f4b6aea9180`.
Avant son arrêt demandé, le 007 a passé 91 tests du runtime, avec deux
tests ignorés. L'export a conservé les cinq contrôles terminés et retiré
le conteneur 007. Son `assessment.json` a pour SHA-256
`d25d50d2b0fc9195d6e92e299b30d09fc5aae4520137576c013d4507ad3da808`.
Le conteneur `linux-replay-20261115-008-sdk` est démarré et le frontend
est raccordé. Le reçu `frontend-attached.json` a pour SHA-256
`4db2f2fd71e42f75d41e441f3efa57a23e78018b9a841098058ac7b3f2458a4b`.
Les 14 fichiers (73 185 996 octets) ont été vérifiés dans le conteneur.
La copie temporaire sur E: est encore présente, sa suppression a été
refusée par l'exécuteur. Le build et le parcours du Studio restent à
vérifier.
Le kit joueur Linux x64 du 008 compte 463 fichiers et 187 265 222
octets. Le reçu d'assemblage `linux-game-kit-assembly-receipt.json`,
relu dans le volume Docker, porte le statut `PRIVATE_STAGED_ONLY`
et le SHA-256
`e877b196c876d080b504de56880b481f9d9b2b56b9e3ba53a7b40985d7338909`.
Le manifeste porte le SHA-256
`374c11d73623d184ddd82552e019d37d38138ea3418bba4deb81bfe26f23bfd7`.
Le kit 008 a été assemblé avec le manifeste de lancement et le module
CC0 déjà vérifiés du Grand Plateau. Le paquet
`13b6303bb7c6cf874032a9b93d0746c3124de0a16bf94e321b7792c8b71edf91`
a été installé et relu par le CLI dans un profil joueur isolé sur Ubuntu
24.04.5 sans SDK. Le bouton « Reprendre » du launcher chargé avec une
autorité X11 non standard a ouvert une copie de la sauvegarde 005.
Le fichier garde son SHA-256. Le moteur 008 quitte normalement. La
projection est prête à l'image 39 et à la dernière image observée, 73.
La simulation est prête à la fermeture. L'invité héberge également le
Studio. Le jeu a été assemblé à partir du kit et d'un manifeste préexistant.
Reçu `grand-plateau-008/assessment.json`, SHA-256
`029d27048e44562294bb69cea302bdd2ae64d5b58b3e4e4e8d8cd4c058605826`.
Le build Release du Studio 008 a terminé. Le reçu final et ses pièces
sont vérifiés sur F: dans `runs/linux-replay-20261115-008-sdk/`, SHA-256
du reçu `c084d19146c7ed4b9a6fda5ab638238c176b2889aff4df35af78cf98262e98fb`.
Le binaire Studio de 50 788 136 octets est identifié par
`006eb5897016af3ae015039543cbba46ac0f47b6b03cef7c9705dfe16d1d5304`.
Le reçu conserve `clean_os_qualified=false` et `graphics_qualified=false`.
La relecture des 463 fichiers et 187 265 222 octets du kit joueur 008
confirme le manifeste, dont les 461 entrées de licences et notices et
les 10 archives sources. Les 48 manifests et verrous du graphe des
notices correspondent au snapshot 008. Le reçu
`R7-08-linux-player-kit-008-license-readback.json` a pour SHA-256
`2b85611634ab4747c08a18f812f7ccdc3268769f05a24540052897b3e2451944`.
La relecture du volume SDK en lecture seule relie les quatre ELF 008 au
reçu du build et les deux binaires joueur au manifeste du kit. Le
moteur, le launcher, le Studio et le CLI déclarent respectivement 6,
15, 17 et 4 dépendances ELF directes. Elles se résolvent avec les
bibliothèques du SDK. Les quatre ELF référencent des symboles jusqu'à
`GLIBC_2.35`. Les noms des bibliothèques directes sont absents du kit
joueur. `R7-08-linux-008-elf-linkage-readback.json` a pour SHA-256
`a2d7c7156230ab387409548fb113d6629cbd29a2c6749de956d631213491a3fd`.
Le runner a assemblé le paquet Studio après le reçu de compilation. Les
238 214 284 octets du paquet ont pour SHA-256
`e1c308a8906fc05ff0682e9221956c7da385452a1f3f4d8981b54ce9709e59b8`.
La relecture rapproche les 465 fichiers, les 462 chemins de licences,
le CLI et le kit adjacent avec les reçus du build. Reçu
`linux-studio-build-008-readback.json`, SHA-256
`3d697b73b7f6670764218afeb3f1a4ca048c0242b7486a0723a4eccd8a974f29`.

L'invité QEMU Ubuntu 24.04.5 sans SDK a reçu le paquet et le CLI avec
leurs SHA-256 vérifiés. Les quatre étapes du CLI ont réussi. Le binaire
installé correspond au build et ses dépendances ELF se résolvent avec
WebKitGTK 2.52.6. Le fichier utilisateur témoin est intact. Reçu
`studio-008-clean-ubuntu/assessment.json`, SHA-256
`30c01493028a128c0e469b0fb68d18bc649e5fb0abd42d45b1a15f99baa8536f`.
QEMU a été arrêté après la collecte. L'installation et les deux entrées
de livraison sont conservées dans son disque différentiel pour les
essais graphiques.
Sur le même invité, le CLI 008 a appliqué une mise à jour du Studio 0837
vers le 008, puis le plan de restauration du 0837. Les trois vérifications
de l'installation observent les révisions 1, 2 et 3, avec les empreintes
des binaires attendues. Les quatre fichiers auteur (7 254 octets) ont
gardé leurs empreintes après chaque transaction. Les huit reçus ont été
relus sur F: et l'invité a été arrêté.
Reçu `studio-008-update-rollback/assessment.json`, SHA-256
`9ddeb727c60a1a117a51bee5c15f06ce05917789d0114a50ca3573e283fc8213`.
Le Studio 008 installé a démarré via le CLI sous Xvfb sur le même Ubuntu
sans SDK. Il a affiché l'atelier puis ouvert **Prairie de qualification**
depuis **Village de qualification**. Les diagnostics indiquent que les
ressources du module sont prêtes pour la projection. La vue 3D reste
vide. Le manifeste auteur et son objet témoin ont gardé leurs empreintes.
Le Studio et l'invité ont été arrêtés après vérification.
Reçu et captures sous `studio-008-gui/`. Le SHA-256 d'`assessment.json`
est `94d034017bedcb2ad7f9ee844d06ab2c9889f009c890f2d9450c1d35bf5efc82`.

Le Studio 008 installé a refusé l'export de la prairie sans définitions
RPG. L'interface a ensuite importé le module CC0 du **Grand Plateau**,
sauvegardé la configuration du jeu et réalisé son export privé.
Le CLI a installé et vérifié le jeu dans un profil joueur distinct du
profil auteur, sur le même Ubuntu sans SDK. La carte et ses données RPG
gardent les mêmes empreintes.

Le launcher du jeu exporté a ouvert une nouvelle partie, puis repris
la sauvegarde créée par F5. Le fichier et l'état relu concordent entre
les deux processus. La première session atteint la projection prête à
l'image 39, puis la perd à la dernière observation, à l'image 55.
La reprise confirme la simulation prête. Le rapport de projection de
cette session est vide après la coupure du serveur X de qualification.
La vue 3D de l'éditeur reste à qualifier.

Reçus sous `studio-008-game-export/`. SHA-256
d'`export-install-proof.json`
`f142164f71fc8b875b3481cb16002f41a9a95bd8283ec8006c830b19fc6b307c`
et de `play-resume-assessment.json`
`3c03f8a271a6db46fe537a2ad803fba21360491c1695c6337898c995faaf50cf`.

Le CLI 008 a désinstallé le Studio de qualification après sa fermeture.
Le paquet de 466 fichiers a été retiré, soit 238 214 268 octets logiques.
Les 23 fichiers de l'espace auteur, dont les quatre fichiers de l'export,
et le fichier témoin ont conservé leurs empreintes. La vérification de
l'installation du jeu et de sa sauvegarde donne le même résultat avant
et après le retrait. Le CLI accepte la racine pour une réinstallation.
Reçu `studio-008-game-export/studio-uninstall-assessment.json`, SHA-256
`49e36b3f63a0dbf67c803d1c6eb626a56054d4201a7d02490248c61559647ac9`.

Après le retrait, le launcher du jeu installé a repris la même
sauvegarde. Le rapport complet a été relu après la fin du processus,
avant la fermeture de Xvfb. La projection est prête aux images 41 et
82, dernière image observée, et la simulation est prête à la fermeture.
Les autres racines de qualification sont conservées dans l'invité.
Reçu `studio-008-game-export/post-uninstall-assessment.json`, SHA-256
`a9bf55920ec615c2f8fc5f94557ec408928eecc8976470ba75a563d3a144ee93`.

Une nouvelle image différentielle Ubuntu 24.04.5 a démarré depuis le
disque de base en lecture seule. Le contrôle confirme l'absence de
Studio et de SDK. L'invité a reçu les quatre fichiers intacts de
l'export 008. L'installateur de l'export a vérifié le dossier neuf,
installé le jeu et fourni l'entrée durable « Jouer ». Les 464 fichiers
du paquet application installé concordent avec son manifeste.

Le launcher a lancé une nouvelle partie, sauvegardé avec F5 et repris
la sauvegarde intacte dans un second processus. La nouvelle partie a
atteint une projection prête à l'image 40, avec un dernier état prêt à
l'image 90. À sa fermeture, le rapport indique un état non prêt. La
reprise s'est terminée avec projection et simulation prêtes, à
l'image 80. Les deux rapports complets sont conservés dans
`studio-008-game-export/player-only-ubuntu/`.

Les reçus `installer-assessment.json` et `play-resume-assessment.json`
ont respectivement les SHA-256
`a2514457d2c8b7483251c1549e82561a32fb0ad9687001f828a521d0b65e17fc` et
`2b7901f06af78f09493479fdf92171e5f66b296b5d9ff3a35af01fb1ddbda2bd`.
La relecture des journaux situe les demandes de fermeture aux images 90
et 79. L'observation finale arrive une image plus tard dans les deux
sessions. Les deux projections conservent les mêmes empreintes de
scène et de gameplay, 169 cellules résidentes et 16 cellules détaillées.
Aucune demande de terrain ni compilation de pipeline n'est en attente.
Seule la nouvelle partie termine avec la physique du terrain streamé
et la simulation non prêtes. Le motif précis de la suspension physique
reste indéterminé. Reçu `player-only-ubuntu/readiness-close-comparison.json`,
SHA-256 `d06ebb3b4e1e1716e072705842a5d471e15d2a7aa307afe1719fdfbcc68aa76f`.
Dans le moteur commun, une nouvelle enveloppe de corps pouvait suspendre
la physique jusqu'à l'enregistrement de la demande, même si le plan
réutilisait exactement les cellules déjà résidentes. Le correctif conserve
la simulation dans ce cas après validation du plan et des colliders.
Les préparations en cours, les changements de source et les erreurs de
chargement conservent leur suspension.
Les 19 tests de la physique du terrain streamé passent sous Windows,
dont la régression sur une demande qui conserve les mêmes cellules.
Journal `streamed-physics-equivalent-coverage-20261115.log`, SHA-256
`8b3d4b8a4ae2bfd3bbc089f348f27d3ce9c2de82412ca5cdd9ea0acc83a5ecf9`.

Le rejeu Linux 008 substitue uniquement `bin/forge-module` dans les 464
fichiers du paquet application. Le module, le lanceur, les réglages
et les données de gameplay sont identiques. Dans l'invité joueur
Ubuntu, l'installateur a créé une installation et un profil neufs.
La nouvelle partie a sauvegardé avec F5 et quitté normalement. Le dernier
état prêt coïncide encore avec la demande de fermeture, cette fois à
l'image 93. À l'image 94, la physique du terrain et la simulation
sont non prêtes, avec les projections d'objets et de bâtiments prêtes
et aucune demande de terrain en attente. Le correctif des demandes de
terrain équivalentes ne résout pas cette perte d'état à la fermeture.
Le moteur transmet désormais un motif de suspension dans les rapports
de terrain échantillonné et procédural. Il distingue les préparations,
les changements de source ou de cellules, les colliders, les commandes
hors enveloppe et la suspension demandée par le contrôle de simulation.
Les critères d'admission sont inchangés.

Reçus sous `player-only-ubuntu/`. SHA-256 de
`terrain-replay-build.log`
`ad739d5f80a7771f0710d80ef41b543760bcd80afb3682f76dedbb9afa6d767a`,
de `terrain-replay-assembly.json`
`16e7d210305f95407113eae9e7c836c6728de1c09dbcfa8d1543c106a18620ac`
et de `terrain-replay-assessment.json`
`0bca602b14c3d55240ff3b548b6fea46ec8e343bc15e2c344711206b2b8d0c62`.

Le rejeu instrumenté a repris les mêmes paquets du Grand Plateau en
remplaçant uniquement le binaire moteur. L'installateur a préparé un
profil joueur neuf sur l'invité Ubuntu. La partie a sauvegardé avec F5.
La demande de fermeture arrive à l'image 86. À l'image 87, la physique
et la simulation sont prêtes, et aucun motif de suspension n'est présent.
La perte d'état enregistrée par les deux rejeux précédents reste à
expliquer. Reçus sous `player-only-ubuntu/` :
`terrain-hold-assembly.json` a pour SHA-256
`df502e166faeb14d6ebe1d75ebe5549d1421d234d6a50741ed02ef4cf66621d4`
et `terrain-hold-assessment.json` a pour SHA-256
`35216d3f992191352309068a61bd20bd2d5670d4d58e529d611c0f3fa1e925d5`.

L'invité est arrêté. Son disque différentiel de 1 419 313 152 octets
conserve les trois installations isolées et leurs sauvegardes pour la
comparaison et les contrôles de maintenance.
Le nettoyage automatique des quatre copies de transit, 212 226 674
octets sur E:, a été bloqué par la politique d'exécution.
La copie de staging des 20 preuves sur E: représente 1 832 388 octets.
L'exécuteur a refusé leur nettoyage. Le conteneur arrêté et le volume
conservent les binaires et le paquet utiles à la qualification.
Le contrôle préalable rapproche les 3 417 entrées dans le périmètre
du frontend avec le snapshot Docker. Il exclut les entrées de l'ancien
launcher déjà hors périmètre du build Linux. Dans la même campagne,
`frontend-source-preflight.json` a pour SHA-256
`ae26cf8c2289460e1471087de899e7524bda34d25fab6cf281d3027e8cd998ee`.
Les 3 497 entrées du build frontend Windows coïncident avec celles du
runtime Release utilisé pour la comparaison de végétation. L'exécutable
Windows conservé correspond à son reçu, ce qui permet de réutiliser ce
runtime sur ces sources. Reçu `windows-runtime-source-match.json`,
SHA-256 `c51cb1b76cfbb7458ab1af688f277d82d14cd8608cfebbe696adf2a3ad15f7ee`.

Le script de qualification du checkout accepte
`--keep-studio-installed` pour poursuivre le parcours
auteur et l'export dans la même installation vérifiée. Le rejeu décrit
ci-dessus a conservé le Studio 008 installé pour le parcours graphique.

Sur Ubuntu sans SDK, le CLI de livraison 006 a relu l'installation Studio
0837 et confirmé le binaire actif, le profil auteur et les trois fichiers
épinglés du projet « Plateau de qualification ». Le manifeste d'installation
et les fichiers auteur conservent leurs empreintes. Le CLI 006 est prêt dans
l'invité arrêté, pour la mise à jour dès réception du paquet Studio 006.
Reçu `studio-0837-baseline-admitted-by-cli-006.json`, SHA-256
`e29a53e35fe70d7c04e05e0abe38ea24686fc959e1b6555dbdc54034ee9cccc4`.

Le cache de compilation isolé du 0837 a été retiré après vérification de
ses 15 journaux, du manifeste et du snapshot de reconstruction conservé sur
E:. Le paquet Studio 0837 et son kit joueur restent disponibles hors du
cache pour le parcours de mise à jour. Le volume Docker passe de
37 908 410 368 à 69 002 940 416 octets libres. Le cache actif du 006 est
distinct. Reçu `candidate-20261103-0837/isolated-target-retirement-20261115.json`,
SHA-256 `396181022a91ded7b922ee7b2dc896f31bea7e46e3ac66185bba305e8cd853b2`.

<a id="gate-7-linux-kit-revu-009"></a>

### 16 novembre — Kit Linux 009 revu pour la redistribution

Le vérificateur Linux admet désormais l'intégration Studio d'un kit
revu pour la redistribution. Le reçu d'assemblage et le manifeste
intégré doivent déclarer le même snapshot, la même plateforme et la
même empreinte de revue. Les tests de preuve réussissent dans 13 cas,
avec un cas de lien symbolique ignoré sur l'hôte Windows.

Le transport du 009 contient 7 606 fichiers et 496 885 392 octets.
Sa relecture intégrale retrouve les empreintes des 48 manifests et
verrous de licences, des deux entrées de résolution et des 51 entrées
de l'audit Studio. Les notices existantes correspondent à ces entrées.
Le binaire et la revue du kit 009 restent à produire.
Reçu `runs/linux-reviewed-20261116-009-preflight/source-and-notices-admission.json`,
SHA-256 `33f36d542ed8316845b3dfdfeedae8aae6da7d6d4066a2afb3d56656ebc3815f`.

Le volume SDK disposait de 16 Gio. Le retrait des targets isolés de
`candidate-20261103-053204` et `webview-20261103-001` rend 60 770 091 008
octets au volume Docker. Les sources ZIP et les journaux exportés,
ainsi que les quatre binaires conservés du candidat WebView, ont été
relus aux empreintes avant retrait. Le volume disposait ensuite de
77 902 106 624 octets. Reçu du 009 `retired-sdk-targets.json`, SHA-256
`e44337bc5973fa3e5a71f860575f26857fe6bfdc16490fda92185fc6f26dd11a`.

Le runner du 009 construit le moteur joueur, le launcher et le kit
privé de référence. Il attend ensuite une revue qui épingle cet
inventaire avant de remonter le kit avec les mêmes binaires et de
l'intégrer au frontend du Studio. Le conteneur du 009 est lancé sur
trois CPU et deux jobs Cargo, avec le cache SDK existant. Les 204 tests
du monde, 1 533 tests moteur et sept tests de lancement et récupération
passent. Quinze tests moteur sont ignorés par cette suite. Clippy strict
arrête le 009 sur la taille du lecteur de manifeste et la comparaison
du suffixe `.glb` dans les étals. Aucun kit 009 n'a été assemblé.
Reçus sous `runs/linux-reviewed-20261116-009/`. SHA-256 de `started.json`
`7a2495bacad2c90b4fab9ce5518ced58cc57fe1ee642cd165daa15508b876b22`
et de `protocol-scripts.json`
`bb55fb0b1792063b43d3c7464366d4f05b573c02ad2565a16221f4f81e8b9cac`.
SHA-256 de `assessment.json`
`b325eb677673354a5074904cd43c7414036a8de11a1327147788bf89c753a79f`
et du journal `result/04-engine-clippy.log`
`0d35a6baa02d0b1aa6efcd66656fc95ab607fd246c6c592b9d8655dee4a72fda`.
Le checkout valide désormais les champs selon la version en une seule
lecture JSON supplémentaire pour les anciennes versions. Le contrôle
du chemin d'archive conserve le suffixe `.glb` en minuscules.
Les 58 tests `forge_project::module_package` passent dans le checkout
Windows corrigé. Reçu `module-package-linux-009-lint-fix-windows-tests-001.log.receipt.json`
à la racine de la campagne Gate 7, SHA-256
`1529b4456dfaa8e45b4e98fafcf75d461b5fc068a5a9c09671d0c79ae10f974d`.
Le frontend a compilé, mais l'inventaire de fin de build a détecté ces
deux corrections dans le checkout commun. Il faut donc le reconstruire
sur les entrées corrigées. SHA-256 de `frontend-build/prepare-frontend-result.json`
`39b1e0306c3811a3c89c774240fa615fa3d6030d0bb82f127c6d004f4345f394`.

Le diagnostic Linux a ensuite détecté treize lints du moteur commun.
Les corrections conservent les réglages des mesures GPU, le routage
des ombres et les transitions des hôtes de scène. Clippy strict réussit
sur les sources du 009 avec les dix fichiers corrigés, tous relus par
SHA-256 dans le checkout commun. Le diagnostic utilise le cache SDK
partagé. Le candidat de distribution doit repartir d'une capture neuve.
Reçu `linux-engine-clippy-followup-004.log.receipt.json`, SHA-256
`c24aa95c482a44907ae5c57ca645543fee313032f6703e9e9a438a21c8b1d1d9`.

### 17 novembre — candidat Linux 010

Le 010 part d'une capture relue de 7 606 fichiers dont les entrées de
licence épinglées concordent avec le 009. Les 204 tests du monde, les
1 533 tests moteur exécutés et les sept tests de lancement et de
récupération réussissent. Clippy strict moteur réussit sur cette capture.
SHA-256 du journal `04-engine-clippy.log` dans le SDK :
`2d2768494278b5a3a88bc1a1cbeb3444133d6a6812d677392c20c223953f535d`.

Le frontend Studio a été compilé hors ligne sur le cache Windows commun.
Les 3 440 chemins contrôlés concordent avec la capture Linux et les
inventaires avant et après le build sont stables. Le marqueur du frontend
déposé dans le conteneur a la même empreinte que le reçu sur F:.
Reçus sous `runs/linux-reviewed-20261117-010/`.
SHA-256 de `frontend-attached.json`
`714c88a4fa94b95aa047b9f810daf172ea1485701a1aa13154206fe84302d80d`
et de `frontend-build/prepare-frontend-build-proof.json`
`c8f5573af39940d84f78c3ec281dd498b15a0bb9d9710c8297f392aa993a4a17`.

Les 92 tests exécutés du runtime exporté réussissent. Deux tests qui
demandent des entrées installées explicites sont ignorés. Clippy strict
arrête le 010 sur la taille de deux fonctions de collecte du runtime,
avant l'assemblage du kit. Le checkout divise ces fonctions en conservant
l'ordre des observations et les mêmes critères de comptage. Le diagnostic
suivant vérifie ensemble les lints du runtime, de la livraison et du
Studio natif avant de repartir d'une capture neuve.
SHA-256 de `assessment.json`
`d7c29ffd66488d8e6bb5a0573e4625bb7176444288c5f63d481f45d3e0c4047b`
et de `result/06-module-runtime-clippy.log`
`2413b9433772c18c12f5c9554854503878b90c9c5eadb6c3e932145f4dca9729`.
Le diagnostic `linux-downstream-clippy-001.log` confirme le passage du
runtime corrigé. Le CLI de livraison signalait une conversion redondante
dans la validation des étals. Les fixtures du contrat de preuve ont été
alignées. L'observation d'éclairage conserve ses champs JSON et la
livraison peut lire le contrat sans lier le moteur. Le diagnostic 003
valide Clippy strict pour le runtime et le CLI, ainsi que les 27 tests
du contrat de preuve du CLI. Le diagnostic 004 valide Clippy strict du
Studio natif après les corrections d'import et d'export. Les huit fichiers
corrigés correspondent aux empreintes relues dans le checkout commun.
SHA-256 de `linux-downstream-clippy-003.json`
`e44d3764d1830d89083b582466aa4e7a838bb3d10e94b6a043d5def6cd4b5450`
et de `linux-downstream-clippy-004.json`
`3063c1015b2e78fc5c039482869412b2ca70f07e501a4ebbc3f5816707c187ca`.
Le cache isolé du 010 a été retiré après relecture des journaux sur F:.
Reçu `isolated-target-retired.json`, SHA-256
`f68d8a3491d31a2022d43d5ccd2e9e2c75c02ec4b010591ce5c2c00a3716cf4b`.

### 17 novembre — candidat Linux 011

La capture relue contient 7 606 fichiers. Son écart avec le 010 se limite
aux huit fichiers vérifiés par les diagnostics 003/004 et à ce guide.
Le graphe des licences et l'audit Studio correspondent aux mêmes entrées.
Les 204 tests du monde, les 1 533 tests moteur exécutés et les sept tests
de lancement et récupération passent sur le 011. Clippy strict moteur
passe sur la même capture. SHA-256 SDK de `02-engine-tests.log`
`c305c3434c3988ae47bf55570f179b185a78d512d13c32d1bf4dba3dc1dd6bc5`
et de `04-engine-clippy.log`
`e1bf2d7bc7e21a8ac1a609e87b9f79984fcb0cdb642d9e915b6af66401a2128a`.
Les 92 tests exécutés du runtime livré et les 243 tests du CLI de
livraison réussissent. Clippy strict passe pour les deux composants.
La compilation Release du runtime réussit en 18 min 38 s et celle du
launcher autonome en 6 min 43 s. SHA-256 SDK de
`05-module-runtime-tests.log`
`2fc5f3d3ca37bd8cb9713535a9a629f650d423cd3c81f4c7ded976ee894fe51a`,
de `06-module-runtime-clippy.log`
`b7b662d2f97df3f378a477cfedd057d5b845a3ba2b69c1011f66bffccdcddb98`,
de `07-delivery-tests.log`
`6db8a86319e81ec05767f3e49d36fc177d083185802f57b3b1eff358ac541598`
et de `08-delivery-clippy.log`
`9d4c4f4d893f24a0d0d2bfacdf548273bda81acc79ec5241e1a0a90bd9a47b48`.
SHA-256 de `09-module-runtime-build.log`
`f5e85f7569c01f7b0f948a5ff3c45dc448e28026188a2cc1b0ddd13a0db6d000`.

Le kit joueur Linux `0.0.62-gate7.linux` a été assemblé au statut
`REDISTRIBUTABLE_STAGED_ONLY`. Il contient 464 fichiers et 189 916 796
octets, dont la revue des 463 fichiers du kit de départ, les dix archives
sources MPL et la notice de la police embarquée. Le frontend du Studio
est disponible dans le SDK, qui poursuit les contrôles et l'intégration.
L'export, l'installation et la reprise sur Ubuntu restent à vérifier.
SHA-256 SDK de `linux-game-kit-manifest.json`
`ec1f755f830e52eb26edd209e216c2c328771fb44c2031f4a6f9674a5f36bf0b`
et de `linux-game-kit-assembly-receipt.json`
`21484c9a07f9d56c4009c358fa681f929df182eb4af736b828aff2dc33c0c2fc`.
Reçus sur F: `R7-08-linux-player-kit-review-evidence.json` (SHA-256
`69bb5a69e0adc7fc814472785ae78345f2f9c30af3cc212eee7cc807575eb720`)
et `linux-kit-redistribution-review.json` (SHA-256
`227515a9e127adbcfa81a2f444c2c64b35fbb9abbef73d0ae4b14ee8b534f517`).

Les contrôles WASM du moteur et du Studio passent sur le 011. Les huit
tests du rythme GPU du frontend et les 457 tests exécutés du Studio natif
passent. Clippy strict réussit sur toutes les cibles du Studio natif.
La compilation Release du Studio réussit. SHA-256 SDK de `10-web-engine-check.log`
`33a02314801c39418eedb4134b5d1d63c0b5e3665aa5616e6f9f5f528048e114`,
de `11-studio-web-check.log`
`a96172820d6343608924b37d7edbff608728aa83b6f018343c4ccaef258726f0`
et de `12-studio-browser-tests.log`
`63c580f88c0cc3a185fb29a4be9d7982f65ac3b66c7533e09a6c216f894be53d`.
SHA-256 SDK de `13-studio-tests.log`
`e250daa8815c5bf8b9671f885c8b1a0fa98707709e389f3ba40f2c7c62e7aa88`
et de `14-studio-clippy.log`
`4125d6e5bc6980f8534e3a21a329ef9f219ee2fc5cb23a9a36385317eac5693d`.
SHA-256 du journal Release
`66f33400dc432718ab69fa94910915dce49d5ca404daad179862c0cce0a8f611`.
L'évaluation SDK se termine sur `PASS_BUILD_ONLY`.
Le paquet Studio privé assemblé compte 241 511 687 octets, SHA-256
`ed8b1b66c38899ca963a1426c16c14a26b65a9e16075c03b54d550b6f7325dd4`.
Reçu `result/linux-studio-package.json`, SHA-256
`942009923130ecd83b25910162682ea128520682acc26b81f0ea0c7703981e48`.

Le frontend du Studio a terminé sa construction sur le cache Windows
commun avec un inventaire stable. L'empreinte du marqueur reçu par le
conteneur correspond à `frontend-attached.json` sur F:.
Reçus sous `runs/linux-reviewed-20261117-011/`, SHA-256 de
`frontend-attached.json`
`95b4705741393df6935e3cae975f6007b9a9245e207a5f7827989b7dc6454c5f`
et de `frontend-build/prepare-frontend-build-proof.json`
`5d3c2b07173f76dc9c3233c869d8db424bb503bf20f90ee3d867d6ed5e831f52`.

Le disque différentiel joueur neuf occupe 197 632 octets et référence
la même base Ubuntu que l'essai joueur 008. La chaîne de stockage est
vérifiée avant démarrage. Reçu
`runs/linux-reviewed-20261117-011-preflight/player-vm-backing-admission.json`,
SHA-256 `3fd4bd91381975fba0ab9f8a8eaceede506ef1677cb4e8ed738eb839d67b19bc`.
Un différentiel auteur neuf sur la même base Ubuntu a servi à installer
le Studio et à produire l'export avant le passage joueur.
Reçu `runs/linux-reviewed-20261117-011-preflight/author-vm-backing-admission.json`,
SHA-256 `7eed5bb6be1dff75bce1a0596660f482f026da3ca62176a01486ef2444998b2c`.
Le module d'origine CC0 du Grand Plateau est réutilisé depuis la
qualification précédente. Ses 4 263 871 octets et ses identifiants
correspondent au module d'origine admis dans Ubuntu. Reçu
`runs/linux-reviewed-20261117-011-preflight/author-input-admission.json`,
SHA-256 `d69f5c2e71da439f91a45020207013555331d576f8a85ac4459d09527540e358`.

Dans l'invité auteur Ubuntu sans SDK, le Studio installé a importé le
module CC0, enregistré le Grand Plateau avec sa carte de départ,
puis exporté le jeu 1.0.0 en qualité Moyenne. Les trois empreintes
calculées sur les fichiers produits correspondent au manifeste de
candidature, qui référence le kit joueur 011 examiné.
Le paquet jeu a pour SHA-256
`c49f803393f74617490b42195278c4b18f47c743648340b51eac67f1e614f1e7`.
Reçus `studio-011-clean-ubuntu/game-export-candidate.json` et
`game-export-result.png`, SHA-256
`4e3cb19cf518916489e630b7b72465f8824f2515645a0c66a9137044c3ec2213`
et `bb2644141e2aaa031771436eca5c97415c35e385c0135adaf976dd004b7fe998`.
L'essai auteur porte sur la composition et l'export. La qualification
du viewport 3D Linux reste ouverte.

Dans un second invité Ubuntu sans Studio ni SDK, l'installateur fourni
avec cet export a vérifié les fichiers et installé le jeu. Le bouton
« Ouvrir le jeu » a lancé le launcher autonome. Une nouvelle partie a
sauvegardé avec F5, puis « Reprendre » a chargé cette partie dans un
second processus. La sauvegarde conserve son SHA-256
`6c911b001c20b12713cb44c96180e4472012d88978dd606b7f008443c2a68186`.

Le rendu joueur utilise llvmpipe. La première partie se ferme avec
projection et simulation prêtes à l'image 199. La reprise atteint
l'état prêt à l'image 32 et le conserve jusqu'à la demande de fermeture
à l'image 131. À l'image 132, la projection finale perd l'état prêt
avec la physique et le gameplay suspendus. Le motif de suspension est
`host_simulation`, sans demande de terrain en attente. Le processus
se ferme avec succès et la sauvegarde reste intacte. Reçu
`player-011-clean-ubuntu/play-resume-assessment.json`, SHA-256
`4e82537a7ba5bbb5f35697ecf47db90fa66fa31086040127677da6d1d52a9204`.

### 15 novembre — jeu du Grand Plateau avec le kit Linux 006

Le kit joueur privé Linux 006 compte 463 fichiers et 187 210 094 octets.
Le rapprochement avec le kit 005 montre 461 fichiers de licences
identiques. Les deux binaires du jeu et du launcher changent. Reçus
`linux-game-kit-assembly-receipt.json` et `R7-08-player-kit-005-006-delta.json`,
SHA-256 respectivement
`ca36807e0c2150f9ec20257f4479ffe67c504dc63d18ee8cf7c5bcd6b84cdc57` et
`461fd7ed9f5225efdb603b13b274f1391a237b719889b80377e6a3af72d64099`.
Le CLI 006 a vérifié le paquet assemblé avec le module CC0 et le manifeste
de launcher déjà épinglés du Grand Plateau. Le paquet a pour SHA-256
`30b67564007b6e70e0b48d143550c9385272c27003d8bc73890495fab487522e`.
Reçu `grand-plateau-assembly-proof.json`, SHA-256
`0cd990135b814bea826c26dfa34629ed3cee27f37b643b0f479b99eaaa2ac094`.

Sur Ubuntu sans SDK, le CLI a activé le 006 au-dessus de l'installation
005. Avec un fichier d'autorité X11 non standard, le bouton « Reprendre »
du launcher 006 a chargé la sauvegarde existante. Le runtime quitte avec
la simulation et la projection prêtes à l'image 93. La projection a été
prête pour la première fois à l'image 44. Le retour au 005 a préservé
la sauvegarde et le fichier témoin. La sauvegarde des Trois Cairns a
également été relue à son empreinte antérieure. Reçus
`grand-plateau-006-upgrade-resume-rollback.json` et
`grand-plateau-006-resume-projection.json`, SHA-256 respectivement
`4fecdb3ed243e38c863b6a4e0ac9c7cd8885ed5e7176c93e26e1cbd4ebe9a2a1` et
`f85e03298383b3cf3c5ff207f444321e575499b645d1493c10bc43f364301faa`.
Le paquet a été assemblé avec le kit du 006 et le module existant. Le
parcours depuis le Studio Linux 006 reste à exécuter.

L'invité est arrêté. Les fichiers de transfert retirés totalisent
203 617 882 octets sur l'hôte, 203 616 486 dans l'invité et 187 403 599
dans le staging Docker. La distribution SDK et les sauvegardes restent
disponibles. Reçu `grand-plateau-006-guest-finalization.json`, SHA-256
`bfc11a5739706d37998c6f888dd0ba44105c6639b7f19dbc2b2f8e8c8aca1137`.

### 10 novembre — retrait du polyfill du graphe Web

Le [backport Tachys 0.2.18](../third_party/tachys-keyed-list-NOTICE.md)
reprend le changement amont du seul appel dans la liste indexée.
Le collecteur a relu 599 identités Cargo pour `forge_studio/web`,
avec verrou, sans réseau ni dépendances de test du projet. Le nouveau
graphe ne contient plus le polyfill et collecte le fichier MIT épinglé
de Tachys. `convert_case_extras` et `erased` restent dans ce graphe.
Reçu `R7-08-tachys-source-notices-20261110.json`, SHA-256
`6fb35561493dc830b12a7f752672ebe31d240dae130df33c1e846bf8405a2d27`.
Le `cargo check` Web release verrouillé et hors ligne passe. Le prochain
paquet du Studio doit être assemblé et rapproché de son propre inventaire.

### 10 novembre — frontend Web construit après les trois corrections

Les deltas [Tachys](../third_party/tachys-keyed-list-NOTICE.md) et
[Leptos macro](../third_party/leptos-macro-case-NOTICE.md) retirent aussi
`erased` et `convert_case_extras`. Trois contrôles ciblés passent sur
les boîtes standard, dont le transfert, la destruction unique et le
refus de type. Le collecteur Web hors ligne confirme 597 identités et
relit les deux textes MIT aux empreintes amont. Le reçu
`R7-08-studio-web-notice-graph-20261110.json` a pour SHA-256
`5243359f3f4490c1642a7b4cbc81912bc230af48534170dd300195eeee282289`.

La construction Trunk release hors ligne termine avec des sources stables.
Les 103 entrées épinglées du reçu Cargo et les 3 025 empreintes du frontend
sont rapprochées. Le WASM Studio de 72 523 923 octets a pour SHA-256
`9c833446b5604a048f353707c98517d83be99848bf1c93f3874dd8af60967ff2`.
Reçu `studio-web-notice-corrections-20261110/R7-08-studio-web-notice-build-link.json`,
SHA-256 `52b2c62b6e79242f66ead08849f95217f04641bfc40f693915bdd0608114383b`.
Périmètre `PASS_BUILD_ONLY`. La preuve d'assemblage et le contrôle du Studio
installé restent à produire.

### 10 novembre — notices du candidat Windows 050

Le nouvel index couvre 727 identités sur les cinq rôles Windows et Web.
Les vérificateurs de livraison ont validé le dossier de 379 fichiers
et les deux portées Studio, sans identité Cargo dépourvue de texte
candidat. Les 65 compléments tiers réutilisés ont été rapprochés des
identités, révisions et empreintes conservées. Le texte du propriétaire
couvre les contributions originales des quatre crates Aurora/NWN
ajoutées au Studio. Les données NWN privées gardent leurs propres droits.
Reçu `studio-web-notice-corrections-20261110/notice-candidate.json`, SHA-256
`23b244b93ca2e875cfa7f563150a5f03d4254c0d7d9298d5f967808935603e7a`.
Audit Studio `studio-notice-scope-audit.json`, SHA-256
`d46b973066d8183409c7382d831665de5be1329b473e7ed87348e1e7542d9ead`.
L'index extrait du Setup 050 correspond à cet audit, avec 597 identités
pour le frontend Web et 414 pour le Studio natif. Le graphe du lot 050
exclut les trois dépendances encore en réserve au lot 049
(`convert_case_extras`, `erased` et `drain_filter_polyfill`). Les
notices de Tachys et Leptos macro figurent dans la liste des licences
du paquet. Reçu `R7-08-studio-050-web-reserves-packaged-scope.json`,
SHA-256 `9d74ab881e87beade76841239ca1e4f56a8f020e4e511e22dc2884fef750a885`.

Cette couverture d'inventaire prépare l'assemblage privé. Les droits
applicables aux composants et médias distribués restent à qualifier.

L'installateur 050 a ensuite été assemblé avec le runtime et le launcher
du même inventaire. Son SHA-256 est
`c2468e4ecef083753ce13bc4efbe934ebd1d621c51a047d76db99f6d34df8c21`.
Dans un nouveau Sandbox Windows 11 hors ligne, sans Studio ni SDK,
l'installation silencieuse s'est terminée avec le code 0. La vérification
native relit la révision active. Les quatre index installés, le Studio
et le CLI correspondent aux empreintes prévues. La fenêtre du Studio
et ses processus WebView2 ont été observés. L'invité s'est arrêté après
écriture du reçu. Ce contrôle cible l'installation, le démarrage et les
notices. Reçu `R7-08-windows-studio-notices-050-assessment.json`,
SHA-256 `54fa01a94264848e98a6082117af3cf481157dbee72a0e22bf228de397f430ce`.
Les intermédiaires d'assemblage vérifiés ont été retirés d'E: pour
360 295 793 octets et les deux copies identiques du manifeste de
sources de F: pour 4 036 970 octets. L'installateur 050, le kit actif,
les entrées de notices et le manifeste canonique servent à la suite.

### 10 novembre — Grand Plateau exporté, installé et repris avec le 050

Dans un invité Windows hors ligne, le Studio 050 installé a configuré
et sauvegardé Le Grand Plateau à partir d'une copie du workspace 038,
puis créé son export privé depuis l'interface. La source hôte conserve
son empreinte. Les quatre fichiers exportés, soit 166 474 815 octets,
ont été relus, y compris tous les payloads et les quatre index de
notices du jeu. Reçu `editor-export/G7-D6-studio-export-050.json`,
SHA-256 `53881591c78793911f5ad902a966aef87556919c0e24530081a32ce5ad16ae79`.

Un second invité Windows 11 hors ligne avec vGPU, sans Studio, workspace
auteur ni SDK, a installé le jeu via son assistant. Le launcher
installé a démarré une nouvelle partie. F5 a créé une sauvegarde,
puis « Reprendre » a rouvert la scène dans un nouveau processus.
Les deux rapports déclarent la projection prête et la sortie réussie.
Ils retrouvent le même SHA-256 de sauvegarde et les mêmes empreintes
d'état et de source. Le moteur, le launcher et les quatre index
installés correspondent au kit 050. Le contrôle des fichiers installés
a utilisé les chemins exacts de la révision active après un échec de
l'inventaire récursif PowerShell sur un chemin long.

Reçu `player-only/G7-D6-clean-windows-grand-plateau-050.json`, SHA-256
`4283c163b0649f57e3874b163d8de48676a74cab8758d9f1331a1ed1d48fe3fa`.
Les deux invités sont arrêtés. L'export privé reste sur E: pour le
rapprochement des droits et l'essai Linux. La preuve 050 couvre un jeu
et une installation neuve. Le cycle de maintenance et les deux jeux
restent documentés dans le périmètre du 049.

Le contrôle R7-08 du Grand Plateau 050 a relu les 420 entrées du paquet
application et les 25 entrées du module, sans extraction. Les quatre
index de notices correspondent au Studio 050. Le catalogue embarque les
preuves déclarées CC0 des deux maillages procéduraux et leurs recettes.
Les manifestes de l'application, du module et de son origine déclarent
`private_local_only`. Les droits du jeu complet, dont le terrain source
et les dépendances livrées, restent à établir. Reçu
`editor-export/R7-08-grand-plateau-050-embedded-rights.json`, SHA-256
`dc78d89cb713d72aa6cdd717c7e1565b496ee121c4de47a29f3be227be40daba`.

Le raster de 4 Mio correspond exactement au générateur du Grand Plateau.
La provenance embarquée exclut les contenus historiques de ce module.
La police Fira Mono figure à l'octet près dans le runtime 050 du jeu,
avec le texte OFL-1.1 épinglé dans le paquet. Le reçu
`editor-export/R7-08-grand-plateau-050-raster-font-provenance.json`, SHA-256
`cd27c7fe3591b9f513247382d51b7bddfc240d692e0ebfab815f58cb27983ee8`,
borne le contrôle aux octets et notices vérifiés. Le jeu reste privé.

Le paquet application 050 référence la notice Fira Mono dans sa liste
de licences et dans l'index des assets. Le fichier livré correspond à
la notice amont épinglée, avec le copyright et le texte OFL-1.1. Les
octets intacts de la police sont intégrés au runtime. Ces preuves
couvrent la présence de la notice exigée pour la police dans ce paquet.
[Texte officiel OFL-1.1](https://openfontlicense.org/open-font-license-official-text/).
Reçu `editor-export/R7-08-grand-plateau-050-fira-mono-notice.json`,
SHA-256 `a6168cc8739309f76c0fa61293796ed6a5badecb4fc18d58ce913023e65c2d68`.

La relecture des 25 entrées du module 050 lie le raster généré à la
zone et confirme que les deux GLB contiennent leur géométrie sans
textures ni buffers externes. La déclaration CC0 embarquée vise les
modèles et leurs recettes. Le raster et les documents de gameplay
restent couverts par la déclaration d'usage privé du module en
attendant une autorisation explicite de redistribution.
Reçu `editor-export/R7-08-grand-plateau-050-content-scope.json`,
SHA-256 `4bbd6d7ce1a7e5cc6cfafb9cc43f6302739b57d6d5f59ea03d4df286b5a299e9`.

Le transport 050 conserve une origine importée privée. Le service
d'export refuse de publier un tel module en changeant la seule licence
du nouvel export. Le 14 novembre, le propriétaire autorise CC0-1.0
pour le terrain et les documents de gameplay originaux du Grand
Plateau dans le prochain candidat Gate 7.

Le module source 1.0.1 a été régénéré à partir de la fixture
`build_regression_module --streamed`, avec la déclaration CC0-1.0 dans
ses licences. Le CLI de livraison a vérifié la construction et la
relecture de l'archive avec `--publish`. Le paquet source est
`E:/OpenRPG_Forge/.build/qualification-data/gate7/grand-plateau-cc0-20261114/grand-plateau-cc0.orpmod`,
SHA-256 `9ca7174c4b89055111dd85465fdb09be84b9e0b35e9a779200271f756539d071`.
Le reçu `editor-export/R7-08-grand-plateau-original-cc0-20261114.json`
porte le SHA-256 `3d57101384309f3e5b7fdc44ed45b509a625ba589c05f75875903dec6d15db35`.
Le même binaire Studio que dans le 050 a importé le module dans un
workspace vide sur l'hôte Windows. Le projet a été sauvegardé et la
carte ouverte. L'option de redistribution de l'export de module a
produit une archive de 4 277 011 octets, SHA-256
`ab7535149a8b12dc8c2375c0418f98a89a7b241204dcd4169601389b6ff044f1`.
Le CLI du 050 a relu l'archive avec `orpmod-inspect --publish`.
Reçu `editor-export/R7-08-grand-plateau-cc0-studio-roundtrip-20261114.json`,
SHA-256 `66691b908dd82a390a5607af471668f63fa9e96e046fa8ce43f099c6d9210717`.
La relecture intégrale des deux transports confirme l'identité de
l'archive de zone, des données de gameplay, du raster et des deux
modèles. Les deux notices d'origine sont présentes aux mêmes
empreintes dans l'export Studio.
Reçu `editor-export/R7-08-grand-plateau-cc0-content-continuity-20261114.json`,
SHA-256 `2fa7d48e8a2fa47564c00744f84d30f171f4b5042187dae89cfe485e14f3f4de`.
La fabrique `build_regression_module --streamed-cc0` reproduit désormais
ce module source depuis le checkout. Le manifeste décodé et tous les
fichiers déclarés sont identiques à la source du premier réexport Studio.
Le nouvel emballage utilise la sérialisation compacte du manifeste, d'où
une empreinte de transport distincte. Le CLI relit cet emballage avec
`orpmod-inspect --publish`. Le mode `--streamed` reproduit le paquet privé
à l'octet près et le même contrôle refuse sa publication.
Reçu `editor-export/R7-08-grand-plateau-cc0-generator-20261114.json`,
SHA-256 `f1334ebe1de2ee18719f6fb0bd06b249981b4f778216bc776563c937e18ef43b`.
L'export de jeu autonome utilise encore le mode privé du kit de
qualification. Les droits des binaires et composants tiers suivent
leurs propres preuves.

Un nouveau Sandbox hors ligne a révélé une inscription WebView2
incohérente. Le Studio 050 signale que le runtime est introuvable après
un code d'installation 0. Le journal confirme que le bootstrap a
retenu la version enregistrée 157.0.4341.42, alors que le seul
exécutable WebView2 relevé était dans le dossier 157.0.4341.46.
L'exécution séparée de l'installeur Microsoft épinglé et signé s'est
terminée avec le code `0x8004090D` et le décalage persistait.
Le script NSIS vérifie désormais la présence de l'exécutable de la
version enregistrée dans chaque périmètre, avant et après la tentative
d'installation WebView2. Les huit tests de la suite d'assemblage Studio,
dont la compilation NSIS, passent. L'essai hors ligne avec l'installateur
réassemblé à partir des composants 050, tous vérifiés par SHA-256, a détecté
la version obsolète et tenté l'installation du runtime signé. Microsoft a
renvoyé `0x8004090D`. Le programme Forge a renvoyé 1 et le dossier
d'installation du Studio est resté absent. Ce résultat valide le refus
explicite dans ce cas. Le démarrage après installation hors ligne de
WebView2 sur un Windows où il est absent reste à qualifier.
Reçu `setup/R7-10-webview2-stale-registration-20261114.json`,
SHA-256 `0adbdd4b15d40c6f956f6aba98efe4c79778cada1670032ea777e594dbb7e369`.

Le même scénario a été rejoué avec l'Evergreen x64 obtenu directement
chez Microsoft le 14 novembre, avec rapprochement des conditions affichées
au téléchargement, SHA-256 et signature Authenticode valide. Le Sandbox
présentait le même décalage entre registre et fichiers avant l'essai.
Le programme Microsoft a renvoyé `0x80040902`. Le décalage est resté
présent et Forge a refusé l'installation, sans créer de dossier Studio.
L'origine et les conditions affichées sont documentées pour ce nouveau
fichier. L'installation d'Evergreen sur un Windows qui en est dépourvu
et l'accord des utilisateurs restent à qualifier.
Reçu `setup/R7-10-webview2-current-evergreen-sandbox-20261114.json`,
SHA-256 `ba394c9aa54158f8c5769806adceb66e7db0c70cf6edb7f5838c702750c02769`.

Le pipeline Windows accepte désormais `--external-webview2`. Ce profil
du Setup vérifie le runtime Evergreen enregistré avant d'extraire les
paquets Forge et affiche le lien Microsoft si le prérequis est absent
ou inutilisable. Le profil avec prérequis embarqué reste disponible.
Les neuf tests ciblés passent, dont la compilation NSIS des deux profils.
Le Setup de diagnostic a réutilisé les trois composants épinglés du 050.
Son inventaire confirme l'absence de l'installeur Microsoft et sa taille
est de 231 976 835 octets. Sur Windows Sandbox hors ligne avec le registre
WebView2 en décalage, il affiche le prérequis dans le journal et renvoie 1.
Le runtime et son inscription restent identiques et le dossier Studio
n'est pas créé. Le parcours d'installation et d'export avec ce profil
reste à valider sur un Windows dont WebView2 est fonctionnel.
Reçus `setup/R7-08-webview2-external-050-build.json` et
`setup/R7-10-webview2-external-050-sandbox.json`, SHA-256
`cc3733bd025b301df84ca6b856db766a9aa8026a0dec12cdde666ac56533b5a8` et
`f9bde0dc65626be367f4447b12ab726bf11ea157cc140ad2954caf8303604a99`.

Dans deux nouveaux invités hors ligne, l'inscription WebView2 a été alignée
sur le binaire Microsoft signé déjà présent dans l'invité. Le Setup externe
050 termine avec le code 0. Le contrôle natif `verify` confirme l'installation
et l'empreinte du CLI livré correspond au 050. Le CLI de lancement annonce
un PID, mais la fenêtre Studio et ses processus WebView2 associés n'ont pas
été observés ensemble. Verdict d'installation uniquement. Le démarrage et
l'export restent ouverts.
Reçu `setup/R7-10-webview2-external-050-install-assessment.json`, SHA-256
`9498eb67f5efbc5bbed5e6f6b75af438f5faabe5346c90b6e3990d02ad80ccc1`.
Un troisième invité précise le résultat du démarrage.
Le PID annoncé par la CLI est resté actif avec une fenêtre intitulée
`Error` pendant 23 observations. Aucun processus WebView2 associé n'a été
observé. Corriger l'inscription vers l'exécutable signé suffit pour le
contrôle du Setup, puis le démarrage exige encore un runtime fonctionnel.
Reçu `setup/R7-10-webview2-external-050-studio-pid.json`, SHA-256
`3d34202ee86a729700d84227d562a860eb857da477a149a537e0dd4e4a12700b`.

Le dialogue du Studio 050 indique que WebView2 reste introuvable pour le
compte de l'invité. Le Setup appelle désormais le même chargeur Microsoft
que Studio, avec un environnement sans surcharges `WEBVIEW2_*`, comme le
lancement installé. Dans le profil externe, ce contrôle précède
l'extraction du paquet Studio. Le profil embarqué le rejoue après la
tentative d'installation Microsoft. Les neuf tests du Setup et le build
du CLI Windows passent. Sur l'hôte, le chargeur renvoie `157.0.4341.46`.
Une surcharge pointant vers un dossier absent provoque `0x80070002` dans
le sous-processus de contrôle brut. La commande utilisée par Setup retrouve
la même version qu'avec l'environnement normal. Le Setup final reste à
rejouer dans un invité propre et à qualifier jusqu'à l'export.
Reçu `setup/R7-10-webview2-loader-final-host.json`, SHA-256
`e99f7a881b31d6e50adda5cfc0c1cf4124894827e597d231808bab81c7c9ce02`.

Le build Windows Release du CLI passe avec des entrées stables. Le chargeur
renvoie aussi `157.0.4341.46` sur l'hôte pour ce binaire. Les graphes Cargo
verrouillés passent de 70 à 84 identités pour le CLI et de 458 à 468 pour le
moteur distribué, avec les mêmes 727 identités et les mêmes textes de base
dans l'inventaire global. Les notices et leurs reçus sont recalculés pour
ces périmètres. Le Setup de diagnostic intègre le CLI Release et les deux
entrées épinglées du Studio 050. La relecture des trois entrées confirme
leurs SHA-256 et l'absence de l'installeur Microsoft dans le Setup. Dans
un nouveau Sandbox hors ligne, l'inscription a été alignée sur le binaire
Microsoft signé déjà présent. Le chargeur utilisé par le Setup Release
renvoie `0x80070002`. Le Setup quitte avec le code 1 avant de créer le
dossier Studio et indique comment obtenir le prérequis Microsoft.
Reçu `notice-webview2-loader-20261114/R7-10-webview2-loader-release-050-sandbox.json`,
SHA-256 `1ae5e36607c9363985f1ea01369c37a06507ed20bd22d76a8217fe5be72651c5`.
Le frontend Studio a également terminé son build hors ligne sur le checkout
commun avec 3 025 fichiers relus et des entrées de compilation stables.
Reçu `notice-webview2-loader-20261114/prepare-frontend-build-proof.json`,
SHA-256 `72514c05d1d737c7cc34f90e7e6fd4acb8554c5a8658e3195aabb9e7572c68b3`.
La construction du 051 reprend le pipeline complet avec l'inventaire qui
épingle également la notice du chargeur WebView2 dans le CLI.
Reçus `notice-webview2-loader-20261114/notice-candidate.json`,
`delivery-cli-build-proof.json` et `R7-10-webview2-loader-release-050-replay-build.json`
dans le même dossier, SHA-256
`d091b1e1393c2c1a526779b05e2d712c9ced96e0702e148f22bc6d588670a35a`,
`a4584b826e27918c9bf9e5e63302bc19b6141d91fb52eedf4b16104f232572d5` et
`f1cf0ab6f179ce0c6ef86db84c2202629b443f704e18bcb2fda33303ec3022b3`.

Le 051 a terminé les builds frontend, CLI, launcher, moteur et Studio,
ainsi que l'assemblage du kit et du paquet privé. Deux fichiers du lot de
conversion (`scripts/assets/retire_exact_unity_overlays.py` et son test)
ont changé pendant l'assemblage NSIS. Le contrôle final du checkout a
refusé de livrer le Setup. La relecture indépendante du fichier
intermédiaire rapproche les trois entrées avec l'assemblage privé, confirme
l'absence du programme d'installation Microsoft et retrouve la notice du
projet et le texte BSD du SDK WebView2 aux empreintes attendues. Ce Setup
sert au diagnostic Windows. Le candidat publiable reste à assembler avec
un inventaire de sources stable.
Reçus `notice-webview2-loader-20261114/next-candidate/setup/studio-installer-result.json`
et `R7-08-studio-051-installer-inventory.json` dans le même dossier,
SHA-256 `f225aed4cb89f8dc07a6548300bc6fcb8000821c45f873e8601a8f4779726e68`
et `47d18798557b82234893cf81802e7bae024ce9f5c10697d507178d69e7bf8514`.
Le script d'ouverture automatique du Sandbox 051 n'a pas écrit son reçu.
Le rejeu explicite via la CLI officielle de Windows Sandbox a vérifié le
SHA-256 du Setup, le code de sortie 1 et l'absence du dossier Studio.
Le journal indique que l'inscription WebView2 `157.0.4341.42` pointe vers
un exécutable absent. Le résultat couvre le refus du prérequis. Les
verdicts d'installation, de démarrage et d'export restent ouverts.
Reçu `next-candidate/setup/clean-windows/studio-notice-smoke-051-explicit.json`
sous `notice-webview2-loader-20261114`, SHA-256
`c885e159116844fe985fbcd7c457bee12c79a75d57510d8a03128efb13e90f6`.

Sur l'hôte Windows doté de WebView2 `157.0.4341.46`, le CLI du 051 a
appliqué et vérifié le paquet épinglé par son inventaire dans une racine
de qualification neuve. Le lancement normal par le CLI a ouvert
« OpenRPG Forge Studio » avec un processus WebView2 enfant. Le binaire
observé correspond au reçu de build. Ce contrôle utilise le profil
WebView2 de l'hôte et couvre le paquet et le lancement CLI sur cet hôte.
Le script hôte qui attendait la sortie standard du CLI n'a rendu la
main qu'à la fermeture du Studio. Le reçu ci-dessous provient de
l'observation indépendante du PID pendant que la fenêtre était ouverte.
Reçu `next-candidate/setup/R7-10-studio-051-windows-host-start-observed.json`
sous `notice-webview2-loader-20261114`, SHA-256
`ec4f8b78db3211ae0749ff45e501c381ec239eefc349c5b433f898cb413670fc`.

Le manifeste du 052 garde les deux scripts de retrait de doublons dans
l'inventaire du checkout. Le contrôle de stabilité du build les classe
comme scripts de conversion hors produit. Le garde-fou rejette une
référence directe du produit vers ces scripts et le lecteur de stockage
partagé `vault_storage.py` figure toujours dans les entrées du build.
NSIS prépare l'exécutable sur le volume de destination avant de le renommer.
Les journaux et reçus peuvent ainsi rester sur F: avec le Setup sur E:.
Le test de séparation des entrées et les neuf tests de l'installateur passent.
Un assemblage de contrôle avec le vrai compilateur NSIS a produit un
exécutable sur E: dont le SHA-256 correspond au reçu écrit sur F:.
Le Setup externe 052 est assemblé sur E: et son reçu de build est sur F:.
La relecture indépendante a comparé les trois entrées NSIS au paquet
privé, confirmé l'absence de l'installeur Evergreen et vérifié les deux
textes WebView2 aux empreintes du checkout. Le Setup pèse 232 083 451 octets.
SHA-256 du Setup `e0db6a135592f2e832bc7e4700c8c2ef4880ee000cdaf70e8527df73b7649415`.
Reçu `notice-webview2-loader-20261114/candidate-052/setup/R7-08-studio-052-installer-inventory.json`,
SHA-256 `f6907ba4be7959838d8716f80c381739ea1a722b20e979dc00093f017fe9a84a`.
Dans un Windows 11 Entreprise 26100 hors ligne avec vGPU, sans Studio,
SDK ni outils de compilation, le Setup épinglé quitte avec le code 1.
Le journal identifie l'exécutable absent pour la version WebView2
enregistrée, puis indique le téléchargement officiel. Aucun dossier
Studio n'est créé. La CLI de Windows Sandbox a exécuté le contrôle
et libéré l'invité après vérification du reçu.
Reçu `notice-webview2-loader-20261114/candidate-052/setup/clean-windows/studio-notice-smoke-052.json`,
SHA-256 `ba064c7b99708dddba48cfa6ebcc11d8a40176c3789833448b897825f06714f9`.

Sur l'hôte Windows, le CLI a appliqué puis vérifié le paquet exact du
052 dans une racine de qualification neuve. Les deux textes de licence
installés correspondent à l'inventaire. Le CLI a lancé le binaire épinglé
du Studio, qui a ouvert sa fenêtre avec un processus WebView2 associé
(`157.0.4341.46`). Le processus de qualification est fermé. Cet essai a
utilisé le profil WebView2 de l'hôte et le CLI de livraison. L'installation
par Setup et l'export sur Windows propre restent ouverts. L'administration
Hyper-V refuse actuellement l'accès à la VM `Forge-Gate7-Windows` depuis
la session courante.
Reçu `notice-webview2-loader-20261114/candidate-052/setup/R7-10-studio-052-windows-host-cli.json`,
SHA-256 `af7af75e9d37b074b0121880425ee52d64a966b46d8df7209cb4925a9d50c928`.

Dans le Studio 052 installé sur l'hôte, l'interface a importé le module
CC0 du Grand Plateau, sauvegardé la configuration du jeu et créé son
export privé. Les quatre fichiers totalisent 166 654 286 octets. La
relecture intégrale des deux paquets vérifie les entrées, ainsi que
les empreintes du launcher, du moteur et des notices du kit 052.
Le module CC0 source conserve son empreinte. Le parcours auteur sur
Windows propre reste ouvert pour ce candidat.
Reçu `notice-webview2-loader-20261114/candidate-052/setup/R7-10-studio-052-windows-host-export.json`,
SHA-256 `5db10b28cae7bf5e4fd30101f1a6be64793b863ff7d509aa4363b79a92dce174`.
Sur l'hôte, l'exécutable `Installer.exe` de cet export a ouvert sa
WebView, chargé ses styles et répondu à la lecture d'état par IPC.
La sonde `--verify-webview` renvoie `pass: false` car elle attend un
bouton absent de la vue d'installation. Elle n'a appliqué aucun plan
d'installation et n'a démarré aucune partie.
Reçu `candidate-052/setup/R7-06-grand-plateau-052-host-installer-webview.json`,
SHA-256 `437238da88d063413fbfcbb0f03ba80a00696eb0a71f1991951226475488c4cc`.

Dans un Windows 11 hors ligne avec vGPU, sans Studio, espace auteur ni
SDK, le CLI a vérifié les quatre fichiers de l'export Grand Plateau 052
et installé le jeu. Le moteur installé s'est ouvert. F5 a enregistré
la partie et F9 a créé la demande de reprise. Le second processus a
utilisé le plan du CLI avec l'empreinte de cette sauvegarde. Il a
consommé la demande et affiché « Sauvegarde reprise ». Les deux rapports
confirment le même hash de sauvegarde et le même état sauvegardé, avec une
sortie normale. Les interfaces d'installation et de reprise du launcher
restent à qualifier sur ce candidat. L'invité a été fermé après collecte.
Reçu `candidate-052/player-cli/player-verified-final.json`, SHA-256
`0065b722c29364261b2957ad3b0608eb08f2d55c0ad28bbee89695bac3bbd3d1`.

Sur l'hôte Windows, l'installateur de l'export exact du Grand Plateau 052
a vérifié une racine neuve, installé le jeu et créé `Jouer.exe`. Le bouton
« Ouvrir le jeu » a démarré le launcher installé. « Jouer » a lancé le
moteur et F5 a créé une sauvegarde dans le profil de qualification isolé.
Après fermeture du jeu, « Reprendre » dans le launcher a démarré un second
processus. Les deux rapports de sortie normale donnent les mêmes empreintes
de sauvegarde et d'état. Le second confirme « Sauvegarde reprise » et la
demande n'est plus présente. Le CLI a servi à relire l'installation après
le parcours. Le parcours via les interfaces reste à rejouer sur Windows propre.
Reçu `notice-webview2-loader-20261114/candidate-052/player-ui/R7-06-grand-plateau-052-windows-host-player-ui.json`,
SHA-256 `a7329764b2c25e6e27343301335d7f7508e2afea2cce3337ef8ee1bcc3044f93`.

Pour R7-08, les quatre PDB du 052 correspondent aux GUID et âges
CodeView du Studio, du CLI, du launcher et du moteur du kit. Les
enregistrements de liaison confirment les objets de `libcmt.lib`,
`libvcruntime.lib` et `libucrt.lib` dans les quatre exécutables. Le
contrôle rapproche les octets des exécutables des reçus de build,
du manifeste du paquet et du reçu du kit. Il précise les composants
auxquels doivent s'appliquer les droits du distributeur.
Reçu `notice-webview2-loader-20261114/candidate-052/setup/R7-08-windows-static-crt-linked-modules-052.json`,
SHA-256 `c4b34efc42d1badd3f81357cbc550a02f625fa780845e540a7f9eee6b5fa7498`.

Dans un nouvel invité de qualification avec réseau, le contrôle natif
du CLI 052 retourne `0x80070002`. L'installateur Evergreen x64 signé
par Microsoft retourne `0x80040902`, depuis le dossier partagé puis
depuis le disque local de l'invité. Le journal Microsoft précise que
son programme n'a pas pu créer un dossier temporaire. Le chargeur
retourne encore `0x80070002` après les deux tentatives. Ce résultat
concerne l'installation du prérequis dans cet invité.
Reçus sous `notice-webview2-loader-20261114/candidate-052/webview2-provisioning/` :
`webview2-provisioning.json`, `webview2-provisioning-local.json` et
`microsoft-installer-errors.json`, SHA-256
`1260b55b4db48b47afca2ebafbff7cf41146d795341128bf1785762f5ae3a785`,
`ecb974a1df917fdd3dfa7150a5fbd24acca0fb2c492d86ebe7c0c6a02fbcd5d2` et
`fb5f9f45621df7635c4ff46849999cf9515201ebd047870209d1095fbfdb6596`.

Un nouvel invité hors ligne reproduit `0x80040902` avec l'installateur
Evergreen signé, lancé sous `SYSTEM` depuis un dossier temporaire local.
L'invité contient l'exécutable Microsoft signé `157.0.4341.46` alors
que le registre déclare `157.0.4341.42`. Dans l'invité jetable, la mise
en cohérence de cette valeur laisse le chargeur du 052 retourner
`0x80070002` dans la session utilisateur. Le Setup n'a pas été relancé
dans cet invité et le Sandbox a été arrêté. La validation positive reste
à effectuer sur un Windows sans SDK où Evergreen fonctionne déjà.
Reçus sous `notice-webview2-loader-20261114/candidate-052/setup/clean-windows-positive/` :
`evergreen-system.json`, `guest-webview2-registration.json` et
`loader-after-alignment.txt`, SHA-256
`419571c1c95fe9e3d9c87f4c2aac3417d3e4ddc34e19b13e1706737450e170ea`,
`cd0530dea486f7719dce4e45a05cd0208a076d97186dfc673a568327ad41650d` et
`b63a94b866ac239d77ddf5869ef25fbda967ad78350a8e70d997442453392768`.

Le PDB du CLI Release correspond au GUID et à l'âge CodeView du binaire
inclus dans ce Setup. Ses enregistrements d'édition des liens identifient
six objets WebView2 et des objets de `libcmt.lib`, `libvcruntime.lib` et
`libucrt.lib`. La licence BSD 3-Clause épinglée pour le SDK s'applique ainsi
aussi au chargeur du CLI. Le Setup de diagnostic réutilise le paquet Studio
050 historique. L'inclusion des notices actualisées dans un nouveau paquet
et les droits de redistribution du CRT restent à établir.
Reçu `notice-webview2-loader-20261114/R7-08-windows-release-cli-webview2-linkage.json`,
SHA-256 `d1ade248ff1847b5c092ab565878eaf1279e517c000fe49807fe304ad723edf2`.
La préparation des notices reconnaît le CLI dans le texte du projet et
retrouve la licence SDK au chemin prévu dans le paquet. Reçu
`notice-webview2-loader-20261114/R7-08-webview2-cli-license-staging.json`,
SHA-256 `4687f7b7b0482b84208ea1cd003376124960ab6a4f4953593d09053a3908006c`.

Pour Tidewater 049, les quatre identifiants et empreintes du catalogue
embarqué correspondent à la réimportation gouvernée. Les quatre
assemblages comportent des retouches et la filiation couvre 52
fichiers du pack Quaternius. Leurs déclarations privées restent
applicables. Une autorisation pour le Grand Plateau ne résout pas
les droits des dérivés Tidewater.

Le paquet du Grand Plateau 050 fournit les sources des neuf crates
Symphonia sous MPL-2.0. Les archives correspondent aux sommes de
contrôle du `Cargo.lock` du candidat. Les licences livrées sont
identiques à celles des archives et la notice indique les chemins
locaux des sources. Les 22 entrées de cet ensemble figurent dans la
liste des licences du paquet.
Reçu `editor-export/R7-08-grand-plateau-050-native-media-sources.json`,
SHA-256 `ccbb0a5bd299bd76fea01f5f374a56184c06a03ac810c5fc999a567a6120339a`.

La relecture du Grand Plateau 052 installé par l'interface confirme que
ces 22 fichiers figurent toujours dans la liste de licences et concordent
avec le manifeste installé. Les neuf archives Symphonia et leurs textes
de licence conservent les empreintes du 050. Le reçu épingle le paquet
exporté, les binaires effectivement lancés et le manifeste installé.
Le droit du paquet reste `private_local_only`.
Reçu `notice-webview2-loader-20261114/candidate-052/player-ui/R7-08-grand-plateau-052-native-media-readback.json`,
SHA-256 `ed7cd564a812cc3944e6c26ede3f58be98969598ac26054bdc0dca00886414f8`.
Le module installé conserve aussi le manifeste d'origine redistribuable
et le texte CC0 du terrain et des documents de gameplay autorisés. Le
contrôle rapproche le module d'origine du reçu propriétaire et confirme
que le module de transport 052 garde sa portée privée.
Reçu `notice-webview2-loader-20261114/candidate-052/player-ui/R7-08-grand-plateau-052-cc0-origin-readback.json`,
SHA-256 `2d3286f451b3ade3301eafbc79d32841ac69da2bbdc1c3219b3ee7c8d57a8419`.

La relecture des quatre index installés du jeu 052 confirme les liens
entre index, le référencement des fichiers dans le manifeste et les
empreintes des 375 textes distincts. Les requêtes Cargo Windows des
rôles moteur et launcher recensent 468 et 298 identités couvertes.
Ces requêtes incluent la fermeture conservative de construction et
de dépendances normales. Les droits de redistribution restent ouverts.
Reçu `notice-webview2-loader-20261114/candidate-052/player-ui/R7-08-grand-plateau-052-installed-notice-indices.json`,
SHA-256 `a73e27aa28bb05152ce440dc9f6224077435c53c4479fab12f1262a90cb36e1a`.

Les index embarqués couvrent les 458 identités non-dev du runtime et les
298 du launcher, avec 375 chemins de textes distincts vérifiés dans le
paquet. Dans le graphe Cargo des dépendances normales sans macros
procédurales, 20 des 27 notices complémentaires du runtime et 8 des 11
du launcher restent à examiner en priorité. Les autres notices restent
embarquées, notamment pour couvrir les contributions possibles du code
généré. Les droits de redistribution restent ouverts.

Reçus `editor-export/R7-08-grand-plateau-050-role-notices.json`
(`7f904ce4c61f02f5a58b8cc03f9b814d139cb9d0abd4cdcb0c27e89b859b7229`)
et `editor-export/R7-08-grand-plateau-050-cargo-role-boundary.json`
(`28fa39b36a7bfce30c9647149ee932742b2e7a631c3805b7019c29c9d9ef0b76`).

Les 38 entrées complémentaires par rôle ont été rapprochées de leurs
archives Cargo verrouillées, de leurs déclarations de licence, de leurs
commits de publication et des textes présents dans le paquet privé 050.
Les 27 entrées du moteur et les 11 du launcher conservent leurs notices.
Pour `selectors 0.36.1` sous MPL-2.0, les graphes Windows du Studio et du
launcher sans dépendances de build ni macros procédurales ne contiennent
pas la crate. Le graphe de construction la rattache à Tauri via
`dom_query`. Les sources verrouillées de Tauri montrent l'usage des
sélecteurs pour repérer les scripts dans le HTML à la compilation et
calculer des empreintes CSP. La portée des sorties générées et le
graphe Linux restent à examiner.
Reçus `editor-export/R7-08-grand-plateau-050-supplement-source-review.json`
et `editor-export/R7-08-selectors-windows-build-boundary-20261114.json`,
SHA-256 `9c6065c2ec059a3dbe9f69ca1ed21eb5921096ed2cc108c798bc7707471b4e65`
et `84696afcc3922f48656278205628eb563ff62bd1902865c11972012a11e06dc6`.

### 10 novembre — cadence appariée 049/050 et référence Tidewater 012

Les deux runtimes livrés ont rejoué le même export Tidewater et son
trajet de 40,8 m, en ordre 049 → 050 → 050 → 049. Les quatre parcours
conservent les mêmes identités, la RX 7800 XT avec Vulkan, le profil
Moyen et le mode FIFO demandé en 1280 × 800. Les bornes du trajet
diffèrent au plus de 5 cm. Toutes les images retenues étaient prêtes.
Le 049 mesure 22,34 et 22,71 ms en moyenne, contre 17,30 et 17,42 ms
pour le 050. Le p95 passe de 30,38–31,40 ms à 26,42–27,45 ms.
Ces intervalles de boucle incluent l'ordonnancement et les diagnostics.
La cible moyenne de 16,67 ms reste à atteindre. Reçu
`tidewater-performance/G7-D6-tidewater-049-050-paired.json`, SHA-256
`9703cf24eb9804a25ab7e552ff46cec394d04c293dee1e3d4d23f2ad6f6eb160`.

L'archive du kit 013 contient le véritable lecteur Tidewater 012,
SHA-256 `1d4858213e74e64ead31332a65a633e7baa1c951645616137de04b3733271d27`.
Avec l'instantané et les préférences de la recette historique, il
reproduit les identités de jeu, d'archive et de zone sur la même RX
7800 XT et le même pilote 26.10.1. Les quatre phases fixes et en
rotation sur place donnent 10,40 à 10,44 ms en moyenne et
13,84 à 15,10 ms au p95, en 2560 × 1440. Un index auteur hors
instantané diffère des entrées historiques. La résolution, les
réglages et le mouvement diffèrent de l'essai 050, ce qui laisse la
parité à qualifier. Reçu
`tidewater-performance/G7-D6-tidewater-reference-012-current-driver.json`,
SHA-256 `1b09844f61853062ab122ca320d9f546d61da5c905802670bde230ec9f762003`.
Le dossier de mesure conserve les traces à comparer sur F:. Les
1 107 521 257 octets temporaires de relecture ont été retirés d'E:
après contrôle des empreintes.

### 10 novembre — profil CPU séparé du 050

Sur le même export et le même trajet, le 050 instrumenté a terminé les
40,80 m avec 783 images prêtes sur 783, à 17,50 ms en moyenne et 27,84 ms
au p95. Quatre fenêtres de cinq secondes après le premier relevé, avec
déplacements et arrêts, donnent 15,73 à 16,10 ms par exécution de
`render.schedule` et 12,23 à 12,53 ms pour `app.main_schedule`. Dans le
rendu, `Prepare` prend 3,64 à 3,71 ms, `PrepareViews` 2,95 à 3,00 ms et
le graphe caméra 4,10 à 4,24 ms. La planification des végétaux prend
3,77 à 4,01 ms par appel, mais seulement 0,13 à 0,17 ms par boucle
principale après prise en compte de sa fréquence.

Les plages CPU se recouvrent. `PrepareViews` inclut l'acquisition de la
surface par Bevy. Les quatre relevés GPU asynchrones du graphe caméra
sont de 4,79 à 5,44 ms et datent de 10,70 à 12,48 ms au relevé. La
référence 012 confirme l'intérêt d'examiner la préparation des ressources
et le graphe caméra, avec des réglages différents. Isoler aussi l'attente
de surface avant d'attribuer le coût de `PrepareViews` au moteur.

Le reçu `tidewater-performance/050-cpu/G7-D6-tidewater-050-cpu-assessment.json`
contient les compteurs, les âges GPU et les empreintes de la trace et des
entrées. SHA-256 `4e70b68b6554eb5157f0c445bc262a5dac63fdde0a081a81d6dc523cd5810aa0`.
Les 211 668 615 octets de cache et de données d'essai sur E: ont été
retirés après vérification de la copie du journal sur F:.

Le checkout partage désormais la liste immuable des paquets d'ombres
des végétaux importés entre l'extraction et la préparation du rendu.
Le contrôle du numéro d'image et les reçus de préparation continuent à
s'appliquer à chaque image. `cargo check --locked --offline -p forge_engine
--tests` passe sous Windows.

Un build release du runtime seul a repris les mêmes entrées que le 050,
avec ces deux fichiers moteur modifiés et des écarts documentaires. Le
reçu confirme que les sources sont restées stables pendant le build.
Une paire de trajets à points de départ et d'arrivée identiques donne
17,07 ms en moyenne et 26,12 ms au p95 pour le 050, puis 17,27 ms et
27,89 ms pour le nouveau runtime. Toutes les images retenues étaient
prêtes, respectivement 803 et 794. Cette paire ne montre aucun gain et
ne suffit pas à attribuer l'écart de 0,20 ms au changement. La cible de
16,67 ms reste ouverte. Examiner ensuite la préparation des ressources et
le graphe caméra. Le reçu
`tidewater-performance/runtime-current-8ecfe753/G7-D6-tidewater-050-current-pair.json`
porte le SHA-256 `3c76ac3ad39b8a5f43e365862d0e5ec9e65f1b3739b9e6762ac33ef6e6be9536`.
Après vérification des traces sur F:, les deux caches et sauvegardes
temporaires ont été retirés d'E: pour 423 202 220 octets.

### 10 novembre — preuve de rendu des réglages personnalisés

Le runtime commun a ouvert le même export à l'arrêt avec les 20 réglages
du 012, sur une surface observée à 2560 × 1440 en FIFO. Le scénario a
terminé l'attente de 840 pas, mais aucune image n'a passé le contrôle de
projection. Le collecteur omettait la preuve graphique lorsque des
réglages personnalisés étaient actifs. La sortie a refusé l'absence de
preuve des surfaces et les temps de cet essai ne qualifient pas la cadence.
Reçu `tidewater-performance/runtime-current-8ecfe753/static-012-controls/G7-D6-static-custom-graphics-refusal.json`,
SHA-256 `039a7f9c37165692c5cd84302e01e4d47a6a72329f433f2b2bdaee2cc8c628d1`.

Le collecteur produit désormais une observation pour ces réglages. Le
runtime et le vérificateur calculent la même identité à partir des valeurs,
de la génération et de l'image d'enregistrement. Les anciennes preuves
sans personnalisation gardent leur format et leurs empreintes. Les 26
tests du contrat portable passent, dont le refus d'une preuve dont les
réglages ont changé. Le runtime release corrigé a terminé le même
scénario avec les mêmes entrées. Les 20 valeurs correspondent au relevé
du 012 et les preuves des surfaces et des inventaires GPU sont valides.

Les 634 images retenues à l'arrêt sont prêtes. La boucle instrumentée
mesure 18,92 ms en moyenne et 21,67 ms au p95 en 2560 × 1440, contre
10,42 à 10,44 ms en moyenne sur les phases fixes du 012. La pose du
lecteur historique est documentée en première personne. Les poses de
caméra et les budgets du rendu doivent encore être appariés avant
d'attribuer l'écart. Le reçu
`tidewater-performance/runtime-current-8ecfe753/custom-graphics-evidence-fix/static-012-controls/G7-D6-static-custom-graphics-replay.json`
porte le SHA-256 `3d8714c3b7d91c9b5c5a07e7d3eae8601d7b89010051d65352bd40f6c89574af`.
Le cache d'extraction a servi aux deux essais. Après vérification des
preuves sur F:, 211 691 706 octets de temporaires ont été retirés d'E:.

### 10 novembre — mesure à l'arrêt en première personne

Le même runtime release a ouvert le même export en 2560 × 1440 FIFO avec
les 20 valeurs du 012. La commande joueur V a activé la vue à la première
personne. La trace confirme un bras de caméra nul, un lacet et un tangage
nuls et la position du joueur à moins de 1 mm de l'origine archivée du 012.
Les 1 039 images stables retenues sont prêtes. La boucle mesure 16,36 ms
en moyenne et 17,81 ms au p95. Le 012 reste à 10,42–10,44 ms sur ses
phases fixes. Le lancement interactif, le préchauffage, les budgets de
rendu et la transformation exacte de la caméra limitent la comparaison.
La cadence du trajet en mouvement reste à qualifier.

Dans trois fenêtres stables du runtime commun, `Prepare` mesure
4,17–4,21 ms et le graphe caméra 5,32–5,36 ms. Les phases fixes du 012
donnent respectivement 1,18–1,20 ms et 2,20–2,27 ms. `PrepareViews` reste
à 0,24–0,26 ms dans ce relevé. Quatre échantillons GPU récents donnent
une médiane de 0,79 ms pour `bin_unpacking`, contre 0,17 ms dans les
dernières valeurs archivées du 012. Ce système traite les lots de maillages
par vue et par phase, y compris les vues de lumière. Les relevés CPU se
recouvrent et les échantillons GPU restent asynchrones. L'écart cible
l'inspection des ressources préparées et des lots de maillages.

Reçu `tidewater-performance/runtime-current-8ecfe753/custom-graphics-evidence-fix/first-person-012-controls/G7-D6-tidewater-first-person-static.json`,
SHA-256 `e6b9d63e8a55c53eaeca774c95dae1301c831b8f04747bb169ef324779208858`.
Les 211 755 533 octets temporaires ont été retirés d'E: après vérification
des traces sur F:.

### 10 novembre — préparation des maillages et vues d'ombre

Le profileur commun sépare les sous-étapes de `Prepare` et compte les
lots non vides par phase. Le trajet instrumenté de 40,80 m a terminé
avec 737 images prêtes. Quatre relevés comptent environ 171 000 entrées
d'instances dans 444 lots, réparties sur neuf vues d'ombre Bevy, contre
environ 19 100 entrées dans les deux phases du rendu direct. Les entrées
se répètent entre vues. Le rendu des ombres source est observé à part.

Le regroupement et la collecte des lots prennent 2,07–2,22 ms par image,
et les groupes de liaison 1,24–1,33 ms. Le build a compilé, mais l'ajout
concurrent de scripts d'audit a invalidé son reçu de livraison. Une autre
compilation était active pendant le trajet. Les durées sont indicatives.
L'inspection doit maintenant distinguer les vues de lumière, les casters
de végétation sans culling et les matériaux qui consomment encore ces
ombres. La végétation déformée dispose de bornes source dans le moteur.
Le branchement de ces bornes doit respecter les transformations GPU.

Reçu `tidewater-performance/runtime-current-8ecfe753/prepare-and-mesh-bin-profile/diagnostic-with-shared-build/prepare-bins-assessment.json`,
SHA-256 `094b57185fa37f88ba2ca41b90c9895622fd70a08ab0e065b144c191eb09effa`.
La copie de diagnostic, son cache et ses sauvegardes ont été retirés d'E:
après vérification des traces conservées sur F:.

Le moteur commun réutilise les enveloppes de déformation des végétaux
pour retirer des listes d'ombre des lumières ponctuelles et des spots les
plantes entièrement hors de leur portée. Les cas tangents et les entités
sans enveloppe restent dans ces listes. La déformation GPU, le rendu
principal et les cascades du soleil conservent leur chemin existant.
`cargo check --locked --offline -p forge_runtime -p forge_engine --tests`
passe sous Windows. Le gain et la parité visuelle restent à mesurer sur
le prochain runtime.

### 14 novembre — ombres locales sur le trajet Tidewater

Le runtime commun du checkout `0986a17c` a terminé le même trajet de
40,80 m que le binaire de diagnostic du 10 novembre, avec les mêmes
extrémités, le même module, le profil Moyen et Vulkan en 1280 × 800.
Les 685 images de référence et les 753 images du candidat étaient
prêtes. La moyenne passe de 20,02 à 18,19 ms, le p95 de 28,17 à
27,86 ms. La cible moyenne de 16,67 ms reste ouverte.

Sur quatre relevés après le premier échantillon, la médiane passe de
170 856 à 57 692 entrées d'instances dans les vues d'ombre. Le rendu
direct conserve environ 19 100 entrées. Le candidat contient aussi des
changements de validation et de préparation des assets. Cette paire
mesure l'écart entre les deux binaires et laisse la parité visuelle à
vérifier.

Reçu `tidewater-performance/runtime-current-1a323d8a-local-shadow-range/cached-20261114/G7-D6-tidewater-local-shadow-range-pair.json`,
SHA-256 `8e413c0a1048602efe25381a9a0448f8dc7fe53e9fcf2bae2b17200da627face`.
Le reçu de compilation a été obtenu sur cache chaud, avec le même
binaire que lors de la première compilation. La première capture des
entrées signalait l'évolution de deux scripts Python de conversion.

Les deux binaires épinglés ont capturé le point de départ en 1280 × 800,
au profil Moyen et à l'image 330, avec la projection et la simulation
prêtes. Le cadrage et les principales ombres au sol et sur les
bâtiments sont cohérents à l'inspection. Les ticks de simulation
diffèrent (231 et 210). La végétation animée et un acteur se trouvent
dans des états différents. Il reste à contrôler les ombres sur le
trajet à des états de simulation appariés.
Reçu `spawn-visual-comparison/G7-D6-tidewater-spawn-visual-pair.json`
dans le dossier du comparatif, SHA-256
`b5ed391d6b2109c4da2a535e77ddcc1f206e3fe13dec2a2ec32fd59fe5642e5e`.

Le scénario natif a ensuite capturé le départ, l'extrémité de l'aller
et le retour pour les deux binaires. Les observations les plus proches
des captures concordent aux ticks 180, 730 et 1270, avec les mêmes
positions de caméra et de joueur. Les principales ombres statiques
restent cohérentes à l'inspection des six captures. Des différences
subsistent dans la végétation animée. Cette inspection couvre trois
vues à l'arrêt et laisse ouverte la parité visuelle complète.
Reçu `route-visual-comparison/G7-D6-tidewater-route-visual-pair.json`
dans le dossier du comparatif, SHA-256
`1c3abf74f95aefc23771c4c35054d6a8f0085317deeea85aa7c440655273ad53`.

La variante qui filtre aussi les cascades du soleil a compilé et a été
rejouée. Les 776 images de déplacement sont prêtes dans chaque essai.
La médiane des entrées d'instances d'ombre baisse de 57 692 à 2 563.
La moyenne passe de 17,63 à 17,64 ms et le p95 de 26,30 à 27,19 ms.
La variante est retirée des sources actives après ce comparatif.
Le filtrage des lumières locales reste dans le moteur commun.
Reçu `tidewater-performance/runtime-directional-shadow-20261114/cached/G7-D6-tidewater-directional-shadow-pair.json`,
SHA-256 `6b1b27d41899111abbe776fe8eb73bb59da435e8c289604f94bfe7a2d7cc0626`.

Le replay visuel a produit six captures. Au point de l'aller, le
vérificateur a refusé l'égalité exacte des positions pour un écart
inférieur à 0,004 mm, alors que les ticks les plus proches concordent.
La parité visuelle complète reste ouverte.

Sur les fenêtres stables du même comparatif, `PrepareViews` prend
4,13 ms côté référence et 3,89 ms côté variante solaire. La préparation
des lots varie de 1,96 à 1,92 ms, alors que le filtrage solaire ajoute
0,65 ms dans la boucle principale. Ces durées se recouvrent. Le profileur
CPU optionnel mesure désormais séparément la préparation des fenêtres
de Bevy, y compris l'acquisition de surface. Ce relevé permettra de
distinguer l'attente de présentation du reste de `PrepareViews`.
Le contrôle de compilation du moteur et de ses cibles de test passe.
Reçu `G7-D6-directional-shadow-steady-cpu.json` dans le dossier du
comparatif solaire, SHA-256
`1fac1323aaf59bf0712db8b1e2dff8b96bcbe6e61e0dedecc9d3b5c99fb74197`.

Le relevé sur le même trajet avec le runtime commun instrumenté confirme
3,77 ms par appel dans la préparation des fenêtres pour 3,83 ms dans
`PrepareViews`, sur deux intervalles stables. En Vulkan, au profil Moyen,
en 1280 × 800 et en FIFO, les 723 images de déplacement prêtes sont à
18,92 ms en moyenne et 28,99 ms au p95. La mesure de la fenêtre comprend
l'acquisition de surface et les autres opérations de Bevy. Pour savoir
combien de temps relève de la synchronisation avec l'affichage, comparer
le même trajet en FIFO et sans synchronisation verticale, sur un même
binaire. Le seuil de 16,67 ms reste ouvert.
Reçu `tidewater-performance/runtime-window-prepare-20261114-cache/G7-D6-tidewater-window-prepare.json`,
SHA-256 `d73c99aceae50de6fe662e0319d492330a6dcc0dbb65724f8b837c1a983bf23e`.

Le comparatif FIFO / présentation immédiate sur un seul binaire a
conservé Vulkan, la RX 7800 XT, le profil Moyen, 1280 × 800 et la même
empreinte fonctionnelle. Les 692 images prêtes en FIFO mesurent
19,80 ms en moyenne et 29,67 ms au p95. Les 697 images prêtes en mode
immédiat mesurent 19,61 ms et 30,29 ms. La préparation de fenêtre passe
de 3,93 à 3,89 ms par appel. Ce relevé instrumenté ne montre pas de gain
sur lequel fonder un changement de comportement en jeu. Le mode normal
reste FIFO. Les mesures Bevy couvrent un intervalle en FIFO et deux en
mode immédiat. La prochaine piste est le coût de préparation et de
soumission du rendu commun, en gardant les mêmes options de qualité.
Reçu `tidewater-performance/runtime-present-mode-20261114-cache/G7-D6-tidewater-presentation-pair.json`,
SHA-256 `b0fb8ca9e8caebf47d1e4a2ec5e3d856f12f8e7ecaebdfd81c2caded84df6c12`.

La relecture des deux journaux épinglés donne un total de 1,47 ms en
FIFO et 1,62 ms en mode immédiat par image pour les deux gardes de
végétation de la simulation. Ces chiffres couvrent la durée entière
des gardes. La part liée aux vérifications d'inventaire redondantes
sera mesurée avec le même trajet en FIFO et le moteur modifié.
Reçu `tidewater-performance/runtime-present-mode-20261114-cache/G7-D6-tidewater-cpu-guard-budget.json`,
SHA-256 `690e34a67b07289ff2fd979b74b0cf9d69dbce4caa7db597ae8411c9f12bb9ae`.

Le comparatif FIFO a rejoué le trajet avec et sans la réutilisation
locale des vérifications d'inventaire végétal. Les deux parcours
ont la même empreinte fonctionnelle, avec un écart de trajet de 4,6 cm.
Sur 716 et 678 images prêtes, la moyenne passe de 19,14 à 20,21 ms et
le p95 de 29,42 à 32,47 ms. La durée médiane de la garde initiale
passe de 1,04 à 1,06 ms par appel. Les 59 tests ciblés passent.
Cette paire ne justifie pas de conserver la variante. Les trois
fichiers de l'essai ont été restaurés au chemin moteur antérieur.
Le critère de cadence reste ouvert.
Reçus `tidewater-performance/runtime-inventory-reuse-20261114/G7-D6-tidewater-inventory-reuse-pair.json`
et `G7-D6-inventory-guard-tests.json` dans le même dossier,
SHA-256 `31be9e076d43e74bf1c7bc990ebeba00970b99b5616687c74f75086b60075799`
et `f8d0a5a8ccf5e46f814969153adef5ee304b22b52ecc15dfb9c86c77978be8fa`.

Le binaire de référence avant cette variante a ensuite rejoué le même
trajet face au 050 livré, au profil Moyen à 1280 × 800 en Vulkan et FIFO.
Les deux passages utilisent le même module et la même empreinte
fonctionnelle. Sans profilage CPU, le 050 mesure 21,00 ms en moyenne
et 29,48 ms au p95. Le moteur de référence retenu mesure 19,36 ms et
30,65 ms, respectivement. Ce passage par binaire montre une moyenne
inférieure avec un p95 supérieur. Le seuil de 16,67 ms et la parité
appariée avec le lecteur historique 012 restent ouverts.
Reçu `tidewater-performance/runtime-retained-vs-050-20261114/G7-D6-tidewater-retained-vs-050.json`,
SHA-256 `d3a494e4f7da0b2d2ae3aa7a7ad4c79e40b28f6eb6cd30b914587743e2754d43`.

Le moteur livré dans le kit 052 a rejoué le trajet Tidewater face à la
référence récente du moteur commun. Les deux passages utilisent Vulkan
FIFO, le profil Moyen à 1280 × 800, le même module et la même empreinte
fonctionnelle. L'écart de distance est de 4,9 cm. Sur 680 et 679 images
prêtes, la moyenne passe de 20,15 à 20,19 ms et le p95 de 31,17 à 31,55 ms.
La cible de 16,67 ms et la parité avec le lecteur historique restent ouvertes.
Reçu `tidewater-performance/runtime-retained-vs-052-20261114/G7-D6-tidewater-retained-vs-052.json`,
SHA-256 `f7125b2887bb3d9c4b82a1fe8d26c89619bc780baccd0a104c440f6aa907c4ab`.

Le même binaire de référence a ensuite démarré Tidewater à 2560 × 1440 en Vulkan et
FIFO, avec les 20 valeurs graphiques du lecteur 012 confirmées dans les
observations. Les identités source, archive et gameplay correspondent. Sur
les quatre secondes centrales du passage immobile, le moteur commun
mesure 20,86 ms en moyenne et 22,23 ms au p95 sur 192 images prêtes.
Les deux phases immobiles du 012 mesurent 10,44 et 10,42 ms en moyenne,
avec des p95 de 13,84 et 15,10 ms sur 384 images chacune. Le joueur
part de la même position. La caméra du 012 est en première personne au
préchauffage et celle du moteur reste en troisième personne, avec un
recul de 4,5 m. Les charges de rendu peuvent donc différer, et le moteur
reste au-dessus du seuil de 16,67 ms sur ce passage.
Reçu `tidewater-performance/runtime-stationary-012-settings-20261114/G7-D6-tidewater-stationary-012-settings.json`,
SHA-256 `83ad6f59b674e849a601e5dc702736b0b207fd8990c19de1251ac629d19e58e6`.

Le 14 novembre, le raccourci de jeu a aussi activé la première personne
sur ce même moteur. Les observations confirment le même point de départ,
un lacet et un tangage nuls, une distance caméra nulle et un champ
vertical de 75°. Sur quatre secondes après stabilisation, les 186 images
prêtes mesurent 21,45 ms en moyenne et 23,02 ms au p95. Le mode caméra
rapproché laisse donc le même ordre d'écart. Le champ vertical historique,
les charges de rendu et les chemins de mesure restent à rapprocher.
Reçu `tidewater-performance/runtime-stationary-012-settings-first-person-20261114/normal-input/G7-D6-tidewater-stationary-first-person.json`,
SHA-256 `4aab6e51c64d9ccd20cdecf576e301585c016dfaa1b8ec1fa94d812dcd967d84`.

Le même exécutable et le même export ont été rejoués au profil Élevé.
À réglages personnalisés, position, caméra et présentation alignés,
la moyenne passe de 21,45 à 25,73 ms et le p95 de 23,02 à 27,63 ms
entre Moyen et Élevé. Au profil Élevé, 155 images prêtes couvrent la
fenêtre centrale de quatre secondes. La préparation des fenêtres Bevy
passe de 8,22 à 10,86 ms par appel en régime stable. Elle comprend
l'acquisition de la surface et ses temps recouvrent les autres mesures.
Le relevé Élevé de 16,36 ms du 10 novembre utilisait un autre binaire.
Le profil seul ne reproduit pas ce relevé. Le code de référence Gate 3
déclare le même champ vertical de 75° en première personne, avec un
plan lointain configuré à 60 km dans le lecteur de référence et un
plan lointain par défaut à 1,6 km dans le contrôleur commun. Ces
valeurs de source complètent les observations de caméra.
Reçu `tidewater-performance/runtime-stationary-012-settings-first-person-20261114/G7-D6-tidewater-stationary-quality-pair.json`,
SHA-256 `de9b1d36defcb9684bea91a084addcf28c23decb22d973f1965ee5e3f75f77f6`.

La relecture des traces du même binaire en première personne et à 2560 × 1440
compare sept relevés stables au profil Moyen et cinq au profil Élevé.
Le rendu principal prépare dans les deux cas environ 19 128 instances et
121 lots. La préparation des ombres passe de 57 935 à 77 297 instances
et de 419 à 549 lots, répartis sur neuf puis dix vues d'ombre non vides.
La durée GPU médiane du graphe de caméra passe de 9,21 à 11,77 ms,
et celle de la passe opaque de 2,80 à 2,82 ms. Les compteurs orientent
le diagnostic vers les ombres du moteur commun pour l'écart entre ces deux
profils. Les durées GPU sont asynchrones et le profil modifie plusieurs
paramètres à la fois. Ces mesures ne déterminent ni la contribution d'une
cascade, ni la cause du retard face au lecteur 012.
Reçu `tidewater-performance/runtime-stationary-012-settings-first-person-20261114/G7-D6-tidewater-quality-workload.json`,
SHA-256 `b54e02fe22dbee2fbee9cc8fb367e71b7f6d4037e8ddb4571b0dd7328f86104c`.

Dans le profil chargé par la politique graphique commune, le passage de
Moyen à Élevé augmente les cascades directionnelles de trois à quatre,
la résolution de la carte d'ombre de 2048 à 4096 et la portée maximale de
96 à 128 m. L'essai précédent avait déjà réduit les listes de casters
directionnels sans gain mesurable sur le trajet. Le profileur optionnel
conserve désormais le total et ajoute le détail par cascade directionnelle,
face de lumière ponctuelle et spot. Chaque ligne compte les lots et les
entrées d'instances préparées. Les vues sans correspondance explicite sont
comptées à part. `cargo check --locked --offline -j 3 -p forge_engine`
passe sous Windows. Le premier build Release a compilé, mais trois
fichiers de conversion avaient changé pendant son exécution. Le rejeu
sur cache chaud a produit un reçu de build avec des entrées stables.

Au point d'arrêt Tidewater, le même binaire instrumenté a relevé onze
échantillons par profil (Vulkan FIFO, 1280 × 800). Les six faces de la
lumière ponctuelle préparent 81 entrées d'instances au total dans les
deux profils. La quatrième cascade directionnelle du profil Élevé
prépare à elle seule 19 365 entrées. Le total d'ombre médian passe de
57 703 à 76 995. Chaque relevé rapproche exactement le détail du total.
Le premier essai en mouvement s'est arrêté avant la fin du trajet. Ce
constat couvre le point d'arrêt et le volume des listes préparées. Le
temps GPU propre à chaque cascade et la parité avec le 012 restent à établir.
Reçu `tidewater-performance/runtime-shadow-view-attribution-20261114/G7-D6-tidewater-shadow-view-attribution.json`,
SHA-256 `faed7a232b485759204b2176d928d22326d6caaa9632b0d53a519add8414b8b6`.

Au profil Élevé, désactiver le profilage CPU et son instrumentation du
rendu donne 25,55 ms en moyenne et 27,38 ms au p95 sur 157 images,
contre 25,73 ms et 27,63 ms avec instrumentation. L'écart observé
avec le relevé du 10 novembre subsiste sur ce passage.
Les manifestes de compilation des deux binaires diffèrent sur 11
fichiers dans les chemins inspectés du moteur, du runtime et de la
livraison. Ils couvrent notamment la préparation du rendu instrumenté,
les listes d'ombre de végétation et la lecture des paquets. Les traces
GPU asynchrones récentes relèvent une hausse dans plusieurs passes.
La portée globale passe de 8,91 à 11,77 ms et la passe opaque de
1,96 à 2,82 ms en médiane sur quatre et cinq observations par binaire.
Cette comparaison ne sépare pas la charge, les fréquences GPU et les
conditions de présentation. L'inventaire d'affichage lu après le test
indique 2560 × 1440 à 144 Hz sur la sortie principale et 1920 × 1080
à 60 Hz sur la sortie secondaire. La fréquence de la sortie secondaire
n'est pas celle du plein écran Tidewater. Les traces du 10 novembre
documentent la taille et le mode FIFO, sans fréquence de sortie.
Reçus `runtime-stationary-012-settings-first-person-20261114/normal-input-high-unprofiled/G7-D6-tidewater-cpu-profiling-pair.json`
et `runtime-stationary-012-settings-first-person-20261114/G7-D6-tidewater-early-late-runtime-delta.json`
sous `tidewater-performance/`, SHA-256
`610bf31ec6dcf97c97e670fe8541fa819296d54056ed8fe8a341abbc61c59409`
et `c33da60dfe38b7dbcae812b4e6e58ed9fc373ebbdd64c20f3df9b7d3b7a3ccb5`.

La relecture des traces de cadence confirme que l'ancre caméra préparée
du 012 et l'observation native Élevé ont les mêmes coordonnées en
première personne `[40, 5.3193101883, -115]`, la même rotation à `1e-8`
près et une surface de 2560 × 1440. Rapportés à la période nominale de
144 Hz observée sur la sortie principale, les intervalles du 012 se
répartissent entre une et deux périodes. Dans le moteur commun, 184 des
186 intervalles au profil Moyen se rangent près de trois périodes et
150 des 157 au profil Élevé sans profileur près de quatre. Ces intervalles
de boucle aident à cibler l'acquisition de surface et la soumission. Ils ne
mesurent pas l'affichage à l'écran. Le plan lointain, la charge du rendu et
les points de mesure diffèrent toujours.
Reçu `tidewater-performance/runtime-stationary-012-settings-first-person-20261114/G7-D6-tidewater-interval-distribution.json`,
SHA-256 `6bdd230d92870eb116db381fc32e02f4371a4b854d41e72bbd966a9fc494f7cb`.

À 2560 × 1440, sur la RX 7800 XT en Vulkan FIFO, la relecture des
traces fixes rapproche 14 mesures GPU du lecteur 012 et du moteur
commun au profil Moyen. La médiane de la passe opaque reste proche
(2,87 ms dans les deux phases du 012, 2,80 ms dans le moteur commun).
Le graphe de caméra passe de 5,96–6,50 à 9,21 ms et la préparation GPU
des lots Bevy (`bin_unpacking`) passe de 0,17 à 0,88 ms. Le relevé commun
retient sept observations de moins de 100 ms. Les relevés du 012 peuvent
répéter une mesure asynchrone, les phases ne sont pas simultanées, et le
plan lointain diffère. Les durées imbriquées ne s'additionnent pas.
Reçu `tidewater-performance/runtime-stationary-012-settings-first-person-20261114/G7-D6-tidewater-012-common-gpu-scope.json`,
SHA-256 `a70757badd8aeb6d697b3fabf8316f5ba392e549793a37927b38ef873c0b339d`.

Le lecteur 012 utilise le `CapturedPlantMaterial`, qui désactive les
ombres Bevy, et le graphe d'ombres source. Le runtime commun utilise le
`WorldPlantMaterial`, qui conserve les ombres Bevy, et active aussi ce
même graphe d'ombres source pour cet export. Le rapport runtime Moyen
confirme cinq lots végétaux actifs dans le graphe source et le journal
confirme des cascades Bevy non vides. Pour rapprocher les deux chemins,
vérifier quels matériaux reçoivent ces cascades, notamment les matériaux
Bevy des contenus mixtes et la contribution des éclairages locaux.
Ces observations ne quantifient pas la part des casters traités deux fois.

Le profileur commun recense désormais les candidats directionnels par
cascade et classe de matériau avant la préparation du rendu. Il sépare
les plantes éclairées par le graphe source, les autres plantes, les
surfaces source et les autres candidats. Les extensions des surfaces
source échantillonnent les ombres Bevy des points et des spots. Le shader
de l'eau Bevy et le brouillard volumétrique exploitent les cascades
directionnelles. Le réglage global des ombres doit donc rester partagé
avec ces consommateurs lors d'une optimisation des casters. La
compilation `cargo check --locked --offline -j 3 -p forge_engine
--features native` passe. Le premier build Release a compilé pendant
des changements du checkout. Le rejeu sur cache chaud a produit un reçu
avec des entrées de compilation stables.

Au point d'arrêt Tidewater (Moyen, 1280 × 800, Vulkan FIFO), onze relevés
stables donnent 18 868 candidats végétaux sous éclairage source dans
chacune des trois cascades Bevy. Les listes correspondantes comptent
19 076, 19 218 et 19 328 entrées. Le reste relève des surfaces sous
éclairage source. La végétation source représente 98,2 % du total
directionnel de ces trois listes. Les nombres correspondent aux
instances préparées dans les relevés du rendu. Le graphe source rapporte
en parallèle cinq lots végétaux actifs. La mesure identifie les matériaux
des candidats, sans mesurer leur couverture sur une carte d'ombre, leur
durée GPU ou la présence de récepteurs natifs sur d'autres modules.
Reçu `tidewater-performance/runtime-directional-caster-ownership-20261114/G7-D6-tidewater-directional-caster-ownership.json`,
SHA-256 `ed8b4d171a85d31dc75fbcc6661adbe73530e93a7b7ce5df6bdf47bf15829c48`.

Sur le même point d'arrêt, un seul binaire Release a comparé le rendu
normal à un mode de profilage qui omet les plantes importées du soleil
Bevy quand leur graphe source est installé. Les deux exécutions terminent
les huit étapes et conservent l'empreinte fonctionnelle et les cinq lots
végétaux source. Les six faces de la lumière ponctuelle conservent leurs
81 entrées et les surfaces restent présentes dans les cascades Bevy.
Le total d'ombre Bevy passe de 57 703 à 1 099 entrées. La moyenne des
intervalles prêts passe de 19,52 à 18,22 ms, le p95 de 22,06 à 20,59 ms
et la médiane GPU du graphe de caméra de 5,85 à 5,45 ms. Le gain est
mesuré en vue fixe, au profil Moyen avec le profileur actif. Le seuil
de 16,67 ms et la comparaison visuelle restent ouverts. Le filtrage est
inactif dans le parcours joueur normal. Avant tout changement permanent,
il faut traiter les récepteurs natifs qui consomment encore ces ombres.
Reçu `tidewater-performance/runtime-source-plant-sun-20261114/G7-D6-tidewater-source-plant-sun-pair.json`,
SHA-256 `db8e33961a96ae65e8460ea78449539d15416dea9b49d2381236a977d712d720`.

Les deux captures natives du même cadrage sont prises à l'image 620
avec ce binaire. L'identité fonctionnelle est identique et les 88 pipelines
sont prêts dans les deux cas. Les grandes ombres restent visibles dans les
deux vues. Les captures diffèrent, notamment sur le feuillage animé, et
correspondent aux ticks 571 et 546. Le SSIM brut est de 0,934391. Il ne
permet pas d'attribuer les différences au filtrage avec ces poses
temporelles distinctes. Ce cadrage ne couvre ni l'eau ni les ombres du
brouillard volumétrique. Le filtrage reste réservé au profilage.
Preuves sous `tidewater-performance/runtime-source-plant-sun-20261114/visual/`.
Reçu `capture-receipt.json`, SHA-256
`4df04a6ca166c9d2eb1c9608d229ffbb194a8c0430222d26cf45f604eb892cb5`.

Le moteur commun applique désormais le filtrage dans le parcours normal
quand le graphe source des plantes est prêt et que la scène ne présente
aucun récepteur visible dépendant du soleil Bevy. Un matériau inconnu
ou le soleil volumétrique conserve la liste Bevy complète. Le ciel source
est explicitement marqué sans réception d'ombres. Les lumières locales
et les autres objets gardent leurs listes. Les deux tests ciblés passent
sur la conservation des listes locales, le récepteur natif, le soleil
volumétrique, l'erreur du graphe et la conservation des autres soleils.
Le test ciblé précède le marquage du ciel source. Le rejeu Release sur
cache chaud a validé les entrées de compilation après les changements
du checkout pendant le premier build. Au point d'arrêt, le parcours
normal conserve les cinq lots source et les 81 entrées ponctuelles.
Les trois cascades Bevy contiennent 208, 350 et 460 entrées, dont aucune
plante source sur les onze relevés stabilisés. La moyenne est de 17,48 ms
et le p95 de 19,47 ms sur 2 633 images prêtes au profil Moyen.
Reçu `tidewater-performance/runtime-source-sun-routed-20261114/G7-D6-tidewater-source-sun-live.json`,
SHA-256 `5eff2bf2c371d366b02487250073c595ec6c10de30c36686b16b52d9c7a38085`.

Sur le trajet apparié, le moteur Release et le 052 livré terminent sur
la même empreinte fonctionnelle au profil Moyen, en Vulkan FIFO à
1280 × 800 et sans profilage natif. Sur 713 puis 741 images prêtes, la
moyenne passe de 19,17 à 18,48 ms, et le p95 de 28,99 à 29,31 ms. Ce
passage compare deux binaires complets. La cible de 16,67 ms, la parité
avec le lecteur historique 012 et la couverture visuelle de l'eau et
du brouillard volumétrique restent à établir.
Reçu `tidewater-performance/runtime-source-sun-routed-20261114/moving/G7-D6-tidewater-source-sun-vs-052-moving.json`,
SHA-256 `500bf8983dca92133addc85ca56ac481921b73bb2cd1dc8c8d83847fab9e08ae`.
La relecture des traces aux empreintes du reçu retrouve les 713 et
741 images prêtes des deux segments. Le 1 % low, calculé avec la
moyenne des huit intervalles les plus lents de chaque passage,
passe de 26,65 à 25,60 images/s. Reçu
`moving/G7-D6-tidewater-052-routed-one-percent-low.json`, SHA-256
`c80d1a402c4cfb31c0d888714a0d0d7bc4f0a7637e5f264e4fc573b2af404209`.

Un essai a indexé les cellules Z des casters végétaux à l'admission
de la population. Il conserve le calcul radial, l'ordre et les limites
de capacité. Les cinq tests du sélecteur passent. Sur le même trajet
Tidewater au profil Moyen, les deux binaires terminent avec la même
empreinte fonctionnelle et les mêmes bornes de trajet. Sur 739 puis
733 images prêtes, la moyenne passe de 18,55 à 18,69 ms, le p95 de
29,70 à 30,81 ms et le 1 % low de 25,13 à 25,18 images/s.
L'index a été retiré des sources après ce comparatif. Le binaire
d'essai demeure dans le cache Cargo commun jusqu'au prochain build.
Reçu `tidewater-performance/runtime-indexed-shadow-selection-20261114/G7-D6-tidewater-indexed-shadow-selection.json`,
SHA-256 `30c17c48f6123df1da0727bb374fb85fdca0e45ee0e6b8200f4f580fd4278697`.

Sur les quatre passages appariés, 172 des 173 intervalles d'au moins
27,78 ms sont mesurés deux images après une nouvelle séquence de
sélection de végétation. Aucun intervalle retenu n'a de HLOD statique
en attente et sa génération reste constante sur chaque passage.
Le décalage pointe vers la publication de végétation et le travail de
rendu qui la suit. La trace ne répartit pas ce coût entre CPU et GPU.
Reçu `runtime-indexed-shadow-selection-20261114/G7-D6-tidewater-vegetation-publication-cadence.json`,
SHA-256 `6c65eb87cb20b1cf1a5cc9872a48510f3bc5b9217f64f15658cb387775dfd6e6`.

La publication du moteur commun compare désormais le composant
`StreamedPlantPart` à sa valeur attendue avant de réécrire les parts
conservées. Les nouvelles parts et les changements de représentation
continuent d'alimenter la détection de changements ECS et le contrôle
de projection. La compilation des tests moteur et le build Release
passent. Le trajet face au 053 conserve les mêmes bornes et la même
empreinte fonctionnelle. La moyenne passe de 17,65 à 17,85 ms, le
p95 de 28,44 à 27,91 ms et le 1 % low de 27,83 à 27,24 images/s.
Les 44 intervalles à deux images de la publication mesurent encore
31,32 puis 31,28 ms en moyenne. La suppression de ces écritures ECS
est conservée, mais les pics persistent et aucun gain global n'est établi.
Reçus `runtime-retained-plant-parts-20261114/G7-D6-tidewater-retained-plant-parts.json`
et `G7-D6-tidewater-retained-plant-parts-cadence.json`, SHA-256
`37faadcebf5658d4bdd2f0fb629fbd4b32a4eb7f0724ddef73c342162590e69e`
et `9ca249803425571fed3ae64b145590ad8362b85e8d5894d18ec566badf51206d`.

La publication réutilise désormais le même ensemble d'indices par LOD
pour refuser les doublons avant toute mutation et pour retirer les
entités sorties de la sélection. Les entités sont parcourues dans
l'ordre de la liste source. Le build Release et les quatre passages
terminent avec la même empreinte fonctionnelle. Dans l'ordre initial,
le p95 passe de 28,38 à 87,18 ms. En ordre inversé, la variante mesure
82,71 ms et le binaire précédent 101,83 ms. Les intervalles supérieurs
à 80 ms apparaissent sur les deux binaires au rejeu. Ces conditions
ne permettent pas de chiffrer l'effet de la mutualisation sur la cadence.
Reçus `runtime-plant-membership-20261114/G7-D6-tidewater-plant-membership.json`
et `runtime-plant-membership-20261114/reverse/G7-D6-tidewater-plant-membership.json`,
SHA-256 `82c136e7f5c732d126a972ad04b4f610004d70d55dd975d87f72f0a6762ba179`
et `921b879f8e2da6c395be1705c1e89385ffe3587e685b8722271133a2fb1de457`.

Le rapprochement des traces FIFO et présentation immédiate prises
sur le même binaire retrouve 43 intervalles lents à deux images de
la publication dans chaque mode, sur 43 et 45 intervalles d'au moins
27,78 ms. Ces intervalles liés à la cadence de publication mesurent
32,68 ms en FIFO et 32,53 ms en mode immédiat, contre 18,93 et
18,76 ms sur les autres images. Le HLOD statique reste stable.
La planification de végétation est mesurée à 3,60–3,90 ms par appel
sur les fenêtres cumulées. La synchronisation FIFO n'explique donc
pas à elle seule les pics observés. Pour distinguer la planification,
la mutation ECS et l'extraction des maillages, le prochain relevé doit
lier ces durées au numéro d'image de publication.
Reçu `runtime-present-mode-20261114-cache/G7-D6-tidewater-presentation-vegetation-cadence.json`,
SHA-256 `d6c02b223db1693419a0eed0d0b928d9afed46f52c3ebd8aa22373493f6173be`.

Le profilage natif rapproche désormais les numéros d'image de la
planification, de la publication végétale et du monde de rendu extrait.
Sur le trajet Tidewater, les 44 publications prennent en moyenne
8,31 ms côté CPU, après une planification à 3,77 ms. Elles ajoutent
et retirent 2,66 entités en moyenne. La préparation du rendu
`ExtractCommands` reste voisine de 0,10 ms dans les intervalles
rapprochés. Ce compteur mesure l'application des commandes dans
l'ordonnanceur de rendu, et ne mesure pas l'extraction elle-même.
Le relevé instrumenté retient 747 images prêtes et un p95 de 28,29 ms.
Le coût mesuré de la publication est la priorité pour le prochain
changement. Les temps CPU ne s'additionnent pas aux intervalles.
Reçu `runtime-plant-frame-timing-20261114/G7-D6-tidewater-plant-frame-timing.json`,
SHA-256 `dbb04f6bcc406644a355cbcc0856b63801da3dfccff2e8982466a8fcedf06461`.

La publication d'une sélection réutilise le contrôle d'intégrité
incrémental des plantes déjà publiées. Le premier déploiement conserve
son contrôle intégral. Le contrôle incrémental relance la validation
complète sur changement ou retrait et garde le contrôle des ressources.
Les 32 tests de végétation streamée et les 29 tests d'inventaire de rendu
passent. Reçu des sept tests d'intégrité ciblés
`tidewater-plant-publication-audit-tests.log.receipt.json`, SHA-256
`7bb198ef82183655c953b2d98297a0d5c227874588008342ecdbf25ce57deb20`.

Sur le trajet Tidewater apparié au profil Moyen, Vulkan FIFO et
1280 × 800, la moyenne passe de 18,25 à 17,81 ms, le p95 de
27,84 à 23,78 ms et le 1 % low de 29,44 à 32,22 images/s.
Les 44 intervalles situés deux images après la publication passent
de 30,03 à 26,58 ms en moyenne, avec 38 puis 13 intervalles
d'au moins 27,78 ms. L'empreinte fonctionnelle est identique et
l'écart maximal aux bornes est de 4,4 cm. Les manifestes diffèrent
aussi sur un fichier Studio hors graphe du binaire joueur, vérifié
par la résolution Cargo. Cette paire reste au-dessus de la cible
moyenne de 16,67 ms et ne qualifie pas la parité historique.
Reçu `runtime-plant-publication-audit-20261114/G7-D6-tidewater-plant-publication-audit.json`,
SHA-256 `b831e8185346c930196d4562097831a22444e6fe59d78246e7c6f708edbb8b70`.

Les recherches de végétation par identité et niveau de détail utilisent
des tables de hachage. Les listes d'indices conservent l'ordre des
entités. Les 32 tests de végétation streamée passent. Le premier build
Release a été refusé après une modification parallèle dans le Studio.
Le rejeu utilise le cache Cargo commun et un inventaire stable.
La résolution Cargo confirme que le Studio est hors du graphe joueur.

Sur le même trajet au profil Moyen, le p95 passe de 27,67 à 24,43 ms,
le 1 % low de 27,65 à 29,59 images/s et la moyenne de 19,34 à
19,30 ms. L'empreinte fonctionnelle est identique et l'écart maximal
aux bornes est de 5 cm. Une paire ne suffit pas à établir la parité
avec le lecteur 012. Reçu
`runtime-plant-identity-lookups-20261114/replay/G7-D6-tidewater-plant-identity-lookups.json`,
SHA-256 `427557d18e5592cdc13dd12c8e83fbb5d99fb1a2f55b4bbce298b20c7928bda6`.

Le calcul de l'empreinte graphique au seul moment de l'application a
été mesuré sur le trajet Tidewater. Les 32 tests graphiques passent.
Le build Release a vérifié que seuls les trois fichiers de cet essai
différaient dans les chemins Rust inspectés. En Vulkan FIFO, au profil
Moyen et à 1280 × 800, toutes les images retenues sont prêtes. Entre la
référence et cette variante, la moyenne passe de 19,47 à 20,39 ms
et le p95 de 30,75 à 33,81 ms. La variante a été retirée des sources
après cette paire. Le critère de cadence reste ouvert.
Reçu `tidewater-performance/runtime-graphics-application-identity-20261114/G7-D6-graphics-application-identity-pair.json`,
SHA-256 `2c0ad83885c05a5895f5050372228a40723574e8fd806c41cb8ba9bdd4fc3ac5`.

Le filtrage des végétaux par face de carte d'ombre ponctuelle a repris
leurs enveloppes de déformation. Le trajet apparié a terminé avec
679 puis 652 images prêtes, la même empreinte fonctionnelle et un écart
maximal de 5,1 cm entre extrémités. Les médianes d'instances d'ombre
restent proches, à 57 694 et 57 667. La moyenne passe de 20,17 à 20,98 ms
et le p95 de 29,76 à 31,49 ms. Le temps de préparation des lots reste
voisin de 2,2 ms. Le filtrage expérimental a été retiré et l'empreinte
du fichier moteur rétablie. Le build Release d'essai a terminé en
16 min 34 s. Le binaire de compilation partagé reflète encore cet essai.
Reçu `tidewater-performance/runtime-point-shadow-cube-20261114/G7-D6-tidewater-point-shadow-cube-pair.json`,
SHA-256 `659021b7b68ef094652cc4bd033acfe565561e66f2bacee4c0390c26a6b35cfa`.

La publication des végétaux du moteur commun utilise une table de
hachage pour retrouver les entités existantes par indice source et
niveau de détail. Les listes de rendu gardent l'ordre de sélection.
Les retraits suivent l'ordre croissant des indices pour conserver la
réutilisation des identifiants d'entité. La suite ciblée de la scène
végétale compte 42 succès et un test GPU ignoré. Le gain de cadence
de cette modification reste à mesurer.
Reçu `tidewater-resident-source-index-tests-20261114.log.receipt.json`
à la racine de la campagne, SHA-256
`376062becfdab7743d516d5ee9c44b70ecc8db4ccceb40d467e1188ccc9cf253`.

## Historique des qualifications

Le Studio 018 est assemblé sur les sources communes. Il intègre l'ouverture
du navigateur sur un espace vide, le runtime avec diagnostics de disponibilité
avant fermeture et le bundle de notices 015. Reçus dans
`first-run-resume-observations-20261105-018`. Installateur privé Windows
`ForgeStudio-0.1.0-preview.20261105.018.exe`, SHA-256
`bece3b6866eea268f041b0673957cdc95361339b4595a3a850d655066e4ba540`.
Reçu `studio-installer-result.json`, SHA-256
`7fdd2d830caf73dde45973af383d8b050dfc17d3112449c8c5c0fce33bdf03a8`.
Le chemin source de la référence Rust est toujours présent dans le WASM 018.
Le manifeste Studio inclut les trois documents Rust et leur preuve.
Pour `erased 0.1.2`, 38 noms de fonctions du WASM avant optimisation
référencent le type `ErasedBox` dans les fermetures de Tachys. Le WASM livré
conserve le chemin du code Tachys et omet la table des noms de fonctions.
L'archive Cargo verrouillée correspond aux sources locales et déclare MIT.
Cette analyse conserve la réserve runtime du Studio et ne tranche pas
l'attribution du code optimisé. Reçu `R7-08-studio-erased-binary-scope-018.json`,
SHA-256 `329980ab288d4f0a254c18eb0d34a2ce906fbe3d058e63835a8e1321e3a84329`.
Le scénario Windows propre 018 est prêt dans `studio-clean-20261105-018`.
Il monte en lecture seule l'installateur du Studio 018 et l'export privé des
Trois Cairns 017, déjà vérifié sur l'hôte. Reçu `windows-sandbox-plan-018.json`,
SHA-256 `e91b349d7d183ad022411142c61512d26d8798a42818e7275171d69a1f759fa4`.
L'activation de Sandbox a réussi avec l'option sans redémarrage automatique.
Windows a demandé le redémarrage effectué depuis. Reçu
`windows-sandbox-activation.json`, SHA-256
`e88d12c4db8a59506f61b27b8b88e3d2f34695907040aab2ba4ef672606cd25e`.
Après redémarrage, le scénario 018 a ouvert Windows Sandbox. Le relevé
invité Windows 11 Entreprise 26100 confirme l'absence de SDK et des cinq
commandes de développement recherchées, avec WebView2 déjà présent.
Reçu `windows-sandbox-guest-preflight-018.json`, SHA-256
`c031d965c62d2d1abf171105e9a03f0a5bac50a6d05124ff508f5e3d3999dbc4`.
L'installation du Studio 018 autorisée a échoué avant la création du plan.
`forge-delivery.exe plan-file` sort avec `0xC0000135`. L'invité ne contient
ni `VCRUNTIME140.dll` ni `VCRUNTIME140_1.dll`. Les binaires natifs 018 les
importent selon leur rôle, alors que l'installateur ne livre que WebView2.
Reçu `R7-10-clean-windows-studio-crt-refusal-018.json`, SHA-256
`9336a83ea9f466f4343ef0471d3d3156ae895824ea414a58454f574137935c13`.
La construction Windows de distribution passe à la liaison statique du CRT.
Le premier outil de livraison recompilé a inspecté le paquet des Trois Cairns
dans le même invité dépourvu de ces DLL. Reçu
`clean-windows-static-crt-20261106-019/R7-10-clean-windows-delivery-cli-static-crt-019.json`,
SHA-256 `19d60fb458c594de9e40c18dd84d035af039c8e90dbc3e65b756dd622486b9fe`.
Le candidat 020 regroupe le Studio, le moteur de jeu exporté, le launcher
autonome et l'outil de livraison. Leurs tables d'imports PE ne contiennent
aucune DLL Visual C++ redistribuable. Reçus dans
`windows-static-crt-candidate-20261106-020` sous la campagne Gate 7.
L'installateur `ForgeStudio-0.1.0-preview.20261106.020.exe` a pour SHA-256
`47bb5b031173a4aaffce26920487ba4e7bb18ef598a7688cbb2bf988c3e01bb8`.
Le premier Sandbox 020, ID `25216f97-0c0c-451a-8409-086b336cca03`, a confirmé
Windows 11 Entreprise 26100 sans SDK, compilateurs ni DLL redistribuables.
Reçu `windows-sandbox-guest-preflight-020.json`, SHA-256
`64d773bcf8fd085e4b48c90d437d13a7f331893416e7045fe90412c0563dd3ef`.
Le bureau invité est resté noir dans Computer Use, malgré Explorer et DWM
actifs dans la session invitée et une reconnexion. Ce Sandbox est arrêté.
Le scénario en rendu logiciel montre le même écran noir, avec un nouveau
précontrôle réussi. Sandbox actif `072bcbc8-2aba-4dd9-9105-7e255ffcbf45`,
fenêtre `41618762`. Reçu
`windows-sandbox-guest-preflight-020-software-display.json`, SHA-256
`5c387199120edbd956561b8090a947349d0aa88824e7cd64772d1ef1d48153b8`.
Après reconnexion dans la fenêtre active, Computer Use voit le bureau invité.
L'assistant du Studio 020 a été affiché, puis l'invité s'est arrêté avant
l'accord au bouton « Installer ». À la reprise, deux nouveaux invités avec
les mêmes entrées épinglées ont refusé l'ouverture de session (nombre
maximal de sessions dépassé et `0x80070520`), y compris après fermeture
des clients résiduels de qualification. Les invités ont été arrêtés.
Reçu `windows-sandbox-resume-connection-refusal-020.json`, SHA-256
`f14acf285da7379f4090a5aa7c9fbe7d427fd57b5818b8b3208b8bc7a015b75c`.
Sans redémarrage hôte, la reprise en rendu logiciel a brièvement ouvert
l'invité `6b2e2216-f61e-44be-be0c-78eb64308703`. Précontrôle invité
`windows-sandbox-guest-preflight-020-recovered-software.json`, SHA-256
`84d8d8c7592b50dbc587e95dafece53231f08626971f845df6f3b584787fb213`.
Le client a affiché une déconnexion et l'ID a disparu après la fermeture
du client précédent. Aucune installation 020 n'a eu lieu dans cet invité.
Le processus Sandbox résiduel a disparu sans redémarrage du PC. Le nouvel
invité en rendu logiciel `d08cc619-035d-40f5-993e-ec421753c876` a installé
le Studio 020 avec l'accord de l'utilisateur. L'installateur a terminé et
le Studio installé a affiché son écran d'accueil. Reçus
`windows-sandbox-guest-preflight-020-software-20261106-165420.json` (SHA-256
`2f1fd22e9058e409179dc5f1c98b199d4de7ad780a3fd74eb9389703791150fe`) et
`R7-10-clean-windows-studio-install-020.json` (SHA-256
`230123a794e9ae3955f1e976e2d5406a5b0f26ff265ae002bc811605ca193d7e`).
Le projet vierge « Mon projet », sa carte et le joueur initial ont été créés
et sauvegardés dans le Studio installé. La vue 3D est restée en chargement
jusqu'à la fermeture de la carte. L'export privé 1.0.0 a produit quatre
fichiers vérifiés par les deux lecteurs natifs. Reçu
`R7-10-clean-windows-studio-export-020.json`, SHA-256
`11a493df2c2bd3ef59072ecf7fdbf4b011b8fbe1f5bf0442315e8a78e866e0f7`.
La copie de reprise du jeu reste sous `windows-clean-evidence-software-20261106-165420/mon-projet-export-020`.
Les trois fichiers auteur sont archivés dans `mon-projet-workspace-v1-020.zip`,
SHA-256 `9f68d36ce00f3c628647a86da6eeb5be9f69cead36094918650c1d409e9add49`.
Installation du jeu et ouverture de `Jouer.exe` réussies dans le Sandbox
en rendu logiciel. Premier démarrage, profil graphique Faible : Bevy a
sélectionné Microsoft Basic Render Driver (CPU/DX12) et reçu `Out of Memory`
avant la première projection prête. Aucune reprise de partie prouvée.
Reçus `R7-10-clean-windows-game-install-020.txt` (SHA-256
`fa135443cc5a32cecc94a42670640547b11bbf36c3b25cb2bf8c82d2d35295d9`) et
`R7-10-clean-windows-game-runtime-report-020.json` (SHA-256
`d23b43b909e9293453267522e218c7a5076ea53c7acc12efb829609724e8501f`).
L'invité logiciel est arrêté. Réutiliser l'export scellé dans un invité
avec GPU et sans Studio pour la partie et la reprise.
Le Sandbox neuf `63051335-5a29-4630-b68f-9f8d8a2c6c19` expose les deux
adaptateurs AMD dont la Radeon RX 7800 XT. Précontrôle
`windows-sandbox-game-only-vgpu-preflight-020.json`, SHA-256
`218b18cd168a6e94f2fc87911db28179046cb43a815b64a1a356190c374cf174`.
Il contient le jeu seul, sans Studio installé ni SDK. À la racine du dossier
partagé, le launcher refuse le chemin Windows canonique qui se termine par
une barre oblique inverse. La copie locale invitée `Desktop/MonProjet020`,
vérifiée aux quatre empreintes, affiche et vérifie le plan d'installation.
Le code de livraison applique désormais sa normalisation de répertoire à
la source du candidat Windows avant et après résolution, avec le même contrôle
des liens. Le test ciblé accepte un dossier canonique terminé par `\` et
vérifie toujours l'intégrité du candidat et le rollback. Reçu
`r7-09-installer-shared-root-manifest-20261106.log.receipt.json` à la racine de
la campagne. Le binaire 020 reste celui d'avant la correction.
L'installation a été autorisée et vérifiée dans ce Sandbox GPU.
`Jouer.exe` a lancé la partie, F5 a créé la sauvegarde et « Reprendre »
a affiché « Sauvegarde reprise » dans un deuxième processus. La Radeon RX
7800 XT a été utilisée en Vulkan pour les deux lancements. La sauvegarde
est restée identique octet pour octet. Les deux sorties réussissent après
une première projection prête à la trame 15. Le second rapport relève
une dernière projection prête à la trame 680 et un état final indisponible
à la fermeture.
Reçu `R7-10-clean-windows-game-vgpu-resume-020.json`, SHA-256
`99881b4005ac8e754cdf33f52219501eab390ef74ca044d67ef8f153e3dc9ce3`.
Le Studio 020 installé dans le Sandbox GPU `eeb47575-98e3-4192-921f-b0a2f50beb9f`
a rouvert `Mon module / Ma carte` depuis les trois fichiers auteur épinglés.
Le terrain s'affiche et la commande « Rapprocher » change le cadrage. Les
diagnostics affichent une vue 3D active et des allocations WebGPU. Le journal
reste sur « Terrain chargé · préparation de l'affichage ». Les trois fichiers
auteur sont inchangés et le binaire lancé correspond à la preuve de build 020.
Reçu `R7-10-clean-windows-studio-viewport-vgpu-020.json`, SHA-256
`a5bf57f92985913a30799ae8b39daca838c62a408692e8d7ec293965d287541f`.
Après accord, la désinstallation NSIS du Studio 020 a terminé dans ce même
Sandbox. Elle a retiré les fichiers des paquets, le bootstrap, le raccourci
et l'enregistrement Windows. Le répertoire des paquets vide et le fichier
de verrouillage de zéro octet subsistent. Les trois fichiers auteur ont les
mêmes SHA-256 avant et après. Reçu `R7-09-clean-windows-studio-uninstall-020.json`,
SHA-256 `270a81cc18bb69f2fb981514f2acc3fc95b251905a60a834121834b4d40e1678`.
L'invité a été arrêté après la vérification. Les exigences globales
R7-07/08/09/10 restent ouvertes.
R7-09 : l'ancien garde NSIS refusait une réinstallation dans ce dossier
résiduel sans marqueur. Le contrôle natif utilisé par NSIS accepte maintenant
uniquement un verrou vide et un répertoire de paquets vide dans un dossier
sans marqueur. Le test ciblé vérifie la désinstallation, le refus d'un fichier
étranger, la réinstallation et la conservation du projet. Les deux recettes
NSIS compilent avec les charges inertes du test. Reçu
`r7-09-studio-reinstall-root-20261106.log.receipt.json` à la racine de la
campagne. Le contrôle depuis un installateur Windows reconstruit reste dû.
Le lot `windows-reinstall-correction-20261106-021` regroupe cette correction
et celle du chemin d'export Windows. Son inventaire de build diffère du 020
sur six fichiers du service de livraison et de sa recette NSIS. Le CLI,
le launcher, le moteur et le Studio ont compilé en release Windows depuis
le cache partagé. Le kit et l'installateur sont assemblés. Reçu
`studio-installer-result.json`, SHA-256
`11a4463ca7ee5ab5232b9ed0faa3ffb826a39d3ced189ae3e8b089a42ebfb390`.
Les quatre binaires Windows 021 sont x64. Les noms de DLL importées
correspondent à ceux du 020, sans import du runtime C++ redistribuable.
Reçu `windows-pe-imports-021.json`, SHA-256
`b81734ba419ef9358c358ca87b8e10fa57a9a8caf38c655603f7b4edd5bd91eb`.
R7-08 : les quatre reçus 021 confirment `+crt-static`. Les Build Tools 2022
17.14, MSVC 14.44 et le SDK 10.0.26100.0 présents sur l'hôte sont recensés
avec les empreintes des trois bibliothèques CRT statiques de référence.
Reçu `R7-08-windows-static-crt-scope-021.json`, SHA-256
`9da9a896f72ef9d687658514e86d0d50281aae9dc53129cffebad219d81343f1`.
Les PDB ont ensuite été appariés aux quatre exécutables livrés par GUID et
âge CodeView. Leurs enregistrements de modules d'édition des liens identifient
des objets de `libcmt.lib`, `libvcruntime.lib` et `libucrt.lib` dans les quatre
rôles, depuis les chemins MSVC et SDK recensés. Reçu
`R7-08-windows-static-crt-linked-modules-021.json`, SHA-256
`729f980f058ae640848cc69287957d946208d48fbd75ef56c261fd9137305f4f`.
La relecture du paquet Studio 021 et de l'export « Mon projet » vérifie
respectivement 472 et 471 entrées de licence. Les deux index des
contributions originales Forge référencent les mêmes textes MIT et Apache-2.0,
identiques à ceux du dépôt. Le module de jeu ne référence aucun asset du
catalogue. Les trois réserves Cargo demeurent et les paquets déclarent un
usage local privé. Reçu `R7-08-windows-021-packaged-notices.json`, SHA-256
`175d1f1645c76c59d2e5caa60914b08d5acf8f3f458281625526bf054da2417e`.
Le catalogue des Build Tools installés identifie les conditions applicables
à cet outil. Le SDK installé renvoie à la liste Microsoft des fichiers
redistribuables. Elle couvre par répertoire les bibliothèques statiques
de `Windows Kits/10/Lib/<version>/ucrt/x64`, où les PDB du lot 021
identifient `libucrt.lib`. La licence du SDK limite la distribution des
bibliothèques statiques au résultat de leur liaison avec le programme et
impose d'autres conditions. Les quatre reçus 050 confirment
`+crt-static`. Les PDB du Studio, du launcher et du CLI de livraison
correspondent aux exécutables 050 par GUID et âge CodeView. Ils confirment
la présence des trois archives CRT. Le PDB disponible pour le runtime
porte le GUID d'un build ultérieur. La trace de liaison du runtime 050
et la base de redistribution pour `libcmt.lib` et `libvcruntime.lib`
restent à établir.
Reçu `studio-web-notice-corrections-20261110/R7-08-windows-sdk-static-ucrt-scope-20261114.json`,
SHA-256 `5b6306bc0d1727313e5852043581ccd12149b05196e7ec22beb718a9e5c753dd`.
Reçu de liaison `studio-web-notice-corrections-20261110/R7-08-windows-static-crt-linked-modules-050.json`,
SHA-256 `64ee2e65b7956a4f7e750cb4b79e6dd33cb510a59ac36600908b200d892c3e37`.

Le catalogue local des Build Tools 17.14.37516.0 renvoie aux conditions
Microsoft de mars 2024. L'exception sans licence Visual Studio vise la
compilation de dépendances C++ tierces open source qui remplissent les
conditions du texte. Pour les deux archives MSVC identifiées dans les
exécutables 050, il reste à documenter la licence Visual Studio
applicable au build et la disposition couvrant la redistribution.
L'inventaire `vswhere` de l'hôte identifie uniquement les Build Tools.
La liste Microsoft REDIST de Visual Studio 2022 porte sur les éditions
Community, Professional et Enterprise et ne nomme pas `libcmt.lib`
ni `libvcruntime.lib`. La documentation technique confirme leur rôle
dans la liaison statique. L'applicabilité des droits reste à établir
pour les exécutables Forge qui les incorporent.
[Liste Microsoft REDIST](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution).
[Bibliothèques CRT MSVC](https://learn.microsoft.com/en-us/cpp/c-runtime-library/crt-library-features?view=msvc-170).
Reçu `studio-web-notice-corrections-20261110/R7-08-windows-msvc-build-tools-terms-20261114.json`,
SHA-256 `85dec8f2420c986654d0badc8467467ceca3a6409783cd0b2d89e3e7ac96312e`.

Les textes Microsoft du runtime 2015–2022 et du redistribuable v14 sont
vérifiés à partir des documents liés dans l'annuaire Microsoft. Ils
couvrent l'usage du runtime par l'utilisateur final. L'acquisition directe
du package Microsoft par l'utilisateur laisse entière la question de la
licence des objets CRT présents par liaison statique dans Forge. Le guide
Microsoft pour Rust et la liste de redistribution 2022 restent les
références pour rapprocher l'environnement de compilation des droits de
distribution applicables. Reçu
`notice-webview2-loader-20261114/R7-08-msvc-runtime-agreement-scope.json`,
SHA-256 `8babe43752f6313920717966fd1ad04aef5575f30c1236286db891929a1a55c0`.
[Guide Rust Windows](https://learn.microsoft.com/en-us/windows/dev-environment/rust/setup) · [Runtime 2015–2022](https://visualstudio.microsoft.com/license-terms/vs2022-cruntime/) · [Redistribuable v14](https://visualstudio.microsoft.com/license-terms/vs2026-ga-visualcpp-v14-redist-runtime/).

La recette partagée accepte `--windows-external-crt` pour construire le
CLI de livraison, le moteur de modules, le launcher et le Studio Windows
avec `-crt-static`. Le reçu conserve les drapeaux utilisés. Les 13 tests
de recette passent, dont le refus sur une étape frontend et la vérification
de la liaison demandée dans le reçu. La première compilation porte sur le
CLI. Pour qualifier un candidat complet, vérifier les imports et les objets
liés dans chaque binaire, puis contrôler les démarrages sur Windows propre
avec le runtime officiel installé séparément à une version compatible.
[Runtime Visual C++ pris en charge](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/).

Le CLI Release à CRT externe démarre sur le poste et résout le WebView2
installé. Ses imports PE comprennent `VCRUNTIME140.dll` et les API CRT de
Windows. Le PDB concorde par GUID et âge et ses enregistrements ne
contiennent aucun module provenant des trois archives CRT statiques
recherchées (`libcmt.lib`, `libvcruntime.lib`, `libucrt.lib`).
Reçus sous `windows-external-crt-20261114/` :
`R7-08-windows-external-crt-cli.json` et
`R7-08-windows-external-crt-cli-linked-archives.json`, SHA-256
`57dc66cbace6bed269bf101c9dc4ecf7432856658d85e2da53caf29dd7b7a743` et
`3fa32eaa465f56c9abdbe93306b09ae54242091d4f65bca998371cc26709829f`.

Dans un Windows Sandbox hors ligne sans SDK, le même CLI quitte avec
`0xC0000135` quand `VCRUNTIME140.dll` est absent. Après exécution du
redistribuable Microsoft x64 signé, le DLL système 14.52.37029.0 est
présent et le CLI répond « Aucune installation active » sur la racine
vide. L'attente de la fin de l'installateur Microsoft avait expiré à
100 secondes. Le contrôle ultérieur établit la présence du DLL et le
démarrage du CLI. Le Sandbox a été arrêté. Ce contrôle couvre le CLI.
Le Setup et les trois binaires produit à CRT externe restent à qualifier.
Reçus sous `windows-external-crt-20261114/clean-windows-cli/` :
`cli-guest-local.json` et `cli-after-official-crt.json`, SHA-256
`344ac93a1108e4b9fcdd942c506fea0081c73c9b76a16fc586bd2ff17a4972d9` et
`aea333c7b6df116fd857e13decc790848153f60b62751507419d57cadab39098`.

Le Setup Studio accepte `--external-crt-min-version`. Dans ce mode,
le contrôle NSIS lit les versions des deux DLL x64 système avant toute
extraction du CLI et indique le téléchargement Microsoft si le runtime
est absent ou trop ancien. Le reçu de build consigne la version minimale.
Les dix tests de recette passent, y compris la compilation avec NSIS
et l'ordre du contrôle avant l'extraction. Les compilations à CRT
externe du launcher autonome puis du moteur utilisent le cache commun.
Le launcher Release a passé son contrôle d'embarquement. Ses imports
comprennent `VCRUNTIME140.dll` et `VCRUNTIME140_1.dll`. Son PDB concordant
ne recense aucun module des trois archives CRT statiques recherchées.
Le Setup vérifie les deux DLL. Le contrôle d'exécution du Setup avec
payload factice reste à faire. Le Sandbox de cet essai a été arrêté
après le refus de la commande de lancement par la politique d'exécution.
Le moteur est en compilation. Reçu
`windows-external-crt-20261114/R7-08-windows-external-crt-launcher.json`,
SHA-256 `63e2a9ab451fbea0c77b93f002fb2f1de965c55f440ce8cec5058f10a5e070bd`.

La résolution Cargo hors ligne du checkout courant retrouve exactement
les identités de l'index 052 pour les cinq graphes concernés. Le moteur
compte 468 identités, le launcher 298, le Studio natif 414, le Studio
Web 597 et le CLI 84. Aucun texte à ajouter n'est identifié par ce
rapprochement. Les reçus de build et l'index doivent encore être reliés
au même manifeste pour assembler le nouveau kit. La compilation moteur
est en cours, suivie des étapes natives sur un inventaire commun et
du contrôle PE/PDB des trois binaires.
Reçu `windows-external-crt-20261114/R7-08-current-windows-notice-closure.json`,
SHA-256 `057cb43fec0742c60866c02e6af023b3a5f7addc1e639de986837c1af0c15fb8`.

Le kit Windows privé 053 réunit le launcher et le moteur à CRT externe
avec les notices vérifiées sur le même manifeste. Le contrôle des
imports PE et des PDB des trois rôles (moteur, launcher et CLI) confirme
`vcruntime140.dll` et ne trouve aucun objet des trois archives statiques
recherchées. Le kit vérifié compte 420 fichiers et 141 930 528 octets.
Son reçu d'assemblage est sur E: dans `gate7-windows-game-kit-external-crt-20261114`,
SHA-256 `ad8fe5d5235ae845bdb1dd7369eede74eb526b0e529e66920923df715cb42416`.
Reçu des binaires `windows-external-crt-20261114/unified/R7-08-windows-external-crt-game-kit-binaries.json`,
SHA-256 `1937f4779ec8b0c8c22778ce3aebb0a49da23e410f0f43c68057c996f54405c6`.
La chaîne construit l'interface Studio puis le binaire qui embarque le
kit. Le Setup et les parcours installés du 053 restent à vérifier.
Sur le poste de développement, le CLI 053 vérifié par son reçu détecte
WebView2 157.0.4341.46. Les deux DLL Visual C++ système x64 portent la
version 14.51.36247.0, au-dessus du minimum choisi pour le Setup.
Ce contrôle de l'hôte n'exécute pas le Setup. Le parcours positif reste
à vérifier sur un Windows sans SDK avec Evergreen fonctionnel.
Dans l'export Windows, l'assistant d'installation du jeu est une copie
du launcher du kit. Avec le kit 053, le runtime Visual C++ x64 est donc
également un prérequis pour ouvrir l'assistant sur le poste joueur.
Reçu `windows-external-crt-20261114/R7-10-studio-053-host-prerequisite-preflight.json`,
SHA-256 `3c94028b45b7806b11edfe2a4fe7673e6fa143aae810d8b35f1fe1225e1fe72b`.

Des modifications d'orchestration Unity ont invalidé deux reçus Web.
Le validateur conserve ces scripts dans l'inventaire du checkout et
les écarte des entrées de compilation. Le lecteur `vault_storage.py`
utilisé pour préparer la bibliothèque de livraison reste une entrée
épinglée. Le garde-fou refuse les références directes aux scripts
d'orchestration depuis les sources et manifests produit. Le test
ciblé passe. Le rejeu 053 reprend avec le cache Cargo commun sous
`windows-external-crt-20261114/unified-product-inputs/`. Les reçus du
moteur, du launcher et du CLI concordent avec le nouveau manifeste.
Le kit de rejeu rassemble 420 fichiers sur 141 930 528 octets et des
notices pour 727 identités. Son reçu a pour SHA-256
`8d24be14193869063df88618ce4f2389edb319b820144f12c27995606477f5b8`.
Le frontend Web est construit sur ce même inventaire. Le reçu
`unified-product-inputs/prepare-frontend-build-proof.json` a pour
SHA-256 `745b98fda70db5555b7b37dd305687fe368ffe4c08ba9334a2f0893e07aa50bf`.
Le contrôle PE/PDB des trois exécutables du rejeu confirme la liaison
dynamique au CRT. Reçu `unified-product-inputs/R7-08-windows-external-crt-game-kit-only.json`,
SHA-256 `952db30d78a2b3d641848d78c34c698a9dccc8c15161e9badd79a1b100328811`.
Le Studio natif et son Setup privé 053 ont été construits. L'audit
PE/PDB des quatre exécutables confirme la liaison externe au CRT.
Reçu `unified-product-inputs/R7-08-windows-external-crt-game-kit-binaries.json`,
SHA-256 `d075539a447895e564622567ea68fde429dab68f5932429051d339b57511bbf1`.
Le Setup a pour SHA-256
`697de92ec05861b48c00ea43151c63b72107edb45c94bbe9a8340685fb897539`.
L'inventaire hors exécution rapproche les trois payloads Forge et
confirme l'absence des runtimes Visual C++ et WebView2 embarqués.
Reçu `setup-053/R7-08-studio-053-installer-inventory.json`, SHA-256
`e933d6d25880053daddaabb60c8cc9849a1eac0f87d0dda596d53054cbf2091b`.
La relecture du même Setup 053 confirme que les trois plugins et le
bitmap sont identiques aux composants NSIS 3.12 vérifiés dans le 050.
Les empreintes du compilateur et de son fichier `COPYING` concordent.
Le journal confirme le choix de zlib et le reçu épingle aussi le
désinstallateur généré.
Reçu `setup-053/R7-08-studio-053-nsis-attribution.json`, SHA-256
`8f244a618c98357fd208e95f248e0a47eeefd0303b6861f976b0aec89242cec2`.

Sur les binaires 053 exacts du Studio et du launcher, les PDB
correspondants ne contiennent aucun module nommé pour `selectors`,
`dom_query`, `tauri-codegen` ou `tauri-macros`. Les deux graphes Cargo
Windows verrouillés rattachent `selectors 0.36.1` à la construction.
Le code de Tauri sérialise le HTML traité et émet des empreintes CSP
de ses scripts intégrés. Le contrôle couvre les modules de liaison
nommés et la logique de génération. Les octets de chaque ressource
embarquée et le graphe Linux restent à examiner.
Reçu `unified-product-inputs/R7-08-selectors-053-build-scope.json`,
SHA-256 `56e3d1ad75c93239744b6dc2c95efbdf97c3a786a432f7691e77bf2be8c54d39`.

La relecture du paquet Studio 053 vérifie ses 422 entrées, les 375
textes de notices référencés et les neuf archives source Symphonia.
Le manifeste du kit extrait du binaire Studio correspond à ses 420
fichiers. Les trois ressources du launcher sont relues depuis les
octets compressés présents dans l'exécutable. Tauri a sérialisé le HTML
et conservé le JavaScript et le CSS à l'identique. La police Fira Mono
du paquet Cargo est retrouvée dans le moteur avec sa notice OFL-1.1.
Le module d'origine du Grand Plateau relu conserve le CC0 autorisé.

Les PDB du Studio et du launcher 053 répertorient chacun un objet
`option-ext 0.2.0`. Le paquet historique contient le texte MPL-2.0.
L'assemblage des prochains kits inclut aussi l'archive source et
vérifie son empreinte contre les verrous Cargo du checkout et du
launcher. La mise en place de 10 archives a passé une relecture
hors ligne. Les suites de livraison donnent 53 succès et un test
de liens symboliques ignoré. Les octets du prochain Setup et de
l'export de jeu complet restent à vérifier.
Reçus `setup-053/R7-08-windows-053-redistribution-evidence.json` et
`R7-08-next-candidate-application-source-staging.json`, SHA-256
`02e4b152c2de8280c25f6f0c318c47dee048455a53e19d36d0083ea5fade3f08`
et `a90f5b8ab119ce068c0e84da57f01fb053d909bc601370d0c0556053ff3db7fb`.

L'installation positive et la reprise depuis l'export restent à
qualifier sur Windows sans SDK avec les deux prérequis fonctionnels.

Le Setup 053 a été exécuté dans un Sandbox Windows 11 26100.9974 sans
SDK. Sans Visual C++, il sort avec le code 1 et indique le prérequis.
Le redistribuable signé de Microsoft termine avec le code 0 et fournit
les deux DLL x64 en version 14.52.37029.0. Le Setup franchit ensuite le
contrôle Visual C++ et refuse WebView2. Le registre annonce la version
157.0.4341.42, le disque contient la 157.0.4341.46 et le chargeur
retourne `0x80070002`. Les deux refus laissent le dossier Studio
absent. Le Sandbox est arrêté. Reçu
`setup-053/clean-windows-prerequisites/R7-10-studio-053-clean-windows-prerequisite-assessment.json`,
SHA-256 `c77bf3b1132e094ce9857bfc0c3609b6de977d4c684c1bd31ebde73b1d5f5cad`.

Le bootstrapper Evergreen x64 en ligne, obtenu chez Microsoft et
vérifié par signature Authenticode, échoue également avec `0x80040902`
dans un nouvel invité avec réseau. Les versions WebView2 déclarée et
présente restent différentes. Après installation réussie de Visual C++,
le chargeur du 053 retourne toujours `0x80070002`. Le Setup 053 n'a
pas été relancé dans cet invité, désormais arrêté. Reçu
`setup-053/clean-windows-online/R7-10-studio-053-evergreen-online.json`,
SHA-256 `e272b9810dc3cbf9564965f8a5aed65a12d8edaa4a3bcf63ba0093d697251752`.

Pour le rejeu hors Sandbox, l'image d'évaluation française Windows 11
Enterprise LTSC 2024 (26100.1742) est disponible sur E: depuis le
[Centre d'évaluation Microsoft](https://www.microsoft.com/fr-fr/evalcenter/download-windows-11-enterprise).
Le reçu conserve l'origine HTTPS, le SHA-256 mesuré de l'ISO et les
signatures Microsoft valides du programme d'installation et du chargeur
EFI. La comparaison avec une empreinte Microsoft publiée pour l'ISO
française reste ouverte. La VM de qualification Hyper-V existe et
l'accès depuis la session courante est refusé. Le QEMU local démarre
avec WHPX, mais son exécutable ne fournit pas l'option TPM. Le média
LTSC reste à démarrer dans un invité distinct avant de rejouer le 053.
Reçu `setup-053/R7-10-studio-053-ltsc-qualification-media.json`,
SHA-256 `47140a33fb1c764cbc5c0613f3920e95a5537dc23e4126166803c39995cecb07`.

L'inventaire hors exécution du Setup 050 recense neuf entrées. Les trois
plugins et le bitmap correspondent au toolset NSIS 3.12 déjà examiné.
Le CLI, le paquet Studio et l'index correspondent aux reçus d'assemblage.
Le programme WebView2 embarqué a l'empreinte de l'entrée Microsoft
signée et du programme déjà inspecté au lot 020, dont le runtime interne
155.0.4283.45 est x64. Le démarrage du Studio 050 en Sandbox a utilisé
WebView2 déjà présent. L'installation hors ligne sur un système sans
WebView2 reste à vérifier.
Reçu `studio-web-notice-corrections-20261110/R7-08-studio-installer-components-050.json`,
SHA-256 `5535d372b1c74fa827594a3ee6874b763a5a346122b52c2e36957c5a3d813e30`.

Le parcours officiel de téléchargement Microsoft présente les conditions
Evergreen à l'utilisateur. Les sections 2 et 9 imposent notamment
l'accord des distributeurs et utilisateurs externes à des conditions
équivalentes et l'information sur SmartScreen lorsqu'il est activé.
La notice de dépôt identifie le programme Evergreen, décrit l'envoi
d'informations par SmartScreen et renvoie aux deux documents Microsoft
indiqués en section 9(a), ainsi qu'à la licence NSIS. La notice reste à
vérifier dans le prochain paquet. Le rattachement des conditions à
l'acquisition du fichier épinglé, la mise en œuvre de l'accord dans le
parcours de distribution et les autres obligations de la section 2
restent ouverts.
Reçu `setup/R7-08-webview2-evergreen-download-terms-20261114.json`,
SHA-256 `52c5d1f19d9340e85c9a17df2d59887b3a93ce5d8790a5bbf93d7446b1455c7c`.
[Conditions Evergreen affichées au téléchargement](https://developer.microsoft.com/microsoft-edge/api/eula/webview2?locale=en-us).

Le chargeur statique Windows du SDK WebView2 est également présent dans
le Studio et le launcher 050. Le paquet Cargo vérifié par `Cargo.lock`
contient un `WebView2LoaderStatic.lib` x64 identique à celui du
paquet Microsoft.Web.WebView2 1.0.3650.58. Les PDB épinglés recensent
six objets du chargeur par binaire. Le dépôt conserve désormais le
texte BSD 3-Clause de ce SDK et le référence dans les notices que le
pipeline de livraison embarque. Vérifier l'inclusion dans les prochains
paquets. Les obligations Evergreen ci-dessus concernent le runtime.
Reçu `R7-08-webview2-sdk-static-loader-050.json`,
SHA-256 `1e45a921c7149e7eaacce97c7787d42b73d5c27df6d354fb26003600f91db1ea`.
[Paquet Microsoft WebView2 SDK](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.3650.58).

Le graph Cargo Windows a été recalculé sur le checkout commun du
14 novembre après l'ajout des options glTF de `forge_assets`. Les cinq
périmètres de livraison retrouvent les mêmes 727 identités distinctes.
Le pipeline a préparé le lot de 379 entrées et copié la licence du
chargeur au chemin référencé par la notice de distribution. L'empreinte
du fichier préparé égale celle du SDK et de l'inventaire des sources.
Ce contrôle de préparation laisse l'inclusion dans un nouvel
installateur à vérifier sur le paquet final.
Reçu `notice-readback-20261114/R7-08-webview2-sdk-license-staging-20261114.json`,
SHA-256 `7f35719f165accad3d4455390e959f392d0f2ac3690bcede47ae2de1b310bed3`.
[Conditions des Build Tools 2022](https://visualstudio.microsoft.com/license-terms/vs2022-ga-diagnosticbuildtools/),
[liste Visual Studio 2022](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution)
et [liste du SDK Windows](https://learn.microsoft.com/en-us/legal/windows-sdk/redist).
L'installateur 021, SHA-256
`74691eb1dfe1519d0b405439d20f9c7e77d7a99b99a51b6546b8b3448dc3755e`,
est ouvert dans le Sandbox GPU `97ab4d22-6f69-40d2-8f90-85ac931a0cd3`.
Le précontrôle `windows-sandbox-reinstall-preflight-021.json` (SHA-256
`fd16cd06f270e2979e7f6af08568e0ea8822df405271ae09e4fff635c3773ca5`)
vérifie le binaire et reproduit le dossier résiduel du retrait 020 à côté
du projet auteur épinglé. Le Studio 021 a été installé dans ce dossier et
« Ma carte » s'affiche dans le projet rouvert. Le binaire du Studio et le CLI
installés correspondent aux preuves de build 021. Les trois fichiers auteur
sont inchangés. Reçu `R7-09-clean-windows-studio-reinstall-021.json`, SHA-256
`65099a009bf5346280d124feea88bc65b6c50509630d1a96a0383caa9d19c504`.
Cet essai couvre la réinstallation après retrait, sur un résidu reproduit,
et l'ouverture des données conservées. Dans un autre invité propre, le
Studio 020 a été installé, mis à jour vers le 021 par NSIS en mode silencieux,
puis restauré au 020 par le CLI de livraison. Les trois états ont été vérifiés
et le Studio restauré a ouvert sa fenêtre. Les trois fichiers auteur sont
restés identiques. Le bootstrap Windows conserve sa version 021 tandis que
le paquet actif revient au 020. Reçu `R7-09-windows-studio-update-rollback-021.json`,
SHA-256 `97e01abca94ea0914a70c156a5d942ac1cca10a99208af3fd78cc58048c3a40c`.
Le cycle Windows du jeu exporté est vérifié dans un Sandbox avec vGPU.
L'installateur 021, lancé à la racine d'un partage en lecture seule, a mis
« Mon projet » 020 à jour vers le 021. La sauvegarde 020 a repris dans le jeu
mis à jour, puis dans le 020 restauré et rouvert depuis `Jouer.exe`. Après
désinstallation, les paquets, `Jouer.exe` et le manifeste d'installation
sont retirés, et le SHA-256 de la sauvegarde est identique. Reçu
`R7-09-windows-game-lifecycle-021.json`, SHA-256
`dc4084e4eba8c1c2670466e65c58c42ec73fd42ae070744344fecd8bcdc77cb6`.
La qualification des distributions Linux reste ouverte.

G7-D5 : relancer une mise à jour Studio déjà activée avec le même paquet
et le même dossier utilisateur conserve désormais la révision active et
la cible du rollback. Le service vérifie de nouveau le paquet et l'état
installé sous verrou. Le test Windows refait le plan après activation,
reprend la mise à jour et revient à la révision précédente avec les
paquets et le workspace préservés. Reçu central
`studio-update-retry-preserves-rollback-20261106-001.log.receipt.json`,
SHA-256 `0573fa09709c529dc9d28d592e1a7dcc41e71f8d9ce31d3c312d534eeba8c75a`.
Cette correction du service est postérieure au 021.
Dans un Sandbox Windows, le paquet 021 a été activé par son CLI sans
copier les exécutables d'amorçage ni créer les raccourcis. Un installateur
de qualification avec le CLI et le script NSIS corrigés a repris cet état.
Il vérifie que le paquet actif correspond au sien, termine l'intégration
Windows et conserve à l'octet près la tête d'installation et le fichier
de projet. Reçu `R7-09-windows-nsis-first-activation-recovery-20261106-001.json`,
SHA-256 `cf2b87a1672fe149e51777baf01df5f2936802c501b36b8ebfa2c84a813370bf`.
L'essai reproduit l'interruption après activation de la première révision.
L'installateur Studio appelle désormais une variante de l'application du
plan qui enregistre un marqueur de reprise sous verrou avant la copie des
paquets. Le test Windows provoque un échec pendant la copie, vérifie
l'absence de tête active, puis reprend avec le paquet corrigé. La révision 1
est vérifiée et le dossier de copie incomplète est préservé. Un dossier
étranger sans marqueur est refusé. Les dix tests Rust ciblés passent, ainsi
que les huit tests NSIS, dont la compilation des deux profils.
Reçu `studio-resume-before-activation-20261106-001.log.receipt.json`,
SHA-256 `601f6e50e59fe6249d9f539781f3a6790fd742b6b974ef4c2d33bc3bf3bd1bb7`.
Ce premier contrôle couvre le service appelé par NSIS.
L'installateur historique Gate 4 `OpenRPG-Forge-Setup.exe`
à la racine du workspace a été rapproché de son reçu 019 par SHA-256
`4573abba6f8471991b913855d85f9122dcf04029d04853c71e64d3f57fb6d127`.
Dans un Sandbox, ce 019 s'arrête sur le Windows vierge avant de publier
sa tête. L'installation aboutit après mise à disposition du runtime
Microsoft Visual C++ x64 dans l'invité. Le Studio 021 installé à côté
réutilise le `UserData` par défaut. Les 89 fichiers de l'espace auteur
historique (144 150 023 octets), un fichier de sauvegarde témoin et les
trois manifestes de paquets privés conservent leurs empreintes. Le Studio
ouvre sa fenêtre et le verrou du workspace historique est occupé.
Reçu `G7-D5-windows-legacy-019-studio-021-coexistence-20261106-001.json`,
SHA-256 `94258ad6481d968b63fe73f9e112c005e5cd7cb7a363fe1581db77e4e9e158b1`.
Cette preuve couvre la coexistence et la reprise de l'espace auteur par
défaut. La reprise en jeu d'une sauvegarde historique, la réouverture
visuelle d'une carte, la reprise des ressources de référence et le retrait
du Hub restent à qualifier.
La source de l'installateur Studio reprend désormais, à sa première
installation, le dossier utilisateur d'une installation Forge historique
valide enregistrée dans Windows. Une destination fournie explicitement
est prioritaire à la première installation. Les mises à jour conservent
le dossier déjà épinglé et une entrée historique orpheline permet le
parcours neuf. Le test Windows appelle le CLI de l'installateur et applique
le plan sur une installation historique isolée. Le projet et la tête
historique conservent leurs empreintes. Onze tests Rust et huit tests
NSIS passent, dont la compilation des deux profils.
Reçu `studio-migrate-custom-user-root-20261106-001.log.receipt.json`,
SHA-256 `571195fa7575d3e457c0ef5aab9de10a2f1a52a452c8666662280eabb777e495`.
Un essai combiné du 6 novembre valide le parcours NSIS complet dans un
Sandbox. Une petite fixture historique v1 enregistre un dossier
utilisateur personnalisé. L'application d'un paquet de test corrompu
échoue après avoir créé le marqueur et le dossier de copie, avant la
publication de la tête. L'installateur de qualification, qui réutilise le
paquet Studio 021 avec le CLI et le script NSIS corrigés, reprend cet état.
Il lit l'enregistrement Windows, active la révision 1, conserve le
dossier personnalisé et laisse les empreintes de la tête historique,
du fichier auteur et de la sauvegarde témoin identiques.
Reçu `G7-D5-windows-nsis-custom-root-before-activation-20261106-001.json`,
SHA-256 `bde9c87b624397ee161c113cccbf833084ee9b274e058e9f3dde4fd5fb0f26d8`.
Le test de dossier personnalisé emploie une installation historique
synthétique. Le reçu 019/021 couvre séparément la coexistence réelle
avec le dossier par défaut.
Les sauvegardes réellement produites par le runtime 019 pour Les Trois
Cairns 1.0.1 et Le Relais des Lanternes 1.0.0 ont été relues depuis
l'archive Gate 4. Le décodeur actuel accepte les deux sauvegardes avec
leurs modules exacts en mode strict et les réencode à l'octet près.
Reçu `G7-D5-legacy-019-native-save-readback-20261106-001.json`,
SHA-256 `f8c388cf5ab361bae9983b59bb75ecbd2d16fe7e24935a012be957d16b027dad`.
Ces enveloppes identifient le module, la zone et les empreintes source
et fonctionnelle. Le lien avec l'identité du jeu exporté doit être
vérifié via le module avant de transférer les données de `UserData/saves`
vers l'espace de sauvegarde du jeu. Ce contrôle CPU établit la
compatibilité des octets.
La commande native `import-legacy-save` relie les deux têtes
d'installation au projet, au module et à la version exacte de la carte.
Elle copie le document validé vers un emplacement libre du jeu exporté,
sous les verrous de livraison et de sauvegarde. Une relance avec la même
sauvegarde conserve l'empreinte et une sauvegarde cible différente est
préservée. Le test Windows confirme l'isolation des projets et l'état
`Ready` dans la vue native utilisée par le launcher. Reçus
`legacy-save-import-project-isolation-20261106-002.log.receipt.json`
(`445ea752b4d5fb3087a718f639595a3b64fbd7655e3721ca8c344fdd071af93c`) et
`legacy-save-import-cli-build-20261106-001.log.receipt.json`
(`59c3a9967d6f3ba7801647147edcc9e6206f50bf8c609687f5400d94414abd4a`).
Le launcher Windows propose aussi la recherche dans « Réglages » lorsque
l'ancienne installation Forge est enregistrée. Le joueur choisit sa
carte et l'action d'import apparaît après vérification de la sauvegarde.
L'empreinte vérifiée est contrôlée à nouveau lors de la copie. La vue
« Reprendre » est actualisée après l'import. Le test du frontend couvre
le refus, la sélection et le focus, puis l'activation de « Reprendre »
sur réponses simulées. Le contrôle UX heuristique relève zéro
avertissement. Le test Rust vérifie le refus d'une empreinte changée et
la vue native après copie. Le contrôle Windows du launcher
`--features standalone` passe.
Reçus `launcher-legacy-save-pinned-review-20261106-001.log.receipt.json`
(`550f2ce8fbb73efd4d0c758e69c303f4e570eba098197f07e1783e3c28cd383c`) et
`launcher-legacy-save-windows-standalone-check-20261106-001.log.receipt.json`
(`79fb890cb9cc06ce2e1db19a26a78e6ba7ae8f254f42b261da9f2830050b1b31`).
Le launcher installé des Trois Cairns a importé la sauvegarde 019 archivée
depuis le dossier historique, puis le jeu a repris la quête terminée, la
récompense et l'endurance à 90 dans Windows Sandbox avec GPU. L'original et
la copie importée ont le même SHA-256. Le candidat privé a été assemblé avec
le module archivé et une structure d'installation historique reconstituée.
Reçu `G7-D5-windows-launcher-legacy-save-resume-20261106-001.json`,
SHA-256 `29dd56506db6f1005b49b8a9d206431327c342ec40f9e749b306f9ac94009726`.
Pour les cartes historiques, le nouvel installateur Studio reprend le
paquet `reference-assets` de l'installation v1 enregistrée lorsque le
même dossier utilisateur est utilisé. La copie transitoire est contrôlée
par le plan de livraison. Le paquet installé conserve son manifeste et
ses droits privés. Le Studio utilise `data/scene/static-assets.json`
avec l'empreinte du manifeste et résout les modèles avec le lecteur
existant, y compris après retrait de l'ancienne installation. La copie
de ressources appartient à l'installation Studio. Sa désinstallation
la retire et préserve le workspace utilisateur.
La lecture du paquet original 019 a vérifié 8 138 fichiers, 496 953 319
octets et les quatre entrées du catalogue. Reçu
`G7-D5-legacy-019-reference-assets-readback-20261106-001.json`, SHA-256
`26e35cb4f4b210bbdce65154365735c0ff6a63ad8942eac4e94672f312aa1db4`.
Le test natif Windows sur fixture couvre la mise à jour, la relance après
retrait de l'installation v1, le retour arrière et la remise à niveau,
le refus d'un catalogue altéré et la désinstallation avec conservation du
workspace. La suite donne 108 tests réussis et deux ignorés. Reçu
`studio-reference-models-migration-20261106-004.log.receipt.json`, SHA-256
`1bfe99abfda74ad2d2a9d8b8e1c1175016aa438e439147d8b58a6aa3d6145174`.
Les paquets d'origine du 019 donnent 21 placements actifs et 10 empreintes
distinctes sur `Tidewater island` (`e620dd19-ead9-5913-a6d6-0f93109148c0`).
Quatre épinglent les octets des GLB du catalogue de scène. Six épinglent
des descripteurs du catalogue `data/buildings/catalog.json`, dont la lecture
relève du lecteur commun `forge_assets::source_surface_package`.
Inventaire `G7-D5-legacy-019-tidewater-model-pins-20261106-001.json`, SHA-256
`076196ac461bc58f2dc5f944aeeb790e1465cd72231129a1faf01802004c77bd`.
Dans un Sandbox avec GPU, le service d'installation a repris les paquets
exacts du 019. Le candidat réutilise le binaire Studio 021 et ajoute le
paquet de référence aux paquets autorisés. Les deux documents auteur
conservent leurs empreintes après migration et désinstallation du 019.
Reçu `G7-D5-windows-studio-021-real-019-migration-20261106-001.json`, SHA-256
`fccc43b6647ff9d5e015d6a25501db9c34a1af3aa6c2b843a3e921cc79e80e11`.
La sélection de `Tidewater island` ouvre le document et sa hiérarchie, mais
la vue 3D reste vide. Le journal signale `The module omitted its authored
vegetation companions`. Le module géré historique n'a pas de pin d'origine
`.orpmod`. Le service de ressources répond sans description dans ce cas,
alors que la carte déclare une source végétale importée. Les dix blocs de
65 383 instances sont présents aux empreintes attendues dans les données
auteur du paquet 019. Il faut raccorder les instances et leurs prototypes
à la description de ressources du lecteur commun. Reçu et capture associés
`G7-D5-windows-studio-021-real-019-open-20261106-001.json`, SHA-256
`e177ef5dd68b8f1ca60639bd1ffd3b502283a6d6e1f56207d9004a701da`.
Le Studio peut désormais décrire les ressources végétales d'une carte
gérée sans origine `.orpmod` depuis le paquet de référence installé.
Le lecteur commun reconstruit l'index depuis le profil historique,
vérifie que son relief correspond à la carte et contrôle chaque ressource
contre les empreintes du profil et du paquet. Le test avec les paquets
originaux du 019 a transmis 84 ressources (46 603 250 octets, index
compris) et décodé les six groupes de géométrie. Reçu
`G7-D5-real-019-legacy-plant-acquisition-20261106-001.json`, SHA-256
`2cc28fbb2e3e5fa73cdef547efece9dc318b61da6bd8e8f87b4b3b7a3b4d2809`.
Les 24 contrôles ciblés passent, dont le refus d'utiliser la voie
historique pour un module qui déclare déjà une origine.
Le Studio 022 et son CLI ont été reconstruits sur les mêmes sources
stables. L'essai réemploie le paquet d'application 021 avec le binaire
Studio remplacé et le paquet `reference-assets` autorisé. Dans le Sandbox
avec GPU, les documents historiques conservent leurs empreintes après
migration et désinstallation du 019. `Tidewater island` ouvre sa
hiérarchie. À 188 s, la vue attend toujours « Chargement des ressources
du module… » et aucune première image 3D n'a été observée. Les relevés
de processus bornent l'observation, sans établir la cause.
Reçu `legacy-019-studio-vegetation-20261106-022/reprise/G7-D5-windows-studio-022-real-019-viewport-20261106-001.json`,
SHA-256 `77ca83264425209aaecb5ff515145c2317a262c036782cdec1cd5c61dfadfa2c`.
Le Sandbox est arrêté. Le paquet temporaire 022 de 342 691 022 octets
a été retiré après conservation du reçu. Le chargement WebView, le
rendu, le réexport complet et le retrait du Hub restent à qualifier.
Le diagnostic du binaire 022 dans un Sandbox hors ligne a ouvert le
Studio, mais WebView2 n'a pas exposé le port de diagnostic local attendu.
La cause exacte de l'attente de 188 s reste donc indéterminée. Le Studio
transporte désormais les blocs de ressources via l'IPC binaire Tauri,
comme les modèles GLB. Il conserve les contrôles de tête, de paquet,
d'ordre et d'empreinte. Une réponse attendue plus de 30 s produit une
erreur identifiant la description ou le bloc et son offset. La lecture
JSON reste disponible pour les consommateurs existants de la commande.
Les 22 tests natifs ciblés et les compilations WebAssembly et Studio
Windows passent. Reçus `studio-module-resource-binary-native-20261106-002.log.receipt.json`,
`studio-module-resource-binary-wasm-20261106-001.log.receipt.json` et
`studio-module-resource-binary-desktop-20261106-001.log.receipt.json`.
Le Studio 024 intègre ce transport. Lancé directement dans un Sandbox Windows
hors ligne avec vGPU et les octets vérifiés du 019, il a affiché la hiérarchie
de Tidewater (32 entrées). À 206 s, la vue affichait encore « Chargement des
ressources du module… », sans première image ni erreur visible. Les deux
empreintes des documents auteur ont été vérifiées après l'ouverture. Reçu
`legacy-019-studio-binary-ipc-20261106-024/G7-D5-windows-studio-024-real-019-viewport-20261106-001.json`,
SHA-256 `0c570e5532e3d1059d2a625aeb477437bc3156e8fc8c14937cab84a4f625f697`.
Le Sandbox a été arrêté et la copie d'essai de 317 819 376 octets retirée.
Une acquisition terminée en erreur réveille désormais aussi la vue en attente
afin qu'elle affiche l'erreur et masque le chargement. Cette correction passe
la compilation WebAssembly (`studio-module-resource-failure-viewport-wasm-20261106-001.log.receipt.json`).
Le Studio 025 intègre cette remontée. Dans un Sandbox avec vGPU, le même
document affiche encore « Chargement des ressources du module… » à 91 s,
sans première image ni erreur de la vue. Une relance du même binaire avec
le journal WebView2 demandé n'a produit ni fichier de journal ni argument
de journalisation observé sur ses processus. Les documents auteur ont
conservé leurs empreintes. Reçu
`legacy-019-studio-loading-error-20261106-025/G7-D5-windows-studio-025-real-019-viewport-20261106-001.json`,
SHA-256 `4e289a5d001ff30150ebf7a2a777e4b3ab7c86765bfcba494a0a892647305f2b`.
Le Sandbox a été arrêté et la copie d'essai de 317 801 395 octets retirée.
La vue affiche désormais des étapes distinctes pour l'inventaire, le transfert,
la préparation et la remise des ressources à la projection. Compilation WebAssembly
vérifiée (`studio-module-resource-milestones-wasm-20261106-001.log.receipt.json`).
Ces étapes restent à observer dans le Studio pour situer l'arrêt du 019.
Les six empreintes historiques du catalogue `data/buildings` sont maintenant
raccordées au paquet de surfaces commun. Le service natif lie ce paquet à
une carte historique qui référence au moins une de ses identités exactes,
contrôle sa fermeture contre l'inventaire installé et conserve la licence
du paquet. Le test avec le workspace et les ressources originaux du 019
décode 906 fichiers (102 543 563 octets), les six groupes de végétation
et les six modèles épinglés par les placements courants. Reçu
`studio-legacy-019-surfaces-native-20261106-001.log.receipt.json`,
SHA-256 `9ec078ea845f017bf48dc1812cc1e08835328f7fc035e08d7187d779d5681227`.
La compilation WebAssembly passe (`studio-legacy-019-surfaces-wasm-20261106-001.log.receipt.json`).
Le chemin de projection utilise le même hôte de surfaces que les modules
reçus. La preuve de rendu et la capture de ces ressources dans le réexport
autonome restent à réaliser.
Le Studio 026, reconstruit sur entrées stables, ouvre les 32 entrées de
Tidewater avec les paquets vérifiés du 019. Le chargement se termine par
« Source objects require their admitted live environment and surface host »
dans la vue et le journal. Le moteur refuse d'installer les surfaces avant
leur hôte d'éclairage. Les deux documents auteur conservent leurs empreintes.
Reçu `legacy-019-studio-surfaces-20261106-026/G7-D5-windows-studio-026-real-019-viewport-20261106-001.json`,
SHA-256 `3bb194722ff162a2a5e7963b2b4dcc15c576e0e1a7ac4e760e92e0844359593a`.
Le Sandbox est arrêté. La copie vérifiée sur E: reste utilisée pour le contrôle
de l'environnement historique, construit à partir de `data/scene` et
`data/clouds` et transmis au décodeur commun.
Le contrôle natif avec `scene-host-tests` passe sur les fichiers originaux du
019. Il lit 1 173 fichiers (105 252 515 octets) et exécute le décodeur du moteur
sur l'environnement, dont les 120 étapes des nuages. Reçu
`studio-legacy-019-environment-native-20261106-001.log.receipt.json`, SHA-256
`fd9209b397ac7a7694f1d832c6a3b4caa501750701c1fd3eafe4bda0ba934545`.
Le Studio 027 est reconstruit sur entrées stables. Dans le Sandbox avec vGPU,
« Ressources du module prêtes pour la projection » apparaît au plus tard à
47 s. À 170 s, le canevas reste vide et le journal affiche toujours ce statut.
Les deux documents auteur conservent leurs empreintes. Reçu
`legacy-019-studio-environment-20261106-027/G7-D5-windows-studio-027-real-019-viewport-20261107-001.json`,
SHA-256 `3a36dd76bf699285229addc9370603ae27a858d786107e7d9c034809473a92f6`.
Le Sandbox est arrêté. La copie vérifiée reste en usage pour le diagnostic
du démarrage de la projection avec le même frontend. Le réexport autonome
reste à qualifier séparément.
Le même frontend 027 a été revérifié puis embarqué dans un hôte de diagnostic
avec DevTools. WebGPU sélectionne `BrowserWebGpu`. La première erreur moteur
est une validation ECS Bevy 0.19.1, `Resource does not exist`. Les noms du
système et du paramètre sont absents de ce build. Les erreurs `unreachable`
et `RefCell already borrowed` suivent ce premier arrêt. Reçu
`legacy-019-studio-environment-20261106-027/G7-D5-windows-studio-027-webgpu-diagnostic-20261107-001.json`,
SHA-256 `c304b163a41a1f3e3cfdadde3a0f01f33e17ecec542213651c9c076706f85804`.
L'invité de diagnostic a été arrêté. Les sections exécutables du WASM avec
symboles correspondent octet pour octet à celles du 027. La pile situe
la validation dans le cycle ECS de `Main::run_main`, à l'appel des étapes
récurrentes. Elle ne donne pas le nom du système qui échoue avant exécution.
Carte d'adresses `legacy-019-studio-environment-20261106-027/webgpu-panic-symbol-map.txt`,
SHA-256 `c9c37b5cbb29762160f4d1c7c0e419252a67764ca49c025dc5ba65e02609b6ac`.
Les 307 737 672 octets temporaires de symbolisation ont été retirés.
Le Studio 028 installe le plugin moteur des matériaux source dans la vue
terrain échantillonnée et enregistre les systèmes GPU des effets après la
création de la vue de rendu. Les builds WASM et Windows passent sur entrées
stables. Dans le Sandbox avec vGPU, les 32 entrées sont visibles et les
ressources annoncées prêtes, mais la première image manque encore à 182 s.
Les empreintes des deux documents auteur correspondent aux originaux.
Reçu `legacy-019-studio-source-graph-20261107-028/G7-D5-windows-studio-028-real-019-viewport-20261107-001.json`,
SHA-256 `a8f7b60f49fd12c7a63168c6f68a43eeee8f6be676e6d0219270e360d9941eb6`.
Le Studio 029 avec `web,bevy/debug` identifie la première erreur à 84 s
dans le Sandbox vGPU. Le système moteur `vegetation_prototypes::wind::prepare_wind_cache`
échoue avant exécution sur `Res<VegetationPresentation>` absent. Le
chargeur des prototypes était déjà installé pour permettre une sélection
future dans le Studio, mais le profil, le champ d'interaction et les
matériaux associés n'étaient installés qu'en présence de modèles déjà
sélectionnés. Reçu `legacy-019-studio-bevy-diagnostic-20261107-029/G7-D5-windows-studio-029-named-ecs-20261107-001.json`,
SHA-256 `deb60e592f7e6eed102c651018368590ce9d7a9d2738dd0903c94b1c5334ac68`.
Les deux documents auteur gardent leurs empreintes. Le Sandbox est arrêté.
Les trois fichiers temporaires du frontend de diagnostic (176 361 646 octets)
ont été retirés après conservation de la preuve. Le candidat 030 fournit au
chargeur le profil de végétation déjà enregistré dans la carte importée,
via l'initialisation commune du moteur. Le Studio 030 est reconstruit sur
entrées stables. Dans le Sandbox avec vGPU, Tidewater affiche le terrain,
la végétation, les bâtiments et les ombres avant le relevé à 104 s. Le
bouton « Rapprocher » déplace la caméra. Les deux documents auteur gardent
leurs empreintes. Reçu
`legacy-019-studio-authored-wind-20261107-030/G7-D5-windows-studio-030-real-019-viewport-20261107-001.json`,
SHA-256 `38a385ffee891f5a6a2e6684e23e980539b8114b58cf39d9a7f0302452308d7e`.
Ce relevé couvre l'ouverture visuelle et la caméra. Le Sandbox est arrêté.
L'export privé des cartes gérées sans origine `.orpmod` utilise désormais
le même lecteur des ressources historiques que le Studio. Il prépare les
liaisons par carte, vérifie chaque octet avant assemblage et conserve
l'inventaire de provenance et les notices. Les GLB historiques de collision
triangulée suivent le catalogue gouverné du jeu exporté avec les mêmes
identités et empreintes. Le fichier original `PROVENANCE.md` du paquet 019
(350 octets, SHA-256 `6e510e0ff2d16cb130ef086af67f10a17c6a4fbabdb76bfeed3c607445699b79`)
a été ajouté à la fixture active sans extraire une seconde copie du paquet.
La bibliothèque native de livraison compile avec ce raccordement. Reçu
`studio-029-private-reference-delivery-native.log.receipt.json`,
SHA-256 `87132480462b32d4668aa571b6acd08138bb8a6b5536ae0242da092968ccfa13`.
L'hôte de diagnostic 029 compile également avec l'export raccordé. Dans un
Sandbox séparé, la carte possède déjà ses règles de jeu. Le bouton
« Créer le fichier .orpmod » en mode privé refuse deux empreintes pour
l'identifiant `1aedd6b3-b7d3-5430-a3ca-6cd4808e1eaa`. Le journal auteur
référence six anciennes révisions de quatre GLB. Les six fichiers
(31 272 396 octets) ont été retrouvés et vérifiés par SHA-256 dans deux
archives de qualification, sans extraire de copie. Aucun paquet n'a été
produit et les empreintes des documents auteur sont identiques. Reçu
`legacy-019-studio-bevy-diagnostic-20261107-029/G7-D5-windows-studio-029-legacy-private-export-20261107-001.json`,
SHA-256 `166c53c99a2acd48dcff1648a63d8bb4f5768176d2aeb28c3b4b22e547b7a4d4`.
L'API 14 du paquet privé ajoute un inventaire de modèles d'historique
par couple identifiant et SHA-256, avec fichier GLB et licence épinglés.
Le catalogue de jeu continue à exiger la révision utilisée par la carte
active. Le constructeur sait transporter les anciens GLB admis. Le Studio
sait les lire par épingle depuis le paquet d'origine du module ouvert.
Les 58 tests du contrat passent, dont la distinction entre révision active
et historique. Reçu `studio-031-historical-project-contract.log.receipt.json`,
SHA-256 `7e9abf643f4d9aa25bb20d5fb636b866e2291943a092ad4b7193d61124650c9c`.
Les 31 tests de transport exécutés passent, avec deux fixtures externes
ignorées. Reçu `studio-031-historical-delivery-contract-r3.log.receipt.json`,
SHA-256 `f1678373633d5a24ea957cb79cd64f845d1d0db78cfda141c7d5dca22875755b`.
Le lecteur Studio compile. Reçu `studio-031-historical-studio-reader-check-r2.log.receipt.json`,
SHA-256 `02cc41b4934f2c323e463f0f60c6b7f36672f130e873f86bf23a4c9e8834237f`.
L'export Studio distingue désormais les modèles de la carte active des
révisions présentes dans le journal auteur. La qualification privée du 019
utilise les six GLB (31 272 396 octets) extraits par empreinte des deux
archives déjà vérifiées. Le manifeste du complément est épinglé au
SHA-256 `24a6fd25663be262916dedc2a0e2e38041b357922716662c7a5f23ca3fe86192`
et lié au manifeste `reference-assets` du 019. Le contrôle de compilation
du Studio passe. Reçu `studio-032-historical-export-studio-check.log.receipt.json`,
SHA-256 `3ba5a5b91b9ce7a586c27bdb8e15070a9bd1d8f2aac30e5a3a31d3cc60123bea`.
Dans Windows Sandbox avec GPU, le Studio natif 032 et l'interface 030
ont exporté la carte du 019 en `.orpmod` privé. L'inspection du paquet
confirme l'API 14, le gameplay d'origine, les quatre modèles actifs et
les six révisions historiques par identifiant et SHA-256. Les deux
documents auteur conservent leurs empreintes. Le paquet de 189 146 663
octets reste disponible sur F: pour la qualification du jeu. Reçu
`legacy-019-model-history-20261107-032/G7-D5-windows-studio-032-legacy-private-export-20261107-001.json`,
SHA-256 `64c18aa9e6d8946119c0f2939c36ad387b8947567f1d5ddf83cf05d16b697bae`.
Le chargeur commun `forge_delivery module-check` a admis ce paquet et ses
onze sources de zone. Les empreintes de la tête et de l'archive correspondent
au reçu Studio. Reçu `editor-launcher-windows-candidate-20261107-033/G7-D5-windows-tidewater-032-runtime-admission-033.json`,
SHA-256 `feefa188cfc5e6e00f2244b10c2f3fe512b9b8736c90097d258d5715e36bfa6f`.
Le runtime Windows 033 construit sur des entrées stables a lancé ce paquet
sur la Radeon RX 7800 XT. La zone, les bâtiments et la végétation ont été
projetés. Le contrôle de scène échoue dans le HLOD statique sur une primitive
transparente et la disponibilité du gameplay alterne pendant l'observation.
Reçu `editor-launcher-windows-candidate-20261107-033/G7-D5-windows-tidewater-032-runtime-033.json`,
SHA-256 `8c22b6096052e7b917e8ae3a523d39049fc0bbe3c585fcb400adb80075babb77`.
Le moteur conserve désormais toutes les primitives d'un objet transparent
dans son rendu détaillé et autorise l'agrégation des autres objets éligibles.
Le test de régression passe avec un objet à primitives opaques et transparentes
à côté d'un objet agrégeable. Reçu `engine-034-static-hlod-retain-blended.log.receipt.json`,
SHA-256 `08a503c6438de76bfbf3f39073042bc8b4b4a9a992efc290fe9793a5cff256fc`.
Le runtime et le launcher Windows 034 ont été construits sur le même
inventaire. Le contrôle des notices retrouve la fermeture Cargo du runtime
et les 298 dépendances du launcher. Reçu
`editor-launcher-windows-candidate-20261107-034/R7-08-windows-notice-preflight-034.json`,
SHA-256 `89a63283d6b8a7bc85ce8d84fc0268295b81a33a0f745669b2ffdbb07dd5db60`.
Le runtime 034 capture le village de Tidewater et couvre ses 407 primitives
statiques sans erreur. Après la trame 200, seules 99 trames sur 400 présentent
une projection prête, alors que le joueur reste au même endroit. L'attente
de la simulation reste intermittente. Reçu
`editor-launcher-windows-candidate-20261107-034/G7-D5-windows-tidewater-032-runtime-034.json`,
SHA-256 `c84d0ef983b00939ad2ea4b0bd54f8a53db95eac2ac4a1bf81b72a18e9284353`.
Un objet mobile conserve maintenant la réservation du terrain quand sa
projection physique est temporairement fixe. Cela évite de retirer les
collisions nécessaires à sa propre réactivation. Les deux tests ciblés
passent, dont la conservation du sol d'un objet distant pendant le blocage
et après la reprise du gameplay. Reçu
`engine-035-streaming-reserved-ground-regression.log.receipt.json`,
SHA-256 `15ca7e92d7ef934d0b51e5df3d7540c8546e99551d5530634a88b27652103da1`.
Le runtime Windows 035 a joué le paquet du Studio 032, sauvegardé puis
repris la partie par son empreinte. Les deux sorties et les deux projections
finales réussissent. Le démarrage repris retrouve l'état sauvegardé exact,
et la sauvegarde conserve le SHA-256
`22d90f78e67697520bc2c4fd1630ad2619c5c389fa760166ab0a962c51f60a5e`.
Les traces conservent neuf trames indisponibles après la première projection
prête dans chaque session. La performance et le parcours depuis le launcher
ne sont pas qualifiés par cet essai natif. Reçu
`editor-launcher-windows-candidate-20261107-035/G7-D5-windows-tidewater-032-native-save-resume-035.json`,
SHA-256 `b577adf3c8816a3b4bf3ce6f1905f6968ae77a4ddd0808943fd001991a0473cd`.
Le kit Windows 035 rassemble 473 fichiers et 141 736 607 octets. Son reçu
`editor-launcher-windows-candidate-20261107-035/game-kit-assembly-receipt.json`
a le SHA-256 `c14164577ce25d6fc92abdbc81aeb23b9fa2e8ea2e9501cc80118fb37ca4af54`.
Le Studio 035 est construit avec ce kit dans le checkout commun, SHA-256
`8469bf3bded03a1a2355dcba6341a19d13ccb3b74a2d01ae650f51cd917c6873`.
Dans le Sandbox `d28da61f-aa68-415e-9ae9-da0314a99891`, son interface a
configuré puis sauvegardé le projet Tidewater historique (carte d'origine,
qualité Élevée) et créé le jeu privé avec son installateur. Les quatre
fichiers exportés totalisent 350 782 604 octets, conservés une seule fois
sur F: dans `editor-launcher-windows-candidate-20261107-035/tidewater-game-export-035`.
Le reçu `studio-tidewater-export-035.json` conserve leurs SHA-256 et celui
du workspace sauvegardé. L'installation dans le dossier utilisateur de
l'invité a terminé. Le launcher a lancé la carte d'origine, F5 a sauvegardé
la partie, puis le bouton « Reprendre » l'a rouverte après fermeture propre.
Les deux rapports gameplay et runtime réussissent, avec 21 objets manuels,
399 primitives enregistrées et zéro pipeline en échec ou en attente en fin
de session. La sauvegarde conserve le SHA-256
`56715da329f5d2437636eb603365cb2a0148734e78c69771a007ad28d5c14f46`.
Reçu `G7-D5-windows-studio-tidewater-launcher-save-resume-035.json`, SHA-256
`f6846b282993de3cd96f9f60d09fbe770cfb0d074f040d7cdc24048c2378db6b`.
Ce contrôle valide le parcours produit privé dans cet invité, qui contient
aussi le Studio et les montages source en lecture seule. Il ne qualifie
ni un invité joueur seul, ni les performances, ni les visuels de présentation.
Le retrait du Hub reste à qualifier.
Le lot `presentation-20261107-036` raccorde les trois visuels du projet à
la collection d'imports gouvernée. Le paquet conserve les images exactes,
leur provenance, leurs licences et les sources/parents nécessaires aux
retouches ; il reste privé. Le launcher affiche ces images après admission
du paquet et applique l'icône à sa fenêtre. Sept tests ciblés et le check
WASM passent. Reçu `implementation-checks-036.json`, SHA-256
`440cc93a8b9a440a55e7040291c998bdd7f661d5e77e1d37fe3d66b911a97804`.
Le launcher, le runtime, l'outil de livraison et le frontend Studio 036 sont
construits sur ces mêmes sources stables. Le kit privé 036 est assemblé
dans `E:/OpenRPG_Forge/.build/tmp/gate7-windows-game-kit-presentation-036`
(473 fichiers, 141 908 127 octets). Les quatre graphes launcher affectés
ont été recalculés hors ligne : leurs ensembles de dépendances restent
identiques au bundle de notices 015 réutilisé sans copie. Les nouveaux reçus
`notice-graph-036.json` et `notice-candidate-036.json` conservent la filiation
des preuves historiques ; ils ne qualifient pas une redistribution publique.
Manifeste candidat `presentation-20261107-036/source-inputs.json`, SHA-256
`fed311ec736c65128c0a64d0c6b7817006e58db449798a089c815239ef659618`.
Le snapshot diagnostic précoce `source-inputs-036.json`, remplacé après les
corrections, a été retiré : 2 030 989 octets. Les manifestes canoniques et
leurs reçus sont conservés. Le Studio 036 est construit avec ce kit,
SHA-256 `bd2d276711b27cd02b4e2afc5a978d0e6a6a808113b6de7daf821937ec83aae0`.
Son interface a sélectionné les trois visuels, examiné/appliqué la
modification puis sauvegardé et réexporté le projet Tidewater en 1.0.1.
Le workspace conserve les mêmes références de carte ; seuls icône, logo
et fond ont changé. L'installateur a mis à jour le jeu 1.0.0 existant.
Le launcher affiche les trois images et son icône native. Les sources de
retouche, le parent de la variante et la preuve de licence sont présents
avec leurs octets exacts dans le paquet installé. « Reprendre » lance la
sauvegarde conservée ; les rapports gameplay et projection réussissent
et le runtime se ferme proprement. Le SHA-256 de la sauvegarde reste
`56715da329f5d2437636eb603365cb2a0148734e78c69771a007ad28d5c14f46`.
Reçu `G7-D4-windows-presentation-update-resume-036.json`, SHA-256
`ee8a578f147c819b51de33dce109d9182c56a412bbd5d7e11de1c5c41ca488e0`.
Les quatre fichiers exportés sont conservés une seule fois sur F: dans
`presentation-20261107-036/tidewater-game-export-036`, 351 130 232 octets.
La copie de travail de l'invité a été retirée après vérification SHA-256
et contrôle des processus : `export-copy-retired-036.json`.
Ce contrôle qualifie la présentation et la reprise après mise à jour du
jeu privé. La préparation du binaire Studio dans cet invité n'est pas une
qualification de son installateur. L'invité joueur seul, les performances,
les licences de redistribution, Linux et le retrait du Hub restent ouverts.
Le même Sandbox reste disponible ; le Studio 036 (PID 7276) est ouvert
sur le projet sauvegardé, le jeu et l'installateur sont fermés.
L'installateur autonome du Studio 036 est disponible dans la même campagne.
`OpenRPG-Forge-Studio-036-Setup.exe` fait 444 344 632 octets et porte le
SHA-256 `7385eeb067fc584214869d771166ac091bd6f9199f0e3205dfe1e82332ffae3e`.
La recette `studio-only` reprend les binaires 036 et leur kit vérifié. Les
reçus, l'index et le manifeste sont dans `studio-installer-036/`.
`studio-notice-scope-audit-036.json` retrouve 336 dépendances natives et 596
Web avec les notices existantes. Trois cas de notice Web restent ouverts.
La préparation porte le verdict `BUILD_ONLY`. La migration depuis une
installation historique et la qualification en environnement propre restent
à vérifier sur le candidat final. L'assemblage temporaire sur E: a été
retiré après contrôle du Setup, des empreintes et des processus. Le reçu
`studio-installer-036/staging-retirement.json` totalise 356 130 420 octets
retirés. Le kit reste disponible pour le Studio de qualification.

Pour le retrait du Hub, reprendre la matrice D1 avec les preuves 035/036/039.
Le launcher joue les contenus déclarés et l'installateur du jeu utilise
le service de livraison commun. L'import `.orpmod` est disponible dans
« Projet et jeu ». Le Studio 039 a ajouté Tidewater à l'espace où Le Grand
Plateau était déjà sauvegardé. Les deux modules réapparaissent après relance,
avec les origines exactes et le premier projet inchangé. Le reçu Windows
`module-import-20261107-038/G7-D3-windows-studio-module-import-039.json`
porte le SHA-256 `22e15c3c4726459894ed46ba1e38bf7886a252b0802cea4450c8584e808b4147`.
L'espace auteur de cet essai reste sous
`E:/OpenRPG_Forge/.build/qualification-data/gate7/module-import-20261107-038/workspace`
pour le prochain export de qualification. Les recettes
historiques `build_delivery.py`, `build_classic_installer.py` et
`repair_installed_launcher.py` portent encore l'ancien Hub. Les suites
Linux `hub`/`shells` et les scripts Hivemind de mise à jour d'une ancienne
installation restent à rapprocher des chemins cibles avant retrait.
Le Setup Studio appelle `recover-legacy-modules` après activation et avant
l'inscription des raccourcis, si l'ancien produit enregistré et le Studio
utilisent le même dossier utilisateur. Les modules actifs et de retour
arrière sont vérifiés puis conservés sous `recovered-modules`. La relance
réutilise les archives valides. Les contrôles Windows et NSIS ciblés passent
sur fixtures. Reçu `G7-D5-legacy-module-recovery-040.json`, SHA-256
`5f9938216a23f4093e1c83eb6305319cab2e753abae4e612323f645dae54cb41`.
Le launcher peut maintenant vérifier les sauvegardes historiques avec les
modules récupérés après le retrait. Le CLI `import-recovered-save` accepte
aussi un profil utilisateur conservé fourni explicitement. Le contrôle
Windows reprend les octets réels de la sauvegarde des Trois Cairns 019
après retrait d'une installation historique reconstruite. Le module et
la sauvegarde source conservent leurs empreintes. Reçu
`G7-D5-windows-recovered-cairns-save-041.json`, SHA-256
`7e6c6dfed57ce51de8a8db3e08e22694c428e4033c0a29b24a3d99fc4261b1a5`.
Le launcher installé a ensuite trouvé, importé et repris cette sauvegarde
dans un Sandbox Windows joueur seul, sans Studio ni SDK. La quête terminée,
la marque de la gardienne et les valeurs 100/90 sont revenues en jeu. Les
deux empreintes restent identiques après fermeture. Reçu
`windows-recovered-save-20261107-041/G7-D5-windows-clean-player-recovered-save-041.json`,
SHA-256 `c5427f3e547c55bfb92e2c16313d0bd6040ba3e62939d04abb25cdb62b4a3b4b`.
L'installation du jeu passe par le service natif et la tête historique
est reconstruite avec les vrais octets 019.

La migration réelle entre les Setup NSIS 019 et Studio 042 a ensuite
réussi en Sandbox Windows hors ligne. Le CRT Microsoft signé a été
installé dans l'invité pour le 019. Le Setup Studio reprend le profil et
la révision originale de `reference-assets`, puis récupère le module
Cairns et l'ancien module de référence avec leurs droits privés.
Après désinstallation du 019, la tête, l'enregistrement et le raccourci
du Hub sont absents. Les 90 fichiers auteur de la fixture 036, la
sauvegarde originale et les deux archives récupérées sont inchangés.
Le binaire Studio 042 démarre depuis le lanceur installé et son empreinte
correspond au build. La réouverture visuelle de la carte 042 reste à couvrir.
 Reçu
`windows-recovered-save-20261107-041/studio-migration/G7-D5-windows-nsis-019-studio-042-migration.json`,
SHA-256 `0b8ee629361c69a67db4a167209a5a3e04d511c7affcbfa2c0cff5f6cd882c5f`.
Le Sandbox est arrêté. Le retrait de la cible et des recettes est consigné
dans le lot 048 ci-dessous.

Le 043 exige le mode `legacy` pour reproduire l'ancienne livraison
avec `build_delivery.py` ou `build_classic_installer.py`. NSIS exige
`LEGACY_WITH_HUB` pour ce profil et `STUDIO_ONLY` pour le Studio. Les
20 contrôles ciblés passent, dont la compilation NSIS des deux profils
avec des données inertes. Reçu `G7-D5-windows-explicit-delivery-profiles-043.json`,
SHA-256 `ffbc4c776b47fad5de714d1230a38b3c397d327c3a39160973bbd37391ebeee4`.
La reproduction explicite et les anciens outils de vérification restent
des consommateurs identifiés. Les outils Hivemind versionnés v975,
v978 et v1020 appellent ou vérifient encore la livraison historique.
Le reçu v1020 est historique. Sa racine enregistrée `F:/instal test/Installation`
ne contient plus de tête au relevé du 7 novembre.

Le 044 raccorde le paquet optionnel `asset-library` au plan de migration
Studio qui transférait déjà `reference-assets`. Les deux révisions, les
descripteurs et les droits sont conservés. Une relance réutilise le contenu
installé. Le test natif Windows relit la bibliothèque après désinstallation
de la fixture historique. Le contrôle de réouverture du catalogue de
référence et la garde de désinstallation du Studio passent également.
Reçu `G7-D5-studio-legacy-asset-library-migration-044.json`, SHA-256
`e10d25b70eff915a048f8686b2d8bdfbca98af940c2d353f2cebc445ea83e335`.
Le 045 retire le Studio et laisse les révisions `asset-library` en place.
Le reçu local relie le profil auteur à la révision à reprendre. La
réinstallation réutilise les octets déjà présents et refuse leur
réactivation s'ils ont changé. Onze tests natifs Windows passent, dont
la reprise de désinstallation après verrouillage du binaire et le refus
des révisions étrangères. Reçu
`G7-D5-studio-retained-library-lifecycle-045.json`, SHA-256
`19366b169f573182d58dc042026155b6c036324231fc3f6c5cce92d3abe2c112`.
Le 046 exerce le Setup 019 d'origine et un Setup NSIS de qualification
qui assemble le paquet Studio 042 inchangé avec le CLI de livraison
recompilé. Dans Windows Sandbox sans SDK, le Setup Studio a transféré
les deux paquets d'assets. Après retrait du 019 puis désinstallation et
réinstallation du Studio, `asset-library` conserve son emplacement, ses
droits et les empreintes de ses trois fichiers. Le profil et le fichier
auteur témoin sont conservés. Le paquet `reference-assets` est absent du
Studio réinstallé. La reprise de ce paquet reste nécessaire pour les
projets historiques qui en dépendent.
Reçu
`windows-library-lifecycle-20261107-046/G7-D5-windows-nsis-019-046-library-lifecycle.json`,
SHA-256 `0c36c82f7ff109f45d5d8844ff324f219e418d1929334bd0b366ddd0ccac298c`.
Le Sandbox est arrêté. Les copies et intermédiaires retirés représentent
2 436 808 237 octets sur E: et 4 066 682 octets de manifestes identiques
sur F:. Les Setup originaux 019 et Studio 042 sont disponibles sur E:.

Le 047 garde aussi `reference-assets` sur place lors du retrait du
Studio. Le reçu v2 raccorde les deux paquets au même profil. Le lecteur
accepte les reçus v1 pour `asset-library`. Les trois tests natifs Windows
incluent la relecture du catalogue par le service de lancement Studio.
Dans un Sandbox propre hors ligne, le Setup de qualification réutilise
le paquet Studio 042 avec le CLI 047. Le CLI installe le paquet original
`reference-assets` 019 (8 138 entrées) et la bibliothèque témoin 046.
Le désinstalleur retire l'application, puis le Setup réinstalle le Studio
avec le même profil et les deux révisions inchangées. Le vérificateur
relit les paquets installés et le catalogue. Ce scénario part du Studio
avec les deux paquets installés. Le Setup historique est couvert par le
046. Reçu
`windows-library-lifecycle-20261107-047/G7-D5-windows-nsis-047-retained-studio-assets.json`,
SHA-256 `91775e26283551ac0216c5499d2fd240cf6d5ad6dc098043d2fa86254de58c61`.
Le Sandbox est arrêté. Les temporaires retirés représentent 787 965 792
octets sur E: et les manifestes identiques 4 066 682 octets sur F:.

Le 048 retire `apps/forge_hub`, le profil NSIS groupé, les anciennes
recettes d'assemblage Windows et les commandes Linux et Hivemind qui
recompilaient le Hub. Le Setup Studio conserve la récupération du 019.
Les vérificateurs et outils de maintenance des livraisons déjà
installées continuent de lire leurs artefacts historiques épinglés.
L'archive cataloguée `gate7-hub-retirement-20261107-048.zip` contient les
sources retirées, les modifications locales et les recettes antérieures,
avec un manifeste SHA-256 et relecture intégrale. Elle occupe 6 981 889
octets sur F:. Le retrait des 22 sources archivées et des quatre schémas
générés libère 8 118 618 octets sur E:.
Les contrôles de livraison exécutent 73 tests, dont un ignoré. Ils
couvrent la compilation NSIS avec un payload inerte, le refus des
anciennes recettes, le paquet Studio et le kit du jeu autonome.
Reçu `G7-D5-hub-source-retirement-048.json`, SHA-256
`31d09890ab6876214c14e41301bd32e7893c6eb96a7e85c67a26e8f108bd39df`.
Le candidat Windows réunifié 049 assemble le Studio, le CLI de livraison,
le launcher autonome et le moteur de modules depuis le même inventaire
de sources. L'installateur NSIS privé correspond à ces binaires.
Reçu `unified-windows-20261107-049/G7-D6-unified-windows-candidate-049.json`,
SHA-256 `873b6d9e44f676dd0dd29a6032d07697a7ba24ab64cce981d9aff3291c628141`.
L'installation et le démarrage du Studio 049 sont constatés dans un
Windows Sandbox hors ligne sans SDK. Le CLI et le Studio installés
correspondent aux empreintes du build. Le premier script n'a pas rendu
son reçu final. Une relecture dans le même invité a constaté la fenêtre
du Studio et ses processus WebView2 sans relancer l'installation.
L'invité est arrêté. Reçu
`unified-windows-20261107-049/clean-windows/G7-D6-clean-windows-studio-049.json`,
SHA-256 `d123f6a059253e4cdf3f249f4b597dc455e93de0959c56702586bcf956a3b393`.
Le Setup conservé sur E: occupe 444 465 159 octets. Le kit de jeu reste
disponible pour la qualification avec 141 964 447 octets de contenu.
Le bilan `unified-windows-20261107-049/staging-retirement.json` consigne
356 484 567 octets de temporaires retirés sur E: et 20 297 030 octets
d'inventaires identiques retirés sur F:.

Dans un nouveau Sandbox hors ligne, le Studio 049 installé a enregistré
les configurations du Grand Plateau et de Tidewater dans une copie du
workspace 039, dont l'original était monté en lecture seule. Les deux exports privés ont des
identités de jeu et de module distinctes. La relecture intégrale de
leurs paquets confirme le même launcher et le même moteur 049.
Reçu `unified-windows-20261107-049/editor-export/G7-D6-two-studio-exports-049.json`,
SHA-256 `15df0ad9036f3a758b79eaf16f5e70b04ea469dc7901af70796fa6d50498c0e6`.

Un Sandbox joueur neuf, hors ligne et sans Studio ni SDK, a installé
les deux jeux depuis leurs `Installer.exe`. Chaque launcher a démarré
une nouvelle partie, reconnu la sauvegarde créée dans le jeu et permis
de la reprendre. Les quatre rapports constatent une projection prête et
une fermeture réussie. Les deux sauvegardes et les deux états
d'installation gardent leurs empreintes au contrôle final, dont
l'installation du Grand Plateau après le parcours Tidewater.
Reçu `unified-windows-20261107-049/player-only/G7-D6-clean-windows-two-games-049.json`,
SHA-256 `abee236f8f02a0f63d6f77ee3d4895795a40b9f9f5e062e3b918e60ceb478984`.
Un troisième Sandbox hors ligne a installé Tidewater 035, activé le 049,
restauré le 035, réappliqué le 049 puis désinstallé le jeu depuis son
launcher. Le moteur actif a repris la sauvegarde 035 après la mise à jour
et après la restauration. Les empreintes des deux sauvegardes ont été
conservées à chaque étape. Le Grand Plateau était représenté par sa
sauvegarde dans cet invité. Le contrôle final confirme le retrait de
`Jouer.exe`, de l'état d'installation et des paquets de Tidewater.
Reçu `unified-windows-20261107-049/maintenance/G7-D6-windows-game-maintenance-049.json`,
SHA-256 `3571f9cbed5272ce69f4b36edd0949f25903b1d64f8a49b72cb1373bf31bc4a3`.
Les trois invités sont arrêtés. Les deux exports occupent 540 065 135
octets sur E: dans `.build/qualification-data/gate7/unified-windows-049-exports`.
Ils restent les entrées exactes pour le rapprochement des droits et des
performances sur ce candidat. Les reçus ci-dessus couvrent le parcours
privé Windows. La qualification Linux et les droits de redistribution
restent ouverts.

Le candidat Linux privé 0837 du 3 novembre reste exploitable pour la
reprise du parcours d'installation. Le reçu de build comporte 15 étapes
réussies. Le volume Docker existant a été monté en lecture seule hors
réseau. Le paquet Studio de 385 439 325 octets et les 470 fichiers du
kit correspondent aux empreintes du build. Les quatre ELF et le ZIP
source ont aussi été vérifiés sur E:. Le checkout actuel diffère sur
355 entrées de la comparaison des sources et manifests produit. Ce
candidat garde son verdict `PASS_BUILD_ONLY` et sa révision.
`editor-launcher-linux-qualification-20261102-001/runs/candidate-20261103-0837/linux-retained-artifacts-recheck-20261107.json`,
SHA-256 `ed715678bb30bc43ff26601fa21e594446149639b6925f23fe04008f902f021a`.
Les copies host du moteur, du launcher et du CLI ont été retirées
après vérification des originaux conservés dans le kit et le dossier
Studio du volume. Retrait de 196 591 536 octets sur E:. Le Studio ELF
reste sur E: pour le préflight de bureau sans extraction du paquet. Le
ZIP permet de relire le snapshot exact. Docker a été arrêté après lecture,
avec zéro conteneur résiduel. La lecture n'a lancé ni build ni produit.
Le contrôleur `scripts/gate7/linux/run_clean_delivery.py` accepte
désormais le vrai paquet Studio privé en plus du scénario historique
avec fixture. Le mode Studio relie le plan et l'état installé aux
empreintes de l'éditeur, puis vérifie la désinstallation et le fichier
témoin. Le contrat a été vérifié localement avec un CLI simulé. L'essai
suivant a utilisé le vrai paquet 0837 et son CLI dans le conteneur SDK
existant, hors réseau, volume source en lecture seule et installation
sur tmpfs. Les cinq commandes `inspect`, `plan-file`, `apply`, `verify`
et `uninstall-studio` se sont terminées avec le code 0. L'éditeur installé
correspond au SHA-256 attendu, est exécutable, puis est retiré. Le fichier
témoin a conservé son empreinte. Le tmpfs et le conteneur ont été libérés
et Docker arrêté. Aucun nouveau paquet conservé, un reçu de 3 897 octets.
`editor-launcher-linux-qualification-20261102-001/runs/candidate-20261103-0837/linux-studio-install-sdk-0837.json`,
SHA-256 `20f80eac00623de3fe695dbd1c218a93c00f0a7c7ea0a48b832441b80283ded8`.
Le Studio n'a pas été démarré. La machine sans SDK et le parcours auteur
restent ouverts. La session hôte ordinaire a reçu un refus d'accès pour
`Get-VM` sur la VM Ubuntu existante.

Un conteneur Debian 12 distinct du SDK a ensuite installé le même paquet
0837 à l'empreinte et résolu toutes les dépendances ELF du Studio. Les
bibliothèques de runtime sont inventoriées, les compilateurs et SDK sont
absents, le volume de paquet est en lecture seule et le réseau a été
déconnecté avant l'installation. Le CLI a lancé l'éditeur sous Xvfb avec
le verrou d'installation. La fenêtre « OpenRPG Forge Studio » est mappée
et son PID appartient à l'éditeur vérifié. Le log signale un bus AT-SPI
absent dans le conteneur. Le Studio a ensuite été désinstallé en conservant
le fichier utilisateur témoin. La fenêtre seule ne qualifie ni le contenu
du WebView, ni le parcours auteur/export, ni la machine propre. L'image
Debian et le conteneur d'essai ont été retirés et Docker arrêté. Le reçu
de 14 568 octets contient le protocole et les observations.
`editor-launcher-linux-qualification-20261102-001/runs/candidate-20261103-0837/linux-studio-installed-sdk-free-container-0837.json`,
SHA-256 `2e2d45dd7256e8f7918e67ac5190b78fa19d8ff6b8432dae1f871e7d782e71d7`.

Un autre conteneur Debian 12 sans SDK a exercé le parcours joueur du
Grand Plateau. Les 470 fichiers du kit Linux 0837 ont été repris à leurs
empreintes depuis le paquet Studio et associés à la configuration et au
module 049 inchangés. Le CLI a assemblé et installé l'application privée.
Le diagnostic natif `--verify-game-launch` du launcher installé a démarré
le runtime du kit et sa fenêtre X11 sous llvmpipe/Vulkan. Après
réinstallation du même paquet, F5 a créé une sauvegarde `forge.game-save`
dont les identités de module, zone, source et jeu correspondent au reçu
Windows. L'empreinte de la sauvegarde est conservée après désinstallation.
Le premier relevé avait compté `graphics.json` comme sauvegarde. Le
contrôle final porte sur `authoring.json` et son enveloppe de jeu. La
reprise par le launcher, l'export depuis le Studio Linux et la qualification
sur OS propre restent à faire. Le conteneur et l'image d'essai ont été
retirés, Docker arrêté, et aucun nouveau paquet n'est conservé.
Reçus `linux-plateau-from-studio-package-container-0837.json` et
`linux-plateau-gameplay-save-container-0837.json` dans
`editor-launcher-linux-qualification-20261102-001/runs/candidate-20261103-0837/`.
Ils occupent 31 800 octets sur F:. SHA-256 du reçu de sauvegarde
`7d25a478358ae676f449eccd7f9f6b7f2d766e932093484fc0c971fd4680e217`.

Le binaire 049 extrait du paquet vérifié a exécuté les deux modules sur
Windows natif, Radeon RX 7800 XT/Vulkan, à 1280 × 800 avec Fifo demandé.
Au profil Moyen, sur les images 300 à 599 avec projection et simulation
prêtes, les intervalles moyens/p95 sont de 6,94/7,41 ms au Grand Plateau
et de 22,72/25,05 ms à Tidewater. Les deux scènes utilisent le même
moteur et des contenus différents. Le profilage séparé de Tidewater
retrouve 22,90/25,52 ms. Entre ses deux derniers relevés cumulés,
`render.Render` mesure 12,24 ms par appel, dont la cause reste à isoler.
L'horodatage GPU asynchrone des ombres source vaut 0,41 à 0,45 ms aux
deux observations de régime. Les intervalles CPU imbriqués ne s'additionnent
pas. Cette mesure à caméra fixe reste distincte d'une mesure en mouvement
ou d'une comparaison avec l'ancien exécutable Tidewater.
Reçu `unified-windows-20261107-049/native-profile/G7-D6-windows-native-profile-049.json`,
SHA-256 `5ca7429b8a0c9cbbe89b29b24c05b861a4f17d3ef8a10062388cf2f5520ca8f4`.
Les 334 388 752 octets de caches, journaux et binaire extrait de ce lot
ont été retirés d'E: après les essais. Les preuves de cadence restent sur F:.

Le runtime commun déduplique maintenant les maillages et matériaux actifs
par identifiant dans l'inventaire graphique. L'ordre d'admission et les
contrôles des dépendances restent identiques. Les 32 tests graphiques
existants passent dans le checkout partagé.
Reçu `unified-049-graphics-inventory-dedup.log.receipt.json`, SHA-256
`1acb5d18767410ac763ab50e249d9695af6bc5aaac1cfbb27f463ccb68fd08f6`.
Le rejeu A/B du 7 novembre a reconstruit le moteur seul. Son inventaire
de compilation ne diffère du 049 que par le fichier de l'inventaire
graphique. Le même export Tidewater, à caméra fixe en Moyen et à
1280 × 800, passe de 23,26 à 22,89 ms en moyenne et de 25,84 à 25,33 ms
au p95. La famille `graphics.inventory.other` passe de 0,113 à 0,108 ms
par appel. L'écart global observé sur un passage par binaire reste faible.
`render.Render` mesure encore 12,43 ms par appel. Les relevés asynchrones
de la bande caméra donnent 6,79 à 9,16 ms côté CPU et 4,72 à 5,56 ms côté
GPU sur le moteur modifié. Ces bandes se recouvrent. Il reste à isoler
le coût d'encodage et de soumission avant d'attribuer les 12,43 ms.
Reçu `unified-windows-20261107-049/native-profile/inventory-dedup/G7-D6-inventory-dedup-comparison.json`,
SHA-256 `fce4d734cd08035fb0c4d8ff1034ca4d85b17f058f3e4bf57783179605609357`.
Relevés CPU/GPU `render-diagnostics-readback.json`, SHA-256
`853d7624495eea937b71ec490014ead2efd07737a2b757020111e1a4c0b36c55`.
Les 541 728 368 octets temporaires sur E: ont été retirés. Les deux
inventaires avant/après sur F: étaient identiques au `source-inputs.json`
conservé à l'empreinte `e9f92a83fdc81a8da59b770c4405d13f335ff3e3c3e072ff14eed6138cd3ce46`.

Les bornes CPU du graphe utilisées par la référence Tidewater sont
maintenant enregistrées dans le moteur commun. La session native sur
le même export Tidewater au profil Moyen mesure 23,02 ms en moyenne
et 25,43 ms au p95 sur 300 images prêtes. Entre les deux derniers relevés
cumulés, `render.Render` mesure 12,52 ms par appel. Les bandes CPU
comprennent 4,25 ms jusqu'à la fin du système atmosphère, 0,13 ms
jusqu'à la fin du système nuages, 4,57 ms jusqu'à la fin du pilote caméra,
1,39 ms pour la soumission et la finalisation du graphe, puis 0,20 ms
après le graphe. La première bande comprend les systèmes qui s'exécutent
avant la fin de l'atmosphère. Le système atmosphère lui-même peut sortir
immédiatement quand sa révision est déjà encodée. La première bande et
celle de la caméra restent les priorités pour isoler le travail CPU.
Reçu `unified-windows-20261107-049/native-profile/render-bands/G7-D6-tidewater-common-render-bands.json`,
SHA-256 `a54755a02c5d5bb3421dac203211dc53df4d8f26373447a1486ec0e3466759db`.
Le profilage est activé à la demande. Les 211 641 266 octets temporaires
ont été retirés d'E:. Les deux inventaires avant/après étaient identiques
au `source-inputs.json` conservé à l'empreinte
`1f212d5f010971141878dc8df4af71833413be2e0570294691ae3f0b812e7d96`.

Le moteur commun partage désormais la matrice de projection de chaque
cascade entre les objets opaques. Chaque objet conserve sa propre pose
par couche. Les matrices inchangées restent résidentes. Le rendu conserve
les opérations modèle puis projection et la cadence des cascades.
Le nouvel essai reprend l'export, le profil et la caméra du relevé
précédent. Sur 300 intervalles prêts, la moyenne passe de 23,02 à
19,38 ms et le p95 de 25,43 à 21,02 ms. `render.Render` passe de
12,52 à 10,17 ms par appel. Les 707 casters opaques, les 715 récepteurs
et les cinq lots végétaux sont retrouvés. Les rapports ne signalent
aucune erreur de pipeline. La comparaison porte sur une vue fixe et
ne comporte pas de relecture directe de l'image de profondeur.
Reçu `unified-windows-20261107-049/native-profile/opaque-shadow-cascades/G7-D6-opaque-shadow-cascade-comparison.json`,
SHA-256 `15cd10e2801f046951b1dddf4029770455a030088e34143f358a21524436b506`.
Les 211 638 534 octets temporaires sur E: ont été retirés. Le
`source-inputs.json` conservé remplace les deux inventaires identiques
avant/après, à l'empreinte
`e0d87063e3266e68d7901ac3daee787b737ee644c54b28ae203127c82d055d19`.

Le moteur conserve les flux d'ombres végétales tant que la sélection
ordonnée reste identique. La position horizontale identique réutilise
la sélection. Une position différente applique le même sélecteur radial
et réutilise les octets si l'ordre des casters n'a pas changé. Les
remplacements invalident le cache. Le vent et les matrices de cascade
restent actualisés par le graphe existant.
Sur le même export Tidewater au profil Moyen et à caméra fixe, la
moyenne passe de 19,38 à 18,97 ms et le p95 de 21,02 à 20,66 ms. Les
deux relevés de régime comptent le même téléversement initial et aucun
nouveau téléversement, avec 259 appels de sélection entre les relevés.
Les cinq lots végétaux, les 707 casters opaques, les 715 récepteurs et
la concordance du vent sont retrouvés.
Reçu `unified-windows-20261107-049/native-profile/resident-plant-shadow-streams/G7-D6-resident-plant-shadow-streams-comparison.json`,
SHA-256 `de8f695ac637e6c791c95865c51646799d34ddcd3d65076640969786445538f5`.
Les 211 629 470 octets temporaires sur E: ont été retirés. Le
`source-inputs.json` conservé remplace les deux inventaires identiques
avant/après, à l'empreinte
`c28320287256f97164d32f39a806227574221cf3e3b31554463181a459fb606e`.

Le scénario natif a ensuite rejoué le même trajet d'environ 41 m sur
l'export Tidewater, avec le runtime distribué du 049 puis le runtime
optimisé déjà compilé. L'écart maximal aux bornes des deux déplacements
est de 5 cm. Sur RX 7800 XT, Vulkan, 1280 × 800 au profil Moyen,
l'intervalle moyen en mouvement passe de 24,53 à 20,61 ms, le p95 de
35,02 à 34,31 ms et le maximum de 69,56 à 46,12 ms. Les 558 et 665
intervalles retenus correspondent tous à des images déclarées prêtes.
Le budget moyen de 16,67 ms reste ouvert. Le relevé couvre un seul trajet
par binaire et ne compare pas le moteur au lecteur Tidewater historique.
Reçu `unified-windows-20261107-049/native-profile/moving-route/G7-D6-tidewater-moving-route-comparison.json`,
SHA-256 `e7fd65ab23ea440fc196be7e28b2b7c377b14a7038a53210558a90d73037eba5`.
Les preuves vérifiées sur F: totalisent 4 908 939 octets. Les caches,
copies et journaux de travail totalisant 541 657 981 octets ont été retirés d'E:.

Le moteur commun enregistre désormais les commandes d'ombres opaques une
fois par cascade dans un `RenderBundle`. Les passes de profondeur
réexécutent les mêmes commandes et les mêmes liaisons. Les matrices de
projection et les poses conservent les écritures précédant chaque passe.
Le nouveau binaire release reprend les mêmes déplacements sur le même
export Tidewater. Les bornes de trajet diffèrent de 4,5 cm au maximum.
La moyenne passe de 20,61 à 19,18 ms, le p95 de 34,31 à 33,99 ms sur
665 et 714 intervalles prêts. Aucun échec de pipeline. La capture finale
montre les ombres des bâtiments et de la végétation. Elle a été prise
après la fenêtre mesurée. Un trajet par binaire, aucune comparaison
pixel à pixel. Le budget moyen de 16,67 ms et le lecteur historique
restent à qualifier.
Reçu `unified-windows-20261107-049/native-profile/opaque-shadow-bundles/G7-D6-opaque-shadow-bundles-comparison.json`,
SHA-256 `f12d9bf84dc1ba64f92d4df1f256a81baeab28499d05a97085f61967c367658d`.
Le lot conserve 14 fichiers de preuve, soit 6 082 447 octets sur F:.
Retrait de 211 601 354 octets de temporaires sur E: et de 4 059 406
octets de copies d'inventaire identiques sur F: après vérification.

La mesure historique Release 012 du 15 octobre a été retrouvée dans
l'archive Gate 3 et vérifiée aux empreintes. Les rapports retrouvent les
mêmes identités de document, d'archive de zone et de données de jeu que
l'export Tidewater 049. Sur la RX 7800 XT et Vulkan, le 012 mesure 15,19
puis 10,51 ms sur les phases fixes et 10,50 puis 10,51 ms en rotation sur
place, à 2560 × 1440 et avec le pilote 26.8.1. Le trajet récent est mesuré
à 1280 × 800 avec le pilote 26.10.1. Les ressources visuelles du lecteur
historique sont chargées séparément, et les réglages diffèrent.
Le profil fixe du moteur commun avant les `RenderBundle` mesure 18,45 ms
pour l'ordonnanceur de rendu, dont 10,13 ms pour `Render`. La seconde phase
fixe historique mesure respectivement 10,03 et 6,55 ms. La préparation
hors du graphe de rendu mérite donc aussi d'être isolée dans un prochain
relevé apparié. Ces valeurs n'établissent pas la cause de l'écart.
Reçu de 6 701 octets obtenu sans relancer les binaires
`unified-windows-20261107-049/native-profile/opaque-shadow-bundles/G7-D6-tidewater-historical-reference-scope.json`,
SHA-256 `24ea6972fca82708bba72dfdccc0676031476885d1faa3d7575157dca2d3b8ba`.

Le binaire avec réutilisation des commandes d'ombres a ensuite repris le
trajet au profil Moyen avec les diagnostics CPU activés. Sur trois
fenêtres de cinq secondes après démarrage, `Render` coûte 8,66 à 8,87 ms
par image principale, `Prepare` 3,91 à 4,03 ms et `PrepareViews`
2,44 à 2,48 ms. Les contrôles végétaux avant et après le mouvement
prennent ensemble 1,46 à 1,90 ms. Ces portées incluent l'ordonnancement
et des appels imbriqués. Les fenêtres comprennent des déplacements et
des pauses. Le parcours instrumenté a terminé, toutes les images
retenues étaient prêtes.
Reçu `unified-windows-20261107-049/native-profile/cpu-moving-route/G7-D6-tidewater-movement-cpu-scope.json`,
SHA-256 `5d45cbc67a49f6186e847166d32b64615829db3d1a9033007522557886ad512d`.
Sept pièces vérifiées, 2 505 430 octets sur F:. Les temporaires du
profil sur E: ont été retirés, soit 211 671 752 octets.

Dans chaque garde végétale, la simulation peut réutiliser le contrôle de
projection réussi du rendu. L'observation se fait après l'actualisation
de l'herbe, puis les accès au monde sont en lecture jusqu'au contrôle de
simulation. Le contrôle de secours de la projection, la validation du
jeton GPU et les vérifications avant et après le déplacement restent
actifs. Les 29 tests de végétation streamée passent, dont les mutations
dans le même tick et le refus du mouvement tardif.
Reçu `tidewater-plant-guard-shared-audit.log.receipt.json`,
SHA-256 `ef3833d01c1d46cec56dae2a5934fe68dde66e5cf90dd3cd699fe8551b4dd416`.
La modification est postérieure au binaire profilé ci-dessus.

Le runtime indexe aussi les signatures d'inventaire par famille. Les
vecteurs de clés triées sont transférés sans copie et chaque famille est
lue directement. Les entrées utilisées pour les empreintes de publication
gardent le même format. Le nouveau binaire release regroupe les deux
modifications. Sur le trajet Tidewater identique, la moyenne passe de
19,18 à 18,89 ms et le p95 de 33,99 à 31,65 ms. Les 714 et 726
intervalles retenus sont prêts. L'écart maximal aux bornes du trajet est
de 4,5 cm. Les comptes des inventaires et des surfaces d'ombre
correspondent. La mesure couvre un trajet par binaire et ne sépare pas
les deux modifications. Le budget moyen de 16,67 ms reste ouvert.
Reçu `unified-windows-20261107-049/native-profile/graphics-inventory-keys/G7-D6-graphics-inventory-keys-comparison.json`,
SHA-256 `630ea851ccf89a889ce48c2b2939a587f4777fb5b01bf7d162f40c9ff6c17ccc`.
Douze pièces, 4 527 786 octets sur F:. Retrait de 211 601 096 octets
de temporaires sur E: et de 4 059 406 octets de copies d'inventaire
identiques sur F: après vérification.

Le chemin de l'exécutable Release 012 pointe désormais sur le cache
Cargo commun. Le lecteur conservé du 27 octobre a une autre empreinte.
Sur la RX 7800 XT en Vulkan avec le pilote 26.10.1, ce lecteur ouvre
l'instantané archivé et retrouve l'empreinte source attendue. Les
préférences et les 13 fichiers de l'instantané sont vérifiés. L'audit
des 8 165 autres fichiers retrouve une différence dans
`data/workspace/workspace-v1/authoring.json`. Le lecteur s'arrête sur
`Invalid source camera pose or field of view` dans `terrain_lod.rs`
(code 101) avant de produire un profil. La comparaison appariée reste
ouverte. Le runtime actuel n'a pas été relancé pour cet essai.
Reçu de 3 641 octets sur F:
`unified-windows-20261107-049/native-profile/G7-D6-tidewater-retained-probe-replay.json`,
SHA-256 `776e5f206153baf592af27c4f741cd5fe32e7b2304ea1d64e68f0c042534ba67`.
Les 54 382 216 octets de temporaires de cet essai restent sous
`.build/tmp/gate7-tidewater-aligned-20261107` sur E: après refus du retrait
par la politique d'exécution.

Les deux contrôles de végétation du moteur comparent la partition
terrain et le HLOD empruntés au contrôleur commun. Ils ne copient leurs
cartes que si la source, la politique, les cellules ou le HLOD ont
changé. Les 29 tests de végétation streamée passent sous Windows.
Le gain sur le trajet Tidewater reste à mesurer sur le prochain binaire
commun. Le journal et le reçu `tidewater-plant-partition-borrowed-audit.log`
et `tidewater-plant-partition-borrowed-audit.log.receipt.json` occupent
5 045 octets sur F:. SHA-256 du reçu
`9a87cb056537b2f618eab1f0ab46593ee0148789073b06e885f285fd784a8924`.

Le runtime release construit sur entrées stables reprend le trajet du
049 sur 40,80 m, au profil Moyen en 1280 × 800, avec Vulkan et FIFO.
Sur les deux fenêtres en mouvement, la moyenne passe de 18,89 à
18,85 ms et le p95 de 31,65 à 31,37 ms. Les 726 puis 727 intervalles
retenus sont prêts. Les bornes du trajet, les inventaires et les comptes
de surfaces d'ombre correspondent. Un passage par version et l'écart
observé ne permettent pas d'attribuer un gain à la modification. Le
budget de 16,67 ms reste ouvert. Le lecteur de notices et sa fixture
ainsi que deux fichiers du protocole Linux ont aussi changé depuis le
build précédent.
Reçu `unified-windows-20261107-049/native-profile/plant-partition-borrowed/G7-D6-plant-partition-borrowed-comparison.json`,
SHA-256 `7395b30a91ede1a87d1363cba1072158ef54dc7648e7d5a2084498418694f049`.
Douze fichiers, 4 525 977 octets sur F: après retrait de 4 059 408
octets de manifestes identiques. Le dossier temporaire E: a été retiré,
dont 190 017 122 octets après reprise du nettoyage en chemins longs.

Le contrôleur terrain commun conserve l'identifiant logique de chaque
cellule et tuile HLOD dès sa publication. Les contrôles comparent encore
le marqueur vivant à cette identité et à la source attendue. Le dernier
trajet Tidewater compte 169 cellules et 144 tuiles HLOD, soit 313 calculs
d'identifiant évités à chaque contrôle terrain complet dans cette scène.
Les 50 tests de streaming passent sous Windows, dont les refus d'un
marqueur de cellule ou de tuile altéré. Le gain de cadence reste à mesurer
sur un build commun ultérieur. Journal et reçu
`terrain-residency-identity-audit.log` et `.log.receipt.json`, 6 920
octets sur F:. SHA-256 du reçu
`f533eccd8e0bcc3480b8eb669fc2991cd37971d418337495832cd2355f72d18b`.

R7-09, cycle du Studio 049. Un Sandbox Windows hors ligne et sans SDK
a installé le 042 puis le 049 sur la même installation. Deux plans natifs
ont réactivé le 042 puis le 049. Le programme de désinstallation NSIS
a retiré le Studio, puis le même Setup 049 l'a réinstallé et son exécutable
exact a ouvert une fenêtre Studio. Les 42 fichiers du workspace de
qualification (271 032 968 octets) et le catalogue de référence ont gardé
leurs empreintes. Le workspace préservé est un contrôle de fichiers.
L'essai d'ouverture et d'export depuis le Studio relève du parcours
distinct déjà consigné. Le Sandbox est arrêté.
Reçu `unified-windows-20261107-049/studio-maintenance/G7-D6-windows-studio-maintenance-049.json`,
SHA-256 `537a15d552539c006fc4c77416dc1e7da75c006fb3cc0d5bbfac31703c6e31b2`.

Le rapprochement Windows privé 049 vérifie 43 pièces et relie les
empreintes des builds aux binaires observés dans les invités. Il couvre le
volet Windows de R7-07, le cycle d'installation et de maintenance Windows
de R7-09 et le parcours en Sandbox Windows 11, hors ligne et avec vGPU,
de R7-10. Le Studio a exporté depuis une copie du workspace auteur. L'invité
joueur a installé et repris les deux jeux sans Studio ni SDK. Les étapes de
maintenance ont leurs propres invités et preuves. Les reçus historiques
gardent leur portée. Ce rapprochement de 6 334 octets a été réalisé sans
nouveau build ou essai en invité.
Reçu `unified-windows-20261107-049/R7-07-09-10-windows-049-assessment.json`,
SHA-256 `a8cc186a3cf6630ed9a63f2300d7f51e41fa366716f51c1cff16082346afb5d4`.

R7-08, périmètre des deux exports 049. Les manifestes limitent les deux
jeux à l'usage privé et déclarent les mêmes 471 entrées de notices pour
l'application Windows. Le catalogue embarqué du Grand Plateau contient deux
maillages procéduraux avec preuve de redistribution. Les quatre bâtiments
Quaternius sélectionnés dans Tidewater ont un libellé CC0-1.0 et un droit
de redistribution déclaré `forbidden`, sans preuve de redistribution
pour les fichiers livrés. R7-08 reste ouvert pour les autres contenus et
composants ainsi que la validation des droits.
Reçu `unified-windows-20261107-049/R7-08-windows-game-export-scope-049.json`,
SHA-256 `a7c58d52a46bc4607e8719b61fbeb8958eefb00c0ff7cf10a2f2bb99115973eb`.
Les archives des lots 017, 018 et 020 d'octobre établissent maintenant la
filiation exacte des quatre GLB. Les 52 entrées sources des recettes
correspondent aux octets de l'archive Quaternius conservée. Les empreintes
relient les assemblages, les retouches de textures et de supports et les
quatre fichiers embarqués. L'éditeur du pack annonce la licence CC0.
[Medieval Village MegaKit](https://quaternius.com/packs/medievalvillagemegakit.html)
La preuve complète le dossier d'import gouverné des modèles. Le chemin de
secours qui les reprend d'une installation de référence produit par
construction un catalogue d'usage privé. Le 049 conserve ses droits déclarés.
Reçu `unified-windows-20261107-049/R7-08-tidewater-quaternius-provenance-049.json`,
SHA-256 `83f8b6501e9fcc901e9d58c35e0ae6cdb8b666521f7284029e27d3a938b6de7e`.
Les quatre modèles ont ensuite passé la préparation, l'application, la
relecture et la sélection pour l'export privé dans une collection Studio
isolée. Le service natif a conservé les quatre identifiants et SHA-256
utilisés par les placements. Les douze fichiers de l'instantané couvrent
les modèles, les recettes de dérivation et les notices. Les fichiers source
du pack sont déclarés CC0 par Quaternius. Les entrées d'essai conservent
le droit de redistribution inconnu pour les assemblages retouchés et le
service refuse leur sélection publique. [Licence du pack](https://quaternius.com/packs/medievalvillagemegakit.html)
L'essai utilise la bibliothèque native Studio du cache commun. Il couvre
ses commandes de collection et l'instantané, sans rejouer l'UI ou
réassembler un module. Le reçu conserve les entrées de reprise et leurs
hashes, soit 31 920 octets sur F:. La collection d'essai, les paquets de
transport et le programme de contrôle ont été retirés d'E:, soit
88 791 984 octets.
Reçu `unified-windows-20261107-049/R7-08-tidewater-governed-reimport-049.json`,
SHA-256 `210c319292b2bb1a3f94f9fea6e077c8501d34fb530da83a5eab055bf7288b22`.

La lecture intégrale des deux transports 049 rapproche leurs descripteurs,
leurs conditions privées d'origine et leurs 5 et 13 entrées de licence.
Dans Tidewater, les manifestes des surfaces, de l'environnement et de la
végétation épinglent la même révision amont et déclarent respectivement
821, 266 et 83 fichiers. Le paquet reprend la licence MIT amont aux
empreintes, mais ne contient pas son fichier de crédits complet. L'amont
distingue la licence de son code des notices des ressources tierces.
La portée des droits doit donc être rapprochée des médias effectivement
embarqués, y compris les six modèles historiques distincts du catalogue
actif. [Crédits amont](https://raw.githubusercontent.com/dgreenheck/tidewater/1438b1abfcaee3267092b75573014f4d9b4a983c/CREDITS.md)

Les six GLB historiques du 049, soit 31 272 396 octets, ont été relus
aux empreintes du paquet. Les recettes archivées relient chacun à
l'un des quatre assemblages Quaternius et à ses retouches de textures
ou de toiture. Les déclarations des retouches et du module restent privées.
Reçu `unified-windows-20261107-049/R7-08-tidewater-six-historical-models-20261114.json`,
SHA-256 `be583ce165a36009a3328a3c65682b1acc76260891e2943622e0320ca96a22bc`.

Le contrôle des ressources de référence du 049 rapproche les trois
manifestes internes des entrées de l'archive. Le catalogue de construction
contient 17 maisons, quatre remises, un hangar à bateaux et un étal. Les
14 textures du village sont rattachées au générateur procédural amont
par la recette d'export. Le contrôle a aussi retrouvé à l'octet près le
bruit bleu des nuages et les sept niveaux du volume de nuages source,
soit 1 202 468 octets au total. Les crédits de la révision épinglée
décrivent le portage du code de Sky Pro. Le document de licence des
textures ne désigne pas les deux fichiers binaires source. Leur
provenance détaillée et les droits des dérivés restent à établir.
Reçu `unified-windows-20261107-049/R7-08-tidewater-reference-media-boundaries-20261114.json`,
SHA-256 `7e049287c5c9a36f2ae1ddd68464d26e5a50c279be0b349199dd9ba9acc9399d`.
[Crédits Tidewater épinglés](https://raw.githubusercontent.com/dgreenheck/tidewater/1438b1abfcaee3267092b75573014f4d9b4a983c/CREDITS.md).

Les deux fichiers source ont été vérifiés par identifiant de blob Git et
SHA-256 à la révision Tidewater. Ils sont utilisés par le moteur commun
pour les nuages. Les crédits Tidewater placent le code adapté sous MIT.
La licence publique du produit commercial Three.js Sky Pro couvre les
éléments fournis sous son propre contrat, avec des restrictions sur leur
intégration dans des créations de tiers. Les éléments disponibles
n'établissent ni que ces deux fichiers proviennent de cette distribution
commerciale, ni qu'une autorisation les couvre dans les modules exportés
par Forge. Le service de livraison maintient les gardes pour la référence
installée et le réexport depuis une origine privée. Pour lever la réserve,
il faut une autorisation applicable aux deux empreintes ou des ressources
de substitution dont les droits et le rendu sont qualifiés.
Reçu `unified-windows-20261107-049/R7-08-tidewater-cloud-assets-rights-20261114.json`,
SHA-256 `d40ca73d90bf209f5ac13949513c60907dd03485468f8b7585c6cbc84fd97572`.
[Licence des textures à la révision Tidewater](https://raw.githubusercontent.com/dgreenheck/tidewater/1438b1abfcaee3267092b75573014f4d9b4a983c/public/clouds/LICENSING.md).
[Licence commerciale Three.js Sky Pro](https://docs.threejsskypro.com/license.html).

Le [générateur Forge](../scripts/tidewater/generate-cloud-textures.py)
produit le volume périodique et ses sept niveaux ainsi que le bruit
de tramage, sans lire les textures d'origine. Il prépare un paquet
à part, vérifie l'empreinte de chaque entrée et met à jour les deux
index. Sa commande a reproduit exactement le paquet de 267 fichiers
utilisé par le lecteur commun. Un essai avec un octet source altéré
a été refusé avant l'écriture du paquet. La sonde native existante
a exécuté les 120 étapes GPU, avec relecture de l'ombre, de
la radiance et du panorama. L'équivalence visuelle en jeu et
l'export de cette variante restent à qualifier.
Reçus `unified-windows-20261107-049/R7-08-generated-cloud-textures-probe-20261114.json`
et `R7-08-generated-cloud-textures-repackage-20261114.json` dans le
même dossier, SHA-256
`76ae0b06c3e44e8053b4fb8cfa357c50132e64c3a5c0517d174aa945bace1623`
et `8c48820d8d3342fcadcc9c3cc71fa86063009c3cf3d10f0e081d431e9b7ed207`.

La même sonde a aussi rendu le paquet source. Les deux séquences
terminent leurs 120 soumissions. L'ombre moyenne passe de 0,679 à 0,659
et l'écart absolu moyen par texel est de 0,068. La radiance et le
panorama présentent également des écarts. Ce contrôle mesure les
sorties brutes de la sonde et laisse ouverte l'acceptation du rendu
dans le jeu exporté.
Reçu `unified-windows-20261107-049/R7-08-generated-cloud-textures-output-comparison-20261114.json`,
SHA-256 `a96556c24bd828d5e739c9dd3ce90f6f31caa57eaf1937f30d84650dcbe9ed9a`.

Le CLI de livraison a réassemblé le module privé avec les huit textures
générées et les deux index mis à jour. Le même binaire du moteur a chargé
les deux transports, avec les mêmes empreintes de zone, de source et de
gameplay. Les captures au départ et vers le ciel montrent que la géométrie
et les principales ombres au départ restent cohérentes, avec une
distribution des nuages visiblement différente. Les vues vers le ciel
ont les mêmes coordonnées du joueur, angles de caméra et tick final.
Les soumissions de nuages restent distinctes et la disponibilité a été
intermittente avant la sortie prête dans les deux essais. Le parcours
Studio et launcher de cette variante reste à qualifier. Un chemin long
a interrompu le nettoyage automatique des fichiers temporaires sur E:.
Reçu `unified-windows-20261107-049/cloud-module-comparison-20261114/R7-08-cloud-module-comparison.json`,
SHA-256 `5a01305961d71a919a40c5094d0cd2ab063de567ee378af47f50cf752c897f08`.

Le service natif de réexport du Studio a repris cette variante privée
avec ses six modèles historiques et ses liaisons de zone, de végétation,
d'environnement et de surfaces. Une demande de redistribution a été
refusée sur les droits du module d'origine. Le jeu réexporté a lancé
une partie, sauvegardé après stabilisation des objets physiques,
demandé une reprise et chargé la même sauvegarde dans un second
processus. Les empreintes du fichier et de l'état de jeu concordent.
Ce contrôle de service et du runtime a utilisé le lancement natif direct.
Le parcours intégral avec l'interface Studio et le launcher reste ouvert.
Reçu `unified-windows-20261107-049/cloud-module-comparison-20261114/R7-08-cloud-private-reexport-native-resume.json`,
SHA-256 `b450e23af91dd6fe04623a6213c8d1132b6afc2373d01a1fdc7aa34112b592d5`.

Le kit Windows 050 et la configuration Tidewater ont assemblé cette
variante privée. La transaction native l'a installée dans un dossier de
qualification sur l'hôte, avec un espace de sauvegarde isolé. Le launcher
installé a démarré la première partie par son entrée de diagnostic.
Après sauvegarde et fermeture du jeu, le bouton « Reprendre » du launcher
graphique a chargé la même sauvegarde dans un nouveau processus. Les deux
sessions terminent avec la projection prête et les mêmes empreintes de
source, de gameplay, de fichier de sauvegarde et d'état. Le parcours
graphique du Studio et de l'installateur reste à qualifier pour cette variante.
Reçu `unified-windows-20261107-049/cloud-module-comparison-20261114/R7-08-cloud-installed-launcher-play-resume.json`,
SHA-256 `878375863b2fab2443a9ed193b57ccd070e9f1b79ff16f4f14d5e6aebc5d791c`.

Le même inventaire relève 21 597 175 octets de répétitions dans le module
Tidewater. Quatre paires modèle livré/source de retouche en représentent
21 579 540. Les chemins ont des rôles distincts. Cette mesure délimite le
lot à mutualiser et les chemins à préserver dans une future distribution.
Les paquets vérifiés sont conservés aux empreintes. Le reçu de 11 554
octets sur F: est
`unified-windows-20261107-049/R7-08-windows-module-media-notice-scope-049.json`,
SHA-256 `d999e072b1884f430d84ffb7a5167684341e8e6f5308042310c1532f0da6ef6b`.

Le manifeste du paquet d'origine 019 épingle
`data/scene/CREDITS-Tidewater.md` au SHA-256
`45fbe42bf547cea34cb6606e9571b59045d0b9d0c6a55321615b9ca791510325`,
mais sa liste de licences ne désigne que `PROVENANCE.md`. Le lecteur
commun transmet désormais aussi les crédits épinglés à la racine de
la scène lors d'un export depuis une référence installée. Le test
Windows de migration vérifie leur relecture après réinstallation et
le refus des crédits altérés. Les exports issus d'un ancien paquet
importé reprennent ses avis existants. Pour vérifier l'ajout dans un
jeu complet, il reste à repartir de la référence installée avec le
prochain Studio. Le statut privé et les réserves sur les médias restent
applicables. Le journal et le reçu `r7-08-legacy-reference-scene-credits`
occupent 1 394 octets sur F:. SHA-256 du reçu
`a10af91f589df8ca92581dbc716bfe18e1eaaf490adc085ea0f1ba6956acb530`.

Le service natif d'export a ensuite assemblé et relu un module privé de
210 733 850 octets depuis la référence 019. L'archive et les blobs
canoniques de Tidewater ont été repris aux empreintes, avec le catalogue
et les modèles historiques du 049. Le paquet embarque les 4 892 octets
des crédits amont sous `_forge-export/reference/notices/data/scene/`,
avec la même empreinte et une entrée de licence. La zone, la végétation,
l'environnement, les surfaces et les six révisions historiques
correspondent au 049. Le contrôle réutilise les bibliothèques du cache
release, sans lancer l'interface Studio ni installer le jeu. Les droits
déclarés restent privés. Les 437 421 640 octets de fichiers de travail
ont été retirés. Reçu et programme de contrôle, 7 878 octets sur F:
`unified-windows-20261107-049/R7-08-reference-credits-export-049-data.json`,
SHA-256 `cd4626880ef6478e75e1a264861fdac122ab27b99edaada67d89087e36279478`.

L'inventaire du Setup 049 relit ses neuf composants et les 475 fichiers
du paquet Studio, dont 472 entrées de notices et de provenance. Les
plugins NSIS et le bitmap correspondent aux fichiers du toolset local.
L'empreinte du programme WebView2 embarqué correspond au paquet Microsoft
dont le runtime interne 155.0.4283.45 a déjà été vérifié x64. Les trois
identités de dépendances Web encore en réserve et les conditions CRT
restent consignées dans le reçu. Les fichiers historiques à l'origine
du module Tidewater imposent toujours l'usage privé du réexport.
Reçu `unified-windows-20261107-049/R7-08-windows-studio-setup-scope-049.json`,
SHA-256 `e9218f5329d233916350c5c73e6acd90e6203aa5dee8994b2023952365d9ef16`.
[Licence NSIS](https://nsis.sourceforge.io/License) et
[déploiement hors ligne WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution#offline-deployment).

Le préparateur Windows recalcule les identités sans texte dans les trois
index du kit. Il refuse un audit qui omet une identité, même avec une
empreinte d'audit valide. Les 13 tests de préparation Studio passent.
La relecture des index aux empreintes du Setup 049 retrouve les trois
réserves WASM et aucune dans la portée Cargo native Windows du Studio.
Le reçu de 1 718 octets sur F: consigne la relecture sans réassemblage du 049.
`unified-windows-20261107-049/R7-08-windows-studio-notice-gaps-replay-049.json`,
SHA-256 `f954274f38d430e81750780f728fa27383b10d9149ef02fd67a4d70b882ae650`.

Le WASM aux empreintes du Setup 049 contient sept fonctions qui passent
la même adresse de localisation issue de `drain_filter_polyfill 0.1.3`.
Elle désigne la ligne 148 de `src/copypasted_impl.rs`, où l'implémentation
indexe le vecteur à compacter. Le désassemblage établit la présence de
ce chemin dans le WASM livré. La comparaison avec la source Rust du
supplément retrouve 167 lignes identiques dans l'ordre. La révision
nightly exacte et la notice propre au paquet restent à établir.
[Polyfill publié](https://github.com/whentze/drain_filter_polyfill/blob/9dc585f3eec048bfc3faf8656e07c4a19273c38f/src/copypasted_impl.rs)
et [référence Rust](https://github.com/rust-lang/rust/blob/69f9c33d71c871fc16ac445211281c6e7a340943/library/alloc/src/vec/drain_filter.rs).

Le graphe verrouillé et les deux sites d'appel confirment que
`convert_case_extras` teste la casse des identifiants dans la macro hôte.
Pour `erased`, le WASM 049 conserve le chemin et le diagnostic de son
appelant Tachys. Les instructions optimisées propres à `erased` restent
à attribuer. Les trois réserves de notices restent dans le reçu de
distribution. Reçu de 4 580 octets sur F:
`unified-windows-20261107-049/R7-08-studio-wasm-instruction-scope-049.json`,
SHA-256 `06c812528fae7e234b66b9c73098803aef117674c605b1780d24d150c3f136d8`.

Dans l'invité 036, la collection du Studio a reçu les deux images originales
de qualification : catalogue révision 2, SHA-256
`2e8181767b9c16c995763604391ad158392ae0b4c14cbd96d0535bdf9bead547`.
La variante verte conserve son parent bleu, la preuve de licence privée et
sa source de retouche. Voir `import-catalog-036.json` et
`import-variant-applied-ui-036.jpg`. Le montage inscriptible
`C:/ForgeQualification/PresentationResults` vise le lot 036 ; `Results`
continue de viser le lot 035. Aucun nouvel invité ni redémarrage du poste.
Consigne propriétaire : les essais Gate 7 déjà autorisés se poursuivent
sans redemander une confirmation à chaque installation ou étape de test.
Conserver cette autorisation dans ce périmètre de qualification isolée ;
respecter les nouveaux ordres d'arrêt, sans en déduire de publication Git.
Stockage du lot Cairns 019 terminé. Les fichiers `qualify.exe`, `.pdb`,
objet intermédiaire et `qualify.rs` ont été retirés de la qualification E:
après contrôle de l'absence de processus actif et comparaison exacte du code
avec le champ `reconstruction_harness` du reçu 019 conservé. Retrait de
15 977 658 octets. Le module, la sauvegarde et le candidat autonome restent
disponibles pour les contrôles de coexistence et de migration.
Stockage 020 : la copie d'assemblage `forge-studio.forgepack` a été retirée
de `.build/tmp/gate7-studio-delivery-20261106-020` après lecture intégrale du
même membre dans l'installateur 020 conservé. Les 342 631 099 octets ont le
SHA-256 `8ac24223905d90c7679875d2ca3575fc536442aa7ccafc415921b02c6f33d110`.
L'installateur 020 reste disponible pour un retour arrière. Le paquet 021
reste en usage pour la qualification en cours.
Pour partir du terrain plat, ouvrir la carte créée et utiliser « Activer les
règles de jeu dans cette carte » avant la sauvegarde. Le Studio crée ainsi
le joueur initial exigé pour l'export. Rouvrir « Projet et jeu » carte
fermée, choisir cette carte comme départ et sauvegarder la configuration.

R7-08 : le WASM livré par le 020 a la même empreinte que le WASM 018 audité.
Les archives verrouillées `erased 0.1.2` et `convert_case_extras 0.2.0`
correspondent aux manifests d'origine et aux sources de leurs commits
de publication respectifs. Les deux manifests déclarent MIT et leurs arbres
amont ne contiennent aucun fichier de licence ou d'attribution distinct.
Reçu `R7-08-upstream-source-reconciliation-020.json`, SHA-256
`92ebd87fe64ced3ce17e32ff90fb5929e4e2c2e49764555ff65d9d2f2b224fc2`.
Les réserves d'attribution et de portée binaire restent ouvertes. Les droits
sur le code CRT désormais lié aux binaires Windows relèvent des conditions
Microsoft applicables au toolset, séparément des notices Cargo.
[Conditions Build Tools 2022](https://visualstudio.microsoft.com/license-terms/vs2022-ga-diagnosticbuildtools/)
et [liste de redistribution 2022](https://learn.microsoft.com/en-us/visualstudio/releases/2022/redistribution).
Les deux paquets effectivement qualifiés pour le lancement et la reprise de
« Mon projet » ont été relus et vérifiés aux empreintes. Le paquet de jeu
embarque 471 fichiers de licence et le module ne référence aucun asset du
catalogue. Les index Cargo de base et des assets correspondent à ceux du
candidat Trois Cairns 017. Les deux index complémentaires diffèrent.
Reçu `R7-08-clean-windows-game-020-notice-scope.json`, SHA-256
`839374309f33c82946eb34aee508ba80930b1b9908a0689a75f694e444330a57`.
Les graphes Cargo Windows du moteur de jeu et du launcher conservent les
mêmes 613 identités cumulées et les mêmes notices candidates que le 017.
Les deux index modifiés conservent leurs entrées de paquets. Le complément
ajoute les pièces de provenance de `drain_filter_polyfill 0.1.3`, avec
l'origine exacte encore en réserve. La police Fira Mono du moteur 020 a été
retrouvée octet pour octet et sa notice vérifiée dans le paquet. Reçu
`R7-08-clean-windows-game-020-notice-delta.json`, SHA-256
`d9ae594e853666c6df1e5cfd5a4de4873ebc89c27c6ac7973015763ac6b36f00`.
Le jeu est en usage local privé. La portée des notices pour les binaires et
les ressources intégrées au moteur reste à établir.
L'inventaire extrait de l'installateur 020 contient neuf entrées. Les trois
plugins et le bitmap correspondent aux fichiers NSIS 3.12, avec compression
zlib, et les trois fichiers Forge au dossier d'assemblage. L'exécutable
WebView2 embarqué correspond au fichier Microsoft vérifié au build.
Reçu `R7-08-studio-installer-components-020.json`, SHA-256
`89cd0185a238ef8a7a893d0ed614db96ea8bb12d92ef196230f2f8fc46b8c630`.
Le stub WebView2 est x86. L'extraction hors exécution du fichier épinglé
confirme que le setup interne et `msedgewebview2.exe` 155.0.4283.45 sont
x64. Reçu `R7-08-webview2-payload-x64-020.json`, SHA-256
`9315d40aa20c6fb939cd88dc9e2f9dafdd6c9ed6dbc4087865bf05a363ade954`.
Le Sandbox Windows 11 du 6 novembre conserve WebView2 156.0.4314.58
après la tentative de retrait via le programme Microsoft signé, qui
retourne le code 93. L'installation hors ligne sur un système dépourvu
de WebView2 reste à vérifier.
La source NSIS traite désormais la valeur `0.0.0.0` comme une absence et
poursuit la recherche dans les autres emplacements. Le détecteur extrait
de cette source passe quatre cas dans le Sandbox avec des clés de test
distinctes. Les huit tests de l'installateur passent, dont la compilation
des deux profils NSIS. Le binaire 021 précède la correction.
Reçu `R7-10-windows-webview2-detection-021.json`, SHA-256
`45463e8abb13ee4102f3dcac4c135cc06fe17e7aad88e9924e0b72ba1560da99`.
[Licence NSIS](https://nsis.sourceforge.io/License) et
[déploiement WebView2 hors ligne](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution#offline-deployment).

R7-06 rejoint R7-01/R7-03/R7-04/R7-05 : 45 preuves et 11 sources revérifiées,
32 tests actuels du launcher standalone, frontend/sources 011–015 identiques
et parcours réel 011 conservé. Reçu `R7-06-game-launcher-assessment-015.json`,
SHA-256 `decfb4a2c863f3304040240a4bdbc6f5a65d96288a70b4b5df9088626f6025e1`.
Ne pas refaire le cycle du launcher sans nouveau changement ; 015 non installé.
Les réglages indiqués non appliqués restent hors qualification fonctionnelle.
R7-04 rejoint R7-01/R7-03/R7-05 : 49 preuves et chemins de ressources fermés
revérifiés, 25 tests actuels des refus après changement des services. Reçu 015
`R7-04-runtime-packaging-assessment-015.json`, SHA-256
`5ef6f45265c58aeb924523d1b1578e315a71aa5c75792a440eda80b68bb8a2be`.
Ni OS propre, ni licence/redistribution, ni installation 015 déduits de ce verdict.
R7-05 rejoint R7-01/R7-03 après rapprochement vérifié de 45 preuves : reçu 015
`R7-05-graphics-profiles-assessment-015.json`, SHA-256
`cf9dc52a2f100dc62fa3284e60183307d59c4050d728801c47a6729c97e83b38`.
Le refus Medium historique est conservé ; ancienne et nouvelle captures vues,
motif de halos non observé dans le 015. Mesures par profil de 39–43 échantillons
sur environ 1,1 s ; aucune acceptation FPS/parité Tidewater n'en est déduite.
R7-02 est qualifié localement Windows. Le Studio 015 a sauvegardé deux copies du cairn existant
à X=80/86 m, Z=-89 m hors cellules des PNJ, puis exporté le jeu privé 1.0.2
`game-8496-1793895505129626800` (workspace révision 14, aucun autre contenu changé).
Le parcours 015 a préparé un agrégat, puis refusé la qualification à l'étape 0 :
l'observateur GPU de HLOD comptait aussi la caméra hors écran de réfraction.
Preuves et export rapproché dans `profile-prepass-lifecycle-20261105-015/hlod-complete-001`.
Correction dans `static_hlod/gpu.rs` sur le marqueur commun `OffscreenSceneCamera` ;
15 tests ciblés passent, dont refus de deux caméras de scène. Runtime 016
construit ; le parcours s'arrête encore à l'étape 0 après 123 activations
d'une image. À chaque activation les casters passent de 28 à 26, l'atlas
est recréé et invalide l'agrégat qui le lit. Reçu `hlod-shadow-feedback-refusal.json`
dans 016, SHA-256 `018ad10e1fff5edfac7cb64aa00ea11aa4eef5591e1a037dee7e566dc446fd4c`.
Le moteur 017 conserve les casters source avec leur visibilité logique dans
le collecteur d'ombres existant ; les contrôles d'admission GPU sont conservés.
Build runtime 017 terminé sur cache partagé ; PID 26692 sorti normalement après
les 18 étapes du même parcours 1.0.2. Lot prêt sur les quatre profils, détail
restauré, puis lot réactivé ; 28 casters dans les fenêtres agrégées mesurées.
Trois captures revues, 21 preuves épinglées dans `R7-02-hlod-assessment-017.json`
(dossier 017), SHA-256 `e8878ffdf90deb114011fa627a47d56dbda5dad9c54d8b000219ebc5688212e2`.
Studio 8496 fermé, 1.0.2 non installé. Le kit privé 017 est assemblé sur les
mêmes sources compilées : 471 fichiers / 141 196 895 octets, runtime
`82f6aa364a2fcf1262298ccccc0915fbdf95a3bae4915334b2134ac66a2398e7`.
Reçu dans 017, `game-kit-assembly-receipt.json`, SHA-256
`b36afaba250a3f4d2c080d4b3a0f46247d6d014e24c390a5dba018d68e0b6a6c`.
Kit sous `.build/tmp/gate7-windows-game-kit-engine-20261105-017`.
Frontend, Studio et CLI 017 terminés. Studio `441127607758195ae8a4d5f0f808c32241c1e1ac4ef4f49be214cfc36170eb31`.
Kit adjacent vérifié dans le `bin` de qualification 017. Export réel
`exports/game-4328-1793901617923867300` depuis le workspace révision 14.
Tous les payloads sont vérifiés et le module C6 est identique au module de
l'essai HLOD. Reçu `studio-ui-export-verification.json` SHA-256
`f27611839c79a1bfd4116b318aa67f122b0ead212ae85232cd6326eb21b06bf2`.
Studio fermé. L'installation neuve 017 autorisée par le propriétaire est en
révision 1. `Jouer.exe`, partie neuve, F5 et reprise via le launcher observés.
Reçu `installed-fresh-resume-cycle.json` SHA-256
`a7ac594a162e6d8978c6ba79bbd76b4c6b8b26db26cf48524e51de76c58db227`.
Le rapport final de reprise indique une projection indisponible à la fermeture,
malgré la sauvegarde reprise, 2 008 ticks et une sortie native réussie.
Après installation des Trois Cairns, une autre reprise du Camp 017 termine
avec la projection prête, sans reproduire l'indisponibilité. Pour dater un
éventuel retour, le runtime commun rapporte les première et dernière images
prêtes et le moteur date l'événement de fermeture. Vérification Cargo ciblée
réussie (`r7-09-resume-projection-frames-check-20261105.log`, SHA-256
`17670cf73950abaf1f75f2e9731772424dc852fc0145654197f7901c1c18b197`).
Les deux jeux installés précèdent cette instrumentation.
Les cycles 008/011 restent les preuves du changement de runtime à carte identique.
Le rapprochement 011→017 relève 19 différences d'entrées compilées hors
launcher, services de distribution et sauvegarde. Reçu
`R7-09-install-path-reconciliation-017.json` SHA-256
`b6054dd6632f16e7c196b191a49cacb5941a73ea334205877c2de58606f9df5b`.
Le même Studio 017 a exporté un second jeu, Les Trois Cairns, depuis le
workspace séparé `second-game-workspace`. L'outil de qualification a préparé
le workspace à partir du module procédural existant. Le titre, la carte de
départ et la configuration ont été enregistrés dans le Studio avant l'export.
Les deux paquets embarquent le même moteur. Leurs identités, modules et
emplacements de sauvegarde sont distincts. Les quatre fichiers du second
export totalisent 160 901 709 octets et leurs payloads ont été vérifiés.
Reçu `second-game-export-verification.json` SHA-256
`615d60876a16110900126f0fb50b9160accdb0b8139f3023ce9bd0555fd31e46`.
L'installateur des Trois Cairns a refusé la racine du Camp 017 avec le
motif « A different game is installed here ». L'installation et la sauvegarde
du Camp ont été vérifiées par SHA-256 après ce refus. Le plan d'installation
neuve des Trois Cairns a ciblé `second-game-install`.
Reçu `second-game-installer-plan-ui.json` SHA-256
`ceefd097dae724222447f5f40b9b142ce1552d86877102479fecad6868969dd5`.
Installation autorisée et appliquée. Nouvelle partie puis reprise des
Trois Cairns, puis reprise du Camp, toutes trois terminées avec projection
prête. Les deux jeux utilisent le même binaire et le même dossier de données
de qualification, avec des sauvegardes séparées par identité de jeu.
L'installation et la sauvegarde du Camp sont identiques au relevé préalable.
Reçu `R7-09-installed-games-isolation-017.json`, SHA-256
`1a20761e1ae34cec51685f80f59033e0ca8ca1e5b4e6593829114f6a2d36fce1`.
Le Studio du second jeu est fermé. Le retrait du cache temporaire
`second-game-studio/webview` a été bloqué avant exécution par le contrôle
automatique. Le dossier contient 305 fichiers et 73 610 970 octets. Ne pas
retenter ce retrait par un autre moyen. Le workspace auteur et son export
sont dans le dossier frère `second-game-workspace`.
Le Camp 017 a été désinstallé par le programme prévu après autorisation.
Les deux sauvegardes et le manifeste d'installation des Trois Cairns
correspondent au reçu de coexistence. Les 2 271 fichiers déclarés des deux
paquets du Camp (412 090 735 octets) et le programme temporaire sont retirés.
Le dossier `packages` vide et le verrou de zéro octet subsistent. Reçu
`R7-09-camp-uninstall-saves-017.json`, SHA-256
`acd8567fb5e23dd304ca6b5371f180b91e25577d783c5f5b96a02b831bebe326`.
Reçu `uninstall-plan-ui.json` SHA-256
`9e089a8992ee222b657fe21ffef0c1d4375f55f8c0b5bafc3180ad3334096675`.
Installateur privé du Studio assemblé sur les mêmes sources. Reçu
`studio-installer-result.json` SHA-256
`86c2a7df15d89432e99ee769ee614176c782cd9676af0a20dfa58291c07ed54f`.
R7-10 : le journal Panther de l'invité propre situe l'échec dans la phase
Windows `Sysprep_Online_Specialize_Bcd` (`0xC0000098`). Le relevé ciblé et
les révisions de reprise sont épinglés dans `final-qualification-preflight.json`,
SHA-256 `c4cd14abdec9538dcf049e79fddd1d5c2110a3da2bc8d1c86d63523cda0fb333`.
Windows Sandbox est disponible comme fonctionnalité hôte désactivée.
Configuration 017 préparée dans le dossier de qualification, accès en lecture
seule aux deux distributions, réseau désactivé, vGPU activé. Le démarrage
prévu relève l'OS, les outils absents ou présents et les empreintes des entrées.
Reçu `windows-sandbox-plan.json` SHA-256
`1b11d4effd400cffa67218d2d06946fe54163464a875e490d8b2ef656f2928d0`.
Le manifeste embarqué et l'installateur 017 correspondent aux reçus de
construction. Le Studio prépare une racine auteur vide et la bibliothèque
d'assets est facultative. Sur l'invité, ouvrir « Projet et jeu », initialiser
l'espace, créer un projet, un module et une carte, puis activer et sauvegarder
les règles de jeu de la carte. Configurer cette carte comme entrée du jeu et
créer les paquets privés. Le parcours est rapproché de huit sources du 017
dans `R7-10-studio-first-run-preflight.json`, SHA-256
`29a7e49e45057b701163f4d2810ac2721309462954df93cf498dcac15c9f8c66`.
L'installation, l'export et la reprise sur l'invité restent à observer.
Dans le checkout commun, le navigateur de modules accepte désormais une
racine auteur vide. L'initialisation reste explicite. L'ouverture demandée
explicitement et les autorités existantes invalides gardent leurs refus.
Deux tests ciblés passent, dont le parcours sur workspace existant, journal
`r7-10-first-run-workspace-host-20261105.log` (racine de la campagne), SHA-256
`8893a6a65d1cc2e739f8632e6ff43c9232ea1222b22307c269452d922501b2ae`.
Le Studio 017 précède cette correction. Sur le 017, l'action d'initialisation
reste accessible dans « Projet et jeu » après le message de fichier absent.
R7-08 : le catalogue réellement embarqué du module 1.0.2 autorise les deux
géométries originales, mais déclare `redistribution: forbidden` pour
`vv-tree-pine-a` et `vv-tree-oak-a`. Le module et le kit restent privés.
Reçu `R7-08-private-candidate-content-scope.json` dans 017, SHA-256
`4698d1430840986a301a27aab41db40ed919f1290c528e0288a5fef25ea02d57`.
Établir leurs droits ou remplacer les modèles avant redistribution ; la licence
du code Forge ne couvre pas ces déclarations d'assets.
Les Trois Cairns offre un périmètre distinct. Les 489 fichiers déclarés dans
ses deux paquets installés correspondent à leurs manifestes. Ses deux GLB
et recettes déclarent CC0-1.0, sans les arbres du Camp ni texte historique
importé. Le contenu RPG correspond au module source, déclaré privé. Les
613 identités des graphes Cargo Windows cumulés du moteur de jeu et de son
launcher ont chacune un texte de notice candidat. La vérification couvre aussi
la notice de la police, les textes et les archives des neuf composants Symphonia.
Reçu `R7-08-trois-cairns-installed-notice-scope-017.json`, SHA-256
`10c06c9ffb476b5ccf446fb953913321c7be80e814bbd65918d967d91b31a07c`.
Le candidat reste privé. L'inventaire Cargo conserve sa portée de graphe
de build et les réserves du Studio sont traitées séparément.
La police Fira Mono a été retrouvée octet pour octet dans le runtime du kit 017.
Le texte OFL embarqué correspond à la notice amont épinglée. Reçu
`R7-08-font-kit-017.json`, SHA-256
`8f411e12754711adda3e39d61cdc740f24aaa157fbb16498aa16dad89b235b46`.
Le WASM épinglé du Studio 017 contient le chemin source de
`drain_filter_polyfill 0.1.3` dans sa section de données. Le fichier adapté
de l'archive Cargo verrouillée correspond sur 87 lignes de code alignées à
la référence Rust 1.66.0. Le reçu rapproche le texte Apache, le texte MIT
et le fichier COPYRIGHT du même commit Rust, sans déterminer le point de
fork exact. Candidat de notice `R7-08-studio-wasm-rust-attribution.json`,
SHA-256 `6b24127a8f8730d293b730780b9da7f9d711b8ec434f1f7bcfa63b923f6f25b4`.
Le bundle privé `E:/OpenRPG_Forge/.build/tmp/gate7-notice-bundle-015`
intègre ce rapprochement comme référence de provenance transitive. Le
collecteur a conservé les entrées précédentes et le préparateur Windows a
vérifié les trois documents Rust et la preuve 017. Reçu
`R7-08-notice-bundle-015-candidate.json`, SHA-256
`c4ab930c86a7cf4b70fb79e42c851cbe9142fd490423d476ee7e4a08ed8779ab`.
Le Studio 018 réutilise le graphe 014 avec ce reçu. Le bundle 015
contient 437 fichiers et 2 923 790 octets. Le 014 reste la référence des
paquets déjà vérifiés. Les trois identités restent en réserve et le Studio
installable 017 utilise toujours les notices 014.
R7-01 rejoint R7-03 après rapprochement vérifié : reçu 015
`R7-01-streaming-assessment-015.json`, SHA-256
`e4e7c0b2cc98fb6849e9f26b5d67c907d23f46028988a9605d3c6b8819341569`.
Le Studio 015 a exporté `game-33412-1793890314656093300` : quatre fichiers /
431 789 877 octets, tous les payloads et deux inspections natives vérifiés.
Reçu `studio-ui-export.json`, SHA-256
`0434e332d728edc7230305298f48956eaca8f8cc34050a989859c12b60a1529f`.
Studio 33412 fermé après cet export. Source auteur alors en révision 12.
L'installation est désormais 008 révision 3 : rollback UI 011 → 008 autorisé,
réouverture via `Jouer.exe`, reprise et sortie du jeu vérifiées. Le SHA du fichier
de sauvegarde et son état restent identiques. Reçu `installed-rollback-resume-cycle.json`
dans 011, SHA-256 `b80cbbbcd6f47fffbb6d913bf9adaac3de1d53e2ffa5471e81d487276d5f76a3`.
La désinstallation du Camp 017 est vérifiée ci-dessus. Le parcours installé
du Studio 018 reste à qualifier.
Ne pas refaire les cycles de profils ou le trajet streaming acquis.
Le retrait des replis 014 a été refusé avant exécution (`blocked by policy`) :
341 912 159 octets conservés, zéro retiré, reçu `replaced-014-retirement-result.json`.
Ces deux nouvelles cibles sont désormais refusées aussi ; ne pas les retenter
par un autre moyen. Les refus antérieurs 005/008/011 restent préservés.

**Profil 015 : correction GPU vérifiée, consolidation terminée.** Runtime 015
construit sur source stable `a0f90d61…`, binaire SHA-256
`41939b16a6c506de4205d4fb2f02dd3549254e0e47f8e48dc1b9a4cadf4f81c7`.
Cycle natif PID 29108 terminé : 14 étapes, Low/Medium/Ultra/High appliqués,
persistés et pleinement prêts. Redémarrage PID 24816 terminé : High relu malgré
le défaut CLI Ultra. Aucun défaut de validation GPU ; quatre captures inspectées.
Préférences isolées ; sauvegarde réelle joueur `223dc3c4…` inchangée. Reçu 015
`qualification-assessment.json`, SHA-256
`f218506709e3dbc37a0a7678805bbd51c9c99899e2ffd91ef67333b1140e328e`.
Ne pas rejouer ce cycle sans nouvelle cause. Sources compilées désormais figées.
Les cinq stages sont terminés sur sources stables. Suivi functions 2870,
frontend 41786 et Studio natif 81801 clos. Binaire Studio SHA-256
`445145909f3a9ba486d7b665b749fdabd54a08a5b7beed4c37c74f3acd42cd0b`.
Le frontend WASM/JS/HTML/CSS est byte-identique au 014 ; seuls deux manifestes
de pins changent. Reçu `frontend-byte-comparison.json`, SHA-256
`b1260fa6215ee21ad946133f0f6b6963c81a9827b87c02df76db11fc69e417aa`.
Kit assemblé (8501 terminé), version `0.1.0-preview.20261105.015`, 471 fichiers /
141 195 871 octets sous `.build/tmp/gate7-windows-game-kit-engine-20261105-015`.
Reçu d'assemblage copié à l'identique dans les preuves 015, SHA-256
`4350e7ab06375cc40cedc1f960d23ea081aea97188e9c648ed97ba84464c0620` ;
manifest kit `39465b28e28f7ae87a11ec113f62437fea6893c8b11899cb261f21e33570fdde`.
Le stage Studio terminé du pipeline existant utilisait
`--studio-game-kit-root E:/OpenRPG_Forge/.build/tmp/gate7-windows-game-kit-engine-20261105-015`
et `--studio-game-kit-receipt-sha256 4350e7ab06375cc40cedc1f960d23ea081aea97188e9c648ed97ba84464c0620`.
Même evidence 015, cache/cargo-home/temp partagés et `--shared-jobs 16`.
Export 014 conservé pour le rapprochement R7-01 ; export 015 vérifié ci-dessus.
Le kit 015 est privé, pas qualifié pour redistribution ; 015 n'est pas installé.

Notice indépendante préparée pendant ce build : `drain_filter_polyfill 0.1.3`
déclare MIT OR Apache-2.0 au commit publié `9dc585f3…`. Les trois blobs source
de l'archive Cargo correspondent aux IDs Git de l'arbre amont complet ; aucun
fichier NOTICE/licence/copyright dans cet arbre. Le texte Apache déjà vérifié
`cfc7749b…` fournit un candidat, sans inférer de titulaire ni clore l'attribution
du code décrit comme repris de Rust. Reçu 015 `notice-drain-apache-candidate.json`,
SHA-256 `f27c1ef6bd1d516c6e20e0f1a0322d12d4749d756b6adb1f4cb40b31df49a42e`.
Ni le bundle 014 ni le kit 015 figés n'ont été modifiés. Deux autres textes
candidats restent absents (`convert_case_extras`, `erased`) ; R7-08 reste ouvert.
Rapprochement des 15 graphes existants : les trois réserves Cargo concernent
uniquement le frontend WASM du Studio ; aucune n'apparaît dans les graphes du
runtime ou du launcher autonome, et le paquet Windows 1.0.2 ne contient aucun WASM.
`convert_case_extras` choisit seulement des noms d'identifiants dans la macro hôte.
Reçus 015 `notice-standalone-scope.json` (`ad91f7b0…`) et
`notice-convert-case-host-use.json` (`31e328ba…`). Séparer les périmètres auteur
et joueur au bilan ; aucune notice ni réserve de droits n'a été retirée.

**Historique 014 : crash réel Ultra → Low, corrigé au 015 ci-dessus.**
Le cycle de profils du binaire exporté 014 (PID 27540) est terminal, sortie 1.
La validation GPU refuse `background_motion_vectors_pipeline` : la pipeline
conservée attend l'attachement Rg16Float absent après retrait du prépass de
mouvement. Aucun redémarrage de persistance effectué ; les anciens reçus du
trajet et de l'export restent intacts. Reçu central 014
`profile-cycle-001/assessment.json`, SHA-256
`70257f32c07c53b9e2abcb4895b108f3fa446bdf90bec6c3e4458d5c43852978`.
Correction uniquement dans `crates/forge_engine/src/source_graphics.rs` : le
contrôleur commun retire avant rendu les composants de cache d'un prépass de
mouvement devenu inactif ; les vues actives et les autres effets restent acquis.
Les cinq tests ciblés passent (69965 terminé) ; Clippy natif (14247) et Web
(97208) passent. Revue bornée de l'agent terminée, aucun défaut concret signalé.
Le build runtime 015 est terminé, contrôleur **18273** clos, dossier central
`profile-prepass-lifecycle-20261105-015`. Source stable
`a0f90d6167669659959916a6f3f56278822b4d72f284f8e7b92d34486b73c000` ;
un seul fichier compilé diffère de 014 (`source_graphics.rs`). Reçu CPU
`correction-assessment.json`. Le cycle affecté et sa reprise ont été exécutés avec :
`python E:/OpenRPG_Forge/.build/tmp/gate7-profile-cycle014.py profile-prepass-lifecycle-20261105-015`.
Le verdict du cycle natif est PASS ; la consolidation indiquée plus haut est autorisée.
Le pilote temporaire accepte maintenant ce candidat explicite ; il épingle le
binaire au reçu de build et réutilise les contenus immuables déjà installés.
Les sorties 014 existent et doivent être préservées ; ne pas les écraser.

**Studio 014 exporté et vérifié ; aucun build actif.** Frontend 45395 et Studio
22428 terminés, source stable `c2d3ff4f…`. Studio `e092fafd…`, preuve native
`399e14c1ff437146b7ec0114c6300c26a56b1309f88a3fdcaef2c463ea3864f5`.
Intégration adjacente vérifiée dans le même workspace, révision 12 inchangée.
Le vrai chargement UI montre terrain, végétation, bâtiments et eau ; reçu 014
`studio-viewport-proof.json`, SHA-256
`266bae2e526491ec89beb5c14671644c1112a5b633ac3f28a705478bf7551925`.
Export réel `exports/game-18328-1793885930736376800` : quatre fichiers /
431 771 957 octets, module identique aux exports précédents, tous les payloads
et deux inspections natives vérifiés. Reçu `studio-ui-export.json`, SHA-256
`61e76c7226803dde3789c3473e69522061a8003c3b9a9f0d6a927467933b670b`.
Studio 18328 fermé proprement après le succès ; installation toujours 011,
révision 2. Le rollback 011 → 008 attend son autorisation distincte ; aucun clic
final effectué. L'export 014 n'a pas été installé. Ne pas refaire le trajet ou
le chargement acquis sans nouvelle cause.

**Stockage résiduel 014 : 341 810 271 octets de repli 011 non retirés.** Le
retrait gardé de `.previous-studio-011.exe` et `.previous-kit-011` a été rejeté
par le contrôle automatique avant démarrage (`blocked by policy`). Aucun octet
retiré et aucune reprise par un autre moyen ; reçu
`replaced-011-retirement-result.json`. Ces deux cibles s'ajoutent aux refus
historiques ci-dessous ; ne pas les retenter. Le core installé 011 reste, lui,
une référence fonctionnelle distincte pour le rollback.

**Kit 014 assemblé ; rollback UI prêt en attente de confirmation.** Assembly
8816 terminé, kit privé `0.1.0-preview.20261105.014` sous
`.build/tmp/gate7-windows-game-kit-engine-20261105-014`, 471 fichiers /
141 177 951 octets. Reçu SHA-256
`3c67f757bfb7e202e18c467441d45974aa608f371eaf44ff21d1f2572d594e6d`,
manifest kit `a7764110101d0d75a4adf6eca716792cfa21dc9a6168ffd527fb81c57e4321fb`.
Copie byte-identique du reçu dans le dossier de preuves 014. Le frontend 45395
est terminé ; le build Studio 22428 utilise ce kit et son reçu via les options
`--studio-game-kit-root` et `--studio-game-kit-receipt-sha256`.

Pour R7-09, le launcher 011 a préparé le rollback dans le dossier installé de
qualification : core 011 `15014fa0…` → core 008 `8576ed1c…`, module inchangé,
sauvegarde `223dc3c4…` conservée. **Clic Restaurer non effectué**, confirmation
asynchrone demandée ; ne pas l'inférer d'une continuation de goal. Le processus
launcher **9748** reste sur le plan, fenêtre retournée **131730824**, app sous
`install/packages/game-3e342129-6ac0-5b9d-a699-d1c4ff2069b9/15014fa0…/bin/forge-game-launcher.exe`.
Reçu 011 `rollback-plan-ui.json`, SHA-256
`43272fcafa2b366324e4832ea43ba33d923b2381d13a76d2c2718a0f23d5840a`, capture
`rollback-plan-ui.jpg`. Réobserver avant entrée. L'autorisation de mise à jour
011 est déjà consommée par son parcours PASS, elle ne vaut pas accord rollback.

**Consolidation 014 — frontend Studio en cours.** Les trois builds natifs
runtime, launcher et CLI sont terminés sur le même manifeste source stable
`c2d3ff4f45ea2df2495d43125bde6bad38918750ae44f420c6ddb9d7863f9f74`.
Runtime `9e08b9d4…`, launcher `b5d9e578…`, CLI `1d015404…` ; preuves dans
`grouped-model-readiness-20261105-014`. Launcher 42230 clos ; CLI terminé.
Préparation frontend en cours, contrôleur **45395**, avec Trunk 0.21.14 et
Binaryen 131 hors ligne, cache/temp communs. Ne pas redémarrer ce build sur
simple expiration d'observation. Après succès : assemblage du kit privé 014
puis build Studio avec ce kit, intégration dans le workspace de qualification
et export réel. Le 011 reste installé et son cycle mise à jour/reprise est PASS.
Le trajet 014 est terminé ; aucun jeu ni installateur de qualification ouvert.

**Trajet 014 terminé et consolidation du kit commencée.** PID 19116 clos,
contrôleur 14044 terminé, sortie 0. Parcours 290,96 m, 799 observations physiques
prêtes et au sol, aucune erreur de projection ; trois captures inspectées.
Même module, scénario et profilage CPU que 013 : audits 71 055 → 40 956,
temps cumulé 33 164,69 → 28 422,68 ms ; moyenne du scope main 39,16 → 33,49 ms.
Moyenne/p95 des intervalles strictement prêts : 41,09/52,29 ms ; aucun 60 FPS
ni parité Tidewater qualifiés. Reçu 014 `qualification-assessment.json`, SHA-256
`06d8af99b9ac48631ec415bb699d5d742b5caa0c45a01ad58537a44a5c411440`.
Ne pas relancer le trajet. Les sources du runtime restent figées pour le kit.
Construction du launcher 014 démarrée via le cache partagé ; frontend temporaire
`.build/tmp/gate7-standalone-launcher-frontend-20261105-014`. Le premier appel
avait été refusé avant tout travail pour un `--shared-jobs` non applicable à ce
stage ; corrigé avec le seul `--launcher-jobs 16`. Aucun travail relancé en double.
Restent ensuite CLI, frontend Studio, assemblage du kit et Studio, sur les mêmes
sources ; aucun nouveau jeu de données ni copie de code. Le jeu installé reste
le 011 dont la mise à jour et la reprise viennent d'être qualifiées.

Launcher 014 terminé (42230 clos), source stable ; CLI 014 lancé ensuite avec
`--delivery-jobs 16`. Pas de `--shared-jobs` sur les stages launcher/CLI. Une fois
CLI terminal PASS, préparer le frontend avec les paramètres hors ligne déjà
validés : Trunk 0.21.14, wasm-opt 131
`E:/OpenRPG_Forge/.build/tools/binaryen-131-001/bin/wasm-opt.exe`, SHA-256
`a3e857e565f18773e24cb3d81d00baa70cee9be24efe775e1960565fea5e65b3`.
Employer `--stage prepare-frontend --shared-working-tree --shared-jobs 16
--frontend-offline --frontend-trunk-version 0.21.14 --frontend-wasm-opt <chemin>
--frontend-wasm-opt-sha256 <empreinte> --frontend-wasm-opt-version 131`, et les
mêmes evidence/cache/cargo-home/temp-dir. Ne pas recréer de pipeline de réemploi.

**Mise à jour UI 008 → 011 exécutée et reprise vérifiée.** Le propriétaire a
confirmé `call_17bd0bf587ba4b4b85768239bc9e7f00`. Clic réel « Mettre à jour »,
installation révision 2, core `15014fa0…`, module `c5ef2b65…` inchangé ; core 008
conservé comme version précédente fonctionnelle pour rollback. Le launcher
mis à jour propose Reprendre ; le jeu affiche « Sauvegarde reprise ». Rapports
natifs prêts, sorties réussies, aucune erreur ; état repris `340b4ed5…` et fichier
joueur SHA-256 `223dc3c4…` identiques à l'original. Aucune nouvelle sauvegarde
écrite. Jeu 12552, launcher 360 et installateur 10556 fermés proprement.
Reçu central 011 `installed-update-resume-cycle.json` et rapports bruts sous
`update-resume-cycle-001/`. La confirmation 011 n'est plus en attente. Cela
qualifie ce parcours de mise à jour/reprise, pas rollback, désinstallation ou
machine propre. Le 014 est un runtime technique distinct, pas le jeu installé.

**Build runtime 014 terminé.** Contrôleur 83953 clos, sources stables, manifeste
`c2d3ff4f45ea2df2495d43125bde6bad38918750ae44f420c6ddb9d7863f9f74`.
Exécutable SHA-256 `9e08b9d4d96c6187dcd769ee5f4210bf809e7e995f094c4d3750b7294d782965`.
Son trajet de mesure a attendu la fin de la vérification de reprise 011 ; il
est **en cours, PID 19116, contrôleur 14044**, avec profilage CPU. Aucune autre
qualification GPU ni compilation à lancer pendant cette mesure. Même scénario
et mêmes payloads que 013, mêmes options de profilage ; aucune nouvelle fenêtre
Studio ni nouvel installateur ouvert. Ne pas relancer le trajet si une simple
observation expire : vérifier le contrôleur et le processus avant décision.

**Regroupement des observations 014 en cours.** Le profilage 013 a établi
71 055 appels d'audit des modèles / 33 164,69 ms cumulées. La nouvelle observation
`ImportedTreePresentation::readiness(&World, expected_head)` partage un audit
CPU local entre GPU strict, ombres strictes et ressources de simulation. Aucun
cache par frame/tick ni suppression de barrière ; un nouvel appel observe à
nouveau les mutations, y compris dans le même tick. L'hôte consomme ce résultat
une fois par observation. `request_gpu` reste inchangé et conserve son contrôle.
Contrôle ciblé 48887 passé (un cas). Le premier diagnostic signale un réexport inutilisé,
corrigé en exposant le type depuis le réexport racine existant. Clippy natif du
raccord passé, contrôleur **73387 terminé** ; Clippy Web 99993 également passé.
Build runtime 014 en cours, contrôleur **83953**, preuves sous
`grouped-model-readiness-20261105-014`. Reçu de correction CPU/Clippy :
`correction-assessment.json`, SHA-256
`c3ed4eb84ac9c5c0fb2c380635b32a86422326f939087dff72fbb52dfad94b86`.
Aucun trajet 014 effectué. Le runtime 013 reste le dernier binaire réellement mesuré.
Une fois le build terminal PASS : lancer un seul trajet avec profilage, sans
compilation en parallèle :
`python E:/OpenRPG_Forge/.build/tmp/run-gate7-dense-route011.py grouped-model-readiness-20261105-014 --cpu-profile`.
Puis analyser sans rejeu :
`python E:/OpenRPG_Forge/.build/tmp/compare-gate7-dense-routes.py grouped-model-readiness-20261105-014 retained-imported-primitives-20261105-013`.
Les deux trajets auront le même profilage CPU ; vérifier nombre/temps des audits
et cadence, sans additionner les scopes imbriqués ni les assimiler au GPU.
L'examen des rôles de dépendances est terminé (lecture seule, aucun build/réseau).
`convert_case_extras 0.2.0` a pour seul parent `leptos_macro 0.8.17` (proc-macro
hôte). `drain_filter_polyfill 0.1.3` et `erased 0.1.2` sont des dépendances
non optionnelles de la bibliothèque runtime `tachys 0.2.18`. Cela ne prouve pas
l'attribution des octets après LTO et n'autorise aucun retrait de notice.
Reçu 014 `notice-dependency-roles.json`, SHA-256
`a041d073544fe5fa243ad90ddba8d2dee4dbf322b8160d5af5ea18a168ade58b`.
R7-08 reste ouvert, avec les droits des assets à établir séparément.

**Résultat 013 terminé.** PID 30112 clos, sortie 0, contrôleur 8360 terminé ;
291,24 m, 801 observations physiques prêtes et au sol, aucune erreur de projection.
Captures départ/aller/retour inspectées. Moyenne/p95 strictement prêts :
44,08/53,23 ms ; boucle instrumentée 151,81 s, suspensions consécutives 2,43 s.
Le 012 était sans profilage CPU ; aucune amélioration FPS nette n'est déduite.
Reçu `retained-imported-primitives-20261105-013/qualification-assessment.json`,
SHA-256 `ce3a3e6ba138295724b2b990ffc792d8799613687a8994f186993a2b4337d60a`.
Comparaison `c08006fd…`, profil CPU `69c1c57e…` dans le même dossier. Mesures de
temps écoulé des scopes imbriqués, non additives, sans timestamps GPU.
Preuves utiles au diagnostic courant : 22 fichiers / 26 397 432 octets ; module
immuable réutilisé sans copie. Le jeu installé demeure le 008, export Studio 011
inchangé et confirmation de mise à jour 011 encore en attente.

**Historique de construction 013 — réutilisation des primitives retenues.** Le chemin
`ImportedTreePresentation::select` conserve maintenant les entités dont la clé
est encore sélectionnée et dont l'audit préalable passe, comme le résident
source utilisé par Tidewater. Il ne réécrit plus leurs composants ni leur
receiver d'éclairage. Les nouvelles clés sont créées, les anciennes retirées ;
la publication et les barrières GPU restent strictes. `trees`, prototypes et
plages restent immuables ; `reveal` possède déjà le changement de visibilité.
Contrôle CPU ciblé 28675 passé : l'ajout d'un LOD préserve les ticks de
Transform/matériau de la primitive conservée ; les mutations étrangères restent
refusées. Le gain de cadence n'est pas encore mesuré. Avec le watchdog décrit
ci-dessous, trois fichiers diffèrent du binaire 012 :
`vegetation_prototypes/imported.rs`, `module_game/species.rs` et
`module_game/graphics/runtime.rs`. Ce dernier expose les spans CPU existants
de couverture/inventaire au profilage opt-in de production ; ils restent inactifs
sans `FORGE_RUNTIME_CPU_PROFILE=1`. Clippy natif strict du lot 013 passe.
Build runtime 013 terminé, contrôleur **66515 clos**, preuves sous
`retained-imported-primitives-20261105-013`, manifeste
`02126f8cdd56c26eb3468edbb4b3f586c2fdb7962e12e7334245ea7803147aa2`.
Aucun kit ni export Studio 013 préparé. Runtime SHA-256
`9f7e81252c7ca77c11bcf5d881e493b9c348b342c3ebf8021a7c47c92b084c39`,
sources stables. Trajet avec profilage CPU **en cours, PID 30112**, contrôleur
8360 ; ne pas lancer d'autre charge GPU ou compilation pendant la mesure.
Commande du trajet unique :
`python E:/OpenRPG_Forge/.build/tmp/run-gate7-dense-route011.py retained-imported-primitives-20261105-013 --cpu-profile`.
Le pilote vérifie le binaire construit, réutilise le module installé immuable,
isole les préférences et conserve les preuves. Comparer ensuite les données,
sans nouveau lancement :
`python E:/OpenRPG_Forge/.build/tmp/compare-gate7-dense-routes.py retained-imported-primitives-20261105-013 simulation-resource-admission-20261105-012`.
Le profilage CPU supplémentaire est signalé explicitement dans le reçu ; ne pas
présenter les intervalles instrumentés comme une acceptation FPS ou une parité
Tidewater. Lire les messages `runtime_cpu_sample` pour orienter le lot suivant.

**Contrôle comparatif 012 terminé — PID 33340 clos, sortie 0.** Le build optimisé du runtime
est terminé (contrôleur 20365 clos), sources stables : manifeste
`11e407e18b824c3cf904bd2a1fb145aeb57de4aefe1e530d86b45772ea66c7df`,
exécutable `8af9bc862deba9f3443d839acaf585e56e08e40636a134ef2337884febcb5f27`.
Le trajet unique reprend le module installé immuable et le même scénario ;
preuves sous `simulation-resource-admission-20261105-012/dense-route-001`.
Le trajet est terminé ; aucun jeu ni build natif actif. Le Studio et son
export 011 sont inchangés ; aucun installateur 012 n'est préparé. Contrôles CPU :
18 inventaires GPU, un cas espèces et 90 runtime passent (deux cas existants
ignorés) ; Clippy natif et Web stricts passent. Comparaison rendue :
`simulation-resource-admission-20261105-012/dense-route-comparison.json`, SHA-256
`11088e7ff05c8b2b92ca1bcbfaa7bd7a76261f61515d620e6aa439feda2c7bc3`.
Intervalles consécutifs de suspension : 87,22 s au 011, 2,23 s au 012 ; boucle
instrumentée après démarrage : 269,21 → 151,12 s. Cadence encore non qualifiée
(moyenne strictement prête 44,75 ms, p95 56,90 ms). Scénario et module identiques,
291,10 m, 799 observations physiques prêtes et au sol. Trois captures inspectées.
Le délai d'attente dans `module_game/species.rs` a ensuite été aligné sur
l'admission physique ; contrôle ciblé 11783 terminé (deux cas passés), Clippy
strict correspondant passé. Ce dernier ajustement est
absent du binaire mesuré. Ne pas présenter la source actuelle comme le binaire
012 ni reconstruire tous les livrables avant d'avoir regroupé les corrections.
Reçu de qualification ciblée `simulation-resource-admission-20261105-012/qualification-assessment.json`,
SHA-256 `7995394924fee0a34bc6787c448aeb77fdff8487f8a064efed559bceeda20a86`.
La source après ajustement du délai est épinglée dans `source-after-watchdog.json`
(`bcef343a…`) ; seul `module_game/species.rs` diffère du runtime 012 construit.
Retrait du travail éphémère 012 refusé automatiquement (« blocked by policy »),
commande non exécutée : zéro retiré, deux fichiers / 86 octets conservés
(préférences privées et verrou vide du processus terminé). Ne pas contourner
ce refus ; reçu `work-retirement-result.json`. Le trajet et ses timings/captures
sur F: restent utiles pour l'analyse de cadence et la comparaison suivante.
Volume courant des preuves 012 : 25 fichiers / 31 798 766 octets ; aucun payload
de module recopié. La sauvegarde joueur 008 d'origine a été relue après le trajet :
SHA-256 `223dc3c48d5f833c797fa7af3994bb81723d68172c3f229f2831f6c5c6c745d6`,
inchangée. Les contrôleurs et le jeu de ce lot sont tous terminés.

**Reprise du trajet riche 011 :** exécution terminée, PID 18728 clos, aucun rejeu
nécessaire pour son analyse. Reçu central
`procedural-production-20261105-011/dense-route-001/assessment.json`, SHA-256
`8c28140be73fdebfde873f0ff70e4e6732c647b749eb2e3b26e2e4e27c197e1f`.
291,01 m parcourus avec collisions ; performance encore ouverte (moyenne prête
instrumentée 40,59 ms, p95 50,60 ms). Le processus natif a terminé avec code 0 ;
l'erreur du pilote après exécution a été corrigée par une analyse seule des
sorties existantes. Une correction des admissions de simulation est en cours
dans les sources communes, sans changement du candidat 011 figé. L'agent
`readiness_perf` ne modifie que les reçus GPU/contexte, le préchauffage forward
et la présentation des espèces ; aucun build ni raccord consommateur délégué.
Le plan de mise à jour 011 attend toujours sa confirmation distincte ;
l'autorisation d'installation du 008 a déjà été exécutée.

**Décision obligatoire du 2 novembre 2026 :** développer l'éditeur Forge et le
launcher de chaque jeu qu'il permet de créer/exporter. Le Hub séparé sort de la
cible produit ; ses fonctions utiles rejoignent les parcours auteur/joueur sur
les services communs. Aube et Tidewater sont des contenus de référence, pas une
identité imposée aux jeux indépendants. La décision et ses limites sont dans le
[contexte canonique](../PROJECT_CONTEXT.md) ;
le [plan de transition](planning/GATE7_IMPLEMENTATION_PLAN.md#transition-editeur-launcher)
est intégré à Gate 7 déjà ouverte. Ses raccords et observations sont décrits dans
le suivi Gate 7 ; les parcours restants ne sont pas qualifiés par ce texte.

Stockage des assets actualisé le 27 octobre 2026 : suivre
[le contrat de publication et clôture](ASSET_STORAGE_POLICY.md) et
[les commandes des lecteurs](../scripts/assets/README.md). Le travail de
conversion reste sur E:, la bibliothèque finale sur F:. Les modèles compactés
exigent le lecteur commun ; exporter un vrai GLB pour un outil externe.

Validation du 18 octobre : [rapport de séparation moteur/carte](evidence/architecture/STRICT_MAP_SEPARATION_20261018.md) ;
14 contrôles réussis et 2 232 tests du workspace réussis. Les preuves de runtime
générique et des extensions optiques gardent des périmètres distincts.
Le dernier parcours natif instrumenté de la carte conserve une réserve de cadence
(44,81 FPS moyens pour un seuil de 60) ; ses contrôles fonctionnels passent.

Revue initiale : 18 octobre 2026 ; actualisation du découpage produit et de
l'état Gate 7 : 5 novembre 2026. Ce guide est un point d'entrée opératoire,
pas un second contexte narratif. Aucun compte Codex, plugin mémoire ou historique
de conversation n'est requis pour lire le projet et développer ses sources.


## Reprise historique — 5 novembre 2026

**Export Studio 011 vérifié le 5 novembre.** Les cinq stages de
construction sont terminés sur les mêmes sources stables. Le WASM est identique
au 010 ; le Studio natif 011 embarque le nouveau manifeste et son kit adjacent.
Le geste réel dans le Studio produit `game-31840-1793869726891704700` : quatre
fichiers / 431 679 285 octets, kit `0.1.0-preview.20261105.011`, module 1.0.1
identique octet pour octet au précédent. Tous les payloads, empreintes et les
deux inspections natives passent. Reçu central 011 `studio-ui-export.json`,
SHA-256 `53f11d24f8cd6b9e309503037829ce3f33c84bd3b834ce21e947af136845248c`.
Workspace de révision 12 inchangé (`a1f2e466…`). Le contrôle de viewport 010
est référencé comme observation antérieure réutilisée, pas comme nouveau rejeu.

Le plan UI 011 prépare la mise à jour de l’installation 008 existante,
`.build/qualification-data/gate7/camp-des-falaises-20261105-008/install`.
Installateur PID 10556 sur le plan « Mettre à jour » ; clic non effectué,
confirmation Computer Use requise. Installation toujours en révision 1 et
sauvegarde joueur `223dc3c4…` inchangée. Reçus `installer-plan-ui.json` et
`update-preflight.json`. Le plan 010 est fermé sans application et obsolète.
Studio 011 PID 31840 fermé proprement ; aucun build ni jeu actif. Contrôleurs
92591 et 95022 terminés ; ne pas les relancer.

Les rapports des deux sessions joueur 008 ont été conservés et vérifiés sur F: ;
73 215 octets de métadonnées temporaires terminées ont été retirés sur E:.
Le retrait de trois copies remplacées du `bin` a été refusé automatiquement
(« blocked by policy ») : **482 363 070 octets conservés, zéro retiré** ; reçu
010 `candidate-replacement.json`. Ne pas contourner ce refus.
Après vérification de l’export 011, les deux replis temporaires 010 créés pour
l’intégration ont été retirés : **341 807 711 octets**, aucun résidu sur ces deux
cibles. Reçus 011 `replaced-010-retirement-preflight.json` et
`replaced-010-retirement-result.json`. Les anciens replis 005/008 dont le retrait
avait été refusé restent intacts ; cette opération ne les concerne pas.

À cette étape du lot 011, seul R7-03 disposait d'un verdict local Windows
sur l'ensemble du critère. Linux était différé.

**Runtime procédural de production 011 construit et démarré.** Le jeu utilise
par défaut l’hôte de streaming moteur pour ses terrains procéduraux ; le mode
complet reste une référence explicitement réservée aux builds de qualification.
Le lancement normal optimisé, sans feature ni variable de qualification, passe
sur le module existant Trois Cairns : 25 cellules résidentes sur 64, physique
prête, source synchronisée, sept tuiles distantes agrégées, aucune erreur de
projection. Capture inspectée ; scène peu chargée, aucun trajet riche ni gain de
performance déduit. Trois contrôles Rust, 16 contrôles du runner et Clippy strict
passent. Reçu central `procedural-production-20261105-011/production-assessment.json`,
SHA-256 `ecc37420b40426e1d0576db16cf1cd02beccae2d701ddf8da902742386ff3105`. Runtime `584f7c60…`, manifeste source
`442b1ac847312c699ef2f76e04369e579ff8c334ddaf947cd33715e249434a32`.
Build 94922, tests 2611 et Clippy 1173 terminés ; aucun jeu actif.
Le frontend et le Studio natif 011 avec le kit sont terminés ;
`frontend-byte-comparison.json` conserve l’identité du WASM.
Le runtime, le launcher protégé et la CLI 011 sont construits sur les mêmes sources
stables. Kit privé `0.1.0-preview.20261105.011` assemblé : 471 fichiers /
141 085 279 octets sous `.build/tmp/gate7-windows-game-kit-engine-20261105-011`.
Reçu d’assemblage SHA-256 `5a7114e3b87a1d9efc2ab50b992e322b5773ea59eada75688d763bbd7ef136e2`.
Le kit a été intégré et exporté depuis le Studio ; le plan 011 attend sa
confirmation. Le plan 010 est fermé sans application. Les trois fichiers du premier lancement mal paramétré
ont été retirés (2 359 octets) ; cause conservée dans le bilan. Aucun critère
global clos. Les contrôleurs 16779, 25594 et 88317 sont terminés.

**Protection de réinstallation ajoutée aux sources, après le build 010.** Le
service commun refuse une nouvelle installation dans une racine où subsiste un
ancien relais de désinstallation ; contrôle répété sous verrou avant staging.
Les mises à jour restent possibles et aucun relais/donnée utilisateur n’est
supprimé par ce refus. Sept contrôles CPU ciblés et Clippy strict passent.
Reçu central `installation-root-reuse-20261105-001/assessment.json`, SHA-256
`6d44735f144d65ca0ac9e145c5c7c96fcba0a1c5be5ef34ff5fed6fd88cc8cd7`. Ce correctif n’est pas dans le kit 010 ; son intégration et le
parcours Windows réel restent à faire. Les anciens installateurs ne reçoivent
pas rétroactivement cette protection ; R7-09 reste ouvert.

Le scénario de gameplay natif est aussi disponible dans le runtime de production
(`native_gameplay.rs`, installation inconditionnelle). Un trajet source/archives
épinglés peut donc être observé sans nouveau build de qualification ; choisir
un trajet valide dans la scène riche, sans confondre ce parcours avec le
démarrage stationnaire du petit module Trois Cairns.

**Historique : les PID et sessions mentionnés ci-dessous sont terminés et leurs plans remplacés.**

**Historique du diagnostic 009 et du correctif :** le contrôle réel montre un inventaire
primaire prêt (63 pipelines, aucun en attente/échec), les ombres des modèles
prêtes, mais aucun inventaire GPU exploitable pour les modèles remplacés.
Le relevé ne permet pas de distinguer une demande absente d’une demande périmée.
Le moteur retente maintenant les demandes différées et renouvelle les sources
devenues périmées via le service d’inventaire existant et une nouvelle génération.
Les entités, positions, documents et barrières GPU sont conservés. Le contrôle
CPU ciblé passe après redimensionnement d’un buffer et adoption d’une texture ;
un appel inchangé reste idempotent, sans faux reçu GPU. Clippy natif (avec tests)
et Web stricts passent. Journaux centraux `species-inventory-renewal-*-20261105-001.log`.
Le lot `studio-vegetation-correction-20261105-010` construit le frontend corrigé,
contrôleur Codex **68975 actif**, sans nouveau kit de distribution. Trois fichiers
compilés changent depuis 008 : runtime de l’hôte (journal du délai), préparation
des espèces et présentation des modèles (renouvellement + régression CPU).
Manifeste `72d0d1741b987b2bb2fd7763a6f63c02c1cfa73e877d3ea28ac9a3a7f0130ebb`.
Reçu de travail `correction-assessment.json` ; le rejeu Studio corrigé reste à faire.
Le frontend 009 et son intégration Studio ont réussi ; sessions 31374/4840
terminées, Studio 009 PID 24596 fermé proprement après le relevé, capture 1886
terminée. Son binaire de diagnostic, encore dans le `bin` du workspace isolé,
porte SHA-256 `de1ea1db333622460963d6e43068cc3c56071e589fd1e38e98faf8e11359031d`.
L’installateur 008 PID 33344 reste en attente de sa confirmation. Ne pas appliquer
son plan sans réponse. Aucun jeu ni autre build lancé ; Gate 7 reste ouverte.

**Historique du diagnostic Studio (terminé) :** le délai de végétation 008 a été reproduit
sur BrowserWebGpu. La console confirme 2 GLB, 20 maillages normalisés et 10
matériaux de vent préparés, sans erreur de shader enregistrée avant le refus.
Journal central 008 `studio-console-001.jsonl` ; capture terminée, Studio de
diagnostic PID 27308 fermé proprement, sauvegarde de révision 12 inchangée.
Le lot central `studio-vegetation-diagnostic-20261105-009` construit un frontend
de diagnostic depuis le checkout commun. Seul changement compilé depuis 008 :
`source_vegetation/scene/streamed/host/runtime.rs`, journal des barrières à
l’expiration du délai, sans modification du rendu/admission. Clippy natif passe.
Manifeste `65b5df5949d3572fae5e8eae192c485476b1d8d4d2bda49699c5df5e2b4d2a4e`.
Contrôleur de construction frontend suivi par Codex : session 31374, actif lors
de cette note. Ne pas le relancer sur un simple délai d’observation. Aucun nouveau
kit de distribution n’est demandé pour cette journalisation. L’installateur 008
PID 33344 reste sur son plan, sans clic Installer ni nouvelle autorisation reçue.

**État courant après reprise de l’export 008 :** export réel réussi depuis le
Studio, `exports/game-17516-1793856648727926000`, quatre fichiers / 431 638 325
octets. Kit `0.1.0-preview.20261105.008`, module 1.0.1 strictement identique au
précédent ; sauvegarde de révision 12 inchangée. Tous les payloads, empreintes et
les deux inspections natives passent. Reçu central du lot 008
`studio-ui-export.json`, SHA-256
`1059d48bc598a448261b00e4abfe173afc54ed7190a2882f12c088d8b1227e0e`.
Le viewport Studio a en revanche refusé sa disponibilité après 120 secondes :
`Imported vegetation did not become ready within 120 seconds`. Le succès natif
008 ne qualifie donc pas le viewport WebGPU. La carte a été fermée sans mutation.
L’installateur 005 a été fermé sans installation (PID terminé). Son ancien plan
et sa question sont obsolètes. L’installateur 008 (PID 33344) présente un plan
vérifié pour `camp-des-falaises-20261105-008/install`, données utilisateur dans
le voisin `user-data`, sous le dossier de qualification Gate 7 sur E:.
La cible est absente ; aucune installation appliquée. Une nouvelle confirmation
Computer Use pour le clic Installer 008 est en attente. Les copies de repli
`.previous-studio-005.exe` et `.previous-kit-005` restent utiles tant que le
chargement Studio 008 n’est pas corrigé. Aucun nouveau build ni test natif répété.
Les paragraphes suivants conservent la chronologie des candidats précédents.

État précédant le diagnostic : le runtime optimisé 008 et les trois reprises de profils passent.
Le frontend, le kit et le Studio sont construits ; le chargement Studio 008
(ancien PID 17516, terminé) a ensuite dépassé son délai. Les contrôleurs
19446/6768/71685 sont terminés, aucun jeu actif. Manifeste source
`3ac42fc2d6638d9c5cdb34ceb3ccb2d387aabf2e9c9fb9801c264d1ac2b2cad4`.
Voir le détail 006–008 ci-dessous ; les cinq constructions 008 sont terminées.
Les cinq builds du lot `windows-editor-engine-20261105-005`
sont terminés et stables. Le Studio mis à jour (PID 3872) a réussi son export
réel dans `exports/game-3872-1793848666618164100`, sous le workspace isolé décrit
ci-dessous. Module 1.0.1 identique octet pour octet au précédent, kit moteur
`0.1.0-preview.20261105.005`, quatre fichiers / 431 207 221 octets. Tous les
payloads et les deux inspections natives passent. Reçu 005 `studio-ui-export.json`,
SHA-256 `d187b5ea5948198781191fccb03a72090a1ab3607f169135e70d56831c38f85c`.
L’installateur 005 (PID 19372) est ouvert sur un plan vérifié pour
`E:/OpenRPG_Forge/.build/qualification-data/gate7/camp-des-falaises-20261105-005/install` ;
`FORGE_DELIVERY_USER_ROOT` pointe sur son voisin `user-data`. La cible est absente.
Une nouvelle confirmation Computer Use pour cliquer Installer est en attente ;
aucune installation appliquée. Les anciennes réponses aux plans 001/003 ne
l’autorisent pas. Les copies `.previous-studio-001.exe` / `.previous-kit-001`
ont été retirées après succès et vérification de l’export : 341 304 927 octets,
reçu `candidate-replacement.json`. Les profils et caches précédemment refusés
restent intacts. Aucun jeu actif ; le frontend 008 ci-dessus reste suivi.
Gate 7 reste ouverte, Linux différé.


Le contrôle indépendant 005 des profils natifs est terminé sans recompilation :
Low, Medium et High démarrent sur l’export courant, atteignent l’état prêt et
écrivent les préférences attendues. Ultra réutilise le rejeu déjà terminé.
Même projection canonique attendue, caméra et fenêtre observée 1280×800/Fifo ;
300 intervalles instrumentés par profil (290 prêts). Moyennes Low 25,287 ms,
Medium 27,022 ms, High 30,091 ms, Ultra 31,662 ms. Ce sont des intervalles du
thread principal, pas des timestamps GPU. Les captures Low/Medium/High sont
inspectées ; luminosité/motifs de l’herbe et ombrage des arbres remplacés restent
non acceptés, sans cause attribuée. Pas de rejeu de réouverture des préférences
ni de parcours installé. Reçu 005 `native-graphics-profiles-001/assessment.json`,
SHA-256 `6dbf273ae0a6c0e39f05102951ff43593e2c64d3b8dd3377f26a97f7f1cfc56a`.
R7-05 reste ouvert ; ces contrôles ne sont pas à relancer sur les mêmes entrées.

Session 74469 terminée, trois processus de rendu terminés. Aucun nouveau build.
Le plan d’installation 005 attend toujours sa confirmation ; ne pas réutiliser
les observations/index UI sans état frais.

Correctif moteur 006 en cours : le chargeur de modèles et ses textures sont
conservés. Le diagnostic relève un raccord manquant, pas une cause visuelle
encore prouvée : les espèces remplacées appelaient le PBR standard tandis que
terrain/objets de la scène source utilisent `source_lighting::surfaces`.
Le nouveau `VegetationMaterial` compose le matériau de vent existant avec le
même Receiver ; shader fragment assemblé sur la même bibliothèque, vertex,
alpha et prepasses conservés. Les inventaires/préchauffages suivent le vrai
matériau composé ; les gardes reconnaissent seulement sa projection possédée.
Aucun asset ou format persistant changé. Deux tests de projections passent,
ainsi que le test GPU existant ombres/alpha/vent (binaire de test réutilisé).
Le premier build des tests refusait un champ Receiver manquant dans une fixture ;
la fixture est corrigée, rejeu 002 vert. Clippy natif et Studio Web stricts passent (contrôleur 6368 terminé).
Le premier Clippy refusait seulement la taille de `convert` ; son bloc végétal
a été extrait et le rejeu 002 passe. Le runtime optimisé 006 est construit sur sources stables (session 23088 terminée),
manifeste `aaae955ae2f0811e7ed7ee22ae5f791f9687d38c00951c5aad0a554318b306fe`,
binaire `d4f3a98a25a271ee503f7ce707b4c2d81743899476b9f97102d3eeaecf566bff`.
Son lancement normal est refusé par `graphics::runtime::coverage`, qui omettait
le matériau composé ; session 33038 terminée, sortie 1. Le refus est conservé
sous 006 `native-module-profile-001` et `correction-assessment.json`.
Le raccord d’admission reconnaît maintenant cette seule famille supplémentaire
et exige son propriétaire source réel, en conservant le refus des matériaux
inconnus ou ambigus. Clippy natif et le test ciblé de refus de propriétaire passent.
Le premier test visait le bin sans ses tests et a exécuté zéro cas ; il n’est pas
compté comme validation. Le rejeu `--lib` 002 exécute bien un cas vert.
Le contrôleur 12385 construit maintenant le successeur 007 ; les reçus/sources
006 sont conservés. Le successeur 007 termine son build sur sources stables : manifeste
`b22cb9e1cc33f31629f93873ec9ed022dfe6e79a7c51840977282c602c6ad6e5`,
runtime `9171c56e73460d1ab30a141e345bdb134170c86e80deebc38c35d9b4dd452221`.
Session 12385 terminée. Son lancement (session 42005 terminée, sortie 1)
passe l’admission puis refuse le shader composé : `Ambiguous import path for
item`. Le shader de vent importe déjà `pbr_types` et `view`, réimportés par le
corps de lumière ajouté. Le parseur naga_oil 0.22 accumule même les chemins
identiques et refuse alors leur usage. L’assemblage retire seulement cette
seconde déclaration pour le shader végétal ; les fonctions communes ne changent
pas. Reçu 007 `correction-assessment.json`. Vérifier cette correction avec le
runtime debug commun avant une nouvelle compilation release : build debug
session 39350, log central `species-source-lighting-debug-build-20261105-001`.
Ce build debug termine, puis le lancement normal du même module passe :
session 18782 terminée, sortie 0, capture à 570 et projection prête. Projection
canonique attendue identique à 005, espèces/collisions prêtes, 109 pipelines,
aucun en attente/échec. Capture inspectée : les modèles reçoivent maintenant
la lumière source ; qualité globale et performance release restent à qualifier.
Binaire debug `04f301873b30750685c9651cfe0948ba50f0c13c4ec637167d72bafd753b5330`.
Reçu 008 `correction-assessment.json`, détails sous `native-module-debug-001`.
Le contrôleur 21409 enchaîne Clippy natif/Web sur la correction d’imports puis
la construction release 008. Ne pas reconstruire de nouveau ce debug ; ne pas
le faire passer pour une preuve de kit release. Après réussite du build 008,
reprendre le lancement normal de 005 avec le binaire 008, même cache/module,
600 frames/capture 570, sortie `008/native-module-profile-001`. Comparer la
projection et le rendu avant reconstruction du frontend et du kit. Le candidat/plan d’installation 005 reste daté et valable
pour son périmètre ; ne pas le présenter comme contenant ce correctif.

Le build optimisé 008 termine sur sources stables, puis le lancement normal
passe avec capture à 570 : projection canonique attendue identique à 005,
espèces/collisions prêtes, 109 pipelines et aucun en attente/échec. Runtime :
`3d2948d2752d0bb14dd056b5712c8061452c6b960541845ab36ef82ee4dc44fb`.
Sur les mêmes 300 intervalles, 31,662 → 32,252 ms (31,58 → 31,01 intervalles/s) ;
aucun gain de performance revendiqué. Reçu `shared-lighting-runtime-assessment.json`,
SHA-256 `73ba23a34bb0638cd265fbe8378b856c28e89ed3ae3e9613ecfc63536151fb26`.
Low/Medium/High passent ensuite avec relecture des préférences réelles de 005,
prioritaires sur le défaut Ultra contradictoire, et fichiers inchangés. Reçu
`native-graphics-profiles-001/assessment.json`, SHA-256
`58d749b31130565f54ce22eb00fe7659904f57a0ed81d4b988fec807d0e893c4`.
Leurs moyennes sont 26,523 / 28,613 / 31,294 ms ; captures inspectées, motifs et
luminosité d’herbe encore non acceptés. Pas de validation humaine ni de parcours
installé. Le frontend Studio 008 compile hors ligne, session 19446, mêmes outils
épinglés Trunk 0.21.14 et wasm-opt 131 que 005. Après frontend : launcher/CLI,
kit version `0.1.0-preview.20261105.008`, Studio avec overlay, puis export UI.
Réutiliser les notices privées 014, sans nouvelle collecte ; tous les manifests
Cargo et locks sont identiques à 005. Ne pas reconstruire le runtime 008.

Intégration 008 construite et installée dans le bin de qualification : cinq
constructions stables sur le même manifeste 3ac42fc2… ci-dessus. Frontend 19446,
launcher/CLI 6768 et Studio 71685 terminés. Studio SHA-256
`cc366f01d2d97f25240957e67c4b6fc89e085e0694bdadf93a60c01a8ec64aea`,
reçu build `f987b79cd35c1bd637841d93b4deb48abc9d430a8a155451370f9c64caf96ca5`.
Kit `E:/OpenRPG_Forge/.build/tmp/gate7-windows-game-kit-engine-20261105-008`,
version `0.1.0-preview.20261105.008`, reçu d’assemblage
`83c580b914264fcc55e83ded6b9eb3422a3b6d50779a6fbf4286c120ea9ea062`, manifeste
`80a5ad66aa704dda805183de5cd8b2df7563e083ac9b7cf877f4794a1f9d326a`.
Kit adjacent : 471 fichiers / 141 046 879 octets, reçu `studio-adjacent-kit.json`.
Le Studio 005 est fermé proprement sans retouche en attente. Le Studio 008 est
ouvert (PID 17516), même profil `webview-20261105-001`, même auteur révision 12.
La seconde carte dense a été ouverte via la liste ; préparation de ressources
encore en cours au dernier état UI observé. Attendre et vérifier le viewport,
puis revenir aux modules sans modifier/sauvegarder la carte avant l’export UI.
Les anciennes copies bin/kit 005 sont temporairement dans
`bin/.previous-studio-005.exe` et `bin/.previous-kit-005` pour retour arrière,
à retirer après succès et contrôle de l’export 008. Le plan installateur 005
(PID 19372) attend toujours une réponse ; il n’est pas appliqué. Le futur plan
008 n’est pas encore prêt ; ne pas appliquer une réponse à un ancien plan sur
un nouveau candidat. Comparaison des entrées 005→008 : exactement neuf fichiers,
ceux du raccord moteur, de l’admission runtime et de leurs tests ; aucun stockage
ou manifest/lock Cargo changé. Pas de build ni jeu actif.

Historique des étapes et des refus conservés :

Le candidat `windows-editor-engine-20261105-001` est construit et intégré ;
aucun build n'est actif. Source :
`d33f587c0b20343594a714138f24f792521634eb604783b483bf6d5d48de0d7d`.
Studio : `a9527576c20fa6182386ba2ee051b7ce3137c847b04ff5254d532e2e41ed484f`.
Le cycle réel du modèle remplacé passe, reçu `studio-species-retouch-cycle.json`.
La session auteur est à révision 12, SHA-256
`a1f2e4667ec0816ad52090249aa423f4a3ead4f68421a9d0d64c08de3f51c3b4`.
La zone dense a pour archive
`4b3c3feb345f511a1800dba517902a2eb4aeb27fbba944c57a15e5e5a8bc7311`
et head `e899e80026ef9655d4e7ec0d04b3ba0f996f40ed19578f4f04552fb8882be61a`.
La retouche supplémentaire est `54e21842-0c00-5877-aa9f-c42d9f00f216`, palms,
rotation 10°, position/échelle conservées ; les autres corrections sont inchangées.
L'export réel actualisé réussit en module 1.0.1 et kit moteur
`0.1.0-preview.20261105.001`. Son dossier sous le workspace de qualification est
`exports/game-28500-1793838186193104800` : quatre fichiers, 431 196 469 octets.
Le reçu `studio-ui-export.json`, SHA-256
`7562215420243dc5412fdc3bbaaf52ca55d6969daac2cb97b6be48ef759676ae`, vérifie
les empreintes, les archives exportées identiques, les réglages et le namespace
de sauvegarde, ainsi que les lectures natives des deux paquets.
Le hash canonique du snapshot workspace exporté est
`3fb3344f463a9adcca62ad937eea183c86b9162ba577523e662c4ee41ad4da64` ;
il est distinct du hash du fichier de session ci-dessus.
Le Studio (PID 28500) a été fermé proprement après confirmation de l'absence
de changement à sauvegarder. Le nouvel `Installer.exe` (PID 25508) avait son plan vérifié pour
`E:/OpenRPG_Forge/.build/qualification-data/gate7/camp-des-falaises-20261105-001/install`.
Son `FORGE_DELIVERY_USER_ROOT` est le dossier voisin `user-data`.
La cible n'existe pas ; aucune installation n'est appliquée. L'installateur
001 est maintenant fermé : le lancement indépendant du module exporté échoue
avant rendu, `Manual placement must name a governed mesh with its exact pin`.
La confirmation Computer Use demandée pour ce plan est donc obsolète : une
réponse tardive ne doit pas installer ce candidat désormais refusé au runtime.
Le plan 003 est remplacé : son installateur a été fermé sans installation.
Son ancienne confirmation ne doit pas déclencher l'application du nouveau plan.
Le correctif dans `apps/forge_runtime/src/module_game/manual.rs` exclut les
retouches végétales reconnues par leur identité et pin de bloc exact de la
résolution des maillages manuels. L'hôte végétal commun les applique déjà.
Le lot `windows-editor-engine-20261105-002` a construit seulement le runtime
affecté après Clippy strict. Source :
`6dd082f84229d5d6391d623279f29df1ed04cea282fcc577fe1a1038b1b91e31` ; runtime :
`147d4c6634015073d7b87ad2d0664aa160eb15f603fa404b4505cbb10045f196`.
La tentative `native-module-export-001` atteint une projection prête mais sa
capture à frame 5970 dépasse le watchdog de 120 s : échec conservé. Le même
binaire, même module et même cache réussissent ensuite `native-module-export-002`
sur 900 frames, capture à 870, sortie 0. 65 383 instances et 13 434 collisions
canoniques ; espèces, gameplay et inventaires GPU prêts, aucune erreur de
projection. Reçu `normal-module-runtime-assessment.json`, SHA-256
`51b25d94028c9d835c1c66f78591772d233cc5d285ec8cbae4d832840684f17a`.
La caméra fixe en Ultra reste à 48,39 ms moyens (20,67 intervalles/s), p95
51,91 ms, sur 591 samples prêts entre frames 300 et 900. Ce ne sont ni des
timestamps GPU ni une comparaison avec Tidewater. Un profilage borné du même
binaire utilise `FORGE_RUNTIME_CPU_PROFILE=1`, sous `native-module-profile-001`.
Le profilage finit avec succès : coût CPU cumulé de `render.Specialize` autour
de 15,95 ms/appel, source-shadow GPU asynchrone autour de 14,29 ms à sa dernière
observation. Reçu `runtime-cost-diagnosis.json` ; les durées imbriquées ne
s'additionnent pas et les valeurs GPU gardent leur âge.
Le correctif en cours réutilise les globals temporels Bevy pour les matériaux
de prototypes chargés, conserve leur instant pour les paquets d'ombres, et
n'émet plus de modification d'asset pour le seul passage du temps. Il touche
`vegetation.rs`, `vegetation_prototypes/wind.rs`, `vegetation_prototypes/imported.rs`,
le shader partagé et `source_shadows/casters/governed.rs`. Les deux tests ciblés
`governed_material_clock` passent, ainsi que Clippy natif strict. Le runtime
optimisé du lot `windows-editor-engine-20261105-003` est construit sur sources
stables, mais refusé au rendu : la passe `pbr_prepass_pipeline` n'a pas le
binding Globals 0:11 utilisé par le shader. Reçu `shared-clock-gpu-refusal.json`.
Ne pas installer ce candidat. Le correctif conserve le transport temporel
existant : le slot zéro inactif du `ShaderBuffer` LOD à binding matériau 101
porte le snapshot Time courant/précédent. Son propriétaire existant
`temporal_lod/render.rs` l'actualise sans modifier l'asset matériau ni ajouter
de buffer ou de format persistant. Le test ciblé de bouclage passe, ainsi que
les deux tests GPU existants réutilisés sans recompilation (picking des modèles
remplacés et cascades d'ombres, alpha et vent). Clippy natif et Studio Web stricts
passent également. Le runtime optimisé du lot 004 est construit et son lancement
de scène complète passe avec capture à 570, projection prête et sortie 0.
Source : `866283acda95e49d41bd9d1b75fe6c3686e2a5831b15f295fa523eac09b8f04e` ;
runtime : `d88abb1da3ca20c6841306e7951e925c83bde1495856b074cf13cc0bdd82cea8`.
Le reçu 004 `shared-clock-runtime-comparison.json`, SHA-256
`f2917ef9f8c18bc3688ec2d42f80eba4b5a4b16d3b0310b78b36a52a977cccf1`,
compare les frames communes 300–599 (300 samples, 290 prêts de chaque côté),
avec état canonique attendu, caméra, résolution 1280×800 et Fifo identiques.
La moyenne passe de 49,414 à 38,641 ms (20,237 à 25,880 intervalles/s, +27,88 %),
p95 de 51,984 à 42,110 ms. Le CPU `render.Specialize` passe de 15,95 à 0,88 ms ;
`render.Queue` de 8,45 à 0,40 ms. Les observations cumulées/imbriquées ne
s'additionnent pas. La projection reste à 65 383 instances, 13 434 colliders
canoniques, 110 pipelines prêts et aucune erreur de rendu.
La performance reste insuffisante : les nouvelles observations situent un coût
dans l'intervalle CPU `render.PrepareViews` (~20,5 ms) et dans les ombres source
GPU asynchrones (~10,84 ms au dernier échantillon, avec âge). Diagnostiquer leur
cause avant une autre modification, sans rejouer les contrôles déjà terminés.
Le code Bevy de `prepare_windows` situe l'attente de surface dans cet intervalle :
ce n'est pas la preuve d'une fonction CPU lente. La référence et le runtime
demandent tous deux la synchronisation verticale par défaut. Le test GPU existant
confirme une Radeon RX 7800 XT discrète/Vulkan. Les anciens casters végétaux
remplacés sont retirés de leur sélection avant quota ; pas de doublon trouvé.
Le lot 005 prépare le tri des modèles par volume de cascade avec le frustum OBB
Bevy et les bornes de déformation existantes. Un buffer d'instances garde trois
plages et une sélection mise en cache ; ni modèles ni distances ne sont réduits.
Les deux tests CPU de sélection passent, session 88887 terminée. Le test GPU
existant d'ombres passe aussi avec une empreinte de profondeur inchangée après
activation du tri. Clippy natif et Studio Web stricts passent. La construction
optimisée du lot 005 termine sur sources stables ; session 59682 terminée.
Source : `5cce335a751eefc7e11197827924b8768a33d1a2cf77a31848619ba43b44127e` ;
runtime : `9b47825ad5d65a8f38b25bc85dbc35caa23285782009926fac77ad144edd2371`.
Le contrôle de scène complète `native-module-profile-001` passe avec capture à
570, sortie 0 et projection prête (session 97814 terminée). Le reçu
`cascade-runtime-comparison.json`, SHA-256
`99c0779db7af91e13291e02c23bb1121c00dc5df8bc3ab989a3d4ef8539faf19`,
vérifie état canonique attendu, caméra, arguments, 1280×800/Fifo/focus identiques.
Sur 300 frames communes 300–599 (290 prêtes de chaque côté), 38,641 → 31,662 ms
en moyenne (25,880 → 31,584 intervalles/s), p95 42,110 → 33,762 ms : +22,04 %
depuis 004 et +56,07 % depuis 002. Instances/colliders canoniques inchangés,
110 pipelines prêts, aucune erreur de rendu. Les valeurs GPU restent datées
et asynchrones ; aucune parité Tidewater ou acceptation visuelle n'est déduite.
Le frontend Studio 005 est construit sur sources stables, session 72938 terminée.
Le launcher autonome et la CLI sont également construits, contrôleur 10792 terminé.
Tous les reçus portent le même manifeste source 5cce335a… ci-dessus.
Le kit `E:/OpenRPG_Forge/.build/tmp/gate7-windows-game-kit-engine-20261105-005`
est assemblé, version `0.1.0-preview.20261105.005`. Reçu d’assemblage SHA-256
`2301800d76fc65748aa75b2097486807091ac2e41bbcca18cb28f68afda2036e`, manifeste
`a25ca83bddbf9a191f7f52f2e430bb69dce0411119fbdf7d5d511e593cc0c594`.
Le Studio natif est construit sur sources stables, session 88029 terminée.
Studio SHA-256 `05c848516c4263b13d7c361a59efa0a26cd5ca15d82f2f7df565a1baa5469897` ;
reçu de build `3fa4976b607e7a10a882cdbd420babb4ebfa93c7345e3a2b56ecd2a25ce80d5f`.
Le bin et son kit adjacent ont été remplacés après contrôle intégral de leurs
empreintes ; reçu 005 `studio-adjacent-kit.json`, 471 fichiers / 140 615 775 octets.
La session auteur révision 12 est inchangée. Le Studio est ouvert (PID 3872),
profil réutilisé `webview-20261105-001`. L’export UI réussit ; son plan 005
attend une nouvelle confirmation au clic Installer (voir état courant ci-dessus).
Les copies de retour arrière du remplacement ont été retirées après validation.
Ne pas reconstruire le runtime ni le frontend sans nouveau changement.
Réutiliser les notices privées existantes, sans relancer leur recherche :
bundle `E:/OpenRPG_Forge/.build/tmp/gate7-notice-bundle-014`, reçu candidat
`notices-winsafe-20261103-001/delivery-candidate.json` (SHA-256
`49581cdaa745ad4a943be41f2bbb0ee47bff6f556334583293e457cdead79d19`),
graphe voisin `graph.json`, attendu
`153c0e49c71cde685e7ff5d2abcf8969b244fcd7c955a0e590e61c115a3d403e`.
Vérifier le fichier de graphe et laisser l'assembleur revalider le graphe Cargo
courant. Le cache de registre est `E:/OpenRPG_Forge/.build/cargo-home/registry`.
Ces notices sont des candidats privés ; leurs limites légales restent inchangées.
Le runtime 004 validé est temporairement conservé dans
`camp-des-falaises-20261105-001/runtime-baseline-004/forge-module.exe`,
117 621 248 octets, empreinte d88abb… ci-dessus : conservation temporaire pour
la comparaison et le remplacement du cache partagé (reçu 005
`baseline-retention.json`). Cette copie est maintenant retirée : comparaison
004/005 terminée, kit 005 et export Studio 005 vérifiés avec le runtime 9b4782…
encore disponible. Reçu 008 `baseline-retirement.json`, retrait après hash,
contrôle de contenu unique, chemin physique et absence de processus actif.
Le dossier `runtime-baseline-004` n’existe plus ; ne pas le recréer.
Sessions 44998, 56544, 59682 et 97814 terminées ; aucun jeu de qualification n'est actif.
Le Studio/kit et l'export conservent le runtime antérieur : ne pas les présenter
comme mis à jour par ce lancement direct. Reprendre sur le même module, cache,
caméra et instrumentation que `native-module-profile-001` du lot 002, avec
600 frames (capture à 570), pas 6000 à cause du watchdog de 120 s.
Traiter le coût identifié avant de réassembler un kit et de demander
l'installation d'un nouveau candidat effectivement prêt. L'ancien game.forgepack
garde le runtime refusé et ne doit pas être présenté comme corrigé par ce run.
La tentative refusée et ses journaux sont dans le sous-dossier 001
`native-module-export-001`. Ne pas la transformer en qualification du jeu.
Ne pas retenter les retraits refusés de `module-cache`, `webview`, `webview-004`
par un autre outil. Les deux profils refusés gardent 972 286 819 octets ; les
profils suivants restent provisoirement associés aux parcours en cours.
Voir le reçu 004 `studio-webview-retirement.json`. Gate 7 reste ouverte, Linux différé.

## Lire et choisir le périmètre

Reprise opératoire du 4 novembre, lot Windows 003 : le workspace isolé
`E:/OpenRPG_Forge/.build/qualification-data/gate7/studio-imported-species-20261104-001`
est sauvegardé ; le candidat Studio corrigé est ouvert. La source courante est
`workspace-v1/authoring.json`, révision 10 : ne pas remplacer les données
gérées par les anciens fichiers de zone du bootstrap. Le jeu configuré est
« Le Camp des Falaises » ; le palmier retouché et les 453 corrections de relief
sont dans l’archive `9dec325cc9c4231f81eb79e7f96f23a6062f4f13f3061c8f78384d509a634737`.
Les reçus réels sont sous la campagne centrale, lot
`windows-editor-engine-20261104-002` : `studio-authoring-cycle.json` et
`studio-relief-export-assessment.json`. Le premier export a échoué avant livraison.
La correction distingue les références de blocs végétaux des GLB externes ;
son test ciblé passe. Les cinq constructions sur les sources
`c794931c4e87a83a1fd55ca7b9829ecde495e4382c06cf44dc7e29f49c4e82ea`
ont terminé sous `windows-editor-engine-20261104-003`, cache commun uniquement.
Le kit adjacent est vérifié et le candidat remplacé dans le même `bin`.
L’export corrigé depuis le Studio a réussi dans
`exports/game-20172-1793826379287132000` du workspace : quatre fichiers,
430 955 200 octets, dont le module à deux cartes, le jeu et `Installer.exe`.
Le reçu `windows-editor-engine-20261104-003/studio-ui-export.json` vérifie les
empreintes, les retouches et la configuration exportées. L’installateur est
ouvert, son plan vérifié pour
`E:/OpenRPG_Forge/.build/qualification-data/gate7/camp-des-falaises-20261104-003/install` ;
le clic final Installer attend la confirmation Computer Use au moment de l’action.
Ne pas appliquer ce plan avant sa réponse. Ensuite qualifier l’installation et
le jeu autonome. Les critères Linux restent
différés, sans nouveau verdict de clôture. Le cache redondant dont le retrait
automatique a été refusé reste préservé ; ne pas relancer cette suppression.
Les sources ont ensuite reçu un correctif de trois lignes dans
`apps/forge_studio/src/authoring_brush_pointer.rs` : conserver l’outil choisi
après un point refusé, puis nettoyer le geste par le `suspend` existant.
Le contrôle Studio WebGPU passe ; ce changement est postérieur au candidat 003
et ne modifie pas ses paquets ni le plan d’installation en attente. Ne pas
annoncer son intégration ou son geste réel comme qualifiés ; le prochain lot
groupé devra les vérifier. Le cas du terrain visible hors cellules physiques
détaillées reste distinct et ouvert. Le reçu est
`studio-brush-retry-assessment-20261104-001.json` dans la campagne centrale.
Un lot groupé supplémentaire de huit fichiers, décrit par
`terrain-and-companion-corrections-20261104-001.json`, raccorde le pointage des
maillages terrain/HLOD résidents à l’intersection Bevy existante, avec contrôle
de source et de visibilité, sans collider ajouté. Deux tests CPU passent et
le contrôle strict moteur/Studio WebGPU passe après réorganisation des fonctions
végétales. La livraison laisse aussi passer les compagnons épinglés par l’entrée
dédiée `module-game`, au plan et avant le spawn ; le chemin ancien refuse
toujours ces paquets. Le test sur installation de fixture et Clippy passent.
Aucun exécutable de fixture n’est lancé. Ces sources doivent rejoindre le
prochain candidat groupé et les parcours réels affectés ; le candidat 003 et
son installation graphique en attente restent inchangés.
La reconstruction groupée sous `windows-editor-engine-20261104-004` est terminée :
les cinq étapes portent le manifeste stable
`9dd50220c6e7191c2caad387ba1c8115877383af5d62db8392de7b48f03f8018`.
Le Studio et son kit adjacent sont remplacés et vérifiés dans le même `bin`.
Le reçu `candidate-replacement.json` confirme la session sauvegardée inchangée,
le retrait des 140 605 535 octets du kit adjacent 003 et les 140 605 023 octets
du nouveau kit. Le binaire Studio est épinglé à
`172bd2c1228667d076b43d0c88ecdc0ebddd8c32af65ef80ef15ec7a5e50d256`.
Le processus Studio 004 ouvert (PID 33864 au lancement) emploie le même
`FORGE_AUTHORING_ROOT` et le profil temporaire neuf `webview-004`.
La seconde carte du module est sélectionnée et charge ses ressources ; reprendre
les gestes de brosse après refus et de sélection des espèces remplacées.
L'installateur 003 reste ouvert, sa cible isolée encore absente au contrôle ;
sa confirmation est en attente. Ne pas appliquer ce plan sans réponse.
Les exports 003 sont inchangés et indépendants du remplacement Studio.


Le parcours réel du candidat 004 confirme le maintien de la brosse après un
clic dans le ciel, mais refuse encore la plage. Le rendu source masque les
cellules techniques de la même partition ; le filtre de pointage les écartait.
Le marqueur propriétaire `PhysicsProjection` et son empreinte autorisent
maintenant leur réemploi sans nouvelle géométrie, raster ou collider.
Trois tests CPU passent, puis le contrôle strict moteur/Studio WebGPU.
Le même essai sélectionne un arbre `trees`, prépare/applique/sauvegarde une
rotation de 15° sans modifier position/échelle : archive
`faca92c12c03f92398b65937bda361348fc9ed716285a738cc3437a2dce7242e`,
head `b196c8ddbf6f9a98fb6997ccf7bbcaf574a27f48b11b4556678f9874fa09afb9`.
Ce n'est pas une preuve de modèle remplacé : le viewport des modules omettait
l'appel `PreparedHost.with_species`, déjà employé par le jeu normal.
Il est raccordé au même chargeur de prototypes et au même catalogue épinglé ;
aucune population procédurale supplémentaire n'est générée. Le contrôle strict
moteur/Studio WebGPU passe sur ces quatre fichiers modifiés. Les gestes de
remplacement et de brosse restent à vérifier après construction.
La séquence unique du candidat 005 est lancée sous
`windows-editor-engine-20261104-005`, manifeste
`483b3cca9f97028e81210ff0c41721fb989dfac57768e5a7b1bf26f7ae8b4ed8`.
Elle enchaîne frontend, jeu, launcher, livraison, kit puis Studio sur le cache
commun avec 8 jobs ; conserver ses sources stables. Le reçu groupé est
`source-terrain-and-module-species-corrections-20261104-001.json`, le constat UI
`windows-editor-engine-20261104-004/studio-ui-observations.json`.
L'installateur 003 attend toujours la confirmation déjà demandée ; le Studio
004 reste ouvert sur sa retouche sauvegardée. Aucun nouveau critère Gate 7
n'est déclaré PASS et aucun contrôle Linux n'est lancé.

Le 5 novembre, le Studio 004 est fermé proprement après confirmation des
retouches enregistrées. La session conserve exactement le SHA-256
`3d4cda396b1b60a29fdff85660c191688f91585a68c2fffcfbe5940319553e99`.
Reçu : `windows-editor-engine-20261104-004/studio-clean-close.json`. Le build
005 a terminé ses cinq constructions ; les sessions 51685 et 13457 sont terminées. Le kit adjacent 004 est à conserver pour le remplacement vérifié
par 005. L’installateur 003 reste ouvert et n’est pas appliqué.


Le retrait des profils temporaires fermés `webview` et `webview-004` du même
workspace est ensuite refusé par le contrôle d'approbation automatique
(`blocked by policy`), avant exécution. Ils conservent 844 fichiers,
972 286 819 octets. Ne pas relancer cette suppression par un autre shell ou
outil ; le reçu `windows-editor-engine-20261104-004/studio-webview-retirement.json`
porte le statut `NOT_EXECUTED_AUTOMATIC_APPROVAL_REJECTED`. La session auteur,
les exports et le cache `module-cache` précédemment protégé sont préservés.


Le frontend et le jeu natif 005 terminent en `BUILD_ONLY`, sources stables.
La séquence initiale 51685 s'arrête ensuite au contrôle des arguments du
launcher, avant construction : `--shared-jobs` n'est pas admis pour ce rôle.
L'appel est corrigé sans modification du produit ; les deux étapes réussies
sont préservées et la reprise 13457 part du launcher, puis livraison, kit et
Studio. `orchestrator-resume.json` conserve cet arrêt et sa reprise dans le
lot 005. Ne pas recommencer le frontend ou le jeu natif.


Les cinq constructions du candidat 005 sont maintenant terminées en
`BUILD_ONLY`, sur le manifeste stable
`483b3cca9f97028e81210ff0c41721fb989dfac57768e5a7b1bf26f7ae8b4ed8`.
Le kit adjacent et le Studio sont remplacés par le lecteur canonique, session
auteur inchangée à `3d4cda396b1b60a29fdff85660c191688f91585a68c2fffcfbe5940319553e99`.
Le Studio est épinglé à `e675daa5dcd4a0b2df0ad09a0cce0342de2a486752b5600893d877b99ee78e2f`.
Le remplacement retire les 140 605 023 octets du kit adjacent 004 vérifié ;
le nouveau kit occupe également 140 605 023 octets. Les refus de suppression
`module-cache`, `webview` et `webview-004` sont respectés et ces chemins restent
intacts. Reçus 005 : `studio-adjacent-kit.json` et `candidate-replacement.json`.
Le Studio 005 est ouvert (PID 19704 au lancement) sur le même workspace,
profil neuf `webview-005`. La seconde carte dense est sélectionnée et charge
ses ressources ; reprendre les gestes de brosse et de modèles remplacés.
Les sessions de build 51685 et 13457 sont terminées ; ne pas les relancer.
L'installateur exporté 003 reste ouvert, sans application de son plan ni
réponse présumée à la confirmation demandée. Aucun nouveau verdict Gate 7.



Depuis la séparation du 18 octobre, développer le moteur dans `crates/forge_engine`
et les contrats génériques d'assets dans `crates/forge_assets`. La carte, ses
ports et ses données résident dans `crates/forge_tidewater` et
`crates/forge_tidewater_assets`. Aucun alias des anciennes API de carte ne reste
dans le moteur. Les exemples de qualification se lancent avec `-p forge_tidewater`
et les vérificateurs de données avec `-p forge_tidewater_assets` ; leurs noms et
arguments restent les mêmes. Lire [ADR-0026](ADR/ADR-0026-strict-engine-map-separation.md)
et les README des composants avant de reprendre un ancien script.

`scripts/Test-GenericEngine.ps1 -PrepareBrowser` contrôle aussi le moteur seul,
sans fonctionnalités par défaut et en WASM, puis le workspace et les ports.
Les scripts historiques de `scripts/hivemind` ne constituent pas des outils
produit et ne sont pas réécrits par cette séparation.

1. Lire [PROJECT_CONTEXT.md](../PROJECT_CONTEXT.md), qui porte l'état canonique.
2. Lire [AGENTS.md](../AGENTS.md) : architecture, gouvernance et règles de travail,
   applicables aussi à un développeur humain ou à un autre assistant.
3. Consulter les [ADR](ADR), en particulier ADR-0001 à ADR-0006, puis les décisions
   propres au sous-système et [ADR-0034](ADR/ADR-0034-github-backup-local-gate-validation.md).
4. Lire l'état courant de la [roadmap](planning/MIGRATION_ROADMAP.md), puis le
   README du sous-système concerné.

La branche active est `Rust`, dans `E:\OpenRPG_Forge\OpenRPG_Forge-Rust` sur le
poste actuel. `main` est la référence historique C#/Godot, sans dépendance
d'exécution du produit Rust. Sur un autre poste, utiliser un clone de `Rust` et
adapter les chemins ; sur ce poste, conserver le checkout commun.

Gate 3 est clôturée le 15 octobre ; Gate 4 est **clôturée localement** le
16 octobre, lot `0.1.0-preview.20261016.019`. Les
[rapports Gate 3](evidence/gate3/GATE3_CLOSURE_20261015.md) et
[Gate 4](evidence/gate4/GATE4_CLOSURE_20261016.md) portent les critères et limites.
Les anciennes sections « reste ouvert » ou « à acquérir » sont des constats datés,
pas des instructions de recommencer les gates. Distribution, Windows vierge,
Linux, réserve GLib et résultats distants historiques gardent des verdicts séparés.

**État au 22 octobre : Gate 5 clôturée localement**, selon son
[rapport final](evidence/gate5/GATE5_CLOSURE_20261022.md) : treize domaines et
R5-P01/P02 validés. Il s'agit de fondations pour créer des quêtes ensuite, sans
livraison d'aventure. Gate 6 est ensuite ouverte le 22 octobre et **clôturée
localement le 23 octobre**, selon son
[rapport final](evidence/gate6/GATE6_CLOSURE_20261023.md) : 21 critères, deux
solutions jouées/reprises pour chaque parcours et archive vérifiée. Le
[bilan comparatif](evidence/gate6/GATE6_COMPARISON_20261023.md) conserve ses limites,
sans gain général d'efficacité démontré. **Gate 7 est ouverte depuis le 23 octobre**
avec objectif de clôture documentée ; suivre son
[plan](planning/GATE7_IMPLEMENTATION_PLAN.md) et son
[suivi](evidence/gate7/GATE7_PROGRESS_20261023.md). Les verdicts à jour
figurent dans l'[état courant](#gate-7-etat-courant). Gate 8 et la gate bonus
restent non ouvertes. GitHub sert
de sauvegarde distante ; ses contrôles sont arrêtés et ne conditionnent plus
les gates, selon ADR-0034. Les validations locales restent exigées. La clôture
n'atteste pas un commit ou un push des modifications locales.

**Reprise du 19 octobre :** le propriétaire ouvre Gate 5 avec objectif de clôture,
selon le [plan de réalisation](planning/GATE5_IMPLEMENTATION_PLAN.md). Pour reprendre
un chantier déjà autorisé, établir son objectif, ses fichiers et ses preuves
actuelles avant toute modification ; une ancienne conversation n'est pas requise
pour comprendre les règles du dépôt.

La répartition propriétaire du 20 octobre remplace celle de l'ouverture : E:
porte les sources, outils, téléchargements, compilations, caches et temporaires ;
F: conserve les preuves, l'historique et la bibliothèque d'assets convertis.
Compiler le checkout commun avec `scripts/Invoke-ForgeCargo.ps1`, la cible
`E:/OpenRPG_Forge/.build/cargo-target`, `CARGO_HOME` sous `.build/cargo-home`,
`CARGO_INCREMENTAL=0` et `TEMP`/`TMP` sous `.build/tmp` sur E:.
Neutraliser les anciennes variables pointant vers F:, sans recopier les caches
historiques. Les preuves sont centralisées sous
`E:/OpenRPG_Forge/Preuves-et-analyses/actives/gate5/20261019/`, physiquement sur F:.
L'archive vérifiée est `F:/OpenRPG_Forge/Preuves-et-analyses/archives/gate5-20261022-e-001.zip` ;
son catalogue est `E:/OpenRPG_Forge/Preuves-et-analyses/rapports/gate5-closure-20261022-e-001.json`.
Les anciens chemins et reçus restent historiques. Vérifier les processus actifs
avant toute nouvelle compilation et consulter le rapport final avant de rejouer
une qualification déjà acquise.

## Préserver le travail local avant la reprise

Le commit de référence de l'inventaire initial du 17 octobre est
`2b0d60fa` (`feat: close Gate 4 governed assets and standalone module delivery`).
Cela identifie un état Git, pas tous les fichiers locaux ni tous les octets
qualifiés : le rapport Gate 4 référence aussi un gel de sources et ses preuves.

La séparation du 18 octobre travaille depuis
`3c3a5efeba2473574597871f223d74aa800cf99a`, toujours sur le checkout partagé
modifié. Les nouveaux composants de carte et les contrats neutres doivent être
conservés avec les suppressions de leurs anciens emplacements : un clone du
commit seul ne reproduit pas ce lot local avant son commit et sa publication.

L'[inventaire local daté](handoff/LOCAL_WORKTREE_20261017.json) liste les états
Git et les chemins visibles pendant cette passation, hors fichiers de cette édition,
avec taille et SHA-256 des fichiers texte de travail lisibles. Les binaires
locaux sont seulement recensés par chemin et taille. Les suppressions restent
des suppressions ; un inventaire n'est ni une sauvegarde ni un commit.
Les fichiers ignorés et les données hors checkout ne sont pas inventoriés par Git.
Le travail concurrent peut évoluer après cette capture.

Le lot local comprend notamment les scripts d'import `scripts/hivemind/`, leurs
deux documents de suivi, des suppressions de configuration Codex et d'anciens
artefacts d'installateur. Ne pas tout ajouter à Git automatiquement. Les suivis
locaux `docs/planning/HIVEMIND_SAVED_PHYSICS_REFERENCE.md` et
`docs/planning/HIVEMIND_SOURCE_LOD_HIERARCHY.md` font partie de ce travail non
publié ; leurs preuves privées peuvent être sur `F:`. Ils sont recensés dans
l'inventaire et doivent être récupérés avec la sauvegarde locale si nécessaires.

Depuis la racine du checkout, ces commandes de lecture permettent de réévaluer
la situation (exécutées pendant la revue) :

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
git diff --name-status
git ls-files --others --exclude-standard
```

Avant de changer de machine, conserver une sauvegarde privée du checkout avec
ses fichiers locaux, des données ci-dessous et des cibles de jonctions utilisées.
Vérifier la relecture et les empreintes de cette sauvegarde. Un clone Git ne
contient pas les fichiers non suivis, les fichiers ignorés ou les données privées.
Ne pas exécuter `reset --hard`, `clean` ou un nettoyage global pour reprendre.
Ne pas restaurer les anciennes configurations d'agents supprimées. Aucun commit
ou push n'est autorisé par ce guide.

## Environnement de développement

La procédure actuellement documentée vise Windows x64. Préparer Git, PowerShell
7.2 ou plus récent (`pwsh`, utilisé par les hooks), Rustup avec MSVC/Windows SDK,
WebView2 pour le shell Studio, Python 3.11 ou plus récent pour les outils et Node.js pour le
frontend Aube et les tests navigateur. Ces outils de développement ne sont pas
des prérequis du client installé. Linux n'est pas qualifié par cette passation.

La [toolchain](../rust-toolchain.toml) épingle Rust **1.98.0**, rustfmt, Clippy,
rust-src et `wasm32-unknown-unknown`. Le
[bootstrap](../scripts/Bootstrap-Rust.ps1) épingle cargo-deny 0.20.2,
cargo-audit 0.22.2, Trunk 0.21.14, Tauri CLI 2.11.4 et wasm-bindgen-cli 0.2.127.
Le script télécharge des dépendances et refuse une branche autre que `Rust`.
Les dépendances locales sous `third_party/` sont nécessaires : garder le clone
complet et `Cargo.lock`. Cadle n'est pas requis pour compiler Forge.

Procédures déjà présentes dans le dépôt, à exécuter sur le poste de reprise après
installation des prérequis (non relancées lors de cette édition documentaire) :

```powershell
.\scripts\Bootstrap-Rust.ps1 -InstallTools
.\scripts\Test-Rust.ps1
```

Le second script lance formatage, Clippy, tests natifs, contrôles WASM, Trunk et
supply-chain. Sélectionner les validations selon le changement et le protocole,
sans assimiler compilation, tests, GPU, acceptation humaine et qualification.
La supply-chain conserve une réserve GLib/Linux ; lire le résultat effectif.

## Données et lancement du poste actuel

Le point d'entrée du propriétaire reste `E:\OpenRPG_Forge\Lancer-Editeur.cmd`.
Il lance le Studio commun et définit `OPENRPG_ASSET_VAULT`. Préserver ce lanceur
et son profil ; changer d'éditeur de code ne nécessite pas de les remplacer.
Les valeurs suivantes ont été lues dans le lanceur, le profil et les scripts le
17 octobre ; elles sont des chemins du poste actuel, pas des valeurs universelles.

| Élément à conserver ou remapper | Emplacement actuel / rôle |
|---|---|
| Studio et profil côte à côte | `E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\release\forge-studio.exe` et `forge-studio.local.json` |
| Projets et cartes auteur | champ `authoring_root` du profil : `E:\OpenRPG_Forge\.gate3-evidence\20261011\p05-human-review-shared019-rpg007-001\authoring-data` |
| Vault privé | `F:\OpenRPG_AssetVault`, référencé par le lanceur et `asset_vault` |
| Assets du frontend | `E:\OpenRPG_Forge\.forge-local\studio-assets`, avec `frontend-static-inputs.json` |
| Données du jeu et des scènes | `E:\OpenRPG_Forge\.forge-local\runtime-data` ; `module_assets.root` vise `data\scene` et son catalogue épinglé |
| Jeu natif séparé | `game.executable` : `E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\release\examples\tidewater_plant_probe.exe` |
| Rapports des parties | `game.session_root` : `E:\OpenRPG_Forge\.forge-local\game-sessions` |
| Preuves et livraisons locales | dossiers voisins `.gate3-evidence`, `.gate4-evidence`, installateur et contenus référencés par les rapports |
| Travaux privés d'import | `F:\OpenRPG_ImportWork` et les emplacements indiqués par les suivis d'import |

Sur une autre machine, rétablir les données nécessaires puis préparer un profil
pour cette machine avec ses chemins. Les chemins relatifs du profil se résolvent
par rapport à l'EXE. Ne pas retirer les vérifications de catalogue/hash pour
compenser une donnée absente. Les projets, sauvegardes, sources privées et leurs
licences doivent être transférés de manière autorisée ; ils ne sont pas inclus
automatiquement dans Git. Les liens des rapports vers les preuves voisines ne
seront utilisables qu'après restauration de ces dossiers.

## Compiler le Studio et préparer une livraison

Le hook [Trunk](../apps/forge_studio/Trunk.toml) compile le worker de navigation
et prépare les assets. Il exige `CARGO_TARGET_DIR` explicitement ; le staging
utilise `FORGE_STUDIO_LOCAL_ASSETS` ou, par défaut, le dossier voisin
`.forge-local/studio-assets`. Il contrôle le manifeste et refuse certaines
jonctions. Un répertoire vide ne remplace pas les données authentifiées.
La présence du manifeste frontend a été constatée pendant la revue ; son contenu
n'a pas été requalifié. L'accès au Vault `F:` a été refusé par l'environnement
de cette session : sa disponibilité et son contenu restent à vérifier sur le
poste de reprise. Ne pas confondre refus d'accès et absence du dossier.

Sur le poste actuel, la consigne propriétaire du 20 octobre remplace celle de
l'ouverture de Gate 5 : développer et compiler sur E:, conserver preuves,
historique et bibliothèque d'assets sur F:. Les chemins installés déjà constatés
plus haut restent des références de l'installation existante.

```powershell
$env:CARGO_TARGET_DIR = 'E:\OpenRPG_Forge\.build\cargo-target'
$env:CARGO_HOME = 'E:\OpenRPG_Forge\.build\cargo-home'
$env:CARGO_INCREMENTAL = '0'
$env:TEMP = 'E:\OpenRPG_Forge\.build\tmp'
$env:TMP = $env:TEMP
$env:FORGE_STUDIO_LOCAL_ASSETS = 'E:\OpenRPG_Forge\.forge-local\studio-assets'
```

Pour le frontend Gate 5, utiliser une destination de travail sur E: avant
d'appeler le wrapper Trunk, puis conserver les preuves utiles sur F:. Le hook
navigation utilise `TRUNK_STAGING_DIR` et hérite du cache Cargo et des temporaires E:.

```powershell
$env:TRUNK_BUILD_DIST = 'E:\OpenRPG_Forge\.build\frontend\gate5-candidat-unique'
$env:TRUNK_OFFLINE = 'true'
$env:TRUNK_SKIP_VERSION_CHECK = 'true'
$env:CARGO_NET_OFFLINE = 'true'
$env:PATH = 'E:\OpenRPG_Forge\.build\tools\binaryen-131-001\bin;' + $env:PATH
```

Trunk 0.21.14 et wasm-bindgen 0.2.127 sont disponibles sur le poste ; ce dernier
correspond au verrou courant. Les exécutables existants sur C: peuvent être lus,
mais aucun nouvel outil ou cache n'y est installé. Le mode hors ligne refuse un
outil absent avant d'ouvrir le cache automatique de Trunk : vérifier les outils
dans `PATH` et préparer les compléments éventuels sous `.build/tools/` sur E:. Voir le
[code de résolution des outils de Trunk 0.21.14](https://github.com/trunk-rs/trunk/blob/v0.21.14/src/tools.rs#L265).
Cette vérification ne constitue pas un build frontend. Le packager
`Build-StudioCandidate.ps1` accepte désormais une sortie neuve sous
`F:/OpenRPG_Forge/` ; lui fournir explicitement les dossiers de cache et de
temporaires F:. Ses anciens défauts ne sont pas les paramètres de Gate 5.
Le fichier d'entrée demande l'optimisation WASM `z`. Binaryen 131 a donc été
recopié depuis l'outil existant sur E: vers le dossier F: ajouté au `PATH`
ci-dessus, avec vérification d'empreinte, version exécutée et licence Apache 2.0.
Le reçu `wasm-opt-tool-001.json`, sous les preuves Gate 5 sur F:, indique qu'aucun
téléchargement ni build frontend n'a été exécuté par cette préparation.
Le hook Trunk peut déjà avoir copié `forge-kit`. Fournir tout de même
`-GovernedKit` au packager : celui-ci exige alors la même liste de fichiers et
les mêmes empreintes, sans remplacement ni duplication des pins. Les tests
d'entrée `candidate-kit-input-tests-002` du 20 octobre passent sur F:, y compris
le refus d'un fichier caché supplémentaire ; ils ne
constituent ni un build du Studio ni une qualification graphique.
Avant tout lancement graphique du nouveau binaire, fixer aussi :

```powershell
$env:FORGE_WEBVIEW_DATA_ROOT = 'E:\OpenRPG_Forge\.build\webview\studio'
```

Le raccordement natif `native_webview.rs` diffère alors la création automatique
des fenêtres et fournit ce chemin absolu au constructeur Tauri avant toute
création de WebView ; la fenêtre Atelier emploie le même dossier. Une valeur
vide ou relative est refusée au démarrage. Ce réglage est nécessaire car le
gestionnaire Tauri peut créer son dossier local par défaut avant l'appel à
WebView2. Les constats préparatoires du 20 octobre sont remplacés, pour l'état
courant, par les validations du candidat final et les parcours graphiques acquis
dans le [rapport Gate 5](evidence/gate5/GATE5_CLOSURE_20261022.md).
Vérifier le chemin effectif dans les processus WebView2 lors d'un nouveau
lancement et conserver les preuves nécessaires dans le catalogue central sur F:.

Le wrapper [Invoke-StudioTrunk.ps1](../scripts/Invoke-StudioTrunk.ps1) gère le
build/serve et normalise `NO_COLOR`. Pour une reconstruction livrable, suivre
[scripts/delivery/README.md](../scripts/delivery/README.md), le validateur commun
et ses reçus, plutôt que d'assembler d'anciens binaires. Le target générique
`forge_runtime` et le jeu livré `tidewater_plant_probe` ont des rôles distincts.
Sérialiser les builds lourds et préserver les sessions du propriétaire.

Le lot historique distingue le launcher Aube, le gestionnaire de contenus,
Studio, le jeu natif et les packs. Cette organisation décrit les livraisons
existantes, pas la cible obligatoire du 2 novembre : éditeur et launcher du jeu
exporté. Conserver les installations utilisables pendant la transition et ne
retirer le Hub qu'après transfert et qualification de ses fonctions utiles. Le
[rapport Gate 4](evidence/gate4/GATE4_CLOSURE_20261016.md) identifie l'installateur
unique et les contenus séparés du lot 019, avec empreintes et limites.

## Fin d'une session de reprise

Consigner le périmètre modifié, les commandes réellement exécutées et leurs
résultats, les preuves et limites, puis la prochaine action concrète. Actualiser
le contexte ou la roadmap seulement si leur état change. Ne pas stocker une
décision indispensable uniquement dans la mémoire d'un assistant. Une mémoire
ancienne ne remplace jamais les fichiers et preuves actuels du projet.

Cette passation a été vérifiée par lecture des scripts, contrôle des liens
documentaires et `git diff --check`. Aucun bootstrap, build, test produit,
lancement de jeu ou transfert de données n'a été effectué pour cette édition.
Le propriétaire demande ensuite la publication du lot documentaire sur `Rust` ;
elle conserve les autres travaux locaux hors de son périmètre.
La reprise depuis un clone neuf sur un autre PC reste à éprouver.

## Contrôles des entrées moteur communes — 18 octobre 2026

Le [bilan d’architecture](planning/ENGINE_RESERVES_20261018.md) distingue les
corrections réalisées de la généricité globale encore ouverte. Le runner local
reproduit les garde-fous, Clippy natif/WASM, tests workspace et le probe optique :

```powershell
pwsh -NoProfile -File scripts/Test-GenericEngine.ps1 -PrepareBrowser
```

Il écrit dans un nouveau dossier `output/generic-engine-<date>`, déjà ignoré par
Git. Il exige une toolchain et les dépendances Rust disponibles localement
(`--locked --offline`) et le CLI `wasm-bindgen` pour préparer le navigateur.
`-TargetDirectory` permet de choisir le cache Cargo. L’étape navigateur doit être
exécutée sur localhost avec WebGPU : la préparation ne vaut pas preuve de rendu.
Le [guide des API](../crates/forge_tidewater/COMMON_SCENE_INPUTS.md) précise les
conventions et limites. Ce runner n’installe ni ne publie le produit et ne clôture
aucune gate.

Pour le probe navigateur, servir le dossier de résultat affiché par le runner :

```powershell
python -m http.server 8769 --bind 127.0.0.1 --directory "output/generic-engine-<date>"
```

Ouvrir `http://127.0.0.1:8769/index.html` dans un navigateur WebGPU. La page doit
afficher PASS après les dix readbacks, puis arrêter le serveur avec Ctrl+C.
Les outils vérifiés ici sont Rust 1.98.0 et `wasm-bindgen` 0.2.127, ce dernier
devant correspondre au lockfile. Les dépendances et le CLI doivent être disponibles
localement avant les commandes `--offline`.

## Assistant IA et MCP — Gate 6 clôturée localement

Les frontières du sous-système sont consignées dans
[ADR-0035](ADR/ADR-0035-assistant-governed-proposals.md). Le domaine de proposition
est sous `crates/forge_project/src/assistant/` ; les protocoles, coordinateurs
natifs et panneaux sont sous `apps/forge_studio/src/assistant*` et `mcp*`.
Le [rapport courant](evidence/gate6/GATE6_PROGRESS_20261022.md) distingue les
preuves métier, fournisseur, Studio et jeu ; ne pas déduire une clôture de la
seule réussite d’un test ou d’un build.

Ajouter la section `assistant` au profil `forge-studio.local.json` adjacent à
l’exécutable. **Conserver les autres sections du profil existant**, notamment
les chemins d’auteur, de Vault et du jeu. Exemple correspondant au serveur local
fourni pour cette qualification :

```json
{
  "assistant": {
    "base_url": "http://192.168.1.41:8080/v1",
    "model": "qwen3.8-flash-next-iq4-xs",
    "api_kind": "chat_completions",
    "enable_reasoning": true,
    "api_key_environment": null,
    "limits": {
      "maximum_attempts": 2,
      "maximum_input_tokens": 100000,
      "maximum_output_tokens": 16384,
      "maximum_micro_usd": 0,
      "prices": {"input_per_million": 0, "output_per_million": 0}
    }
  }
}
```

Les tarifs sont des entiers en micro-USD par million de tokens. Les zéros de cet
exemple décrivent l’absence de facturation fournisseur déclarée pour le serveur
local, sans mesurer son coût matériel ou électrique. Pour une clé, inscrire
seulement le nom de sa variable native dans `api_key_environment`, jamais sa
valeur dans le profil, les sources ou les preuves. Une adresse HTTP exige une
IP locale explicitement configurée ; une destination distante exige HTTPS.
`FORGE_ASSISTANT_CONFIGURATION` permet une surcharge native limitée au processus
de qualification ; elle ne modifie pas le profil du propriétaire.

Sauvegarder puis relancer Studio pour charger la configuration. Dans **Créer et
raconter → Assistant**, vérifier destination, modèle et plafonds. Sélectionner
les propriétés, cibles et région ; les références d’assets se recherchent
localement puis se cochent, avec un droit de placement séparé. La création de
contenu est un choix explicite et exige les capacités Creator adéquates. Préparer
et relire les données exactes, autoriser l’envoi, attendre le résultat, examiner
le diff puis approuver l’application et sauvegarder la carte. Le délai affiché
varie de 2 à 16 minutes avec le plafond de sortie ; une attente n’entraîne aucun
nouvel appel automatique. Le raisonnement est demandé explicitement sans être
affiché ou enregistré.

Les jobs restent sous `assistant-jobs` de la racine d’auteur. Après interruption,
inspecter le panneau de reprise et les empreintes saved/pending avant toute
promotion. Réutiliser le résultat reçu avec une nouvelle revue quand le contexte
le permet ; un Started incertain ne prouve pas un échec serveur. Un résultat
valide dépassant le budget reste conservé et bloqué. Ne pas effacer un journal
pour contourner ses quotas. Après une application non sauvegardée perdue, seul
le contexte original permet de retrouver le résultat Staged ; un reçu canonique
présent est réconcilié sans nouvelle application.

Pour MCP, le client lance ce même exécutable avec `--mcp-stdio`. L’interface
Studio doit d’abord préparer, afficher et autoriser le contexte. Les outils
`forge_inspect_selection` et `forge_propose_edits` ne peuvent pas approuver ou
appliquer un diff. L’approbation reste dans Studio. Les propositions persistées
sous `assistant-mcp-proposals` n’accordent aucun droit au redémarrage : autoriser
à nouveau le contexte puis relire le nouveau diff. Une ancienne approbation est
invalide. Révoquer ou déconnecter retire l’autorité sans purger les preuves.

La campagne de qualification reste consultable sous
`Preuves-et-analyses/actives/gate6-ia-mcp/` au hub commun, physiquement sur F:.
Elle est aussi archivée dans `archives/gate6-ia-mcp-20261023.zip`, lecture et
SHA-256 intégralement vérifiés, avec catalogue central et reçu
`rapports/gate6-archive-20261023.json`. Les originaux sont conservés. Le candidat
Studio qualifié est le binaire commun `f23467af…` ; le Studio déjà ouvert du
propriétaire n'est pas remplacé. Le dernier contrôle Qwen est injoignable :
vérifier l'endpoint avant tout nouvel envoi, sans inventer de résultat réussi.
Les scripts `scripts/gate6/` compilent le produit commun et préparent des données
isolées. Le pilote UI observe les sources sauvegardées ; il ne remplace pas les
clics de consentement et d’application. Le lancement canonique du propriétaire
et son profil ne doivent pas être remplacés par ces données de test.

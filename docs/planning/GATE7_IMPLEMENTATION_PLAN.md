# Gate 7 — Monde étendu et distribution

Ouverture propriétaire : 23 octobre 2026. Objectif : clôture sur preuves.
Autorités : `PROJECT_CONTEXT.md`, `MIGRATION_ROADMAP.md`, `AGENTS.md`, ADR-0034.
Checkout commun : `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`, branche `Rust`.

État au 7 novembre : **R7-01 à R7-06 qualifiés localement sous Windows**.
Le 049 privé couvre les volets Windows de R7-07, R7-09 et R7-10. Leurs
volets Linux et R7-08 restent ouverts. Voir l'[état courant](../HANDOFF.md#gate-7-etat-courant)
et la [matrice d'acceptation](../evidence/gate7/GATE7_PROGRESS_20261023.md#matrice-dacceptation).

Le 10 novembre, le frontend Web release a été construit avec les deltas
[Tachys](../../third_party/tachys-keyed-list-NOTICE.md) et
[Leptos macro](../../third_party/leptos-macro-case-NOTICE.md). Les trois
dépendances Cargo en réserve dans le 049 sont absentes du graphe de ce
build. Le paquet privé 050 a passé le démarrage et la relecture des
quatre index de notices sur Windows Sandbox hors ligne sans SDK.
L'audit ne relève aucune identité Cargo sans texte candidat. Les droits
de redistribution restent à établir.

R7-02 : parcours natif 017 sur module de l'export
privé 1.0.2, substitution, retour au détail et réactivation sur les profils ;
réduction géométrique mesurée sur le cas dense séparé. Reçu
`R7-02-hlod-assessment-017.json`, SHA-256
`e8878ffdf90deb114011fa627a47d56dbda5dad9c54d8b000219ebc5688212e2`.
R7-06 : parcours réel 011 rapproché des mêmes
sources/frontend 015 et des 32 tests actuels ; 45 preuves et 11 sources vérifiées.
Reçu `R7-06-game-launcher-assessment-015.json`, SHA-256
`decfb4a2c863f3304040240a4bdbc6f5a65d96288a70b4b5df9088626f6025e1`.
Installation 015 et application des réglages indiqués non pris en charge restent distinctes.
R7-04 est rapproché dans le reçu central 015
`R7-04-runtime-packaging-assessment-015.json`, SHA-256
`5ef6f45265c58aeb924523d1b1578e315a71aa5c75792a440eda80b68bb8a2be`.
Il ne vaut pas installation 015 ou OS propre. R7-05 est rapproché dans le reçu central 015
`R7-05-graphics-profiles-assessment-015.json`, SHA-256
`cf9dc52a2f100dc62fa3284e60183307d59c4050d728801c47a6729c97e83b38`.
Profils et mesures réelles existent ; leurs fenêtres courtes ne valent pas
benchmark FPS ni parité Tidewater. R7-01 est rapproché dans le reçu central 015
`R7-01-streaming-assessment-015.json`, SHA-256
`e4e7c0b2cc98fb6849e9f26b5d67c907d23f46028988a9605d3c6b8819341569`.
Les 72 tests actuels, le trajet et les services de persistance suffisent à ce
rapprochement ; ne pas imposer une trace par cellule, un objectif FPS ou un autre
trajet sans cause nouvelle. Cela ne clôture pas HLOD, performances ou distribution.
Le Studio 015 est désormais exporté et vérifié (`studio-ui-export.json`, SHA-256
`0434e332d728edc7230305298f48956eaca8f8cc34050a989859c12b60a1529f`).
015 non installé. Retour 011 → 008 qualifié sur l'installation de test
révision 3 : reprise depuis `Jouer.exe`, sauvegarde et état inchangés.
La désinstallation du Camp 017 est vérifiée, avec conservation des deux
sauvegardes. Reçu `R7-09-camp-uninstall-saves-017.json`, SHA-256
`acd8567fb5e23dd304ca6b5371f180b91e25577d783c5f5b96a02b831bebe326`.
Le cycle du candidat privé Windows 049 est consigné dans le rapprochement
`R7-07-09-10-windows-049-assessment.json`, décrit dans `docs/HANDOFF.md`.

Point opératoire du 5 novembre : mise à jour installée 008 → 011, retour 008
et reprises de la même sauvegarde vérifiés. La consolidation 014 est terminée : cinq stages de build sur
sources stables, chargement réel du viewport Studio puis export privé vérifié
(`grouped-model-readiness-20261105-014/studio-ui-export.json`, SHA-256
`61e76c7226803dde3789c3473e69522061a8003c3b9a9f0d6a927467933b670b`).
014 non installé. Aucun nouveau critère global clos ; les limites du suivi
restent applicables. Le retrait des deux copies de repli Studio/kit 011 a été
refusé par la politique automatique, zéro octet retiré / 341 810 271 conservés ;
ne pas contourner ce refus.

Un cycle de profils 014 a ensuite reproduit un crash Ultra → Low. Le correctif
015 du contrôleur moteur commun passe cinq tests, Clippy natif/Web, les quatre
profils réels et un redémarrage relisant High malgré un défaut Ultra. Reçu 015
`qualification-assessment.json`, SHA-256
`f218506709e3dbc37a0a7678805bbd51c9c99899e2ffd91ef67333b1140e328e`.
Kit 015 assemblé ; frontend Studio en cours de consolidation. Ne pas rejouer le
cycle déjà acquis sans nouvelle cause. Le candidat 015 n'est pas installé.

<a id="transition-editeur-launcher"></a>

## Transition obligatoire : éditeur et launcher du jeu — 2 novembre 2026

**Décision propriétaire acceptée, transition en cours.** Le produit expose
l'éditeur OpenRPG Forge et le launcher propre au jeu créé avec cet éditeur.
Le Hub séparé sort de la cible. Le contexte canonique définit la règle ; cette
section est son plan d'exécution unique, dans **Gate 7 déjà ouverte**. Le plan
initial du 2 novembre était documentaire ; des raccords et des tests ont depuis
été réalisés, avec leurs limites dans le [suivi](../evidence/gate7/GATE7_PROGRESS_20261023.md).
Les parcours complets et les lots ci-dessous restent à qualifier.

### Cible et écarts constatés

Parcours final : **créer/configurer dans le Studio → exporter le jeu avec son
launcher → installer et jouer sans le Studio ni l'environnement de développement**.
Le runtime natif peut rester un exécutable interne lancé par le launcher ; il
consomme le même moteur que l'éditeur. Les services et CLI techniques, ainsi que
l'assistant d'installation ponctuel, ne deviennent pas des applications produit
supplémentaires. Aucun objectif artificiel de « deux fichiers seulement ».

| Existant vérifié le 2 novembre | Destination obligatoire | Travail restant |
| --- | --- | --- |
| `apps/forge_studio` : création/édition et export de modules | Éditeur Forge, propriétaire du parcours auteur et de la préparation de distribution du jeu | Configurer le launcher depuis le projet et raccorder l'export complet, sans assembler manuellement des JSON ou copier des binaires hors produit |
| `apps/forge_launcher` : interface Aube, paquet Cargo `forge_launcher_prototype`, inventaire de modules installés | Une implémentation de launcher réutilisable, configurée et livrée pour chaque jeu | Préserver les fonctions et la qualité utiles, retirer l'identité Aube imposée et la dépendance au répertoire de conception local ; limiter l'inventaire au jeu et à ses contenus déclarés |
| `apps/forge_hub` : gestionnaire séparé, installation, vérification, rollback et accès jeu/éditeur | Fonctions auteur dans le Studio, opérations joueur nécessaires dans le launcher du jeu | Inventorier puis transférer chaque commande ; retirer ensuite l'application, ses raccourcis et ses recettes de distribution |
| `tools/forge_delivery` : services/CLI de livraison et exécution des modules | Services natifs communs réutilisés par les deux parcours et l'installateur | Conserver plans, admission, transactions, hashes, refus et reprise ; aucun nouveau backend concurrent |
| `build_classic_installer.py`, `build_delivery.py` : recettes `launcher` et `content-manager` distinctes, assemblage des applications | Distribution de l'éditeur et distribution autonome du jeu exporté, chacune avec ses dépendances nécessaires | Séparer les ensembles livrés par rôle ; aucun Hub ni installation préalable de Forge requis pour jouer au jeu exporté |

Sources de l'inventaire initial : README des trois applications, manifestes
Cargo, `apps/forge_launcher/src/installed_play.rs`,
`tools/forge_delivery/README.md` et scripts de livraison ci-dessus. Le schéma
et l'export actuels des modules ne sont pas déclarés équivalents à un export
autonome de jeu avec son launcher. La carte d'architecture exploratoire décrit
l'existant ; elle n'est ni une preuve de qualification ni la cible à reproduire.

### Lots ordonnés et conditions de sortie

L'inventaire D1 a été réalisé le 2 novembre
(`editor-launcher-d1-inventory-001.md`). Sous Windows, les preuves des lots
017 et 020–021 couvrent l'export depuis le Studio, le lancement et la reprise
sans Studio côté joueur, l'isolation des sauvegardes entre deux jeux et le
cycle d'installation du Studio et du jeu exporté. Voir le
[suivi des critères](../evidence/gate7/GATE7_PROGRESS_20261023.md).
Les lots 042, 046 et 047 qualifient la migration et la reprise Windows
sur des parcours ciblés. Le 048 retire la cible Hub du checkout. Le
candidat réunifié doit encore couvrir les critères de Gate 7.

1. **G7-D1 — Borner la transition sur les entrées courantes.** Inventorier les
   commandes Hub/launcher/Studio, leurs consommateurs, tests, chemins installés,
   raccourcis, exports, contrats et données utilisateur. Associer chaque fonction
   utile à son utilisateur et à un propriétaire existant. Relever les processus
   et sources de qualification actifs ; identifier les preuves devenant à rejouer.
   Compléter ADR-0021 par un addendum daté, sans réécrire sa décision historique,
   puis les README concernés. Sortie : matrice de transfert exhaustive et fermeture
   de dépendances proposée, sans suppression ni nouveau build à cette étape.
2. **G7-D2 — Fermer la configuration de jeu et de launcher.** Examiner d'abord
   les documents projet/module/package existants, puis y porter, avec version et
   migration si nécessaire, l'identité du jeu, titre, présentation, ressources
   licenciées, contenus/cartes admis, réglages et point d'entrée. Le projet reste
   la source de vérité éditable ; le manifeste livré en est une dérivation validée,
   pas une configuration canonique concurrente. Aube et Tidewater restent des
   projets de référence ; un nouveau projet n'hérite pas de leurs contenus.
   Sortie : contrats versionnés et cas de refus/relecture validés, avec identités
   et emplacements de sauvegarde séparés pour deux jeux indépendants.
3. **G7-D3 — Raccorder les deux interfaces au même socle.** Le Studio permet de
   régler la présentation et de préparer la distribution du jeu. Les opérations
   Hub nécessaires à l'auteur rejoignent ce parcours. Le launcher existant devient
   configurable par le projet exporté et n'énumère que son jeu et ses contenus
   déclarés ; les opérations joueur de vérification, mise à jour ou restauration
   qui sont retenues appellent les services communs. Aucune fonction d'édition
   n'est requise pour jouer. Conserver les capacités utiles de présentation,
   réglages, documentation et navigation en les liant au projet. Sortie : gestes
   réels Studio/launcher, erreurs de sauvegarde/install visibles, clavier et
   accessibilité vérifiés selon les règles UX/UI du dépôt ; aucun shell de remplacement.
4. **G7-D4 — Exporter et installer un jeu autonome.** Raccorder le builder et
   les recettes existantes au parcours Studio, avec fermeture des ressources,
   licences, programmes, profils et identités réellement utilisés. Le jeu exporté
   contient son launcher configuré et son runtime commun, sans Hub, Studio,
   checkout, Vault de développement, `launcher-aube`, Node/Cargo/Python ni
   fournisseur IA obligatoire au lancement. Les prérequis OS admis restent
   déclarés et vérifiés. Préserver les assets nécessaires et leurs droits :
   un export privé/local ne devient pas redistribuable par cette transition.
   Sortie : depuis le Studio, créer/retoucher, sauvegarder, exporter, installer,
   lancer et reprendre un jeu indépendant ; modifier sa présentation puis
   réexporter sans perdre les retouches ni mélanger ses sauvegardes avec celles
   d'un second jeu. Aucun travail manuel hors produit ne vaut preuve du parcours.
5. **G7-D5 — Retirer le Hub et les résidus remplacés.** Après les preuves D3/D4,
   migrer les installations existantes sans perte des workspaces, sauvegardes,
   modules et droits. Vérifier upgrade interrompu, reprise, rollback et refus
   d'installation incompatible. Retirer `apps/forge_hub`, sa cible, recettes,
   raccourcis et tests exclusivement obsolètes ; réaffecter les tests de
   comportement conservés aux services/parcours correspondants. Retirer les
   anciens noms `prototype` et dépendances de conception devenus inutiles après
   inventaire des consommateurs. Toute compatibilité temporaire doit avoir un
   usage nommé et une condition de retrait, sans maintenir un troisième produit.
   Sortie : aucune dépendance active au Hub dans le nouveau parcours ; bilan des
   fichiers/octets retirés ou encore nécessaires. Les sources uniques, licences
   et reçus historiques ne sont pas supprimés comme des caches.
6. **G7-D6 — Qualifier le candidat Gate 7 réunifié.** Produire les binaires et
   distributions depuis les mêmes entrées stables, rejouer les tests ciblés puis
   les parcours complets affectés sur Windows et Linux. Vérifier installation
   neuve, évolution d'une installation existante, désinstallation préservant les
   données et deux jeux indépendants. Exécuter le parcours utilisateur sur OS
   propre avec les prérequis déclarés ; un dossier isolé du poste de développement
   ne suffit pas. Rapprocher les dix critères et leurs réserves sans déduire un
   résultat visuel/performance d'un test de contrat, d'un build ou d'un renommage.
   Sortie : mêmes critères Gate 7, preuves finales du nouveau parcours et verdict
   conservé ouvert tant qu'une exigence manque ; aucune Gate 8 ou publication implicite.

### Rattachement aux critères existants de Gate 7

Cette décision précise le produit à qualifier ; elle ne remplace pas les dix
critères par une nouvelle liste et n'abaisse aucune exigence. État courant :
R7-03 qualifié localement sous Windows sur son reçu du 27 octobre, R7-01/R7-02/R7-04/R7-05/R7-06 sur les
rapprochements du 5 novembre ; quatre critères ouverts. Aucun lot D1–D6 ne vaut à
lui seul clôture d'un critère.

| Critère | Portée de la transition et requalification attendue |
| --- | --- |
| R7-01 — Streaming | Préserver le propriétaire moteur commun ; vérifier son emploi effectif par le jeu exporté et ses transitions, sans seconde implémentation |
| R7-02 — HLOD | Préserver la représentation et les mesures exigées ; qualifier le même chemin dans le runtime livré, sans simplifier la scène pour faire passer le launcher |
| R7-03 — Sauvegardes | Conserver le verdict local Windows daté ; tester en régression les identités, chemins et reprises depuis les nouveaux exports/launchers, sans promouvoir ce reçu en qualification multiplateforme |
| R7-04 — Packaging runtime | Jeu et launcher autonomes issus du projet, ressources fermées, refus d'altération et absence de dépendance au Hub/Studio/Vault/IA de développement |
| R7-05 — Profils | Low/Medium/High/Ultra configurables, appliqués et persistés dans le même moteur ; rendu et performances réels toujours à qualifier |
| R7-06 — Lanceur | Launcher propre au jeu exporté : identité issue du projet, lancement/reprise, contenus déclarés, erreurs et retour ; Aube seule ne prouve pas la généricité |
| R7-07 — Windows/Linux | Construire et contrôler les rôles éditeur et jeu/launcher sur chaque plateforme, depuis les entrées effectivement qualifiées |
| R7-08 — Licences | Agréger les droits des composants et contenus réellement livrés dans chaque rôle ; garder les réserves non résolues visibles |
| R7-09 — Installateur | Installation/mise à jour/reprise/rollback et désinstallation des distributions cible, sans Hub résident et avec conservation des données existantes |
| R7-10 — Machine propre | Parcours éditeur/export et jeu distribué sur les OS propres visés, sans dépendance de développement ; distinguer les prérequis des deux rôles |

### Coordination avec les qualifications en cours

- Continuer les travaux moteur, streaming/HLOD, profils et rendu de Gate 7 sur
  leurs propriétaires communs. La transition des applications ne justifie ni un
  redémarrage de la gate ni une branche/copie parallèle du produit. Les travaux
  bonus et réseau restent hors périmètre.
- Avant de toucher un fichier consommé par une qualification active, identifier
  son inventaire, son binaire et le point de reprise. Sérialiser les mutations
  communes et les builds lourds ; préserver les processus de l'utilisateur. Un
  reçu sur anciennes entrées reste daté et ne qualifie pas le candidat modifié.
- D1/D2 peuvent être préparés sans concurrencer les essais GPU. Avant les gestes
  D3–D6, appliquer les skills UX/UI utiles ; les agents ne sont pas délégués par
  ce plan. Tester les commandes et invariants avant les parcours natifs, puis
  utiliser le cache Cargo partagé et les lanceurs du dépôt pour les builds requis.
- Réemployer la campagne existante `gate7-monde-distribution` et son suivi, avec
  des reçus bornés nommés `editor-launcher-*` lorsque les lots seront exécutés.
  Pas de nouvelle campagne/racine d'évidence ou copie des assets par lot.
  Estimer les besoins avant une production volumineuse ; réutiliser les binaires
  et données immuables, isoler uniquement les tests mutables. Retirer les essais
  remplacés et copies reconstructibles après contrôle des usages ; conserver les
  sources uniques, métadonnées fonctionnelles et preuves utiles selon la politique
  du projet. Publier le bilan résiduel et les volumes au retrait du Hub et à la
  clôture de Gate 7, sans quota arbitraire ni scan F: concurrent.

**Reprise G7-D5.** La reprise après activation de la première révision du
Studio est vérifiée sous Windows. L'installateur a aussi repris après un
échec de copie avant activation en conservant le dossier utilisateur
personnalisé d'une fixture historique. Les installateurs 019 et 021
coexistent avec conservation et réouverture de l'espace auteur par défaut.
L'import et la reprise en jeu de la sauvegarde historique 019 des Trois
Cairns sont vérifiés depuis le launcher autonome Windows (reçu
`G7-D5-windows-launcher-legacy-save-resume-20261106-001.json`). Le nouvel
installateur Studio sait reprendre le paquet privé `reference-assets`
historique et brancher son catalogue sur le lecteur Studio existant. Le
paquet original 019 a été relu intégralement et transféré dans le Sandbox.
La carte Tidewater s'ouvre sans modifier les documents auteur. Après
l'ajout de la description des compagnons végétaux pour les modules gérés
sans origine `.orpmod`, le lecteur commun reçoit ces compagnons de la
vraie carte 019 via le service natif. Les six modèles du catalogue historique
sont raccordés au lecteur commun. Le Studio 027 passe la préparation de
l'environnement historique et des ressources de la carte, puis attend encore
la première image à 170 s. Le Studio 028 raccorde le plugin moteur des
matériaux source et l'initialisation GPU des effets. La première image reste
absente à 182 s sous Windows. Le diagnostic 029 isole le profil de végétation
absent dans le système de préparation du vent des prototypes. Le correctif
030 raccorde le profil de la carte à l'initialisation moteur existante. Sous
Windows Sandbox, le 030 affiche la scène historique et répond à « Rapprocher ».
Le réexport privé échoue actuellement sur les révisions multiples des modèles
du journal. Les six anciennes révisions ont été retrouvées par SHA-256.
Le [suivi D5](../HANDOFF.md) référence les reçus natifs et Windows. Il reste à
transporter les révisions exactes et à qualifier le jeu exporté autonome.
Actualisation du 7 novembre : le Studio 035 a réexporté ce module avec les
modèles actifs et historiques ; le jeu installé a sauvegardé et repris
depuis son propre launcher. Le Studio 036 a ensuite modifié ses trois
visuels depuis l'interface, sauvegardé et réexporté en 1.0.1. La mise à
jour de l'installation 1.0.0 affiche la nouvelle présentation et reprend
la sauvegarde inchangée. Reçus 035 et 036 dans le suivi D5/D4 de
`docs/HANDOFF.md`. Ce dernier invité conserve aussi le Studio et des
montages de qualification : sa seule réussite ne prouve pas encore le
nouveau candidat en environnement joueur seul.
Le 041 qualifie la reprise d'une sauvegarde 019 dans Les Trois Cairns
sur un Windows joueur seul, avec la tête historique reconstruite retirée.
Le 042 couvre le passage du vrai Setup historique au Setup Studio puis la
désinstallation du 019 sous Windows. Le [suivi D5](../HANDOFF.md) référence
la preuve de conservation du profil, des sauvegardes, des modules, des droits
et de la révision d'assets. Le 043 rend la reproduction du Setup avec Hub
explicite dans les deux CLI Python et dans NSIS. Les scripts historiques
restent disponibles pour leurs preuves. Terminer le transfert des
consommateurs recensés dans `editor-launcher-d1-inventory-001.md` avant de
retirer la cible et les recettes historiques.

La preuve 042 couvre le Setup réel avec `reference-assets`. Depuis le 044,
le plan natif transfère aussi `asset-library` quand le Studio déclare ce
paquet optionnel et reprend le profil historique. Le test vérifie la
révision, le descripteur et le contenu après désinstallation de la fixture
historique. Le 045 ajoute le retrait du Studio avec conservation sur place
de `packages/asset-library`. Le profil et la révision sélectionnée sont
raccordés à la réinstallation par le reçu `.forge-studio-library.json`.
Les essais natifs Windows couvrent la reprise après fichier verrouillé et
le refus de réactiver un contenu altéré. Le 046 vérifie les Setup NSIS en
Sandbox avec le paquet Studio 042 inchangé et le CLI courant. Le cycle
préserve `asset-library` et le fichier auteur témoin. Le paquet
`reference-assets` a été transféré au premier Setup mais manque après
réinstallation du Studio une fois le 019 retiré. Le 047 corrige ce cas
avec le même reçu de conservation pour les deux paquets. Le parcours
NSIS en Sandbox réutilise le paquet original de référence 019 et la
bibliothèque témoin. Après retrait du Studio et réinstallation, les deux
révisions, le catalogue de référence et le profil auteur sont vérifiés.
Le 048 retire `apps/forge_hub`, le profil NSIS groupé et les recettes qui
recompilaient le Hub dans le checkout. Les sources exactes et leurs
modifications locales sont dans l'archive ZIP cataloguée du 048. Les
vérifications des installations existantes restent disponibles pour les
anciens artefacts. G7-D6 a produit le candidat Windows réunifié 049.
Les quatre binaires, le kit de jeu et le Setup Studio utilisent le même
inventaire d'entrées. Le Setup a installé et démarré le Studio dans un
Windows Sandbox sans SDK. Le Studio installé a exporté Le Grand Plateau
et Tidewater. Un invité joueur seul a installé les deux exports 049 puis
lancé et repris chaque jeu avec sa propre sauvegarde. Le moteur et le
launcher installés ont les mêmes empreintes dans les deux jeux. Un autre
invité Windows a validé le cycle Tidewater 035 → 049 → 035 → 049 puis
désinstallation. Le jeu a repris la sauvegarde 035 après mise à jour et
après restauration. Les sauvegardes de Tidewater et du Grand Plateau ont
gardé leurs empreintes après le retrait. Les reçus et la portée de ces
parcours sont dans la [passation](../HANDOFF.md#gate-7-etat-courant).
Sous Linux, le build privé 0837 a permis d'installer et d'ouvrir la fenêtre
Studio dans un conteneur Debian 12 sans SDK. Le kit 0837 a servi à installer
et lancer Le Grand Plateau via le diagnostic natif, à créer une sauvegarde
et à en vérifier la conservation après désinstallation. Restent à qualifier
le build des sources courantes, le parcours auteur/export, la reprise via le
launcher, la mise à jour, la restauration et le parcours complet sur OS propre.

Sur le même export Tidewater au profil Moyen, le trajet natif mesuré passe
de 24,53 ms en moyenne avec le runtime distribué du 049 à 20,61 ms après
les premières optimisations, puis à 19,18 ms avec les commandes d'ombres
opaques réutilisées. Le regroupement des contrôles végétaux et des clés
d'inventaire donne 18,89 ms en moyenne et 31,65 ms au p95. Le dernier
relevé sur ce trajet donne 18,85 ms en moyenne et 31,37 ms au p95.
Une seule exécution par variante ne permet pas d'attribuer cet écart.
Le relevé historique Release 012 retrouve les mêmes
empreintes de jeu et de zone. La résolution, le pilote, les réglages et
le scénario diffèrent. Le budget moyen de 16,67 ms et une comparaison
appariée avec le lecteur historique restent ouverts. Le 10 novembre,
deux paires en ordre inverse sur le trajet Tidewater ont mesuré
17,30–17,42 ms avec le runtime 050 contre 22,34–22,71 ms avec le 049
livré. Le véritable lecteur 012 a aussi été rejoué sur le pilote courant
dans son scénario fixe et rotatif à 2560 × 1440. Les reçus et leurs
périmètres figurent dans la [passation](../HANDOFF.md#gate-7-etat-courant).

L'inventaire R7-08
des exports 049 confirme leur portée privée et localise les quatre modèles
Tidewater dont la redistribution est refusée par le catalogue embarqué.
La filiation des quatre GLB est rapprochée jusqu'au pack source CC0. Le
reçu couvre les octets sources et les dérivations archivées. Le service
d'import Studio a repris les quatre modèles avec leurs identifiants et
empreintes dans une collection isolée, puis vérifié l'instantané privé.
Le service d'export natif a ensuite intégré les crédits de la référence
Tidewater à l'export, avec contrôle du reçu et des données du module.
Les droits finaux des dérivés et des autres composants livrés restent à
établir. Le 049 conserve sa réserve historique pour `drain_filter_polyfill`.
Le 050 a été construit à partir du graphe Web corrigé, qui exclut ce paquet.
Le Studio Windows a aussi passé le cycle 042 → 049 → 042 → 049,
désinstallation et réinstallation du 049. Le contrôle vérifie le
paquet Studio, son lancement et les 42 fichiers auteur conservés.
Le reçu d'inventaire du Setup 049 rapproche les composants et notices
livrés. Les réserves d'attribution, de droits et de plateforme sont
consignées dans `docs/HANDOFF.md`.

## Réalisation et ordre

Priorité propriétaire clarifiée le 24 octobre : **un seul moteur commun pour
Tidewater et les autres modules**. Reprendre les mécanismes et optimisations
déjà construits pour Tidewater, les généraliser dans le moteur lorsqu'ils
dépendent encore de la carte, puis brancher tous les consommateurs sur cette
même implémentation. Les modules apportent leurs documents, ressources et
réglages ; ils ne réimplémentent pas ces capacités. Avant toute extension de
résidence physique ou sélection végétale, inventorier l'existant, ses
dépendances et les divergences introduites dans le jeu de modules. Ne pas
poursuivre une variante propre à Gate 7. Le moteur ne dépend pas du composant
de carte ; les données et gardes spécifiques du dataset restent séparées.
La vérification doit comparer la carte de référence et un autre module sur
ce chemin commun, en conservant les exigences de packaging et de qualification.
Cette règle couvre l'ensemble des capacités réutilisables construites pour la
référence, pas uniquement la végétation : terrain, lumière et atmosphère,
effets de rendu, mouvement, physique et interactions doivent être rapprochés
de leurs consommateurs. Pour chaque reprise, identifier l'implémentation
existante, son propriétaire commun, ses données propres à la carte et les
preuves comportementales des deux parcours. Une façade ou un renommage ne
termine pas ce travail. Cela ne crée aucune nouvelle fonction maritime ni
n'ouvre la perspective bonus.

Le 2 novembre, le vent de zone a rejoint les données auteur et le moteur commun
([ADR-0062](../ADR/ADR-0062-shared-scene-wind.md)). Un parcours Studio et un
module exporté l'utilisent localement ; ce dernier reprend encore du contenu
Tidewater. Raccorder les autres effets et le mouvement des objets aux mêmes
propriétaires, puis vérifier une carte entièrement différente et comparer le
rendu et les performances aux mêmes réglages. La qualification complète des
scènes source et de leurs compagnons physiques et particulaires reste à terminer.

Un module synthétique indépendant utilise depuis le 2 novembre le propriétaire
et le cycle communs des scènes source en mode de qualification épinglé, avec
un rendu et un inventaire GPU observés. À ce bilan du 2 novembre, le lancement
normal refusait encore ces scènes ; les compagnons, la famille source non éclairée, les contenus réels et
la comparaison de performance appariée restent à qualifier. Le bilan central
est `module-source-graph-assessment-001.json` ; il ne clôt aucun critère.
État vérifié le 5 novembre : le préflight et la préparation ordinaires acceptent
désormais les scènes déclarées par les mêmes services, sans mode de qualification.
Les compagnons physiques/particulaires exigent les pins réels de zone et de
transport. Les tests `ordinary_installed_launch_routes_source_scenes_through_the_common_host`
et `installed_module_preflight_requires_real_pins_for_physics_and_particle_inputs`
passent dans le lot CPU du runtime 012 ; ils ne démarrent pas le renderer et ne
prouvent donc pas une qualification GPU complète de ces contenus.
L'import direct et les exports privés Vault/kit utilisent à présent le même
validateur déclaré, vérifié aussi sur une enveloppe mutualisée synthétique ;
`source-export-adapters-assessment-001.json` précise les quatre tests Vault réel
ignorés et les limites de ce raccord.

Première reprise du 24 octobre : contrôle des nuages, exposition, spectre de
l'océan, mouvement pendulaire, budget des embruns et cascades d'ombres sont
déplacés dans `forge_engine`, sans réexport depuis le composant de carte.
Leurs fichiers conservent les mêmes octets et notices. La sélection végétale
proche/lointaine et le seuil de déplacement d'un mètre sont partagés avec le
moteur. Référence et modules consomment la même sélection par niveau, avec les
capacités, plages et transitions source ; le mode qui neutralisait les distances
est retiré. L'admission conserve toute la population auteur, indépendamment
du nombre de représentations dessinées. Les barrières GPU et les collisions
restent contrôlées séparément. Cette reprise ne termine pas l'extraction :
plusieurs adapters de rendu restent dans `forge_tidewater`. La parité et la
performance doivent encore être établies par les rejeux natifs, puis par la
qualification complète. Les reçus de cette tranche portent le préfixe central
`shared-engine-` ; le suivi conserve aussi ses échecs et réparations.

Le 27 octobre, [ADR-0055](../ADR/ADR-0055-shared-solar-optical-pass.md) transfère
le passage optique solaire et ses deux shaders à `forge_engine::lens_flare`.
Référence, ancien viewport Studio et sondes indépendantes consomment ce même
propriétaire ; les sources 019 identifient le transfert sans changement de
calcul ni de WGSL. Clippy natif/Web, cinq tests et 25 frontières passent ; le
rejeu GPU retrouve huit images exactes des flare/témoins/AO sur deux scènes,
avec grain variable explicitement hors parité exacte. Le raccord de toute
la chaîne optique au viewport canonique et aux modules demeure exigé.
Le cycle des quatre profils sur le module terrain source réussit avec reprise
des préférences, mais les points lumineux Medium persistent après 600 ticks
et relance : `source-profiles-visual-review-001.json` refuse la qualification
visuelle et conserve R7-05 ouvert. Ces preuves ne remplacent pas la reprise
des passes source de post-traitement et de reconstruction temporelle.

Les sources 020 transfèrent ensuite la projection des bâtiments auteur dans
`forge_engine::authoring_buildings`, directement consommée par Studio et le jeu
de modules, selon [ADR-0056](../ADR/ADR-0056-shared-authored-building-projection.md).
La collecte existante des casters marqués rejoint également le propriétaire
moteur. La comparaison des sources conserve calculs, boucle et assertions ;
Clippy natif/Web, format, 25 frontières et les sept tests moteur de projection,
réconciliation et passage des portes passent. Les deux tests du consommateur
d'ombres de la référence et Clippy du jeu de modules passent également ; les
sources 020 restent inchangées (`shared-buildings-validation-001.json`).
Ce transfert ne qualifie pas à lui seul
une construction dans le Studio ou une image GPU de bâtiment.

Les sources 023 transfèrent ensuite contrat et reconstruction temporels, AO,
grain, masques de coque et helpers de réfraction dans le moteur selon
[ADR-0057](../ADR/ADR-0057-shared-temporal-optical-core.md). Quarante fichiers
sont comparés aux originaux après les seuls changements déclarés de propriétaire
et noms internes ; les UUID sont conservés. Les 2 780 tests principaux,
Clippy natif/Web, format et 25 frontières passent sur ces sources inchangées.
La sonde optique GPU suivante retrouve dix PNG exacts sur deux scènes pour AO,
grain et flare ; l'exécution TAA, les masques et le raccord complet des modules
restent dus. La sélection Nature révèle également que l'inventaire GPU compte
sa caméra ID hors écran comme une seconde caméra de scène ; les sources 026
corrigent ce filtre avec le marqueur commun existant. Neuf tests et Clippy
natif/Web passent, sans transformer ce résultat en parcours Studio réussi.

1. Figer l'inventaire physique des entrées, l'état Git et les processus actifs.
   Réutiliser les contrats, lanceur et installateur existants ; examiner leurs
   limites avant toute nouvelle représentation ou dépendance.
2. Définir par ADR le streaming borné, ses identités et son admission atomique.
   Implémenter une tranche monde réellement consommée par le moteur : cellules
   proches, représentation lointaine HLOD, déchargement et retour sans mutation
   des documents, liens gameplay ou collisions proches.
3. Qualifier un parcours sur une carte plus grande : mémoire, cellules, délais,
   frame times après préparation, transitions aller/retour et erreurs d'entrée.
   Une projection CPU ou un test synthétique ne qualifie pas le rendu natif.
4. Vérifier sauvegardes/reprises multi-zones : module, zone et révision liés,
   progression conservée, absence de confusion entre deux modules, interruption
   d'écriture et sources auteur immuables. Étendre le mécanisme existant si requis.
5. Construire le runtime autonome et le launcher du jeu exporté selon la
   [transition obligatoire](#transition-editeur-launcher), appliquer
   Low/Medium/High/Ultra et agréger les licences depuis les entrées réelles.
   Vérifier admission des packages, droits, absence du Vault/workspace/fournisseur
   IA, refus des packages altérés, installation/mise à jour et désinstallation.
6. Construire et exécuter les contrôles Windows et Linux disponibles ; qualifier
   une machine propre avec les prérequis réellement présents. Un répertoire
   isolé sur le poste de développement n'est pas une machine propre.
7. Rejouer les contrôles affectés sur entrées stables, rapprocher chacun des dix
   critères et les mesures du monde étendu, produire le rapport de clôture si
   tous sont prouvés. Archiver les données froides avec manifeste SHA-256,
   lecture intégrale, contrôle de stabilité et catalogue central.

## HLOD statique — raccord acquis et qualification restante

La hiérarchie de terrain possède son rejeu natif. `mesh_hlod` et
`static_hlod_geometry` préparent des meshes statiques agrégés et réduits.
`static_hlod` consomme maintenant ces sorties dans le jeu de module, sur la
partition admise du terrain. Les placements et bâtiments conservent leurs
sources/collisions ; les parties d'un objet basculent ensemble, après observation
des ressources GPU. Le rejeu `static-hlod-native-002/analysis-001.json` observe
détail → agrégation → détail, après les 60 ticks de préparation du plan ; le
refus brut portant sur les deux ticks initiaux de gravité reste conservé.

Le raccord implémenté et ses tests couvrent les exigences suivantes :

- enregistrer les primitives admises des placements et bâtiments avec leurs
  identités, pins, poses, matériaux et layouts ; conserver les racines auteur
  et les collisions existantes ;
- réutiliser les clés et la sélection courante du streaming terrain pour les
  groupes statiques, au lieu de créer une seconde grille canonique. Une boîte
  d'objet touchant une cellule détaillée force toutes ses parties au détail,
  y compris lorsque son origine est plus loin. Les limites de groupe ne
  découpent pas un objet ou son identité ;
- préparer des groupes hiérarchiques dérivés, avec une seule préparation bornée,
  leurs sources épinglées et les anciens groupes comptés durant le staging ;
- garder les originaux jusqu'à observation des ressources GPU réellement
  préparées, publier une sélection complète et restaurer le détail lors d'un
  refus, d'une source remplacée ou d'une ressource retirée ;
- lorsqu'un ancien groupe doit être scindé, restaurer toutes ses primitives
  originales pendant la préparation des nouveaux groupes. Les sous-groupes de
  matériaux ne masquent pas des parties d'un même objet avant acquittement de
  l'ensemble. Le cache source conservé pour cette restauration doit rester
  nommé et mesuré, sans être présenté comme de la mémoire déchargée ;
- observer l'aller/retour et le retrait des groupes : le cas natif minimal
  conserve 120 triangles, et prouve une substitution agrégée, avec retour des
  dix primitives originales ; il ne prouve pas de gain de rendu.

Le plan de qualification prévoyait de :

- rendre une scène plus dense et des surfaces courbes/texturées effectivement
  réduites, avec erreurs de géométrie/attributs, profils appliqués et revue
  native. Le cas `dense-hlod-native-001` établit une réduction de 8,99 % sur
  deux sphères texturées, une restauration complète et des différences de
  deux pixels au lointain. Il mesure les intervalles et la mémoire, sans gain
  de frame time ni économie mémoire établis. Étendre cette qualification au
  candidat complet et aux profils ; ne pas transformer les nombres de meshes
  en draw calls ;
- raccorder la végétation aux catalogues, LOD, collisions et déformations
  gouvernés existants. Le refus explicite de végétation raster dans le module
  reste en place jusqu'à présence de son véritable adapter ; le retirer seul
  ne fournirait aucune projection. `imported_vegetation::streaming` prépare
  maintenant une sélection empruntée des instances auteur sur la partition
  admise, avec sources de collision indépendantes. Les prototypes normalisés
  calculent leurs enveloppes depuis les trois LOD. Raccorder ensuite les
  catalogues/prototypes réellement épinglés dans le package, leurs paramètres
  de déformation et la physique ; observer les admissions GPU, l'aller/retour
  et les profils sur la végétation rendue avant toute qualification générale.

Deux chemins existants sont à conserver lors de ce raccord : les espèces
`LayerSpecies` épinglent un `VegetationPrototypeCatalog` avec normalisation et
trois LOD GLB ; les blocs `ImportedVegetationSource` conservent leurs clés de
prototype et paramètres de source, dont certains requièrent les ressources et
shaders du package de plantes importées. Un GLB standard ne remplace pas ce
second contrat. L'export doit fermer leurs références par un inventaire de
prototypes, dépendances, paramètres, droits et hashes vérifiés ; l'hôte doit
utiliser les octets embarqués, sans chercher un catalogue dans le Vault ou le
workspace. Les gardes spécialisés du dataset Tidewater ne deviennent pas une
condition liée à une carte nommée dans le moteur générique. Les nouvelles
sélections ne doivent être publiées qu'après préparation complète, preuve GPU
et concordance physique, avec retour sûr aux sources en cas de refus.

Le transport API 3 et `LoadedModuleZone::vegetation_species` résolvent désormais
les métadonnées d'espèces depuis les fichiers installés. Le raccord natif lit
maintenant les GLB dans des tampons vérifiés et utilise la source mémoire du
chargeur existant. Le rejeu privé `plant-host-native-001/analysis-001.json`
observe leur chargement, leurs ressources GPU, les bindings Rapier et un court
aller/retour, avec trois PNG originaux inspectés. Cette route concerne les
espèces gouvernées sur terrain procédural ; le refus raster reste actif.
Le plugin de végétation accepte désormais la tête auteur exacte ; le jeu lui
transmet chemins, rivières, bâtiments et promotions dès sa préparation initiale.
Les bindings de qualité conservent les mêmes sources lors des transitions.
Charger le bon modèle sur une population procédurale qui ignore ces retouches
ne suffirait pas. Les barrières du jeu et du scénario de qualification couvrent
désormais cette projection. Restent notamment l'animation native des déformations,
les transitions et l'agrégation lointaine, les profils, le raster et le candidat
complet ; le cas privé ne qualifie pas l'export Studio des espèces.

Le bootstrap conserve déjà les octets d'origine et les payloads ; son refus des
nouveaux compagnons reste nécessaire tant que le Studio ne les résout pas depuis
cette autorité et ne sait pas restaurer les catalogues/modèles historiques pour
undo/redo. Cette restauration reste distincte de la fermeture des blobs de
terrain d'ADR-0039 et de la résolution des métadonnées de la tête courante.

Le raccord raster doit réutiliser les règles source sans déplacer leurs gardes
spécifiques dans le moteur générique. L'inspection confirme que
`forge_tidewater_assets::tidewater_plants::PlantPackage` ferme déjà les ressources
par hash et taille, mais admet un commit, six groupes, des dimensions de textures
et un total d'instances précis. `tidewater_plants::canonical::prepare` conserve
également la garde de 65 383 instances. Ces contrats de qualification du dataset
doivent rester intacts. Ils ne constituent pas un décodeur universel de l'index
`ImportedPrototypes` d'API 3.

La prochaine tranche raster doit séparer la fermeture et le décodage des
prototypes de la population canonique, puis fournir à un adapter générique les
meshes, attributs source, textures, paramètres et shaders réellement épinglés.
La sélection `imported_vegetation::streaming` doit alimenter les projections
sur les clés admises du terrain, avec correspondance des identités, paramètres,
collisions et groupes. Les niveaux source ne doivent pas être remplacés par un
GLB ou un troisième modèle inventé lorsqu'un prototype possède deux niveaux.
Les bindings de lumière, de mouvement et d'ombres du shader source doivent
rester explicitement fermés. Observer ensuite admission GPU, distance,
déformations, restauration, physique et coûts sur le candidat raster, avant
de retirer son refus dans le jeu de module.

La première séparation de géométrie est raccordée au chemin de rendu existant :
`forge_assets::source_vegetation_geometry` décode positions, normales, UV,
attributs de végétation/matériau/lobe et indices sans Bevy ou nombre de groupes
imposé. L'adapter de plantes, également consommé par l'herbe, construit ses
meshes depuis ce résultat. Les valeurs et l'absence des attributs source restent
distinctes ; les valeurs par défaut historiques restent dans l'adapter Bevy.
Les descriptions de package, paramètres de population, gardes du dataset et
shaders restent inchangés. Cette séparation ne ferme pas à elle seule l'index
installé, son renderer et ses ressources GPU ; le refus raster du module reste
donc actif jusqu'au raccord complet.

Le décodage réel des onze niveaux est vérifié par `verify_plant_geometry` sur
le package original (pin du manifeste
`1fec8a063214ad05ceac19f277dbd17e131e04d2714378bae9239efd12224f83`).
Les 84 fichiers ont été restaurés avec vérification complète de leur archive,
et les streams décodés sont identiques octet pour octet à leurs sources.
Le lecteur est désormais dans `forge_assets::source_vegetation_package`,
réexporté par son ancienne façade, sans dépendance vers le monde ou le renderer
de carte. Le résolveur de livraison ferme l'index et les ressources de chaque
zone et rapproche clés de prototypes, ABI des paramètres et tête auteur. Le
module privé `plant-import-private-001.orpmod` embarque les vrais octets et
douze placements synthétiques sur la régression existante ; son lecteur
installé vérifie les six groupes, huit niveaux de plantes et trois d'herbe.
Ce cas reste privé et ne qualifie pas l'export Studio ou le rendu importé.

Réutiliser le module installé et son sous-répertoire `plants/imported` indiqués
par `plant-import-private-001.json` dans la campagne centrale. Les copies de
fabrication, l'archive temporaire sur E: et la restauration source initiale
ont été retirées après comparaison complète ; l'archive de preuve unique sur F:
et le cache installé conservent leurs octets. Ne pas restaurer une autre copie
du package pour la prochaine tranche sans besoin de données mutables isolées.

Le prochain raccord natif doit consommer ces géométries, paramètres, textures et
shader avec la population canonique et ses retouches, sur la partition terrain
déjà admise. Les gardes du dataset source restent dans son adapter, sans
condition de carte nommée dans le moteur. La projection, les collisions et
les barrières GPU précèdent la levée du refus de `streamed::prepare` et du
jeu importé ; la seule admission du package ne doit pas lever ces refus.

Le raccord dérivé en cours utilise `tidewater_plants::scene::streamed` : il
prépare la population auteur exacte, conserve ses identités et l'ordre source,
puis consomme la sélection de la partition terrain admise. Le choix d'un LOD
réutilise le dernier modèle réel lorsqu'un prototype ne possède pas de
troisième niveau. Les gardes de qualification du dataset restent inchangés.
Une barrière moteur `render_inventory` inventorie les meshes, matériaux,
textures et buffers de stockage réellement préparés, avec vue et fence GPU.
Elle ne prouve ni pixels, ni physique, ni performances. Les tests de cette
tranche précèdent le contexte de scène natif et les collisions ; le refus du
jeu importé et celui de végétation raster sont conservés pendant ce raccord.

Le code dérivé et ses régressions sont rapprochés dans
`plant-render-validation-001.json` : 1 254 tests distincts, Clippy strict natif,
Studio WASM/WebGPU, format et stabilité des entrées. Le shader source possède
des refus de distance et un éclaircissement internes ; le mode explicite de
sélection par cellules neutralise uniquement ces contrôles, avec conservation
des expressions de référence. La fixture source épinglée vérifie le staging
CPU, sans preuve GPU. Avant le lancement natif, raccorder les enveloppes
réelles de modèles/déformation, le contexte de scène et les collisions, puis
qualifier couverture, restauration, profils et coûts. Aucun refus du jeu n'est
levé par ces tests de préparation.

La brique physique `physics::imported_vegetation` est maintenant vérifiée
par `plant-physics-validation-002.json` : 1 112 tests moteur/rendu/jeu,
Clippy natif/WASM et stabilité des entrées. Les formes possédées et leurs
bindings Rapier survivent aux changements de LOD ; le vrai KCC est arrêté
par le proxy redimensionné et peut le contourner. Les enveloppes des corps
mobiles sont partagées avec le terrain. Les sous-dimensionnements numériques,
formes dégénérées et demandes de rôles non encore synchronisées ferment la
barrière avant publication/simulation. Le jeu n'utilise pas encore cette
brique : installer son résident sur la sélection auteur exacte, attendre
`matches`/`ready` avec l'admission GPU, puis recontrôler après les actuateurs.
Les entrées de lumière, ciel/environnement, ombres terrain/objets et mouvement
du shader doivent provenir d'un contexte de scène fermé et effectif ; le
lecteur des 83 ressources de prototypes ne fournit pas à lui seul ce contexte.
Observer ensuite le rendu et le parcours réels avant de lever les gardes.

Le matériau du monde joué est maintenant vérifié isolément par
`plant-native-validation-001.json` : 461 tests, contrôles stricts natif/WASM,
et `plant-native-gpu-007` sur le vrai GPU. `world::WorldPlantMaterial` utilise
les surfaces source avec les lumières, environnement et ombres Bevy ; son
buffer de trame courant/précédent fournit les déformations des prépasses.
Les trois types de plantes ont leurs pixels attribués séparément et leurs
pipelines visible/normale/mouvement/ombres compilés. Ce cas ne publie pas de
sélection gameplay et ne remplace pas la qualification du candidat complet.
La fixture de transport historique a un stretch de canopée nul, désormais
refusé avant allocation par ce matériau. Le rejeu GPU corrige douze paramètres
et crée un raster seulement en mémoire, sous une tête de fixture distincte.
Ne pas utiliser cette correction de test comme fallback du runtime.

La suite raccorde ce matériau au contexte réel et au vent borné du jeu, après
calcul des enveloppes depuis les géométries et les déformations du profil
source. La même sélection canonique alimente ses résidents de rendu et de
physique ; l'hôte attend leur concordance et une fence couvrant la publication,
puis rejoue la barrière après les actuateurs. Qualifier ensuite le vrai
parcours raster, sa restauration, ses profils et ses coûts.

Le calcul d'enveloppes est maintenant vérifié dans
`plant-bounds-validation-001.json` : 469 tests, contrôles stricts natif/WASM et
rejeu GPU avec les dix profils source. `Prepared::world_envelopes` dérive ses
rayons/mouvements des meshes et matrices préparés, sur toute la plage de vent
source 0 à 25 ; `WorldEnvelopes::select` ferme les poses f64 et le document
végétal sur la partition admise. Les ressources des dix profils sont préparées,
avec pixels isolés des trois types de shader et fences après capture ; il ne
s'agit pas d'une lecture des positions GPU ou du parcours complet. Le jeu doit
encore installer le matériau et sa trame sur son contexte réel, conserver ces
enveloppes pour la même tête, publier une sélection partagée avec la physique
et attendre leurs deux admissions. Le rendu de référence et les refus actuels
du jeu restent conservés pendant ce raccord.

Ces travaux restent dans Gate 7 et ne valent ni qualification générale HLOD ni
clôture d'un critère sur les seuls tests de préparation.

Le raccord `plant-game-*` installe maintenant un hôte de plantes sur raster,
lié par type à la tête et au terrain préparés. Sa vue auteur dérivée immuable
évite une reprojection à chaque tick ; une sélection sur la partition terrain
alimente rendu et collisions. La disponibilité est vérifiée avant les actuateurs
et à nouveau avant la physique, sans lever les suspensions des autres adapters.
Le jeu n'admet ce chemin que via cet hôte ; les espèces procédurales et les
refus sans hôte restent conservés. Le contrat de preuve du jeu rapproche les
identités, comptes, LOD et admissions ; ses DTO restent utilisables par les
tests de livraison indépendamment du renderer.

Les régressions `plant-game-regressions-001` passent (404 bibliothèque,
67 jeu, 30 ignorés existants). Le nouveau module privé `plant-game-world-001`
porte douze arbres synthétiques sur le raster de 2 km, avec des paramètres
de canopée explicitement valides et les vrais prototypes installés. Son archive
et son cache sont vérifiés ; les copies de fabrication demeurent après refus
du nettoyage par le contrôle automatique. La qualification du parcours rendu,
du retour, des collisions dans ce jeu et des coûts reste à rapprocher, ainsi
que les contrôles finaux sur les mêmes entrées. Ce raccord ne ferme pas les
profils, le candidat complet ou l'export Studio des compagnons.

Le rapprochement final `plant-game-validation-001.json` passe sur les mêmes
entrées 009 : contrôles natif/livraison/WASM, formats et 669 exécutions de tests.
Le rejeu privé 002 observe réellement détail → groupe → détail des douze arbres
sur 160 m et retour, avec cinq publications et admissions GPU/Rapier/trame. Le
rejeu 003 et son analyse rapprochent les contacts normaux du vrai KCC avec un
tronc auteur, le glissement tangent, le contournement et le retour. Les refus
initiaux des protocoles restent intacts. Les cinq PNG retenus sont inspectés.
Le budget étendu de 160 Mio / 224 fichiers contient les deux modules autonomes
et 111 fichiers de preuves hors rapport. Les copies de fabrication restent
présentes après refus du contrôle automatique de nettoyage.

La suite porte désormais sur les profils du jeu, le candidat raster complet,
ses dix profils source joués et ses coûts. Le cas à douze arbres ne qualifie
pas ces ensembles, ni l'export Studio, les sauvegardes multi-zones ou la
distribution sur OS propre. Les dix critères restent ouverts.

Le premier raccord des profils au raster est vérifié séparément dans
`cell-quality-validation-001.json` : 851 tests, contrôles stricts natif/WASM,
format et entrées stables. Deux strides renderer sont préparés par transaction,
sans réduction des collisions proches ni effacement d'une édition auteur en
attente. L'API expose la finesse CPU admise ; l'hôte doit toujours vérifier ses
ressources et fences GPU. Le diagnostic natif observe les quatre budgets de
la table et le retour au défaut : 280 meshes préparés à chaque capture,
changements réels des triangles/pixels et cinq fences couvrant leurs captures.
High/Ultra partagent ici les mêmes strides ; aucun gain de frame time, draw
calls ou mémoire GPU n'est déduit des compteurs de buffers CPU.

La suite compose ces réglages avec les autres domaines effectivement présents
dans le module, conserve les capacités/refus explicites, puis raccorde menu,
préférences versionnées, écriture atomique et relecture au lancement. Réutiliser
le module privé installé et le cache commun pour vérifier application et refus
sur Low/Medium/High/Ultra, puis qualifier le candidat complet. Ne pas ajouter de
ressources fictives pour acquitter le contrôleur global historique, ni transformer
la preuve du seul raster en profil global appliqué. Les dix critères restent ouverts.

## Profils du jeu — raccord natif vérifié, candidat complet restant

ADR-0041 et les rejeux `module-quality-native-006` à `009`, sur les entrées 007,
vérifient les quatre profils du module raster privé, leur application effective,
le menu F2, la persistance, un nouveau processus relisant Ultra sans commande
et le refus/rétablissement d'une préférence invalide. Deux inventaires GPU
supplémentaires (terrain/modèles) coexistent avec le reçu principal des plantes.
Élevé/Ultra utilisent la reconstruction temporelle et ses prérequis observés,
avec les vrais vecteurs des déformations précédentes des plantes. L'inspection
de onze captures et la mesure ciblée de `module-quality-visual-003.json`
constatent la correction du bruit de contact découvert dans le premier essai.

La politique ne représente que les domaines réellement installés. Les champs
auteur, populations, collisions et niveaux source sont conservés. Le cas privé
ne qualifie pas le candidat raster dense, ses dix profils source joués, tous
ses domaines d'affichage ou ses performances. Les contrôles finaux et le
rapprochement de cette tranche sont consignés dans le suivi ; R7-05 et les neuf
autres critères restent ouverts jusqu'à leur qualification complète.

Le relevé long est maintenant vérifié séparément : `world-traversal-native-001`
rejoue 160 m aller/retour avec la reconstruction temporelle. Les 11 193
intervalles restent sous 8 Mio grâce au format v2, sans sous-échantillonnage des
intervalles ou réétiquetage des fences ; les instantanés et leurs quotas sont
explicites. Le lecteur conserve la compatibilité avec les relevés v1. Les
preuves de cette fixture ne ferment pas le candidat dense.

Ses sources complètes sont désormais récupérées et vérifiées sur E: :
`world-candidate-source-001.json`, terrain 2 km / 2 048² échantillons et 65 383
plantes dans dix profils source, avec dix blocs immuables et l'archive auteur
initiale. Réutiliser ces 15 fichiers et le package de prototypes installé. La
prochaine composition doit conserver le contenu RPG, les placements/bâtiments,
les identités du candidat et des collisions cohérentes avec le véritable relief ;
elle ne doit pas hériter aveuglément des poses à altitude zéro de la régression
plate. Produire ensuite le package autonome fermé, observer tous les profils
joués, les transitions et les coûts avant de conclure sur R7-01/02/04/05.

La composition complète passe l'admission du package privé 1.0.4
(`world-candidate-compose-003`) et son premier rendu est observé. Le parcours
`world-candidate-native-004` est interrompu proprement pour performances
insuffisantes : 1 359 ms de moyenne sur les intervalles prêts instrumentés,
toute la population encore sélectionnée et environ 18 millions de triangles.
La priorité retenue à ce stade était de mesurer puis corriger ce surcoût sur ce
candidat, puis de comparer à Tidewater dans des conditions identiques. Ne pas qualifier
les seules petites fixtures ou réduire la population auteur pour obtenir un
succès. La nouvelle seed relocalise les identités RPG sans importer leurs
timelines historiques ; l'export Studio et la restauration des compagnons
restent à qualifier séparément.

Le premier correctif CPU conserve les contrôles complets après modification,
détecte les changements/suppressions via les ticks et curseurs et continue
d'acquérir les fences GPU à chaque contrôle. 490 tests, contrôles stricts
natif/WASM et formats passent sur les entrées 006. Le diagnostic stationnaire
identique 005/006 ramène le contrôle du rendu de 17 à 0,44 ms/appel et les
intervalles prêts de 1 338 à 678 ms : le résultat restait insuffisant. La
résidence physique et la sélection de végétation ont depuis été bornées dans le
moteur commun : le parcours ultérieur `world-candidate-native-028` relève
13,30 ms en moyenne et 25,19 ms au 95e percentile sur les images prêtes, avec
23 à 62 formes résidentes pour 13 434 sources. Il s'agit d'un essai daté,
instrumenté, sans comparaison appariée à Tidewater ni qualification du candidat
actuel. Les mesures récentes de référence utilisent notamment une autre
résolution, un autre binaire et une autre scène ; leurs FPS ne sont pas
comparables à ceux de cet essai. Pour établir la parité, rejouer la même scène
et le même trajet en référence puis comme module avec résolution, profil,
synchronisation verticale, préchauffage et durée identiques, en alternant
référence → module → module → référence sur plusieurs répétitions. Épingler les
binaires et contenus, rapporter aussi les images non prêtes et mesurer sans
profilage ; utiliser ensuite les traces CPU séparément pour localiser les coûts.
Rejouer enfin sur une carte indépendante. Préserver les données auteur, les identités, les déformations, la
préparation GPU/backend et les retours sûrs ; requalifier le produit final et
comparer les performances à la référence dans les mêmes conditions.

## Preuves et ressources

Campagne stable : `E:/OpenRPG_Forge/Preuves-et-analyses/actives/gate7-monde-distribution/`.
Sources/builds/temp sur E: dans `.build/`, sans cache ou binaire par conversation.
La règle propriétaire actualisée remplace le budget initial par le contrôle
des usages : toute copie requise est estimée avant production, réutilisée tant
qu'elle sert à la qualification ou à sa reprise, puis retirée quand elle est
remplacée et reconstructible depuis les sources/outils conservés. Aucun quota
arbitraire ne justifie conservation ou retrait. Ne pas recopier les anciennes campagnes.
Une seule opération de stockage sur le HDD F: à la fois ; aucun parcours de
jonction lors d'un inventaire. Les données de test mutables sont des copies.

## Verdict

Les résultats de domaine, admission, rendu, parcours utilisateur, performance,
construction et qualification d'OS sont distingués dans
[le suivi](../evidence/gate7/GATE7_PROGRESS_20261023.md).
Les critères ne changent pas pour adapter le verdict aux moyens disponibles.
En cas de condition externe, terminer les travaux indépendants et conserver
l'exigence non satisfaite avec le moyen concret de la vérifier.
L'ouverture ne demande ni commit/push, publication, Gate 8 ou chantier maritime.


## Mesures communes et priorité physique — 25 octobre 2026

Le CPU/rendu est mesuré avec les mécanismes repris de la référence dans
`forge_engine::cpu_timing` et `forge_engine::render_timing`. Le profil natif
s'active avant lancement avec `FORGE_RUNTIME_CPU_PROFILE=1`. Les anciens noms
propres à l'essai et leurs reçus restent historiques. L'outil de référence et
le jeu de modules utilisent les mêmes horloges ; les contrôles de disponibilité,
la présentation et les données auteur ne sont pas modifiés par ces mesures.

`shared-timing-validation-001.json` et `shared-timing-diagnostic-001.json`
constatent un coût dominant de boucle fixe : 35,74 ms/pas, 527 pas pour 39
mesures principales terminées. Les scopes physiques végétaux vérifient encore
13 434 proxies, avec 5,90 ms par contrôle. Les diagnostics GPU publiés observés
sont de 4,51 à 7,75 ms et conservent leur âge ; ils ne sont pas des fences de
l'image courante. Les scopes sont imbriqués, asynchrones et non additifs.

La prochaine correction doit étendre le plugin physique commun existant :
conserver toute la préparation canonique et les identités, puis ne projeter
dans ECS/Rapier que les formes requises par les enveloppes conservatrices des
corps et de leurs mouvements prédits. Reprendre le prédicat d'activation actuel,
sans inventer une grille de carte. Distinguer sources canoniques et proxies
résidents dans les observations. Exiger l'admission réelle des nouvelles formes
et le retrait effectif des anciennes avant disponibilité, conserver la détection
des mutations/erreurs de propriété, puis qualifier contact et aller/retour.
Le relevé du coût et le candidat complet restent requis après cette correction.
Tous les critères de Gate 7 restent ouverts.

## Raccordement restant au moteur commun — 25 octobre 2026

La séparation des dépendances ne suffit pas à la consigne propriétaire :
tout mécanisme réutilisable construit pour la référence doit appartenir au
moteur et servir aux modules qui déclarent cette capacité. Le contenu,
les réglages, les pins de ressources et gardes du dataset restent dans leur
intégration. Les ports réutilisables ne doivent pas demeurer une réserve de
fonctions dans la crate d'une carte.

La revue initiale constate notamment que `tidewater_plants::scene::Vegetation`,
`scene::resident`, `scene::streamed` et `world::{SourceMaterial, Frame}`
portent encore de l'exécution réutilisable dans `forge_tidewater`. Le sélecteur
source et les horloges déjà communs ne clôturent pas cette extraction. Préparer
des entrées validées avec les formats existants, transférer l'exécution vers
le sous-système moteur concerné et raccorder les deux consommateurs au même
chemin. Conserver côté carte les contrôles de population de référence, les
acquisitions et la provenance ; ne pas créer de dépendance du moteur vers
`forge_tidewater_assets`. Préserver les notices avec les ports repris.

La résidence physique bornée est une extension du plugin moteur existant,
avec les mêmes enveloppes et le même prédicat d'activation. Sa validation
reste distincte de ce raccordement du rendu, de la comparaison à la référence,
des performances release et de la clôture des critères de Gate 7.

Validation du 25 octobre : `physical-residency-validation-001.json` rapproche
les contrôles, entrées et deux essais natifs. Le scénario stationnaire
012/013 conserve le plan et toute la projection attendue : 13 434 sources,
23 formes résidentes, intervalle prêt moyen 576,55 → 19,01 ms. L'aller-retour
014 termine 17 étapes avec 139 observations GPU datées, 23 à 62 formes et une
trace non tronquée. Les images non prêtes sont conservées. La boucle fixe
observée sur ce parcours coûte 8,95 ms/pas ; le contrôle physique 0,67 ms/appel.
Ces moyennes ne sont pas un benchmark release : durées et mouvements diffèrent
entre le diagnostic CPU stationnaire et ce parcours. La reprise restante du
rendu commun, les profils et le protocole de monde étendu restent exigés.

## Transfert du noyau de rendu végétal — 25 octobre 2026

La reprise place les layouts `Record`/`Params`, les attributs, la frame
courante/précédente et `WorldPlantMaterial` dans
`forge_engine::source_vegetation`. Son `scene::resident` reprend la sélection,
les buffers/modèles partagés, les entités survivantes, les identités et le
snapshot de draws ; l'adapter fournit son matériau ID. Les deux anciens
fichiers de résident sont retirés. Studio, la référence et le résident de
module appellent directement ce code, sans réexport depuis la crate de carte.
Les constructeurs admettent des groupes indépendants du dataset et valident
les entrées dérivées avant allocation ; ils ne créent aucun document canonique.

Les schémas, gardes six groupes / 65 383 sources, acquisitions et pins restent
dans les chargeurs. Les anciens types et gardes ne sont pas copiés dans le
moteur. Les dispositions GPU et UUID de shaders sont conservés ; les noms
d'attributs de diagnostic deviennent neutres, avec les mêmes IDs et formats.
Les tests natifs/WASM, tests de disposition/retour/refus et huit contrôles
structurels sont rapprochés dans la campagne centrale. Le test d'atomicité
compare le nombre d'entités avant/après, en tenant compte des entités internes
initialisées par Bevy ; l'échec initial reste conservé.

Le bilan `shared-plants-validation-001.json` confirme 704 tests moteur / deux
ignorés, 381 tests de bibliothèque / trente ignorés, 83 tests d'exemple et un
test explicite de shader épinglé, ainsi que Clippy natif/WebGPU. Les entrées
006 restent inchangées après les natifs 015/016. Ils conservent les plans et
projections attendues de 013/014 ; 016 termine 17 étapes, 133 observations GPU
datées et une trace non tronquée. Les moyennes des intervalles prêts sont
18,09 ms au camp et 13,48 ms sur le parcours, sans verdict de performance release.
Ce résultat valide le transfert du noyau et son usage par le module complet,
pas une nouvelle qualification GPU de la référence complète ou du Studio.

## Transfert de l'hôte végétal et de sa préparation — 25 octobre 2026

La tranche suivante transfère les mécanismes réutilisables de `scene::streamed`
(préparation, bornes, admission/publication et hôte) dans le moteur. La
préparation canonique, les textures et la dérivation du shader sous éclairage
monde ont le même propriétaire. Les anciens fichiers de la carte sont retirés.
Le packing original et la description du profil sont dans le codec commun
des ressources : arithmétique f64/f32, ordre source, schémas, identifiants,
licences et pins sont conservés ; les gardes du dataset ne sont pas des
conditions du moteur liées à l'identité d'un module.

Le Studio appelle `source_vegetation::scene::from_population`, fondé sur le
même `streamed::Prepared` que le jeu. Un test avec quatre instances couvre une
population auteur indépendante de la taille de l'île. La préparation CPU ne
vaut pas admission GPU ; les fences réelles, retraits et acquittements Rapier
restent exigés. La référence conserve son chargeur de dataset et emploie le
résident commun. La publication du module conserve son identité dérivée v2.

Les contrôles et rejeux de cette tranche portent le préfixe central
`shared-streaming-`. Le bilan `shared-streaming-validation-001.json` rapproche
200 tests assets, 732 tests moteur / deux ignorés, 355 tests de bibliothèque
de carte / trente ignorés, 83 tests d'exemple, le shader épinglé, neuf contrôles
d'architecture, Clippy natif/WebGPU et formatage réussis. Les natifs 017/018
passent sur les mêmes plans, package et projection attendue que 015/016 :
018 termine 17 étapes avec 137 observations GPU datées et 23 à 62 formes
résidentes sur 13 434 sources. Les intervalles prêts moyens sont de 18,12 ms
au camp et 13,30 ms sur le parcours, sans gain statistique ou FPS global
qualifié déduit. Les 2 417 entrées restent stables après exécution.
Le matériau sous éclairage capturé et les ports encore couplés de grass,
ombres et picking
restent à transférer et qualifier. Le terrain raster et la référence CDLOD
restent distincts. Les qualifications générales de Gate 7 restent ouvertes ;
aucun verdict global n'est déduit de la séparation des fichiers.

## Matériau capturé et récepteurs communs — 25 octobre 2026

Le matériau et son staging appartiennent maintenant à
`source_vegetation::captured::CapturedPlantMaterial`, le vent à
`source_vegetation::motion`, la lumière directe/SH et ses buffers à
`source_lighting`, et les réglages/récepteurs d'ombres à `source_shadows`.
Les appels de Studio, de la référence et des autres matériaux sont directs.
Sept anciens fichiers de carte sont retirés. Les tailles 176/48/448/464 octets,
les quinze dépendances du matériau capturé, les UUID et bindings sont conservés.
Les paramètres d'encodage restent fournis par le graphe de profondeur existant.

Le bilan `shared-lighting-validation-001.json` rapproche formatage, Clippy
natif/WebGPU, 748 tests moteur / trois ignorés, 339 tests de bibliothèque de
carte / vingt-neuf ignorés, 83 tests d'exemple, le shader épinglé et dix
contrôles d'architecture. Les natifs 019/020 passent et conservent les plans,
package et projection attendue de 017/018 ; 020 termine les 17 étapes avec
138 observations GPU datées et une trace non tronquée. Les 2 420 entrées /
282 764 134 octets restent inchangés après les essais.

Le contrôle de référence reste **non qualifié visuellement**. Le premier mode
scène refusait un ensemble réduit ; l'ensemble étendu reprend ensuite terrain
et village. Son shader brut nécessitait le préparateur existant
`prepare-village-lighting-uniform.py`, appliqué à une entrée isolée avec sources
inchangées. Après préparation, les processus 004/005 réussissent, mais leurs
images sont noires : 9 216 points échantillonnés sont noirs, et l'inspection
visuelle confirme le défaut. Le réglage statique du parcours jouable ne le
corrige pas. Les anciens contrôles du pilote, limités au code de sortie et au
PNG, sont insuffisants ; leur revendication de shader exécuté n'est pas admise.

Priorité suivante : observer les valeurs et la propriété du buffer de lumière
commun, puis les entrées de rendu/caméra et la capture pour attribuer et corriger
l'image noire. Ne pas la traiter comme un simple refus esthétique et ne pas
affaiblir l'admission. La validation GPU de l'éclairage capturé, le graphe de
profondeur, grass, picking et les autres qualifications générales restent ouverts.

## Capture noire — correction du récepteur commun, 25 octobre 2026

Le bilan `lighting-diagnostic-validation-001.json` résout la réserve de capture
noire sur l'ensemble de référence réduit, sans changer ses ressources, sa caméra
ou sa lumière. Le reçu natif expose les valeurs CPU, l'état de propriété et la
caméra réelle. Un readback optionnel du buffer commun confirme les mêmes 176
octets sur le GPU ; il refuse de recréer une allocation déjà possédée par un
publisher GPU et ne vaut pas fence de draw.

Les images noires sont désormais refusées par contrôle du contenu. Le mode
silhouette existant montre la géométrie, sans qualifier l'éclairage. Le shader
des nuages calculait ses coordonnées avec une étendue nulle avant de mélanger
son résultat avec une force nulle. La garde commune retourne une transmission
neutre avant tout échantillonnage lorsque la force est exactement zéro ; les
forces positives suivent le corps source inchangé. Plantes, terrain et village
utilisent ce même récepteur moteur.

Le contrôle avec readback échoue visuellement avant la garde et passe après,
avec les mêmes valeurs CPU/GPU et matrices de caméra. Le rejeu normal, sans
readback, passe également. L'ensemble exclut toujours grass, ciel et eau :
aucune parité visuelle générale ou acceptation esthétique n'est déduite.
Les natifs 021/022 du module passent sur les plans et projections attendues de
019/020 ; 022 termine 17 étapes et 140 observations GPU datées. Les 752 tests
moteur / trois ignorés, 339 tests de carte / vingt-neuf ignorés, 83 tests
d'exemple, shader épinglé, Clippy natif/WebGPU, formatage et dix contrôles
d'architecture passent. Les 2 422 fichiers / 282 777 352 octets restent stables.
Poursuivre la reprise et qualification des ports restants et les critères généraux.

## Herbe et sélection de terrain communes — 25 octobre 2026

Le sélecteur CDLOD et ses bornes raster sont transférés dans
`forge_world::terrain_lod` et `terrain_lod_bounds`, sans façade dans la crate
Tidewater. Le corps du sélecteur est inchangé, hors documentation de module ;
celui des bornes est inchangé, hors imports. Leurs tests changent de propriétaire.
Terrain, océan, ombres, Studio et outils source appellent ces mêmes fonctions.
Le dessin raster de cellules et le dessin CDLOD restent des adapters distincts
à rapprocher et qualifier ; cette étape ne prouve pas leur parité.

`forge_engine::source_vegetation::grass` reçoit la sélection source et le
résident de buffers/entités. Les gardes de schéma et le décodage du paquet sont
repris dans le codec `forge_assets::source_vegetation_package::grass`.
Les 256² cellules, leur ordre z/x, les trois capacités 48/160/420 et les
bindings de matrices identité sont conservés. La capture statique retire son
ancienne création séparée de draws et emploie le résident commun, comme la
marche. Les ressources et les 64 fichiers WGSL du dépôt restent inchangés.

Le bilan central `shared-grass-validation-001.json` rapproche 512 tests de
domaine/ressources, 759 tests moteur / trois ignorés, 332 tests de carte /
vingt-neuf ignorés, 83 tests d'exemple, un test de shader épinglé et onze
contrôles structurels. Clippy natif/WebGPU et formatage passent. Deux tests
existants de raccords terrain, refusés par Clippy, vérifient désormais les bits
exacts ; les 192 tests monde sont rejoués après cette correction. Les anciens
échecs restent consignés. Les captures scène/couverture reprennent exactement
7/12/0 cellules de l'oracle. Le jeu de données contient 221 fichiers réutilisés,
sans nouveau payload de ressources ; il reste une entrée de qualification,
pas une distribution. Les natifs 023/024 du module passent, avec les mêmes plans,
package et projections attendues que 021/022. Les 2 423 entrées restent stables.

Suite identifiée à cette étape : raccorder cette herbe au matériau sous éclairage monde et
à l'hôte de module, avec champs de terrain/masque réellement admis et les contrats
auteur déjà présents, puis qualifier l'édition/picking Studio. Ne pas fournir
des textures neutres ni déclarer cette activation acquise par le seul déplacement
de fichiers. Graphe de profondeur, autres ports, profils, monde étendu et
qualification de distribution restent ouverts, ainsi que les dix critères.

## Champ d'herbe déclaré dans le jeu de modules — 25 octobre 2026

Le raccordement ci-dessus est réalisé pour le profil importé source. Le bilan
central `module-grass-validation-001.json` relie le même résident moteur à
`WorldPlantMaterial`, avec raster original, corrections de coins et masque
réels. Le décodeur réutilise les géométries déjà admises. Le shader conserve
la branche source d'herbe, son animation et sa position précédente. Les
corrections de terrain et la construction de caméra finie passent également
dans le moteur, avec appels directs depuis leurs consommateurs.

[ADR-0042](../ADR/ADR-0042-explicit-imported-grass-fields.md) consigne une
déclaration canonique explicite : document végétal v2 avec référence à l'index
du profil fermé. Le v1 reste identique et sans champ implicite ; l'emploi
d'une bibliothèque ne suffit pas à demander le contenu de la carte source.
Le candidat autonome `composed-003-grass` conserve 103/106 payloads identiques,
le terrain, les corrections RPG et les blocs de plantes. Ses données de test
occupent 214 fichiers / 221 884 779 octets, sous 256 Mio / 384 fichiers.

Les essais natifs 025 sans déclaration et 026/027 avec déclaration passent.
Le 027 termine 17 étapes : 4 682 images prêtes sur 5 119, 136 observations GPU
principales et 301 achèvements de ressources d'herbe distincts. Chaque état
prêt exige la génération publiée et achevée, avec 3 meshes, 3 matériaux,
7 images et 5 buffers réellement admis ; aucune texture neutre ne remplace
le raster ou le masque. Les captures inspectées et la référence 003/004
complètent cette preuve, sans prétendre relire tous les pixels de chaque image.

Les tests et Clippy natif/WebGPU passent ; les 2 425 entrées restent stables.
L'intervalle prêt du 027 (moyenne 13,60 ms, p95 26,11 ms) reste un diagnostic
de développement. La suite porte sur le parcours auteur Studio v2,
l'édition/picking, les autres profils, les ports encore couplés, puis les
critères généraux de monde étendu et de distribution. Les dix critères de
Gate 7 restent ouverts ; la parité complète avec Tidewater reste à démontrer.

## Picking source commun et occlusion GPU — 25 octobre 2026

Selon [ADR-0043](../ADR/ADR-0043-shared-source-picking.md), le matériau ID,
shader, passage hors écran et snapshot d'occlusion sont transférés au moteur.
Les occluders emploient des paramètres de type de matériau fournis par les
consommateurs, avec le contrat de shader source existant. Les producteurs et
filtres de caméras temporaires emploient `OffscreenSceneCamera`. L'exigence de
profondeur échantillonnable rejoint `forge_engine::view_depth`, consommé par
AO et flare. Le graphe d'ombres à cascades reste une extraction distincte.

Le bilan central `shared-picking-validation-001.json` rapproche 771 tests
moteur, 326 tests de carte, 84 tests d'exemple, treize contrôles structurels,
Clippy natif/WebGPU et formatage. Le contrôle sur le shader épinglé conserve
les déformations, alpha et IDs 24 bits. Le passage GPU utilise la zone exacte
du module et des surfaces de référence comme occluders diagnostiques : trois
images relues, 37 identités visibles résolues, 5 108 pixels masqués par
terrain/surfaces et 3 970 supplémentaires par l'objet standard. Ce montage
ne qualifie pas le module complet ou le parcours Studio.

Le jeu de modules 028 passe les 17 étapes et garde exactement la projection
attendue de 027. La capture du bosquet est inspectée. Les données immuables
sont réutilisées ; le snapshot supplémentaire coûte 54 164 octets nouveaux
et 12 fichiers / 27 292 660 octets logiques, sous 32 Mio / 32 fichiers.
Le budget des preuves de tranche est 200 Mio / 128 fichiers, mesuré dans le
bilan. Poursuivre graphe de profondeur, autres ports et parcours Studio,
puis qualification générale des dix critères, qui restent ouverts.

## Graphe commun des ombres à cascades — 25 octobre 2026

[ADR-0044](../ADR/ADR-0044-shared-source-cascade-graph.md) transfère
l'exécution dans `forge_engine::source_shadows::casters` : trois cascades,
uploads opaque/terrain/plantes, animation et reprises, frames transitoires et
kernels GPU des casters masqués et prototypes indexés. Les codecs gardent leurs
gardes d'acquisition ; le GPU vérifie les indices contre l'inventaire admis.
La collecte d'éligibilité des objets de carte reste dans son intégration.
Les consommateurs Studio/référence appellent les propriétaires communs.

Le bilan central `shared-cascades-validation-001.json` rapproche 787 tests
moteur, 311 tests de carte, 84 tests d'exemple, un smoke d'extraction et quatorze
contrôles structurels, avec Clippy natif/WebGPU et formatage. Une comparaison
réelle du binaire précédent et du nouveau conserve exactement les 50 331 648
octets des trois profondeurs, à caméra, lumière, pose et ressources identiques.
Le contrôle GPU indépendant d'installation tardive, désactivation, remplacement
et refus atomique passe. Les images finales restent distinctes (écart RGB
maximal observé 9 niveaux) ; aucune parité pixel n'est déduite des profondeurs.
Le mode testé ne qualifie pas les récepteurs finaux PCSS/PCF.

Le module 029 termine les mêmes 17 étapes que 028 et conserve sa projection
attendue. Les ressources du test sont réutilisées ; seules les métadonnées
d'ombres d'origine sont restaurées depuis l'archive vérifiée. Nouvelle entrée :
222 fichiers / 181 134 092 octets logiques sous 256 Mio / 256 fichiers,
39 823 octets de nouveaux fichiers physiques. Preuves sous 200 Mio / 256 fichiers.

Suite : raccorder réellement les capacités et récepteurs nécessaires aux hôtes
de modules via leurs données/réglages canoniques ; qualifier casters animés et
prototypes réels, Studio, profils et les autres ports. Le déplacement du graphe
ne suffit pas à cette reprise globale. Les dix critères de Gate 7 restent ouverts.

## Vent source réellement consommé par le module — 25 octobre 2026

[ADR-0045](../ADR/ADR-0045-shared-source-wind-clock.md) corrige la divergence
du vent : le module remplace son intégrateur de rafales, wrap spatial et cycle
de 16 phases par la même horloge que la référence. Le transport est celui du
source ; le delta du module reste borné à 0,1 s. Force/fréquence auteur et phase
initiale sont conservées, et la fréquence règle uniquement la forme d'onde.
Le candidat de l'horloge est committé après publication réussie de la frame,
avec les vraies valeurs précédentes. Le marqueur hors écran commun est ignoré
pour choisir la caméra interactive ; une seconde caméra interactive est refusée.

Le bilan central `shared-wind-validation-001.json` rapproche 792 tests moteur,
309 tests de carte, cinq tests d'entrées génériques, 84 tests d'exemple,
quinze contrôles structurels, Clippy natif/WebGPU et formatage. Les corps
source des deux horloges sont vérifiés conservés, hors propriétaire et getter
de temps double précision. Les tests comparent les rafales et phases exactes
sur changements de direction, deltas nuls/lents, wrap et fréquence auteur.

Le natif 030 garde le module v1 sans herbe implicite ; 031 passe 17 étapes sur
le module v2, avec la projection attendue de 029. La référence statique 005
et le bosquet du module sont inspectés. Aucun asset n'est copié ou modifié.
Budget de preuves : 200 Mio / 128 fichiers, mesuré dans le bilan.

Cette reprise est effective pour le vent du module ; elle ne résout pas le
raccordement restant des récepteurs/effets de référence au chemin PBR du module.
Continuer ce raccordement, Studio, profils et qualifications générales. Les dix
critères de Gate 7 restent ouverts.

## Exécution commune de l'environnement — 25 octobre

[ADR-0046](../ADR/ADR-0046-shared-source-environment-graph.md) transfère les graphes
existants d'atmosphère, nuages, ciel et environnement HDR vers le moteur. Les
consommateurs Studio et référence nomment leurs propriétaires communs. Les
lecteurs d'acquisition conservent la provenance et les gardes du dataset ; les
agrégats préparés/résidents ne chargent aucun fichier. Le diagnostic des programmes
emploie maintenant leur préparation de production, sans compilation privée.

`shared-environment-validation-001.json` rapproche 825 tests moteur, 278 tests
de carte, 43 tests d'intégration, 84 tests d'exemple, seize contrôles structurels,
Clippy natif/WebGPU et formatage. La comparaison normalisée de 155 fonctions et
les octets des 64 fichiers WGSL sont conservés. Les essais GPU vérifient quatre
phases solaires/readbacks distincts avec ressources conservées, deux générations
de nuages de 120 étapes et les pixels du ciel. Les 22 programmes sont compilés
sur le périphérique de scène ; le graphe HDR vivant n'est pas exécuté par cet essai.

Les modules 032/033 passent ; le 033 termine 17 étapes avec la projection attendue
du 031, 139 observations GPU principales et 301 achèvements de ressources d'herbe.
La référence statique 006 est inspectée. Les ressources restaurées sont vérifiées
contre les archives ; les scripts qu'elles contiennent ne sont pas exécutés.
Les 2 436 entrées / 282 833 868 octets restent stables. Budget de preuves de cette
tranche : 200 Mio / 256 fichiers, mesuré dans le bilan ; aucune purge implicite.

Suite obligatoire : admission explicite des ressources d'environnement dans le
paquet autonome, configuration canonique et consommation du rendu source par
les modules. Le chemin PBR actuel ne reprend pas encore cet éclairage et ses
récepteurs PCSS/PCF. Les autres ports, parcours auteur Studio, profils, comparaison
à conditions identiques et dix critères de Gate 7 restent à terminer.

## Admission et hôte d'environnement du module — 25 octobre

`module-environment-validation-001.json` documente le raccord sous ADR-0047.
L'API 4 déclare les ressources par aire ; les API antérieures conservent leur
sérialisation sans activation implicite. Les codecs transférés sont communs,
et le lecteur de module ferme index/pins/tailles avant ses lectures. L'hôte moteur
réutilise les graphes existants avec la caméra réelle, la quantification source de
hauteur et des observations distinctes d'encodage, publication HDR et completion GPU.

Le paquet 1.0.6 conserve la tête, le terrain, les 65 383 plantes, 13 434 sources de
collision et le RPG du candidat précédent. Les natifs 036/037 passent ; le 037
termine les 17 étapes avec 136 preuves graphiques datées, 4 548 frames prêtes et
une trace non tronquée. Ses intervalles prêts (moyenne 13,91 ms, p95 26,17 ms)
restent des diagnostics. Le 035 conserve le refus graphique initial du ciel ;
sa correction garde le refus des matériaux inconnus/ambigus et ajoute la barrière
de ciel à chaque application de profil. Le module ancien 034 passe sans environnement.

La reprise des surfaces PBR sous éclairage/récepteurs source reste obligatoire.
Ne pas assimiler cette admission à la parité visuelle ou de performances. L'export
fidèle de la fermeture d'environnement, la configuration auteur/Studio, les profils
et qualifications générales restent ouverts ; l'exporteur refuse la perte de ces
ressources tant qu'il ne peut pas les préserver. Les dix critères ne sont pas clos.

## Surfaces sous éclairage source — en cours, 25 octobre

ADR-0048 et `module-surface-validation-001.json` suivent ce raccord. L'exécution
des ombres du relief et leur préparation de mipmaps résident dans
`source_shadows::heightfield`. Le kernel attribué réutilise le corps acquis
épinglé ; `PreparedInput::from_heights` le consomme sans recherche d'acquisition.
La préparation/sélection des casters végétaux réside dans
`source_vegetation::scene::shadow`, appelée directement par Studio/référence.
Le code transféré et ses tests sont vérifiés conservés après seuls changements
de propriétaires, formatage et trois labels de diagnostic.

Le matériau du monde conserve maintenant les fonctions source de lumière directe,
d'environnement et de translucidité derrière une spécialisation explicite, avec
les mêmes déformations et prepasses. `bind_source_scene` exige les générations
résidentes et leurs ressources typées. La liaison depth/comparaison inutilisée
dans la spécialisation PBR a un vrai asset 1×1×3 compté : huit images d'herbe.
Les échecs natifs 038/039 (sampler puis attributs de point d'entrée sur un helper)
sont conservés. Le 040 passe six étapes et le 041 passe 17 étapes en PBR ; cela
ne qualifie pas la nouvelle spécialisation source, encore non activée dans le module.

Le test heightfield 001 conserve son échec face à l'image archivée. Le mode
comparatif explicite 002 exécute trois générations : verticale, shader acquis,
kernel moteur. Les deux dernières sont identiques sur ce GPU ; l'ancien test
d'égalité à l'archive reste disponible et n'est pas relabellisé. La comparaison
mesure 22 pixels différents sur 262 144, canal hauteur uniquement, maximum 0,03125,
aucune valeur non finie. Aucune tolérance ni parité archivée n'est qualifiée.

Le raccord suivant active la spécialisation végétale du module avec ses vraies
ressources de scène. Il réemploie les casters et le graphe communs, ainsi que
`CascadeView::from_camera_matrix`. Le vent visible alimente les casters sans
autre intégration. Les inventaires des plantes et de l'herbe sont renouvelés
lors d'un changement de casters. Les paramètres de nuages sont publiés dans la
frame commune de 176 octets, en évitant les modifications de matériaux à chaque
déplacement. Le bilan `module-surface-live-validation-001.json` rapproche les
essais et conserve leurs échecs.

Suite requise : raccorder la lumière/réception du terrain et des autres surfaces
au même moteur ; compléter les casters standards alpha/skinnés et qualifier
Studio, les quatre profils et les performances dans des conditions appariées.
L'export fidèle, la distribution et les dix critères généraux restent ouverts.

## Récepteurs du terrain et des objets — raccord partiel, 25 octobre

`shared-surface-validation-001.json` rapproche équations, consommateurs et
preuves natives. `source_lighting::surface_library` possède les fonctions de
soleil, SH/HDR et réception d'ombres ; les sept fonctions de BRDF comparées aux
shaders acquis plante/village/terrain sont identiques hors blancs. Les matériaux
gardent le décodage standard, la palette du terrain et les prépasses existantes.
`surfaces::material` délègue au matériau composé Bevy et déclare explicitement
les dépendances de sa base et de son extension. Les liaisons originales restent
vérifiables par les barrières de bâtiment, le gameplay et le HLOD existant.

Le 051 passe 17 étapes, le 052 six étapes historiques sur le même binaire ; les
références réduites 007/008 passent en masque/scène. Le 050 conserve son refus
pour trace tronquée malgré ses 17 étapes terminées. La capacité passe de 512 à
1 024 observations datées par domaine : le 051 en utilise 538 côté végétal,
dans 2 424 589 octets. Les 20 000 intervalles, les 8 Mio, les dates originales
et le refus de toute troncature restent inchangés.

Suite obligatoire : restaurer réception et ombres des lumières ponctuelles
intérieures existantes en réemployant les mécanismes présents ; extraire les
mécanismes de lumière locale encore privés ; activer le raccord indépendamment
de la végétation. Compléter ensuite les casters alpha/skinnés, les autres effets
réutilisables, Studio, les quatre profils et les performances appariées.
Cette reprise ne termine aucun des dix critères généraux de Gate 7.


## Lumières locales sur les surfaces communes — restauration partielle, 25 octobre

`shared-local-lights-validation-001.json` rapproche les contrôles du raccord aux
fonctions Bevy existantes de lumière ponctuelle, spot et ombre ponctuelle. Le
natif 054 termine 17 étapes et le 055 six étapes historiques. L'essai isolé 008
vérifie les contributions, les ombres ponctuelles et le noir sans lumière ; son
verdict strict reste FAIL sur un pixel par comparaison ponctuelle/spot, d'un
niveau maximal de canal. Les refus précédents restent conservés sans tolérance.

Suite obligatoire : activer l'hôte commun sans végétation ; transférer le
mécanisme privé des lumières en unités source sans changer ses calculs ; résoudre
et qualifier l'écart d'image strict ; couvrir casters alpha/skinnés, autres
mécanismes réutilisables, Studio, profils et performances appariées. La reprise
des fonctions doit être effective dans tout module déclarant la capacité. Les
dix critères généraux restent ouverts.


## Cycle de vie des surfaces sans plantes — preuve native, 25 octobre

`shared-scene-validation-001.json` rapproche le transfert du pilotage dans
`source_lighting::surfaces::{scene,live}`, la frame commune, les gardes et les
consommateurs. Le 058 passe les 17 étapes du module complet et le 059 les six
étapes historiques. `surface-scene-native-002` passe six étapes avec le même
binaire et un paquet fermé sans végétation : 330 récepteurs, zéro lot végétal,
cascades encodées et completion GPU datée. Le profil source raster 2048 est
admis directement ; aucun hôte ou paquet végétal n'est requis dans cet essai.
Le module de qualification conserve terrain, environnement et RPG, avec une
nouvelle tête explicite ; il ne remplace pas le candidat complet.

Suite obligatoire : transférer les lumières en unités source encore privées,
résoudre l'écart strict des images locales, couvrir les casters alpha/skinnés,
les autres mécanismes réutilisables, Studio et les quatre profils ; mesurer les
performances release à conditions appariées. L'activation sans plantes est
prouvée dans ce profil, sans qualifier tous les autres terrains ou profils.
Les dix critères généraux restent ouverts.


## Lumières locales source — propriétaire moteur repris, 25 octobre

`shared-source-lights-validation-001.json` rapproche la reprise du collecteur,
projection, récepteur WGSL et transport dans le moteur. Tidewater et les imports
Studio utilisent ces propriétaires directement. Les calculs contrôlés et le
WGSL conservent leurs octets ; aucun solveur d'éclairage concurrent n'est ajouté.
Les protocoles de vue/sélection interrogent les vrais producteurs temporels ;
ils conservent les refus de packet absent, caméra étrangère et révision périmée.
Les tests et les lectures GPU de l'oracle et des 32 lampes réelles passent.
Le 060 conserve son interruption de fenêtre ; le 061 passe 17 étapes, le 062
six étapes historiques et le cas sans plantes 003 six étapes.

Suite obligatoire : fermer et exporter les compagnons de lumières des assets
de module ; projeter leurs vrais repères d'instances ; collecter et faire
consommer des paquets non vides par les matériaux standards, terrain et plantes
communs en reprenant le récepteur existant. Prouver le dessin et les variations
jour/nuit/désactivation, puis Studio et les quatre profils. Le transport seul
ne suffit pas. Les autres mécanismes encore privés, l'écart strict des images
ponctuelles, les casters alpha/skinnés, les performances release appariées et
les qualifications générales restent ouverts. Aucun critère général n'est clos.


## Réception locale standard/terrain — 25 octobre 2026

`shared-source-receivers-validation-001.json` relie le binding réel du paquet
source commun et la dérivation du récepteur existant sur les surfaces standard
et terrain. Le dessin 002 passe ses cinq comparaisons : contribution non vide,
jour, désactivation, inventaire invalide et reprise exacte. Deux régions
indépendantes confirment les murs standard et le sol terrain éclairés.
Les 855 tests moteur passent, trois restent ignorés ; l'exemple et les 20 gardes
d'architecture passent. Clippy moteur/carte et Studio WebGPU passent.
Le module 063 termine ses 17 étapes et le module sans plantes 004 ses six étapes.
Leurs surfaces déclarent quatre buffers réels ; les quotas produit sont conservés.
Le dérivé du replay modifie uniquement le nombre de buffers attendu pour cette
nouvelle dépendance et conserve le script historique. Les entrées compilées
restent inchangées. Le contrôle photométrique 009 reste FAIL : un pixel d'écart
sur point et spot, images identiques à 008, sans tolérance ajoutée.

Suite : fermer/exporter les compagnons lumineux du module, projeter les vrais
owners et fournir leur inventaire autoritaire au collecteur commun ; raccorder
la réception végétale au même paquet en vérifiant les limites GPU. Prouver ensuite
le parcours de jeu complet avec lampes, Studio, jour/nuit/désactivation et les
quatre profils, puis la comparaison release appariée. La scène sèche de sonde
ne clôture pas ces parcours. Les dix critères généraux de Gate 7 restent ouverts.


## Compagnon lumineux conservé dans le document — 25 octobre 2026

`shared-light-bindings-validation-001.json` relie le contrat de référence, les
repères de modèle et l'adaptateur de projection commun. Le head 18 et la famille
49/50/51 rendent la capacité explicite ; l'absence conserve les anciens formats.
La fermeture requise couvre aussi les sources nécessaires aux checkpoints et à
Annuler/Rétablir. Les API de module actuelles refusent une référence non prise en
charge, même retirée du head courant mais présente dans l'historique.
Le workspace passe 2 709 tests (63 ignorés) et l'outil autonome de distribution
112 tests (deux ignorés). Clippy workspace, outil autonome et Studio WebGPU passent.
Les gardes d'architecture et le contrôle de texte restent distincts de l'observation
réelle du Studio. Le conflit de cache Navigation du test 002 est conservé ; après
reconstruction ciblée de ses artefacts dev, le test 003 passe sans changement source.

Suite obligatoire : exploiter `required_local_light_packages` dans le snapshot
et l'export des modules ; fermer les manifestes et tous les membres via le codec
`SourceLocalLightsPackage`, garder les licences et pins d'origine, et versionner
le support de cette capacité dans l'API de module. Charger les paquets fermés et
raccorder les véritables racines de placement à `projection::placement`, puis
fournir au collecteur l'inventaire attendu issu des données et du plan de résidence.
Raccorder les producteurs Studio à ces références et leurs erreurs de ressources.
Prouver le dessin non vide du module, jour/nuit/désactivation, la réception végétale,
les quatre profils, Studio et la comparaison release appariée. Les autres mécanismes
réutilisables et les dix critères généraux restent ouverts.


## Paquets lumineux fermés par l'API de module 5 — 25 octobre 2026

Le contrat commun d'export et d'admission des paquets lumineux est désormais
implémenté pour une zone et pour les exports agrégés. Il couvre l'historique,
les sources originales encodées comme données inertes, la licence, les pins
des porteurs et la fermeture des repères. Les tests incluent le chargement
installé, le paquet réservé à une zone secondaire, la déduplication entre zones,
la suppression suivie d'Annuler et les refus de ressources/références invalides.
Le bilan central `shared-module-lights-validation-001.json` conserve les entrées
et les contrôles. Les textes datés précédents décrivent l'état antérieur.

Suite immédiate : raccorder le chargeur au propriétaire moteur commun de scène
et à `projection::placement`, avec les véritables racines canoniques et une
liste attendue issue des données admises. Employer la vraie caméra, le temps
et la valeur nocturne de la scène, puis le collecteur et transport GPU existants.
Le lecteur conserve un refus explicite jusqu'à ce raccordement ; aucun module
lumineux n'est annoncé jouable sur la seule preuve de transport. Raccorder
ensuite Studio et les plantes, prouver les profils et les contrôles jour/nuit/
désactivation, et mesurer la référence et le module à conditions identiques.
Tous les mécanismes réutilisables doivent suivre ce même principe moteur,
sans variante par module. Les dix critères généraux restent ouverts.


## Raccordement au producteur commun des placements lumineux — 25 octobre 2026

Le lecteur de modules est désormais raccordé au producteur moteur commun et aux
paquets admis. Le refus global des références de l'étape précédente est retiré ;
un environnement source déclaré et ses surfaces communes restent requis par
le chemin de réception actuel. Les références uniquement historiques sont
chargées et conservées sans fabriquer de lumières courantes. L'inventaire attendu
est indépendant du monde ECS observé. La caméra, le temps et la valeur nocturne
viennent de la scène ; Studio réutilise aussi ce lecteur d'entrées. Les producteurs
se réservent exclusivement la collection. Le verdict de préparation du jeu
contrôle les inventaires, l'identité et la mise en file datée pour la même caméra.

Le bilan `shared-placement-light-host-validation-001.json` couvre les tests CPU
et compilations de ce raccordement. Suite immédiate : fabriquer une variante
de qualification avec de vraies références enregistrées et les paquets fermés,
lancer le lecteur natif, observer ses états et inspecter ses captures. Prouver
le dessin avec paquet non vide et les contrôles réels jour/nuit/désactivation ;
reprendre la publication solaire existante plutôt que créer un second calcul
ou une horloge divergente. Raccorder la production et l'export des références
dans Studio, puis la réception végétale, les quatre profils et la comparaison
release appariée. Ces étapes et les autres mécanismes réutilisables restent
nécessaires ; les dix critères généraux restent ouverts.


## Première exécution native des références lumineuses — 25 octobre 2026

Le module de qualification API 5 est composé à partir du module sans plantes,
en conservant terrain, environnement, RPG, géométrie et fichiers immuables.
Une commande d'archive ajoute la référence et son repère local sur un porteur
explicitement choisi. Le paquet original de lumières est fermé et admis, puis
chargé par le lecteur installé. `saved-lights-module-native-001` passe ses six
étapes avec un émetteur attendu, une caméra cohérente, une file datée et les
330 récepteurs communs prêts. Le bilan
`saved-lights-module-validation-001.json` relie composition, compilation et capture.

Le cas conserve le soleil acquis de jour : zéro lumière sélectionnée est
attendu et vérifié. Il ne remplace pas la preuve de contribution nocturne.
Suite obligatoire : reprendre la publication solaire utilisée par la référence
et le Studio dans le propriétaire moteur, avec ses contrôleurs, le readback
d'atmosphère et la soumission solaire des nuages existants. Examiner d'abord
les données et commandes déjà présentes avant tout nouveau contrat. Faire
varier réellement soleil/ciel/ombres/éclairage, sans forcer seulement la valeur
nocturne du collecteur. Prouver ensuite le paquet non vide, les contrôles
jour/nuit/désactivation, les plantes, les profils et la comparaison release.
Tous les autres consommateurs et les dix critères généraux restent ouverts.


## Soleil commun : jour, nuit et référence retirée — 25 octobre 2026

Le raccordement solaire requis après le premier module lumineux de jour est
réalisé dans `source_sky::publication`, avec consommation directe par les trois
chemins existants. L'API 6 transporte ses paramètres de module. Les variantes
privées conservent terrain, géométrie, RPG, environnement fermé et historique.
Les essais `solar-light-module-native-001..003` passent avec le même binaire :
un émetteur sélectionné à minuit, aucun courant après retrait, aucun sélectionné
de jour malgré sa référence conservée. Les captures et la comparaison de régions
fixes prouvent la contribution nocturne sur terrain et modèles.

Le bilan central `shared-solar-publication-validation-001.json` conserve également
les échecs de développement et les limites. Suite : réception lumineuse des
plantes avec contrôle des limites GPU, parcours Studio jusqu'à l'export des
paramètres/références, quatre profils et benchmark release apparié avec la
référence. La suppression d'une référence ne remplace pas encore un contrôle
interactif activé/désactivé du même module. Vérifier les autres mécanismes de
Tidewater selon la même règle : propriétaire moteur commun et consommateurs
réels, avec données propres au module. Les dix critères généraux restent ouverts.


## Récepteurs végétaux raccordés — 26 octobre 2026

Le générateur et le buffer lumineux sont consommés par les deux matériaux végétaux
communs, dont les fabriques Studio/référence et le lecteur de modules. Le module
complet passe les deux cas nocturnes avec présence puis retrait de la référence ;
les captures sont inspectées. Les ressources originales sont préservées et les
deux variantes restent sous 320 Mio/1 600 fichiers chacune. Les preuves de cette
tranche sont reliées par `shared-plant-local-lights-validation-001.json`.

Suite : vérifier le parcours Studio et l'export des paramètres/références, puis
le contrôle interactif et les quatre profils. Rejouer le matériau acquis dans
la référence et mesurer les parcours release appariés, sans assimiler le comptage
des bindings à un test WebGPU navigateur. Les autres mécanismes réutilisables,
les livrables de distribution et les dix critères généraux restent à qualifier.


## Export des compagnons depuis l'origine — 26 octobre 2026

Le backend commun conserve maintenant environnement, horloge, végétation et
lumières de l'origine admise sur les aires du snapshot. L'essai privé retouche
un porteur et place la carte complète en secondaire pour exercer la fermeture
et l'API maximale. Les refus de publication d'une origine privée et de conflit
de ressource restent prouvés. Le paquet réexporté est relu par le lecteur API 6
existant, avec six étapes et capture inspectée. Les preuves sont reliées par
`shared-origin-companions-validation-001.json`.

Le parcours UI n'est pas assimilé à ce test de fonction : le bootstrap Studio
refuse toujours les compagnons végétaux. Suite obligatoire : restaurer et rendre
ces sources par les propriétaires moteur communs dans Studio, sauvegarder/rouvrir,
exporter depuis l'interface et rejouer le résultat. Les paramètres solaires,
références lumineuses et contrôles interactifs doivent rester modifiables par
l'auteur. Poursuivre ensuite profils, référence appariée, builds et distributions
finales. Les dix critères généraux restent ouverts.


## 26 octobre 2026 — Liaison des lumières d’objets dans le moteur commun

`forge_engine::source_lighting::local_lights::projection::source_object`
possède désormais la jointure des identités source, les attestations de
construction, la vérification des ancres et la projection/remplacement des
lumières sur une racine auteur unique et propagée. Son entrée est un inventaire
typé d’objets vérifiés, sans catalogue ni scène de carte imposés. Le Studio
(admission et réconciliation) et les deux sondes de qualification appellent ce
propriétaire directement. L’adaptateur de carte ne garde que le décodage de ses
descripteurs vérifiés ; aucune réexportation ne conserve l’ancien mécanisme.

Le bilan central `shared-object-light-bindings-validation-001.json` rapproche les 2 721 tests
workspace (63 ignorés), 21 contrôles d’architecture, Clippy natif/WASM et le
formatage. Un cycle indépendant de toute scène de carte vérifie copie, retouche,
suppression, phases et refus atomiques des racines ambiguës, parentes ou changées.
Les données réelles produisent 19 objets lumineux et 32 émetteurs ; six relectures
du buffer GPU de 448 octets vérifient caméra, invalidation et reprise, avec et
sans paquet temporel. Les deux manifestes acquis gardent leurs pins.

Ces sondes ne dessinent pas les maillages et ne qualifient pas le parcours Studio
complet, les performances ni la parité visuelle. L’outil natif de restauration
de module refuse encore la végétation ; la matérialisation auteur complète
des compagnons, les autres reprises moteur et les dix critères Gate 7 restent
à terminer et qualifier. Les échecs intermédiaires de compilation/emprunt puis
d’inventaire dans le test indépendant restent conservés avec leurs corrections.


## 26 octobre 2026 — Lecture native des compagnons végétaux du module ouvert

Le Studio possède un protocole natif de description et de lecture par blocs de
64 Kio des ressources végétales de l’origine du module ouvert. La résolution
des espèces gouvernées et des prototypes importés emploie les mêmes décodeurs
`forge_delivery::module_vegetation` que le jeu installé. Le nouveau chemin
`restore_authoring` vérifie séparément le head d’origine admis et le head auteur
courant ; il produit une liaison dérivée, sans réécrire un manifeste, une archive,
une preuve ni un droit. L’admission installée garde son exigence de head exact.
Les identités et pins viennent du host canonique ; la vue ne choisit aucun chemin
natif. Chaque ressource est assemblée intégralement puis vérifiée par SHA-256.

Le bootstrap de qualification ne refuse plus globalement la végétation : il
restaure et vérifie chaque zone avant activation, et conserve l’origine et ses
payloads. Le bilan central `module-vegetation-restore-validation-001.json` rapproche
2 723 tests workspace, 121 tests de transport, 21 contrôles d’architecture,
Clippy natif/WASM et formatage. L’essai privé du module complet à deux zones
lit exactement 84 ressources (46 605 576 octets), vérifie une retouche canonique
après revue, les lectures obsolètes/refusées et la réouverture du lecteur.
Les tests synthétiques couvrent les deux familles de ressources en édition,
tout en conservant le refus des heads différents dans l’admission installée.

Le parcours qualifié est natif et son aller-retour JSON est testé. Aucun WebView
ni draw GPU n’est exécuté par cet essai. Le viewport consomme encore la
bibliothèque de référence : son raccordement à cette lecture du module, les
catalogues d’auteur et la qualification visuelle complète restent à terminer.
Ce bilan ne qualifie ni les performances, ni Linux, ni une machine propre et ne
clôture aucun des dix critères généraux Gate 7. Les essais échoués restent
conservés : revue obligatoire manquante dans le test, puis confusion initiale
entre le head installé et le head auteur, avant correction du chemin commun.


## 26 octobre 2026 — Végétation du module dans le viewport commun

La végétation du module ouvert est maintenant acquise depuis son origine
canonique par le WebView, puis confiée aux propriétaires existants du moteur :
préparation, textures, rendu, herbe, sélection et projection des espèces. Le
Studio ne réimplémente pas ces mécanismes. Le chargement vérifie head, module,
zone, origine, ressources et hashes ; un échec ne bascule pas sur la bibliothèque
de référence. Les ressources immuables sont réutilisées et les réponses d'une
ancienne sélection sont rejetées, y compris au retour d'un picking GPU.

Le bilan central `module-vegetation-viewport-validation-001.json` rapproche
2 726 tests workspace (65 ignorés), 21 contrôles d'architecture, Clippy natif/WASM,
formatage et les deux builds du Studio. L'acquisition privée lit 84 ressources
avec 765 requêtes, puis une seule description en réutilisant le paquet immuable.
L'instance réelle ouvre les deux zones enregistrées : zéro matériau végétal sur
la première, onze sur la seconde, avec herbe source annoncée et végétation visible.
Les captures et sorties DOM sont conservées ; elles ne prouvent pas seules
l'origine des octets, vérifiée séparément par le test d'autorité/acquisition.

La vérification révèle encore des objets de référence affichés sur la première
zone malgré une liste canonique vide. Le terrain, les objets et l'environnement
gardent des acquisitions de référence à raccorder aux données du module.
L'éclairage du Studio reste fixe. Les instantanés de cadence ne sont pas un
benchmark apparié. L'édition/sauvegarde/réouverture/réexport dans l'UI, la fidélité
complète, les performances et les distributions restent à qualifier. Les dix
critères généraux Gate 7 restent ouverts. L'instance de qualification a été fermée
sans demander de sauvegarde ; aucun essai historique n'est réécrit.


## 26 octobre 2026 — Matériau et géométrie de surfaces dans le moteur commun

`forge_engine::source_surface` possède maintenant le matériau de surfaces source,
sa spécialisation GPU, la conversion des surfaces opaques neutres, les attributs
de maillage, les paramètres de lumière sous l'eau et le relais solaire. Studio,
runtime et référence utilisent directement les mêmes types. Le shader garde son
UUID et les attributs gardent leurs identifiants et formats. Le flare consomme un
set de publication moteur ; le moteur ne dépend plus d'un système optique de carte.
Les ressources acquises et les raccords de shader encore présents dans l'adaptateur
ne deviennent pas des fonctions spécifiques à Tidewater : leur extraction reste
à poursuivre, ainsi que les projections d'objets et l'acquisition depuis le module.

Le bilan central `shared-source-surfaces-validation-001.json` rapproche 2 728 tests
workspace (65 ignorés), 22 contrôles d'architecture, Clippy natif/WASM, formatage
et un binaire natif reconstruit. Le décodeur de maillage conserve ses tokens après
les renommages documentés et aucun fichier WGSL ne change. Le module exporté
inchangé passe les six étapes du scénario natif avec végétation, herbe, environnement,
récepteurs de surfaces et lumière locale admis. Les captures sont inspectées.

Cette reprise ne corrige pas encore les objets hérités implicitement du relief
dans Studio. Elle ne qualifie pas l'UI complète, tous les matériaux/profils, la
parité visuelle ni les performances release. Les dix critères généraux Gate 7
restent ouverts. Voir ADR-0049 pour la propriété des contrats et les limites.


## 26 octobre 2026 — Projection commune des objets source triangulés

`forge_engine::source_surface::{geometry,objects}` possède le cache de géométries
par révision, la projection des placements et leurs surfaces de collision, ainsi
que leur remplacement et retrait dans Rapier. Le décodage du paquet acquis reste
un adaptateur ; il fournit des parties typées au moteur. Studio, sondes et collecte
d'ombres emploient directement les propriétaires moteur, sans anciens types en façade.
L'essai moteur indépendant de toute carte couvre copie, transformations, géométrie
partagée, révisions/refus atomiques, racine parentée et retrait/rétablissement du backend.

Le bilan central `shared-source-objects-validation-001.json` rapproche 2 730 tests
workspace (65 ignorés), 23 contrôles d'architecture, Clippy natif/WASM, formatage et
deux sondes natives reconstruites. Le paquet réel fournit 23 bâtiments, 111 parties
et 112 569 sommets. Les collisions, copies et rayons passent ; 107 parties opaques
alimentent le graphe GPU sur trois cascades. Les trois générations de profondeur
diffèrent après transformation et la suppression laisse zéro texel occupé.
Les données brutes et leur vue de couverture inspectée sont conservées.

La fixture native est explicitement dérivée du paquet, sans prétendre relire une
carte auteur. Le rendu couleur, les autres familles d'objets, l'acquisition explicite
des ressources du module dans Studio et son parcours complet restent à qualifier.
L'héritage implicite des objets de référence n'est pas encore corrigé. Aucun gain
de performances ni critère général Gate 7 n'est déclaré clos. Voir ADR-0050.


## 26 octobre 2026 — Objets explicitement embarqués dans le module, API 7

Le contrat du module possède une liaison d'objets par zone : index, inventaire fermé,
identités et pins des assets source. Elle reste distincte des GLB gouvernés. Le
transport vérifie tailles, hashes, fermeture exacte et présence de la licence dans
la provenance ; l'absence de liaison n'infère aucun objet depuis le relief. Le codec
`forge_assets::source_surface_package` et la préparation moteur
`forge_engine::source_surface::package` reprennent les implémentations existantes.
Les anciennes API gardent leur forme JSON quand la liaison est absente.

Le bilan central `module-source-objects-validation-001.json` rapproche 2 734 tests
workspace, 122 tests de distribution, Clippy natif/WASM, formatage et contrôles
d'architecture. L'essai privé sur une zone RPG de qualification embarque les 23
assets du paquet réel et ses 821 ressources, puis vérifie placement, retouche et
réexport avec conservation des pins et des droits d'origine. Les refus d'inventaire,
de corruption et de licence non déclarée sont testés. Les trois relectures GPU de
profondeur restent identiques à la sonde précédente après transfert du lecteur et
de la préparation ; cela ne qualifie pas le rendu couleur.

Le nouveau lecteur n'est pas encore raccordé à l'affichage des objets du module
dans Studio ni dans le jeu. Le chemin natif des placements attend encore un mesh
gouverné et refuse les nouvelles références source ; l'héritage implicite de référence
dans Studio reste à corriger. L'UI complète, les autres familles, les profils et les
performances restent à qualifier. Les dix critères généraux Gate 7 restent ouverts.

## 26 octobre — Consommation native des objets source

Le jeu de module consomme les objets API 7 via la géométrie, les matériaux et les
collisions du moteur commun. Le rejeu privé `module-object-runtime-native-003`
affiche deux bâtiments source dans un module indépendant et passe ses six étapes,
avec ressources GPU réelles et captures inspectées. La couverture HLOD suit les
originaux détaillés ; leur agrégation n'est pas qualifiée. Studio, autres adapters,
parité et performances restent à terminer. Voir le suivi Gate 7 et le catalogue
central `module-object-runtime-validation-001.json`. Les dix critères généraux
restent ouverts ; aucune clôture ni publication n'est déduite de ce raccord.


## 26 octobre — Propriétaire commun du terrain et du cache de patches

Le matériau, les textures, le CDLOD, normales/AO et remplacement sont maintenant
dans `forge_engine::source_terrain`, avec le cache de patches de la référence.
Le codec et les contrôles de capture/liaisons sous l'eau ont aussi leurs propriétaires
communs. Les anciens modules et façades sont retirés. ADR-0053 et le catalogue
`shared-terrain-owners-validation-001.json` rapprochent la qualification locale.

Suite exigée : embarquer et admettre des ressources terrain explicitement liées
à la zone, puis utiliser ce propriétaire dans le viewport canonique et le jeu de
module, avec leurs barrières GPU/physiques. Le matériau auteur courant ne devient
pas le matériau source par ce transfert. Aucun critère général n'est fermé ;
les autres extractions, le parcours Studio, profils et comparaison restent dus.

## 26 octobre — Ressources terrain fermées, API 8

ADR-0054 lie explicitement les ressources d'une zone au hash de son descripteur
raster complet. Le lecteur reprend le codec commun et refuse dérive de repère,
pin, fermeture ou licence. Une géométrie compatible sans liaison ne lit rien.
Le réexport depuis l'origine conserve les ressources après retouche canonique.
Le catalogue `module-terrain-resources-validation-001.json` vérifie les entrées,
2 751 tests du workspace principal, 126 de livraison, un essai privé réel et
25 frontières, Clippy natif/Web/livraison et format. Les ressources sources sont
revérifiées ; le lot privé occupe environ 272,4 Mio / 58 fichiers, sous son budget.

Suite : transport Studio puis raccord effectif au terrain commun, en conservant
peinture, relief, collisions et admission GPU. Le matériau auteur reste courant ;
aucun rendu ni gain de performances n'est déduit du succès de transport/réexport.
Les dix critères généraux restent ouverts.

## 26 octobre — Terrain : autorité native et acquisition commune

Le bilan `module-terrain-transport-validation-001.json` qualifie l'ajout au canal
de ressources existant, au cache commun et au lecteur `LoadedModuleZone::terrain`.
Le paquet réel traverse 713 requêtes wire initiales ; cache et retouche utilisent
ensuite une description et la même allocation. Annulation, ancienne tête et
description vide sont contrôlées. Clippy natif/Web/livraison, format, 2 752 tests
principaux, 126 de livraison et l'essai privé passent ; les entrées sont stables.
La fixture mutable occupe 222 633 007 octets / 58 fichiers, sous 512 Mio / 1 024.

Prochaine intégration : faire consommer ce paquet par le terrain commun dans le
viewport et le jeu, avec peinture canonique, relief, collisions et gardes GPU.
L'acquisition compilée pour Web n'est pas encore un parcours WebView exécuté.
Les effets, sélection Nature, profils et comparaison restent exigés ; les dix
critères généraux ne sont pas fermés.

## 27 octobre — Peinture canonique dans le terrain source commun

La scène terrain commune sait maintenant conserver les retouches explicites du
document. Elle reprend l'évaluateur de poids auteur et les calculs WGSL de palette
existants, partagés avec le matériau PBR. Le champ recadré est borné à 128 Mio,
garde les espaces neutres et la diagonale du raster ; la géométrie et les collisions
ne sont pas remplacées par une autre source. Le programme sans retouche conserve
ses octets, et aucune liaison texture supplémentaire n'est déclarée.

`module-terrain-paint-validation-001.json` qualifie 2 757 tests principaux,
Clippy natif/Web, format, 25 frontières, sonde CPU et captures GPU inspectées.
L'image sans peinture est identique octet pour octet à la référence ; 459
échantillons changent 48 825 pixels localisés. Le module de régression passe ses
six étapes avec les captures inspectées, sur les mêmes entrées stables. Les
échecs intermédiaires de fixture et de chargement du shader sont conservés.

Le raccord au viewport canonique et au jeu reste l'étape suivante : consommer
les ressources déjà acquises, réutiliser le CDLOD/cache commun, conserver relief,
collisions et publication GPU, puis rejouer l'édition réelle. Ce succès de scène
commune ne ferme aucun des dix critères généraux ni la comparaison de performances.

## 27 octobre — Raccord natif au profil terrain commun

Le jeu utilise maintenant `source_terrain::host` pour une zone qui déclare son
profil terrain et son environnement. Le host reprend la scène, le CDLOD et le
cache moteur ; les cellules gardent partition et collisions, sans leur ancien
dessin. Les ressources viennent du paquet et de la scène admis, sans recherche
d'une carte ou bibliothèque globale. Le visiteur composé existant est partagé
avec les surfaces éclairées, pour que le terrain déclare base et extension.

Le catalogue `module-source-terrain-runtime-validation-001.json` qualifie un paquet
API 8 réel : 57 patches, 13 textures, 5 buffers et physique synchronisée, avec
captures inspectées. Référence et peinture gardent leurs PNG exacts ; le module
sans profil source passe aussi. Les entrées stables passent 2 764 tests principaux,
87 tests du consommateur, 126 de livraison, Clippy natif/Web et 25 frontières.
Les refus de lecteur de dossier, chemin logique réanalysable et inventaire vide
restent conservés, avec les corrections et le rejeu réussi.

Suite : raccorder le viewport Studio à ses ressources acquises et au même host,
traiter source remplacée et portée vide/sans origine, préserver les retouches et
gardes physiques/GPU, puis qualifier caméra, édition réelle et performances à
conditions identiques. Les autres extractions et critères de distribution restent
dus ; aucun des dix critères généraux n'est fermé par ce démarrage natif.

## 27 octobre — Terrain Studio et réutilisation pendant la peinture

Le vrai Studio du candidat 004 qualifie annuler/rétablir peinture et relief,
rotation de caméra, aperçu continu abandonné et sauvegarde/réouverture :
`studio-source-terrain-webview-004.json`. Le refus historique à l'annulation de
peinture reste conservé ; les imports du programme terrain sont maintenant
stables entre peint et vide. Ce parcours est Windows, sur les sources 015.

La tranche suivante conserve les ressources du terrain pour une peinture seule,
sur les mêmes propriétaires moteur et le même matériau. Le paquet admis et tous
les champs hors peinture/révision doivent rester identiques ; les autres changements
passent par la préparation complète. Les gardes GPU/physiques restent exigés.
Le contrôle natif sur les sources 016 conserve les handles et des PNG exacts,
avec Clippy natif/Web et 25 frontières. La suite principale termine avec
2 778 PASS, 68 ignorés et 144 suites sur des entrées inchangées
(`studio-source-terrain-code-checkpoint-003.json`). Le rejeu du Studio 005 passe :
peinture sur relief, annuler/rétablir, sauvegarde et réouverture, avec retour à
l'archive initiale exacte. La correction suivante touche uniquement un test
HLOD bloquant sur l'unique worker Linux : attente coopérative, mêmes assertions
et échéances. Les sources 017 passent les mêmes 2 778 tests Windows et Clippy
strict. `studio-source-terrain-current-validation-001.json` vérifie les entrées
stables, jeu release et WASM identiques, shell Studio identique hors 24 octets de
métadonnées debug identifiés. Linux passe 192 tests monde et 920 tests moteur ;
ses builds release continuent. Les coûts diagnostiques ne qualifient
pas les performances release appariées, qui restent exigées avec les autres
capacités, profils et critères de distribution.

Le contrôle natif `multizone-native-001/` passe ensuite A→B→A→B par quatre
lancements du même exécutable release. Les deux zones du paquet immuable ont des
inventaires distincts et retrouvent exactement leur état sauvegardé, y compris
positions, consommation de l'objet et reçus. Les quatre captures sont inspectées.
Le rapprochement `R7-03-saves-assessment-001.json` associe ce parcours aux tests
passés du stockage réel : séparation intermodules, identifiants locaux répétés,
refus d'une sauvegarde changée et staging tronqué préservant le fichier validé et
l'autre zone. R7-03 est qualifié localement sous Windows ; le voyage entre zones
dans une même session n'est pas une exigence de ce critère de sauvegarde et
n'est pas revendiqué. Neuf autres critères restent ouverts.

Le rejeu Nature du candidat 005 révèle ensuite un clic non consommé : le host
commun affiche les plantes, mais le sélecteur reste installé seulement dans
l'ancien viewport capturé. Le raccord des sources 018 réemploie le passage ID
et les instances publiées du host, avec le shader/buffers du matériau monde et
les occluders opaques réels. ADR-0043 décrit les propriétaires et les refus.
Clippy natif/Web, format, 25 frontières, 2 780 tests principaux et le shader privé
épinglé passent sur 2 528 entrées stables (`studio-source-terrain-code-checkpoint-004.json`).
Le candidat Studio 006 compile avant son rejeu Nature. Linux reprend le protocole
après synchronisation des deux lockfiles natifs, sans changement de versions.
Les essais High/Low sur Ubuntu sans SDK restent refusés à l'échéance graphique
du bootstrap du ciel logiciel ; les gardes restent inchangés.

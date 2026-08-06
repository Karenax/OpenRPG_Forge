# État du projet — OpenRPG Forge

Dernière mise à jour : 5 août 2026.

## Gate actuel

**MVP solo validé ; G7 Creator Core, G8 Aurora Foundation, G9 Network Beta, G10 Live Director et
G11 Contenu moderne fermés. AV4B livre maintenant une première carte 3D extérieure jouable,
exportable et visuellement vérifiée ; AV1 ferme le stockage installable du Vault et AV2 sécurise le
cycle de vie des acquisitions ainsi que leur normalisation 3D.**

Les fichiers de contexte, les règles persistantes, le backlog et les ADR initiaux sont intégrés. La
solution C# cible .NET 10, l’application Forge se charge dans le moteur 3D et le runtime reconstruit la
zone versionnée « Crypte des Cendres ». Le parcours jouable couvre le déplacement, l’interaction
avec Elyra, la clé, la porte, le combat data-driven, la phase 2 ForgeScript du boss, la quête, la
sortie, l'export, la sauvegarde et la reprise.

Forgotten Forest reconstruit une zone extérieure canonique 128 × 128 m : relief 33 × 33, sentier,
ruisseau, quatre recettes de biome, 357 instances MultiMesh, camp, pont, tour et sanctuaire. Les 22
modèles Kenney Mini Forest sont importés individuellement depuis le Vault sous licence CC0-1.0 avec
hash, manifestes, palette externe, crédits et provenance. Le runtime offre une caméra orbitale et un
déplacement relatif à la caméra. Quatre segments d’eau animés exposent nage et courant ; un
`NavigationRegion3D` de 1 990 polygones bloque 58 triangles de rivière hors traversée et relie le
camp au sanctuaire par un chemin réel de 72 points. Le smoke termine dialogue, collecte, passage,
boss et quête.

Le lancement interactif direct est validé sur le workspace scindé `1.16.0` fourni avec le
dépôt. `Run-Prototype.ps1` compile le runtime avant ouverture, journalise la session, et une erreur
de chargement produit maintenant un écran de diagnostic OpenRPG Forge plutôt qu'une fenêtre grise.
Le smoke `runtime-plain-launch` protège ce chemin sans argument, indépendamment des tests `.orpmod`.
`Run-ForgottenForest.ps1` authorise de nouveau la carte et ses assets de manière idempotente, puis la
lance en Forward+ ; l’archive de 75 fichiers est validée et rejouée par le gate depuis son cache.

Le graphe d’architecture transversal est conservé comme outillage local, déterministe et sans
dépendance externe. Ses 18 tests et son contrôle de fraîcheur font maintenant partie de la
vérification globale du dépôt, en plus de leur workflow CI dédié.

Le dossier Aurora 2026 est conservé comme snapshot immuable. Les 717 capacités possèdent maintenant
un gate cible ; les 76 P0 Core sont auditées avec preuves dans le tracker projet. Le premier
incrément Creator prouve en plus un second module Core « Les Îles Brisées », créé par la commande de
nouveau module, exporté en `.orpmod` et démarré en headless sans profil de combat ni contenu propre à
la Crypte. G7-02 ajoute une coque Creator à trois panneaux, neuf workspaces stables, un explorateur
hiérarchique, une sélection partagée et des diagnostics cliquables. G7-03 rend le workspace
Blueprints opérationnel : palette recherchable, édition transactionnelle, dérivation depuis une
instance et références inversées recalculées depuis le projet.
G7-04 ajoute la manipulation directe du Monde : palette glissable, validation de cellule avec
fantôme coloré, sélection dans le viewport et translation X/Z par gizmo avec historique partagé.
G7-05 sépare les zones en documents versionnés, partage un contexte de zone active entre
l’explorateur, l’aperçu et les commandes, puis ajoute l’assistant « Zones & transitions ».
G7-06 ajoute un clipboard sémantique, le collage et la duplication d’entités avec UUID neufs, ainsi
que la copie complète ou gabarit d’une zone avec remapping contrôlé de ses transitions.
G7-07 ajoute un index de recherche global dérivé, les références inversées Core et un renommage
transactionnel prévisualisé qui conserve les UUID du projet, module, zones, blueprints, dialogues et
quêtes.
G7-08 rend configurables les métadonnées de publication localisées et les variables globales typées,
les a introduites au format 0.8, les migre dans le workspace 1.3, les indexe dans la recherche et branche le runtime sur un blackboard
validé.
G7-09 sépare les palettes Standard et Custom : packs installés en lecture seule, contenu projet,
généré, favori et temporaire partagent un catalogue canonique, des filtres Aurora et une provenance
versionnée avec auteur, SPDX, hash, modifications et origine IA.
G7-10 remplace la caméra de prévisualisation figée par une caméra auteur orbit/pan/zoom, focus,
projections orthographiques, pilote WASD, vitesse et bookmarks persistés hors du module.
G7-11 introduit un cycle de vie Core indépendant du moteur : `ProjectLoaded`, `AreaEntered`,
`WorldStarted` puis les paires `AreaExited`/`AreaEntered` sont typés, versionnés, ordonnés et
observables avec autorité, cause, acteur, transition, provenance et bindings ForgeScript stables.
G7-12 ajoute un cache de bytecode ForgeScript vérifié, un graphe de dépendances versionné, le calcul
transitif de l'impact d'un changement et les dépendances de packs automatiques. L'espace Scripts
affiche désormais les ressources à recalculer ou réutiliser avant l'export.
G7-13 ajoute cinq profils canoniques Editor, Client, DedicatedServer, Test et Shipping, un registre
versionné des dépendances tierces, l’écran Aurora Validation & distribution, un NOTICE généré et le
blocage des cibles distribuables lorsque licence ou provenance reste inconnue.
G7-14 ajoute une navigation Core par zone avec quatre tailles/modes d’agents, portes dynamiques,
liens hors-maillage, rebuild partiel, validation géométrique et reachability depuis les spawns. Son
écran Aurora configure les données et superpose le bake au viewport sans faire de la scène la source
de vérité.
G7-15 unifie les diagnostics sous un contrat canonique à UUID, sévérité, chemin, sujet, propriété,
source et provenance. La coque Aurora cible le document concerné et propose des correctifs guidés
prévisualisables, transactionnels et annulables ; CLI et MCP exposent le même diagnostic structuré.
G7-16 charge un catalogue d’assets v1 de 605 entrées avec UUID stable, hash, licence, provenance et
disponibilité. L’espace Assets, le CLI et MCP partagent recherche à facettes, aperçu et
prévisualisation d’un import gouverné sans écriture ; la mise en page nominale est validée visuellement.
G7-17 ajoute un catalogue de localisation canonique au module, extrait métadonnées, dialogues et
quêtes, résout les replis par langue et offre édition, pseudo-localisation, dry-run et historique
transactionnel dans une surface Creator dédiée.
G7-18 complète l’échelle auteur : valeurs X/Y/Z, mode uniforme, Ctrl + molette, contraintes,
validation géométrique et commande atomique multi-entités avec historique partagé.
G7-20 ferme la productivité de la coque : filtres multi-types persistés et réappliqués au viewport,
isolation et fantômes, placement déterministe groupé, zones ouvertes comparables, aide locale sourcée
et sept profils métier. Son smoke moteur restaure projet, profil et historique après vérification.
G7-19 ajoute un A* Core déterministe avec obstacles dynamiques, coûts, récupération des extrémités,
budget de recherche, instrumentation et invalidation incrémentale affichés par l’analyse Navigation.

## État prouvé

- dépôt Git configuré sur `main` et synchronisé avec `https://github.com/Karenax/OpenRPG_Forge.git` ;
- Windows 11 Professionnel 64 bits, build 26200 ;
- Git `2.55.0.windows.2` disponible ;
- SDK .NET `10.0.302` installé et épinglé par `global.json` ;
- moteur 3D .NET requis installé et versionné ;
- CMake n'est pas requis ; le build natif emploie Zig 0.16.0 épinglé et Blender LTS 4.5.10 est
  disponible comme worker de création optionnel ;
- solution de 25 projets restaurée et compilée en Release sans avertissement ni erreur ;
- 705 tests .NET, 24 tests Python généraux et 22 tests Python du pipeline d'assets réussis ;
- snapshot Aurora validé sur 74 hashes, tracker projet déterministe de 717 lignes et matrice de
  gates contrôlés par le gate global ;
- splash OpenRPG_Forge configuré et validé en PNG dans les deux applications, barres de titre et
  icônes des fenêtres Forge personnalisées puis contrôlées par le smoke test et une capture ciblée ;
- application Forge chargée, projet ouvert, aperçu reconstruit, placement/transformation/suppression/undo/redo exécutés puis sauvegardés en mode headless ;
- assistant Nouveau module validé : choix Intérieur/Extérieur, dimensions 2D, agrégat module, zone
  d’entrée, sol, spawn et workspace atomique indexé au format `1.16.0` ; le gabarit extérieur active
  son heightmap canonique (smoke dédié 96 × 80), avec lecture/migration testée des agrégats
  `0.6.8` à `0.6.0`, `0.5.0`, `0.4.0`, `0.3.0`, `0.2.0` et `0.1.0` ;
- propriétés locales d’instances validées : valeurs texte, booléen, entier et nombre sous schéma v1,
  édition visuelle, sérialisation, commande transactionnelle, dry-run, audit, undo/redo et smoke ;
- Play from here validé : spawn sélectionné, snapshot de l’état courant, lancement runtime, retour
  des logs et suppression du snapshot temporaire ;
- domaine narratif validé : dialogues branchés, conditions/actions typées, localisation, validation
  de graphe, parcours, quêtes à états/transitions, journal et détection des transitions invalides ;
- éditeur Dialogues Forge validé : sélection et ajout de nœuds, choix, condition, cible, localisation,
  aperçu, commande transactionnelle, audit et annuler/rétablir ;
- projet de démonstration byte-identique avant et après le scénario d’édition automatisé ;
- runtime headless : dialogue d’Elyra, quête canonique à quatre états, conditions objet/variable,
  16 entités et 10 collisions statiques chargées, porte déverrouillée, phase 2 déclenchée et quête terminée ;
- combat data-driven : statistiques, factions/équipes, capacités, coûts/cooldowns, effets, indices
  visuels et butin déterministe ;
- ForgeScript natif : AngelScript 2.38.0, bytecode/versioning, API allowlistée, quotas et scripts du
  Gardien validés ;
- format `.orpmod` : manifeste, hash, export atomique, validation hostile, extraction sûre, cache
  isolé et chargement runtime ;
- distribution G7-13 : manifest 1.1 avec cible, cinq profils à UUID/schéma/provenance, références de
  secrets sans valeurs, registre tiers, conformité CLI, NOTICE généré et refus Shipping des inconnues ;
- sauvegarde `.orpsave` 1.1.0 : delta de progression et snapshot météo atomiques liés au module/hash,
  checkpoint créé dans un processus puis repris et terminé dans un second ;
- host MCP officiel 1.4.1 sur stdio : résumé/révision/diagnostics, plan/diff/apply/inverse/audit de
  dialogue, ForgeScript et agent de scénario ; pont par pipe `CurrentUserOnly`, jeton 256 bits et
  reconnexions testés ;
- export `.orpmod` disponible depuis l'interface Forge et depuis le CLI ;
- démarrage headless de référence : 165,398 à 225,207 ms sur cinq exécutions, 98 nœuds ;
- second module générique « Les Îles Brisées » : deux entités, une collision, PlayerSpawn sans
  dépendance au combat RPG, interaction vide propre, export/validation/chargement `.orpmod` ;
- coque Creator G7-02 : workspaces Monde, Blueprints, Dialogues, Quêtes, Scripts, Assets, Validation,
  Playtests et Assistant, explorateur virtuel, inspecteur partagé verrouillable et navigation depuis
  un diagnostic vers sa cible ;
- catalogue Blueprints G7-03 : recherche par nom, slug ou type, création/édition par commandes avec
  dry-run et historique, conversion d’une instance en nouvelle définition sans perte des overrides,
  puis index des usages dans les entités et les locuteurs de dialogues ;
- manipulation Monde G7-04 : drag-and-drop depuis une palette intégrée, projection sur le sol,
  snap, limites et occupation validés avant commande, fantôme vert/rouge, sélection et translation
  directe avec gizmo X/Z, undo/redo du document canonique ;
- zones G7-05 : document de zone JSON 1.0 séparé et adressé par hash, index workspace 0.7, migration
  legacy, contexte actif, environnement/dimensions, création vide ou jouable, `PortalLink` typé,
  diagnostics localisés et workflow intégré avec undo/redo ;
- clipboard G7-06 : payload sémantique versionné, collage et duplication d’entités avec nouveaux
  UUID, validation spatiale, aperçu et historique ; copie complète ou gabarit d’une zone, remapping
  des ancres et transitions internes, politique explicite pour les références externes et round-trip ;
- recherche G7-07 : index plein texte accent-insensible avec filtres de type et de tag, chemins et
  provenance, usages inversés des références Core, ciblage dans la sélection partagée et renommage
  sûr prévisualisé avec dry-run, validation, UUID stable, undo/redo et round-trip ;
- configuration G7-08 : publication localisée avec auteur, version, licence, image, tags,
  classification et crédits ; variables à UUID, type, valeur par défaut, scope et provenance ;
  éditeur à aperçu obligatoire, migration du format workspace 0.8 vers 1.3, package versionné et blackboard runtime ;
- palettes G7-09 : catalogue unique filtrable par pack installé, projet, généré, favori et temporaire,
  packs verrouillés, provenance versionnée visible, recherche, dry-run, historique et round-trip ;
- caméra G7-10 : profil auteur UUID/schéma v1, orbite, panoramique, zoom adaptatif, focus, vues
  cardinales, vitesse, pilote clavier, bookmarks, persistance atomique et historique séparé ;
- cycle de vie G7-11 : événements projet, monde et zone à UUID/schéma v1, ordre déterministe,
  autorité, cause, contexte ancien/nouveau, acteur, transition, dry-run, diagnostics et observation
  dans le runtime Core ;
- build G7-12 : cache ForgeScript par hashes source/API/compilateur, contrôle d'intégrité, graphe de
  ressources UUID/schéma v1, impact transitif, état jetable, dépendances de packs automatiques,
  analyse CLI JSON et écran Aurora Scripts & Build ;
- distribution G7-13 : profils Editor/Client/DedicatedServer/Test/Shipping persistés, registre de
  dépendances avec version/licence/propriétaire/justification/cadence/retrait, commande dry-run,
  historique, écran Aurora Validation, export ciblé, NOTICE et gate de licence ;
- navigation G7-14 : configuration de zone 1.1, profils Petit/Moyen/Grand/Volant, portes selon trois
  états, liens Saut/Porte/Vol, grille et fingerprint déterministes, plan de rebuild partiel, cache
  dérivé versionné, reachability spawn-vers-objet, validation géométrique CLI et overlay 3D ;
- diagnostics G7-15 : modèle canonique v1, UUID stable, sévérité, chemin, UUID sujet, propriété,
  source et provenance, navigation partagée, correctifs guidés spatiaux, dry-run, commande, patch,
  inverse, audit, undo/redo, persistance, CLI et MCP structurés ;
- guide créateur, licences/provenance et rapport de performance livrés ;
- rendu 1280 × 720 inspecté et scripts de lancement interactif validés ;
- Python `3.13.14` disponible pour le graphe d’architecture.

Le rapport détaillé est dans `docs/ENVIRONMENT.md`.

## Limites actuelles

- le gizmo Monde couvre la translation sur le plan X/Z mais pas encore rotation, échelle, pivot,
  axes contraints, coordonnées locales/globales, alignements ou multi-sélection ;
- le clipboard n’expose pas encore couper ni la copie multi-sélection depuis le viewport, et la
  duplication de zone ne porte pas les futurs documents terrain, navigation, éclairage ou audio ;
- la recherche globale couvre les documents Core canoniques existants et les diagnostics ; le
  navigateur Assets possède son index spécialisé, tandis que son intégration à la recherche globale,
  les graphes auteur, terrains, navigation et audio attend leurs schémas versionnés ;
- les scopes joueur, groupe et serveur sont définis mais leur partition runtime attend le Network
  Kit ; les anciens modules restent autorisés à employer temporairement des clés non déclarées ;
- les transitions de zone sont éditables, persistées et validées et leur contrat d'événements est
  prêt ; le runtime ne reconstruit toutefois pas encore la scène de destination, et orientation,
  hooks, streaming et génération automatique du lien inverse restent à faire ;
- la coque expose les neuf workspaces et relie Monde, Blueprints, Dialogues, Scripts, Assets et
  Validation ; les catalogues Blueprints et Assets ainsi que la conformité de distribution sont
  opérationnels, tandis que les éditeurs complets Quêtes et Assistant relèvent encore des gates
  suivants ; Scripts possède l'analyse de build et de cache mais pas encore l'éditeur de code complet ;
- les diagnostics sont désormais structurés et navigables, mais les profils de validation, filtres,
  catégories, bundle d’export et correctifs propres aux futurs imports d’assets restent à livrer ;
- les visuels finaux du MVP sont des primitives procédurales originales ; ils ne constituent pas
  encore une direction artistique de production avec modèles, animations, audio et VFX finis ;
- le pont MCP sécurisé est implémenté et testé, mais son rattachement permanent au cycle de vie de
  l'éditeur et les permissions interactives restent à faire avant un usage multi-processus général ;
- le catalogue de blueprints ne gère pas encore versions, paramètres exposés, miniatures,
  namespaces et registre réel de packages, tags ou profils de compatibilité ; les éditeurs complets de quêtes
  et autres agrégats restent à livrer pour des modules entièrement originaux ;
- la branche RPG du runtime de démonstration contient encore des comportements spécifiques à Elyra,
  la clé, la porte et le boss ; le démarrage Core générique est séparé mais l'extraction complète
  attend les incréments RPG Kit ;
- le Blender Worker exécute désormais un round-trip réel gouverné et le serveur autoritaire possède
  sa bêta testée ; la surveillance d'assets continue et le déploiement multijoueur public de
  production restent à livrer ;
- le premier Asset Vault externe contient 555 assets CC0 Poly Haven/ambientCG et 50 packs 3D Kenney,
  soit environ 5 236 modèles logiques estimés ; Forgotten Forest en consomme désormais une sélection
  cohérente de 22 GLB. Les packs locaux possèdent maintenant registre v1, stockage SHA-256
  dédupliqué, backup de désinstallation et crédits agrégés. Une ressource ambientCG réelle franchit
  désormais normalisation, collisions, LOD, miniature et audit ; le traitement massif du catalogue
  complet reste à étendre ;
- Forgotten Forest est jouable sur le terrain générique avec végétation, bâtiments, éclairage,
  caméra, quête et export. Un bake de navigation avancé, une eau simulée, des animations, de l’audio
  et des VFX de production restent nécessaires avant de qualifier la carte de contenu final ;
- le workflow CI est défini, mais son exécution distante attend le premier push.

## Décisions verrouillées

- Le moteur 3D .NET versionné dans l’environnement est retenu pour le MVP.
- C# est le langage principal ; Rust n’est pas dans le cœur du MVP.
- Le toolset commence comme extension de l’éditeur 3D standard, sans fork.
- `Forge.Domain` et `Forge.Application` restent indépendants du moteur.
- ForgeScript repose sur AngelScript via une GDExtension C++ minimale.
- Les modules communautaires utilisent le format propre `.orpmod`.
- L’IA passe par MCP et le bus de commandes transactionnel.
- Les surfaces produit utilisent l’identité OpenRPG Forge et un vocabulaire indépendant du moteur.
- Le MVP est la tranche verticale « Crypte des Cendres ».
- Le premier serveur réutilisera le runtime 3D en mode headless autoritaire.

## Décisions encore ouvertes

- nom commercial définitif ;
- licence du code propre (`Apache-2.0` ou `MIT OR Apache-2.0` proposés) ;
- charte graphique finale ; la direction UX sombre/braise et le contexte 3D provisoire sont déjà
  documentés dans `docs/UX_AURORA_2026.md` ;
- validation de la vue 3/4 rotative comme vue prioritaire du MVP ;
- règles RPG initiales originales et minimales ;
- cible matérielle définitive après mesures interactives GPU ;
- consommation runtime autoritaire des bakes de navigation et navigation de foule ;
- éventuel service de persistance externe après le MVP solo.

## Prochaine tâche unique

Le second module original **La Veillée des Éclats** est généré par 17 commandes d'auteur puis
persisté via `ProjectSession`; son round-trip et son export `.orpmod` sont couverts automatiquement.

L'audit **G11 Contenu moderne et qualité de distribution** couvre 99/99 exigences dans 20 lots bornés.
**G11-A est fermé** : profils textures/modèles persistés canoniquement, commande d'auteur avec
dry-run et undo/redo, plan/cache déterministes et traitement réel d'un GLB Kenney et d'une texture
ambientCG sans modification du Vault. Le Content Browser expose ces réglages dans la même surface
que la licence et la provenance.

**G11-B est fermé** : manifeste content-addressed persisté et éditable, chunks/plateformes, collision,
animation, VFX et LOD, inclusion `.orpmod`, contrôle runtime de présence et SHA-256, surface auteur
transactionnelle revue visuellement.

**G11-C est fermé** : round-trip externe persistant et transactionnel, worker Blender typé
optionnel, parité plateforme, migrations/désinstallations analysées en dry-run et production réelle
  d'un GLB 2 texturé depuis un OBJ du Vault. Blender 4.5.10 a produit collisions, LOD, miniature
  locale déterministe et audit qualité, le hash du source est resté inchangé et le rendu de contrôle
  est valide.

**G11-D est fermé** : canaux Development/Preview/Stable, matrice Windows/Linux/macOS, signature
ECDSA avec confiance/révocation, delta content-addressed reprenable et réversible, bundle Windows
reproductible, installation journalisée et désinstallation isolée. Les exports applicatifs physiques
Linux/macOS, Authenticode et feeds distants restent explicitement aux adaptateurs de livraison.

**G11-E est fermé** : l’espace Données persiste des tables typées et gouvernées, prévisualise puis
exécute les imports/exports en masse, produit des packs de blueprints reproductibles et expose une
fusion à trois voies qui conserve les deux variantes conflictuelles.

**G11-F est fermé** : le profil éditeur v2 applique échelle UI, taille de texte et chaîne de polices,
gère plusieurs onglets de zone, comparaison protégée, favoris personnels et récents. Le module porte
un profil artistique transactionnel et des avertissements d’échelle, densité texel, shader, époque,
réalisme et palette.

**G11-G est fermé** : profil de zone PBR/HDR, éclairage Forward+ par pixel, tone mapping, exposition,
budgets de lumières et d'ombres, ciel physique/procédural/panorama et migrations sont partagés par
l'aperçu Creator et le runtime.

**G11-H est fermé** : vent global/local, états météo autoritaires, pluie/neige procédurales, eau,
zones de réverbération et sauvegarde météo 1.1 partagent un modèle canonique, la commande auteur et
les adapters Creator/runtime.

**G11-I est fermé** : chaque zone peut porter un terrain générique versionné, des matériaux et
biomes indépendants, des trous, routes/rivières/ponts, des volumes de vol/nage/espace, des routes
aériennes et un profil de chargement asynchrone. Le Creator et le runtime reconstruisent le même mesh,
ses collisions et overlays ; l'auteur applique le profil par commande transactionnelle et les formats
zone `1.4.0`, projet `0.6.11` et workspace `1.5.0` migrent les données absentes.
L’onglet Sculpture ajoute désormais sept opérations canoniques — lever, abaisser, aplanir, lisser,
peindre un matériau, ouvrir et refermer — avec prévisualisation, dry-run, persistance et Undo/Redo
par trait. La peinture est un masque sparse versionné consommé par le même adapter de rendu.
Les formats courants sont désormais zone `1.9.0`, projet agrégé `0.6.22` et workspace scindé `1.16.0` ;
les documents antérieurs migrent vers un masque vide qui conserve le rendu par recettes de biome.

**G11-J est fermé** : le module et chaque zone portent des timers bornés, les règles de repos et la
restauration d'abandon d'une séquence. La fenêtre Rythme, repos & séquences applique module et zone
en une transaction ; le runtime avance les timers et accepte `R` selon la règle canonique. Les
formats zone `1.5.0`, projet `0.6.12` et workspace `1.6.0` sont migrés. G11-K porte maintenant les
séquences/cutscenes, ancres caméra et transforms interpolés.

**G11-K est fermé** : séquences, flux de démarrage, ancres caméra, overrides et tweens partagent un
contrat persistant entre studio auteur, runtime et MCP.

**G11-L est fermé** : nage/plongée, escalade, roue d’actions, contrôle tactique du temps et
fournisseurs de combat sont configurables et consommés par le runtime joueur.

**G11-M est fermé** : progression, difficultés et mutateurs sont configurables au module ;
perception et combat sont configurables par zone. RPG Kit applique les modificateurs de sens,
Network Beta décide le PvP sous autorité serveur, ForgeScript lit des snapshots sûrs et le runtime
sauvegarde expérience et niveau en `.orpsave` 1.2. Les formats zone `1.7.0`, projet `0.6.15` et
workspace `1.9.0` sont migrés. G11-N porte maintenant audio spatial/événementiel, patrouilles
inter-zones et chargement.

**G11-N est fermé** : chaque zone configure cues, scatter spatial déterministe, voix localisées et
consenties, patrouilles inter-zones ainsi qu'image, vidéo, conseils et progression de chargement.
Le Creator applique le profil par commande annulable ; ForgeScript et MCP reçoivent des APIs
gouvernées ; le runtime relie l'overlay au streaming réel et joue les ressources importées en 3D.
Les formats zone `1.8.0`, projet `0.6.16` et workspace `1.10.0` sont migrés. G11-O porte maintenant
les règles data-driven, l'UI scriptable et la sérialisation runtime.

**G11-O est fermé** : le module configure schémas de classes/progression/armes/VFX/incantation,
documents UI, événements typés, JSON runtime et politique de snapshots. Le workspace **Données**,
le runtime, ForgeScript et MCP partagent la commande canonique. La Crypte affiche un HUD déclaratif
alimenté par JSON. Les formats projet `0.6.17`, workspace `1.11.0` et sauvegarde `.orpsave` `1.3.0`
sont migrés. G11-P porte maintenant copie runtime, indicateurs, NUI, compilateur et documentation.

**G11-P est fermé** : la copie runtime remappe UUID, ownership, références et état sous autorité ;
les AOE disposent d’une sélection déterministe et d’un mesh coloré ; le HUD utilise NUI 2.0 avec
formulaires, listes, canvas, accessibilité, localisation et inspecteur. ForgeScript compile par lot
avec cache incrémental, diagnostics stables, télémétrie et conformance. Six documents sont générés
et contrôlés par CLI/MCP/gate. Les formats projet `0.6.18` et workspace `1.12.0` sont migrés.

**G11-Q est fermé** : les tâches agentiques sont bornées par objectif, scope, interdictions,
acceptation, propriétaire et dépendances ; les crash artifacts expurgent les secrets et portent
version, empreinte et reproduction ID. Le Creator présente six domaines de santé, journalise les
autosauvegardes et propose une récupération comparée. Le client de contenu reprend, vérifie et
déduplique sous contrôle de confiance, licence et stockage utilisateur. Les formats projet `0.6.19`
et workspace `1.13.0` sont migrés.

**G11-R est fermé** : le laboratoire qualité Creator, CLI, runtime et MCP partage une empreinte
déterministe. Il mesure la couverture par langue, bloque placeholders et pluriels incohérents,
produit un aperçu pseudo-localisé, compile et exécute les scripts natifs sous quotas, parcourt les
dialogues et quêtes avec fixtures et simule neuf rencontres par zone peuplée avec navigation et
nettoyage vérifiés. Les formats projet `0.6.19` et workspace `1.13.0` restent inchangés. À cette
fermeture, le tracker comptait 47 capacités `Tested` et G11-S devenait la tranche active.

**G11-S est fermé** : le laboratoire persiste ses scénarios réseau hostiles, fixtures visuelles,
benchmarks, budgets desktop/mobile/serveur et capture intégrée. Creator affiche les cinq nouvelles
cartes ; CLI, runtime et MCP partagent l’empreinte et les lectures sans mutation. Le runtime a
produit huit comparaisons réelles en deux résolutions, deux caméras et deux qualités. Les formats
projet `0.6.20` et workspace `1.14.0` sont migrés. Ce jalon comptait 52 capacités `Tested` avant la
fermeture de G11-T.

**G11-T est fermé** : Creator expose maintenant quinze cartes qualité et configure les profils
accessibilité/fuzzing sans éditer les fichiers générés. Le runtime applique taille de texte,
contraste, sous-titres et descriptions sonores ; 112 cas déterministes couvrent sept frontières avec
zéro crash inattendu et zéro évasion de chemin. Le registre tiers impose risque, état, deux rôles de
revue et audit sécurité, puis projette ces preuves dans le manifeste `.orpmod` 1.2. Les formats
projet `0.6.22` et workspace `1.16.0` sont migrés. Forgotten Forest fournit maintenant la carte 3D
jouable ; les prochains incréments portent sa finition artistique et les workflows de packs. Le tracker compte désormais 58 capacités
`Tested`.

**AV4B — tranche jouable navigable livrée** : le module `forgotten-forest` est authorisé depuis le
domaine et persisté comme workspace scindé. Son pack curaté copie 22 GLB et leur palette externe sans
modifier le Vault, écrit profils, reçus, 44 chunks de manifeste et crédits. Le runtime charge un
workspace externe ou un `.orpmod`, reconstruit relief, collisions, features et scatter déterministe,
puis instancie les modèles réels. Les tests couvrent l’authoring, l’idempotence de l’import et le
scénario jouable complet ; une capture Forward+ 1280 × 720 a été inspectée après correction du
culling, des échelles, du cadrage et du HUD. Le bake runtime dérive maintenant 1 990 polygones du
terrain canonique, applique pente et politiques de traversée, crée le lien du pont et prouve un
chemin moteur de 72 points. Le ruisseau emploie quatre surfaces shader animées, quatre volumes de
nage et un courant directionnel. La suite AV4B porte la finition audiovisuelle, puis l’intégration de
ces outils dans le Creator Aurora.

**AV1 — stockage Asset Vault fermé** : le registre externe versionné projette l’état installé dans
les 605 entrées du catalogue, Creator, CLI et MCP. Les modèles et aperçus sont copiés une seule fois
par SHA-256, plusieurs packs partagent le même blob, les originaux restent inchangés et une
désinstallation sauvegarde registre et objets avant de retirer les références actives. Les crédits
Markdown et texte sont reconstruits depuis les seuls assets installés. Trois nouveaux tests portent
la suite .NET à 705 et `ORF-CONT-027`/`ORF-CONT-030` deviennent `Tested`.

**AV2 — cycle d’acquisition fermé** : les téléchargements reprennent par HTTP Range, remplissent un
cache SHA-256 et, avec `--update`, versionnent tout nouvel original et son extraction sans écraser le
snapshot antérieur. Le diff de catalogues sépare ajouts, changements, retraits et inchangés. La
désinstallation est sèche par défaut, rejette traversal, plan altéré ou contenu modifié, puis déplace
les octets vers un backup récupérable avec reçu et registre. Vingt tests Python valident ce contrat
sans retélécharger le Vault réel. Le registre de gouvernance distingue en plus sources automatisées,
  packs manuels, catégories refusées ou différées et gate d’admission d’un nouvel adaptateur.

**AV2 — normalisation 3D fermée** : le contrat de round-trip ajoute deux opérations typées,
`generate_thumbnail` et `audit_quality`. Le worker écrit d’abord GLB et PNG dans des fichiers
temporaires, contrôle leurs en-têtes, ne les publie qu’après succès, retire les métadonnées PNG
variables et conserve un rapport d’échec exploitable sans remplacer le dernier modèle valide.
L’audit vérifie géométrie, normales, textures, matériaux, dimensions, pivot, triangle budget,
collision, LOD et miniature, puis calcule les scores licence, confiance source, technique, visuel,
performance, métadonnées et global. La preuve réelle `ambientcg:3DApple001` donne 500 triangles,
2 LOD, 1 collision, une miniature 512 × 512 revue visuellement et un score global de 99/100.

# État du projet — OpenRPG Forge

Dernière mise à jour : 3 août 2026.

## Gate actuel

**MVP solo validé, G0 fermé et G7 Creator Core ouvert — G7-19 franchi.**

Les fichiers de contexte, les règles persistantes, le backlog et les ADR initiaux sont intégrés. La
solution C# cible .NET 10, l’application Forge se charge dans le moteur 3D et le runtime reconstruit la
zone versionnée « Crypte des Cendres ». Le parcours jouable couvre le déplacement, l’interaction
avec Elyra, la clé, la porte, le combat data-driven, la phase 2 ForgeScript du boss, la quête, la
sortie, l'export, la sauvegarde et la reprise.

Le graphe d’architecture transversal est conservé comme outillage local, déterministe et sans
dépendance externe. Ses 17 tests et son contrôle de fraîcheur font maintenant partie de la
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
les persiste dans le format 0.8, les indexe dans la recherche et branche le runtime sur un blackboard
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
G7-19 ajoute un A* Core déterministe avec obstacles dynamiques, coûts, récupération des extrémités,
budget de recherche, instrumentation et invalidation incrémentale affichés par l’analyse Navigation.

## État prouvé

- dépôt Git configuré sur la branche `main`, baseline initiale locale enregistrée et origine `https://github.com/Karenax/OpenRPG_Forge.git` prête pour un futur push ;
- Windows 11 Professionnel 64 bits, build 26200 ;
- Git `2.55.0.windows.2` disponible ;
- SDK .NET `10.0.302` installé et épinglé par `global.json` ;
- moteur 3D .NET requis installé et versionné ;
- CMake et Blender ne sont pas requis ; le build natif emploie le toolchain Zig 0.16.0 épinglé ;
- solution de 16 projets restaurée et compilée en Release sans avertissement ni erreur ;
- 256 tests .NET et 24 tests Python réussis ;
- snapshot Aurora validé sur 74 hashes, tracker projet déterministe de 717 lignes et matrice de
  gates contrôlés par le gate global ;
- splash OpenRPG_Forge configuré et validé en PNG dans les deux applications, barres de titre et
  icônes des fenêtres Forge personnalisées puis contrôlées par le smoke test et une capture ciblée ;
- application Forge chargée, projet ouvert, aperçu reconstruit, placement/transformation/suppression/undo/redo exécutés puis sauvegardés en mode headless ;
- assistant Nouveau module validé : agrégat module, zone d’entrée, sol, spawn et workspace atomique
  indexé au format `1.0.0`, avec zone indépendante 1.0 et lecture/migration testée des agrégats
  `0.6.5`, `0.6.4`, `0.6.3`, `0.6.0`, `0.5.0`, `0.4.0`, `0.3.0`, `0.2.0` et `0.1.0` ;
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
- sauvegarde `.orpsave` 1.0.0 : delta de progression atomique lié au module/hash, checkpoint créé
  dans un processus puis repris et terminé dans un second ;
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
  éditeur à aperçu obligatoire, format workspace 0.8, package versionné et blackboard runtime ;
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
- Blender Worker et le serveur autoritaire ne sont pas commencés ;
- le premier Asset Vault externe contient 555 assets CC0 Poly Haven/ambientCG et 50 packs 3D Kenney,
  soit environ 5 236 modèles logiques estimés ; leur normalisation visuelle, l’interface Assets et la
  sélection Forgotten Forest restent à livrer ;
- le prototype `forge_forest_map` charge une scène 128 × 128 m, génère la forêt par chunks
  `MultiMesh`, peint forêt/clairière/fleurs/rochers/zones interdites dans un masque RGBA versionné et
  remplace les modèles depuis une ressource Godot ou le catalogue installé ; le relief, la navigation
  et la composition Forgotten Forest finale restent à livrer ;
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

Exécuter **G8 MERCHANT-01** pour `ORF-MER-012`, `019`, `001`, `002` et `003` : transaction, boutique et stock.

# État du projet — OpenRPG Forge

Dernière mise à jour : 2 août 2026.

## Gate actuel

**MVP solo validé, G0 fermé et G7 Creator Core ouvert — G7-04 franchi.**

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

## État prouvé

- dépôt Git configuré sur la branche `main`, baseline initiale locale enregistrée et origine `https://github.com/Karenax/OpenRPG_Forge.git` prête pour un futur push ;
- Windows 11 Professionnel 64 bits, build 26200 ;
- Git `2.55.0.windows.2` disponible ;
- SDK .NET `10.0.302` installé et épinglé par `global.json` ;
- moteur 3D .NET requis installé et versionné ;
- CMake et Blender ne sont pas requis ; le build natif emploie le toolchain Zig 0.16.0 épinglé ;
- solution de 16 projets restaurée et compilée en Release sans avertissement ni erreur ;
- 148 tests .NET et 22 tests Python réussis ;
- snapshot Aurora validé sur 74 hashes, tracker projet déterministe de 717 lignes et matrice de
  gates contrôlés par le gate global ;
- splash OpenRPG_Forge configuré et validé en PNG dans les deux applications, barres de titre et
  icônes des fenêtres Forge personnalisées puis contrôlées par le smoke test et une capture ciblée ;
- application Forge chargée, projet ouvert, aperçu reconstruit, placement/transformation/suppression/undo/redo exécutés puis sauvegardés en mode headless ;
- assistant Nouveau module validé : agrégat module, zone d’entrée, sol, spawn et workspace atomique
  au format `0.6.0`, avec migrations testées depuis `0.5.0`, `0.4.0`, `0.3.0`, `0.2.0` et `0.1.0` ;
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
- guide créateur, licences/provenance et rapport de performance livrés ;
- rendu 1280 × 720 inspecté et scripts de lancement interactif validés ;
- Python `3.13.14` disponible pour le graphe d’architecture.

Le rapport détaillé est dans `docs/ENVIRONMENT.md`.

## Limites actuelles

- le gizmo Monde couvre la translation sur le plan X/Z mais pas encore rotation, échelle, pivot,
  axes contraints, coordonnées locales/globales, alignements ou multi-sélection ;
- la coque expose les neuf workspaces et relie Monde, Blueprints et Dialogues ; le premier catalogue
  Blueprints est opérationnel, tandis que ses workflows avancés et les éditeurs complets Quêtes,
  Scripts, Assets, Validation et Assistant relèvent encore du Creator Core G7 ;
- les visuels finaux du MVP sont des primitives procédurales originales ; ils ne constituent pas
  encore une direction artistique de production avec modèles, animations, audio et VFX finis ;
- le pont MCP sécurisé est implémenté et testé, mais son rattachement permanent au cycle de vie de
  l'éditeur et les permissions interactives restent à faire avant un usage multi-processus général ;
- le catalogue de blueprints ne gère pas encore versions, paramètres exposés, provenance/licence,
  miniatures, namespaces de packages, tags ou profils de combat ; les éditeurs complets de quêtes
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
- navigation dynamique et rebake partiel ;
- éventuel service de persistance externe après le MVP solo.

## Prochaine tâche unique

Ouvrir **G7-05 — zones indépendantes et transitions** pour `ORF-AREA-001`, `ORF-AREA-002`,
`ORF-AREA-030` et `ORF-AREA-037` : documents de zone, contexte actif, transitions et validation.

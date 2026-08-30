# AGENTS.md — OpenRPG Forge

## Mission

Construire un moteur et un toolset de RPG 3D moderne, orienté modules et à code source disponible
pour les usages non commerciaux, inspiré par la promesse d’Aurora sans reprendre les marques,
contenus, règles ou actifs de Neverwinter Nights/D&D.

Le produit doit rester utilisable sans IA, sans abonnement et sans outil propriétaire obligatoire.

## Sources de vérité

Avant toute modification importante, lire :

1. `PROJECT_CONTEXT.md`
2. `STATUS.md`
3. `TASKS_MVP.md`
4. `docs/planning/COMPLETE_FUNCTION_BACKLOG.md`
5. `tracking/project/AURORA_PARITY_COVERAGE.csv`
6. les ADR applicables dans `docs/ADR/`
7. la documentation spécialisée du sous-système concerné

`PROJECT_CONTEXT.md` est l'unique contexte narratif courant. Ne pas recréer de méga-contexte,
README initial ou journal de livraison parallèle. Les historiques Git et les preuves liées suffisent.

Une décision enregistrée dans un ADR ne doit pas être renversée silencieusement. Proposer un nouvel ADR de remplacement.

## Gouvernance Aurora 2026

- `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/` est un snapshot de recherche immuable. Ne pas le
  modifier pour refléter l'état du projet.
- Le tracker opérationnel est `tracking/project/AURORA_PARITY_COVERAGE.csv`, généré par
  `scripts/Update-AuroraParityTracker.ps1`.
- Le plan exhaustif des 717 fonctions est `docs/planning/COMPLETE_FUNCTION_BACKLOG.md`, généré par
  `scripts/Update-CompleteFunctionBacklog.ps1`. Chaque lot `FC-*` contient une à cinq exigences.
- Avant une feature Aurora, lire son exigence source, le tracker projet et le rapport d'écart dans
  `docs/aurora-parity/`.
- Traiter un ensemble cohérent de une à cinq feature IDs. Une priorité P2/P3 ne détourne pas une
  tâche P0/P1 en cours.
- La parité est fonctionnelle et clean-room : ne copier ni code, asset, texte, règles, nom public,
  API ou format propriétaire.
- Ne pas créer de classe ou projet vide uniquement pour augmenter la couverture.
- Une ligne ne passe `Tested` qu'avec une preuve automatisée ou une procédure reproductible liée
  dans le tracker projet.
- Après une modification autorisée du tracker, régénérer puis vérifier aussi le plan exhaustif. Ne
  jamais annoncer 100 % fonctionnel tant que les 717 lignes ne sont pas `Tested`.

## Pile imposée pour le MVP

- Godot **4.7.1 .NET** comme moteur, runtime et base d’éditeur.
- C#/.NET comme langage principal.
- Bibliothèques de domaine C# sans dépendance Godot.
- Application Creator autonome Godot, sans fork du moteur. Le plugin d’éditeur historique reste
  disponible uniquement pendant la migration et ne reçoit plus de nouvelle surface produit.
- AngelScript encapsulé dans une GDExtension C++ minimale pour `ForgeScript`.
- Python uniquement pour Blender, traitement d’assets et workers IA.
- MCP Host en C# avec le SDK MCP officiel.
- JSON versionné pour les données ; GLB/glTF pour les assets 3D.
- Format de module propre `.orpmod` ; ne pas utiliser un PCK Godot non fiable comme format communautaire.
- Git et tests automatisés dès le premier lot.

Rust est autorisé uniquement après mesure ou besoin isolé clairement documenté. Ne pas introduire Rust dans le cœur du MVP.

## Architecture obligatoire

Les couches de domaine et d’application ne doivent jamais référencer Godot.

Flux autorisé :

`Studio/LegacyEditor/Runtime/MCP -> Application Commands -> Domain -> Persistence/Adapters`

L’IA, les plugins et les interfaces ne modifient jamais directement les fichiers du projet. Toute mutation passe par le bus de commandes transactionnel et produit :

- un identifiant de commande ;
- une révision de base ;
- une validation ;
- un diff prévisualisable ;
- une opération inverse ou un snapshot ;
- un journal d’audit ;
- un résultat de test si applicable.

## Frontière moteur–modules

- Un module dépend uniquement des contrats publics et versionnés de Forge ; le moteur, le runtime,
  l'application et le Studio ne dépendent jamais d'un slug, d'un tag ou d'un asset propre à ce module.
- Lorsqu'un module révèle un réglage manquant, ajouter une capacité générique au schéma, sa validation,
  son adaptation runtime et sa surface dans le Creator autonome. Ne pas ajouter un branchement spécial.
- Le workspace édité par le Creator est la source de vérité pour cartes, placements, assets et réglages.
  Aucun service de `Forge.Application` ni commande cœur de `Forge.Cli` ne doit régénérer un vrai jeu.
- Une modification de contenu sous `apps/Forge.Runtime/content/` se valide avec les validateurs et
  playtests de module ; elle ne déclenche pas à elle seule une régénération du graphe d'architecture.
- Les tests peuvent nommer un module et vérifier ses valeurs attendues, mais ne doivent pas introduire
  de comportement de production réservé à ce module. Toute nouvelle capacité est aussi vérifiée sur un
  second workspace ou par une régression de la Crypte des Cendres.

## Discipline de développement

- Une tâche bornée par branche ou worktree.
- Aucun « grand refactoring » mêlé à une fonctionnalité.
- Avant de coder, identifier les fichiers touchés et les critères d’acceptation.
- Après modification, exécuter les tests pertinents et rapporter exactement les résultats.
- Ne jamais annoncer qu’une fonction est terminée sans preuve de compilation ou de test.
- Ne jamais masquer une erreur avec un `try/catch` vide, une valeur par défaut silencieuse ou la désactivation d’un test.
- Ne pas ajouter de dépendance de production sans vérifier sa licence, sa maintenance et son utilité.
- Préférer les bibliothèques officielles ou les dépendances minimales.
- Les formats persistants et API publiques doivent être versionnés.
- Ajouter ou mettre à jour un ADR lorsqu’une décision structurelle est prise.
- Mettre `STATUS.md` et `TASKS_MVP.md` à jour à la fin d’une session ayant modifié l’état réel.

## Graphe d’architecture

Le graphe déterministe décrit les relations entre interfaces, clients, points d’entrée,
services, repositories, modèles et tests. Le code reste la source de vérité.

- avant une modification transversale, vérifier la fraîcheur avec
  `dotnet run --project tools/Forge.ArchitectureGraph -- check` ;
- interroger le sous-graphe avec
  `dotnet run --project tools/Forge.ArchitectureGraph -- query "<module ou symbole>" --mode impact --format context --depth 2 --max-nodes 30` ;
- interroger aussi les tests avec `--mode tests --format paths --depth 2 --max-nodes 40` ;
- utiliser `--format paths` pour obtenir uniquement les fichiers à examiner et augmenter les bornes
  seulement si le premier résultat est insuffisant ;
- ne jamais charger intégralement `docs/architecture/graph.json` dans le contexte d’un agent ;
- après toute modification d’un fichier source indexé ou des règles d’architecture, exécuter
  `dotnet run --project tools/Forge.ArchitectureGraph -- generate` ;
- avant de terminer une intervention, vérifier la fraîcheur avec
  `dotnet run --project tools/Forge.ArchitectureGraph -- check --full` ;
- ne jamais corriger manuellement `graph.json` ou `overview.mmd`, car ces fichiers sont générés.

Le mode d’emploi et la signification des relations sont documentés dans
`docs/architecture/README.md`.

## Conventions C#

- Identifiants, namespaces et API en anglais.
- Nullable reference types activés.
- Warnings as errors pour le code propre au projet.
- Types du domaine petits et testables.
- Préférer les records immuables pour les commandes, événements et DTO.
- Un type public principal par fichier.
- Pas de singleton global pour le domaine.
- Pas de `Godot.Node`, `Vector3`, `Resource`, `Variant` ou signal Godot dans `Forge.Domain` et `Forge.Application`.
- Utiliser les types propres `Vector3Value`, `QuaternionValue`, `EntityId`, `AssetId`, etc., puis convertir dans les adapters.
- Toute sérialisation doit être explicitement versionnée et couverte par un test.
- Les horloges, UUID et hasard doivent être injectables dans les tests.
- Les règles autoritaires s’exécutent côté serveur.

## Conventions Godot

- Construire le Creator autonome avec les contrôles, viewports, gizmos, imports glTF et primitives
  Godot, sans dépendre de `EditorPlugin`, `EditorInterface` ou de l’arbre de l’éditeur Godot.
- Conserver une vue 3D centrale sur les pages spatiales et utiliser des pages spécialisées pleine
  surface uniquement lorsque la 3D n’aide pas : dialogues, quêtes, scripts, IA et validation.
- Une seule fenêtre produit est visible. Les `Window` natives sont réservées aux dialogues système
  indispensables ; les rubriques, documents et inspecteurs restent des `Control` intégrés.
- Les nœuds Godot sont des représentations, pas la source de vérité métier.
- Les scènes générées doivent pouvoir être reconstruites depuis le modèle de domaine.
- Tout code d’éditeur doit être isolé du runtime exporté.
- Le plugin historique doit rester fonctionnel jusqu’à la parité de la matrice de migration ; sa
  suppression exige une validation utilisateur du Studio et une distribution autonome qualifiée.

## ForgeScript

- ForgeScript est AngelScript avec une API RPG contrôlée.
- Ne pas exposer directement l’API Godot.
- Interdire fichiers, réseau, processus, threads, chargement natif et accès mémoire.
- Exposer des handles opaques et une API versionnée.
- Prévoir quotas CPU, profondeur d’appel, mémoire et récursion d’événements.
- Compiler et tester les scripts avant inclusion dans un module.
- Conserver source et bytecode avec versions de VM/API.
- Les scripts de gameplay autoritaires s’exécutent sur le serveur.

## IA et MCP

- L’IA ne doit pas écrire directement dans le dépôt ou les modules par l’intermédiaire des outils Forge.
- Les outils de lecture sont séparés des outils de mutation.
- Les mutations sont `dry_run` par défaut.
- Une application exige un `plan_id` lié à une révision de projet.
- Les outils doivent être de haut niveau : créer une zone, ajouter un dialogue, compiler un script, lancer un test.
- Éviter les outils génériques du type « exécuter n’importe quelle commande » dans le MCP livré aux créateurs.
- Commencer avec peu d’outils ; élargir seulement lorsqu’un workflow manuel réel est stabilisé.
- Les modèles IA et fournisseurs 3D doivent rester interchangeables.
- Tout asset généré conserve sa provenance et sa licence.

## Qualification des cartes générées

- Les exigences visibles du brief deviennent des seuils d'audit explicites. Un nombre demandé de
  maisons ou de PNJ ne doit jamais être remplacé par un simple comptage technique de nœuds.
- Après la construction complète de la zone, lancer `scripts/Invoke-MapQualityAudit.ps1` sur le vrai
  workspace et le spawn joueur, dans le renderer Forward+ Vulkan. Renseigner au minimum les nombres
  demandés de patrouilleurs et de groupes de bâtiments ; une carte verdoyante conserve les seuils de
  strates et de densité locale de couverture du sol.
- Le marqueur `OPENRPG_FORGE_MAP_QUALITY_MACHINE_PASS` est une preuve intermédiaire. Il contrôle le
  terrain texturé réellement résolu, son espacement d'échantillonnage, ses couches de peinture et leur
  couverture, les MultiMeshes de végétation matérialisés, les acteurs animés, leur déplacement et
  contact au sol pendant plusieurs frames, les fondations visibles, les intersections entre végétation
  et bâtiments, les corridors de cadrage ainsi que les entrées orbit/zoom de la caméra.
- Inspecter ensuite soi-même tous les PNG obligatoires du runtime : `overview`, `overhead`,
  `ground-level`, puis `patrol-character` dès qu'un PNJ est demandé et `building-detail` dès qu'un
  bâtiment est demandé, complété par les gros plans du même seuil `door-closed` et `door-open`. Ne
  lancer `scripts/Confirm-MapQualityVisualReview.ps1` que si texture du sol, végétation, bâtiments,
  entrées, personnages et cadrages sont crédibles dans chaque image ; renseigner explicitement
  `EntrancesAlignedAndOperable`. Ce script est le
  seul à émettre `OPENRPG_FORGE_MAP_QUALITY_PASS` ; il exige la baseline produit comparée et lie la
  revue aux SHA-256 des captures.
- Une image faible, vide, illisible, contenant un faux personnage ou un objet flottant rejette le lot,
  même si tous les tests et métriques passent. Corriger la cause générique, relancer l'audit complet et
  réinspecter toutes les vues avant de présenter la carte à l'utilisateur.
- Comparer la carte à la référence produit courante. Le déterminisme, les graphes et un `PASS` local ne
  prouvent pas une amélioration : ne déclarer le nouveau processus supérieur qu'après la gate
  comparative du benchmark (effort de correction ou score visuel à l'aveugle) sans régression
  éliminatoire.
- Une provenance `human-forward-plus:` ne doit jamais être déclarée à partir des logs, empreintes ou
  captures techniques du worker seuls. Elle suppose une inspection visuelle réelle du résultat chargé.

## Sécurité des modules

- Refuser les chemins absolus et les traversées `..`.
- Limiter tailles, nombre de fichiers et taux de décompression.
- Vérifier hash, type MIME réel et schéma.
- Isoler l’espace de noms de chaque module.
- Ne pas charger de DLL ou script natif provenant d’un module communautaire.
- Ne pas autoriser un module communautaire à remplacer les ressources du runtime.
- Utiliser PCK seulement pour du contenu de confiance contrôlé par l’équipe.

## Définition de terminé

Une tâche est terminée uniquement si :

- les critères d’acceptation sont satisfaits ;
- le code compile ;
- les tests pertinents passent ;
- les erreurs et limites connues sont documentées ;
- aucune dépendance non justifiée n’a été ajoutée ;
- le statut et la documentation sont cohérents ;
- le diff reste centré sur la tâche.

## Interdictions du MVP

Ne pas démarrer avant validation explicite :

- fork de Godot ;
- renderer personnalisé ;
- monde ouvert ou streaming massif ;
- serveur Rust séparé ;
- multijoueur public persistant ;
- marketplace ;
- génération 3D imposée par un fournisseur unique ;
- système de règles copié de D&D ;
- éditeur visuel universel remplaçant tout code ;
- réécriture générale pour « préparer le futur » sans besoin concret.

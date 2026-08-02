# AGENTS.md — OpenRPG Forge

## Mission

Construire un moteur et un toolset de RPG 3D libre, moderne et orienté modules, inspiré par la promesse d’Aurora sans reprendre les marques, contenus, règles ou actifs de Neverwinter Nights/D&D.

Le produit doit rester utilisable sans IA, sans abonnement et sans outil propriétaire obligatoire.

## Sources de vérité

Avant toute modification importante, lire :

1. `MEGACONTEXT.md`
2. `STATUS.md`
3. `TASKS_MVP.md`
4. les ADR applicables dans `docs/ADR/`
5. la documentation spécialisée du sous-système concerné

Une décision enregistrée dans un ADR ne doit pas être renversée silencieusement. Proposer un nouvel ADR de remplacement.

## Gouvernance Aurora 2026

- `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/` est un snapshot de recherche immuable. Ne pas le
  modifier pour refléter l'état du projet.
- Le tracker opérationnel est `tracking/project/AURORA_PARITY_COVERAGE.csv`, généré par
  `scripts/Update-AuroraParityTracker.ps1`.
- Avant une feature Aurora, lire son exigence source, le tracker projet et le rapport d'écart dans
  `docs/aurora-parity/`.
- Traiter un ensemble cohérent de une à cinq feature IDs. Une priorité P2/P3 ne détourne pas une
  tâche P0/P1 en cours.
- La parité est fonctionnelle et clean-room : ne copier ni code, asset, texte, règles, nom public,
  API ou format propriétaire.
- Ne pas créer de classe ou projet vide uniquement pour augmenter la couverture.
- Une ligne ne passe `Tested` qu'avec une preuve automatisée ou une procédure reproductible liée
  dans le tracker projet.

## Pile imposée pour le MVP

- Godot **4.7.1 .NET** comme moteur, runtime et base d’éditeur.
- C#/.NET comme langage principal.
- Bibliothèques de domaine C# sans dépendance Godot.
- Plugin d’éditeur Godot, sans fork du moteur au départ.
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

`Editor/Runtime/MCP -> Application Commands -> Domain -> Persistence/Adapters`

L’IA, les plugins et les interfaces ne modifient jamais directement les fichiers du projet. Toute mutation passe par le bus de commandes transactionnel et produit :

- un identifiant de commande ;
- une révision de base ;
- une validation ;
- un diff prévisualisable ;
- une opération inverse ou un snapshot ;
- un journal d’audit ;
- un résultat de test si applicable.

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

- avant une modification transversale, interroger le sous-graphe concerné avec
  `python scripts/architecture_graph.py query "<module ou symbole>"` ;
- utiliser `--format paths` pour obtenir uniquement les fichiers à examiner et augmenter
  `--depth` ou `--max-nodes` seulement si le premier résultat est insuffisant ;
- ne jamais charger intégralement `docs/architecture/graph.json` dans le contexte d’un agent ;
- après toute modification d’un fichier source indexé ou des règles d’architecture, exécuter
  `python scripts/architecture_graph.py generate` ;
- avant de terminer une intervention, vérifier la fraîcheur avec
  `python scripts/architecture_graph.py check` ;
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

- Réutiliser le viewport 3D, l’inspecteur, les gizmos et l’Undo/Redo natifs avant de recréer un éditeur.
- Ajouter des écrans spécialisés uniquement pour les domaines qui en ont besoin : dialogues, quêtes, scripts, IA, validation.
- Les nœuds Godot sont des représentations, pas la source de vérité métier.
- Les scènes générées doivent pouvoir être reconstruites depuis le modèle de domaine.
- Tout code d’éditeur doit être isolé du runtime exporté.
- Le toolset doit fonctionner dans l’éditeur Godot standard avant toute tentative de fork ou de rebranding.

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

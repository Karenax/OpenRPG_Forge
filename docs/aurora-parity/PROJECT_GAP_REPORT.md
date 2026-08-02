# Rapport d’écart projet — Aurora 2026

Date de référence : 2 août 2026.

## Résumé

Le vertical slice « Crypte des Cendres » est fonctionnel et son gate global est vert. Il prouve le
modèle canonique initial, les commandes transactionnelles, l’édition élémentaire, le runtime solo,
la narration, le combat, ForgeScript, les modules, les sauvegardes et MCP. Il ne prouve pas encore
qu’un auteur peut créer un module RPG original complet sans modifier le code ou le JSON.

L’audit strict des 76 P0 de `SHELL`, `PROJECT`, `AREA`, `PALETTE`, `BUILD` et `QA` classe :

| Statut d’audit | Nombre |
|---|---:|
| `Tested` | 2 |
| `Implemented` avec note `Partial` | 52 |
| `Designed` | 1 |
| `NotStarted` | 21 |

Parmi les 641 autres lignes, `ORF-AREA-012` est maintenant auditée `Implemented` partiel dans le
registre projet générique ; les 640 restantes gardent leur baseline `Researched` jusqu’à un audit de
leur code, de leurs tests et de leurs critères d’acceptation.

## Architecture réelle versus cible

| Sujet | État réel | Cible |
|---|---|---|
| Domaine | `Forge.Domain` indépendant du moteur mais contenant aussi dialogue, quête et combat | Core général et RPG Kit versionné |
| Données auteur | index `project.json` et documents de zone adressés par hash ; autres agrégats encore regroupés | documents logiques séparables pour zones, blueprints et ressources |
| Mutations | commandes, révisions, dry-run, patches, inverse et historique pour les workflows MVP | voie unique pour chaque workspace, CLI et outil MCP |
| Toolset | coque Creator, catalogue Blueprints, zones, clipboard sémantique et Monde avec drag-and-drop, fantôme et gizmo X/Z | workflows spécialisés complets, gizmos complets, panneaux détachables et historique universel |
| Runtime | reconstruction data-driven partielle avec logique Crypte dans `GameRoot` | runtime générique piloté par composants, événements et Kits |
| Contenu | primitives, scripts et package de démonstration | catalogue, imports contrôlés, provenance, localisation et pipeline GLB/audio |
| Build | export/validate/load `.orpmod` | build, test, migrate, normalize, diff, references et incrémental |
| Réseau et GM | absents | Network Kit puis Live Director autoritaire et audité |

## P0 déjà prouvées

- `ORF-QA-008` — tests unitaires du domaine : IDs, sérialisation, migrations, commandes,
  validations, règles et patches passent dans le gate global.
- `ORF-QA-010` — tests runtime headless : chargement, scénario, sauvegarde et reprise produisent des
  marqueurs et artefacts reproductibles.

Les autres capacités reconnaissables restent partielles. Leur preuve exacte et leur manque sont
portés ligne par ligne dans `tracking/project/AURORA_CORE_P0_AUDIT.csv`.

## Lacunes prioritaires

1. La branche RPG du runtime référence encore Elyra, la clé, le boss et la sortie dans son
   adaptateur même si le démarrage Core générique est maintenant séparé et testé.
2. L’éditeur fournit désormais coque, catalogue, drag-and-drop et translation directe, mais pas
   encore rotation/échelle/pivot, multi-sélection ni les workspaces Quêtes, Scripts, Assets et Validation complets.
3. Les blueprints sont éditables mais restent intégrés à `project.json`, sans version, paramètres,
   provenance ou namespace de package ; les documents de terrain, navigation, éclairage et audio de
   zone restent à définir autour des zones désormais indépendantes.
4. Les usages d’entités et de locuteurs sont indexés ; le renommage sûr, la recherche globale, l’index complet, le cache
   incrémental, l’accès CLI/MCP et l’export des diagnostics restent absents.
5. La licence du code propre et le registre d’approbation des dépendances restent ouverts.
6. La CI distante n’a jamais été exécutée.

## Dépendances

- Les prochains workspaces réutilisent désormais le catalogue, le contexte de sélection et les
  diagnostics partagés de la coque Creator.
- Les extensions du gizmo réutiliseront les commandes de transformation génériques et le validateur
  de placement de G7-04, sans écrire directement dans la scène moteur.
- Le renommage sûr étendra l’index moteur-indépendant livré en G7-03 à tous les documents canoniques.
- Le RPG Kit dépend de workflows Core stables mais ne doit pas contaminer le Core avec un ruleset.
- MCP et l’assistant intégré ne s’étendent qu’après validation du workflow humain correspondant.
- Network Kit précède obligatoirement Live Director.

## Risques et mitigations

| Risque | Mitigation |
|---|---|
| Refactor massif du domaine avant un second cas réel | extractions incrémentales avec tests de la Crypte et d’un second module |
| Nouvelle UI construite sur des contrats spécifiques à la démo | livrer G7-01 runtime générique avant la coque complète |
| Fichier éditeur monolithique | extraire un workspace ou service par incrément sans réécriture générale |
| Faux progrès par nombre de lignes du catalogue | mesurer les scénarios Builder, Writer, Scripter, Agent, Network et GM |
| Perte de frontière IP | conserver le snapshot et produire uniquement des contrats, noms et assets Forge originaux |

## Propositions d’ADR à instruire

- frontière progressive `Forge Core` versus `Forge RPG Kit` ;
- structure des documents auteur séparés et migrations depuis `project.json` ;
- coque Forge dédiée versus extension de l’éditeur 3D, sans autoriser un fork implicite ;
- index canonique de références et renommage transactionnel ;
- politique de licence du code, des dépendances et des assets de distribution.

## Première tâche

`tasks/G7-06-SEMANTIC-CLIPBOARD.md` est terminée. La suite est G7-07 : recherche globale,
références et renommage sûr transactionnel.

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
| `Implemented` avec note `Partial` | 40 |
| `Designed` | 1 |
| `NotStarted` | 33 |

Les 641 autres lignes restent `Researched` jusqu’à un audit de leur code, de leurs tests et de leurs
critères d’acceptation.

## Architecture réelle versus cible

| Sujet | État réel | Cible |
|---|---|---|
| Domaine | `Forge.Domain` indépendant du moteur mais contenant aussi dialogue, quête et combat | Core général et RPG Kit versionné |
| Données auteur | `project.json` canonique unique | documents logiques séparables pour zones, blueprints et ressources |
| Mutations | commandes, révisions, dry-run, patches, inverse et historique pour les workflows MVP | voie unique pour chaque workspace, CLI et outil MCP |
| Toolset | écran Forge unique, fenêtres spécialisées et aperçu 3D | workspaces cohérents avec explorateur, sélection, diagnostics et historique partagés |
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
2. L’éditeur ne fournit ni explorateur Forge, ni gizmos directs, ni drag-and-drop, ni workspaces
   Blueprints, Quêtes, Scripts, Assets et Validation complets.
3. Les zones et blueprints ne sont pas encore des ressources auteur indépendantes et éditables.
4. Les références inverses, le renommage sûr, le build incrémental et les diagnostics cliquables
   sont absents.
5. La licence du code propre et le registre d’approbation des dépendances restent ouverts.
6. La CI distante n’a jamais été exécutée.

## Dépendances

- La coque Creator dépend d’un contexte de sélection, d’un historique et de diagnostics partagés.
- Les gizmos et le drag-and-drop dépendent de commandes de placement et transformation génériques.
- Le renommage sûr dépend d’un index de références du contenu, distinct du graphe d’architecture.
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

`tasks/G7-01-GENERIC-SECOND-MODULE.md` est terminée. La suite est la coque Creator G7-02 :
explorateur, contexte de sélection et diagnostics partagés.

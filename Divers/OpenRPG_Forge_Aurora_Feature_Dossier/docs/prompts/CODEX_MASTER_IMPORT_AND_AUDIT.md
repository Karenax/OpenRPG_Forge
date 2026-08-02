# Prompt maître Codex — importer et auditer le dossier Aurora

Tu travailles dans le dépôt OpenRPG Forge existant.

Lis d’abord :

- `AGENTS.md`
- le MEGACONTEXT existant
- `docs/aurora-parity/README_FIRST.md`
- `docs/aurora-parity/AGENTS_AURORA_PARITY.md`
- `docs/aurora-parity/00_EXECUTIVE_SUMMARY.md`
- `docs/aurora-parity/04_FORGE_ARCHITECTURE_IMPACT.md`
- `docs/aurora-parity/06_ROADMAP.md`
- `docs/aurora-parity/tracking/AURORA_PARITY_COVERAGE.csv`

Le dossier contient 717 exigences de capacité. **Ne tente pas de toutes les implémenter.**

## Mission de cette session

Réalise uniquement un audit de couverture du dépôt actuel et prépare le premier epic P0 cohérent.

### 1. Inspection

- vérifie Git, branche et modifications locales ;
- inspecte les solutions/projets, documents, tests et scripts ;
- identifie le nom réel des namespaces et composants ;
- ne renomme rien pendant l’audit ;
- ne télécharge aucune dépendance.

### 2. Mapping

Pour chaque feature P0 des domaines SHELL, PROJECT, AREA, PALETTE, BUILD et QA :

- indique `NotStarted`, `Researched`, `Designed`, `Implemented` ou `Tested` ;
- ajoute un chemin de preuve réel ;
- n’invente pas de couverture à partir d’un nom de fichier ;
- marque `Partial` dans les notes si seule une partie existe.

Mets à jour une copie du tracker dans le dépôt. Ne modifie pas le dossier source si celui-ci est conservé comme snapshot immuable; crée alors `tracking/project/AURORA_PARITY_COVERAGE.csv`.

### 3. Gap report

Crée `docs/aurora-parity/PROJECT_GAP_REPORT.md` avec :

- architecture réelle versus architecture cible ;
- P0 déjà couverts ;
- P0 manquants ;
- dépendances ;
- risques ;
- incompatibilités avec le MEGACONTEXT ;
- propositions d’ADR, sans prendre de décision silencieuse.

### 4. Premier epic

Crée une fiche de tâche bornée pour le plus petit incrément qui rapproche de :

> créer un projet, une zone canonique, placer une instance de blueprint, sauvegarder, recharger et annuler.

La fiche contient 1 à 5 feature IDs, fichiers autorisés, non-objectifs, critères d’acceptation et tests.

### 5. Interdictions

- aucune implémentation massive ;
- pas de nouveaux projets vides ;
- pas de Network Kit, Live Director, IA 3D ou terrain avancé ;
- pas de fork Godot ;
- pas de modification du ruleset ;
- pas de copie NWN ;
- pas de statut `Tested` sans test exécuté.

## Réponse finale

Donne :

1. état Git ;
2. fichiers audités ;
3. couverture P0 résumée ;
4. gap critique ;
5. fichiers de rapport/tracker créés ;
6. commandes de test exécutées ;
7. première tâche recommandée, unique et bornée.

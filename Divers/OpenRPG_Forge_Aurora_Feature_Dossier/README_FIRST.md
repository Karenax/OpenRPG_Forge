# OpenRPG Forge — dossier de parité fonctionnelle Aurora

**Version : 1.0**  
**Recherche arrêtée au : 2026-08-02**  
**Catalogue : 717 fonctionnalités, 26 domaines**

## But

Ce dossier décrit ce que l’écosystème Aurora permettait de construire et transforme ces capacités en spécifications originales pour OpenRPG Forge.

Il est conçu pour être copié dans un dépôt géré par Codex. Il ne contient aucun code ou asset de Neverwinter Nights.

## Première lecture

1. `00_EXECUTIVE_SUMMARY.md`
2. `01_SCOPE_METHOD_CONFIDENCE.md`
3. `03_IP_AND_CLEAN_ROOM_BOUNDARIES.md`
4. `04_FORGE_ARCHITECTURE_IMPACT.md`
5. `06_ROADMAP.md`
6. `08_CODEX_INTEGRATION.md`
7. `docs/prompts/CODEX_MASTER_IMPORT_AND_AUDIT.md`

## Contenu

```text
README_FIRST.md
AGENTS_AURORA_PARITY.md
AURORA_PARITY_MEGACONTEXT.md
FEATURE_CATALOG.md
feature_catalog.json / .csv
SOURCES.md / sources.json
00–09 documents de cadrage
docs/
├── requirements/       un document détaillé par domaine
├── implementation/     contrats d’architecture
├── research/           résultats historiques
└── prompts/            prompts Codex/Hermes
tracking/
├── AURORA_PARITY_COVERAGE.csv
├── AURORA_EVENT_MATRIX.csv
├── SCRIPT_CAPABILITY_MATRIX.csv
├── SOURCE_COVERAGE.csv
└── DECLARED_DEPENDENCIES.csv
schemas/
└── aurora-parity-feature.schema.json
```

## Installation dans le dépôt

Copier le dossier sous :

```text
docs/aurora-parity/
```

Ne remplacez pas le MEGACONTEXT existant. Le fichier `AURORA_PARITY_MEGACONTEXT.md` devient une référence spécialisée.

## Première commande à Codex

Utiliser `docs/prompts/CODEX_MASTER_IMPORT_AND_AUDIT.md`.

Ce prompt demande un audit et un mapping. Il n’autorise pas Codex à générer toutes les fonctions en une seule passe.

## Tracker

Le tracker est initialisé à `Researched`. Ce statut signifie seulement que la capacité a été documentée. Il ne signifie ni conçue dans le dépôt, ni implémentée, ni testée.

## Frontière IP

La parité est fonctionnelle. Aucun format, asset, texte, règles, nom public ou code NWN n’est inclus. Voir `03_IP_AND_CLEAN_ROOM_BOUNDARIES.md`.

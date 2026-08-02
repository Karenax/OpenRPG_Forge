# Intégration du dossier dans le dépôt Codex

## Emplacement recommandé

Copier le dossier complet dans :

```text
docs/aurora-parity/
```

Copier ou fusionner `AGENTS_AURORA_PARITY.md` dans les instructions de projet sans remplacer les décisions existantes.

## Ordre de lecture

1. `README_FIRST.md`
2. `00_EXECUTIVE_SUMMARY.md`
3. `01_SCOPE_METHOD_CONFIDENCE.md`
4. `04_FORGE_ARCHITECTURE_IMPACT.md`
5. `08_ROADMAP.md`
6. le document de domaine concerné dans `docs/requirements/`
7. `tracking/AURORA_PARITY_COVERAGE.csv`

## Ce que Codex ne doit pas faire

- générer des centaines de classes vides pour « couvrir » le catalogue ;
- déclarer une feature terminée sans test ;
- ajouter les fonctions RPG au Core ;
- copier des noms, données ou formats NWN ;
- forker Godot pour contourner une petite limitation ;
- ajouter un serveur multijoueur avant que le runtime local de la tranche verticale fonctionne ;
- exposer des mutations MCP de fichiers bruts.

## Première utilisation

Le premier prompt fourni demande à Codex d’auditer le dépôt existant et de mapper les features déjà présentes. Il ne demande aucune implémentation massive.

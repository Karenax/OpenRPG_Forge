# ADR 0005 — Format communautaire `.orpmod`

- Statut : accepté
- Date : 1 août 2026

## Contexte

Un PCK Godot communautaire peut contenir des ressources capables d’entrer en collision avec le runtime. Le format de module doit rester inspectable, versionné, portable et validable avant chargement.

## Décision

Les modules communautaires utilisent une archive ZIP contrôlée portant l’extension `.orpmod`.

- `manifest.json` décrit version de format, module, runtime minimum, API ForgeScript, point d’entrée, dépendances et hash.
- Les données métier sont du JSON versionné ; les modèles sont en GLB/glTF ; scripts, localisation, provenance et licences ont des emplacements dédiés.
- Avant extraction ou montage, le loader valide chemins, nombre de fichiers, taille totale, ratio de compression, extensions, types réels, hashes, schémas, identifiants et dépendances.
- Les chemins absolus et traversées `..`, DLL, assemblies, binaires natifs et scripts Godot sont refusés.
- Chaque module possède un espace de noms isolé et ne peut remplacer les ressources du runtime.
- Les PCK sont réservés au contenu de confiance contrôlé par l’équipe.

## Conséquences

- Le packager et le loader doivent avoir des tests de round-trip et de sécurité.
- Les migrations de format sont explicites.
- La provenance et les licences d’assets font partie du livrable, pas d’un processus externe facultatif.

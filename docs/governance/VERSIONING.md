# Versioning sémantique OpenRPG Forge

Schéma de politique : v1. Provenance : `openrpg-forge:p0-r002`.

- Core : `0.6.34`.
- RPG Kit, Network Kit et Live Director Kit : `0.6.34`.
- Package `.orpmod` : `1.2.0`.
- Schémas persistants distincts : projet `0.6.36`, workspace `1.28.0`, manifeste `1.2.0`.

Core et Kits suivent SemVer et restent compatibles dans une même ligne majeure/mineure. Un Kit
déclare la version Core minimale qu'il consomme. Les packages sont compatibles par version majeure.
Les versions de schéma ne sont jamais déduites de la version produit : une migration explicite est
requise pour tout document persistant ancien.

Toute modification observable rejoint `CHANGELOG.md`. Une rupture incrémente la version majeure,
une capacité rétrocompatible la mineure et une correction compatible le patch. Les préversions et
métadonnées suivent SemVer 2.0.0.

# ADR 0008 — Propriétés locales versionnées des instances

- Statut : accepté
- Date : 2 août 2026

## Contexte

Une instance placée doit pouvoir personnaliser un nom, un état, une statistique ou une variable sans
dupliquer son blueprint. Un dictionnaire JSON libre rendrait les valeurs ambiguës, les migrations
difficiles et l’édition peu fiable.

## Décision

- `EntityInstance` contient un `EntityPropertyOverrides` immuable.
- Le schéma d’overrides courant est la version `1` et stocke une liste ordonnée de clés uniques.
- Les valeurs sont explicitement typées : texte, booléen, entier ou nombre fini. Leur représentation
  persistante est normalisée avec une culture invariante.
- Les clés acceptent lettres, chiffres, point, tiret et underscore, avec des limites de taille
  explicites pour les clés et les valeurs.
- Le format projet courant devient `0.3.0`. Les documents `0.2.0` et `0.1.0` sont migrés avec un jeu
  d’overrides vide lorsque le champ est absent.
- Toute modification passe par `set_entity_overrides`, qui fournit révision de base, dry-run,
  diagnostics, patch, inverse et audit. L’inspecteur Forge utilise cette commande.

## Conséquences

- Une instance peut diverger localement tout en conservant une référence stable vers son blueprint.
- Sérialisation, undo/redo, playtest et futurs outils MCP partagent le même contrat typé.
- L’ajout d’un nouveau type de valeur ou d’une nouvelle structure exigera une migration explicite du
  schéma d’overrides et, si le document persistant change, du format projet.
- La résolution sémantique de clés comme `hitPoints` ou `locked` appartient aux futurs systèmes de
  gameplay ; T1.3 garantit leur stockage et leur édition, pas encore leur interprétation générale.

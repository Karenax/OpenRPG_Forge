# ADR 0007 — Agrégat de module et création de workspace

- Statut : accepté
- Date : 2 août 2026

## Contexte

Le premier format de projet stockait directement les blueprints et les zones. Il ne possédait pas
l’identité du contenu distribuable, sa description ni sa zone d’entrée. Forge ne pouvait donc pas
créer proprement un module vide ou préparer son futur export `.orpmod`.

## Décision

- Un projet de travail contient un `ModuleDefinition` pour le MVP.
- Le module possède un ID opaque, un slug, un nom, une description et une référence stable vers sa
  zone d’entrée.
- Les blueprints et les zones restent dans `ProjectDefinition` pendant la tranche verticale ; le
  passage éventuel à plusieurs modules par projet exigera une nouvelle décision et une migration.
- Le format de projet courant devient `0.2.0` et le lecteur migre explicitement les documents
  `0.1.0` en leur ajoutant un module principal déterministe.
- La commande `new_module` produit l’agrégat initial, son audit, une zone, un sol provisoire et un
  point de départ. `ProjectSession.Create` écrit ensuite le workspace uniquement dans un dossier
  vide, avec des remplacements atomiques.

## Conséquences

- Forge peut créer un module jouable sans copier le projet de démonstration.
- Le runtime, le playtest et le futur packager disposent d’une zone d’entrée explicite.
- Les projets `0.1.0` restent ouvrables et sont réécrits en `0.2.0` à la prochaine sauvegarde.
- Un dossier non vide n’est jamais écrasé par l’assistant de création.

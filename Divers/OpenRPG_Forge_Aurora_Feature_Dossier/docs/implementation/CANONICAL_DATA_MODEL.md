# Modèle de données canonique

## Principes

- JSON UTF-8, camelCase et ordre stable.
- `documentType`, `schemaVersion`, `id` et `slug` pour chaque document principal.
- UUID pour les références; les chemins et slugs sont modifiables.
- Un fichier logique par ressource lorsque cela améliore les diffs.
- Pas de données dérivées sauf cache explicitement jetable.
- Toutes les références portent un type attendu.

## Documents proposés

```text
forge.project
forge.area
forge.blueprint
forge.creature
forge.faction
forge.dialogue
forge.quest
forge.item
forge.placeable
forge.door
forge.trigger
forge.encounter
forge.route
forge.audio-cue
forge.store
forge.companion-profile
forge.ability
forge.effect
forge.ruleset
forge.package
```

## Instance versus définition

Une définition appartient à un package. Une instance appartient à une zone ou un état runtime. L’instance référence la définition et ne conserve que ses overrides et son état mutable.

## State runtime

L’état sauvegardé ne doit pas recopier toute la définition. Il enregistre l’ID, la version/fingerprint, les composants persistants et les différences.

## Migrations

Chaque type possède une chaîne de migrations testée. Une migration ne modifie jamais la source sans sauvegarde et rapport.

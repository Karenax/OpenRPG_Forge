# Contrats MCP pour la parité Aurora

## Profils

```text
forge.read
forge.author.area
forge.author.narrative
forge.author.rpg
forge.script
forge.test
forge.live-director
forge.admin
```

## Outils de base

```text
project.get_summary
project.search
project.find_references
project.validate
transaction.begin
transaction.preview
transaction.commit
transaction.rollback
```

## Outils par domaine

Chaque document de requirements décrit les opérations nécessaires. Leur implémentation doit préférer quelques commandes riches et typées plutôt qu’un outil par champ.

Exemple :

```text
area.apply_commands
```

reçoit un batch de commandes de placement validées; il ne reçoit jamais du code Godot ou un fragment `.tscn`.

## Sécurité

- transports locaux au début ;
- authentification si HTTP ;
- allowlist par profil ;
- limite d’objets et de documents par transaction ;
- aucune exécution shell ;
- aucune écriture hors projet ;
- protection de révision ;
- audit ;
- prompt injection traitée comme donnée non fiable.

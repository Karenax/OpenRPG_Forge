# G13-C — Progression data-driven des créatures

## Périmètre

- `ORF-CRE-016` — pistes de progression, classes génériques, niveaux et multiclassage configurable ;
- `ORF-CRE-017` — axes de disposition facultatifs et définis par le projet ;
- `ORF-CRE-018` — compétences, rangs, entraînement, expertise, points et difficulté ;
- `ORF-CRE-019` — talents à rangs, prérequis, passifs, grants de capacités et hooks typés ;
- `ORF-CRE-020` — loadouts appris, préparés, deck, cooldown, charges et ressources.

## Contrat livré

`CreatureProgressionProfile` est un document canonique v1 porté par le blueprint de créature. Son
UUID, sa provenance, son ruleset, ses pistes, axes, compétences, talents, loadout et overrides sont
indépendants de Godot et de toute règle propriétaire. Les références de pouvoirs sont résolues par
slug contre le `CombatProfile` du même blueprint.

`SetCreatureProgressionHandler` impose révision, type de blueprint, lecture seule, UUID unique,
validation localisée, dry-run, patch, inverse, audit et historique. Le projet agrégé passe en
`0.6.24`, le workspace en `1.18.0`, et les versions `0.6.23`/`1.17.0` migrent avec un profil absent.
Le round-trip JSON et l’export/chargement `.orpmod` conservent exactement la configuration.

L’écran **Progression & pouvoirs** propose un catalogue de créatures, quatre onglets de formulaire
et un résumé lisible : niveau total, parcours, dispositions, compétences, talents et pouvoirs
préparés. Il expose les états vide et erreur, puis appelle la même commande que les autres clients.
Le viewport et le runtime affichent le niveau total ; `CreatureProgressionScriptService` fournit à
ForgeScript un handle UUID/schéma et un snapshot en lecture seule. Une extension absente reste
optionnelle ; si elle est requise, le runtime renvoie un diagnostic stable.

## Preuves

- domaine : `CreatureProgressionProfileTests` — 3 tests ;
- commande et handle script : 4 tests ciblés ;
- sérialisation et `.orpmod` : migration, round-trip exact et export/chargement réel ;
- build solution — zéro warning, zéro erreur ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_PASS` ;
- non-régression : le smoke restaure exactement le hash du module source.

## Limites explicites

- Le profil décrit et valide une configuration de créature ; la distribution interactive de points
  pendant une partie et l’interface joueur de montée de niveau restent des workflows futurs.
- Les hooks sont des identifiants canoniques, pas du code arbitraire. Leur exécution nécessite un
  binding ForgeScript autorisé par le module.

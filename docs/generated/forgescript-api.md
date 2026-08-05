# ForgeScript API 1.0

## cast_ability

`void cast_ability(EntityRef target, const string &in ability)` — Demande une capacité au service de combat.
Availability: rpg-kit, server
Example: `cast_ability(target, "ash-nova")`

## health

`int health(EntityRef target)` — Lit les points de vie courants.
Availability: client, server
Example: `health(hero)`

## quest

`void quest(const string &in transition)` — Demande une transition de quête validée.
Availability: rpg-kit, server
Example: `quest("crypt.completed")`

## say

`void say(const string &in text)` — Publie une réplique localisable.
Availability: client, server
Example: `say("Bienvenue")`

## spawn

`EntityRef spawn(const string &in blueprint)` — Crée une entité sous quota et autorité serveur.
Availability: server
Example: `spawn("ash-guardian")`

## unlock

`void unlock(EntityRef target)` — Déverrouille une cible autorisée.
Availability: server
Example: `unlock(door)`

## variable

`void variable(const string &in key, const string &in value)` — Modifie une variable autorisée.
Availability: server
Example: `variable("door", "open")`

## Ressources MCP

- `forge://api/cast_ability`
- `forge://api/health`
- `forge://api/quest`
- `forge://api/say`
- `forge://api/spawn`
- `forge://api/unlock`
- `forge://api/variable`

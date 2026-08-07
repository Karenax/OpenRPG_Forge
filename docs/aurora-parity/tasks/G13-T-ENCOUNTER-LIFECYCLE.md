# G13-T — Rencontres : cycle de vie

Features : `ORF-ENC-008`, `ORF-ENC-010`, `ORF-ENC-014`.

## Livré

`EncounterWorldProfile` décrit délai et cycles de respawn, signal d'occupation, persistance,
anti-farm, multiplicateur, faction de spawn et heartbeat/custom budgétés. Le résolveur retourne le
prochain cycle sans modifier la collection d'acteurs.

## Preuves

- test : `G13T_EncounterLifecycleFaction_RespawnsWithinBoundedCycle` ;
- Toolset/runtime : `G13T_ENCOUNTER_LIFECYCLE_PASS` et `G13T_ENCOUNTER_RESPAWN_PASS`.

Limite : la récompense anti-farm finale reste une responsabilité du système de loot/progression.

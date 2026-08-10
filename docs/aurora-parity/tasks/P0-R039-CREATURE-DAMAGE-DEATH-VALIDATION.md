# P0 R039 — dégâts, mort et validation de créature

Lot stable : `R039` / `FC-CREATURE-P0-02` — trois fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-CRE-036` — OnDamaged.
- `ORF-CRE-043` — OnDeath.
- `ORF-CRE-049` — Validation de créature.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/05_CREATURE.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R039 |
|---|---|---|
| transaction de dégâts | `CreatureCombatService` bornait `BeforeDamage` et produisait `DamageApplied` | trois bindings canoniques persistants, `BeforeDamage` budgété/limité, payload post-transaction complet et ordre runtime |
| mort | `EntityDied` RPG Kit contenait killer, contributeurs, cause, transform, loot et policy | binding ForgeScript `entity.died`, formulaire, payload runtime et preuve sur la mort réelle du boss |
| validation | `CreatureFactory.Diagnose`, validateurs projet des profils créature et diagnostics pré-build | synthèse visible réunissant rig/apparence, règles, équipement, capacités, scripts, faction, ressources et budgets |
| auteur/agents | écran événements G13-F sans dégâts/mort | section dédiée, aperçu, commande dry-run/apply, undo/redo, round-trip, handle typé et MCP filtré |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| hook déterministe avant dégâts | `CreatureDamageDeathBindings.BeforeDamage`, budget 1–50 ms, ajustement 0–1 | `DamageLifecycleIsDeterministicAndDeathCarriesCompletePostTransactionContext` |
| événement après transaction | `CreatureEventRuntimeResolver.DamageApplied` avec transaction, demandé/appliqué/restant/type/source/cause | tests Domain et marker runtime R039 |
| mort complète | `CreatureEventRuntimeResolver.EntityDied` avec killer, contributeurs, cause, position, loot et politique | tests Domain et boss du smoke runtime |
| validation pré-build | `CreatureFoundationValidator`, validateurs présentation/progression/loot/rôle/événements et `ProjectValidator` | suites Domain/RpgKit et fixture Crypte |
| interface transactionnelle | section « Dégâts et mort transactionnels » de `CreatureEventWindow` | capture inspectée `evidence/r039-creature-damage-death-ui.png`, smoke Toolset R039 |
| persistance | propriété compatible `DamageDeath` du profil événement | test round-trip projet et fixture canonique |
| ForgeScript/MCP | snapshot sûr et ressource MCP détaillant handlers/budget/ratio | tests Application et MCP ciblés |

## Non-objectifs

- ouvrir les événements P1 voisins ;
- changer les règles de calcul de dégâts du RPG Kit ;
- ajouter P1/P2, AV7–AV10, commit ou push.

## Validation finale

- tests ciblés Domain, Application, ModuleFormat et MCP verts ;
- capture 1380×860 inspectée, neuf bindings et section dégâts/mort lisibles ;
- smokes `OPENRPG_FORGE_TOOLSET_R039_CREATURE_DAMAGE_DEATH_PASS` et
  `OPENRPG_FORGE_RUNTIME_R039_CREATURE_DAMAGE_DEATH_PASS` ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R040.

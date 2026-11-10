# Runtime M3 - quête scriptée de bout en bout

**Version :** 8 novembre 2026.
**Portée :** sous-ensemble vérifié de la fixture `ab_proving` du bundle
`module-migration-bundle@1.0.0`. Ce document ne décrit pas une compatibilité NWN générale.
**Code :** `crates/forge_nwn_runtime`, complété par `forge_aurora_runtime` et `forge_cli`.

## 1. Résultat vérifié

Le scénario synthétique exécute, dans l'ordre :

1. chargement du module (`ab_mod_load`) ;
2. entrée dans le camp (`ab_camp_enter`) ;
3. conversation de départ (`ab_guide_talk`, conditions `ab_cond_start`) ;
4. branche active (`ab_cond_active`) ;
5. transition vers la crypte, entrée (`ab_crypt_enter`) ;
6. trigger de relique différé d'un tick (`ab_relic_trigger`) ;
7. coffre et relique (`ab_chest_dist`) ;
8. mort du gardien (`ab_guard_death`) ;
9. retour au camp par la porte (`ab_gate_used`) ;
10. branches relique et récompense (`ab_cond_relic`, `ab_reward`) ;
11. branche terminée (`ab_cond_done`) ;
12. sauvegarde, reprise et contrôle d'idempotence.

Résultat observé : journal `[10, 20, 30]`, or `50`, inventaire `AB_RELIC`, deux transitions,
quatre branches de dialogue choisies, aucun diagnostic non supporté.

## 2. Événements réellement traités

Le runtime M3 n'est pas un moteur d'événements NWN complet : `M3Scenario` appelle explicitement
les points d'entrée de la fixture. Chaque appel fournit un `VmContext` typé.

| Événement | Script | Contexte | Ordre | Preuve |
| --- | --- | --- | --- | --- |
| Chargement du module | `ab_mod_load` | `OBJECT_SELF = module` | 1 | `executes_module_load_and_the_quest_branches_deterministically` |
| Entrée de zone (camp) | `ab_camp_enter` | self module, `GetEnteringObject = PC` | 2 | même test + `m3_scenario_completes...` |
| Début de conversation | `ab_guide_talk` | self guide, `GetLastUsedBy = PC` | 3 | transcription de dialogue |
| Condition de départ | `ab_cond_start` | self module | 4 | branche `Parle-moi du pont.` |
| Condition active | `ab_cond_active` | self module | 5 | branche `Où se trouve la relique ?` |
| Entrée de zone (crypte) | `ab_crypt_enter` | self module, entrant PC | 6 | événement `crypt_enter` |
| Trigger de relique | `ab_relic_trigger` | self trigger, entrant PC | 7 | événement `timer` au tick 1 |
| Coffre disturbé | `ab_chest_dist` | self coffre, utilisateur PC | 8 | journal 20 et message |
| Mort du gardien | `ab_guard_death` | self gardien | 9 | `ab_combat_won = 1` |
| Porte utilisée | `ab_gate_used` | self porte, utilisateur PC | 10 et 13 | deux messages de porte |
| Condition relique | `ab_cond_relic` | self module | 11 | branche `J'ai la relique.` |
| Action de récompense | `ab_reward` | self module | 12 | or 50, journal 30 |
| Condition terminée | `ab_cond_done` | self module | 14 | branche `Le pont est-il réparé ?` |

### Événements non traités

`OnHeartbeat`, `OnPerception`, `OnSpellCastAt`, `OnAttacked`, `OnDamaged`, `OnOpen`, `OnClose`,
`OnLock`, `OnUnlock`, `OnClick`, `OnInventoryDisturbed`, `OnTrapTriggered`, `OnPlayerEnter`,
`OnPlayerLeave`, `OnCutsceneAbort`, `OnAcquireItem`, `OnUnacquireItem`, `OnDeath` d'une créature
autre que le gardien, et les événements de zone non listés ci-dessus ne sont pas reliés au runtime.
Ils ne sont pas annoncés comme supportés et aucun script correspondant n'est exécuté par `M3Scenario`.

## 3. Parseur NCS

- signature `NCS V1.0`, octet de version `0x42`, taille déclarée sur 4 octets ;
- en-tête de 13 octets, opérandes multi-octets en big-endian ;
- limite de 4 Mio et de 200 000 instructions ;
- refus typé : signature, version, taille, troncature, opcode inconnu, type de constante, chaîne ;
- les opcodes connus mais non exécutés sont décodés puis marqués `Unsupported` ; ils échouent à
  l'exécution avec un diagnostic visible ;
- corpus de non-régression : toutes les troncatures de chaque NCS de la fixture sont refusées, et
  des mutations d'octets sont parsées sans panic.

Opcode VM réellement exécutés : `0x01` `CPDOWNSP`, `0x02` `RSADD`, `0x03` `CPTOPSP`, `0x04`
constantes, `0x05` `ACTION`, `0x06` `LOGANDII`, `0x0B` `EQUAL`, `0x1B` `MOVSP`, `0x1D` `JMP`,
`0x1E` `JSR`, `0x1F` `JZ`, `0x20` `RETN`.

## 4. VM déterministe

- pile de valeurs typées (`int`, `float`, `string`, `object`), slots de 4 octets ;
- frames d'appel par `JSR`/`RETN`, sauts vérifiés sur les offsets d'instruction ;
- `OBJECT_SELF`, objet entrant, dernier utilisateur et premier joueur fournis par contexte ;
- budgets : 100 000 pas et 4 096 valeurs de pile par exécution ;
- ordre des arguments d'un appel natif inversé depuis la pile du compilateur ;
- suspension/reprise explicite par `VmContinuation` (`execute_limited`/`resume`) ; les travaux
  différés passent aussi par le scheduler.

## 5. Registre des natives

Routine = ordinal de la fonction dans `nwscript.nss` de NWN:EE. Version du registre :
`nwscript-ee-ordinals-v1`, exposée dans le rapport sous `nativeRegistry`.

| ID | Native | Arguments | Statut |
| --- | --- | --- | --- |
| 242 | `GetModule` | 0 | supporté |
| 51 | `GetLocalInt` | 2 | supporté |
| 55 | `SetLocalInt` | 3 | supporté |
| 548 | `GetFirstPC` | 0 | supporté |
| 367 | `AddJournalQuestEntry` | 6 | supporté |
| 25 | `GetEnteringObject` | 0 | supporté |
| 217 | `GetIsPC` | 1 | supporté |
| 374 | `SendMessageToPC` | 2 | supporté |
| 330 | `GetLastUsedBy` | 0 | supporté |
| 204 | `ActionStartConversation` | 4 | supporté |
| 322 | `GiveGoldToCreature` | 2 | supporté |
| 6 | `AssignCommand` | 2 | supporté (action `StartConversation`/`ExecuteScript`) |
| 7 | `DelayCommand` | 2 | supporté (action différée, tick déterministe) |
| 8 | `ExecuteScript` | 2 | supporté via hôte de scripts explicite |
| 294 | `ActionDoCommand` | 1 | supporté (action immédiate) |

Toute routine absente du registre produit `unknown-native`. Toute routine déclarée non supportée
produit `unsupported-native`. Une arité incorrecte produit `native-arity`. Aucun appel non
implémenté n'est silencieux.

## 6. Ordonnanceur et effets

`ScriptScheduler` exécute des scripts différés avec un identifiant monotone, un tick d'échéance et
un contexte typé. L'ordre est `(tick, identifiant)`. Le snapshot conserve le tick, les travaux en
attente et les identifiants déjà exécutés ; une reprise ne rejoue pas un travail terminé.
`AssignCommand`, `DelayCommand`, `ActionDoCommand` et `ExecuteScript` sont supportés pour les
actions `StartConversation` et `ExecuteScript`, avec une file d'action déterministe et un hôte de
scripts explicite. Les autres actions NWScript (déplacement, attaque, sorts, etc.) restent
diagnostiquées comme non supportées.

Toute mutation de gameplay passe par `RuntimeEffect` : `SetLocalInt`, `AddJournalEntry`,
`SetQuestStatus`, `SetDoorState`, `SendMessage`, `GiveGold`, `AddInventory`, `MarkDead`,
`StartConversation`, `EndConversation`. Le lot est validé avant application, filtré par
`EffectPolicy` (autorisation globale ou refus par type), puis chaque effet reçoit un reçu séquencé
(`AppliedEffect { sequence, script, receipt, effect }`). Un lot refusé ne modifie pas l'état. Le
runtime n'écrit ni dans l'ECS Bevy ni dans un document Forge ; il n'expose pas encore les autorités
de physique, qui restent à raccorder.

## 7. Dialogue conditionnel

`ab_guide_dlg` contient 9 nœuds et 9 liens. Les quatre réponses sont filtrées par les scripts
`ab_cond_start`, `ab_cond_active`, `ab_cond_relic` et `ab_cond_done`. Le runtime :

1. part du lien sans source ;
2. évalue chaque condition dans le contexte du module ;
3. ne présente que les réponses dont la condition vaut vrai ;
4. exécute l'action de la réponse choisie ;
5. suit le lien enfant vers le nœud suivant et consigne la transcription.

Le mapping TLK n'est pas exercé : la fixture porte ses textes dans le document de dialogue.

## 8. Journal, quête et récompense

Entrées observées : 10 (disponible), 20 (active), 30 (terminée). `ab_reward` est protégé par
`ab_quest_complete` et `ab_reward_given` ; une seconde exécution ne rajoute ni or ni entrée.
`RuntimeState.quest_statuses` persiste aussi `active`, `completed` et `failed` via
`SetQuestStatus`. Le JRL complet n'est pas projeté ; les transitions d'échec sont disponibles au
niveau runtime mais la fixture n'en exerce pas.

## 9. Sauvegarde et reprise

Schéma `forge.aurora-m3-save@1`, qui contient :

- `SessionSnapshot` : zone courante, joueur, portes, zones chargées/visitées, événements, tick,
  transitions, distance, positions de retour ;
- `RuntimeState` : variables module/objet, journal, or, inventaire, morts, conversations, effets,
  natifs appelés, diagnostics ;
- `SchedulerSnapshot` : tick, travaux en attente, travaux terminés ;
- `continuation: Option<VmContinuation>` : reprise d'une exécution suspendue, testée par
  sérialisation JSON ; non utilisée pour suspendre le scénario M4.
- transcription de dialogue et étapes complétées.

La reprise recharge la session et l'état, puis rejoue volontairement `ab_mod_load` et `ab_reward`
pour prouver que les gardes d'initialisation et de récompense empêchent tout doublon. Le rapport
observe `resume_idempotent=true`, or et journal inchangés, et aucun travail différé rejoué.

## 10. Diagnostics

Le rapport `forge.aurora-m3-report@1` expose :

- `natives` : appels par native ;
- `nativeRegistry` : version du registre (`nwscript-ee-ordinals-v1`) ;
- `effects` : nombre de reçus appliqués ;
- `unsupported` : diagnostics `unknown-native`, `unsupported-native`, `native-arity`,
  `registry-mismatch` ;
- `executed_scripts`, `events`, `dialogue_transcript`, `journal_entries`, `gold`, `inventory` ;
- `save_sha256`, `resume_idempotent`.

Un script qui rencontre une native non supportée échoue avec une erreur typée et un diagnostic.
Le module n'est jamais déclaré complet si la liste `unsupported` n'est pas vide.

## 11. Limites explicites

- pas de compatibilité NWN générale, pas de ruleset D&D, pas de combat d20 ;
- pas de natives d'action, de heartbeat ni de files d'action NWScript ;
- suspension d'une instruction VM supportée au niveau VM, mais non encore intégrée à la sauvegarde
  M3/M4 de scénario ;
- pas de projection JRL/TLK/2DA complète ;
- pas de chargement visuel des modèles NWN pour M3 ; la preuve est headless ;
- chaînes NCS non UTF-8 décodées avec remplacement, pas de table de code page ;
- pas de bus de commandes `forge_gameplay`, de permissions ou de physique dans l'adaptateur M3 ;
- les événements non listés en section 2 restent non supportés.

## 12. Reproduction

```powershell
cargo test -p forge_nwn_runtime
cargo run -q -p forge_cli -- aurora-scenario `
  crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1 `
  --report E:/OpenRPG_Forge/Preuves-et-analyses/actives/aurora-bridge/m3/m3-report.json
```

Le détail des preuves, hashes et critères GO/NO-GO est consigné dans
`Preuves-et-analyses/actives/aurora-bridge/m3/README.md`.

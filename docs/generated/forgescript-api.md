# ForgeScript API 1.0

## audio_emitter

`AudioEmitterRef audio_emitter(const string &in id)` — Obtient un handle audio typé sans exposer le nœud moteur.
Availability: client, server
Example: `audio_emitter("a004...")`

## audio_is_active

`bool audio_is_active(AudioEmitterRef emitter)` — Lit l'état d'un émetteur audio.
Availability: client, server
Example: `audio_is_active(emitter)`

## audio_set_active

`void audio_set_active(AudioEmitterRef emitter, bool enabled)` — Demande une activation audio validée par le runtime.
Availability: server
Example: `audio_set_active(emitter, true)`

## cancel_movement

`void cancel_movement(MovementTaskRef task)` — Annule une tâche et publie son achèvement.
Availability: server
Example: `cancel_movement(task)`

## cast_ability

`void cast_ability(EntityRef target, const string &in ability)` — Demande une capacité au service de combat.
Availability: rpg-kit, server
Example: `cast_ability(target, "ash-nova")`

## create_effect

`EffectHandle create_effect(const string &in effectId)` — Crée un handle d'effet opaque sous autorité serveur.
Availability: server
Example: `EffectHandle ash = create_effect("ash-burn")`

## current_area

`AreaRef current_area()` — Obtient le handle opaque de la zone d'exécution.
Availability: client, server
Example: `AreaRef area = current_area()`

## destroy_entity

`void destroy_entity(EntityRef entity)` — Émet une commande de destruction validée par l'autorité.
Availability: server
Example: `destroy_entity(target)`

## dialogue_choices

`array<DialogueChoiceRef> dialogue_choices(DialogueNodeRef node)` — Lit les choix dans l'ordre priorité puis liste.
Availability: client, rpg-kit, server
Example: `dialogue_choices(node)`

## dialogue_entry

`DialogueNodeRef dialogue_entry(DialogueGraphRef graph)` — Lit le nœud d'entrée canonique du graphe.
Availability: client, rpg-kit, server
Example: `dialogue_entry(graph)`

## dialogue_graph

`DialogueGraphRef dialogue_graph(const string &in id)` — Obtient un handle de graphe sans exposer le stockage ou le moteur.
Availability: client, rpg-kit, server
Example: `dialogue_graph("4000...")`

## entity_position

`Vec3 entity_position(EntityRef entity)` — Lit une copie de la position canonique sans exposer le moteur.
Availability: client, server
Example: `Vec3 p = entity_position(hero)`

## face_towards

`MovementTaskRef face_towards(EntityRef entity, const Vec3 &in target)` — Crée une tâche d'orientation.
Availability: server
Example: `face_towards(hero, target)`

## find_entity

`EntityRef find_entity(AreaRef area, const string &in tag)` — Recherche une entité par tag dans la portée de zone bornée.
Availability: client, server
Example: `EntityRef door = find_entity(area, "door")`

## follow

`MovementTaskRef follow(EntityRef entity, EntityRef target, float distance)` — Crée une tâche de suivi à distance finie.
Availability: server
Example: `follow(companion, hero, 2.0f)`

## format_float

`string format_float(double value, uint precision)` — Formate un nombre fini avec une précision bornée.
Availability: client, server
Example: `say(format_float(distance, 2))`

## format_int

`string format_int(int64 value)` — Formate un entier selon la culture invariante.
Availability: client, server
Example: `say(format_int(score))`

## health

`int health(EntityRef target)` — Lit les points de vie courants.
Availability: client, server
Example: `health(hero)`

## local_get_entity

`EntityRef local_get_entity(EntityRef owner, const string &in key, EntityRef fallback)` — Lit un handle d'entité local opaque.
Availability: client, server
Example: `EntityRef target = local_get_entity(hero, "target", 0)`

## local_get_float

`double local_get_float(EntityRef owner, const string &in key, double fallback)` — Lit une variable numérique locale typée.
Availability: client, server
Example: `double speed = local_get_float(hero, "speed", 0)`

## local_get_int

`int64 local_get_int(EntityRef owner, const string &in key, int64 fallback)` — Lit une variable entière locale typée.
Availability: client, server
Example: `int64 stage = local_get_int(hero, "quest.stage", 0)`

## local_get_string

`string local_get_string(EntityRef owner, const string &in key, const string &in fallback)` — Lit une chaîne locale typée.
Availability: client, server
Example: `string title = local_get_string(hero, "title", "")`

## local_get_vector

`Vec3 local_get_vector(EntityRef owner, const string &in key, const Vec3 &in fallback)` — Lit un vecteur local typé.
Availability: client, server
Example: `Vec3 home = local_get_vector(hero, "home", vec3(0, 0, 0))`

## local_set_entity

`void local_set_entity(EntityRef owner, const string &in key, EntityRef value)` — Écrit un handle d'entité local sans pointeur natif.
Availability: server
Example: `local_set_entity(hero, "target", enemy)`

## local_set_float

`void local_set_float(EntityRef owner, const string &in key, double value)` — Écrit une variable numérique locale typée.
Availability: server
Example: `local_set_float(hero, "speed", 3.5)`

## local_set_int

`void local_set_int(EntityRef owner, const string &in key, int64 value)` — Écrit une variable entière locale typée.
Availability: server
Example: `local_set_int(hero, "quest.stage", 2)`

## local_set_string

`void local_set_string(EntityRef owner, const string &in key, const string &in value)` — Écrit une chaîne Unicode locale sous quota.
Availability: server
Example: `local_set_string(hero, "title", "Gardienne")`

## local_set_vector

`void local_set_vector(EntityRef owner, const string &in key, const Vec3 &in value)` — Écrit une position ou direction locale typée.
Availability: server
Example: `local_set_vector(hero, "home", goal)`

## move_to

`MovementTaskRef move_to(EntityRef entity, const Vec3 &in destination, const string &in mode)` — Crée une tâche de navigation avec identifiant stable et événement de fin.
Availability: server
Example: `MovementTaskRef task = move_to(hero, goal, "run")`

## quat

`Quat quat(float x, float y, float z, float w)` — Construit un quaternion fini.
Availability: client, server
Example: `Quat rotation = quat(0, 0, 0, 1)`

## quat_normalize

`Quat quat_normalize(const Quat &in value)` — Normalise un quaternion de façon déterministe.
Availability: client, server
Example: `rotation = quat_normalize(rotation)`

## quest

`void quest(const string &in transition)` — Demande une transition de quête validée.
Availability: rpg-kit, server
Example: `quest("crypt.completed")`

## quest_journal

`QuestJournalRef quest_journal(const string &in id)` — Obtient un handle de journal de quête typé et versionné.
Availability: client, rpg-kit, server
Example: `quest_journal("a808...")`

## quest_simulate

`QuestSimulationResult quest_simulate(QuestJournalRef journal, const string &in scenario)` — Exécute un scénario borné en sandbox sans mutation du projet.
Availability: rpg-kit, server
Example: `quest_simulate(journal, "author-preview")`

## quest_transitions

`array<QuestTransitionRef> quest_transitions(QuestJournalRef journal)` — Lit les transitions ordonnées sans exposer le document ou le moteur.
Availability: client, rpg-kit, server
Example: `quest_transitions(journal)`

## random_int

`int random_int(int minimum, int maximum)` — Produit un entier déterministe depuis la graine de simulation.
Availability: client, server
Example: `int roll = random_int(1, 20)`

## regex_match

`bool regex_match(const string &in value, const string &in pattern)` — Évalue une expression régulière ECMAScript bornée.
Availability: client, server
Example: `bool valid = regex_match(tag, "^[a-z-]+$")`

## release_effect

`void release_effect(EffectHandle effect)` — Libère un handle d'effet et invalide sa génération.
Availability: server
Example: `release_effect(ash)`

## resolve_asset

`AssetId resolve_asset(const string &in assetId)` — Résout un identifiant d'asset en handle opaque typé et générationnel.
Availability: client, server
Example: `AssetId icon = resolve_asset("ui/quest-marker")`

## say

`void say(const string &in text)` — Publie une réplique localisable.
Availability: client, server
Example: `say("Bienvenue")`

## set_velocity

`void set_velocity(EntityRef entity, const Vec3 &in velocity)` — Définit une vélocité validée par le runtime.
Availability: server
Example: `set_velocity(hero, vec3(0, 4, 0))`

## simulation_tick

`int64 simulation_tick()` — Lit le tick de simulation autoritaire injecté par l'hôte.
Availability: client, server
Example: `int64 tick = simulation_tick()`

## simulation_time_ms

`int64 simulation_time_ms()` — Lit le temps de simulation en millisecondes sans horloge système.
Availability: client, server
Example: `int64 now = simulation_time_ms()`

## spawn

`EntityRef spawn(const string &in blueprint)` — Crée une entité sous quota et autorité serveur.
Availability: server
Example: `spawn("ash-guardian")`

## spawn_at

`EntityRef spawn_at(AreaRef area, const string &in blueprint, const Vec3 &in position)` — Émet une commande de spawn serveur sous quota.
Availability: server
Example: `spawn_at(area, "guard", vec3(2, 0, 4))`

## teleport

`void teleport(EntityRef entity, AreaRef area, const Vec3 &in destination)` — Émet une téléportation canonique avec zone et destination.
Availability: server
Example: `teleport(hero, area, goal)`

## unicode_length

`uint unicode_length(const string &in value)` — Compte les points de code UTF-8 sous quota.
Availability: client, server
Example: `uint letters = unicode_length(name)`

## unlock

`void unlock(EntityRef target)` — Déverrouille une cible autorisée.
Availability: server
Example: `unlock(door)`

## variable

`void variable(const string &in key, const string &in value)` — Modifie une variable autorisée.
Availability: server
Example: `variable("door", "open")`

## vec_dot

`float vec_dot(const Vec3 &in left, const Vec3 &in right)` — Calcule un produit scalaire déterministe.
Availability: client, server
Example: `float alignment = vec_dot(a, b)`

## vec_length

`float vec_length(const Vec3 &in value)` — Calcule une longueur déterministe.
Availability: client, server
Example: `float distance = vec_length(goal)`

## vec_normalize

`Vec3 vec_normalize(const Vec3 &in value)` — Normalise un vecteur fini, zéro restant zéro.
Availability: client, server
Example: `Vec3 direction = vec_normalize(delta)`

## vec3

`Vec3 vec3(float x, float y, float z)` — Construit un vecteur fini.
Availability: client, server
Example: `Vec3 goal = vec3(1, 0, 3)`

## Ressources MCP

- `forge://api/audio_emitter`
- `forge://api/audio_is_active`
- `forge://api/audio_set_active`
- `forge://api/cancel_movement`
- `forge://api/cast_ability`
- `forge://api/create_effect`
- `forge://api/current_area`
- `forge://api/destroy_entity`
- `forge://api/dialogue_choices`
- `forge://api/dialogue_entry`
- `forge://api/dialogue_graph`
- `forge://api/entity_position`
- `forge://api/face_towards`
- `forge://api/find_entity`
- `forge://api/follow`
- `forge://api/format_float`
- `forge://api/format_int`
- `forge://api/health`
- `forge://api/local_get_entity`
- `forge://api/local_get_float`
- `forge://api/local_get_int`
- `forge://api/local_get_string`
- `forge://api/local_get_vector`
- `forge://api/local_set_entity`
- `forge://api/local_set_float`
- `forge://api/local_set_int`
- `forge://api/local_set_string`
- `forge://api/local_set_vector`
- `forge://api/move_to`
- `forge://api/quat`
- `forge://api/quat_normalize`
- `forge://api/quest`
- `forge://api/quest_journal`
- `forge://api/quest_simulate`
- `forge://api/quest_transitions`
- `forge://api/random_int`
- `forge://api/regex_match`
- `forge://api/release_effect`
- `forge://api/resolve_asset`
- `forge://api/say`
- `forge://api/set_velocity`
- `forge://api/simulation_tick`
- `forge://api/simulation_time_ms`
- `forge://api/spawn`
- `forge://api/spawn_at`
- `forge://api/teleport`
- `forge://api/unicode_length`
- `forge://api/unlock`
- `forge://api/variable`
- `forge://api/vec_dot`
- `forge://api/vec_length`
- `forge://api/vec_normalize`
- `forge://api/vec3`

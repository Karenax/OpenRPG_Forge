# ForgeScript API 1.0

> Generated from the versioned API registration catalog. Do not edit manually.

## action_cancel

`void action_cancel(ScriptTaskRef task)` — Annule une action par son handle opaque et publie son résultat.
Availability: rpg-kit, server
MCP resource: `forge://api/action_cancel`
Example: `action_cancel(task)`

## action_complete

`void action_complete(ScriptTaskRef task, const string &in value)` — Termine une action en conservant une valeur de complétion bornée.
Availability: rpg-kit, server
MCP resource: `forge://api/action_complete`
Example: `action_complete(task, "hit")`

## action_enqueue

`ScriptTaskRef action_enqueue(EntityRef owner, const string &in action, int priority)` — Ajoute une action typée à la file déterministe de l'entité.
Availability: rpg-kit, server
MCP resource: `forge://api/action_enqueue`
Example: `ScriptTaskRef task = action_enqueue(hero, "attack", 100)`

## audio_caption

`AudioCaptionRef audio_caption(AudioEmitterRef emitter, const string &in locale)` — Lit une caption localisée sans exposer le HUD ou le nœud audio.
Availability: client, server
MCP resource: `forge://api/audio_caption`
Example: `audio_caption(emitter, "fr")`

## audio_complete

`void audio_complete(AudioEmitterRef emitter, const string &in outcome)` — Clôt une lecture et publie son résultat de complétion.
Availability: server
MCP resource: `forge://api/audio_complete`
Example: `audio_complete(music, "finished")`

## audio_emitter

`AudioEmitterRef audio_emitter(const string &in id)` — Obtient un handle audio typé sans exposer le nœud moteur.
Availability: client, server
MCP resource: `forge://api/audio_emitter`
Example: `audio_emitter("a004...")`

## audio_fade

`void audio_fade(AudioEmitterRef emitter, float gain, float seconds)` — Applique un fondu borné et publie son événement.
Availability: server
MCP resource: `forge://api/audio_fade`
Example: `audio_fade(emitter, 0.0f, 1.5f)`

## audio_is_active

`bool audio_is_active(AudioEmitterRef emitter)` — Lit l'état d'un émetteur audio.
Availability: client, server
MCP resource: `forge://api/audio_is_active`
Example: `audio_is_active(emitter)`

## audio_listener

`void audio_listener(EntityRef listener, const string &in policy)` — Applique une politique d'écoute camera, entity, player ou every-listener.
Availability: server
MCP resource: `forge://api/audio_listener`
Example: `audio_listener(hero, "player")`

## audio_pause

`void audio_pause(AudioEmitterRef emitter)` — Suspend une lecture sans perdre son handle typé.
Availability: server
MCP resource: `forge://api/audio_pause`
Example: `audio_pause(emitter)`

## audio_resume

`void audio_resume(AudioEmitterRef emitter)` — Reprend une lecture suspendue.
Availability: server
MCP resource: `forge://api/audio_resume`
Example: `audio_resume(emitter)`

## audio_set_active

`void audio_set_active(AudioEmitterRef emitter, bool enabled)` — Demande une activation audio validée par le runtime.
Availability: server
MCP resource: `forge://api/audio_set_active`
Example: `audio_set_active(emitter, true)`

## audio_snapshot

`void audio_snapshot(const string &in snapshot)` — Active un snapshot de mixage validé sans exposer le moteur audio.
Availability: server
MCP resource: `forge://api/audio_snapshot`
Example: `audio_snapshot("combat")`

## audio_start

`void audio_start(AudioEmitterRef emitter)` — Démarre une instance selon la politique configurée.
Availability: server
MCP resource: `forge://api/audio_start`
Example: `audio_start(emitter)`

## audio_stop

`void audio_stop(AudioEmitterRef emitter)` — Arrête toutes les instances autorisées.
Availability: server
MCP resource: `forge://api/audio_stop`
Example: `audio_stop(emitter)`

## cancel_movement

`void cancel_movement(MovementTaskRef task)` — Annule une tâche et publie son achèvement.
Availability: server
MCP resource: `forge://api/cancel_movement`
Example: `cancel_movement(task)`

## cast_ability

`void cast_ability(EntityRef target, const string &in ability)` — Demande une capacité au service de combat.
Availability: rpg-kit, server
MCP resource: `forge://api/cast_ability`
Example: `cast_ability(target, "ash-nova")`

## checkpoint_respawn

`RespawnResult checkpoint_respawn(CheckpointRef checkpoint, EntityRef actor)` — Demande un respawn valide avec cout, groupe, cadavre et spawn sur.
Availability: server
MCP resource: `forge://api/checkpoint_respawn`
Example: `RespawnResult result = checkpoint_respawn(checkpoint, hero)`

## combat_attack

`bool combat_attack(EntityRef attacker, EntityRef target, const string &in ability, float amount, int seed)` — Émet une attaque attribuée, seedée et autoritaire vers le service de combat.
Availability: rpg-kit, server
MCP resource: `forge://api/combat_attack`
Example: `combat_attack(hero, target, "slash", 12.0f, 97)`

## control_context

`ControlLockRef control_context(GameplaySessionRef session)` — Lit la pile de controles avec proprietaire et restauration garantie.
Availability: client, server
MCP resource: `forge://api/control_context`
Example: `ControlLockRef controls = control_context(session)`

## create_effect

`EffectHandle create_effect(const string &in effectId)` — Crée un handle d'effet opaque sous autorité serveur.
Availability: server
MCP resource: `forge://api/create_effect`
Example: `EffectHandle ash = create_effect("ash-burn")`

## current_area

`AreaRef current_area()` — Obtient le handle opaque de la zone d'exécution.
Availability: client, server
MCP resource: `forge://api/current_area`
Example: `AreaRef area = current_area()`

## custom_event

`CustomEventRef custom_event(const string &in name, int version, const string &in payload)` — Construit un événement custom borné après validation du schéma de payload.
Availability: server
MCP resource: `forge://api/custom_event`
Example: `CustomEventRef evt = custom_event("forge.damage", 1, "amount=4")`

## debug_break

`DebugSessionRef debug_break(const string &in session_id, const string &in authority)` — Ouvre une session de débogage locale ou serveur permissionnée.
Availability: client, server
MCP resource: `forge://api/debug_break`
Example: `DebugSessionRef debug = debug_break("session:1", "local-author")`

## debug_step

`void debug_step(DebugSessionRef session, const string &in mode)` — Exécute into, over, out ou continue sur une session suspendue.
Availability: client, server
MCP resource: `forge://api/debug_step`
Example: `debug_step(debug, "over")`

## destroy_entity

`void destroy_entity(EntityRef entity)` — Émet une commande de destruction validée par l'autorité.
Availability: server
MCP resource: `forge://api/destroy_entity`
Example: `destroy_entity(target)`

## deterministic_delay

`void deterministic_delay(int64 delay_ms, const string &in handler)` — Planifie un délai dans le temps de simulation, jamais sur l'horloge système.
Availability: server
MCP resource: `forge://api/deterministic_delay`
Example: `deterministic_delay(250, "OnReady")`

## dialogue_authoring

`DialogueAuthoringRef dialogue_authoring(DialogueGraphRef graph)` — Lit tokens, participants, modes, sources du wizard et notes sans exposer le document.
Availability: client, rpg-kit, server
MCP resource: `forge://api/dialogue_authoring`
Example: `DialogueAuthoringRef authoring = dialogue_authoring(graph)`

## dialogue_choices

`array<DialogueChoiceRef> dialogue_choices(DialogueNodeRef node)` — Lit les choix dans l'ordre priorité puis liste.
Availability: client, rpg-kit, server
MCP resource: `forge://api/dialogue_choices`
Example: `dialogue_choices(node)`

## dialogue_complete

`void dialogue_complete(DialogueSessionRef session, const string &in outcome)` — Termine une session de dialogue avec un résultat borné.
Availability: rpg-kit, server
MCP resource: `forge://api/dialogue_complete`
Example: `dialogue_complete(talk, "accepted")`

## dialogue_entry

`DialogueNodeRef dialogue_entry(DialogueGraphRef graph)` — Lit le nœud d'entrée canonique du graphe.
Availability: client, rpg-kit, server
MCP resource: `forge://api/dialogue_entry`
Example: `dialogue_entry(graph)`

## dialogue_evaluation

`DialogueEvaluationRef dialogue_evaluation(DialogueNodeRef node)` — Lit le mode d'evaluation explicite et les poids deterministes.
Availability: client, rpg-kit, server
MCP resource: `forge://api/dialogue_evaluation`
Example: `DialogueEvaluationRef evaluation = dialogue_evaluation(node)`

## dialogue_experience

`DialogueExperienceRef dialogue_experience(DialogueGraphRef graph)` — Lit cycle fin/abandon, recherche, revue, mémoire et checks de compétence par handle typé.
Availability: client, rpg-kit, server
MCP resource: `forge://api/dialogue_experience`
Example: `DialogueExperienceRef experience = dialogue_experience(graph)`

## dialogue_graph

`DialogueGraphRef dialogue_graph(const string &in id)` — Obtient un handle de graphe sans exposer le stockage ou le moteur.
Availability: client, rpg-kit, server
MCP resource: `forge://api/dialogue_graph`
Example: `dialogue_graph("4000...")`

## dialogue_notes

`DialogueNotesRef dialogue_notes(DialogueNodeRef node)` — Lit intentions, emotion, source, statut, TODO et auteur sans les afficher au joueur.
Availability: client, rpg-kit, server
MCP resource: `forge://api/dialogue_notes`
Example: `DialogueNotesRef notes = dialogue_notes(node)`

## dialogue_participant

`DialogueParticipantRef dialogue_participant(DialogueNodeRef node)` — Lit le participant resolu par role, query, tag ou entite et son fallback.
Availability: client, rpg-kit, server
MCP resource: `forge://api/dialogue_participant`
Example: `DialogueParticipantRef speaker = dialogue_participant(node)`

## dialogue_start

`DialogueSessionRef dialogue_start(const string &in graph_id, EntityRef speaker, EntityRef listener)` — Démarre un graphe de dialogue avec participants explicites.
Availability: rpg-kit, server
MCP resource: `forge://api/dialogue_start`
Example: `DialogueSessionRef talk = dialogue_start("intro", warden, hero)`

## dialogue_tokens

`array<DialogueTokenRef> dialogue_tokens(DialogueNodeRef node)` — Lit les tokens types du noeud par handles generationnels.
Availability: client, rpg-kit, server
MCP resource: `forge://api/dialogue_tokens`
Example: `array<DialogueTokenRef> tokens = dialogue_tokens(node)`

## effect_apply

`EffectHandle effect_apply(EntityRef source, EntityRef target, const string &in effect_id, float magnitude, float duration_seconds)` — Applique un effet typé borné et retourne un handle générationnel.
Availability: rpg-kit, server
MCP resource: `forge://api/effect_apply`
Example: `EffectHandle burning = effect_apply(hero, target, "burning", 2.0f, 10.0f)`

## effect_dispel

`void effect_dispel(EntityRef target, const string &in tag)` — Dissipe les effets autorisés partageant le tag demandé.
Availability: rpg-kit, server
MCP resource: `forge://api/effect_dispel`
Example: `effect_dispel(target, "fire")`

## entity_position

`Vec3 entity_position(EntityRef entity)` — Lit une copie de la position canonique sans exposer le moteur.
Availability: client, server
MCP resource: `forge://api/entity_position`
Example: `Vec3 p = entity_position(hero)`

## event_publish

`void event_publish(CustomEventRef event_ref)` — Publie un événement custom autoritaire avec corrélation et trace.
Availability: server
MCP resource: `forge://api/event_publish`
Example: `event_publish(evt)`

## event_subscribe

`void event_subscribe(const string &in name, int version, const string &in handler)` — Abonne un handler à un événement namespacé et versionné.
Availability: server
MCP resource: `forge://api/event_subscribe`
Example: `event_subscribe("forge.damage", 1, "OnDamage")`

## face_towards

`MovementTaskRef face_towards(EntityRef entity, const Vec3 &in target)` — Crée une tâche d'orientation.
Availability: server
MCP resource: `forge://api/face_towards`
Example: `face_towards(hero, target)`

## faction_reputation

`FactionResultRef faction_reputation(EntityRef actor, const string &in faction_id, int delta, bool join, const string &in scope, const string &in cause)` — Modifie adhésion et réputation avec scope, cause et bornes autoritaires.
Availability: rpg-kit, server
MCP resource: `forge://api/faction_reputation`
Example: `FactionResultRef standing = faction_reputation(hero, "wardens", 25, true, "module", "rescued-member")`

## find_entity

`EntityRef find_entity(AreaRef area, const string &in tag)` — Recherche une entité par tag dans la portée de zone bornée.
Availability: client, server
MCP resource: `forge://api/find_entity`
Example: `EntityRef door = find_entity(area, "door")`

## flight_simulation

`FlightSimulationRef flight_simulation()` — Obtient le profil de vol 3D type sans exposer le noeud moteur.
Availability: client, server
MCP resource: `forge://api/flight_simulation`
Example: `FlightSimulationRef flight = flight_simulation()`

## follow

`MovementTaskRef follow(EntityRef entity, EntityRef target, float distance)` — Crée une tâche de suivi à distance finie.
Availability: server
MCP resource: `forge://api/follow`
Example: `follow(companion, hero, 2.0f)`

## format_float

`string format_float(double value, uint precision)` — Formate un nombre fini avec une précision bornée.
Availability: client, server
MCP resource: `forge://api/format_float`
Example: `say(format_float(distance, 2))`

## format_int

`string format_int(int64 value)` — Formate un entier selon la culture invariante.
Availability: client, server
MCP resource: `forge://api/format_int`
Example: `say(format_int(score))`

## gameplay_session

`GameplaySessionRef gameplay_session()` — Obtient la session gameplay canonique par handle generationnel.
Availability: client, server
MCP resource: `forge://api/gameplay_session`
Example: `GameplaySessionRef session = gameplay_session()`

## health

`int health(EntityRef target)` — Lit les points de vie courants.
Availability: client, server
MCP resource: `forge://api/health`
Example: `health(hero)`

## hot_reload_request

`HotReloadRef hot_reload_request(const string &in module_id, int version, const string &in source_hash)` — Prépare un reload sûr à la frontière d'un handler avec migration et invalidation.
Availability: server
MCP resource: `forge://api/hot_reload_request`
Example: `HotReloadRef reload = hot_reload_request("module", 2, "sha256:new")`

## inventory_transfer

`InventoryResultRef inventory_transfer(EntityRef from, EntityRef to, const string &in item_id, int quantity)` — Transfère un item par transaction autoritaire avec capacité, rollback et audit.
Availability: rpg-kit, server
MCP resource: `forge://api/inventory_transfer`
Example: `InventoryResultRef loot = inventory_transfer(chest, hero, "crypt-key", 1)`

## local_get_entity

`EntityRef local_get_entity(EntityRef owner, const string &in key, EntityRef fallback)` — Lit un handle d'entité local opaque.
Availability: client, server
MCP resource: `forge://api/local_get_entity`
Example: `EntityRef target = local_get_entity(hero, "target", 0)`

## local_get_float

`double local_get_float(EntityRef owner, const string &in key, double fallback)` — Lit une variable numérique locale typée.
Availability: client, server
MCP resource: `forge://api/local_get_float`
Example: `double speed = local_get_float(hero, "speed", 0)`

## local_get_int

`int64 local_get_int(EntityRef owner, const string &in key, int64 fallback)` — Lit une variable entière locale typée.
Availability: client, server
MCP resource: `forge://api/local_get_int`
Example: `int64 stage = local_get_int(hero, "quest.stage", 0)`

## local_get_string

`string local_get_string(EntityRef owner, const string &in key, const string &in fallback)` — Lit une chaîne locale typée.
Availability: client, server
MCP resource: `forge://api/local_get_string`
Example: `string title = local_get_string(hero, "title", "")`

## local_get_vector

`Vec3 local_get_vector(EntityRef owner, const string &in key, const Vec3 &in fallback)` — Lit un vecteur local typé.
Availability: client, server
MCP resource: `forge://api/local_get_vector`
Example: `Vec3 home = local_get_vector(hero, "home", vec3(0, 0, 0))`

## local_set_entity

`void local_set_entity(EntityRef owner, const string &in key, EntityRef value)` — Écrit un handle d'entité local sans pointeur natif.
Availability: server
MCP resource: `forge://api/local_set_entity`
Example: `local_set_entity(hero, "target", enemy)`

## local_set_float

`void local_set_float(EntityRef owner, const string &in key, double value)` — Écrit une variable numérique locale typée.
Availability: server
MCP resource: `forge://api/local_set_float`
Example: `local_set_float(hero, "speed", 3.5)`

## local_set_int

`void local_set_int(EntityRef owner, const string &in key, int64 value)` — Écrit une variable entière locale typée.
Availability: server
MCP resource: `forge://api/local_set_int`
Example: `local_set_int(hero, "quest.stage", 2)`

## local_set_string

`void local_set_string(EntityRef owner, const string &in key, const string &in value)` — Écrit une chaîne Unicode locale sous quota.
Availability: server
MCP resource: `forge://api/local_set_string`
Example: `local_set_string(hero, "title", "Gardienne")`

## local_set_vector

`void local_set_vector(EntityRef owner, const string &in key, const Vec3 &in value)` — Écrit une position ou direction locale typée.
Availability: server
MCP resource: `forge://api/local_set_vector`
Example: `local_set_vector(hero, "home", goal)`

## locator

`LocatorRef locator(AreaRef area, const string &in id)` — Obtient une ancre audio, VFX, spawn, objectif ou gameplay typée.
Availability: client, server
MCP resource: `forge://api/locator`
Example: `LocatorRef fire = locator(area, "a065...")`

## locator_collection

`LocatorCollectionRef locator_collection(AreaRef area, const string &in id)` — Résout une collection par requête avec ordre stable ou sélection déterministe.
Availability: client, server
MCP resource: `forge://api/locator_collection`
Example: `LocatorCollectionRef anchors = locator_collection(area, "a065...")`

## move_to

`MovementTaskRef move_to(EntityRef entity, const Vec3 &in destination, const string &in mode)` — Crée une tâche de navigation avec identifiant stable et événement de fin.
Availability: server
MCP resource: `forge://api/move_to`
Example: `MovementTaskRef task = move_to(hero, goal, "run")`

## patrol_next

`PatrolStepRef patrol_next(PatrolRouteRef route, int deterministic_roll)` — Avance une patrouille déterministe selon sa politique canonique.
Availability: server
MCP resource: `forge://api/patrol_next`
Example: `PatrolStepRef step = patrol_next(route, 42)`

## perception_query

`PerceptionResultRef perception_query(EntityRef observer, EntityRef target, const string &in sense, float distance, float range, bool line_of_sight, float signal, int seed)` — Évalue une perception déterministe avec sens, portée, LOS, stimulus et trace.
Availability: rpg-kit, server
MCP resource: `forge://api/perception_query`
Example: `PerceptionResultRef seen = perception_query(guard, hero, "sight", 2.0f, 10.0f, true, 0.8f, 42)`

## physics_scene

`PhysicsSimulationRef physics_scene()` — Obtient la scene physique canonique et ses corps et liaisons types.
Availability: client, server
MCP resource: `forge://api/physics_scene`
Example: `PhysicsSimulationRef physics = physics_scene()`

## projectile_launch

`ProjectileRef projectile_launch(ProjectileSimulationRef profile, const Vec3 &in origin, const Vec3 &in target)` — Lance un projectile valide sous autorite et revision serveur.
Availability: server
MCP resource: `forge://api/projectile_launch`
Example: `ProjectileRef bolt = projectile_launch(projectile_simulation(), origin, target)`

## quat

`Quat quat(float x, float y, float z, float w)` — Construit un quaternion fini.
Availability: client, server
MCP resource: `forge://api/quat`
Example: `Quat rotation = quat(0, 0, 0, 1)`

## quat_normalize

`Quat quat_normalize(const Quat &in value)` — Normalise un quaternion de façon déterministe.
Availability: client, server
MCP resource: `forge://api/quat_normalize`
Example: `rotation = quat_normalize(rotation)`

## quest

`void quest(const string &in transition)` — Demande une transition de quête validée.
Availability: rpg-kit, server
MCP resource: `forge://api/quest`
Example: `quest("crypt.completed")`

## quest_journal

`QuestJournalRef quest_journal(const string &in id)` — Obtient un handle de journal de quête typé et versionné.
Availability: client, rpg-kit, server
MCP resource: `forge://api/quest_journal`
Example: `quest_journal("a808...")`

## quest_simulate

`QuestSimulationResult quest_simulate(QuestJournalRef journal, const string &in scenario)` — Exécute un scénario borné en sandbox sans mutation du projet.
Availability: rpg-kit, server
MCP resource: `forge://api/quest_simulate`
Example: `quest_simulate(journal, "author-preview")`

## quest_transition

`QuestResultRef quest_transition(const string &in quest_id, const string &in scope_id, const string &in from_status, const string &in to_status, const string &in objective_id, int delta, const string &in cause)` — Applique une transition de quête scoped avec objectif, historique et cause.
Availability: rpg-kit, server
MCP resource: `forge://api/quest_transition`
Example: `QuestResultRef step = quest_transition("crypt", "party:1", "active", "completed", "find-key", 1, "found")`

## quest_transitions

`array<QuestTransitionRef> quest_transitions(QuestJournalRef journal)` — Lit les transitions ordonnées sans exposer le document ou le moteur.
Availability: client, rpg-kit, server
MCP resource: `forge://api/quest_transitions`
Example: `quest_transitions(journal)`

## random_int

`int random_int(int minimum, int maximum)` — Produit un entier déterministe depuis la graine de simulation.
Availability: client, server
MCP resource: `forge://api/random_int`
Example: `int roll = random_int(1, 20)`

## regex_match

`bool regex_match(const string &in value, const string &in pattern)` — Évalue une expression régulière ECMAScript bornée.
Availability: client, server
MCP resource: `forge://api/regex_match`
Example: `bool valid = regex_match(tag, "^[a-z-]+$")`

## release_effect

`void release_effect(EffectHandle effect)` — Libère un handle d'effet et invalide sa génération.
Availability: server
MCP resource: `forge://api/release_effect`
Example: `release_effect(ash)`

## replay_test

`ReplayReportRef replay_test(ReplayTestRef replay)` — Execute le replay headless autoritaire et produit un rapport borne.
Availability: server
MCP resource: `forge://api/replay_test`
Example: `ReplayReportRef report = replay_test(replay)`

## resolve_asset

`AssetId resolve_asset(const string &in assetId)` — Résout un identifiant d'asset en handle opaque typé et générationnel.
Availability: client, server
MCP resource: `forge://api/resolve_asset`
Example: `AssetId icon = resolve_asset("ui/quest-marker")`

## route_diagnostics

`RouteDiagnosticsRef route_diagnostics(PatrolRouteRef route)` — Lit segments, sens, coûts, actions, liens cassés, projection navigation et timing.
Availability: client, server
MCP resource: `forge://api/route_diagnostics`
Example: `RouteDiagnosticsRef debug = route_diagnostics(route)`

## runtime_shell

`RuntimeShellRef runtime_shell(GameplaySessionRef session)` — Lit le shell carte, journal et HUD fourni par le kit.
Availability: client, server
MCP resource: `forge://api/runtime_shell`
Example: `RuntimeShellRef shell = runtime_shell(session)`

## save_policy

`PersistencePolicyRef save_policy(GameplaySessionRef session)` — Lit portee, autorite, conflits, cloud et confidentialite sans acces fichier.
Availability: client, server
MCP resource: `forge://api/save_policy`
Example: `PersistencePolicyRef saves = save_policy(session)`

## say

`void say(const string &in text)` — Publie une réplique localisable.
Availability: client, server
MCP resource: `forge://api/say`
Example: `say("Bienvenue")`

## set_velocity

`void set_velocity(EntityRef entity, const Vec3 &in velocity)` — Définit une vélocité validée par le runtime.
Availability: server
MCP resource: `forge://api/set_velocity`
Example: `set_velocity(hero, vec3(0, 4, 0))`

## simulation_tick

`int64 simulation_tick()` — Lit le tick de simulation autoritaire injecté par l'hôte.
Availability: client, server
MCP resource: `forge://api/simulation_tick`
Example: `int64 tick = simulation_tick()`

## simulation_time_ms

`int64 simulation_time_ms()` — Lit le temps de simulation en millisecondes sans horloge système.
Availability: client, server
MCP resource: `forge://api/simulation_time_ms`
Example: `int64 now = simulation_time_ms()`

## spawn

`EntityRef spawn(const string &in blueprint)` — Crée une entité sous quota et autorité serveur.
Availability: server
MCP resource: `forge://api/spawn`
Example: `spawn("ash-guardian")`

## spawn_at

`EntityRef spawn_at(AreaRef area, const string &in blueprint, const Vec3 &in position)` — Émet une commande de spawn serveur sous quota.
Availability: server
MCP resource: `forge://api/spawn_at`
Example: `spawn_at(area, "guard", vec3(2, 0, 4))`

## target_preview

`TargetPreviewRef target_preview(TargetingSimulationRef profile, const Vec3 &in origin, const Vec3 &in direction)` — Previsualise une forme de ciblage et sa validite sans mutation.
Availability: client, server
MCP resource: `forge://api/target_preview`
Example: `TargetPreviewRef preview = target_preview(targeting_simulation(), origin, direction)`

## teleport

`void teleport(EntityRef entity, AreaRef area, const Vec3 &in destination)` — Émet une téléportation canonique avec zone et destination.
Availability: server
MCP resource: `forge://api/teleport`
Example: `teleport(hero, area, goal)`

## timer_cancel

`void timer_cancel(TimerRef timer)` — Annule un timer par son handle générationnel.
Availability: server
MCP resource: `forge://api/timer_cancel`
Example: `timer_cancel(timer)`

## timer_schedule

`TimerRef timer_schedule(int64 delay_ms, int64 repeat_ms, const string &in handler, const string &in schedule)` — Planifie un timer ou schedule déterministe sous quota.
Availability: server
MCP resource: `forge://api/timer_schedule`
Example: `TimerRef timer = timer_schedule(1000, 5000, "OnTick", "daily")`

## unicode_length

`uint unicode_length(const string &in value)` — Compte les points de code UTF-8 sous quota.
Availability: client, server
MCP resource: `forge://api/unicode_length`
Example: `uint letters = unicode_length(name)`

## unlock

`void unlock(EntityRef target)` — Déverrouille une cible autorisée.
Availability: server
MCP resource: `forge://api/unlock`
Example: `unlock(door)`

## variable

`void variable(const string &in key, const string &in value)` — Modifie une variable autorisée.
Availability: server
MCP resource: `forge://api/variable`
Example: `variable("door", "open")`

## vec3

`Vec3 vec3(float x, float y, float z)` — Construit un vecteur fini.
Availability: client, server
MCP resource: `forge://api/vec3`
Example: `Vec3 goal = vec3(1, 0, 3)`

## vec_dot

`float vec_dot(const Vec3 &in left, const Vec3 &in right)` — Calcule un produit scalaire déterministe.
Availability: client, server
MCP resource: `forge://api/vec_dot`
Example: `float alignment = vec_dot(a, b)`

## vec_length

`float vec_length(const Vec3 &in value)` — Calcule une longueur déterministe.
Availability: client, server
MCP resource: `forge://api/vec_length`
Example: `float distance = vec_length(goal)`

## vec_normalize

`Vec3 vec_normalize(const Vec3 &in value)` — Normalise un vecteur fini, zéro restant zéro.
Availability: client, server
MCP resource: `forge://api/vec_normalize`
Example: `Vec3 direction = vec_normalize(delta)`

## waypoint

`WaypointRef waypoint(AreaRef area, const string &in id)` — Obtient un handle de waypoint typé et générationnel.
Availability: client, server
MCP resource: `forge://api/waypoint`
Example: `WaypointRef marker = waypoint(area, "a064...")`

## waypoint_arrive

`ArrivalRef waypoint_arrive(WaypointRef waypoint)` — Exécute orientation, animation, attente, interaction et callback sous autorité.
Availability: server
MCP resource: `forge://api/waypoint_arrive`
Example: `ArrivalRef arrival = waypoint_arrive(marker)`

## waypoint_reveal

`bool waypoint_reveal(WaypointRef waypoint, const string &in scope, const string &in event_name)` — Révèle un marqueur par la commande runtime autoritaire.
Availability: server
MCP resource: `forge://api/waypoint_reveal`
Example: `waypoint_reveal(marker, "party", "quest.crypt.found")`

## world_calendar_ms

`int64 world_calendar_ms(WorldClockRef clock)` — Lit le calendrier déterministe dérivé de l'horloge monde.
Availability: client, server
MCP resource: `forge://api/world_calendar_ms`
Example: `int64 calendar = world_calendar_ms(clock)`

## world_clock

`WorldClockRef world_clock()` — Obtient l'horloge monde opaque liée au temps de simulation.
Availability: client, server
MCP resource: `forge://api/world_clock`
Example: `WorldClockRef clock = world_clock()`

## world_time

`WorldTimeSimulationRef world_time()` — Obtient l'horloge, le calendrier et les planifications par handle type.
Availability: client, server
MCP resource: `forge://api/world_time`
Example: `WorldTimeSimulationRef clock = world_time()`

## Ressources MCP

- `forge://api/action_cancel`
- `forge://api/action_complete`
- `forge://api/action_enqueue`
- `forge://api/audio_caption`
- `forge://api/audio_complete`
- `forge://api/audio_emitter`
- `forge://api/audio_fade`
- `forge://api/audio_is_active`
- `forge://api/audio_listener`
- `forge://api/audio_pause`
- `forge://api/audio_resume`
- `forge://api/audio_set_active`
- `forge://api/audio_snapshot`
- `forge://api/audio_start`
- `forge://api/audio_stop`
- `forge://api/cancel_movement`
- `forge://api/cast_ability`
- `forge://api/checkpoint_respawn`
- `forge://api/combat_attack`
- `forge://api/control_context`
- `forge://api/create_effect`
- `forge://api/current_area`
- `forge://api/custom_event`
- `forge://api/debug_break`
- `forge://api/debug_step`
- `forge://api/destroy_entity`
- `forge://api/deterministic_delay`
- `forge://api/dialogue_authoring`
- `forge://api/dialogue_choices`
- `forge://api/dialogue_complete`
- `forge://api/dialogue_entry`
- `forge://api/dialogue_evaluation`
- `forge://api/dialogue_experience`
- `forge://api/dialogue_graph`
- `forge://api/dialogue_notes`
- `forge://api/dialogue_participant`
- `forge://api/dialogue_start`
- `forge://api/dialogue_tokens`
- `forge://api/effect_apply`
- `forge://api/effect_dispel`
- `forge://api/entity_position`
- `forge://api/event_publish`
- `forge://api/event_subscribe`
- `forge://api/face_towards`
- `forge://api/faction_reputation`
- `forge://api/find_entity`
- `forge://api/flight_simulation`
- `forge://api/follow`
- `forge://api/format_float`
- `forge://api/format_int`
- `forge://api/gameplay_session`
- `forge://api/health`
- `forge://api/hot_reload_request`
- `forge://api/inventory_transfer`
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
- `forge://api/locator`
- `forge://api/locator_collection`
- `forge://api/move_to`
- `forge://api/patrol_next`
- `forge://api/perception_query`
- `forge://api/physics_scene`
- `forge://api/projectile_launch`
- `forge://api/quat`
- `forge://api/quat_normalize`
- `forge://api/quest`
- `forge://api/quest_journal`
- `forge://api/quest_simulate`
- `forge://api/quest_transition`
- `forge://api/quest_transitions`
- `forge://api/random_int`
- `forge://api/regex_match`
- `forge://api/release_effect`
- `forge://api/replay_test`
- `forge://api/resolve_asset`
- `forge://api/route_diagnostics`
- `forge://api/runtime_shell`
- `forge://api/save_policy`
- `forge://api/say`
- `forge://api/set_velocity`
- `forge://api/simulation_tick`
- `forge://api/simulation_time_ms`
- `forge://api/spawn`
- `forge://api/spawn_at`
- `forge://api/target_preview`
- `forge://api/teleport`
- `forge://api/timer_cancel`
- `forge://api/timer_schedule`
- `forge://api/unicode_length`
- `forge://api/unlock`
- `forge://api/variable`
- `forge://api/vec3`
- `forge://api/vec_dot`
- `forge://api/vec_length`
- `forge://api/vec_normalize`
- `forge://api/waypoint`
- `forge://api/waypoint_arrive`
- `forge://api/waypoint_reveal`
- `forge://api/world_calendar_ms`
- `forge://api/world_clock`
- `forge://api/world_time`

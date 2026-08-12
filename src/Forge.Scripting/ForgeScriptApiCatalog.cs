namespace Forge.Scripting;

public static class ForgeScriptApiCatalog
{
    private static IReadOnlyList<ApiRegistration> ScriptP1GameplayRegistrations { get; } =
    [
        Api("action_enqueue", "ScriptTaskRef action_enqueue(EntityRef owner, const string &in action, int priority)", "Ajoute une action typée à la file déterministe de l'entité.", "ScriptTaskRef task = action_enqueue(hero, \"attack\", 100)", "server", "rpg-kit"),
        Api("action_cancel", "void action_cancel(ScriptTaskRef task)", "Annule une action par son handle opaque et publie son résultat.", "action_cancel(task)", "server", "rpg-kit"),
        Api("action_complete", "void action_complete(ScriptTaskRef task, const string &in value)", "Termine une action en conservant une valeur de complétion bornée.", "action_complete(task, \"hit\")", "server", "rpg-kit"),
        Api("combat_attack", "bool combat_attack(EntityRef attacker, EntityRef target, const string &in ability, float amount, int seed)", "Émet une attaque attribuée, seedée et autoritaire vers le service de combat.", "combat_attack(hero, target, \"slash\", 12.0f, 97)", "server", "rpg-kit"),
        Api("effect_apply", "EffectHandle effect_apply(EntityRef source, EntityRef target, const string &in effect_id, float magnitude, float duration_seconds)", "Applique un effet typé borné et retourne un handle générationnel.", "EffectHandle burning = effect_apply(hero, target, \"burning\", 2.0f, 10.0f)", "server", "rpg-kit"),
        Api("effect_dispel", "void effect_dispel(EntityRef target, const string &in tag)", "Dissipe les effets autorisés partageant le tag demandé.", "effect_dispel(target, \"fire\")", "server", "rpg-kit"),
    ];

    private static IReadOnlyList<ApiRegistration> ScriptP1WorldRegistrations { get; } =
    [
        Api("inventory_transfer", "InventoryResultRef inventory_transfer(EntityRef from, EntityRef to, const string &in item_id, int quantity)", "Transfère un item par transaction autoritaire avec capacité, rollback et audit.", "InventoryResultRef loot = inventory_transfer(chest, hero, \"crypt-key\", 1)", "server", "rpg-kit"),
        Api("quest_transition", "QuestResultRef quest_transition(const string &in quest_id, const string &in scope_id, const string &in from_status, const string &in to_status, const string &in objective_id, int delta, const string &in cause)", "Applique une transition de quête scoped avec objectif, historique et cause.", "QuestResultRef step = quest_transition(\"crypt\", \"party:1\", \"active\", \"completed\", \"find-key\", 1, \"found\")", "server", "rpg-kit"),
        Api("dialogue_start", "DialogueSessionRef dialogue_start(const string &in graph_id, EntityRef speaker, EntityRef listener)", "Démarre un graphe de dialogue avec participants explicites.", "DialogueSessionRef talk = dialogue_start(\"intro\", warden, hero)", "server", "rpg-kit"),
        Api("dialogue_complete", "void dialogue_complete(DialogueSessionRef session, const string &in outcome)", "Termine une session de dialogue avec un résultat borné.", "dialogue_complete(talk, \"accepted\")", "server", "rpg-kit"),
        Api("faction_reputation", "FactionResultRef faction_reputation(EntityRef actor, const string &in faction_id, int delta, bool join, const string &in scope, const string &in cause)", "Modifie adhésion et réputation avec scope, cause et bornes autoritaires.", "FactionResultRef standing = faction_reputation(hero, \"wardens\", 25, true, \"module\", \"rescued-member\")", "server", "rpg-kit"),
        Api("perception_query", "PerceptionResultRef perception_query(EntityRef observer, EntityRef target, const string &in sense, float distance, float range, bool line_of_sight, float signal, int seed)", "Évalue une perception déterministe avec sens, portée, LOS, stimulus et trace.", "PerceptionResultRef seen = perception_query(guard, hero, \"sight\", 2.0f, 10.0f, true, 0.8f, 42)", "server", "rpg-kit"),
    ];

    private static IReadOnlyList<ApiRegistration> ScriptP1RuntimeToolRegistrations { get; } =
    [
        Api("audio_snapshot", "void audio_snapshot(const string &in snapshot)", "Active un snapshot de mixage validé sans exposer le moteur audio.", "audio_snapshot(\"combat\")", "server"),
        Api("audio_listener", "void audio_listener(EntityRef listener, const string &in policy)", "Applique une politique d'écoute camera, entity, player ou every-listener.", "audio_listener(hero, \"player\")", "server"),
        Api("audio_complete", "void audio_complete(AudioEmitterRef emitter, const string &in outcome)", "Clôt une lecture et publie son résultat de complétion.", "audio_complete(music, \"finished\")", "server"),
        Api("world_clock", "WorldClockRef world_clock()", "Obtient l'horloge monde opaque liée au temps de simulation.", "WorldClockRef clock = world_clock()", "server", "client"),
        Api("world_calendar_ms", "int64 world_calendar_ms(WorldClockRef clock)", "Lit le calendrier déterministe dérivé de l'horloge monde.", "int64 calendar = world_calendar_ms(clock)", "server", "client"),
        Api("timer_schedule", "TimerRef timer_schedule(int64 delay_ms, int64 repeat_ms, const string &in handler, const string &in schedule)", "Planifie un timer ou schedule déterministe sous quota.", "TimerRef timer = timer_schedule(1000, 5000, \"OnTick\", \"daily\")", "server"),
        Api("timer_cancel", "void timer_cancel(TimerRef timer)", "Annule un timer par son handle générationnel.", "timer_cancel(timer)", "server"),
        Api("deterministic_delay", "void deterministic_delay(int64 delay_ms, const string &in handler)", "Planifie un délai dans le temps de simulation, jamais sur l'horloge système.", "deterministic_delay(250, \"OnReady\")", "server"),
        Api("event_subscribe", "void event_subscribe(const string &in name, int version, const string &in handler)", "Abonne un handler à un événement namespacé et versionné.", "event_subscribe(\"forge.damage\", 1, \"OnDamage\")", "server"),
        Api("custom_event", "CustomEventRef custom_event(const string &in name, int version, const string &in payload)", "Construit un événement custom borné après validation du schéma de payload.", "CustomEventRef evt = custom_event(\"forge.damage\", 1, \"amount=4\")", "server"),
        Api("event_publish", "void event_publish(CustomEventRef event_ref)", "Publie un événement custom autoritaire avec corrélation et trace.", "event_publish(evt)", "server"),
        Api("hot_reload_request", "HotReloadRef hot_reload_request(const string &in module_id, int version, const string &in source_hash)", "Prépare un reload sûr à la frontière d'un handler avec migration et invalidation.", "HotReloadRef reload = hot_reload_request(\"module\", 2, \"sha256:new\")", "server"),
        Api("debug_break", "DebugSessionRef debug_break(const string &in session_id, const string &in authority)", "Ouvre une session de débogage locale ou serveur permissionnée.", "DebugSessionRef debug = debug_break(\"session:1\", \"local-author\")", "server", "client"),
        Api("debug_step", "void debug_step(DebugSessionRef session, const string &in mode)", "Exécute into, over, out ou continue sur une session suspendue.", "debug_step(debug, \"over\")", "server", "client"),
    ];

    private static IReadOnlyList<ApiRegistration> AudioControlRegistrations { get; } =
    [
        Api("audio_start", "void audio_start(AudioEmitterRef emitter)", "Démarre une instance selon la politique configurée.", "audio_start(emitter)", "server"),
        Api("audio_stop", "void audio_stop(AudioEmitterRef emitter)", "Arrête toutes les instances autorisées.", "audio_stop(emitter)", "server"),
        Api("audio_pause", "void audio_pause(AudioEmitterRef emitter)", "Suspend une lecture sans perdre son handle typé.", "audio_pause(emitter)", "server"),
        Api("audio_resume", "void audio_resume(AudioEmitterRef emitter)", "Reprend une lecture suspendue.", "audio_resume(emitter)", "server"),
        Api("audio_fade", "void audio_fade(AudioEmitterRef emitter, float gain, float seconds)", "Applique un fondu borné et publie son événement.", "audio_fade(emitter, 0.0f, 1.5f)", "server"),
        Api("audio_caption", "AudioCaptionRef audio_caption(AudioEmitterRef emitter, const string &in locale)", "Lit une caption localisée sans exposer le HUD ou le nœud audio.", "audio_caption(emitter, \"fr\")", "server", "client"),
        Api("waypoint", "WaypointRef waypoint(AreaRef area, const string &in id)", "Obtient un handle de waypoint typé et générationnel.", "WaypointRef marker = waypoint(area, \"a064...\")", "server", "client"),
        Api("waypoint_reveal", "bool waypoint_reveal(WaypointRef waypoint, const string &in scope, const string &in event_name)", "Révèle un marqueur par la commande runtime autoritaire.", "waypoint_reveal(marker, \"party\", \"quest.crypt.found\")", "server"),
        Api("patrol_next", "PatrolStepRef patrol_next(PatrolRouteRef route, int deterministic_roll)", "Avance une patrouille déterministe selon sa politique canonique.", "PatrolStepRef step = patrol_next(route, 42)", "server"),
        Api("waypoint_arrive", "ArrivalRef waypoint_arrive(WaypointRef waypoint)", "Exécute orientation, animation, attente, interaction et callback sous autorité.", "ArrivalRef arrival = waypoint_arrive(marker)", "server"),
        Api("locator", "LocatorRef locator(AreaRef area, const string &in id)", "Obtient une ancre audio, VFX, spawn, objectif ou gameplay typée.", "LocatorRef fire = locator(area, \"a065...\")", "server", "client"),
        Api("locator_collection", "LocatorCollectionRef locator_collection(AreaRef area, const string &in id)", "Résout une collection par requête avec ordre stable ou sélection déterministe.", "LocatorCollectionRef anchors = locator_collection(area, \"a065...\")", "server", "client"),
        Api("route_diagnostics", "RouteDiagnosticsRef route_diagnostics(PatrolRouteRef route)", "Lit segments, sens, coûts, actions, liens cassés, projection navigation et timing.", "RouteDiagnosticsRef debug = route_diagnostics(route)", "server", "client"),
    ];

    private static IReadOnlyList<ApiRegistration> GameplaySimulationRegistrations { get; } =
    [
        Api("flight_simulation", "FlightSimulationRef flight_simulation()", "Obtient le profil de vol 3D type sans exposer le noeud moteur.", "FlightSimulationRef flight = flight_simulation()", "server", "client"),
        Api("projectile_launch", "ProjectileRef projectile_launch(ProjectileSimulationRef profile, const Vec3 &in origin, const Vec3 &in target)", "Lance un projectile valide sous autorite et revision serveur.", "ProjectileRef bolt = projectile_launch(projectile_simulation(), origin, target)", "server"),
        Api("physics_scene", "PhysicsSimulationRef physics_scene()", "Obtient la scene physique canonique et ses corps et liaisons types.", "PhysicsSimulationRef physics = physics_scene()", "server", "client"),
        Api("target_preview", "TargetPreviewRef target_preview(TargetingSimulationRef profile, const Vec3 &in origin, const Vec3 &in direction)", "Previsualise une forme de ciblage et sa validite sans mutation.", "TargetPreviewRef preview = target_preview(targeting_simulation(), origin, direction)", "server", "client"),
        Api("world_time", "WorldTimeSimulationRef world_time()", "Obtient l'horloge, le calendrier et les planifications par handle type.", "WorldTimeSimulationRef clock = world_time()", "server", "client"),
    ];

    private static IReadOnlyList<ApiRegistration> GameplaySessionRegistrations { get; } =
    [
        Api("gameplay_session", "GameplaySessionRef gameplay_session()", "Obtient la session gameplay canonique par handle generationnel.", "GameplaySessionRef session = gameplay_session()", "server", "client"),
        Api("save_policy", "PersistencePolicyRef save_policy(GameplaySessionRef session)", "Lit portee, autorite, conflits, cloud et confidentialite sans acces fichier.", "PersistencePolicyRef saves = save_policy(session)", "server", "client"),
        Api("checkpoint_respawn", "RespawnResult checkpoint_respawn(CheckpointRef checkpoint, EntityRef actor)", "Demande un respawn valide avec cout, groupe, cadavre et spawn sur.", "RespawnResult result = checkpoint_respawn(checkpoint, hero)", "server"),
        Api("runtime_shell", "RuntimeShellRef runtime_shell(GameplaySessionRef session)", "Lit le shell carte, journal et HUD fourni par le kit.", "RuntimeShellRef shell = runtime_shell(session)", "server", "client"),
        Api("control_context", "ControlLockRef control_context(GameplaySessionRef session)", "Lit la pile de controles avec proprietaire et restauration garantie.", "ControlLockRef controls = control_context(session)", "server", "client"),
        Api("replay_test", "ReplayReportRef replay_test(ReplayTestRef replay)", "Execute le replay headless autoritaire et produit un rapport borne.", "ReplayReportRef report = replay_test(replay)", "server"),
    ];

    private static IReadOnlyList<ApiRegistration> DialogueAuthoringRegistrations { get; } =
    [
        Api("dialogue_authoring", "DialogueAuthoringRef dialogue_authoring(DialogueGraphRef graph)", "Lit tokens, participants, modes, sources du wizard et notes sans exposer le document.", "DialogueAuthoringRef authoring = dialogue_authoring(graph)", "server", "client", "rpg-kit"),
        Api("dialogue_experience", "DialogueExperienceRef dialogue_experience(DialogueGraphRef graph)", "Lit cycle fin/abandon, recherche, revue, mémoire et checks de compétence par handle typé.", "DialogueExperienceRef experience = dialogue_experience(graph)", "server", "client", "rpg-kit"),
        Api("dialogue_tokens", "array<DialogueTokenRef> dialogue_tokens(DialogueNodeRef node)", "Lit les tokens types du noeud par handles generationnels.", "array<DialogueTokenRef> tokens = dialogue_tokens(node)", "server", "client", "rpg-kit"),
        Api("dialogue_participant", "DialogueParticipantRef dialogue_participant(DialogueNodeRef node)", "Lit le participant resolu par role, query, tag ou entite et son fallback.", "DialogueParticipantRef speaker = dialogue_participant(node)", "server", "client", "rpg-kit"),
        Api("dialogue_evaluation", "DialogueEvaluationRef dialogue_evaluation(DialogueNodeRef node)", "Lit le mode d'evaluation explicite et les poids deterministes.", "DialogueEvaluationRef evaluation = dialogue_evaluation(node)", "server", "client", "rpg-kit"),
        Api("dialogue_notes", "DialogueNotesRef dialogue_notes(DialogueNodeRef node)", "Lit intentions, emotion, source, statut, TODO et auteur sans les afficher au joueur.", "DialogueNotesRef notes = dialogue_notes(node)", "server", "client", "rpg-kit"),
    ];

    public static IReadOnlyList<ApiRegistration> Registrations { get; } =
    [
        .. ScriptP1GameplayRegistrations,
        .. ScriptP1WorldRegistrations,
        .. ScriptP1RuntimeToolRegistrations,
        .. AudioControlRegistrations,
        .. GameplaySimulationRegistrations,
        .. GameplaySessionRegistrations,
        .. DialogueAuthoringRegistrations,
        Api("health", "int health(EntityRef target)", "Lit les points de vie courants.", "health(hero)", "server", "client"),
        Api("variable", "void variable(const string &in key, const string &in value)", "Modifie une variable autorisée.", "variable(\"door\", \"open\")", "server"),
        Api("say", "void say(const string &in text)", "Publie une réplique localisable.", "say(\"Bienvenue\")", "server", "client"),
        Api("cast_ability", "void cast_ability(EntityRef target, const string &in ability)", "Demande une capacité au service de combat.", "cast_ability(target, \"ash-nova\")", "server", "rpg-kit"),
        Api("spawn", "EntityRef spawn(const string &in blueprint)", "Crée une entité sous quota et autorité serveur.", "spawn(\"ash-guardian\")", "server"),
        Api("quest", "void quest(const string &in transition)", "Demande une transition de quête validée.", "quest(\"crypt.completed\")", "server", "rpg-kit"),
        Api("quest_journal", "QuestJournalRef quest_journal(const string &in id)", "Obtient un handle de journal de quête typé et versionné.", "quest_journal(\"a808...\")", "server", "client", "rpg-kit"),
        Api("quest_transitions", "array<QuestTransitionRef> quest_transitions(QuestJournalRef journal)", "Lit les transitions ordonnées sans exposer le document ou le moteur.", "quest_transitions(journal)", "server", "client", "rpg-kit"),
        Api("quest_simulate", "QuestSimulationResult quest_simulate(QuestJournalRef journal, const string &in scenario)", "Exécute un scénario borné en sandbox sans mutation du projet.", "quest_simulate(journal, \"author-preview\")", "server", "rpg-kit"),
        Api("unlock", "void unlock(EntityRef target)", "Déverrouille une cible autorisée.", "unlock(door)", "server"),
        Api("audio_emitter", "AudioEmitterRef audio_emitter(const string &in id)", "Obtient un handle audio typé sans exposer le nœud moteur.", "audio_emitter(\"a004...\")", "server", "client"),
        Api("audio_is_active", "bool audio_is_active(AudioEmitterRef emitter)", "Lit l'état d'un émetteur audio.", "audio_is_active(emitter)", "server", "client"),
        Api("audio_set_active", "void audio_set_active(AudioEmitterRef emitter, bool enabled)", "Demande une activation audio validée par le runtime.", "audio_set_active(emitter, true)", "server"),
        Api("dialogue_graph", "DialogueGraphRef dialogue_graph(const string &in id)", "Obtient un handle de graphe sans exposer le stockage ou le moteur.", "dialogue_graph(\"4000...\")", "server", "client", "rpg-kit"),
        Api("dialogue_entry", "DialogueNodeRef dialogue_entry(DialogueGraphRef graph)", "Lit le nœud d'entrée canonique du graphe.", "dialogue_entry(graph)", "server", "client", "rpg-kit"),
        Api("dialogue_choices", "array<DialogueChoiceRef> dialogue_choices(DialogueNodeRef node)", "Lit les choix dans l'ordre priorité puis liste.", "dialogue_choices(node)", "server", "client", "rpg-kit"),
        Api("current_area", "AreaRef current_area()", "Obtient le handle opaque de la zone d'exécution.", "AreaRef area = current_area()", "server", "client"),
        Api("find_entity", "EntityRef find_entity(AreaRef area, const string &in tag)", "Recherche une entité par tag dans la portée de zone bornée.", "EntityRef door = find_entity(area, \"door\")", "server", "client"),
        Api("entity_position", "Vec3 entity_position(EntityRef entity)", "Lit une copie de la position canonique sans exposer le moteur.", "Vec3 p = entity_position(hero)", "server", "client"),
        Api("spawn_at", "EntityRef spawn_at(AreaRef area, const string &in blueprint, const Vec3 &in position)", "Émet une commande de spawn serveur sous quota.", "spawn_at(area, \"guard\", vec3(2, 0, 4))", "server"),
        Api("destroy_entity", "void destroy_entity(EntityRef entity)", "Émet une commande de destruction validée par l'autorité.", "destroy_entity(target)", "server"),
        Api("move_to", "MovementTaskRef move_to(EntityRef entity, const Vec3 &in destination, const string &in mode)", "Crée une tâche de navigation avec identifiant stable et événement de fin.", "MovementTaskRef task = move_to(hero, goal, \"run\")", "server"),
        Api("face_towards", "MovementTaskRef face_towards(EntityRef entity, const Vec3 &in target)", "Crée une tâche d'orientation.", "face_towards(hero, target)", "server"),
        Api("follow", "MovementTaskRef follow(EntityRef entity, EntityRef target, float distance)", "Crée une tâche de suivi à distance finie.", "follow(companion, hero, 2.0f)", "server"),
        Api("teleport", "void teleport(EntityRef entity, AreaRef area, const Vec3 &in destination)", "Émet une téléportation canonique avec zone et destination.", "teleport(hero, area, goal)", "server"),
        Api("set_velocity", "void set_velocity(EntityRef entity, const Vec3 &in velocity)", "Définit une vélocité validée par le runtime.", "set_velocity(hero, vec3(0, 4, 0))", "server"),
        Api("cancel_movement", "void cancel_movement(MovementTaskRef task)", "Annule une tâche et publie son achèvement.", "cancel_movement(task)", "server"),
        Api("vec3", "Vec3 vec3(float x, float y, float z)", "Construit un vecteur fini.", "Vec3 goal = vec3(1, 0, 3)", "server", "client"),
        Api("vec_length", "float vec_length(const Vec3 &in value)", "Calcule une longueur déterministe.", "float distance = vec_length(goal)", "server", "client"),
        Api("vec_dot", "float vec_dot(const Vec3 &in left, const Vec3 &in right)", "Calcule un produit scalaire déterministe.", "float alignment = vec_dot(a, b)", "server", "client"),
        Api("vec_normalize", "Vec3 vec_normalize(const Vec3 &in value)", "Normalise un vecteur fini, zéro restant zéro.", "Vec3 direction = vec_normalize(delta)", "server", "client"),
        Api("quat", "Quat quat(float x, float y, float z, float w)", "Construit un quaternion fini.", "Quat rotation = quat(0, 0, 0, 1)", "server", "client"),
        Api("quat_normalize", "Quat quat_normalize(const Quat &in value)", "Normalise un quaternion de façon déterministe.", "rotation = quat_normalize(rotation)", "server", "client"),
        Api("format_int", "string format_int(int64 value)", "Formate un entier selon la culture invariante.", "say(format_int(score))", "server", "client"),
        Api("format_float", "string format_float(double value, uint precision)", "Formate un nombre fini avec une précision bornée.", "say(format_float(distance, 2))", "server", "client"),
        Api("unicode_length", "uint unicode_length(const string &in value)", "Compte les points de code UTF-8 sous quota.", "uint letters = unicode_length(name)", "server", "client"),
        Api("regex_match", "bool regex_match(const string &in value, const string &in pattern)", "Évalue une expression régulière ECMAScript bornée.", "bool valid = regex_match(tag, \"^[a-z-]+$\")", "server", "client"),
        Api("resolve_asset", "AssetId resolve_asset(const string &in assetId)", "Résout un identifiant d'asset en handle opaque typé et générationnel.", "AssetId icon = resolve_asset(\"ui/quest-marker\")", "server", "client"),
        Api("create_effect", "EffectHandle create_effect(const string &in effectId)", "Crée un handle d'effet opaque sous autorité serveur.", "EffectHandle ash = create_effect(\"ash-burn\")", "server"),
        Api("release_effect", "void release_effect(EffectHandle effect)", "Libère un handle d'effet et invalide sa génération.", "release_effect(ash)", "server"),
        Api("random_int", "int random_int(int minimum, int maximum)", "Produit un entier déterministe depuis la graine de simulation.", "int roll = random_int(1, 20)", "server", "client"),
        Api("simulation_tick", "int64 simulation_tick()", "Lit le tick de simulation autoritaire injecté par l'hôte.", "int64 tick = simulation_tick()", "server", "client"),
        Api("simulation_time_ms", "int64 simulation_time_ms()", "Lit le temps de simulation en millisecondes sans horloge système.", "int64 now = simulation_time_ms()", "server", "client"),
        Api("local_set_int", "void local_set_int(EntityRef owner, const string &in key, int64 value)", "Écrit une variable entière locale typée.", "local_set_int(hero, \"quest.stage\", 2)", "server"),
        Api("local_get_int", "int64 local_get_int(EntityRef owner, const string &in key, int64 fallback)", "Lit une variable entière locale typée.", "int64 stage = local_get_int(hero, \"quest.stage\", 0)", "server", "client"),
        Api("local_set_float", "void local_set_float(EntityRef owner, const string &in key, double value)", "Écrit une variable numérique locale typée.", "local_set_float(hero, \"speed\", 3.5)", "server"),
        Api("local_get_float", "double local_get_float(EntityRef owner, const string &in key, double fallback)", "Lit une variable numérique locale typée.", "double speed = local_get_float(hero, \"speed\", 0)", "server", "client"),
        Api("local_set_string", "void local_set_string(EntityRef owner, const string &in key, const string &in value)", "Écrit une chaîne Unicode locale sous quota.", "local_set_string(hero, \"title\", \"Gardienne\")", "server"),
        Api("local_get_string", "string local_get_string(EntityRef owner, const string &in key, const string &in fallback)", "Lit une chaîne locale typée.", "string title = local_get_string(hero, \"title\", \"\")", "server", "client"),
        Api("local_set_entity", "void local_set_entity(EntityRef owner, const string &in key, EntityRef value)", "Écrit un handle d'entité local sans pointeur natif.", "local_set_entity(hero, \"target\", enemy)", "server"),
        Api("local_get_entity", "EntityRef local_get_entity(EntityRef owner, const string &in key, EntityRef fallback)", "Lit un handle d'entité local opaque.", "EntityRef target = local_get_entity(hero, \"target\", 0)", "server", "client"),
        Api("local_set_vector", "void local_set_vector(EntityRef owner, const string &in key, const Vec3 &in value)", "Écrit une position ou direction locale typée.", "local_set_vector(hero, \"home\", goal)", "server"),
        Api("local_get_vector", "Vec3 local_get_vector(EntityRef owner, const string &in key, const Vec3 &in fallback)", "Lit un vecteur local typé.", "Vec3 home = local_get_vector(hero, \"home\", vec3(0, 0, 0))", "server", "client"),
    ];

    public static IReadOnlyList<string> Signatures { get; } = Registrations.Select(item => item.Signature).ToArray();

    private static ApiRegistration Api(string name, string signature, string summary, string example,
        params string[] availability) => new(name, signature, summary, "1.0",
            availability.ToHashSet(StringComparer.Ordinal), example, $"forge://api/{name}");
}

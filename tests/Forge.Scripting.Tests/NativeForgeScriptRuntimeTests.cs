namespace Forge.Scripting.Tests;

public sealed class NativeForgeScriptRuntimeTests
{
    private readonly NativeForgeScriptRuntime _runtime = new();

    [Fact]
    public void NativeHost_ReportsPinnedVmAndApiVersions()
    {
        Assert.Equal("2.38.0", _runtime.VirtualMachineVersion);
        Assert.Equal("1.0.0", _runtime.ApiVersion);
    }

    [Fact]
    public void CompileAndExecute_LoadsBytecodeAndEmitsMinimalApiEvents()
    {
        const string source = """
            void main()
            {
                EntityRef hero = spawn("hero");
                int current = health(hero);
                variable("warden.phase", "two");
                say("La braise se lève.");
                cast_ability(hero, "ash-nova");
                EntityRef spirit = spawn("ash-spirit");
                quest("crypt-of-ashes");
                unlock(spirit);
            }
            """;

        var compilation = _runtime.Compile(source);
        var artifact = Assert.IsType<ForgeScriptArtifact>(compilation.Artifact);
        var result = _runtime.Execute(artifact);

        Assert.True(compilation.Success);
        Assert.NotEmpty(artifact.Bytecode);
        Assert.Equal(ForgeScriptExecutionStatus.Completed, result.Status);
        Assert.Equal(
            ["spawn", "health", "variable", "say", "cast", "spawn", "quest", "unlock"],
            result.Events.Select(scriptEvent => scriptEvent.Kind));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Compile_ReturnsStructuredDiagnosticsForInvalidSource()
    {
        var result = _runtime.Compile("void main() { missing_api(); }");

        Assert.False(result.Success);
        Assert.Null(result.Artifact);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == "error" && diagnostic.Message.Contains("missing_api", StringComparison.Ordinal));
    }

    [Fact]
    public void TypedEventEntryCompilesAndMainRunsWithoutWarnings()
    {
        const string source = "void OnDamaged(const int &in e) { int observed = e; } " +
                              "void main() { say(\"ready\"); }";

        var compilation = _runtime.Compile(source);
        var execution = _runtime.Execute(Assert.IsType<ForgeScriptArtifact>(compilation.Artifact));

        Assert.True(compilation.Success);
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(ForgeScriptExecutionStatus.Completed, execution.Status);
        Assert.Contains(execution.Events, item => item.Kind == "say" && item.Value == "ready");
    }

    [Fact]
    public void Execute_StopsAtInstructionQuotaAndHostContinues()
    {
        var endless = Compile("void main() { while (true) { say(\"loop\"); } }");

        var failed = _runtime.Execute(
            endless,
            new ForgeScriptQuotas(instructionLimit: 8, timeoutMilliseconds: 1000));
        var healthy = _runtime.Execute(Compile("void main() { say(\"healthy\"); }"));

        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, failed.Status);
        Assert.Contains(failed.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("instruction_limit", StringComparison.Ordinal));
        Assert.Equal(ForgeScriptExecutionStatus.Completed, healthy.Status);
        Assert.Equal("healthy", Assert.Single(healthy.Events).Value);
    }

    [Fact]
    public void Execute_EnforcesRecursionAndSpawnCaps()
    {
        var recursive = Compile("void dive() { dive(); } void main() { dive(); }");
        var spawning = Compile("void main() { spawn(\"a\"); spawn(\"b\"); }");

        var recursionResult = _runtime.Execute(
            recursive,
            new ForgeScriptQuotas(recursionLimit: 4));
        var spawnResult = _runtime.Execute(
            spawning,
            new ForgeScriptQuotas(spawnCap: 1));

        Assert.NotEqual(ForgeScriptExecutionStatus.Completed, recursionResult.Status);
        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, spawnResult.Status);
        Assert.Contains(spawnResult.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("spawn_cap", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_EnforcesWallClockTimeout()
    {
        var endless = Compile("void main() { while (true) { int value = 1; } }");

        var result = _runtime.Execute(
            endless,
            new ForgeScriptQuotas(instructionLimit: int.MaxValue, timeoutMilliseconds: 1));

        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("timeout", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_RejectsIncompatibleVersionMetadata()
    {
        var artifact = Compile("void main() {}");
        var incompatible = artifact with { ApiVersion = "99.0.0" };

        Assert.Throws<InvalidOperationException>(() => _runtime.Execute(incompatible));
    }

    [Fact]
    public void World_movement_utilities_and_local_variables_execute_through_the_allowlist()
    {
        const string source = """
            void main()
            {
                AreaRef area = current_area();
                EntityRef hero = find_entity(area, "hero");
                Vec3 destination = vec3(3, 4, 0);
                Vec3 direction = vec_normalize(destination);
                Quat rotation = quat_normalize(quat(0, 0, 0, 1));
                float distance = vec_length(destination);
                float alignment = vec_dot(direction, direction);
                local_set_int(hero, "quest.stage", 2);
                local_set_float(hero, "movement.speed", distance);
                local_set_string(hero, "title", "Warden");
                local_set_entity(hero, "target", hero);
                local_set_vector(hero, "home", destination);
                int64 stage = local_get_int(hero, "quest.stage", 0);
                double speed = local_get_float(hero, "movement.speed", 0);
                string title = local_get_string(hero, "title", "missing");
                EntityRef target = local_get_entity(hero, "target", 0);
                Vec3 home = local_get_vector(hero, "home", vec3(0, 0, 0));
                MovementTaskRef move = move_to(hero, home, "run");
                MovementTaskRef face = face_towards(hero, destination);
                MovementTaskRef escort = follow(hero, target, 2.0f);
                set_velocity(hero, vec3(0, 1, 0));
                teleport(hero, area, destination);
                EntityRef guard = spawn_at(area, "ash-guard", destination);
                Vec3 observed = entity_position(guard);
                destroy_entity(guard);
                cancel_movement(move);
                bool valid = regex_match("ash-guard", "^[a-z-]+$");
                uint letters = unicode_length(title);
                say(format_int(stage));
                say(format_float(speed + alignment + rotation.w + observed.y, 2));
            }
            """;

        var compilation = _runtime.Compile(source);
        var result = _runtime.Execute(Assert.IsType<ForgeScriptArtifact>(compilation.Artifact));

        Assert.True(compilation.Success, string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
        Assert.Equal(ForgeScriptExecutionStatus.Completed, result.Status);
        Assert.Contains(result.Events, item => item.Kind == "world.area");
        Assert.Contains(result.Events, item => item.Kind == "world.query");
        Assert.Contains(result.Events, item => item.Kind == "world.spawn");
        Assert.Contains(result.Events, item => item.Kind == "world.destroy");
        Assert.Equal(3, result.Events.Count(item => item.Kind == "movement.started"));
        Assert.Contains(result.Events, item => item.Kind == "movement.teleported");
        Assert.Contains(result.Events, item => item.Kind == "movement.velocity");
        Assert.Contains(result.Events, item => item.Kind == "movement.completed");
        Assert.Equal(5, result.Events.Count(item => item.Kind == "local.set"));
        Assert.Contains(result.Events, item => item.Kind == "say" && item.Value == "2");
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Deferred_action_combat_and_effect_apis_compile_and_emit_typed_intents()
    {
        const string source = """
            void main()
            {
                EntityRef hero = spawn("hero");
                EntityRef target = spawn("warden");
                ScriptTaskRef attackTask = action_enqueue(hero, "attack", 100);
                action_complete(attackTask, "hit");
                ScriptTaskRef moveTask = action_enqueue(hero, "move", 20);
                action_cancel(moveTask);
                bool hit = combat_attack(hero, target, "slash", 12.0f, 97);
                EffectHandle burning = effect_apply(hero, target, "burning", 2.0f, 10.0f);
                effect_dispel(target, "fire");
                release_effect(burning);
            }
            """;

        var compilation = _runtime.Compile(source);
        var result = _runtime.Execute(Assert.IsType<ForgeScriptArtifact>(compilation.Artifact));

        Assert.True(compilation.Success,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
        Assert.Equal(ForgeScriptExecutionStatus.Completed, result.Status);
        Assert.Equal(2, result.Events.Count(item => item.Kind == "action.queued"));
        Assert.Contains(result.Events, item => item.Kind == "action.completed");
        Assert.Contains(result.Events, item => item.Kind == "action.cancelled");
        Assert.Contains(result.Events, item => item.Kind == "combat.attack");
        Assert.Contains(result.Events, item => item.Kind == "effect.applied");
        Assert.Contains(result.Events, item => item.Kind == "effect.dispelled");
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Inventory_quest_dialogue_faction_and_perception_apis_emit_typed_world_intents()
    {
        const string source = """
            void main()
            {
                EntityRef chest = spawn("chest");
                EntityRef hero = spawn("hero");
                EntityRef warden = spawn("warden");
                InventoryResultRef loot = inventory_transfer(chest, hero, "key", 1);
                QuestResultRef quest = quest_transition("crypt", "party:1", "active", "completed", "find-key", 1, "found");
                DialogueSessionRef talk = dialogue_start("graph:intro", warden, hero);
                dialogue_complete(talk, "accepted");
                FactionResultRef faction = faction_reputation(hero, "wardens", 25, true, "module", "rescued");
                PerceptionResultRef seen = perception_query(warden, hero, "sight", 2.0f, 10.0f, true, 0.8f, 98);
            }
            """;

        var compilation = _runtime.Compile(source);
        var result = _runtime.Execute(Assert.IsType<ForgeScriptArtifact>(compilation.Artifact));

        Assert.True(compilation.Success,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
        Assert.Equal(ForgeScriptExecutionStatus.Completed, result.Status);
        Assert.Contains(result.Events, item => item.Kind == "inventory.transfer");
        Assert.Contains(result.Events, item => item.Kind == "quest.transition");
        Assert.Contains(result.Events, item => item.Kind == "dialogue.started");
        Assert.Contains(result.Events, item => item.Kind == "dialogue.completed");
        Assert.Contains(result.Events, item => item.Kind == "faction.reputation");
        Assert.Contains(result.Events, item => item.Kind == "perception.query");
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Audio_time_custom_event_hot_reload_and_debugger_apis_emit_typed_tool_intents()
    {
        const string source = """
            void main()
            {
                EntityRef hero = spawn("hero");
                AudioEmitterRef music = audio_emitter("music:dawn");
                audio_start(music);
                bool active = audio_is_active(music);
                audio_pause(music);
                audio_resume(music);
                audio_fade(music, 0.25f, 2.0f);
                AudioCaptionRef caption = audio_caption(music, "fr");
                audio_snapshot("combat");
                audio_listener(hero, "player");
                audio_complete(music, "finished");
                WorldClockRef clock = world_clock();
                int64 calendar = world_calendar_ms(clock);
                TimerRef timer = timer_schedule(1000, 5000, "OnTick", "daily");
                timer_cancel(timer);
                deterministic_delay(250, "OnReady");
                event_subscribe("forge.damage", 1, "OnDamage");
                CustomEventRef evt = custom_event("forge.damage", 1, "amount=4");
                event_publish(evt);
                HotReloadRef reload = hot_reload_request("module", 2, "sha256:new");
                DebugSessionRef debug = debug_break("session:1", "local-author");
                debug_step(debug, "over");
            }
            """;

        var compilation = _runtime.Compile(source);
        var result = _runtime.Execute(Assert.IsType<ForgeScriptArtifact>(compilation.Artifact));

        Assert.True(compilation.Success,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
        Assert.Equal(ForgeScriptExecutionStatus.Completed, result.Status);
        Assert.Contains(result.Events, item => item.Kind == "audio.started");
        Assert.Contains(result.Events, item => item.Kind == "audio.faded");
        Assert.Contains(result.Events, item => item.Kind == "audio.completed");
        Assert.Contains(result.Events, item => item.Kind == "time.clock");
        Assert.Contains(result.Events, item => item.Kind == "time.timer.scheduled");
        Assert.Contains(result.Events, item => item.Kind == "time.delay");
        Assert.Contains(result.Events, item => item.Kind == "event.published");
        Assert.Contains(result.Events, item => item.Kind == "hot-reload.requested");
        Assert.Contains(result.Events, item => item.Kind == "debug.break");
        Assert.Contains(result.Events, item => item.Kind == "debug.step");
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Bounded_regex_abuse_is_interrupted_and_does_not_poison_the_runtime()
    {
        var abusivePattern = new string('a', ForgeScriptCapabilityManifest.MaximumRegexPatternBytes + 1);
        var abusive = Compile($"void main() {{ regex_match(\"value\", \"{abusivePattern}\"); }}");

        var rejected = _runtime.Execute(abusive);
        var healthy = _runtime.Execute(Compile("void main() { say(\"healthy-after-regex\"); }"));

        Assert.Equal(ForgeScriptExecutionStatus.QuotaExceeded, rejected.Status);
        Assert.Contains(rejected.Diagnostics, item => item.Message.Contains("regex_bytes_exceeded", StringComparison.Ordinal));
        Assert.Equal(ForgeScriptExecutionStatus.Completed, healthy.Status);
        Assert.Equal("healthy-after-regex", Assert.Single(healthy.Events).Value);
    }

    private ForgeScriptArtifact Compile(string source)
    {
        var result = _runtime.Compile(source);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<ForgeScriptArtifact>(result.Artifact);
    }
}

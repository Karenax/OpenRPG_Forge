using Forge.Mcp.Scripts;
using Forge.Scripting;

namespace Forge.Mcp.Tests.Scripts;

public sealed class ForgeScriptAgentServiceTests
{
    private readonly ForgeScriptAgentService _agent = new(new NativeForgeScriptRuntime());

    [Fact]
    public void ApiAndCompilerExposeVersionedStructuredResults()
    {
        var api = _agent.GetApi();
        var result = _agent.Compile("void main() { say(\"hello\"); }");

        Assert.Equal("2.38.0", api.VirtualMachineVersion);
        Assert.Equal("1.0.0", api.ApiVersion);
        Assert.Equal("AngelScript", api.Language);
        Assert.Equal("2.38", api.DialectVersion);
        Assert.Equal(".as", api.CanonicalSourceExtension);
        Assert.Contains("source + apiVersion + compilerVersion", api.BytecodeCacheKeyContract, StringComparison.Ordinal);
        Assert.Contains("void Handler(const Payload &in e)", api.EntryContracts);
        Assert.Contains(api.Signatures, signature => signature.StartsWith("void say(const string &in", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("AudioEmitterRef audio_emitter", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("void audio_set_active(AudioEmitterRef", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("void audio_pause(AudioEmitterRef", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("void audio_fade(AudioEmitterRef", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("AudioCaptionRef audio_caption", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("WaypointRef waypoint", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("bool waypoint_reveal", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("PatrolStepRef patrol_next", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("ArrivalRef waypoint_arrive", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("LocatorRef locator", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("LocatorCollectionRef locator_collection", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("RouteDiagnosticsRef route_diagnostics", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("MovementTaskRef move_to", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("ScriptTaskRef action_enqueue", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("bool combat_attack", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("EffectHandle effect_apply", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("void effect_dispel", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("InventoryResultRef inventory_transfer", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("QuestResultRef quest_transition", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("DialogueSessionRef dialogue_start", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("FactionResultRef faction_reputation", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("PerceptionResultRef perception_query", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("void audio_snapshot", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("WorldClockRef world_clock", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("TimerRef timer_schedule", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("CustomEventRef custom_event", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("HotReloadRef hot_reload_request", StringComparison.Ordinal));
        Assert.Contains(api.Signatures, signature => signature.StartsWith("DebugSessionRef debug_break", StringComparison.Ordinal));
        Assert.Equal(6, api.Capabilities.Count);
        Assert.Contains("1.0.0", api.SupportedVersions);
        Assert.Contains(api.Deprecations, item => item.Symbol == "variable");
        Assert.Contains(api.Migrations, item => item.From == "1.0");
        Assert.Contains("filesystem", api.ForbiddenCapabilities);
        Assert.Contains("regex=256/4096", api.SandboxContract, StringComparison.Ordinal);
        Assert.Equal("scripts/forge-script-permissions.json", api.ModulePermissionPath);
        Assert.Equal(1, api.ModulePermissionSchemaVersion);
        Assert.Contains("deny-default", api.ModulePermissionContract, StringComparison.Ordinal);
        Assert.Equal(5, api.QualityTools.Count);
        Assert.Contains(api.QualityTools, item => item.StartsWith("profiler:", StringComparison.Ordinal));
        Assert.NotNull(api.GeneratedReference);
        Assert.Contains("Do not edit manually", api.GeneratedReference.Markdown);
        Assert.Contains("forge://api/say", api.GeneratedReference.McpResources);
        Assert.True(result.Succeeded);
        Assert.True(result.BytecodeSize > 0);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void CompilerDiagnosticsAndSandboxEventsRemainStructured()
    {
        var invalid = _agent.Compile("void main( { say(\"broken\"); }");
        var sandbox = _agent.TestInSandbox("void main() { say(\"hello\"); spawn(\"spirit\"); }");

        Assert.False(invalid.Succeeded);
        Assert.NotEmpty(invalid.Diagnostics);
        Assert.True(sandbox.Succeeded);
        Assert.Contains(sandbox.Events, item => item.Kind == "say" && item.Value == "hello");
        Assert.Contains(sandbox.Events, item => item.Kind == "spawn");
    }

    [Fact]
    public void Compiler_and_sandbox_reject_forbidden_capabilities_with_stable_paths()
    {
        const string source = "void main() { file_open(\"secret\"); }";

        var compilation = _agent.Compile(source);
        var sandbox = _agent.TestInSandbox(source);

        Assert.False(compilation.Succeeded);
        Assert.Equal("script.sandbox.capability_forbidden", Assert.Single(compilation.Diagnostics).Code);
        Assert.StartsWith("script/source:1:", compilation.Diagnostics[0].Path, StringComparison.Ordinal);
        Assert.False(sandbox.Succeeded);
        Assert.Equal("capability_forbidden", sandbox.Status);
    }

    [Fact]
    public void BatchCompilerAndConformanceAreAvailableWithoutShellAccess()
    {
        var first = _agent.CompileBatch(
        [
            new ForgeScriptCompilationUnit("shared.as", "void main() {}", []),
            new ForgeScriptCompilationUnit("main.as", "void main() {}", ["shared.as"]),
        ]);
        var second = _agent.CompileBatch(
        [
            new ForgeScriptCompilationUnit("shared.as", "void main() {}", []),
            new ForgeScriptCompilationUnit("main.as", "void main() {}", ["shared.as"]),
        ]);
        var conformance = _agent.RunCompilerConformance();

        Assert.True(first.Success);
        Assert.Equal(2, second.Reused);
        Assert.All(conformance, result => Assert.True(result.Passed));
    }

    [Fact]
    public void P1_gameplay_apis_are_visible_and_execute_only_inside_the_sandbox()
    {
        const string source = """
            void main()
            {
                EntityRef hero = spawn("hero");
                EntityRef target = spawn("target");
                ScriptTaskRef task = action_enqueue(hero, "attack", 50);
                bool hit = combat_attack(hero, target, "slash", 8.0f, 97);
                EffectHandle effect = effect_apply(hero, target, "burning", 2.0f, 10.0f);
                action_complete(task, "hit");
                effect_dispel(target, "fire");
                release_effect(effect);
            }
            """;

        var result = _agent.TestInSandbox(source);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Events, item => item.Kind == "action.queued");
        Assert.Contains(result.Events, item => item.Kind == "combat.attack");
        Assert.Contains(result.Events, item => item.Kind == "effect.applied");
    }

    [Fact]
    public void P1_world_apis_are_versioned_and_execute_without_external_access()
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

        var result = _agent.TestInSandbox(source);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Events, item => item.Kind == "inventory.transfer");
        Assert.Contains(result.Events, item => item.Kind == "quest.transition");
        Assert.Contains(result.Events, item => item.Kind == "dialogue.started");
        Assert.Contains(result.Events, item => item.Kind == "faction.reputation");
        Assert.Contains(result.Events, item => item.Kind == "perception.query");
    }

    [Fact]
    public void P1_runtime_tool_apis_compile_test_and_remain_inside_the_native_sandbox()
    {
        const string source = """
            void main()
            {
                EntityRef hero = spawn("hero");
                AudioEmitterRef music = audio_emitter("music:dawn");
                audio_start(music);
                audio_snapshot("combat");
                audio_listener(hero, "player");
                WorldClockRef clock = world_clock();
                TimerRef timer = timer_schedule(1000, 5000, "OnTick", "daily");
                event_subscribe("forge.damage", 1, "OnDamage");
                CustomEventRef evt = custom_event("forge.damage", 1, "amount=4");
                event_publish(evt);
                HotReloadRef reload = hot_reload_request("module", 2, "sha256:new");
                DebugSessionRef debug = debug_break("session:1", "local-author");
                debug_step(debug, "over");
            }
            """;

        var compilation = _agent.Compile(source);
        var result = _agent.TestInSandbox(source);

        Assert.True(compilation.Succeeded);
        Assert.Empty(compilation.Diagnostics);
        Assert.True(result.Succeeded);
        Assert.Contains(result.Events, item => item.Kind == "audio.started");
        Assert.Contains(result.Events, item => item.Kind == "time.timer.scheduled");
        Assert.Contains(result.Events, item => item.Kind == "event.published");
        Assert.Contains(result.Events, item => item.Kind == "hot-reload.requested");
        Assert.Contains(result.Events, item => item.Kind == "debug.step");
    }
}

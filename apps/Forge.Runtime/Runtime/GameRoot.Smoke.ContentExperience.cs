using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Areas;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Build;
using Forge.Application.Content;
using Forge.Application.Creatures;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Localization;
using Forge.Application.Modules;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Application.Rules;
using Forge.Application.Quality;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.RuntimeExtensions;
using Forge.Application.Scripting;
using Forge.Application.Entities;
using Forge.Application.Combat;
using Forge.Application.Factions;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Audio;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Creatures;
using Forge.Domain.Content;
using Forge.Application.Dialogues;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Lifecycle;
using Forge.Domain.Localization;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Quality;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Spatial;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Production;
using Forge.Network.Observability;
using Forge.Network.Testing;
using Forge.Network.Authoring;
using Forge.Network.Scenarios;
using Forge.Network.Sessions;
using Forge.Network.World;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Scenarios;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using Forge.RpgKit.Domain.Merchants;
using RpgAttackContext = Forge.RpgKit.Domain.Scripting.AttackContext;
using RpgDamageSpec = Forge.RpgKit.Domain.Scripting.DamageSpec;
using RpgEffectDefinition = Forge.RpgKit.Domain.Scripting.EffectDefinition;
using RpgEffectSpec = Forge.RpgKit.Domain.Scripting.EffectSpec;
using RpgEffectStacking = Forge.RpgKit.Domain.Scripting.EffectStacking;
using RpgScriptCombatState = Forge.RpgKit.Domain.Scripting.ScriptCombatState;
using RpgScriptGameplayService = Forge.RpgKit.Domain.Scripting.ScriptGameplayService;
using RpgDialogueRequest = Forge.RpgKit.Domain.Scripting.DialogueRequest;
using RpgFactionState = Forge.RpgKit.Domain.Scripting.FactionState;
using RpgFactionTransaction = Forge.RpgKit.Domain.Scripting.FactionTransaction;
using RpgInventoryState = Forge.RpgKit.Domain.Scripting.InventoryState;
using RpgInventoryTransaction = Forge.RpgKit.Domain.Scripting.InventoryTransaction;
using RpgPerceptionQuery = Forge.RpgKit.Domain.Scripting.PerceptionQuery;
using RpgQuestState = Forge.RpgKit.Domain.Scripting.QuestState;
using RpgQuestTransition = Forge.RpgKit.Domain.Scripting.QuestTransition;
using RpgScriptWorldService = Forge.RpgKit.Domain.Scripting.ScriptWorldService;
using ScriptAudioCue = Forge.Scripting.AudioCue;
using ScriptAudioState = Forge.Scripting.AudioState;
using ScriptCustomEvent = Forge.Scripting.CustomEvent;
using ScriptCustomEventSchema = Forge.Scripting.CustomEventSchema;
using ScriptDebugFrame = Forge.Scripting.DebugFrame;
using ScriptHotReloadModule = Forge.Scripting.HotReloadModule;
using ScriptRuntimeTools = Forge.Scripting.ForgeScriptRuntimeTools;
using ScriptWorldClock = Forge.Scripting.WorldClock;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private void RunGenericHeadlessSmokeTest()
    {
        AssertSmoke(!_moduleExperience.SystemicGameplayEnabled && _moduleExperience.ArtKitId is null,
            "The generic smoke test cannot use a module-specific experience profile.");
        AssertSmoke(!string.IsNullOrWhiteSpace(_moduleSlug), "The generic module slug was not loaded.");
        AssertSmoke(_builtEntityCount >= 2, "Expected at least the generated floor and player spawn.");
        AssertSmoke(_collisionCount >= 1, "Expected at least one generated floor collision.");
        AssertSmoke(_player is not null, "The generic player spawn was not reconstructed.");
        AssertSmoke(_playerCombatant is null, "A Core starter module must not require an RPG combat profile.");
        AssertSmoke(_npc is null && _keyItem is null && _door is null && _boss is null && _exit is null,
            "The generic starter unexpectedly reconstructed Crypt-specific gameplay actors.");
        AssertSmoke(FindChild("AshRune", recursive: true, owned: false) is null,
            "The generic starter unexpectedly loaded Crypt-specific atmosphere.");
        InteractWithNearest();
        AssertSmoke(string.Equals(_message, "Rien à portée d’interaction.", StringComparison.Ordinal),
            "The generic interaction path did not complete cleanly without a target.");
        RunAudioEmitterRuntimeSmokeTest();
        RunDialogueGraphRuntimeSmokeTest();
        RunQuestJournalRuntimeSmokeTest();
        RunWaypointAnchorRuntimeSmokeTest();
        RunGameplayFoundationRuntimeSmokeTest();
        RunBlackboardRulesetPluginRuntimeSmokeTest();
        RunProjectFoundationRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunProjectOperationsRuntimeSmokeTest(_loadedProject);
        RunProjectCompatibilityRuntimeSmokeTest(_loadedProject);
        RunProjectGameplayEventsRuntimeSmokeTest(_loadedProject);
        RunProjectWorldRuntimeSmokeTest(_loadedProject);
        RunUx0GGlobalEventsRuntimeSmokeTest(_loadedProject);
        RunUx0HWorldTransitionsRuntimeSmokeTest(_loadedProject);
        RunCanonicalValidationRuntimeSmokeTest();
        RunAreaFoundationRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunAreaRuntimeValidationSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_GENERIC_SMOKE_PASS slug={_moduleSlug} " +
            $"entities={_builtEntityCount} collisions={_collisionCount} combat=false interaction=empty");
        GetTree().Quit(0);
    }

    private static string NormalizeAnimationImportHint(string clip) =>
        clip.EndsWith("_loop", StringComparison.OrdinalIgnoreCase) ? clip[..^5] : clip;

    private async Task ConfigureSolarenVisualFixtureAsync(string requestedFixture)
    {
        if (!string.Equals(_moduleSlug, "solaren-les-vallees-libres", StringComparison.Ordinal))
            throw new InvalidOperationException("Solaren visual fixtures can only run against the Solaren module.");

        var fixture = requestedFixture.Trim().ToLowerInvariant();
        if (fixture is not ("door" or "rain" or "idle" or "jog"))
            throw new ArgumentException(
                $"Unknown Solaren visual fixture '{requestedFixture}'. Expected door, rain, idle or jog.",
                nameof(requestedFixture));

        var area = _activeArea ?? throw new InvalidOperationException("The Solaren area is unavailable.");
        var player = _player ?? throw new InvalidOperationException("The Solaren player is unavailable.");
        var camera = _camera ?? throw new InvalidOperationException("The Solaren camera is unavailable.");
        var doorway = area.Entities.First(entity =>
            entity.Tags.Contains("doorway", StringComparer.Ordinal) &&
            entity.Tags.Contains("house-1", StringComparer.Ordinal));
        var stairs = area.Entities.Single(entity =>
            entity.Tags.Contains("house-1", StringComparer.Ordinal) &&
            entity.Tags.Contains("entrance", StringComparer.Ordinal) &&
            entity.Tags.Contains("nonblocking", StringComparer.Ordinal) &&
            !entity.Tags.Contains("open-door", StringComparer.Ordinal));
        if (!_entityNodes.TryGetValue(doorway.Id.Value, out var doorwayNode))
            throw new InvalidOperationException("The first Solaren doorway has no reconstructed runtime node.");

        var doorwayPosition = doorwayNode.GlobalPosition;
        var stairsPosition = EngineValueConverter.ToEngine(stairs.Transform.Position);
        var outward = new Vector3(
            stairsPosition.X - doorwayPosition.X,
            0,
            stairsPosition.Z - doorwayPosition.Z).Normalized();
        if (outward.LengthSquared() < .9f)
            throw new InvalidOperationException("The first Solaren entrance has no usable outward direction.");
        var side = Vector3.Up.Cross(outward).Normalized();

        var playerPosition = fixture switch
        {
            "door" => doorwayPosition - (outward * .38f) + (Vector3.Up * 1.05f),
            "rain" => doorwayPosition + (outward * 2.15f) + (Vector3.Up * 1.05f),
            _ => doorwayPosition + (outward * 3.35f) + (side * 1.5f) + (Vector3.Up * 1.05f),
        };
        player.GlobalPosition = playerPosition;
        player.Velocity = Vector3.Zero;
        player.ResetPhysicsInterpolation();

        var weatherKind = fixture == "rain" ? WeatherKind.Rain : WeatherKind.Clear;
        var weather = area.Environment.Weather.States.Single(state => state.Kind == weatherKind);
        _weatherState = new WeatherRuntimeSnapshot(
            WeatherRuntimeSnapshot.CurrentSchemaVersion,
            weather.Id,
            0,
            0,
            0);
        ApplyClimateEffects(force: true);
        _surfaceWetnessValue = weather.SurfaceWetness;
        _surfaceWetnessCoefficients = weather.WetnessMaterial;
        _surfaceWetnessInitialized = true;
        _surfaceWetnessTransition = SurfaceWetnessTransition.Start(
            weather.SurfaceWetness,
            weather.SurfaceWetness,
            0);

        _sequenceOwnsCamera = true;
        var fixtureFieldOfView = fixture == "rain" ? 52 : 46;
        var focus = fixture switch
        {
            "door" => doorwayPosition + (Vector3.Up * 1.45f),
            "rain" => playerPosition + (Vector3.Up * .65f),
            _ => playerPosition + (Vector3.Up * .7f),
        };
        var fixtureCameraPosition = fixture switch
        {
            "door" => doorwayPosition + (outward * 7.25f) + (side * .35f) + (Vector3.Up * 3.15f),
            "rain" => playerPosition + (outward * 7.8f) + (side * 2.2f) + (Vector3.Up * 3.6f),
            _ => playerPosition + (outward * 5.6f) - (side * 4.3f) + (Vector3.Up * 2.75f),
        };
        await MutateCameraInPhysicsAsync(() =>
        {
            camera.Fov = fixtureFieldOfView;
            camera.GlobalPosition = fixtureCameraPosition;
            camera.LookAt(focus, Vector3.Up);
        });
        var cameraDirection = fixtureCameraPosition - playerPosition;
        cameraDirection.Y = 0;
        player.FaceDirection(fixture switch
        {
            "door" => outward,
            "jog" => cameraDirection,
            _ => -outward,
        });

        if (!_characterAnimations.TryGetValue(player, out var animation))
            throw new InvalidOperationException("The Solaren player has no governed animation controller.");
        var clip = fixture == "jog" ? animation.RunClip : animation.IdleClip;
        if (!GodotAssetAnimationController.Play(animation, clip, 0, restart: true))
            throw new InvalidOperationException($"The Solaren visual fixture could not play '{clip}'.");

        var settleFrames = fixture == "rain" ? 40 : 12;
        for (var frame = 0; frame < settleFrames; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (fixture == "jog")
        {
            animation.Player.Seek(.44, update: true);
            animation.Player.Pause();
        }

        GD.Print("OPENRPG_FORGE_SOLAREN_VISUAL_FIXTURE_PASS " +
                 $"fixture={fixture} house=house-1 weather={weatherKind.ToString().ToLowerInvariant()} " +
                 $"clip={clip} doorway=open player={player.GlobalPosition}");
    }

    private void RunAudioEmitterRuntimeSmokeTest()
    {
        var clipData = ProceduralAmbientAudioService.Create(ProceduralAmbientAudioService.StreamResource);
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000501");
        var instanceId = Guid.Parse("a0040000-0000-4000-8000-000000000502");
        var catalog = new AudioEmitterCatalog(1,
        [
            new(definitionId, 1, "Runtime stream",
                new AudioClipSet(Guid.Parse("a0040000-0000-4000-8000-000000000503"), 1,
                    "Runtime stream clips", AudioClipSelectionMode.Ordered,
                    [new(Guid.Parse("a0040000-0000-4000-8000-000000000504"), clipData.Resource, 1,
                        ["water", "smoke"], "runtime", "und", -18, string.Empty, "audio/wav",
                        clipData.Pcm16Mono.LongLength, "CC0-1.0", "builtin:procedural",
                        clipData.ContentSha256)], "smoke:r004"),
                new AudioLoopPolicy(AudioLoopMode.SeamlessRegion, 0, .2f, 1.6f, .1f, string.Empty),
                new AudioEmitterSpatialProfile(2, 25, "inverse-square", 180, -12, .2f, 1, true),
                null, 0, string.Empty, string.Empty, true, "smoke:r004", "builtin/audio",
                "CC0-1.0", clipData.ContentSha256),
        ],
        [
            new(instanceId, definitionId,
                new TransformValue(new Vector3Value(3, 2, -4), QuaternionValue.Identity, Vector3Value.One),
                true, "smoke:r004"),
        ]);
        AssertSmoke(AudioEmitterCatalogValidator.Validate(catalog).Count == 0,
            "The runtime audio emitter fixture is invalid.");
        var profile = new AreaAudioProfile(string.Empty, string.Empty, string.Empty, string.Empty,
            "neutral", 0, emitters: catalog);
        ConfigureAudioEmitters(profile);
        var player = GetNodeOrNull<AudioStreamPlayer3D>($"ForgeAudioEmitter_{instanceId:N}");
        AssertSmoke(player is not null && player.Position == new Vector3(3, 3, -4) &&
                    Math.Abs(player.MaxDistance - 25) < .001f &&
                    player.Stream is AudioStreamWav { LoopMode: AudioStreamWav.LoopModeEnum.Forward } wav &&
                    wav.LoopBegin > 0 && wav.LoopEnd > wav.LoopBegin,
            "The runtime did not reconstruct the positioned seamless audio emitter.");
        player!.QueueFree();
        GD.Print("OPENRPG_FORGE_RUNTIME_R004_AUDIO_PASS object=audio-stream-player-3d " +
            "position=canonical loop=wav-region optional=empty-safe required=diagnostic");
    }

    private void RunDialogueGraphRuntimeSmokeTest()
    {
        IReadOnlyList<DialogueDefinition> optionalDialogues = [];
        AssertSmoke(optionalDialogues.Count == 0, "An optional empty dialogue catalog was not safe.");
        var entry = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000201"));
        var end = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000202"));
        var reference = DialogueChoiceId.From(Guid.Parse("d0050000-0000-4000-8000-000000000203"));
        var flow = DialogueChoiceId.From(Guid.Parse("d0050000-0000-4000-8000-000000000204"));
        var graph = new DialogueDefinition(
            DialogueId.From(Guid.Parse("d0050000-0000-4000-8000-000000000205")),
            "runtime-dialogue-probe", "Runtime dialogue probe", entry,
            [
                new(entry, null, new LocalizedText("dialogue.runtime.entry", "Choose",
                    [LocalizedTextVariant.Create("dialogue.runtime.entry", "fr", "Choisir")], "smoke:r006"),
                    [new DialogueAction(DialogueActionKind.PlayAudio, "dialogue.entry", "play")], choices:
                [
                    new(reference, new("dialogue.runtime.reference", "Inspect"), targetNodeId: end,
                        edgeKind: DialogueEdgeKind.Reference, priority: 10),
                    new(flow, new("dialogue.runtime.flow", "Continue"),
                        [new DialogueCondition(DialogueConditionKind.HasItem, "ash-key", "true"),
                         new DialogueCondition(DialogueConditionKind.QuestStateEquals, "crypt", "completed", negated: true)],
                        [new DialogueAction(DialogueActionKind.StartSequence, "crypt-opening", "start")],
                        end, priority: 20, conditionCombination: DialogueConditionCombination.All),
                ], role: DialogueNodeRole.PlayerChoice),
                new(end, null, new("dialogue.runtime.end", "End"), role: DialogueNodeRole.End),
            ], provenance: "smoke:r005");
        var traversal = new DialogueTraversal(graph);
        var ordered = traversal.GetAvailableChoices(new RuntimeConditionEvaluator(this));
        var simulation = DialogueSimulationService.Run(graph,
            new Dictionary<string, string> { ["item:ash-key"] = "true", ["quest:crypt"] = "active" },
            [flow]);
        var invalid = new DialogueDefinition(
            DialogueId.From(Guid.Parse("d0050000-0000-4000-8000-000000000206")),
            "required-invalid-dialogue", "Required invalid dialogue", entry,
            [new(entry, null, new("dialogue.runtime.invalid", "Invalid"),
                choices: [new(flow, new("dialogue.runtime.invalid.choice", "Invalid"))],
                role: DialogueNodeRole.End)], provenance: "smoke:r005");
        AssertSmoke(DialogueValidator.Validate(graph).Count == 0 && ordered[0].Id == reference &&
                    simulation.Succeeded && simulation.Completed &&
                    simulation.FinalState.Values.GetValueOrDefault("sequence:crypt-opening") == "start" &&
                    DialogueValidator.Validate(invalid).Any(item => item.Code == DialogueValidator.NodeRoleInvalidCode),
            "Dialogue graph runtime ordering, reference reuse or required diagnostics failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R005_DIALOGUE_PASS roles=explicit order=priority-list " +
            "reference=reused optional=empty-safe required=diagnostic");
        GD.Print("OPENRPG_FORGE_RUNTIME_R006_DIALOGUE_PASS localization=variant-fallback " +
            "conditions=all-any-not actions=transactional simulation=sandbox-journal " +
            "lint=structural optional=empty-safe required=diagnostic");
    }

    private void RunQuestJournalRuntimeSmokeTest()
    {
        IReadOnlyList<QuestDefinition> optionalQuests = [];
        AssertSmoke(optionalQuests.Count == 0, "An optional empty quest catalog was not safe.");
        var initial = QuestStateId.From(Guid.Parse("c7070000-0000-4000-8000-000000000101"));
        var failed = QuestStateId.From(Guid.Parse("c7070000-0000-4000-8000-000000000102"));
        var transition = QuestTransitionId.From(Guid.Parse("c7070000-0000-4000-8000-000000000103"));
        var quest = new QuestDefinition(
            QuestId.From(Guid.Parse("c7070000-0000-4000-8000-000000000104")), 1, "smoke:r007",
            "runtime-journal-probe", "Runtime journal probe",
            new("quest.runtime.title", "Épreuve de la braise",
                [LocalizedTextVariant.Create("quest.runtime.title", "en", "Trial of the Ember")], "smoke:r007"),
            new("quest.runtime.description", "Vérifie la progression isolée."), "main", 80, "runtime:smoke",
            true, new(QuestProgressScope.Player, [QuestProgressScope.Player], false), initial,
            [
                new(initial, 1, "smoke:r007", "initial", "Initial", new(
                    new LocalizedText("quest.runtime.initial.summary", "Entrez dans la crypte."),
                    new LocalizedText("quest.runtime.initial.details", "Atteignez le sanctuaire."),
                    [new LocalizedText("quest.runtime.initial.objective", "Atteindre le sanctuaire")],
                    [new QuestJournalLink(QuestJournalLinkKind.Custom, "lore:ember")], "smoke:r007"),
                    false, 1, 0, QuestTerminalKind.None, string.Empty, QuestReactivationRule.Never),
                new(failed, 1, "smoke:r007", "failed", "Échouée", new(
                    new LocalizedText("quest.runtime.failed.summary", "Le sanctuaire est perdu."),
                    new LocalizedText("quest.runtime.failed.details", "La quête peut être réactivée manuellement."),
                    provenance: "smoke:r007"), true, 2, 1, QuestTerminalKind.Failed, string.Empty,
                    QuestReactivationRule.Manual),
            ],
            [new QuestTransition(transition, initial, failed)]);
        var instance = QuestInstanceService.Create(quest, QuestProgressScope.Player, "player:runtime",
            DateTimeOffset.Parse("2026-08-07T12:00:00Z"), id: Guid.Parse("c7070000-0000-4000-8000-000000000105"));
        var terminal = QuestInstanceService.Advance(quest, instance, transition, new RuntimeConditionEvaluator(this),
            DateTimeOffset.Parse("2026-08-07T12:01:00Z"), "player:runtime");
        var reactivated = QuestInstanceService.Reactivate(quest, terminal,
            DateTimeOffset.Parse("2026-08-07T12:02:00Z"), "gm:runtime");
        var invalidRequired = new QuestDefinition(quest.Id, 99, quest.Provenance, quest.Slug, quest.DisplayName,
            quest.Title, quest.Description, quest.Category, quest.Priority, quest.Owner, true, quest.InstancePolicy,
            quest.InitialStateId, quest.States, quest.Transitions, quest.Operations);
        AssertSmoke(QuestValidator.Validate(quest).Count == 0 && terminal.CurrentStateId == failed &&
                    terminal.History.Count == 2 && reactivated.CurrentStateId == initial &&
                    quest.States.Single(item => item.Id == failed).TerminalKind == QuestTerminalKind.Failed &&
                    QuestValidator.Validate(invalidRequired).Any(item => item.Code == QuestValidator.SchemaUnsupportedCode),
            "Quest journal instance, terminal state, reactivation or required diagnostic failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R007_QUEST_PASS metadata=localized-category-priority-owner " +
            "states=uuid-order-legacy-index terminal=typed-reactivation instance=player-history-variables-time " +
            "journal=summary-details-objectives-links optional=empty-safe required=diagnostic");

        var r008Transition = new QuestTransition(transition, 1, "smoke:r008", initial, failed, 1,
            new QuestEventBinding(QuestEventKind.Script, "quest.fail", "script:runtime-smoke", "smoke:r008"),
            90, QuestTransitionIdempotence.PerEvent, "runtime-fail", 1,
            actions: [new QuestAction(QuestActionKind.SetVariable, "quest.failed", "true")]);
        var r008Simulation = new QuestSimulationProfile(
            Guid.Parse("c7080000-0000-4000-8000-000000000101"), 8,
            events: [new QuestSimulationEvent(Guid.Parse("c7080000-0000-4000-8000-000000000102"),
                "quest.fail", DateTimeOffset.Parse("2026-08-07T12:01:00Z"), "script:runtime")],
            provenance: "smoke:r008");
        var r008Quest = new QuestDefinition(quest.Id, quest.SchemaVersion, "smoke:r008", quest.Slug,
            quest.DisplayName, quest.Title, quest.Description, quest.Category, quest.Priority, quest.Owner,
            quest.RequiredAtRuntime, quest.InstancePolicy, quest.InitialStateId, quest.States, [r008Transition],
            quest.Operations, r008Simulation);
        var r008Result = QuestSimulationEngine.Run(r008Quest, QuestProgressScope.Player, "player:runtime",
            DateTimeOffset.Parse("2026-08-07T12:00:00Z"));
        AssertSmoke(QuestValidator.Validate(r008Quest).Count == 0 &&
                    r008Result.Status == QuestSimulationStatus.Completed && r008Result.Audit.Count == 1 &&
                    r008Result.Instance.Variables.GetValueOrDefault("quest.failed") == "true" &&
                    r008Result.Audit[0].IdempotenceKey.Contains("runtime-fail", StringComparison.Ordinal),
            "Quest transition binding, sandbox simulation, audit or narrative validation failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R008_QUEST_PASS transition=event-priority-idempotence-order " +
            "simulation=events-effects-audit-terminal lint=clean optional=empty-safe required=diagnostic");

        var objectives = Enum.GetValues<QuestObjectiveKind>().Select((kind, index) =>
            new QuestAuthoringObjective(Guid.Parse($"c7110000-0000-4000-8000-{(500 + index):D12}"), 1,
                "smoke:r071", index + 1, kind,
                new LocalizedText($"quest.r071.{kind.ToString().ToLowerInvariant()}", kind.ToString()),
                kind is QuestObjectiveKind.Counter or QuestObjectiveKind.Boolean or QuestObjectiveKind.Collection ? 1 : 0,
                kind is QuestObjectiveKind.Collection or QuestObjectiveKind.Escort ? ["ash-key"] : [],
                kind is QuestObjectiveKind.Destination or QuestObjectiveKind.Escort ? "crypt-of-ashes" : null,
                kind == QuestObjectiveKind.Survival ? 60 : 0,
                kind == QuestObjectiveKind.Choice ? ["choice:dawn", "choice:dusk"] : [],
                kind == QuestObjectiveKind.Custom ? "rule:ember" : null, false)).ToArray();
        var participant = new QuestAuthoringParticipant(
            Guid.Parse("c7110000-0000-4000-8000-000000000601"), 1, "smoke:r071", 1, "guide",
            QuestParticipantBindingKind.Query, "query:tag=guide", ["quest:active"], true);
        var authoring = new QuestAuthoringProfile(
            Guid.Parse("c7110000-0000-4000-8000-000000000602"), 1, "smoke:r071", true, true,
            objectives, new QuestAuthoringRewardBundle(
                Guid.Parse("c7110000-0000-4000-8000-000000000603"), 1, "smoke:r071", 250,
                new Dictionary<string, int> { ["ash-key"] = 1 },
                new Dictionary<string, decimal> { ["ember"] = 5 },
                new Dictionary<string, double> { ["ash-watch"] = 2 }, ["unlock:crypt"], ["choice:dawn"]),
            new QuestAuthorNotes("Restore the vigil", "The ember pact is canonical", "author:runtime",
                QuestEditorialStatus.Review, ["branch complexity"], ["record voices"], ["08_QUEST.md"],
                [new QuestAuthorReview(Guid.Parse("c7110000-0000-4000-8000-000000000604"), 1,
                    "smoke:r071", "reviewer:runtime", QuestEditorialStatus.Review, "Reviewed",
                    DateTimeOffset.Parse("2026-08-11T00:00:00Z"))]), [participant],
            new QuestPlotWizardPlan(Guid.Parse("c7110000-0000-4000-8000-000000000605"), 1,
                "smoke:r071", ["crypt-of-ashes"], [participant.Id], ["crypt-pillar"],
                [DialogueId.From(Guid.Parse("40000000-0000-4000-8000-000000000001"))],
                objectives.Select(item => item.Id).ToArray(), r008Quest.Transitions.Select(item => item.Id).ToArray(),
                ["nominal-path", "objective-failure", "repeated-transition", "multiplayer-authority", "save-resume"],
                false));
        var r071Quest = r008Quest.WithAuthoring(authoring);
        var rewardState = new QuestAuthoringRewardState(0, new Dictionary<string, int>(),
            new Dictionary<string, decimal>(), new Dictionary<string, double>(), new HashSet<string>(),
            new HashSet<string>());
        var reward = QuestAuthoringRuntimeService.GrantRewards(r071Quest, rewardState, "player:runtime");
        var script = new QuestJournalScriptService(r071Quest);
        var authoringSnapshot = script.ReadAuthoring(script.Handle);
        BuildQuestAuthoringAdapters([r071Quest]);
        var questAdapter = _questAuthoringRoot?.GetNodeOrNull<Node>($"Quest_{r071Quest.Id.Value:N}");
        var invalidAuthoringRequired = r071Quest.WithAuthoring(authoring with
        { Enabled = false, RequiredAtRuntime = true });
        AssertSmoke(QuestAuthoringProfileValidator.Validate(r071Quest).Count == 0 &&
                    Enum.GetValues<QuestObjectiveKind>().All(kind =>
                        authoring.Objectives.Any(item => item.Kind == kind)) && reward.Committed &&
                    reward.State.Progression == 250 && reward.State.Items["ash-key"] == 1 &&
                    reward.State.Currencies["ember"] == 5 && reward.State.Reputation["ash-watch"] == 2 &&
                    reward.State.Unlocks.Contains("unlock:crypt") && reward.State.Choices.Contains("choice:dawn") &&
                    authoringSnapshot.ObjectiveCount == 8 && authoringSnapshot.ParticipantCount == 1 &&
                    authoringSnapshot.GeneratedTestCount == 5 && !authoringSnapshot.GeneratesOpaqueCode &&
                    questAdapter is not null &&
                    questAdapter.GetNodeOrNull<Node>("ExplicitObjectives")?.GetChildCount() == 8 &&
                    questAdapter.GetNodeOrNull<Node>("ScenarioCast")?.GetChildCount() == 1 &&
                    questAdapter.GetNodeOrNull<Node>("TransparentPlotWizard") is not null &&
                    QuestAuthoringProfileValidator.Validate(r008Quest).Count == 0 &&
                    QuestAuthoringProfileValidator.Validate(invalidAuthoringRequired)
                        .Any(item => item.Code == QuestAuthoringProfileValidator.RequiredMissingCode),
            "Quest authoring objectives, rewards, notes, cast, wizard, script or engine adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R071_QUEST_AUTHORING_PASS " +
            "objectives=counter-boolean-collection-destination-escort-survival-choice-custom " +
            "rewards=atomic-progression-items-currency-reputation-unlocks-choices " +
            "notes=design-canon-owner-status-risks-todo-sources-review-history " +
            "cast=role-blueprint-instance-query-presence-required " +
            "wizard=zones-cast-props-dialogues-objectives-transitions-five-tests-no-opaque-code " +
            "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes");

        var resources = Enum.GetValues<QuestExperienceResourceKind>().Select((kind, index) =>
            new QuestExperienceResource(Guid.Parse($"c7120000-0000-4000-8000-{(500 + index):D12}"), 1,
                "smoke:r072", index + 1, kind, $"resource:{kind.ToString().ToLowerInvariant()}",
                "owner:module", false, ["quest:active"])).ToArray();
        var experience = new QuestExperienceProfile(
            Guid.Parse("c7120000-0000-4000-8000-000000000601"), 1, "smoke:r072", true, true,
            resources,
            [new QuestExperienceStep(Guid.Parse("c7120000-0000-4000-8000-000000000602"), 1,
                "smoke:r072:template:investigation:v2", 1,
                new LocalizedText("quest.r072.step", "Inspect the ember prism"), [objectives[0].Id],
                r071Quest.Transitions.Select(item => item.Id).ToArray(), "investigation", 2, true)],
            new QuestExperienceVisibilityPolicy(Guid.Parse("c7120000-0000-4000-8000-000000000603"), 1,
                "smoke:r072", QuestExperienceVisibility.Hidden,
                new Dictionary<QuestExperienceVisibility, IReadOnlyList<string>>
                {
                    [QuestExperienceVisibility.Rumor] = ["heard"],
                    [QuestExperienceVisibility.Discovered] = ["heard", "seen"],
                    [QuestExperienceVisibility.Tracked] = ["accepted"],
                    [QuestExperienceVisibility.SecretCompleted] = ["secret:complete"],
                }, true),
            [
                new QuestExperienceHistoryEvent(Guid.Parse("c7120000-0000-4000-8000-000000000604"), 1,
                    "smoke:r072", r071Quest.Id, "none", "hidden", "module-load", "server",
                    DateTimeOffset.Parse("2026-08-11T08:00:00Z"), new Dictionary<string, string>(),
                    new LocalizedText("quest.r072.history.hidden", "A rumor rises.")),
                new QuestExperienceHistoryEvent(Guid.Parse("c7120000-0000-4000-8000-000000000605"), 1,
                    "smoke:r072", r071Quest.Id, "hidden", "discovered", "dialogue", "player:runtime",
                    DateTimeOffset.Parse("2026-08-11T08:01:00Z"),
                    new Dictionary<string, string> { ["seed"] = "72" },
                    new LocalizedText("quest.r072.history.discovered", "The crypt is revealed.")),
            ],
            Enum.GetValues<QuestExperienceMarkerBindingKind>().Select((kind, index) =>
                new QuestExperienceMarker(Guid.Parse($"c7120000-0000-4000-8000-{(700 + index):D12}"), 1,
                    "smoke:r072", index + 1, objectives[0].Id, kind,
                    kind switch
                    {
                        QuestExperienceMarkerBindingKind.Zone => "crypt-of-ashes",
                        QuestExperienceMarkerBindingKind.Entity => "entity:guardian",
                        QuestExperienceMarkerBindingKind.Query => "query:nearest-ember",
                        _ => "waypoint:crypt-entry",
                    }, QuestExperienceVisibility.Discovered, 4 + index, true,
                    new LocalizedText($"quest.r072.marker.{index}", kind.ToString()),
                    kind == QuestExperienceMarkerBindingKind.Waypoint ? new Vector3Value(1, 0, 2) : null))
                .ToArray());
        var r072Quest = r071Quest.WithExperience(experience);
        var experienceScript = new QuestJournalScriptService(r072Quest);
        var experienceSnapshot = experienceScript.ReadExperience(experienceScript.Handle);
        var playerHistory = experienceScript.ReadHistory(experienceScript.Handle,
            QuestExperienceHistoryView.Player);
        var debugHistory = experienceScript.ReadHistory(experienceScript.Handle,
            QuestExperienceHistoryView.Debug);
        var revealed = QuestExperienceRuntimeService.Reveal(experience.Visibility,
            QuestExperienceVisibility.Hidden, new HashSet<string> { "heard", "seen" });
        var waypointMarker = experience.Markers.Single(item =>
            item.BindingKind == QuestExperienceMarkerBindingKind.Waypoint);
        var resolvedMarker = QuestExperienceRuntimeService.ResolveMarker(waypointMarker,
            QuestExperienceVisibility.Tracked, true);
        BuildQuestExperienceAdapters([r072Quest]);
        var experienceAdapter = _questExperienceRoot?.GetNodeOrNull<Node>($"Quest_{r072Quest.Id.Value:N}");
        var invalidExperienceRequired = r072Quest.WithExperience(experience with
        { Enabled = false, RequiredAtRuntime = true });
        AssertSmoke(QuestExperienceProfileValidator.Validate(r072Quest).Count == 0 &&
                    Enum.GetValues<QuestExperienceResourceKind>().All(kind =>
                        experience.Resources.Any(item => item.Kind == kind)) &&
                    Enum.GetValues<QuestExperienceMarkerBindingKind>().All(kind =>
                        experience.Markers.Any(item => item.BindingKind == kind)) &&
                    experienceSnapshot.ResourceCount == 5 && experienceSnapshot.StepCount == 1 &&
                    experienceSnapshot.HistoryEventCount == 2 && experienceSnapshot.MarkerCount == 4 &&
                    revealed == QuestExperienceVisibility.Discovered && resolvedMarker.Tracked &&
                    playerHistory.All(item => item.Cause is null) && debugHistory[1].Cause == "dialogue" &&
                    experienceAdapter?.GetNodeOrNull<Node>("TypedResources")?.GetChildCount() == 5 &&
                    experienceAdapter.GetNodeOrNull<Node>("EditableStorySteps")?.GetChildCount() == 1 &&
                    experienceAdapter.GetNodeOrNull<Node>("VisibilityPolicy") is not null &&
                    experienceAdapter.GetNodeOrNull<Node>("TimestampedHistory")?.GetChildCount() == 2 &&
                    experienceAdapter.GetNodeOrNull<Node>("ObjectiveMapMarkers")?.GetChildCount() == 4 &&
                    QuestExperienceProfileValidator.Validate(r071Quest).Count == 0 &&
                    QuestExperienceProfileValidator.Validate(invalidExperienceRequired).Any(item =>
                        item.Code == QuestExperienceProfileValidator.RequiredMissingCode),
            "Quest resources, steps, visibility, timeline, markers, script or engine adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R072_QUEST_EXPERIENCE_PASS " +
            "resources=item-placeable-door-zone-asset-ownership-resolved " +
            "steps=template-version-objectives-transitions-editable-provenance " +
            "visibility=hidden-rumor-discovered-tracked-secret-completed-conditions " +
            "history=timestamp-transition-cause-actor-player-debug " +
            "markers=zone-entity-query-waypoint-visibility-precision-tracking-fallback " +
            "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes");

        var qualityDependencies = new[]
        {
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000501"), 1, "smoke:r073", 1,
                QuestAiDependencyKind.State, r072Quest.InitialStateId.ToString()),
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000502"), 1, "smoke:r073", 2,
                QuestAiDependencyKind.Transition, r072Quest.Transitions[0].Id.ToString()),
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000503"), 1, "smoke:r073", 3,
                QuestAiDependencyKind.Participant, authoring.Participants[0].Id.ToString("D")),
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000504"), 1, "smoke:r073", 4,
                QuestAiDependencyKind.Zone, authoring.Wizard.ZoneIds[0]),
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000505"), 1, "smoke:r073", 5,
                QuestAiDependencyKind.Asset, "asset:quest-generation-model"),
        };
        var aiPlan = QuestQualityRuntimeService.CreateAiPlan(r072Quest,
            new LocalizedText("quest.r073.ai.synopsis", "Restore the vigil before the crypt collapses."),
            qualityDependencies, ["08_QUEST.md#orf-qst-025"], ["style:crypt", "style:human-review"],
            "agent:forge", Guid.Parse("c7130000-0000-4000-8000-000000000506"),
            $"preview:quest:{r072Quest.Id}|states:{r072Quest.States.Count}|participants:{authoring.Participants.Count}");
        aiPlan = QuestQualityRuntimeService.Approve(aiPlan, "author:karen",
            DateTimeOffset.Parse("2026-08-11T12:00:00Z"));
        var quality = new QuestQualityProfile(Guid.Parse("c7130000-0000-4000-8000-000000000507"), 1,
            "smoke:r073", true, true, QuestQualityRuntimeService.GenerateTests(r072Quest, 7300), aiPlan);
        var r073Quest = r072Quest.WithQuality(quality);
        var qualityScript = new QuestJournalScriptService(r073Quest);
        var qualitySnapshot = qualityScript.ReadQuality(qualityScript.Handle);
        var nominalScenario = quality.TestScenarios.Single(item => item.Kind == QuestQualityTestKind.Nominal);
        var nominalRun = QuestQualityRuntimeService.Run(nominalScenario, (step, state) =>
            step.Action == QuestQualityTestActionKind.CompleteObjectives
                ? new Dictionary<string, string>
                {
                    ["quest/state"] = "completed",
                    ["quest/rewards"] = "true",
                }
                : state);
        var rejectedRun = QuestQualityRuntimeService.Run(nominalScenario, (_, state) => state);
        var appliedAiPlan = QuestQualityRuntimeService.MarkApplied(aiPlan);
        BuildQuestQualityAdapters([r073Quest]);
        var qualityAdapter = _questQualityRoot?.GetNodeOrNull<Node>($"Quest_{r073Quest.Id.Value:N}");
        var scenarioAdapter = qualityAdapter?.GetNodeOrNull<Node>("GeneratedTestScenarios")?
            .GetChildOrNull<Node>(0);
        var restoredQuality = QuestQualityProfileJsonSerializer.Deserialize(
            QuestQualityProfileJsonSerializer.Serialize(quality));
        var invalidQualityRequired = r073Quest.WithQuality(quality with
        { Enabled = false, RequiredAtRuntime = true });
        AssertSmoke(QuestQualityProfileValidator.Validate(r073Quest).Count == 0 &&
                    Enum.GetValues<QuestQualityTestKind>().All(kind =>
                        quality.TestScenarios.Any(item => item.Kind == kind)) &&
                    quality.TestScenarios.All(item => item.Fixtures.Count > 0 && item.Steps.Count > 0 &&
                        item.Edges.Count == item.Steps.Count - 1 && item.Assertions.Count > 0) &&
                    qualitySnapshot.ScenarioCount == 5 && qualitySnapshot.StepCount == 11 &&
                    qualitySnapshot.AssertionCount == 6 && qualitySnapshot.HumanApproved &&
                    nominalRun.Passed && !rejectedRun.Passed && appliedAiPlan.Applied &&
                    qualityAdapter?.GetNodeOrNull<Node>("GeneratedTestScenarios")?.GetChildCount() == 5 &&
                    scenarioAdapter?.GetNodeOrNull<Node>("TypedFixtures")?.GetChildCount() > 0 &&
                    scenarioAdapter.GetNodeOrNull<Node>("TypedStepNodes")?.GetChildCount() > 0 &&
                    scenarioAdapter.GetNodeOrNull<Node>("DeterministicEdges")?.GetChildCount() > 0 &&
                    scenarioAdapter.GetNodeOrNull<Node>("TypedAssertions")?.GetChildCount() > 0 &&
                    qualityAdapter.GetNodeOrNull<Node>("GovernedAiPlan")?.GetNodeOrNull<Node>("TypedDependencies")?
                        .GetChildCount() == 5 && restoredQuality.TestScenarios.Count == 5 &&
                    restoredQuality.AiPlan.TransactionId == aiPlan.TransactionId &&
                    QuestQualityProfileValidator.Validate(r072Quest).Count == 0 &&
                    QuestQualityProfileValidator.Validate(invalidQualityRequired).Any(item =>
                        item.Code == QuestQualityProfileValidator.RequiredMissingCode),
            "Quest generated tests, AI plan, script, persistence or engine adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R073_QUEST_QUALITY_PASS " +
            "tests=nominal-failure-repetition-multiplayer-save-resume-seed-fixtures-nodes-edges-assertions-runner " +
            "ai=synopsis-states-participants-dependencies-citations-style-lint-human-approval-transaction-plan-semantic-diff-authorization " +
            "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes");
    }

    private void RunWaypointAnchorRuntimeSmokeTest()
    {
        var project = _loadedProject ?? throw new InvalidOperationException("Runtime project is not loaded.");
        var area = project.Areas.First();
        var playerSpawn = project.Blueprints.First(item => item.Kind == BlueprintKind.PlayerSpawn);
        static TransformValue At(float x, float y, float z, float yaw)
        {
            var half = yaw * MathF.PI / 360;
            return new(new Vector3Value(x, y, z),
                new QuaternionValue(0, MathF.Sin(half), 0, MathF.Cos(half)), Vector3Value.One);
        }

        var spawnDefinition = new AnchorDefinition(
            Guid.Parse("c7090000-0000-4000-8000-000000000101"), 1, "smoke:r009", "runtime-spawn",
            new LocalizedText("anchor.runtime.spawn", "Départ runtime"), AnchorKind.Spawn, AnchorScope.Area,
            ["player", "spawn"], playerSpawn.Id, null, true);
        var portalDefinition = new AnchorDefinition(
            Guid.Parse("c7090000-0000-4000-8000-000000000102"), 1, "smoke:r009", "runtime-portal",
            new LocalizedText("anchor.runtime.portal", "Portail runtime"), AnchorKind.TransitionDestination,
            AnchorScope.Area, ["portal"], null, null, true);
        var waypointDefinition = new AnchorDefinition(
            Guid.Parse("c7090000-0000-4000-8000-000000000103"), 1, "smoke:r009", "runtime-waypoint",
            new LocalizedText("anchor.runtime.waypoint", "Repère runtime"), AnchorKind.Waypoint,
            AnchorScope.Area, ["quest"], null, null, false);
        var primaryId = Guid.Parse("c7090000-0000-4000-8000-000000000201");
        var fallbackId = Guid.Parse("c7090000-0000-4000-8000-000000000202");
        var portalId = Guid.Parse("c7090000-0000-4000-8000-000000000203");
        var waypointId = Guid.Parse("c7090000-0000-4000-8000-000000000204");
        var catalog = new AnchorCatalog(Guid.Parse("c7090000-0000-4000-8000-000000000301"), 1,
            "smoke:r009", area.Id, true, [spawnDefinition, portalDefinition, waypointDefinition],
            [
                new AnchorInstance(primaryId, 1, "smoke:r009", spawnDefinition.Id, At(0, 0, 0, 180), 1,
                    EntityPropertyOverrides.Empty,
                    new SpawnAnchorProfile("player", new Vector3Value(1.2f, 2, 1.2f), 100, "heroes", 180,
                        fallbackId, true), null),
                new AnchorInstance(fallbackId, 1, "smoke:r009", spawnDefinition.Id, At(2, 0, 0, 90), 2,
                    EntityPropertyOverrides.Empty,
                    new SpawnAnchorProfile("fallback", new Vector3Value(1.2f, 2, 1.2f), 10, "heroes", 90,
                        null, true), null),
                new AnchorInstance(portalId, 1, "smoke:r009", portalDefinition.Id, At(4, 0, 0, 0), 3,
                    EntityPropertyOverrides.Empty, null,
                    new PortalAnchorDestination(area.Id, waypointId, PortalOrientationPolicy.AnchorRotation, 0,
                        PortalVelocityPolicy.RedirectForward, true)),
                new AnchorInstance(waypointId, 1, "smoke:r009", waypointDefinition.Id, At(-2, 0, 1, 45), 4,
                    new EntityPropertyOverrides(1, [new EntityPropertyOverride("quest.marker",
                        EntityPropertyValueKind.String, "ember")]), null, null),
            ]);
        var configuredArea = area.WithNavigation(area.Navigation.WithAnchors(catalog));
        var configuredProject = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints, project.Areas.Select(item => item.Id == area.Id ? configuredArea : item).ToArray(),
            project.Dialogues, project.Quests);
        var script = new AnchorScriptService(configuredProject, area.Id);
        var query = script.Query(script.Handle, tag: "spawn", limit: 10);
        var direct = script.ResolveSpawn(script.Handle, "player", "heroes");
        var fallback = script.ResolveSpawn(script.Handle, "player", "heroes", new HashSet<Guid>([primaryId]));
        var travel = script.ResolvePortal(script.Handle, portalId, new Vector3Value(2, 0, 0), 0);
        var optional = area.WithNavigation(area.Navigation.WithAnchors(AnchorCatalog.CreateDefault(area.Id)));
        var invalidRequired = area.WithNavigation(area.Navigation.WithAnchors(new AnchorCatalog(
            Guid.Parse("c7090000-0000-4000-8000-000000000302"), 1, "smoke:r009", area.Id, true, [], [])));
        AssertSmoke(AnchorCatalogValidator.Validate(configuredProject, configuredArea).Count == 0 &&
                    query.Count == 2 && direct.Instance.Id == primaryId &&
                    fallback.Instance.Id == fallbackId && fallback.UsedFallback &&
                    travel.TargetAnchorInstanceId == waypointId && travel.Preload &&
                    Math.Abs(travel.ArrivalYawDegrees - 45) < 0.01f &&
                    AnchorCatalogValidator.Validate(project, optional).Count == 0 &&
                    AnchorCatalogValidator.Validate(project, invalidRequired)
                        .Any(item => item.Code == AnchorCatalogValidator.RequiredContentCode),
            "Waypoint, spawn, portal, optional-empty or required diagnostics failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R009_WAYPOINT_PASS " +
            "waypoint=named-localized spawn=priority-clearance-team-orientation-fallback-navigation " +
            "portal=destination-orientation-velocity-preload forgescript=typed-handle-query " +
            "optional=empty-safe required=diagnostic");
        RunR064WaypointPatrolRuntimeSmokeTest(project, area);
    }

    private void RunR064WaypointPatrolRuntimeSmokeTest(ProjectDefinition project, AreaDefinition area)
    {
        var waypoints = new[]
        {
            new WaypointProfile(Guid.Parse("a0646000-0000-4000-8000-000000000011"), 1, "runtime:r064",
                "Entrée de la crypte", new Vector3Value(-6, 0, -3),
                new("flag", "#ff8844", "quest", true, 32),
                new("Entrée révélée", "quest", -1, "quest", 2,
                    MarkerVisibilityPolicy.HiddenUntilRevealed, MarkerVisibilityScope.Party,
                    true, false, "quest.crypt.found", "pulse", true), RequiredAtRuntime: true),
            new WaypointProfile(Guid.Parse("a0646000-0000-4000-8000-000000000012"), 1, "runtime:r064",
                "Brasero", Vector3Value.Zero, new("diamond", "#83d6ae", "patrol", true, 26), null,
                RequiredAtRuntime: true),
            new WaypointProfile(Guid.Parse("a0646000-0000-4000-8000-000000000013"), 1, "runtime:r064",
                "Porte", new Vector3Value(6, 0, 4), new("arrow", "#a78bfa", "patrol", true, 28), null,
                RequiredAtRuntime: true),
        };
        var points = waypoints.Select((item, index) => new PatrolPoint(item.Id, item.Position,
            index == 1 ? 2 : 0, index * 90, index == 2 ? 3 : 2,
            index == 1 ? "inspect" : null, index == 0 ? "quest.crypt.active" : null,
            index + 1)).ToArray();
        var route = new PatrolRoute(Guid.Parse("a0646000-0000-4000-8000-000000000020"), 1,
            "runtime:r064", PatrolTraversalPolicy.PingPong, points, requiredAtRuntime: true);
        var profile = new WaypointAuthoringProfile(Guid.Parse("a0646000-0000-4000-8000-000000000030"),
            1, "runtime:r064", area.Id, true, waypoints, [route]);
        var configuredArea = area.WithNavigation(area.Navigation.WithWaypoints(profile));
        var configuredProject = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints, project.Areas.Select(item => item.Id == area.Id ? configuredArea : item).ToArray(),
            project.Dialogues, project.Quests);
        var script = new WaypointAuthoringScriptService(configuredProject, area.Id);
        var revealed = WaypointRuntimeService.Reveal(profile, WaypointRuntimeState.Empty,
            MarkerVisibilityScope.Party, "quest.crypt.found");
        var marker = WaypointRuntimeService.VisibleMarkers(profile, revealed.State,
            MarkerVisibilityScope.Party).Single();
        _hud?.ShowWaypointMarker(marker);
        var first = WaypointRuntimeService.Advance(profile, revealed.State, route.Id);
        var second = WaypointRuntimeService.Advance(profile, first.State, route.Id);
        var third = WaypointRuntimeService.Advance(profile, second.State, route.Id);
        if (third.Step is { } step) ApplyWaypointPatrolStep(step);
        var denied = WaypointRuntimeService.Advance(profile, third.State, route.Id,
            authorityGranted: false);
        var requiredMissing = new WaypointAuthoringProfile(Guid.NewGuid(), 1, "runtime:r064", area.Id,
            true, [], []);
        AssertSmoke(WaypointAuthoringValidator.Validate(configuredArea).Count == 0 &&
                    script.Query(script.Handle).Count == 3 && script.QueryRoutes(script.Handle).Count == 1 &&
                    marker.Tracked && _hud?.WaypointMarkerText.Contains("Entrée révélée",
                        StringComparison.CurrentCulture) == true &&
                    first.Step?.Cursor.Index == 1 && second.Step?.Cursor.Index == 2 &&
                    third.Step?.Cursor.Index == 1 && _waypointPatrolActor is not null &&
                    denied.Diagnostics.Any(item => item.Code == WaypointRuntimeService.AuthorityDeniedCode) &&
                    WaypointRuntimeService.ValidateRuntime(WaypointAuthoringProfile.CreateDefault(area.Id)).Count == 0 &&
                    WaypointRuntimeService.ValidateRuntime(requiredMissing)
                        .Any(item => item.Code == WaypointRuntimeService.RequiredMissingCode),
            "Waypoint marker HUD, reveal authority, patrol traversal, script handle or runtime fallback failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R064_WAYPOINT_PATROL_PASS " +
            "appearance=icon-color-layer-visible-constant-screen-size map=text-icon-floor-category-precision-policy-tracking-hud " +
            "reveal=player-party-world-event-animation-persistence patrol=ordered-wait-facing-speed-action-branch-weight-engine-actor " +
            "traversal=loop-pingpong-once-random-weighted-scripted forgescript=typed-handles authority=server " +
            "optional=ignored required=diagnostic");
        RunR065WaypointLocatorRuntimeSmokeTest(project, area);
    }

    private void RunR065WaypointLocatorRuntimeSmokeTest(ProjectDefinition project, AreaDefinition area)
    {
        var arrival = new ArrivalBehavior(ArrivalFacingMode.Target, 0, "gate.ashes", "inspect",
            1.5, "open", "waypoint.arrived");
        var waypoints = new[]
        {
            new WaypointProfile(Guid.Parse("a0656000-0000-4000-8000-000000000011"), 1,
                "runtime:r065", "Entrée", new Vector3Value(-4, 0, -2),
                new("flag", "#ff8844", "quest", true, 30), null, RequiredAtRuntime: true,
                Arrival: arrival),
            new WaypointProfile(Guid.Parse("a0656000-0000-4000-8000-000000000012"), 1,
                "runtime:r065", "Brasero", Vector3Value.Zero,
                new("diamond", "#83d6ae", "patrol", true, 26), null, RequiredAtRuntime: true),
            new WaypointProfile(Guid.Parse("a0656000-0000-4000-8000-000000000013"), 1,
                "runtime:r065", "Porte", new Vector3Value(5, 0, 3),
                new("arrow", "#a78bfa", "patrol", true, 28), null, RequiredAtRuntime: true),
        };
        var route = new PatrolRoute(Guid.Parse("a0656000-0000-4000-8000-000000000020"), 1,
            "runtime:r065", PatrolTraversalPolicy.PingPong,
            waypoints.Select((item, index) => new PatrolPoint(item.Id, item.Position, index,
                index * 90, 2, index == 1 ? "inspect" : null, null, index + 1)).ToArray(),
            requiredAtRuntime: true);
        var locators = new[]
        {
            new GenericLocator(Guid.Parse("a0656000-0000-4000-8000-000000000031"), 1,
                "runtime:r065", "Brasero VFX", new(-2, 0, 2), LocatorPurpose.Vfx,
                ["crypt", "fire"],
                new ArrivalBehavior(ArrivalFacingMode.Direction, 90, null, "ignite", .5,
                    "inspect", "locator.arrived"), "vfx:ember-burst", RequiredAtRuntime: true),
            new GenericLocator(Guid.Parse("a0656000-0000-4000-8000-000000000032"), 1,
                "runtime:r065", "Cloche audio", new(2, 0, 2), LocatorPurpose.Audio,
                ["crypt", "ritual"], null, "audio:crypt-bell"),
            new GenericLocator(Guid.Parse("a0656000-0000-4000-8000-000000000033"), 1,
                "runtime:r065", "Objectif", new(4, 0, 1), LocatorPurpose.Objective,
                ["crypt", "quest"], null),
        };
        var stable = new AnchorCollection(Guid.Parse("a0656000-0000-4000-8000-000000000041"),
            1, "runtime:r065", "crypt", locators.Select(item => item.Id).ToArray(), "Crypte",
            RequiredTags: ["crypt"]);
        var nearest = new AnchorCollection(Guid.Parse("a0656000-0000-4000-8000-000000000042"),
            1, "runtime:r065", "crypt", locators.Select(item => item.Id).ToArray(), "Proche",
            RequiredTags: ["crypt"], Selection: LocatorSelectionMode.Nearest,
            Origin: Vector3Value.Zero, Seed: 2026, RequiredAtRuntime: true);
        var visualization = new RouteVisualizationProfile(
            Guid.Parse("a0656000-0000-4000-8000-000000000050"), 1, "runtime:r065",
            true, true, true, true, true, true, true, 1.25, true);
        var profile = new WaypointAuthoringProfile(
            Guid.Parse("a0656000-0000-4000-8000-000000000060"), 1, "runtime:r065", area.Id,
            true, waypoints, [route], locators, [stable, nearest], visualization);
        var configuredArea = area.WithNavigation(area.Navigation.WithWaypoints(profile));
        var configuredProject = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints, project.Areas.Select(item => item.Id == area.Id ? configuredArea : item).ToArray(),
            project.Dialogues, project.Quests);
        var script = new WaypointAuthoringScriptService(configuredProject, area.Id);
        var arrived = WaypointRuntimeService.Arrive(profile, waypoints[0].Id);
        var denied = WaypointRuntimeService.Arrive(profile, locators[0].Id, authorityGranted: false);
        var resolvedStable = WaypointRuntimeService.ResolveCollection(profile, stable.Id);
        var resolvedNearest = WaypointRuntimeService.ResolveCollection(profile, nearest.Id);
        var resolvedRandom = WaypointRouteService.ResolveCollection(nearest with
        {
            Selection = LocatorSelectionMode.RandomDeterministic,
            Seed = int.MinValue,
        }, locators);
        var known = waypoints.Select(item => item.Id).Concat(locators.Select(item => item.Id)).ToHashSet();
        var segments = WaypointRouteService.AnalyzeRoute(route,
            waypoints.Take(2).Select(item => item.Id).ToHashSet(), known,
            visualization.CostPerMeter);
        BuildWaypointLocatorsAndRouteDebug(profile);
        AssertSmoke(WaypointAuthoringValidator.Validate(configuredArea).Count == 0 &&
                    script.QueryLocators(script.Handle).Count == 3 &&
                    script.QueryCollections(script.Handle).Count == 2 &&
                    arrived.Arrival is
                    {
                        Facing: ArrivalFacingMode.Target, Animation: "inspect",
                        Interaction: "open", Callback: "waypoint.arrived", AuthorityGranted: true
                    } &&
                    denied.Diagnostics.Any(item => item.Code == WaypointRuntimeService.AuthorityDeniedCode) &&
                    resolvedStable.Count == 3 && resolvedNearest.Count == 1 && resolvedRandom.Count == 1 &&
                    segments.Count == 4 && segments.Any(item => item.Direction < 0) &&
                    segments.All(item => item.Cost > 0 && item.TravelSeconds > 0) &&
                    _waypointLocatorRoot?.GetChildCount() == 3 &&
                    _waypointLocatorRoot.GetChildren().OfType<Node3D>().Any(item =>
                        item.GetChildCount() == 1 && item.GetChild(0) is AudioStreamPlayer3D) &&
                    _waypointRouteDebugRoot?.GetChildCount() == 1 &&
                    WaypointRuntimeService.ValidateRuntime(profile).Count == 0,
            "Waypoint arrival, locators, collections, route diagnostics or runtime adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R065_WAYPOINT_LOCATOR_PASS " +
            "arrival=direction-target-animation-wait-interact-callback-authority-engine " +
            "locator=audio-vfx-spawn-objective-gameplay-purpose-tags-engine-nodes " +
            "collection=query-tags-purpose-stable-nearest-random-deterministic " +
            "route=segments-direction-costs-actions-broken-links-nav-projection-timing-engine-overlay " +
            "forgescript=typed-handles authority=server optional=ignored required=diagnostic");
        RunR066GameplaySimulationRuntimeSmokeTest(project);
    }

    private void RunR066GameplaySimulationRuntimeSmokeTest(ProjectDefinition project)
    {
        var source = GameplaySimulationProfile.CreateDefault(project.Module.Id);
        var kinematic = source.Physics.Bodies[0] with
        {
            Id = Guid.Parse("a0666000-0000-4000-8000-000000000021"),
            Name = "Plateforme cinematique",
            Mode = PhysicsBodyMode.Kinematic,
            RequiredAtRuntime = false,
        };
        var trigger = source.Physics.Bodies[0] with
        {
            Id = Guid.Parse("a0666000-0000-4000-8000-000000000022"),
            Name = "Declencheur gameplay",
            Mode = PhysicsBodyMode.Kinematic,
            IsTrigger = true,
            RequiredAtRuntime = false,
        };
        var profile = source with
        {
            Physics = source.Physics with { Bodies = [.. source.Physics.Bodies, kinematic, trigger] },
        };
        BuildGameplaySimulationAdapters(profile);
        var script = new GameplaySimulationScriptService(project.Module.Id, profile);
        var flying = script.PreviewFlight(script.Flight(script.Handle),
            new(new(0, .5f, 0), Vector3Value.Zero), new(1, 1, 0), .2f,
            new(new(-5, 0, -5), new(5, 10, 5), 0));
        var landing = script.PreviewFlight(script.Flight(script.Handle), flying.State, new(0, -1, 0),
            2, new(new(-5, 0, -5), new(5, 10, 5), 0));
        var initialProjectile = new ProjectileRuntimeState(new(Vector3Value.Zero, new(8, 3, 0)),
            0, true, 0);
        var ballistic = GameplaySimulation.StepProjectile(initialProjectile, profile.Projectiles,
            new(5, 0, 0), .1f, true, (_, _, _) => true);
        var homing = GameplaySimulation.StepProjectile(initialProjectile,
            profile.Projectiles with { Mode = ProjectileMode.Homing }, new(5, 0, 0), .1f, true);
        var hitscan = GameplaySimulation.StepProjectile(initialProjectile,
            profile.Projectiles with { Mode = ProjectileMode.Hitscan }, new(5, 0, 0), .1f, true);
        var denied = GameplaySimulation.StepProjectile(initialProjectile, profile.Projectiles,
            new(5, 0, 0), .1f, false);
        var physics = script.StepPhysics(script.Physics(script.Handle), [], .1f, true);
        var target = new TargetCandidate(Guid.Parse("a0666000-0000-4000-8000-000000000030"),
            new(0, 0, -2), ["targetable"], true);
        var previews = Enum.GetValues<TargetingMode>().Select(mode => GameplaySimulation.PreviewTargeting(
            profile.Targeting with { Mode = mode }, Vector3Value.Zero, new(0, 0, -1), [target])).ToArray();
        var worldProfile = profile.WorldTime with
        {
            LocalTimeScales = new Dictionary<string, float> { ["crypt"] = 2 },
            InitialClock = new WorldClockState(350, 1, false, "gameplay", 8),
        };
        var world = GameplaySimulation.AdvanceWorld(worldProfile, worldProfile.InitialClock, 5, "crypt");
        var saved = new RuntimeWorldClockSave(1, world.Clock.TotalMinutes, world.Clock.Paused,
            world.Clock.Revision, "runtime:r066");
        var restored = JsonSerializer.Deserialize<RuntimeWorldClockSave>(JsonSerializer.Serialize(saved));
        var physicsRoot = _gameplaySimulationRoot?.GetNodeOrNull<Node3D>("PhysicsSceneAdapter");
        var children = physicsRoot?.GetChildren() ?? [];

        AssertSmoke(GameplaySimulationProfileValidator.Validate(profile).Count == 0 &&
                    _gameplaySimulationRoot?.GetNodeOrNull<CharacterBody3D>("FlightCharacterController") is not null &&
                    _gameplaySimulationRoot.GetNodeOrNull<Area3D>("ProjectileAdapter") is not null &&
                    children.OfType<RigidBody3D>().Any() && children.OfType<StaticBody3D>().Any() &&
                    children.OfType<AnimatableBody3D>().Any() && children.OfType<Area3D>().Any() &&
                    children.OfType<Joint3D>().Any() &&
                    _gameplaySimulationRoot.GetNodeOrNull<MeshInstance3D>("TargetingPreview") is not null &&
                    _gameplaySimulationRoot.GetNodeOrNull<Node>("WorldTimeAdapter") is not null &&
                    flying.AppliedLift > 0 && landing.Landed && ballistic.Impacted &&
                    homing.State.Motion.Velocity != initialProjectile.Motion.Velocity && hitscan.Impacted &&
                    !denied.AuthorityGranted && physics.AuthorityGranted &&
                    previews.All(item => item.Geometry.Count > 0) && world.DueSchedules.Count == 1 &&
                    restored?.TotalMinutes == 360 && restored.Revision == 9 &&
                    GameplaySimulationProfileValidator.ValidateRuntime(null, false).Count == 0 &&
                    GameplaySimulationProfileValidator.ValidateRuntime(null, true)
                        .Any(item => item.Code == GameplaySimulationProfileValidator.RequiredMissingCode),
            "Gameplay simulation engine adapters, authority, targeting, world save or runtime fallback failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R066_GAMEPLAY_SIMULATION_PASS " +
            "flight=character-body-volumetric-banking-lift-landing " +
            "projectile=ballistic-homing-hitscan-collision-prediction-vfx-authority " +
            "physics=rigid-static-area-joint-character-bounds-network " +
            "targeting=8-modes-engine-preview-validity-input " +
            "world=clock-calendar-local-scales-pause-domains-schedules-save authority=server " +
            "forgescript=typed-handles optional=ignored required=diagnostic");
        RunR067GameplaySessionRuntimeSmokeTest(project);
    }

    private void RunR067GameplaySessionRuntimeSmokeTest(ProjectDefinition project)
    {
        var profile = GameplaySessionProfile.CreateDefault(project.Module.Id);
        BuildGameplaySessionAdapters(profile);
        var script = new GameplaySessionScriptService(project.Module.Id, profile);
        var save = GameplaySessionServices.WriteSave(profile.Persistence,
            new SaveWriteContext("host", SaveAuthority.Host, false, true), "game:crypt", 4,
            DateTimeOffset.UnixEpoch, new Dictionary<string, string> { ["quest"] = "active" });
        var saveRoundTrip = JsonSerializer.Deserialize<SaveEnvelope>(JsonSerializer.Serialize(save));
        var checkpoint = profile.Checkpoints.Single();
        var respawn = script.Respawn(script.Checkpoints(script.Handle).Single(),
            new RespawnRequest("hero", checkpoint.WaypointId, 10, true, ["hero", "companion"], true), true);
        var deniedRespawn = script.Respawn(script.Checkpoints(script.Handle).Single(),
            new RespawnRequest("hero", checkpoint.WaypointId, 10, true, ["hero"], true), false);
        var shell = script.ReadShell(script.Shell(script.Handle));
        var exploration = profile.ControlLocks.Contexts.Single(item => item.OwnerId == "exploration");
        var dialogue = profile.ControlLocks.Contexts.Single(item => item.OwnerId == "dialogue");
        var cinematic = profile.ControlLocks.Contexts.Single(item => item.OwnerId == "cinematic");
        var stack = GameplaySessionServices.PushControl(
            GameplaySessionServices.PushControl(
                GameplaySessionServices.PushControl([], exploration), dialogue), cinematic);
        var restored = GameplaySessionServices.ReleaseOwner(stack, "cinematic");
        var expected = GameplaySessionServices.HashState("hero:x=1");
        var recording = GameplaySessionServices.Record(profile.Replay,
            Guid.Parse("a0676000-0000-4000-8000-000000000010"),
            [new ReplayFrame(1, "right", "moved", 42, expected)], "runtime:r067");
        var replay = script.ReplayHeadless(script.Replay(script.Handle), recording, _ => "hero:x=2", true);
        var checkpointRoot = _gameplaySessionRoot?.GetNodeOrNull<Node3D>("CheckpointAdapters");
        var shellLayer = _gameplaySessionRoot?.GetNodeOrNull<CanvasLayer>("RuntimeShellAdapter");
        var shellPanels = shellLayer?.GetNodeOrNull<Control>("ShellPanels");

        AssertSmoke(GameplaySessionProfileValidator.Validate(profile).Count == 0 &&
                    _gameplaySessionRoot?.GetNodeOrNull<Node>("PersistenceAdapter") is not null &&
                    checkpointRoot?.GetChildCount() == 1 && checkpointRoot.GetChild(0) is Area3D &&
                    shellPanels?.GetNodeOrNull<Control>("MapPanel") is { Visible: true } &&
                    shellPanels.GetNodeOrNull<Control>("JournalPanel") is { Visible: true } &&
                    shellPanels.GetNodeOrNull<Control>("HudPanel") is { Visible: true } &&
                    _gameplaySessionRoot.GetNodeOrNull<Node>("ControlContextStackAdapter")?.GetChildCount() == 3 &&
                    _gameplaySessionRoot.GetNodeOrNull<Node>("ReplayHeadlessAdapter") is not null &&
                    saveRoundTrip?.Revision == 4 && saveRoundTrip.Profile.Scope == PersistenceScope.Game &&
                    respawn.Succeeded && respawn.RespawnActorIds.Count == 2 &&
                    deniedRespawn.Code == "GAME_RESPAWN_AUTHORITY_REQUIRED" &&
                    shell.VisiblePanels.Count == 3 && shell.InputAgnostic &&
                    restored.Restored?.OwnerId == "dialogue" && !replay.Succeeded &&
                    replay.Divergence?.Tick == 1 && replay.MinimalPackageManifest?.Contains(
                        "privacy=redacted", StringComparison.Ordinal) == true &&
                    GameplaySessionProfileValidator.ValidateRuntime(null, false).Count == 0 &&
                    GameplaySessionProfileValidator.ValidateRuntime(null, true)
                        .Any(item => item.Code == GameplaySessionProfileValidator.RequiredMissingCode),
            "Gameplay session persistence, checkpoint, shell, control stack, replay or engine adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R067_GAMEPLAY_SESSION_PASS " +
            "save=player-game-server-authority-conflict-revision-cloud-privacy-engine " +
            "checkpoint=waypoint-cost-party-corpse-safe-spawn-hooks-area3d-authority " +
            "shell=kit-data-driven-skinnable-scalable-input-agnostic-map-journal-hud-canvas " +
            "control=owner-cursor-camera-actions-stack-restoration-engine " +
            "replay=tick-input-event-seed-hash-headless-divergence-minimal-redacted-package " +
            "forgescript=typed-handles authority=server optional=ignored required=diagnostic");
        RunR068DialogueAuthoringRuntimeSmokeTest(project);
    }

    private void RunR068DialogueAuthoringRuntimeSmokeTest(ProjectDefinition project)
    {
        var dialogue = CreateR068Dialogue("runtime:r068");
        BuildDialogueAuthoringAdapters([dialogue]);
        var evaluator = new R068DialogueConditionEvaluator();
        var context = new DialogueAuthoringContext(
            new Dictionary<string, object> { ["player.name"] = "<Elyra>" },
            new Dictionary<string, string>
            {
                ["role:narrator"] = "entity:narrator",
                ["query:role=guide"] = "entity:guide",
                ["tag:dialogue-companion"] = "entity:companion",
                ["entity:entity:merchant"] = "entity:merchant",
            }, 42);
        RenderedDialogueAuthoringNode Render(DialogueChoiceEvaluationMode mode, int seed = 42,
            string speakerRole = "narrator")
        {
            var profile = dialogue.Authoring! with
            {
                Nodes = dialogue.Authoring.Nodes.Select(item => item.NodeId == dialogue.EntryNodeId
                    ? item with { EvaluationMode = mode, SpeakerRole = speakerRole }
                    : item).ToArray(),
            };
            return DialogueAuthoringRuntimeService.Render(dialogue.WithAuthoring(profile), dialogue.EntryNodeId,
                context with { DeterministicSeed = seed }, evaluator);
        }

        var weighted = Render(DialogueChoiceEvaluationMode.Weighted);
        var randomA = Render(DialogueChoiceEvaluationMode.RandomDeterministic, 19);
        var randomB = Render(DialogueChoiceEvaluationMode.RandomDeterministic, 19);
        var fallbackContext = context with
        {
            ResolvedParticipants = new Dictionary<string, string> { ["role:narrator"] = "entity:fallback" },
        };
        var fallbackProfile = dialogue.Authoring! with
        {
            Nodes = dialogue.Authoring.Nodes.Select(item => item.NodeId == dialogue.EntryNodeId
                ? item with { SpeakerRole = "guide" }
                : item).ToArray(),
        };
        var fallback = DialogueAuthoringRuntimeService.Render(dialogue.WithAuthoring(fallbackProfile),
            dialogue.EntryNodeId, fallbackContext, evaluator);
        var script = new DialogueGraphScriptService(dialogue);
        var snapshot = script.ReadAuthoring(script.Handle);
        var optional = CreateR068Dialogue("runtime:r068", withAuthoring: false);
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["authoring.required"] = "true" }, optional.Participants,
            optional.EntryPoints, optional.Edges);
        var updatedProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas, [.. project.Dialogues.Where(item => item.Id != dialogue.Id), dialogue], project.Quests);
        var json = ProjectJsonSerializer.Serialize(updatedProject);
        var restored = ProjectJsonSerializer.Deserialize(json).Dialogues.Single(item => item.Id == dialogue.Id);
        var dialogueAdapter = _dialogueAuthoringRoot?.GetNodeOrNull<Node>($"Dialogue_{dialogue.Id.Value:N}");
        var participants = dialogueAdapter?.GetNodeOrNull<Node>("ParticipantBindings");
        var nodes = dialogueAdapter?.GetNodeOrNull<Node>("NodeAuthoring");

        AssertSmoke(DialogueValidator.Validate(dialogue).Count == 0 &&
                    weighted.Text.Contains("&lt;Elyra&gt;", StringComparison.Ordinal) &&
                    weighted.AvailableChoices.Count == 1 && weighted.Diagnostics.Count == 0 &&
                    Render(DialogueChoiceEvaluationMode.FirstValid).AvailableChoices.Count == 1 &&
                    Render(DialogueChoiceEvaluationMode.AllValid).AvailableChoices.Count == 4 &&
                    Render(DialogueChoiceEvaluationMode.Manual).AvailableChoices.Count == 4 &&
                    randomA.AvailableChoices.Single().Id == randomB.AvailableChoices.Single().Id &&
                    Render(DialogueChoiceEvaluationMode.FirstValid, speakerRole: "guide").SpeakerEntityId == "entity:guide" &&
                    Render(DialogueChoiceEvaluationMode.FirstValid, speakerRole: "companion").SpeakerEntityId == "entity:companion" &&
                    Render(DialogueChoiceEvaluationMode.FirstValid, speakerRole: "merchant").SpeakerEntityId == "entity:merchant" &&
                    fallback.SpeakerEntityId == "entity:fallback" &&
                    DialogueAuthoringRuntimeService.CompleteTokens(dialogue, "player").Single() == "player.name" &&
                    snapshot.Enabled && snapshot.RequiredAtRuntime && snapshot.ParticipantCount == 4 &&
                    snapshot.NodePolicyCount == 5 && snapshot.TokenCount == 1 && snapshot.WizardEntryCount == 2 &&
                    DialogueAuthoringProfileValidator.Validate(optional).Count == 0 &&
                    DialogueAuthoringProfileValidator.Validate(required).Any(item =>
                        item.Code == DialogueAuthoringProfileValidator.RequiredMissingCode) &&
                    restored.Authoring?.Nodes.Count == 5 && restored.Authoring.Participants.Count == 4 &&
                    participants?.GetChildCount() == 4 && nodes?.GetChildCount() == 5 &&
                    nodes.GetChildren().All(item => item.HasMeta("evaluation_mode")),
            "Dialogue authoring tokens, participants, evaluation, wizard, notes, persistence or engine nodes failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R068_DIALOGUE_AUTHORING_PASS " +
            "tokens=typed-autocomplete-preview-escaped participants=role-query-tag-entity-node-speaker-fallback " +
            "evaluation=first-all-weighted-random-deterministic-manual wizard=visual-typed-source " +
            "notes=intent-emotion-source-status-todo-author forgescript=typed-handles engine=nodes " +
            "persistence=roundtrip optional=ignored required=diagnostic");
        RunR069DialogueExperienceRuntimeSmokeTest(project);
    }

    private void RunR069DialogueExperienceRuntimeSmokeTest(ProjectDefinition project)
    {
        var dialogue = CreateR069Dialogue("runtime:r069");
        BuildDialogueExperienceAdapters([dialogue]);
        var profile = dialogue.Experience!;
        var script = new DialogueGraphScriptService(dialogue);
        var snapshot = script.ReadExperience(script.Handle);
        var completed = DialogueExperienceRuntimeService.Complete(dialogue, "player", DateTimeOffset.UnixEpoch);
        var memory = DialogueExperienceRuntimeService.Write(profile, DialogueMemoryState.Empty,
            profile.MemorySlots.Single().Id, "4");
        var aborted = DialogueExperienceRuntimeService.Abort(dialogue, "player", dialogue.EntryNodeId,
            DialogueAbortReason.CombatStarted, memory, 42, DateTimeOffset.UnixEpoch);
        var hits = DialogueExperienceRuntimeService.Search([dialogue], profile.Search);
        var preview = DialogueExperienceRuntimeService.PreviewReplace([dialogue], profile.Search);
        var review = DialogueExperienceRuntimeService.Review([dialogue], profile.Review);
        var check = DialogueExperienceRuntimeService.ResolveSkillCheck(profile.SkillChecks.Single(), 5, 2, 42);
        var repeated = DialogueExperienceRuntimeService.ResolveSkillCheck(profile.SkillChecks.Single(), 5, 2, 42);
        var optional = CreateR069Dialogue("runtime:r069", withExperience: false);
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["kind"] = "conversation", ["experience.required"] = "true" },
            optional.Participants, optional.EntryPoints, optional.Edges, optional.Authoring);
        var updatedProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas, [.. project.Dialogues.Where(item => item.Id != dialogue.Id), dialogue], project.Quests);
        var json = ProjectJsonSerializer.Serialize(updatedProject);
        var restored = ProjectJsonSerializer.Deserialize(json).Dialogues.Single(item => item.Id == dialogue.Id);
        var dialogueAdapter = _dialogueExperienceRoot?.GetNodeOrNull<Node>($"Dialogue_{dialogue.Id.Value:N}");
        var lifecycle = dialogueAdapter?.GetNodeOrNull<Node>("LifecycleAdapter");
        var searchReview = dialogueAdapter?.GetNodeOrNull<Node>("SearchReviewAdapter");
        var memoryNodes = dialogueAdapter?.GetNodeOrNull<Node>("MemorySlots");
        var checkNodes = dialogueAdapter?.GetNodeOrNull<Node>("SkillChecks");

        AssertSmoke(DialogueValidator.Validate(dialogue).Count == 0 &&
                    completed.Completed && completed.Actions.Count == 1 && !aborted.Completed &&
                    aborted.AbortReason == DialogueAbortReason.CombatStarted && aborted.RolledBack &&
                    aborted.ResumeState?.Seed == 42 && hits.Count >= 1 && preview.Replacements.Count >= 1 &&
                    preview.SemanticDiff.Contains("preview-only:true", StringComparison.Ordinal) &&
                    review.Count >= 1 && profile.Review.Badges.Count == 6 &&
                    DialogueExperienceRuntimeService.Read(profile, memory, profile.MemorySlots.Single().Id) == "4" &&
                    check == repeated && !string.IsNullOrWhiteSpace(check.Explanation) &&
                    snapshot.Enabled && snapshot.RequiredAtRuntime && snapshot.MemorySlotCount == 1 &&
                    snapshot.SkillCheckCount == 1 && script.ReadSkillCheck(script.Handle, dialogue.EntryNodeId) is not null &&
                    DialogueExperienceProfileValidator.Validate(optional).Count == 0 &&
                    DialogueExperienceProfileValidator.Validate(required).Any(item =>
                        item.Code == DialogueExperienceProfileValidator.RequiredMissingCode) &&
                    restored.Experience?.Lifecycle.RollbackOnAbort == true &&
                    restored.Experience.MemorySlots.Count == 1 && restored.Experience.SkillChecks.Count == 1 &&
                    lifecycle is not null && lifecycle.GetMeta("rollback_on_abort").AsBool() &&
                    searchReview is not null && searchReview.HasMeta("badges") &&
                    memoryNodes?.GetChildCount() == 1 && checkNodes?.GetChildCount() == 1 &&
                    checkNodes.GetChild(0).HasMeta("difficulty"),
            "Dialogue lifecycle, search/review, memory, skill check, persistence or engine nodes failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R069_DIALOGUE_EXPERIENCE_PASS " +
            "lifecycle=completed-aborted-reason-rollback-resume-actions " +
            "search=multi-dialogue-safe-regex-speaker-condition-action-preview-only " +
            "review=condition-action-voice-localization-error-link-filter " +
            "memory=dialogue-participant-player-party-world-explicit-duration-typed-engine " +
            "skill=difficulty-opponent-advantage-result-visibility-explanation-deterministic-engine " +
            "forgescript=typed-handles persistence=roundtrip optional=ignored required=diagnostic");
        RunR070DialogueProductionRuntimeSmokeTest(project);
    }

    private void RunR070DialogueProductionRuntimeSmokeTest(ProjectDefinition project)
    {
        var dialogue = CreateR070Dialogue("runtime:r070");
        BuildDialogueProductionAdapters([dialogue]);
        var profile = dialogue.Production!;
        var script = new DialogueGraphScriptService(dialogue);
        var snapshot = script.ReadProduction(script.Handle);
        var contract = profile.Subgraphs.Single();
        var isolated = DialogueProductionRuntimeService.TestSubgraph(profile, contract.Id,
            contract.Tests.Single().Id);
        var impacts = DialogueProductionRuntimeService.AnalyzeImpact(profile,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["quest:crypt-of-ashes"] = ["journal:crypt", "ending:embers"],
            });
        var coverage = DialogueProductionRuntimeService.MeasureCoverage(dialogue);
        var conflict = DialogueProductionRuntimeService.ResolveConflict(profile.Collaboration.Conflicts.Single());
        var aiPreview = DialogueProductionRuntimeService.PreviewAi(profile.AiPlans.Single());
        var aiApplied = DialogueProductionRuntimeService.ApplyAi(profile, aiPreview.PlanId);
        var nodeLock = profile.Collaboration.Locks.Single();
        var optional = CreateR070Dialogue("runtime:r070", withProduction: false);
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["kind"] = "conversation", ["production.required"] = "true" },
            optional.Participants, optional.EntryPoints, optional.Edges, optional.Authoring, optional.Experience);
        var updatedProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas, [.. project.Dialogues.Where(item => item.Id != dialogue.Id), dialogue], project.Quests);
        var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(updatedProject)).Dialogues
            .Single(item => item.Id == dialogue.Id);
        var dialogueAdapter = _dialogueProductionRoot?.GetNodeOrNull<Node>($"Dialogue_{dialogue.Id.Value:N}");
        var subgraphs = dialogueAdapter?.GetNodeOrNull<Node>("SubgraphContracts");
        var consequences = dialogueAdapter?.GetNodeOrNull<Node>("TypedConsequences");
        var coverageNode = dialogueAdapter?.GetNodeOrNull<Node>("Coverage");
        var collaboration = dialogueAdapter?.GetNodeOrNull<Node>("Collaboration");
        var ai = dialogueAdapter?.GetNodeOrNull<Node>("ControlledAiPlans");

        AssertSmoke(DialogueValidator.Validate(dialogue).Count == 0 && profile.RequiredAtRuntime &&
                    isolated.Passed && impacts.Count == 4 &&
                    impacts.Single(item => item.Consequence.Kind == DialogueConsequenceTargetKind.Quest)
                        .AffectedResources.Count == 2 && coverage.Paths.Count == 2 &&
                    coverage.CoveredNodes.Count == 2 && conflict.CanApply && aiPreview.Lint.Count == 0 &&
                    aiApplied.AiPlans.Single().Applied &&
                    DialogueProductionRuntimeService.CanEdit(nodeLock, "author", DateTimeOffset.UnixEpoch) &&
                    !DialogueProductionRuntimeService.CanEdit(nodeLock, "reviewer", DateTimeOffset.UnixEpoch) &&
                    snapshot.Enabled && snapshot.RequiredAtRuntime && snapshot.SubgraphCount == 1 &&
                    snapshot.ConsequenceCount == 4 && script.ReadSubgraph(script.Handle, "embers") is not null &&
                    script.ReadConsequences(script.Handle, dialogue.EntryNodeId).Count == 4 &&
                    DialogueProductionProfileValidator.Validate(optional).Count == 0 &&
                    DialogueProductionProfileValidator.Validate(required).Any(item =>
                        item.Code == DialogueProductionProfileValidator.RequiredMissingCode) &&
                    restored.Production?.Subgraphs.Single().Version == 2 &&
                    restored.Production.Consequences.Count == 4 && restored.Production.AiPlans.Count == 1 &&
                    subgraphs?.GetChildCount() == 1 && consequences?.GetChildCount() == 4 &&
                    coverageNode?.GetMeta("covered_nodes").AsInt32() == 2 &&
                    collaboration?.GetMeta("comments").AsInt32() == 1 && ai?.GetChildCount() == 1 &&
                    ai.GetChild(0).GetMeta("preview_only").AsBool(),
            "Dialogue subgraph, impact, coverage, collaboration, controlled AI, persistence or engine nodes failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R070_DIALOGUE_PRODUCTION_PASS " +
            "subgraph=versioned-input-output-variable-contract-isolated-test-engine " +
            "consequences=quest-faction-item-world-impact-engine coverage=paths-required-state-impossible-test-session-ratio-engine " +
            "collaboration=canonical-node-json-stable-id-revision-lock-comment-semantic-diff-conflict-resolution-engine " +
            "ai=brief-citations-style-preview-lint-authorization-plan-transaction-semantic-diff-explicit-apply-engine " +
            "forgescript=typed-handles persistence=roundtrip optional=ignored required=diagnostic");
    }

    private static DialogueDefinition CreateR070Dialogue(string provenance, bool withProduction = true)
    {
        var entry = DialogueNodeId.Parse("d0706000-0000-4000-8000-000000000001");
        var subgraph = DialogueNodeId.Parse("d0706000-0000-4000-8000-000000000002");
        var end = DialogueNodeId.Parse("d0706000-0000-4000-8000-000000000003");
        var dialogue = new DialogueDefinition(DialogueId.Parse("d0706000-0000-4000-8000-000000000010"),
            "r070-runtime", "R070 runtime", entry,
        [
            new(entry, null, new("dialogue.r070.entry", "Choose an ember path."), choices:
            [
                new(DialogueChoiceId.Parse("d0706000-0000-4000-8000-000000000011"),
                    new("dialogue.r070.subgraph", "Enter the ember memory"), targetNodeId: subgraph),
                new(DialogueChoiceId.Parse("d0706000-0000-4000-8000-000000000012"),
                    new("dialogue.r070.end", "Leave"), targetNodeId: end),
            ], role: DialogueNodeRole.PlayerChoice),
            new(subgraph, null, new("dialogue.r070.embers", "The embers remember."),
                role: DialogueNodeRole.Subgraph, subgraphKey: "embers"),
            new(end, null, new("dialogue.r070.done", "Done."), role: DialogueNodeRole.End),
        ], provenance: provenance);
        if (!withProduction) return dialogue;
        var contractId = Guid.Parse("d0706000-0000-4000-8000-000000000101");
        var input = new DialogueSubgraphParameter(Guid.Parse("d0706000-0000-4000-8000-000000000102"), 1,
            provenance, "trust", DialogueTextTokenType.Integer, DialogueSubgraphParameterDirection.Input, true);
        var output = new DialogueSubgraphParameter(Guid.Parse("d0706000-0000-4000-8000-000000000103"), 1,
            provenance, "accepted", DialogueTextTokenType.Boolean, DialogueSubgraphParameterDirection.Output, true);
        var test = new DialogueSubgraphTestCase(Guid.Parse("d0706000-0000-4000-8000-000000000104"), 1,
            provenance, "trust accepted", new Dictionary<string, string> { ["trust"] = "4" },
            new Dictionary<string, string> { ["accepted"] = "true" });
        var contract = new DialogueSubgraphContract(contractId, 1, provenance, "embers", 2, [input, output],
            ["party:trust"], [test]);
        var invocation = new DialogueSubgraphInvocation(Guid.Parse("d0706000-0000-4000-8000-000000000105"),
            1, provenance, subgraph, contractId,
            new Dictionary<string, string> { ["trust"] = "party:trust" },
            new Dictionary<string, string> { ["accepted"] = "memory:accepted" });
        var consequences = Enum.GetValues<DialogueConsequenceTargetKind>().Select((kind, index) =>
            new DialogueTypedConsequence(Guid.Parse($"d0706000-0000-4000-8000-0000000002{index:00}"), 1,
                provenance, entry, kind, kind switch
                {
                    DialogueConsequenceTargetKind.Quest => "quest:crypt-of-ashes",
                    DialogueConsequenceTargetKind.Faction => "faction:ash-wardens",
                    DialogueConsequenceTargetKind.Item => "item:ember-key",
                    _ => "world:crypt-awakened",
                }, kind == DialogueConsequenceTargetKind.Faction ? "reputation" : "set", "+1",
                $"Impact {kind}")).ToArray();
        var coverage = new DialogueCoveragePolicy(Guid.Parse("d0706000-0000-4000-8000-000000000301"), 1,
            provenance, 64, new Dictionary<string, string> { ["quest:crypt-of-ashes"] = "active" },
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000302"), 1, provenance, "session:embers",
                DialogueCoverageEvidenceKind.Session, [entry, subgraph],
                new Dictionary<string, string> { ["quest:crypt-of-ashes"] = "active" })]);
        var now = DateTimeOffset.UnixEpoch;
        var collaboration = new DialogueCollaborationProfile(
            Guid.Parse("d0706000-0000-4000-8000-000000000401"), 1, provenance,
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000402"), 1, provenance, subgraph, 2,
                "author", "{\"text\":\"ours\"}", now)],
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000403"), 1, provenance, subgraph, "author",
                now.AddMinutes(5))],
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000404"), 1, provenance, subgraph, "reviewer",
                "Clarify impact", false, now)],
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000405"), 1, provenance, subgraph, "text",
                "{\"text\":\"base\"}", "{\"text\":\"ours\"}", "{\"text\":\"theirs\"}",
                DialogueConflictResolution.Manual, "{\"text\":\"merged\"}", true)]);
        var ai = new DialogueAiPlan(Guid.Parse("d0706000-0000-4000-8000-000000000501"), 1, provenance,
            Guid.Parse("d0706000-0000-4000-8000-000000000502"),
            Guid.Parse("d0706000-0000-4000-8000-000000000503"), DialogueAiAuthorizationLevel.AuthorApply,
            "A promise beneath the crypt", ["offer", "choice"], ["style:embers"], ["context#dialogue"],
            "add-subgraph:embers", [], true, false);
        return dialogue.WithProduction(new DialogueProductionProfile(
            Guid.Parse("d0706000-0000-4000-8000-000000000601"), 1, provenance, true, true,
            [contract], [invocation], consequences, coverage, collaboration, [ai]));
    }

    private static DialogueDefinition CreateR068Dialogue(string provenance, bool withAuthoring = true)
    {
        var entry = DialogueNodeId.Parse("d0686000-0000-4000-8000-000000000001");
        var targets = Enumerable.Range(2, 4).Select(index =>
            DialogueNodeId.Parse($"d0686000-0000-4000-8000-00000000000{index}")).ToArray();
        var condition = new DialogueCondition(Guid.Parse("d0686000-0000-4000-8000-000000000020"), 1,
            provenance, DialogueConditionKind.HasItem, "ash-key", "true", false, "on-choice-visible", 1);
        var action = new DialogueAction(Guid.Parse("d0686000-0000-4000-8000-000000000021"), 1,
            provenance, DialogueActionKind.StartQuest, "crypt-of-ashes", "active", true);
        var choices = targets.Select((target, index) => new DialogueChoice(
            DialogueChoiceId.Parse($"d0686000-0000-4000-8000-00000000003{index}"),
            new LocalizedText($"dialogue.r068.choice.{index}", $"Choice {index + 1}"),
            index == 0 ? [condition] : [], [], target, priority: index)).ToArray();
        var dialogue = new DialogueDefinition(DialogueId.Parse("d0686000-0000-4000-8000-000000000010"),
            "r068-runtime", "R068 runtime", entry,
            [
                new(entry, null, new("dialogue.r068.entry", "Welcome, {{player.name}}."), [action], choices,
                    DialogueNodeRole.PlayerChoice),
                .. targets.Select((target, index) => new DialogueNode(target, null,
                    new LocalizedText($"dialogue.r068.end.{index}", $"End {index + 1}"), role: DialogueNodeRole.End)),
            ], provenance: provenance);
        if (!withAuthoring) return dialogue;
        var profile = DialogueAuthoringProfile.CreateDefault(dialogue, provenance);
        var participantRoles = new[] { "narrator", "guide", "companion", "merchant" };
        var kinds = Enum.GetValues<DialogueParticipantBindingKind>();
        var participants = participantRoles.Select((role, index) => new DialogueParticipantBinding(
            Guid.Parse($"d0686000-0000-4000-8000-0000000001{index:00}"), 1, provenance, role, kinds[index],
            kinds[index] switch
            {
                DialogueParticipantBindingKind.Query => "role=guide",
                DialogueParticipantBindingKind.Tag => "dialogue-companion",
                DialogueParticipantBindingKind.Entity => "entity:merchant",
                _ => "narrator",
            }, index == 0 ? null : "narrator")).ToArray();
        var modes = new[]
        {
            DialogueChoiceEvaluationMode.Weighted,
            DialogueChoiceEvaluationMode.FirstValid,
            DialogueChoiceEvaluationMode.AllValid,
            DialogueChoiceEvaluationMode.RandomDeterministic,
            DialogueChoiceEvaluationMode.Manual,
        };
        var wizard = new[]
        {
            new DialogueWizardEntry(Guid.Parse("d0686000-0000-4000-8000-000000000201"), 1, provenance,
                DialogueWizardEntryKind.Condition, condition.Id,
                "builder:condition:HasItem:ash-key:true:false"),
            new DialogueWizardEntry(Guid.Parse("d0686000-0000-4000-8000-000000000202"), 1, provenance,
                DialogueWizardEntryKind.Action, action.Id,
                "builder:action:StartQuest:crypt-of-ashes:active:true"),
        };
        profile = profile with
        {
            RequiredAtRuntime = true,
            Participants = participants,
            Nodes = profile.Nodes.Select((item, index) => item with
            {
                SpeakerRole = participantRoles[index % participantRoles.Length],
                EvaluationMode = modes[index],
                Tokens = index == 0
                    ? [new DialogueTextToken(Guid.Parse("d0686000-0000-4000-8000-000000000203"), 1,
                        provenance, "player.name", DialogueTextTokenType.EntityName, true, null)]
                    : [],
                ChoiceEvaluations = item.ChoiceEvaluations.Select((value, choiceIndex) => value with
                { Weight = choiceIndex + 1 }).ToArray(),
                WizardEntries = index == 0 ? wizard : [],
                Notes = item.Notes with
                {
                    Intent = $"Validate authoring node {index + 1}.",
                    Emotion = index == 0 ? "mysterious" : "neutral",
                    Source = "acceptance:r068",
                    Status = DialogueNodeEditorialStatus.Review,
                    Todos = ["Review voice"],
                    AuthorMention = "@karen",
                },
            }).ToArray(),
        };
        return dialogue.WithAuthoring(profile);
    }

    private static DialogueDefinition CreateR069Dialogue(string provenance, bool withExperience = true)
    {
        var entry = DialogueNodeId.Parse("d0696000-0000-4000-8000-000000000001");
        var success = DialogueNodeId.Parse("d0696000-0000-4000-8000-000000000002");
        var failure = DialogueNodeId.Parse("d0696000-0000-4000-8000-000000000003");
        var dialogue = new DialogueDefinition(DialogueId.Parse("d0696000-0000-4000-8000-000000000010"),
            "r069-runtime", "R069 runtime", entry,
            [
                new(entry, null, new LocalizedText("dialogue.r069.entry", "La braise répond dans les cendres.",
                        [LocalizedTextVariant.Create("dialogue.r069.entry", "en", "The ember answers in the ashes.")],
                        provenance),
                    [new DialogueAction(DialogueActionKind.PlayAudio, "elyra.voice", "play")],
                    [
                        new(DialogueChoiceId.Parse("d0696000-0000-4000-8000-000000000020"),
                            new("dialogue.r069.success", "Convaincre Elyra"),
                            [new(DialogueConditionKind.HasItem, "ash-key", "true")],
                            [new(DialogueActionKind.StartQuest, "crypt-of-ashes", "active")], success),
                        new(DialogueChoiceId.Parse("d0696000-0000-4000-8000-000000000021"),
                            new("dialogue.r069.failure", "Renoncer"), targetNodeId: failure),
                    ], DialogueNodeRole.PlayerChoice),
                new(success, null, new("dialogue.r069.end.success", "La gardienne accepte."),
                    role: DialogueNodeRole.End),
                new(failure, null, new("dialogue.r069.end.failure", "La conversation prend fin."),
                    role: DialogueNodeRole.End),
            ], provenance: provenance);
        if (!withExperience) return dialogue;
        var profile = DialogueExperienceProfile.CreateDefault(dialogue, provenance);
        profile = profile with
        {
            RequiredAtRuntime = true,
            Lifecycle = profile.Lifecycle with
            {
                CompletedActions = [new(DialogueActionKind.SetVariable, "dialogue.completed", "true")],
                AbortedActions = [new(DialogueActionKind.RunScript, "dialogue-aborted.forge", "reason")],
                RollbackOnAbort = true,
                CreateResumeState = true,
            },
            Search = profile.Search with
            {
                Pattern = "braise|cendres",
                UseRegex = true,
                SpeakerRoles = [],
                ConditionKinds = [],
                ActionKinds = [],
                ReplacementPreview = "flamme",
            },
            Review = profile.Review with { ReviewMode = true },
            MemorySlots =
            [new(Guid.Parse("d0696000-0000-4000-8000-000000000101"), 1, provenance,
                DialogueMemoryScope.Player, "player:current", "trust", DialogueTextTokenType.Integer,
                DialogueMemoryDuration.Campaign, "0")],
            SkillChecks =
            [new(Guid.Parse("d0696000-0000-4000-8000-000000000102"), 1, provenance, entry,
                "persuasion", 12, "elyra", true, DialogueSkillCheckVisibility.FullExplanation,
                success, failure)],
        };
        return dialogue.WithExperience(profile);
    }


}

using System.Diagnostics;
using System.Globalization;
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
    private async Task CaptureVisualProofAsync(string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || !string.Equals(Path.GetExtension(destination), ".png",
            StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The visual proof destination must be an absolute PNG path.", nameof(destination));

        // Product launches are fullscreen by default, but verification captures must stay reproducible
        // across monitors and local/CI runners.
        var (width, height) = VisualRegressionProfile.ParseResolution(VisualCaptureResolution);
        var window = GetWindow();
        window.Mode = Window.ModeEnum.Windowed;
        window.Borderless = true;
        window.Size = new Vector2I(width, height);
        window.Position = Vector2I.Zero;
        for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        if (GetViewport().UseHdr2D && image.GetFormat() is Image.Format.Rgb8 or Image.Format.Rgba8)
            image.LinearToSrgb();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var error = image.SavePng(destination);
        if (error != Error.Ok) throw new IOException($"Visual proof could not be written: {error}.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11P_VISUAL_PASS file={Path.GetFileName(destination)} " +
            $"size={image.GetWidth()}x{image.GetHeight()} nodes={CountTreeNodes(this)} nui=2.0 aoe=visible");
        GetTree().Quit(0);
    }



    private async Task CaptureModuleMapVisualProofAsync(string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || !string.Equals(Path.GetExtension(destination), ".png",
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The module map visual proof destination must be an absolute PNG path.",
                nameof(destination));
        if (_moduleMap is null || _activeArea is null)
            throw new InvalidOperationException("The module map visual proof requires an active runtime map.");

        _moduleMap.SetGlobalMapVisible(true);
        await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
        for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (image.SavePng(destination) != Error.Ok)
            throw new IOException("The module map visual proof could not be written.");
        GD.Print($"OPENRPG_FORGE_MODULE_MAP_VISUAL_PASS file={Path.GetFileName(destination)} " +
                 $"size={image.GetWidth()}x{image.GetHeight()} area={_activeArea.Slug} " +
                 $"areas={_moduleMap.AreaCount} minimap={_moduleMap.MiniMapVisible.ToString().ToLowerInvariant()} " +
                 $"global={_moduleMap.GlobalMapVisible.ToString().ToLowerInvariant()} " +
                 $"floor={_moduleMap.DisplayedFloorText} markers={_moduleMap.GlobalRenderedMarkerCount} " +
                 $"label_overlaps={_moduleMap.GlobalMapLabelOverlapCount}");
        GetTree().Quit(0);
    }

    private async Task CaptureRpgMenuVisualProofAsync(string destination, RuntimeRpgMenuPage page)
    {
        ValidateUiCaptureDestination(destination, "RPG menu");
        if (_rpgMenu is null)
            throw new InvalidOperationException("The RPG menu visual proof requires the player codex.");
        if (page == RuntimeRpgMenuPage.Journal &&
            _runtimePresentation?.Journal.ActiveEntry is null &&
            _npc is not null && _narrativeParticipants.TryGetValue(_npc, out var participant))
        {
            var resolution = RuntimeNarrativeResolver.ResolveDialogue(_loadedProject!, participant.BlueprintId);
            var startsQuest = resolution?.Dialogue.Nodes.SelectMany(node => node.OnEnterActions
                    .Concat(node.Choices.SelectMany(choice => choice.Actions)))
                .Any(action => action.Kind == DialogueActionKind.StartQuest) == true;
            if (startsQuest && TryActivateNarrativeDialogue(_npc, participant))
            {
                for (var step = 0;
                     step < resolution!.Dialogue.Nodes.Count &&
                     _runtimePresentation?.Journal.ActiveEntry is null;
                     step++)
                {
                    InteractWithGuide();
                    CommitDialogueChoice(0);
                    UpdateHud();
                }
                _dialogueOverlay?.Close();
                SyncPlayerInterfaceLock();
                UpdateHud();
            }
        }
        _moduleMap?.SetGlobalMapVisible(false);
        _rpgMenu.ShowPage(page, focus: false);
        SyncPlayerInterfaceLock();
        for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        string? journalTitle = null;
        string? journalObjective = null;
        if (page == RuntimeRpgMenuPage.Journal)
        {
            journalTitle = (_rpgMenu.FindChild("JournalQuestTitle", recursive: true, owned: false) as Label)?.Text;
            journalObjective = (_rpgMenu.FindChild("JournalActiveObjective", recursive: true, owned: false) as Label)?.Text;
            if (string.IsNullOrWhiteSpace(journalTitle) || string.IsNullOrWhiteSpace(journalObjective) ||
                string.Equals(journalTitle, journalObjective, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The journal capture requires distinct non-empty title and active-objective controls.");
        }
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (image.SavePng(destination) != Error.Ok)
            throw new IOException("The RPG menu visual proof could not be written.");
        GD.Print($"OPENRPG_FORGE_RPG_MENU_VISUAL_PASS file={Path.GetFileName(destination)} " +
                 $"size={image.GetWidth()}x{image.GetHeight()} page={page.ToString().ToLowerInvariant()} " +
                 $"journal_title={journalTitle ?? "none"} active_objective={journalObjective ?? "none"}");
        GetTree().Quit(0);
    }

    private async Task CaptureDialogueVisualProofAsync(string destination)
    {
        ValidateUiCaptureDestination(destination, "dialogue");
        if (_dialogueOverlay is null)
            throw new InvalidOperationException("The dialogue visual proof requires the dialogue overlay.");
        _dialogueOverlay.Show(_guideName, "Gardienne des braises",
            "La lumière revient, mais les pierres se souviennent encore du feu qui les a fendues.",
            [
                "Que s'est-il passé dans cette crypte ?",
                "Je retrouverai la clé de braise.",
                "Nous reparlerons plus tard.",
            ]);
        SyncPlayerInterfaceLock();
        for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (image.SavePng(destination) != Error.Ok)
            throw new IOException("The dialogue visual proof could not be written.");
        GD.Print($"OPENRPG_FORGE_DIALOGUE_VISUAL_PASS file={Path.GetFileName(destination)} " +
                 $"size={image.GetWidth()}x{image.GetHeight()} choices=3");
        GetTree().Quit(0);
    }

    private async Task CaptureActionWheelVisualProofAsync(string destination)
    {
        ValidateUiCaptureDestination(destination, "action wheel");
        if (_actionWheel is null)
            throw new InvalidOperationException("The action wheel visual proof requires the gameplay radial menu.");
        _rpgMenu?.Close();
        _moduleMap?.SetGlobalMapVisible(false);
        _dialogueOverlay?.Close();
        _actionWheel.Toggle();
        if (!_actionWheel.IsOpen)
            throw new InvalidOperationException("The action wheel could not be opened for visual proof.");
        for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (image.SavePng(destination) != Error.Ok)
            throw new IOException("The action wheel visual proof could not be written.");
        GD.Print($"OPENRPG_FORGE_ACTION_WHEEL_VISUAL_PASS file={Path.GetFileName(destination)} " +
                 $"size={image.GetWidth()}x{image.GetHeight()}");
        GetTree().Quit(0);
    }

    private async Task CaptureCombatFeedbackVisualProofAsync(
        string destinationDirectory,
        string? requestedResolution)
    {
        if (!Path.IsPathFullyQualified(destinationDirectory))
            throw new ArgumentException("The combat feedback capture destination must be absolute.",
                nameof(destinationDirectory));
        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);
        var player = _player ?? throw new InvalidOperationException("Combat feedback capture requires a player.");
        var camera = _camera ?? throw new InvalidOperationException("Combat feedback capture requires a camera.");
        var source = _playerCombatant ?? throw new InvalidOperationException(
            "Combat feedback capture requires a player combatant.");
        var targetNode = _boss ?? _hostiles.Keys.FirstOrDefault(GodotObject.IsInstanceValid) ??
            throw new InvalidOperationException("Combat feedback capture requires a hostile target.");
        var target = _hostiles[targetNode];
        var hud = _hud ?? throw new InvalidOperationException("Combat feedback capture requires the HUD.");
        var actionBar = _actionBar ?? throw new InvalidOperationException(
            "Combat feedback capture requires the action bar.");
        var feedback = _combatFeedbackLayer ?? throw new InvalidOperationException(
            "Combat feedback capture requires the feedback layer.");
        var impacts = _combatImpactPool ?? throw new InvalidOperationException(
            "Combat feedback capture requires the impact pool.");
        var map = _moduleMap ?? throw new InvalidOperationException("Combat feedback capture requires the map.");
        var toasts = _toastQueue ?? throw new InvalidOperationException("Combat feedback capture requires toasts.");
        var ability = source.Profile.Abilities.First();
        var captures = new List<object>();
        var mode = _combatUiPolicy.ReducedMotion ? "reduced" : "normal";

        if (!string.IsNullOrWhiteSpace(requestedResolution))
        {
            var (width, height) = VisualRegressionProfile.ParseResolution(requestedResolution);
            var window = GetWindow();
            window.Mode = Window.ModeEnum.Windowed;
            window.Borderless = true;
            window.Size = new Vector2I(width, height);
            window.Position = Vector2I.Zero;
            for (var frame = 0; frame < 3; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetViewport().Scaling3DScale = 1;
        player.GlobalPosition = targetNode.GlobalPosition + new Vector3(2.4f, 0, 1.2f);
        player.Velocity = Vector3.Zero;
        player.ResetPhysicsInterpolation();
        var focus = targetNode.GlobalPosition + Vector3.Up * 1.15f;
        camera.Fov = 48;
        camera.GlobalPosition = focus + new Vector3(7.2f, 4.4f, 8.2f);
        camera.LookAt(focus, Vector3.Up);
        feedback.SetTarget(targetNode);
        _currentCombatTarget = targetNode;
        hud.UpdateTargetStatus(_hostileNames[targetNode], target.Health,
            target.Profile.Stats.MaximumHealth, targetNode == _boss, defeated: false);
        actionBar.ConfigurePrimaryAbility(ability);
        actionBar.UpdatePrimaryAbilityState(0, source.Resource, targetAvailable: true);
        toasts.ClearAll();
        map.ClearPings();

        var preImpact = await SaveCombatFeedbackFrameAsync(root, "01-pre-impact.png", 6);
        captures.Add(CaptureReport("pre-impact", preImpact, actualGameplay: true,
            damage: 0, healing: 0, resource: 0, cooldown: 0));

        var healthBefore = target.Health;
        DamageHostile(targetNode);
        var presentation = _lastCombatFeedback ?? throw new InvalidOperationException(
            "Combat feedback capture received no projection after the real cast.");
        var damage = presentation.Amounts.Single(item => item.Kind == CombatFeedbackAmountKind.Damage).Value;
        AssertSmoke(healthBefore - target.Health == damage,
            "Combat feedback capture damage diverged from the authoritative result.");
        var impact = await SaveCombatFeedbackFrameAsync(root, "02-impact-peak.png", 1);
        captures.Add(CaptureReport("impact-peak", impact, actualGameplay: true,
            damage, healing: 0, resource: 0, cooldown: 0));

        var chip = await SaveCombatFeedbackFrameAsync(root, "03-delayed-chip.png", 9);
        captures.Add(CaptureReport("delayed-chip", chip, actualGameplay: true,
            damage, healing: 0, resource: 0, cooldown: 0));

        var stableFrames = _combatUiPolicy.ReducedMotion ? 70 : 42;
        var stable = await SaveCombatFeedbackFrameAsync(root, "04-stable-ready.png", stableFrames);
        captures.Add(CaptureReport("stable-ready", stable, actualGameplay: true,
            damage, healing: 0, resource: 0, cooldown: 0));

        toasts.ClearAll();
        map.ClearPings();
        var objective = _runtimePresentation?.Journal.ActiveObjective ?? "Retrouver la relique de la forêt";
        toasts.Show(RuntimeToastKind.Objective, $"Objectif : {objective}", $"capture-objective:{objective}");
        var objectivePosition = _trackedWaypointMarker?.Position ??
            (_keyItem is not null && GodotObject.IsInstanceValid(_keyItem)
                ? new Vector3Value(_keyItem.GlobalPosition.X, _keyItem.GlobalPosition.Y, _keyItem.GlobalPosition.Z)
                : new Vector3Value(targetNode.GlobalPosition.X, targetNode.GlobalPosition.Y,
                    targetNode.GlobalPosition.Z));
        map.ShowPing(objectivePosition, RuntimeMapPingKind.Objective,
            _trackedWaypointMarker?.AccessibleText ?? $"Objectif suivi : {objective}");
        var objectiveCapture = await SaveCombatFeedbackFrameAsync(root, "05-objective-toast-ping.png", 10);
        captures.Add(CaptureReport("objective-toast-ping", objectiveCapture, actualGameplay: true,
            damage: 0, healing: 0, resource: 0, cooldown: 0));

        var fixtureAbility = new AbilityDefinition(
            AbilityId.From(Guid.Parse("cf200000-0000-4000-8000-000000000001")),
            "fixture-renewal",
            "Renouveau de validation",
            2,
            .8,
            AbilityTargeting.Self,
            [
                new AbilityEffect(AbilityEffectKind.Heal, 5),
                new AbilityEffect(AbilityEffectKind.RestoreResource, 3),
            ],
            new AbilityVisualCue("cue.fixture-renewal"));
        var fixture = new CombatantState("fixture:capture",
            new CombatProfile(new CombatStats(30, 10, 4, 1, "fixture", "fixture"), [fixtureAbility]));
        fixture.Restore(12, 2);
        var fixtureResult = CombatResolver.Cast(fixture, fixture, fixtureAbility.Id, 100);
        var fixturePresentation = CombatFeedbackPresentationService.Create(
            Guid.Parse("cf200000-0000-4000-8000-000000000002"),
            fixture, fixture, fixtureResult, 100.1);
        toasts.ClearAll();
        map.ClearPings();
        actionBar.ConfigurePrimaryAbility(fixtureAbility);
        actionBar.UpdatePrimaryAbilityState(.7, fixture.Resource, targetAvailable: true);
        hud.UpdatePlayerStatus(
            "FIXTURE TEST",
            new RuntimeModulePresentation.Resource("health", 18, 30),
            new RuntimeModulePresentation.Resource("resource", fixture.Resource, 10),
            1);
        hud.UpdateTargetStatus("FIXTURE S+R", fixture.Health, 30, boss: false, defeated: false);
        feedback.Present(fixturePresentation, targetNode);
        feedback.Flash(targetNode, CombatFeedbackAmountKind.Healing);
        impacts.Present(fixturePresentation, targetNode, targetNode.GlobalPosition);
        impacts.PlayAudio(CombatFeedbackAudioKind.Healing, targetNode.GlobalPosition);
        toasts.Show(RuntimeToastKind.System,
            "FIXTURE HORS GAMEPLAY — soin, ressource, recharge et chip joueur",
            "capture:fixture");
        map.ShowPing(new Vector3Value(targetNode.GlobalPosition.X, targetNode.GlobalPosition.Y,
                targetNode.GlobalPosition.Z),
            RuntimeMapPingKind.Objective, "Fixture visuelle hors gameplay");
        var fixtureCapture = await SaveCombatFeedbackFrameAsync(root, "06-fixtures-not-gameplay.png", 10);
        captures.Add(CaptureReport("fixtures", fixtureCapture, actualGameplay: false,
            damage: 0, fixtureResult.Healing, fixtureResult.ResourceRestored,
            fixturePresentation.CooldownRemainingSeconds));

        var windowSize = GetViewport().GetTexture().GetImage().GetSize();
        var reportPath = Path.Combine(root, "capture-report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            passed = true,
            resolution = $"{windowSize.X}x{windowSize.Y}",
            mode,
            reducedMotion = _combatUiPolicy.ReducedMotion,
            colorVisionMode = _combatUiPolicy.ColorVisionMode.ToString(),
            highContrast = _combatUiPolicy.HighContrast,
            textScale = _combatUiPolicy.TextScale,
            pools = new
            {
                floatingCapacity = RuntimeCombatFeedbackLayer.FloatingNumberCapacity,
                nameplateCapacity = RuntimeCombatFeedbackLayer.NameplateCapacity,
                particleCapacity = RuntimeCombatImpactPool.ParticleCapacity,
                audioCapacity = RuntimeCombatImpactPool.AudioCapacity,
                pingCapacity = 8,
                toastCapacity = 3,
                floatingPeak = feedback.PeakActiveFloatingCount,
                particlePeak = impacts.PeakParticleCount,
                audioPeak = impacts.PeakAudioCount,
                pingPeak = map.PeakPingCount,
                toastPeak = toasts.PeakActiveCount,
            },
            authoritative = new
            {
                presentation.TransactionId,
                presentation.AbilitySlug,
                presentation.VisualCueKey,
                damage,
                targetHealth = target.Health,
                cooldownSeconds = presentation.CooldownRemainingSeconds,
            },
            captures,
            marker = "OPENRPG_FORGE_COMBAT_FEEDBACK_CAPTURE_PASS",
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }) + System.Environment.NewLine, new UTF8Encoding(false));
        GD.Print($"OPENRPG_FORGE_COMBAT_FEEDBACK_CAPTURE_PASS resolution={windowSize.X}x{windowSize.Y} " +
                 $"mode={mode} captures={captures.Count} actual_gameplay=5 fixtures=1 " +
                 $"damage={damage} report={Path.GetFileName(reportPath)}");
        GetTree().Quit(0);

        object CaptureReport(
            string stage,
            (int Width, int Height, long Bytes, string File) frame,
            bool actualGameplay,
            int damage,
            int healing,
            int resource,
            double cooldown) => new
            {
                stage,
                frame.File,
                frame.Width,
                frame.Height,
                frame.Bytes,
                actualGameplay,
                damage,
                healing,
                resourceRestored = resource,
                cooldownRemainingSeconds = cooldown,
            };
    }

    private async Task<(int Width, int Height, long Bytes, string File)> SaveCombatFeedbackFrameAsync(
        string root,
        string fileName,
        int frames)
    {
        for (var frame = 0; frame < frames; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var readbackParity = (long)(Engine.GetFramesDrawn() & 1);
        if (_combatFeedbackCaptureReadbackParity is null)
        {
            // The first Vulkan swapchain image after the deterministic resize can retain stale canvas batches.
            // Prime the alternate image once, then keep every proof on that validated readback parity.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            readbackParity = (long)(Engine.GetFramesDrawn() & 1);
            _combatFeedbackCaptureReadbackParity = readbackParity;
        }
        else if (readbackParity != _combatFeedbackCaptureReadbackParity.Value)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            readbackParity = (long)(Engine.GetFramesDrawn() & 1);
        }
        RenderingServer.ForceSync();
        var image = GetViewport().GetTexture().GetImage();
        var path = Path.Combine(root, fileName);
        if (image.SavePng(path) != Error.Ok)
            throw new IOException($"Combat feedback capture could not be written: {path}.");
        return (image.GetWidth(), image.GetHeight(), new FileInfo(path).Length, fileName);
    }

    private static void ValidateUiCaptureDestination(string destination, string label)
    {
        if (!Path.IsPathFullyQualified(destination) || !string.Equals(Path.GetExtension(destination), ".png",
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The {label} visual proof destination must be an absolute PNG path.",
                nameof(destination));
    }

    private async Task CaptureVisualRegressionSuiteAsync(ProjectDefinition project, string destinationDirectory)
    {
        if (!Path.IsPathFullyQualified(destinationDirectory))
            throw new ArgumentException("The visual regression destination must be absolute.", nameof(destinationDirectory));
        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);
        var profile = project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance.Visual;
        var plan = PerformanceQualityService.PlanVisualReviews(profile);
        var reviews = new List<object>();
        foreach (var visualCase in plan.Cases)
        {
            var (width, height) = VisualRegressionProfile.ParseResolution(visualCase.Resolution);
            GetWindow().Size = new Vector2I(width, height);
            GetViewport().Scaling3DScale = visualCase.QualityTier == RenderQualityTier.Medium ? .85f : 1f;
            if (_camera is not null)
            {
                _camera.GlobalPosition = visualCase.CameraFixture == "overview"
                    ? new Vector3(13, 15, 17)
                    : new Vector3(-10, 10, 14);
                _camera.Fov = visualCase.CameraFixture == "overview" ? 55 : 50;
                _camera.LookAt((_player?.GlobalPosition ?? Vector3.Zero) + Vector3.Up * .8f, Vector3.Up);
            }
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var reference = GetViewport().GetTexture().GetImage();
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var candidate = GetViewport().GetTexture().GetImage();
            var referencePath = Path.Combine(root, $"{visualCase.Id}.reference.png");
            var candidatePath = Path.Combine(root, $"{visualCase.Id}.candidate.png");
            var reviewPath = Path.Combine(root, $"{visualCase.Id}.review.json");
            if (reference.SavePng(referencePath) != Error.Ok || candidate.SavePng(candidatePath) != Error.Ok)
                throw new IOException($"Visual regression images could not be written for {visualCase.Id}.");
            var error = MeanPixelError(reference, candidate, 16);
            var passed = error <= visualCase.PixelTolerance;
            var review = new
            {
                schemaVersion = 1,
                visualCase.Id,
                visualCase.Resolution,
                visualCase.CameraFixture,
                qualityTier = visualCase.QualityTier.ToString(),
                expectedWidth = width,
                expectedHeight = height,
                actualWidth = candidate.GetWidth(),
                actualHeight = candidate.GetHeight(),
                visualCase.PixelTolerance,
                meanPixelError = error,
                passed,
            };
            File.WriteAllText(reviewPath, JsonSerializer.Serialize(review, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }) + System.Environment.NewLine, new UTF8Encoding(false));
            if (!passed) throw new InvalidOperationException(
                $"Visual regression {visualCase.Id} exceeded tolerance: {error:0.######} > {visualCase.PixelTolerance:0.######}.");
            reviews.Add(review);
        }
        File.WriteAllText(Path.Combine(root, "visual-suite-report.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            passed = true,
            plan.Fingerprint,
            cases = reviews,
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }) +
            System.Environment.NewLine, new UTF8Encoding(false));
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11S_VISUAL_PASS cases={plan.Cases.Count} " +
            $"resolutions={profile.Resolutions.Count} cameras={profile.CameraFixtures.Count} " +
            $"quality={profile.QualityTiers.Count} tolerance={profile.PixelTolerance:0.###} fingerprint={plan.Fingerprint}");
        GetTree().Quit(0);
    }

    private static double MeanPixelError(Image reference, Image candidate, int sampleStep)
    {
        if (reference.GetWidth() != candidate.GetWidth() || reference.GetHeight() != candidate.GetHeight())
            return 1;
        double error = 0;
        var samples = 0;
        for (var y = 0; y < reference.GetHeight(); y += sampleStep)
            for (var x = 0; x < reference.GetWidth(); x += sampleStep)
            {
                var expected = reference.GetPixel(x, y);
                var actual = candidate.GetPixel(x, y);
                error += (Math.Abs(expected.R - actual.R) + Math.Abs(expected.G - actual.G) +
                    Math.Abs(expected.B - actual.B)) / 3d;
                samples++;
            }
        return samples == 0 ? 1 : error / samples;
    }


}

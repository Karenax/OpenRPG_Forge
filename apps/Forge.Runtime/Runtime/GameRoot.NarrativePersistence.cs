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


public partial class GameRoot : Node3D
{
    private bool TryActivateNarrativeDialogue(
        Node3D participantNode,
        (BlueprintId BlueprintId, string DisplayName) participant)
    {
        if (_loadedProject is null)
            return false;
        RuntimeDialogueResolution? resolution;
        try
        {
            resolution = RuntimeNarrativeResolver.ResolveDialogue(
                _loadedProject, participant.BlueprintId, "default");
        }
        catch (InvalidOperationException exception)
        {
            _message = $"{participant.DisplayName} possède plusieurs dialogues d'entrée concurrents.";
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_DIALOGUE_AMBIGUOUS participant={participant.BlueprintId} " +
                        $"error={exception.Message}");
            return false;
        }

        if (resolution is null)
        {
            _message = $"{participant.DisplayName} reste silencieux : aucun dialogue authoré ne correspond à ce participant.";
            return false;
        }

        var continueTraversal = _activeDialogueTraversal is { Completed: false } &&
                                _activeDialogueTraversalDialogueId == resolution.Dialogue.Id &&
                                _activeDialogue?.Id == resolution.Dialogue.Id &&
                                _activeDialogueEntryNodeId == resolution.EntryNodeId &&
                                ReferenceEquals(_activeDialogueParticipant, participantNode);
        _activeDialogue = resolution.Dialogue;
        _activeDialogueEntryNodeId = resolution.EntryNodeId;
        if (!continueTraversal)
        {
            _activeDialogueTraversal = new DialogueTraversal(resolution.Dialogue, resolution.EntryNodeId);
            _activeDialogueTraversalDialogueId = resolution.Dialogue.Id;
        }
        _activeDialogueParticipant = participantNode;
        _guideName = participant.DisplayName;
        GD.Print($"OPENRPG_FORGE_RUNTIME_DIALOGUE_RESOLVED participant={participant.BlueprintId} " +
                 $"dialogue={resolution.Dialogue.Id} entry={resolution.EntryNodeId}");
        return true;
    }

    private void InteractWithGuide()
    {
        if (_activeDialogue is null || _activeDialogueEntryNodeId is null)
        {
            _message = $"{_guideName} reste silencieux : aucun dialogue n’est lié à ce module.";
            return;
        }

        if (_activeDialogueTraversal is null || _activeDialogueTraversalDialogueId != _activeDialogue.Id)
        {
            _activeDialogueTraversal = new DialogueTraversal(_activeDialogue, _activeDialogueEntryNodeId);
            _activeDialogueTraversalDialogueId = _activeDialogue.Id;
        }
        var traversal = _activeDialogueTraversal;
        var start = traversal.Start();
        var evaluator = new RuntimeConditionEvaluator(this);
        var variantText = ApplyDialoguePresentation(start.Node!.Id, evaluator);
        _activeDialogueChoices = traversal.GetAvailableChoices(evaluator);
        _dialogueChoiceCommitted = false;
        var spokenLine = variantText ?? start.Node!.Text.Fallback;
        if (_actionWheel?.IsOpen == true) _actionWheel.Close();
        var portrait = ResolveActiveDialoguePortrait();
        _dialogueOverlay?.Show(
            _guideName,
            ResolveActiveDialogueRole(),
            spokenLine,
            _activeDialogueChoices.Select(item => item.Text.Fallback).ToArray(),
            portrait.Path,
            portrait.AltText);
        _message = $"{_guideName} : {spokenLine}";
        SyncPlayerInterfaceLock();
    }

    private void HandleDialogueOverlayChoiceSelected(int index)
    {
        _dialogueChoiceCommitted = true;
        CommitDialogueChoice(index);
    }

    private void CommitDialogueChoice(int index)
    {
        if (_activeDialogueTraversal is not { } traversal) return;
        var start = traversal.Start();
        if (_activeDialogueChoices.Count == 0)
        {
            _message = $"{_guideName} : {start.Node?.Text.Fallback ?? "…"}";
            _activeDialogueTraversal = null;
            _activeDialogueTraversalDialogueId = null;
            return;
        }
        if (index < 0 || index >= _activeDialogueChoices.Count) return;

        var evaluator = new RuntimeConditionEvaluator(this);
        var step = traversal.Choose(_activeDialogueChoices[index].Id, evaluator);
        ApplyDialogueActions(step.Actions);
        if (step.Completed)
        {
            _activeDialogueTraversal = null;
            _activeDialogueTraversalDialogueId = null;
        }
        _activeDialogueChoices = [];
        var destination = step.Node?.Text.Fallback ?? start.Node?.Text.Fallback ?? "…";
        _message = $"{_guideName} : {destination}";
        UpdateHud();
    }

    private void HandleDialogueOverlayClosed()
    {
        if (_dialogueChoiceCommitted)
        {
            _dialogueChoiceCommitted = false;
            if (_activeDialogueTraversal is null) ClearActiveDialogueSession();
        }
        else
        {
            ClearActiveDialogueSession();
        }
        SyncPlayerInterfaceLock();
    }

    private void ClearActiveDialogueSession()
    {
        _activeDialogueTraversal = null;
        _activeDialogueTraversalDialogueId = null;
        _activeDialogueChoices = [];
        _activeDialogue = null;
        _activeDialogueEntryNodeId = null;
        _activeDialogueParticipant = null;
    }

    private EntityInstance? ResolveActiveDialogueEntity()
    {
        if (_activeDialogueParticipant is null || _activeArea is null) return null;
        var entityId = _entityNodes.FirstOrDefault(item =>
            ReferenceEquals(item.Value, _activeDialogueParticipant)).Key;
        return entityId == Guid.Empty
            ? null
            : _activeArea.Entities.SingleOrDefault(entity => entity.Id.Value == entityId);
    }

    private (string? Path, string? AltText) ResolveActiveDialoguePortrait()
    {
        var entity = ResolveActiveDialogueEntity();
        if (entity is null) return (null, null);
        var portrait = RuntimeModuleAssetPathResolver.ResolveImage(
            _moduleContentRoot,
            GetOverride(entity, "runtime.portrait"));
        return (portrait, GetOverride(entity, "runtime.portraitAlt"));
    }

    private string ResolveActiveDialogueRole()
    {
        var entity = ResolveActiveDialogueEntity();
        return entity is null
            ? "Guide du voyage"
            : GetOverride(entity, "runtime.role") ?? "Guide du voyage";
    }

    private string? ApplyDialoguePresentation(DialogueNodeId nodeId, IDialogueConditionEvaluator evaluator)
    {
        if (_activeDialogue is null) return null;
        var participant = _activeDialogue.Nodes.First(item => item.Id == nodeId).SpeakerBlueprintId;
        var participantTags = ResolveActiveDialogueEntity()?.Tags.ToHashSet(StringComparer.Ordinal) ??
                              new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<DialogueCompanionState> companions = participant is null ? [] :
            [new(participant, _activeDialogueParticipant is not null, 100, participantTags)];
        var context = new DialoguePresentationRuntimeContext("fr-FR",
            _activeDialogue.Presentation?.RequiredAtRuntime == true, false, false, true,
            DateTimeOffset.UtcNow, companions, new Dictionary<Guid, DateTimeOffset>());
        var resolution = DialoguePresentationRuntimeResolver.Resolve(_activeDialogue, nodeId, context);
        foreach (var diagnostic in resolution.Diagnostics)
            GD.PrintErr($"OPENRPG_FORGE_DIALOGUE_PRESENTATION_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        if (resolution.Diagnostics.Count > 0) return null;

        _dialogueAnimationCueCount += resolution.AnimationCues.Count;
        if (_activeDialogueParticipant is not null &&
            _characterAnimations.TryGetValue(_activeDialogueParticipant, out var animation))
        {
            foreach (var cue in resolution.AnimationCues)
            {
                var clip = animation.Clips.FirstOrDefault(item =>
                    string.Equals(item, cue.AnimationId, StringComparison.OrdinalIgnoreCase)) ?? animation.IdleClip;
                GodotAssetAnimationController.Play(animation, clip,
                    Math.Max(0, cue.BlendMilliseconds) / 1000.0, restart: true);
            }
        }

        if (resolution.VoiceCue is not null)
        {
            _dialogueVoiceCueCount++;
            _dialogueSubtitleFallback = resolution.UsesSubtitleFallback;
            GD.Print($"OPENRPG_FORGE_DIALOGUE_VOICE locale={resolution.VoiceCue.Locale} " +
                $"take={resolution.VoiceCue.Take} fallback={resolution.UsesSubtitleFallback.ToString().ToLowerInvariant()} " +
                $"subtitle={resolution.VoiceCue.Subtitle.Key}");
        }

        if (resolution.CameraShot is { } shot && _camera is not null)
        {
            _dialogueCameraShotCount++;
            _pendingCameraFieldOfView = shot.FieldOfViewDegrees;
            if (_activeDialogueParticipant is not null)
                _pendingCameraLookAt = _activeDialogueParticipant.GlobalPosition + Vector3.Up;
        }
        _dialogueBookmarkCount += _activeDialogue.Presentation?.Bookmarks.Count(item => item.NodeId == nodeId) ?? 0;
        if (resolution.Interjection is not null) _dialogueInterjectionCount++;

        var adaptive = DialogueAdaptiveRuntimeResolver.Resolve(_activeDialogue, nodeId, 0,
            AdaptiveDialogueContext(), evaluator);
        foreach (var diagnostic in adaptive.Diagnostics)
            GD.PrintErr($"OPENRPG_FORGE_ADAPTIVE_DIALOGUE_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        if (adaptive.Diagnostics.Count > 0) return null;
        if (adaptive.TimedChoice is not null) _dialogueTimedChoiceCount++;
        if (adaptive.Variant is null) return null;
        _dialogueVariantCount++;
        _dialogueVariantKey = adaptive.Variant.Variant.Text.Key;
        GD.Print($"OPENRPG_FORGE_DIALOGUE_VARIANT set={adaptive.Variant.Set.Key} " +
            $"key={_dialogueVariantKey} locale={adaptive.Variant.Locale} roll={adaptive.Variant.DeterministicRoll}");
        return adaptive.Variant.Variant.Text.Fallback;
    }

    private DialogueAdaptiveRuntimeContext AdaptiveDialogueContext() => new("fr-FR", true, 1, false, false, 2026,
        _activeDialogue?.Id.Value ?? Guid.Empty, new Dictionary<Guid, IReadOnlyList<Guid>>());

    private void ApplyDialogueActions(IReadOnlyList<DialogueAction> actions)
    {
        foreach (var action in actions)
        {
            switch (action.Kind)
            {
                case DialogueActionKind.SetVariable:
                    SetProjectVariable(action.Key, action.Value);
                    break;
                case DialogueActionKind.StartQuest:
                    if (_questTracker?.Accept(action.Key, track: true, out var newlyAccepted) != true)
                    {
                        if (action.Required)
                            throw new InvalidOperationException(
                                $"Dialogue action references missing quest '{action.Key}'.");
                        break;
                    }
                    SyncTrackedQuestDefinition();
                    var acceptedQuest = _questTracker.ResolveQuest(action.Key)!;
                    if (!newlyAccepted) break;
                    if (!TryAdvanceQuestByEvent(
                            acceptedQuest,
                            QuestEventKind.Dialogue,
                            action.Value,
                            _activeDialogue?.Id.ToString()) &&
                        !TryAdvanceQuestByEvent(
                            acceptedQuest,
                            QuestEventKind.Manual,
                            "manual",
                            sourceReference: null) && action.Required)
                        throw new InvalidOperationException(
                            $"Quest '{action.Key}' has no authored start transition for '{action.Value}'.");
                    break;
                case DialogueActionKind.AdvanceQuest:
                case DialogueActionKind.CompleteQuest:
                    var quest = _questTracker?.ResolveQuest(action.Key);
                    if (quest is null || !_questTracker!.IsAccepted(quest.Id))
                    {
                        if (action.Required)
                            throw new InvalidOperationException(
                                $"Dialogue action references an unaccepted quest '{action.Key}'.");
                        break;
                    }
                    var eventKind = string.Equals(action.Value, "manual", StringComparison.Ordinal)
                        ? QuestEventKind.Manual
                        : QuestEventKind.Dialogue;
                    var eventName = eventKind == QuestEventKind.Manual ? "manual" : action.Value;
                    if (!TryAdvanceQuestByEvent(quest, eventKind, eventName, _activeDialogue?.Id.ToString()) &&
                        action.Required)
                        throw new InvalidOperationException(
                            $"Quest '{action.Key}' has no authored transition for '{action.Value}'.");
                    break;
                case DialogueActionKind.GiveItem:
                    GrantInventoryItem(action.Key, 1);
                    break;
                case DialogueActionKind.UnlockEntity:
                    UnlockEntity(action.Key);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unsupported dialogue action.");
            }
        }
    }

    private IReadOnlyList<QuestTransition> GetAvailableQuestTransitions()
    {
        if (_questTracker?.TrackedQuestId is not { } trackedQuestId)
            return [];
        return _questTracker.GetAvailableTransitions(trackedQuestId, new RuntimeConditionEvaluator(this));
    }

    private bool TryAdvanceQuestByEvent(
        QuestDefinition quest,
        QuestEventKind kind,
        string eventName,
        string? sourceReference)
    {
        if (_questTracker is null || string.IsNullOrWhiteSpace(eventName))
            return false;
        var matches = _questTracker.GetAvailableTransitions(quest.Id, new RuntimeConditionEvaluator(this))
            .Where(transition => transition.EventBinding.Kind == kind &&
                string.Equals(transition.EventBinding.EventName, eventName, StringComparison.Ordinal) &&
                (transition.EventBinding.SourceReference is null ||
                 string.Equals(transition.EventBinding.SourceReference, sourceReference, StringComparison.Ordinal)))
            .ToArray();
        if (matches.Length == 0)
            return false;
        if (matches.Length > 1)
            throw new InvalidOperationException(
                $"Quest event '{kind}:{eventName}' resolves to multiple transitions in quest '{quest.Id}'.");
        AdvanceQuest(quest, matches[0]);
        return true;
    }

    private void AdvanceQuest(QuestTransition transition)
    {
        if (_trackedQuestDefinition is null)
            return;
        AdvanceQuest(_trackedQuestDefinition, transition);
    }

    private void AdvanceQuest(QuestDefinition quest, QuestTransition transition)
    {
        if (_questTracker is null)
            return;
        if (quest.Operations is not null)
        {
            var context = QuestOperationsContext();
            var exclusive = QuestOperationsRuntimeResolver.ResolveExclusive(quest, transition.Id, context);
            if (!exclusive.Allowed)
            {
                _message = "Cette voie de quête est incompatible avec un choix antérieur.";
                return;
            }
            var share = QuestOperationsRuntimeResolver.ResolveShare(quest, context);
            if (!share.Accepted)
            {
                _message = "Le groupe n’a pas validé cette progression de quête.";
                return;
            }
            _chosenQuestTransitions.Add(transition.Id);
        }
        var advance = _questTracker.Advance(quest, transition, new RuntimeConditionEvaluator(this));
        var result = advance.Result;
        _questState = result.Completed ? PrototypeQuestState.Completed : PrototypeQuestState.Active;
        foreach (var action in result.Actions)
        {
            switch (action.Kind)
            {
                case QuestActionKind.SetVariable:
                    SetProjectVariable(action.Key, action.Value);
                    break;
                case QuestActionKind.GiveItem:
                    GrantInventoryItem(action.Key, 1);
                    break;
                case QuestActionKind.UnlockEntity:
                    UnlockEntity(action.Key);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unsupported quest action.");
            }
        }

        _message = result.State.Journal.Fallback;
        if (quest.Operations is not null)
        {
            var notice = QuestOperationsRuntimeResolver.ResolveNotification(quest);
            GD.Print($"OPENRPG_FORGE_RUNTIME_QUEST_NOTICE visible={notice.Visible.ToString().ToLowerInvariant()} " +
                     $"cinematic={notice.Cinematic.ToString().ToLowerInvariant()} group={notice.GroupKey} " +
                      $"state={result.State.Id}");
        }
        SyncTrackedQuestDefinition();
    }

    private void SyncTrackedQuestDefinition()
    {
        _trackedQuestDefinition = _questTracker?.TrackedQuestId is { } trackedQuestId
            ? _loadedProject?.Quests.SingleOrDefault(quest => quest.Id == trackedQuestId)
            : null;
    }

    private QuestStateDefinition? TrackedQuestRuntimeState =>
        _questTracker?.TrackedQuestId is { } trackedQuestId
            ? _questTracker.GetCurrentState(trackedQuestId)
            : null;

    private bool IsQuestTerminal(QuestId questId) =>
        _questTracker?.GetCurrentState(questId)?.IsTerminal == true;

    private bool QuestStateMatches(string questReference, string expectedValue)
    {
        var quest = _questTracker?.ResolveQuest(questReference);
        var state = quest is null ? null : _questTracker?.GetCurrentState(quest.Id);
        return state is not null &&
               (string.Equals(state.Id.ToString(), expectedValue, StringComparison.Ordinal) ||
                string.Equals(state.DisplayName, expectedValue, StringComparison.OrdinalIgnoreCase));
    }

    private QuestOperationsRuntimeContext QuestOperationsContext() => new(
        DateTimeOffset.UtcNow, null, 0, 1, 1, true, true, _chosenQuestTransitions);

    private ItemRuntimeContext AdvancedItemContext(ItemAdvancedProfile profile, int seed) => new(
        6, 20, new HashSet<string>(["lore", "smithing"]), new HashSet<string>(["ash-attuned"]),
        new HashSet<string>(["surface-alliance"]),
        new HashSet<string>(_questState == PrototypeQuestState.NotStarted
            ? ["target.ash-sealed"] : ["target.ash-sealed", "quest.accepted"]),
        "crypt", profile.Economy.Currency, 1, profile.Durability?.Initial ?? 100, seed, "player:local", true);

    private void UnlockEntity(string entityId)
    {
        if (_doorEntityId is null || !string.Equals(entityId, _doorEntityId.ToString(), StringComparison.Ordinal))
        {
            return;
        }

        _doorUnlocked = true;
        if (_door is not null && GodotObject.IsInstanceValid(_door))
        {
            _door.QueueFree();
        }

        _message = $"{_gateDisplayName} s’ouvre.";
    }

    private void SetProjectVariable(string key, string value)
    {
        if (_blackboard.TrySet(key, value, out var error))
        {
            return;
        }

        _message = $"La variable de projet '{key}' a refusé une valeur incompatible.";
        GD.PrintErr($"OPENRPG_FORGE_RUNTIME_VARIABLE_REJECTED key={key} error={error}");
    }

    private bool SaveRuntime(string path)
    {
        if (_player is null || _playerCombatant is null || _moduleId is null ||
            _moduleContentSha256 is null || _currentAreaId is null)
        {
            GD.PrintErr("OPENRPG_FORGE_RUNTIME_SAVE_FAILED runtime_not_ready");
            return false;
        }

        var position = _player.GlobalPosition;
        var save = new RuntimeSaveDocument(
            RuntimeSaveDocument.CurrentFormatVersion,
            _moduleId,
            _moduleContentSha256,
            _currentAreaId,
            DateTimeOffset.UtcNow,
            new RuntimeVector3(position.X, position.Y, position.Z),
            new RuntimeCombatantSave(
                _playerCombatant.Id,
                _playerCombatant.Health,
                _playerCombatant.Resource),
            _inventory.Snapshot()
                .Select(stack => new RuntimeInventorySaveStack(stack.Slug, stack.Quantity))
                .ToArray(),
            _blackboard.Snapshot().OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new RuntimeKeyValue(item.Key, item.Value))
                .ToArray(),
            null,
            null,
            _hostiles.Values
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new RuntimeCombatantSave(item.Id, item.Health, item.Resource))
                .ToArray(),
            new RuntimeProgressFlags(
                _hasKey,
                _doorUnlocked,
                _bossPhaseTwo,
                _bossDefeated,
                _escaped),
            _environmentProfile?.Weather.PersistState == true && _weatherState is not null
                ? new RuntimeWeatherSave(
                    _weatherState.SchemaVersion,
                    _weatherState.StateId.ToString(),
                    _weatherState.Revision,
                    _weatherState.ServerRevision,
                    _weatherState.ElapsedSeconds)
                : null,
            new RuntimeProgressionSave(1, _progressionExperience, _progressionLevel),
            BuildEntityStateSnapshots(),
            _worldClockSnapshot is null ? null : new RuntimeWorldClockSave(1,
                _worldClockSnapshot.TotalMinutes, _worldClockSnapshot.Paused, _worldClockSnapshot.Revision,
                _worldClockSnapshot.Provenance),
            _questTracker?.Snapshot()
                .Select(state => new RuntimeQuestSaveState(
                    state.QuestId.ToString(), state.StateId.ToString(), state.IsAccepted))
                .ToArray() ?? [],
            _questTracker?.TrackedQuestId?.ToString(),
            _mapExploration is null
                ? null
                : new RuntimeMapExplorationSave(
                    _mapExploration.Scope.ToString().ToLowerInvariant(),
                    _mapExploration.ExplorerId,
                    _mapExploration.ActiveFloor,
                    _mapExploration.RevealedCells.Order(StringComparer.Ordinal).ToArray(),
                    _mapExploration.RevealedPins.Select(id => id.ToString("D"))
                        .Order(StringComparer.Ordinal).ToArray()));
        var result = RuntimeSaveStore.Save(path, save);
        if (!result.Succeeded)
        {
            _message = "Échec de la sauvegarde de partie.";
            GD.PrintErr(
                "OPENRPG_FORGE_RUNTIME_SAVE_FAILED " +
                string.Join(";", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
            return false;
        }

        if (_gameplayControls?.Foundation.Save is { } savePolicy)
        {
            var archived = RuntimeSaveArchiveStore.Save(ArchiveRoot(path), ArchiveSlot(path),
                Math.Max(1, save.SavedAtUtc.UtcTicks), save, savePolicy, [], save.SavedAtUtc);
            if (!archived.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_SAVE_FAILED {archived.Diagnostics[0].Code}:" +
                            archived.Diagnostics[0].Message);
                return false;
            }
        }

        _message = $"Partie sauvegardée — {Path.GetFileName(path)}";
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_WRITTEN format={RuntimeSaveDocument.CurrentFormatVersion} " +
            $"file={Path.GetFileName(path)} quests={save.QuestStates?.Count ?? 0} " +
            $"tracked={save.TrackedQuestId ?? "none"}");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS mode=write entities={save.EntitySnapshots?.Count ?? 0} " +
            "components=transform-combat-inventory-blackboard schema=1");
        UpdateHud();
        return true;
    }

    private bool LoadRuntimeSave(string path)
    {
        if (_moduleId is null || _moduleContentSha256 is null)
        {
            GD.PrintErr("OPENRPG_FORGE_RUNTIME_LOAD_FAILED runtime_not_ready");
            return false;
        }

        var result = RuntimeSaveStore.Load(path, _moduleId, _moduleContentSha256);
        if (!result.Succeeded || result.Save is null)
        {
            _message = "Cette sauvegarde est absente, invalide ou incompatible avec le module.";
            GD.PrintErr(
                "OPENRPG_FORGE_RUNTIME_LOAD_FAILED " +
                string.Join(";", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
            return false;
        }
        var loadedSave = result.Save;

        var archivePath = ArchiveRoot(path);
        if (_gameplayControls?.Foundation.Save is { } savePolicy && Directory.Exists(archivePath))
        {
            var archived = RuntimeSaveArchiveStore.Load(archivePath, ArchiveSlot(path), 0,
                _moduleId, _moduleContentSha256, []);
            if (!archived.Succeeded || archived.Save is null)
            {
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_LOAD_FAILED {archived.Diagnostics[0].Code}:" +
                            archived.Diagnostics[0].Message);
                return false;
            }
            loadedSave = archived.Save;
        }

        try
        {
            ApplyRuntimeSave(loadedSave);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            _message = "La sauvegarde référence un état de jeu indisponible.";
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_LOAD_FAILED save.state_invalid:{exception.Message}");
            return false;
        }

        _loadedSave = true;
        _message = $"Partie reprise — {Path.GetFileName(path)}";
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_LOADED format={loadedSave.FormatVersion} " +
            $"file={Path.GetFileName(path)} quests={loadedSave.QuestStates?.Count ?? 0} " +
            $"tracked={loadedSave.TrackedQuestId ?? "none"}");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS mode=load entities={loadedSave.EntitySnapshots?.Count ?? 0} " +
            "components=transform-combat-inventory-blackboard schema=1");
        return true;
    }

    private void ApplyRuntimeSave(RuntimeSaveDocument save)
    {
        if (_player is null || _playerCombatant is null ||
            !string.Equals(save.AreaId, _currentAreaId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The save area does not match the loaded runtime area.");
        }

        _player.GlobalPosition = new Vector3(
            (float)save.PlayerPosition.X,
            (float)save.PlayerPosition.Y,
            (float)save.PlayerPosition.Z);
        _playerCombatant.Restore(save.Player.Health, save.Player.Resource);
        _inventory.Replace(save.Inventory.Select(stack => new RuntimeInventoryStack(stack.Slug, stack.Quantity)));

        if (save.MapExploration is null)
        {
            _mapExploration = null;
            _mapExplorationAreaId = null;
            _mapExplorationCell = null;
        }
        else
        {
            if (_activeArea is null ||
                !Enum.TryParse<AreaMapExplorationScope>(save.MapExploration.Scope, true, out var scope))
            {
                throw new InvalidOperationException("The saved map exploration scope is unavailable.");
            }

            var restored = new AreaMapExplorationState(
                scope,
                save.MapExploration.ExplorerId,
                save.MapExploration.RevealedCells.ToHashSet(StringComparer.Ordinal),
                save.MapExploration.RevealedPins.Select(Guid.Parse).ToHashSet(),
                save.MapExploration.ActiveFloor);
            var normalized = AreaExperienceAuthoringScriptService.Normalize(_activeArea, restored);
            _mapExploration = normalized.State;
            _mapExplorationAreaId = _activeArea.Id.ToString();
            _mapExplorationCell = null;
            ReportMapExplorationDiagnostics(normalized.Diagnostics);
        }

        _blackboard = ProjectBlackboardScriptService.ForRuntime(_blackboard.Definitions);
        var variableDiagnostics = _blackboard.Load(save.Variables.ToDictionary(
            variable => variable.Key,
            variable => variable.Value,
            StringComparer.Ordinal));
        if (variableDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", variableDiagnostics));
        }

        if (_questTracker is not null)
        {
            var questStates = (save.QuestStates ?? [])
                .Select(state => new RuntimeQuestStateSnapshot(
                    QuestId.Parse(state.QuestId), QuestStateId.Parse(state.StateId), state.IsAccepted))
                .ToArray();
            var trackedQuestId = save.TrackedQuestId is null ? null : QuestId.Parse(save.TrackedQuestId);
            _questTracker.Restore(questStates, trackedQuestId);
            SyncTrackedQuestDefinition();
            var acceptedStates = _questTracker.Snapshot().Where(state => state.IsAccepted)
                .Select(state => _questTracker.GetCurrentState(state.QuestId)!)
                .ToArray();
            _questState = acceptedStates.Any(state => !state.IsTerminal)
                ? PrototypeQuestState.Active
                : acceptedStates.Any(state => state.IsTerminal)
                    ? PrototypeQuestState.Completed
                    : PrototypeQuestState.NotStarted;
        }

        if (save.Weather is not null)
        {
            if (_environmentProfile is null ||
                !_environmentProfile.Weather.States.Any(state =>
                    string.Equals(state.Id.ToString(), save.Weather.StateId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("The saved weather state is unavailable in the current area.");
            }

            _weatherState = new WeatherRuntimeSnapshot(
                save.Weather.SchemaVersion,
                Guid.Parse(save.Weather.StateId),
                save.Weather.Revision,
                save.Weather.ServerRevision,
                save.Weather.ElapsedSeconds);
            ApplyClimateEffects(force: true);
        }

        var hostileSaves = save.Hostiles.ToDictionary(item => item.EntityId, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _hostiles.ToArray())
        {
            if (hostileSaves.TryGetValue(pair.Value.Id, out var hostileSave))
            {
                pair.Value.Restore(hostileSave.Health, hostileSave.Resource);
                continue;
            }

            _hostiles.Remove(pair.Key);
            _hostileNames.Remove(pair.Key);
            pair.Key.QueueFree();
        }

        if (hostileSaves.Keys.Any(id => _hostiles.Values.All(item =>
                !string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException("A saved hostile is no longer available in the current scene.");
        }

        ApplyEntityStateSnapshots(save.EntitySnapshots ?? []);

        _hasKey = save.Progress.HasKey;
        _doorUnlocked = save.Progress.DoorUnlocked;
        _bossPhaseTwo = save.Progress.BossPhaseTwo;
        _bossDefeated = save.Progress.BossDefeated;
        _escaped = save.Progress.Escaped;
        _progressionExperience = save.Progression?.Experience ?? 0;
        _progressionLevel = save.Progression?.Level ?? 1;
        if (save.WorldClock is not null)
            _worldClockSnapshot = new WorldClockSnapshot(save.WorldClock.TotalMinutes, save.WorldClock.Paused,
                save.WorldClock.Revision, save.WorldClock.Provenance);
        if (_hasKey && _keyItem is not null && GodotObject.IsInstanceValid(_keyItem))
        {
            _keyItem.QueueFree();
        }

        if (_doorUnlocked && _door is not null && GodotObject.IsInstanceValid(_door))
        {
            _door.QueueFree();
        }
    }

    private IReadOnlyList<RuntimeEntityStateSave> BuildEntityStateSnapshots()
    {
        if (_moduleRuntimePolicies is null) return [];
        var policy = _moduleRuntimePolicies.Extensions.SnapshotPolicy;
        var snapshots = new List<RuntimeEntityStateSave>();
        foreach (var pair in _entityNodes.OrderBy(item => item.Key))
        {
            var components = new List<RuntimeStateComponentSave>();
            if (policy.Components.Contains("transform"))
            {
                var position = pair.Value.GlobalPosition;
                components.Add(new RuntimeStateComponentSave("transform", JsonSerializer.Serialize(new
                {
                    x = position.X,
                    y = position.Y,
                    z = position.Z,
                })));
            }
            var combatant = pair.Value == _player ? _playerCombatant : _hostiles.GetValueOrDefault(pair.Value);
            if (policy.Components.Contains("combat") && combatant is not null)
                components.Add(new RuntimeStateComponentSave("combat", JsonSerializer.Serialize(new
                {
                    health = combatant.Health,
                    resource = combatant.Resource,
                })));
            var isPlayer = pair.Value == _player;
            var inventory = isPlayer && policy.Components.Contains("inventory")
                ? _inventory.Snapshot().Take(policy.MaxInventoryItems)
                    .Select(stack => new RuntimeInventorySaveStack(stack.Slug, stack.Quantity)).ToArray() : [];
            var blackboard = isPlayer && policy.Components.Contains("blackboard")
                ? _blackboard.Snapshot().OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new RuntimeKeyValue(item.Key, item.Value)).ToArray() : [];
            snapshots.Add(new RuntimeEntityStateSave(pair.Key.ToString("D"), 1, components, [], inventory,
                blackboard));
        }
        return snapshots;
    }

    private void ApplyEntityStateSnapshots(IReadOnlyList<RuntimeEntityStateSave> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!Guid.TryParse(snapshot.EntityId, out var id) || !_entityNodes.TryGetValue(id, out var node))
                throw new InvalidOperationException($"Saved entity '{snapshot.EntityId}' is unavailable.");
            var transform = snapshot.Components.SingleOrDefault(item => item.Key == "transform");
            if (transform is null) continue;
            using var json = JsonDocument.Parse(transform.JsonValue);
            var root = json.RootElement;
            node.GlobalPosition = new Vector3(root.GetProperty("x").GetSingle(), root.GetProperty("y").GetSingle(),
                root.GetProperty("z").GetSingle());
            node.ResetPhysicsInterpolation();
        }
    }

    private bool IsNear(Node3D? target, float distance) =>
        _player is not null &&
        target is not null &&
        GodotObject.IsInstanceValid(target) &&
        _player.GlobalPosition.DistanceTo(target.GlobalPosition) <= distance;

}

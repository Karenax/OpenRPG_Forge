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
    private static void AssertSmoke(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private StandardMaterial3D CreateMaterial(Color color, bool emission = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.72f,
            Metallic = 0.08f,
        };
        if (_renderPlan is not null)
        {
            GodotEnvironmentAdapter.ApplyPbrDefaults(material, _renderPlan);
        }

        if (emission)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 2.4f;
        }

        return material;
    }

    private void CreateLabel(Node3D parent, string text, Color color, float height, bool isLocalHero = false)
    {
        var label = new Label3D
        {
            Name = RuntimeWorldLabelNodeName,
            Text = text,
            Position = new Vector3(0, height, 0),
            Modulate = color,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 32,
            OutlineSize = 8,
            FixedSize = false,
        };
        parent.AddChild(label);
        _worldLabels[label] = (color, isLocalHero);
    }

    private static string SafeNodeName(string value) =>
        new(value.Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());

    private static string ArchiveRoot(string savePath) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(savePath))!, ".openrpg-save-archive");

    private static string ArchiveSlot(string savePath)
    {
        var name = Path.GetFileNameWithoutExtension(savePath).ToLowerInvariant();
        var safe = new string(name.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "quick" : safe[..Math.Min(safe.Length, 64)];
    }

    private static int CountTreeNodes(Node root)
    {
        var count = 1;
        foreach (Node child in root.GetChildren())
        {
            count += CountTreeNodes(child);
        }

        return count;
    }

    private static void RegisterInputActions()
    {
        RegisterKeyAction("move_forward", Key.W, Key.Up);
        RegisterKeyAction("move_back", Key.S, Key.Down);
        RegisterKeyAction("move_left", Key.A, Key.Q, Key.Left);
        RegisterKeyAction("move_right", Key.D, Key.Right);
        RegisterKeyAction("interact", Key.E, Key.Key2);
        RegisterKeyAction("attack", Key.Key1);
        RegisterKeyAction("jump", Key.Space);
        RegisterKeyAction("quick_save", Key.F5, Key.Key5);
        RegisterKeyAction("quick_load", Key.F9, Key.Key6);
        RegisterKeyAction("rest", Key.R, Key.Key4);
        RegisterKeyAction("skip_sequence", Key.Escape);
        RegisterKeyAction("action_wheel", Key.Tab, Key.Key3);
        RegisterJoypadAction("action_wheel", JoyButton.Y);
        RegisterKeyAction("tactical_pause", Key.P);
        RegisterJoypadAction("tactical_pause", JoyButton.Start);
        RegisterKeyAction("slow_time", Key.T);
        RegisterKeyAction("inventory", Key.I);
        RegisterKeyAction("character", Key.C);
        RegisterKeyAction("journal", Key.J);
        RegisterKeyAction("module_map", Key.M);
        RegisterKeyAction("combat_mode", Key.V);
        RegisterKeyAction("dive", Key.Ctrl);
        RegisterKeyAction("climb", Key.Shift);
        RegisterKeyAction("camera_left", Key.Home);
        RegisterKeyAction("camera_right", Key.End);
        RegisterModifiedKeyAction("toggle_fps", Key.R, ctrl: true);
    }

    private static void ApplyRuntimeInputProfile(RuntimeInputProfile profile)
    {
        foreach (var action in profile.Actions)
        {
            if (!InputMap.HasAction(action.Key)) InputMap.AddAction(action.Key);
            InputMap.ActionEraseEvents(action.Key);
            foreach (var binding in profile.Bindings.Where(item =>
                         string.Equals(item.ActionKey, action.Key, StringComparison.Ordinal)))
            {
                if (binding.Device == RuntimeInputDevice.KeyboardMouse &&
                    Enum.TryParse<Key>(binding.Binding, true, out var key))
                    InputMap.ActionAddEvent(action.Key, new InputEventKey { PhysicalKeycode = key });
                else if (binding.Device == RuntimeInputDevice.Gamepad &&
                         Enum.TryParse<JoyButton>(binding.Binding, true, out var button))
                    InputMap.ActionAddEvent(action.Key, new InputEventJoypadButton { ButtonIndex = button });
            }
        }
    }

    private static void RegisterModifiedKeyAction(StringName action, Key key, bool ctrl = false,
        bool alt = false, bool shift = false)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        var inputEvent = new InputEventKey
        {
            PhysicalKeycode = key,
            CtrlPressed = ctrl,
            AltPressed = alt,
            ShiftPressed = shift,
        };
        if (!InputMap.ActionHasEvent(action, inputEvent)) InputMap.ActionAddEvent(action, inputEvent);
    }

    private static bool HasKeyBinding(StringName action, Key key, bool ctrl = false) =>
        InputMap.HasAction(action) && InputMap.ActionGetEvents(action)
            .OfType<InputEventKey>()
            .Any(input => input.PhysicalKeycode == key && input.CtrlPressed == ctrl);

    private static void RegisterKeyAction(StringName action, params Key[] keys)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }

        foreach (var key in keys)
        {
            var inputEvent = new InputEventKey { PhysicalKeycode = key };
            if (!InputMap.ActionHasEvent(action, inputEvent))
            {
                InputMap.ActionAddEvent(action, inputEvent);
            }
        }
    }

    private static void RegisterJoypadAction(StringName action, params JoyButton[] buttons)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var button in buttons)
        {
            var inputEvent = new InputEventJoypadButton { ButtonIndex = button };
            if (!InputMap.ActionHasEvent(action, inputEvent)) InputMap.ActionAddEvent(action, inputEvent);
        }
    }

    private sealed class RuntimeAreaChunkLoader : IAreaChunkLoader
    {
        private readonly string _root;
        private readonly string _rootPrefix;

        internal RuntimeAreaChunkLoader(string root)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
            _root = Path.GetFullPath(root);
            _rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public async Task<AreaChunkPayload> LoadAsync(string resourcePath, CancellationToken cancellationToken)
        {
            var normalized = WeatherStateProfile.NormalizeOptionalResource(resourcePath);
            if (normalized.Length == 0)
            {
                throw new InvalidDataException("Area streaming resource path cannot be empty.");
            }

            var fullPath = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Required area resource '{normalized}' is unavailable inside the module.", fullPath);
            }

            var content = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            var dependencyFile = fullPath + ".deps";
            IReadOnlyList<string> dependencies = File.Exists(dependencyFile)
                ? (await File.ReadAllLinesAsync(dependencyFile, cancellationToken).ConfigureAwait(false))
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 && !line.StartsWith('#'))
                    .Select(WeatherStateProfile.NormalizeOptionalResource)
                    .ToArray()
                : [];
            return new AreaChunkPayload(normalized, content, dependencies);
        }
    }

    private sealed class RuntimeConditionEvaluator : IDialogueConditionEvaluator, IQuestConditionEvaluator
    {
        private readonly GameRoot _runtime;

        internal RuntimeConditionEvaluator(GameRoot runtime) => _runtime = runtime;

        public bool IsSatisfied(DialogueCondition condition) => condition.Kind switch
        {
            DialogueConditionKind.VariableEquals =>
                _runtime._blackboard.TryGetValue(condition.Key, out var value) && value == condition.ExpectedValue,
            DialogueConditionKind.HasItem => _runtime.HasInventoryItem(condition.Key),
            DialogueConditionKind.QuestStateEquals =>
                _runtime.QuestStateMatches(condition.Key, condition.ExpectedValue),
            _ => false,
        };

        public bool IsSatisfied(QuestCondition condition) => condition.Kind switch
        {
            QuestConditionKind.VariableEquals =>
                _runtime._playtestQuestVariables.Any(item =>
                    item.Key.EndsWith($":{condition.Key}", StringComparison.Ordinal) &&
                    item.Value == condition.ExpectedValue) ||
                _runtime._blackboard.TryGetValue(condition.Key, out var value) && value == condition.ExpectedValue,
            QuestConditionKind.HasItem => _runtime.HasInventoryItem(condition.Key),
            _ => false,
        };
    }

    private sealed class R068DialogueConditionEvaluator : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => true;
    }

    private enum PrototypeQuestState
    {
        NotStarted,
        Active,
        Completed,
    }
}

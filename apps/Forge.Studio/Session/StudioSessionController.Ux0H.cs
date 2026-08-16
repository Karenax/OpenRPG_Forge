using Forge.Application.Lifecycle;
using Forge.Application.Rules;
using Forge.Domain.Lifecycle;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private static bool TryBuildUx0HPolicies(
        StudioProjectEventConfigurationDraft draft,
        ModuleRuntimePolicyProfile source,
        ProjectWorldProfile ux0GWorld,
        out SequenceAbortPolicy sequenceAbort,
        out ProjectWorldProfile world,
        out ModuleRulesProfile rules,
        out string message)
    {
        sequenceAbort = source.SequenceAbort;
        world = ux0GWorld;
        rules = source.Rules;
        message = string.Empty;
        try
        {
            if (!SameSequenceAbortConfiguration(draft.SequenceAbort, source.SequenceAbort))
            {
                var binding = source.SequenceAbort.Binding with
                {
                    Handler = draft.SequenceAbort.Handler,
                    ScriptPath = NormalizeScriptPath(draft.SequenceAbort.ScriptPath),
                    RequiredAtRuntime = draft.SequenceAbort.RequiredAtRuntime,
                    Provenance = "studio:ux0-h-project",
                };
                sequenceAbort = new SequenceAbortPolicy(source.SequenceAbort.Id,
                    source.SequenceAbort.SchemaVersion, draft.SequenceAbort.Enabled, binding,
                    draft.SequenceAbort.Restore, draft.SequenceAbort.MaximumNarrativeRollbackSteps,
                    "studio:ux0-h-project");
            }

            if (!SameWorldClockConfiguration(draft.WorldClock, source.World.Clock))
            {
                var clock = new WorldClockProfile(
                    source.World.Clock.Id,
                    source.World.Clock.SchemaVersion,
                    "studio:ux0-h-project",
                    draft.WorldClock.DaysPerMonth,
                    draft.WorldClock.MonthsPerYear,
                    draft.WorldClock.HoursPerDay,
                    draft.WorldClock.MinutesPerHour,
                    draft.WorldClock.Speed,
                    draft.WorldClock.Paused,
                    draft.WorldClock.InitialMinute,
                    draft.WorldClock.AreaTimeZones.Where(item => item.Configured).ToDictionary(item => item.AreaKey,
                        item => item.OffsetMinutes, StringComparer.Ordinal),
                    draft.WorldClock.AstronomicalMinutes);
                world = new ProjectWorldProfile(ux0GWorld.Id, ux0GWorld.SchemaVersion,
                    "studio:ux0-h-project", ux0GWorld.Enabled, ux0GWorld.RequiredAtRuntime,
                    ux0GWorld.CustomEvents, clock);
            }

            if (!SameProgressionConfiguration(draft.Progression, source.Rules.Progression))
            {
                var progression = new ProgressionPolicy(
                    source.Rules.Progression.Id,
                    source.Rules.Progression.SchemaVersion,
                    draft.Progression.RulesetId,
                    draft.Progression.Curve,
                    draft.Progression.GroupMultiplier,
                    draft.Progression.DifficultyMultiplier,
                    draft.Progression.Sources.ToDictionary(item => item.Key,
                        item => item.Multiplier, StringComparer.Ordinal),
                    draft.Progression.ShareWithParty,
                    "studio:ux0-h-project");
                rules = new ModuleRulesProfile(source.Rules.Id, source.Rules.SchemaVersion,
                    progression, source.Rules.Difficulties, source.Rules.ActiveDifficultyKey,
                    source.Rules.Mutators, "studio:ux0-h-project", source.Rules.Plugins);
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException)
        {
            message = HumanizeUx0HConstructionFailure(exception);
            return false;
        }
    }

    private static bool SameSequenceAbortConfiguration(
        StudioSequenceAbortDraft draft,
        SequenceAbortPolicy source) =>
        draft.Enabled == source.Enabled && draft.Restore == source.Restore &&
        draft.MaximumNarrativeRollbackSteps == source.MaximumNarrativeRollbackSteps &&
        draft.Handler == source.Binding.Handler &&
        NormalizeScriptPath(draft.ScriptPath) == source.Binding.ScriptPath &&
        draft.RequiredAtRuntime == source.Binding.RequiredAtRuntime;

    private static bool SameWorldClockConfiguration(
        StudioWorldClockDraft draft,
        WorldClockProfile source) =>
        draft.DaysPerMonth == source.DaysPerMonth && draft.MonthsPerYear == source.MonthsPerYear &&
        draft.HoursPerDay == source.HoursPerDay && draft.MinutesPerHour == source.MinutesPerHour &&
        draft.Speed.Equals(source.Speed) && draft.Paused == source.Paused &&
        draft.InitialMinute == source.InitialMinute &&
        draft.AreaTimeZones.Count(item => item.Configured) == source.AreaTimeZoneOffsets.Count &&
        draft.AreaTimeZones.Where(item => item.Configured).All(item =>
            source.AreaTimeZoneOffsets.TryGetValue(item.AreaKey, out var value) && value == item.OffsetMinutes) &&
        draft.AstronomicalMinutes.Count == source.AstronomicalMinutes.Count &&
        draft.AstronomicalMinutes.All(item => source.AstronomicalMinutes.TryGetValue(item.Key, out var value) &&
                                               value == item.Value);

    private static bool SameProgressionConfiguration(
        StudioProgressionDraft draft,
        ProgressionPolicy source) =>
        draft.RulesetId == source.RulesetId && draft.GroupMultiplier.Equals(source.GroupMultiplier) &&
        draft.DifficultyMultiplier.Equals(source.DifficultyMultiplier) &&
        draft.ShareWithParty == source.ShareWithParty && draft.Curve.SequenceEqual(source.Curve) &&
        draft.Sources.Count == source.SourceMultipliers.Count &&
        draft.Sources.All(item => source.SourceMultipliers.TryGetValue(item.Key, out var value) &&
                                  value.Equals(item.Multiplier));

    private static string HumanizeUx0HConstructionFailure(Exception exception) => exception switch
    {
        ArgumentOutOfRangeException =>
            "Vérifiez les restaurations, les paliers d’expérience et les multiplicateurs : une valeur n’est pas sûre.",
        _ => "Vérifiez le calendrier, les horaires, les zones et la progression avant de réessayer.",
    };

    internal StudioUx0HSmokeResult RunUx0HContractSmoke()
    {
        if (Project is null)
        {
            return EmptyUx0HSmokeResult();
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux0h-" +
            Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(tempRoot, "world-transition-workspace");
        var controller = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(workspace, Project);
            if (!created.Succeeded || created.Session is null)
            {
                return EmptyUx0HSmokeResult();
            }
            var scriptsDirectory = Path.Combine(workspace, "scripts");
            Directory.CreateDirectory(scriptsDirectory);
            const string relativeScript = "scripts/ux0_h_sequence_abort.forge";
            File.WriteAllText(Path.Combine(workspace, relativeScript.Replace('/', Path.DirectorySeparatorChar)),
                "void main() { }" + Environment.NewLine);
            if (!controller.Open(workspace).Succeeded)
            {
                return EmptyUx0HSmokeResult();
            }

            var beforeProject = controller.Project!;
            var beforePolicies = beforeProject.Module.RuntimePolicies;
            var source = controller.CaptureProjectEventConfiguration();
            var firstZone = source.WorldClock.AreaTimeZones.First();
            var candidate = source with
            {
                SequenceAbort = source.SequenceAbort with
                {
                    Enabled = true,
                    Restore = SequenceAbortRestoreFlags.All,
                    MaximumNarrativeRollbackSteps = 32,
                    Handler = "main",
                    ScriptPath = relativeScript,
                    RequiredAtRuntime = true,
                },
                WorldClock = source.WorldClock with
                {
                    DaysPerMonth = 30,
                    MonthsPerYear = 10,
                    HoursPerDay = 20,
                    MinutesPerHour = 50,
                    Speed = 2.5,
                    Paused = false,
                    InitialMinute = 95,
                    AreaTimeZones = source.WorldClock.AreaTimeZones.Select(item =>
                        item.AreaKey == firstZone.AreaKey
                            ? item with { OffsetMinutes = 25, Configured = true }
                            : item).ToArray(),
                    AstronomicalMinutes = new Dictionary<WorldAstronomicalEventKind, int>
                    {
                        [WorldAstronomicalEventKind.Dawn] = 125,
                        [WorldAstronomicalEventKind.Sunrise] = 150,
                        [WorldAstronomicalEventKind.Sunset] = 750,
                        [WorldAstronomicalEventKind.Dusk] = 800,
                        [WorldAstronomicalEventKind.Midnight] = 0,
                    },
                },
                Progression = source.Progression with
                {
                    Curve =
                    [
                        new ProgressionCurvePoint(1, 0),
                        new ProgressionCurvePoint(2, 120),
                        new ProgressionCurvePoint(3, 360),
                        new ProgressionCurvePoint(4, 840),
                    ],
                    GroupMultiplier = 1.2,
                    DifficultyMultiplier = .9,
                    Sources =
                    [
                        new StudioProgressionSourceDraft("combat", "Combat", 1.1),
                        new StudioProgressionSourceDraft("exploration", "Exploration", .8),
                        new StudioProgressionSourceDraft("quest", "Quêtes", 1.4),
                    ],
                    ShareWithParty = true,
                },
            };

            var missing = controller.PreviewProjectEventConfiguration(candidate with
            {
                SequenceAbort = candidate.SequenceAbort with
                {
                    ScriptPath = "scripts/missing-sequence-abort.forge",
                },
            });
            var invalidClock = controller.PreviewProjectEventConfiguration(candidate with
            {
                WorldClock = candidate.WorldClock with { DaysPerMonth = 0 },
            });
            var invalidProgression = controller.PreviewProjectEventConfiguration(candidate with
            {
                Progression = candidate.Progression with
                {
                    Curve =
                    [
                        new ProgressionCurvePoint(1, 0),
                        new ProgressionCurvePoint(2, 100),
                        new ProgressionCurvePoint(3, 90),
                    ],
                },
            });
            var missingScriptBlocked = !missing.Succeeded;
            var invalidValuesBlocked = !invalidClock.Succeeded && !invalidProgression.Succeeded;
            var humanDiagnostics = missingScriptBlocked && invalidValuesBlocked &&
                                   HumanUx0HMessage(missing.Message) && HumanUx0HMessage(invalidClock.Message) &&
                                   HumanUx0HMessage(invalidProgression.Message);

            var preview = controller.PreviewProjectEventConfiguration(candidate);
            var dryRun = preview.Succeeded && ReferenceEquals(beforeProject, controller.Project);
            var applied = controller.ApplyProjectEventConfiguration(candidate);
            var policies = controller.Project!.Module.RuntimePolicies;
            var abort = policies.SequenceAbort;
            var sequenceAbort = applied.Succeeded && abort.Enabled && abort.Restore == SequenceAbortRestoreFlags.All &&
                                abort.MaximumNarrativeRollbackSteps == 32 &&
                                abort.Binding.ScriptPath == relativeScript && abort.Binding.Handler == "main" &&
                                abort.Binding.RequiredAtRuntime && abort.Binding.Authority == EventAuthorityScope.Server;
            var clock = policies.World.Clock;
            var worldClock = clock.DaysPerMonth == 30 && clock.MonthsPerYear == 10 &&
                             clock.HoursPerDay == 20 && clock.MinutesPerHour == 50 &&
                             clock.Speed.Equals(2.5) && clock.InitialMinute == 95 &&
                             clock.AreaTimeZoneOffsets[firstZone.AreaKey] == 25 &&
                             clock.AstronomicalMinutes[WorldAstronomicalEventKind.Dawn] == 125;
            var progression = policies.Rules.Progression;
            var progressionConfigured = progression.Curve.Count == 4 &&
                                        progression.Curve[^1] == new ProgressionCurvePoint(4, 840) &&
                                        progression.GroupMultiplier.Equals(1.2) &&
                                        progression.DifficultyMultiplier.Equals(.9) &&
                                        progression.SourceMultipliers["quest"].Equals(1.4) &&
                                        progression.ShareWithParty;

            var snapshot = new SequenceRuntimeState("camera.exploration", [], new HashSet<string>(),
                new HashSet<string>(), 2, Guid.NewGuid().ToString("D"), false);
            var active = snapshot with
            {
                CameraId = "camera.cinematic",
                ControlStack = [new Forge.Domain.Gameplay.ControlContext(Guid.NewGuid(), 1,
                    "studio:ux0-h", "cinematic", false, "cinematic", [])],
                LockIds = new HashSet<string> { "door.story" },
                NarrativeRevision = 12,
                SequenceActive = true,
            };
            var abortResult = SequenceAbortCoordinator.Abort(abort, active, snapshot);
            var initialClock = CustomWorldEventBus.CreateInitialSnapshot(clock);
            var clockResult = CustomWorldEventBus.AdvanceClock(clock, initialClock, 2, firstZone.AreaKey);
            var progressionResult = RulesRuntimeService.ResolveProgression(progression, 200, "quest", 2, 0);
            var runtimeExecution = abortResult.Accepted && abortResult.CameraRestored &&
                                   abortResult.NarrativeRolledBack &&
                                   clockResult.CrossedEvents.Contains(WorldAstronomicalEventKind.Dawn) &&
                                   progressionResult.AwardedExperience > 0 && progressionResult.Level >= 2;

            var neighborPoliciesPreserved = ReferenceEquals(beforePolicies.Cinematics, policies.Cinematics) &&
                                            ReferenceEquals(beforePolicies.Gameplay, policies.Gameplay) &&
                                            ReferenceEquals(beforePolicies.Extensions, policies.Extensions) &&
                                            ReferenceEquals(beforePolicies.Rest, policies.Rest) &&
                                            beforePolicies.Rules.Difficulties.SequenceEqual(
                                                policies.Rules.Difficulties) &&
                                            ReferenceEquals(beforePolicies.Rules.Mutators, policies.Rules.Mutators) &&
                                            ReferenceEquals(beforePolicies.Rules.Plugins, policies.Rules.Plugins) &&
                                            policies.World.CustomEvents.SequenceEqual(beforePolicies.World.CustomEvents);

            var undo = controller.Undo();
            var undone = undo.Succeeded && ReferenceEquals(beforeProject, controller.Project);
            var redo = controller.Redo();
            var redone = redo.Succeeded && controller.Project!.Module.RuntimePolicies.SequenceAbort.Binding.ScriptPath ==
                relativeScript && controller.Project.Module.RuntimePolicies.World.Clock.DaysPerMonth == 30 &&
                controller.Project.Module.RuntimePolicies.Rules.Progression.Curve.Count == 4;
            var atomicUndoRedo = applied.Succeeded && undone && redone;

            var saved = controller.Save();
            controller.Close();
            var opened = reopened.Open(workspace);
            var persisted = reopened.Project?.Module.RuntimePolicies;
            var persistenceRoundTrip = saved.Succeeded && opened.Succeeded && persisted is not null &&
                                       persisted.SequenceAbort.Binding.ScriptPath == relativeScript &&
                                       persisted.World.Clock.DaysPerMonth == 30 &&
                                       persisted.World.Clock.AreaTimeZoneOffsets[firstZone.AreaKey] == 25 &&
                                       persisted.Rules.Progression.Curve[^1] ==
                                       new ProgressionCurvePoint(4, 840) &&
                                       persisted.Cinematics.Startup == beforePolicies.Cinematics.Startup;

            var succeeded = sequenceAbort && worldClock && progressionConfigured && runtimeExecution &&
                            missingScriptBlocked && invalidValuesBlocked && dryRun && atomicUndoRedo &&
                            persistenceRoundTrip && neighborPoliciesPreserved && humanDiagnostics;
            return new StudioUx0HSmokeResult(succeeded, sequenceAbort, worldClock, progressionConfigured,
                runtimeExecution, missingScriptBlocked, invalidValuesBlocked, dryRun, atomicUndoRedo,
                persistenceRoundTrip, neighborPoliciesPreserved, humanDiagnostics);
        }
        finally
        {
            controller.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static bool HumanUx0HMessage(string message) =>
        !message.Contains("scripts/", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("module/", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("hash", StringComparison.OrdinalIgnoreCase);

    private static StudioUx0HSmokeResult EmptyUx0HSmokeResult() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false);
}

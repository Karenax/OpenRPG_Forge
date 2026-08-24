using Forge.Application.Lifecycle;
using Forge.Domain.Lifecycle;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private static IEnumerable<(string? ScriptPath, string Handler, bool RequiredAtRuntime)>
        Ux0GScriptReferences(StudioProjectEventConfigurationDraft draft) =>
        draft.RestBindings.Select(item => (item.ScriptPath, item.Handler, item.RequiredAtRuntime))
            .Concat(draft.Heartbeats.Select(item =>
                (item.ScriptPath, item.Handler, item.RequiredAtRuntime)))
            .Concat(draft.CustomWorldEvents.Select(item =>
                (item.ScriptPath, item.Handler, item.RequiredAtRuntime)));

    private static bool TryBuildUx0GPolicies(
        StudioProjectEventConfigurationDraft draft,
        ModuleRuntimePolicyProfile source,
        out IReadOnlyList<HeartbeatScheduleDefinition> heartbeats,
        out RestLifecycleProfile rest,
        out ProjectWorldProfile world,
        out string message)
    {
        heartbeats = source.Heartbeats;
        rest = source.Rest;
        world = source.World;
        message = string.Empty;
        try
        {
            if (!SameRestConfiguration(draft, source.Rest))
            {
                var configured = draft.RestBindings.ToDictionary(item => item.EventName, StringComparer.Ordinal);
                if (configured.Count != source.Rest.Bindings.Count)
                {
                    message = "Rechargez les événements de repos : leur liste a changé.";
                    return false;
                }
                var bindings = source.Rest.Bindings.Select(binding =>
                {
                    var item = configured[binding.EventName];
                    return binding with
                    {
                        Handler = item.Handler,
                        ScriptPath = NormalizeScriptPath(item.ScriptPath),
                        RequiredAtRuntime = item.RequiredAtRuntime,
                        Provenance = "studio:ux0-g-events",
                    };
                }).ToArray();
                rest = new RestLifecycleProfile(source.Rest.Id, source.Rest.SchemaVersion, draft.RestEnabled,
                    draft.RestPartyPolicy, draft.RestDurationSeconds, bindings, "studio:ux0-g-events");
            }

            if (!SameHeartbeatConfiguration(draft.Heartbeats, source.Heartbeats))
            {
                heartbeats = draft.Heartbeats.Select(item => new HeartbeatScheduleDefinition(
                    item.Id,
                    HeartbeatScheduleDefinition.CurrentSchemaVersion,
                    item.Name,
                    HeartbeatScope.Module,
                    item.Enabled,
                    item.IntervalMilliseconds,
                    item.BudgetMilliseconds,
                    item.SuspendWhenInactive,
                    "1.0",
                    item.Handler,
                    "studio:ux0-g-events",
                    NormalizeScriptPath(item.ScriptPath),
                    item.RequiredAtRuntime)).ToArray();
            }

            if (!SameWorldConfiguration(draft, source.World))
            {
                var customEvents = draft.CustomWorldEvents.Select(item => new CustomEventSchema(
                    item.Id,
                    CustomEventSchema.CurrentSchemaVersion,
                    "studio:ux0-g-events",
                    item.Namespace,
                    item.Name,
                    item.PayloadVersion,
                    item.Fields,
                    item.RequiredExtension,
                    "1.0",
                    item.Handler,
                    EventAuthorityScope.Server,
                    NormalizeScriptPath(item.ScriptPath),
                    item.RequiredAtRuntime)).ToArray();
                world = new ProjectWorldProfile(source.World.Id, source.World.SchemaVersion,
                    "studio:ux0-g-events", draft.WorldEventsEnabled, draft.WorldEventsRequiredAtRuntime,
                    customEvents, source.World.Clock);
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException)
        {
            message = HumanizeUx0GConstructionFailure(exception);
            return false;
        }
    }

    private static bool SameRestConfiguration(
        StudioProjectEventConfigurationDraft draft,
        RestLifecycleProfile source)
    {
        if (draft.RestEnabled != source.Enabled || draft.RestPartyPolicy != source.PartyPolicy ||
            draft.RestDurationSeconds != source.DefaultDurationSeconds ||
            draft.RestBindings.Count != source.Bindings.Count)
        {
            return false;
        }
        var byName = draft.RestBindings.ToDictionary(item => item.EventName, StringComparer.Ordinal);
        return source.Bindings.All(binding => byName.TryGetValue(binding.EventName, out var item) &&
            item.Handler == binding.Handler && NormalizeScriptPath(item.ScriptPath) == binding.ScriptPath &&
            item.RequiredAtRuntime == binding.RequiredAtRuntime);
    }

    private static bool SameHeartbeatConfiguration(
        IReadOnlyList<StudioHeartbeatScheduleDraft> draft,
        IReadOnlyList<HeartbeatScheduleDefinition> source)
    {
        if (draft.Count != source.Count)
        {
            return false;
        }
        var byId = draft.ToDictionary(item => item.Id);
        return source.All(schedule => byId.TryGetValue(schedule.Id, out var item) &&
            item.Name.Trim() == schedule.Name && item.Enabled == schedule.Enabled &&
            item.IntervalMilliseconds == schedule.IntervalMilliseconds &&
            item.BudgetMilliseconds == schedule.BudgetMilliseconds &&
            item.SuspendWhenInactive == schedule.SuspendWhenInactive && item.Handler == schedule.Handler &&
            NormalizeScriptPath(item.ScriptPath) == schedule.ScriptPath &&
            item.RequiredAtRuntime == schedule.RequiredAtRuntime);
    }

    private static bool SameWorldConfiguration(
        StudioProjectEventConfigurationDraft draft,
        ProjectWorldProfile source)
    {
        if (draft.WorldEventsEnabled != source.Enabled ||
            draft.WorldEventsRequiredAtRuntime != source.RequiredAtRuntime ||
            draft.CustomWorldEvents.Count != source.CustomEvents.Count)
        {
            return false;
        }
        var byId = draft.CustomWorldEvents.ToDictionary(item => item.Id);
        return source.CustomEvents.All(schema => byId.TryGetValue(schema.Id, out var item) &&
            item.Namespace == schema.Namespace && item.Name == schema.Name &&
            item.PayloadVersion == schema.PayloadVersion &&
            item.RequiredExtension == schema.RequiredExtension &&
            item.Handler == schema.Handler && NormalizeScriptPath(item.ScriptPath) == schema.ScriptPath &&
            item.RequiredAtRuntime == schema.RequiredAtRuntime &&
            DictionaryEqual(item.Fields, schema.Fields));
    }

    private static bool DictionaryEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count && left.All(pair =>
            right.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static string HumanizeUx0GConstructionFailure(Exception exception) => exception switch
    {
        ArgumentOutOfRangeException =>
            "Vérifiez le rythme, le budget et la durée : une valeur dépasse les limites sûres.",
        _ => "Vérifiez les noms, les scripts et les options des événements globaux.",
    };

    internal StudioUx0GSmokeResult RunUx0GContractSmoke()
    {
        if (Project is null)
        {
            return EmptyUx0GSmokeResult();
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux0g-" +
            Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(tempRoot, "global-event-workspace");
        var controller = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(workspace, Project);
            if (!created.Succeeded || created.Session is null)
            {
                return EmptyUx0GSmokeResult();
            }
            var scriptsDirectory = Path.Combine(workspace, "scripts");
            Directory.CreateDirectory(scriptsDirectory);
            const string relativeScript = "scripts/ux0_g_global_events.forge";
            File.WriteAllText(Path.Combine(workspace, relativeScript.Replace('/', Path.DirectorySeparatorChar)),
                "void main() { }" + Environment.NewLine);
            if (!controller.Open(workspace).Succeeded)
            {
                return EmptyUx0GSmokeResult();
            }

            var selected = controller.FindForgeScripts().SingleOrDefault(item =>
                item.RelativePath == relativeScript);
            if (selected is null)
            {
                return EmptyUx0GSmokeResult();
            }

            var beforeProject = controller.Project!;
            var beforePolicies = beforeProject.Module.RuntimePolicies;
            var source = controller.CaptureProjectEventConfiguration();
            var gameplay = source.GameplayBindings.Select(binding =>
                binding.EventName is "equipment.changed" or "progression.changed"
                    ? binding with
                    {
                        Handler = "main",
                        ScriptPath = relativeScript,
                        RequiredAtRuntime = binding.EventName == "progression.changed",
                    }
                    : binding).ToArray();
            var restBindings = source.RestBindings.Select(binding => binding with
            {
                Handler = "main",
                ScriptPath = relativeScript,
                RequiredAtRuntime = binding.EventName == "rest.completed",
            }).ToArray();
            var heartbeatId = Guid.NewGuid();
            var heartbeat = new StudioHeartbeatScheduleDraft(
                heartbeatId, "Simulation du monde", true, 1_500, 25, true,
                "main", "OnGlobalHeartbeat", relativeScript, true);
            var customId = Guid.NewGuid();
            var customName = UniqueCustomEventName(source.CustomWorldEvents, "studio_signal");
            var custom = new StudioCustomWorldEventDraft(
                customId, "module", customName, 1,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "string" },
                false, "main", "OnCustomWorldEvent", relativeScript, true);
            var candidate = source with
            {
                GameplayBindings = gameplay,
                ReplicateEquipmentAndProgression = false,
                RestEnabled = true,
                RestPartyPolicy = PartyRestPolicy.WholeParty,
                RestDurationSeconds = 12,
                RestBindings = restBindings,
                Heartbeats = source.Heartbeats.Append(heartbeat).ToArray(),
                WorldEventsEnabled = true,
                CustomWorldEvents = source.CustomWorldEvents.Append(custom).ToArray(),
            };
            var missing = controller.PreviewProjectEventConfiguration(candidate with
            {
                Heartbeats = candidate.Heartbeats.Select(item => item.Id == heartbeatId
                    ? item with { ScriptPath = "scripts/missing-global-event.forge" }
                    : item).ToArray(),
            });
            var missingScriptBlocked = !missing.Succeeded;
            var humanDiagnostics = missingScriptBlocked &&
                                   !missing.Message.Contains("scripts/", StringComparison.OrdinalIgnoreCase) &&
                                   !missing.Message.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                                   !missing.Message.Contains("hash", StringComparison.OrdinalIgnoreCase);

            var preview = controller.PreviewProjectEventConfiguration(candidate);
            var dryRun = preview.Succeeded && ReferenceEquals(beforeProject, controller.Project);
            var applied = controller.ApplyProjectEventConfiguration(candidate);
            var policies = controller.Project!.Module.RuntimePolicies;
            var equipmentAndProgression = applied.Succeeded && !policies.GameplayEvents
                .ReplicateEquipmentAndProgression && policies.GameplayEvents.Bindings
                .Where(item => item.EventName is "equipment.changed" or "progression.changed")
                .All(item => item.ScriptPath == relativeScript && item.Handler == "main" &&
                             item.Authority == EventAuthorityScope.Server);
            var restLifecycle = policies.Rest.Enabled && policies.Rest.PartyPolicy == PartyRestPolicy.WholeParty &&
                                policies.Rest.DefaultDurationSeconds == 12 &&
                                policies.Rest.Bindings.Count == 4 && policies.Rest.Bindings.All(item =>
                                    item.ScriptPath == relativeScript && item.Handler == "main");
            var persistedHeartbeat = policies.Heartbeats.SingleOrDefault(item => item.Id == heartbeatId);
            var globalHeartbeat = persistedHeartbeat is
            {
                Scope: HeartbeatScope.Module,
                IntervalMilliseconds: 1_500,
                BudgetMilliseconds: 25,
                ScriptPath: relativeScript,
                Handler: "main",
                RequiredAtRuntime: true,
            };
            var persistedCustom = policies.World.CustomEvents.SingleOrDefault(item => item.Id == customId);
            var userEvent = persistedCustom is
            {
                Namespace: "module",
                PayloadVersion: 1,
                ScriptPath: relativeScript,
                Handler: "main",
                RequiredAtRuntime: true,
                Authority: EventAuthorityScope.Server,
            } && persistedCustom.Fields.TryGetValue("message", out var type) && type == "string";

            var dispatch = new HeartbeatScheduler().Advance([persistedHeartbeat!], 1_500, true)
                .Dispatches.SingleOrDefault();
            var bus = new CustomWorldEventBus();
            bus.Register(policies.World);
            var published = bus.Publish(customId, 1,
                new Dictionary<string, string> { ["message"] = "bonjour" }, DateTimeOffset.UnixEpoch);
            var runtimeDispatch = dispatch?.ScriptPath == relativeScript && dispatch.Handler == "main" &&
                                  dispatch.RequiredAtRuntime && published.SchemaId == customId &&
                                  published.QualifiedName == $"module.{customName}";

            var neighborPoliciesPreserved = ReferenceEquals(beforePolicies.Extensions, policies.Extensions) &&
                                            ReferenceEquals(beforePolicies.Rules, policies.Rules) &&
                                            ReferenceEquals(beforePolicies.Gameplay, policies.Gameplay) &&
                                            ReferenceEquals(beforePolicies.Cinematics, policies.Cinematics) &&
                                            ReferenceEquals(beforePolicies.SequenceAbort, policies.SequenceAbort) &&
                                            ReferenceEquals(beforePolicies.World.Clock, policies.World.Clock) &&
                                            policies.LifecycleEvents.Bindings.Select(item =>
                                                    (item.Event, item.Authority, item.Handler, item.ScriptPath,
                                                        item.RequiredAtRuntime))
                                                .SequenceEqual(beforePolicies.LifecycleEvents.Bindings.Select(item =>
                                                    (item.Event, item.Authority, item.Handler, item.ScriptPath,
                                                        item.RequiredAtRuntime)));

            var undo = controller.Undo();
            var undone = undo.Succeeded && controller.Project!.Module.RuntimePolicies.Heartbeats
                .All(item => item.Id != heartbeatId) && controller.Project.Module.RuntimePolicies.World.CustomEvents
                .All(item => item.Id != customId);
            var redo = controller.Redo();
            var redone = redo.Succeeded && controller.Project!.Module.RuntimePolicies.Heartbeats
                .Any(item => item.Id == heartbeatId) && controller.Project.Module.RuntimePolicies.World.CustomEvents
                .Any(item => item.Id == customId);
            var atomicUndoRedo = applied.Succeeded && undone && redone;

            var saved = controller.Save();
            controller.Close();
            var opened = reopened.Open(workspace);
            var persisted = reopened.Project?.Module.RuntimePolicies;
            var persistenceRoundTrip = saved.Succeeded && opened.Succeeded && persisted is not null &&
                                       persisted.Rest.Bindings.All(item => item.ScriptPath == relativeScript) &&
                                       persisted.Heartbeats.Single(item => item.Id == heartbeatId).ScriptPath ==
                                       relativeScript && persisted.World.CustomEvents
                                           .Single(item => item.Id == customId).ScriptPath == relativeScript;

            var succeeded = equipmentAndProgression && restLifecycle && globalHeartbeat && userEvent &&
                            runtimeDispatch && missingScriptBlocked && dryRun && atomicUndoRedo &&
                            persistenceRoundTrip && neighborPoliciesPreserved && humanDiagnostics;
            return new StudioUx0GSmokeResult(succeeded, equipmentAndProgression, restLifecycle,
                globalHeartbeat, userEvent, runtimeDispatch, missingScriptBlocked, dryRun, atomicUndoRedo,
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

    private static StudioUx0GSmokeResult EmptyUx0GSmokeResult() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false);

    private static string UniqueCustomEventName(
        IReadOnlyList<StudioCustomWorldEventDraft> existing,
        string requested)
    {
        var names = existing.Where(item => item.Namespace == "module")
            .Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        if (!names.Contains(requested))
        {
            return requested;
        }
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = $"{requested}_{index}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
        throw new InvalidOperationException("Impossible de créer un nom d’événement utilisateur unique.");
    }
}

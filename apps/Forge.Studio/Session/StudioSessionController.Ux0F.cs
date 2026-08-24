using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private static readonly IReadOnlySet<string> Ux0FGameplayEvents = new HashSet<string>(
        [
            "player.joining",
            "player.ready",
            "player.leaving",
            "player.disconnected",
            "inventory.changed",
            "item.used",
            "ability.activated",
            "life.downed",
            "life.dying",
            "life.dead",
            "life.respawnrequested",
            "life.respawned",
        ],
        StringComparer.Ordinal);

    private readonly SetModuleRuntimePoliciesHandler _setModuleRuntimePolicies = new(new SystemClock());

    internal IReadOnlyList<StudioEventScriptChoice> FindForgeScripts()
    {
        if (Session is null)
        {
            return [];
        }

        var scriptsRoot = Path.Combine(Session.RootDirectory, "scripts");
        if (!Directory.Exists(scriptsRoot))
        {
            return [];
        }

        try
        {
            var candidates = Directory.EnumerateFiles(scriptsRoot, "*.forge", SearchOption.AllDirectories)
                .Select(path =>
                {
                    var relative = Path.GetRelativePath(Session.RootDirectory, path).Replace('\\', '/');
                    return new StudioEventScriptChoice(relative, HumanScriptName(relative));
                })
                .Where(item => TryResolveForgeScript(item.RelativePath, requireExisting: true, out var path) &&
                               HasForgeScriptHandler(path, "main"))
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToArray();
            var duplicateNames = candidates.GroupBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            return candidates.Select(item => duplicateNames.Contains(item.DisplayName)
                    ? item with { DisplayName = $"{item.DisplayName} · {HumanScriptFolder(item.RelativePath)}" }
                    : item)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    internal StudioProjectEventConfigurationDraft CaptureProjectEventConfiguration()
    {
        var project = Project ?? throw new InvalidOperationException("No project is open.");
        var policies = project.Module.RuntimePolicies;
        var lifecycleDefaults = ProjectLifecycleEventProfile.CreateDefault(project.Module.Id).Bindings
            .ToDictionary(binding => (binding.Event, binding.Authority));
        var gameplayDefaults = ProjectGameplayEventProfile.CreateDefault(project.Module.Id.Value).Bindings
            .ToDictionary(binding => binding.EventName, StringComparer.Ordinal);
        var restDefaults = ModuleRuntimePolicyProfile.CreateDefault(project.Module.Id).Rest.Bindings
            .ToDictionary(binding => binding.EventName, StringComparer.Ordinal);
        var abortDefault = ModuleRuntimePolicyProfile.CreateDefault(project.Module.Id).SequenceAbort;
        var clock = policies.World.Clock;
        var areaTimeZones = project.Areas
            .OrderBy(area => area.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(area => new StudioWorldTimeZoneDraft(
                area.Id.ToString(),
                area.DisplayName,
                clock.AreaTimeZoneOffsets.GetValueOrDefault(area.Id.ToString(), 0),
                clock.AreaTimeZoneOffsets.ContainsKey(area.Id.ToString())))
            .ToArray();
        var progression = policies.Rules.Progression;
        return new StudioProjectEventConfigurationDraft(
            policies.LifecycleEvents.Enabled,
            policies.LifecycleEvents.RequiredAtRuntime,
            policies.LifecycleEvents.Bindings.Select(binding => new StudioLifecycleEventBindingDraft(
                binding.Event,
                binding.Authority,
                binding.Handler,
                lifecycleDefaults[(binding.Event, binding.Authority)].Handler,
                binding.ScriptPath,
                binding.RequiredAtRuntime)).ToArray(),
            policies.GameplayEvents.Enabled,
            policies.GameplayEvents.RequiredAtRuntime,
            policies.GameplayEvents.UseDownedState,
            policies.GameplayEvents.AutoRespawn,
            policies.GameplayEvents.ReplicateEquipmentAndProgression,
            policies.GameplayEvents.Bindings.Select(binding => new StudioGameplayEventBindingDraft(
                binding.EventName,
                binding.Handler,
                gameplayDefaults[binding.EventName].Handler,
                binding.ScriptPath,
                binding.RequiredAtRuntime)).ToArray(),
            policies.Rest.Enabled,
            policies.Rest.PartyPolicy,
            policies.Rest.DefaultDurationSeconds,
            policies.Rest.Bindings.Select(binding => new StudioGameplayEventBindingDraft(
                binding.EventName,
                binding.Handler,
                restDefaults[binding.EventName].Handler,
                binding.ScriptPath,
                binding.RequiredAtRuntime)).ToArray(),
            policies.Heartbeats.Select(schedule => new StudioHeartbeatScheduleDraft(
                schedule.Id,
                schedule.Name,
                schedule.Enabled,
                schedule.IntervalMilliseconds,
                schedule.BudgetMilliseconds,
                schedule.SuspendWhenInactive,
                schedule.Handler,
                schedule.ScriptPath is null ? schedule.Handler : "OnGlobalHeartbeat",
                schedule.ScriptPath,
                schedule.RequiredAtRuntime)).ToArray(),
            policies.World.Enabled,
            policies.World.RequiredAtRuntime,
            policies.World.CustomEvents.Select(schema => new StudioCustomWorldEventDraft(
                schema.Id,
                schema.Namespace,
                schema.Name,
                schema.PayloadVersion,
                schema.Fields,
                schema.RequiredExtension,
                schema.Handler,
                schema.ScriptPath is null ? schema.Handler : "OnCustomWorldEvent",
                schema.ScriptPath,
                schema.RequiredAtRuntime)).ToArray(),
            new StudioSequenceAbortDraft(
                policies.SequenceAbort.Enabled,
                policies.SequenceAbort.Restore,
                policies.SequenceAbort.MaximumNarrativeRollbackSteps,
                policies.SequenceAbort.Binding.Handler,
                abortDefault.Binding.Handler,
                policies.SequenceAbort.Binding.ScriptPath,
                policies.SequenceAbort.Binding.RequiredAtRuntime),
            new StudioWorldClockDraft(
                clock.DaysPerMonth,
                clock.MonthsPerYear,
                clock.HoursPerDay,
                clock.MinutesPerHour,
                clock.Speed,
                clock.Paused,
                clock.InitialMinute,
                areaTimeZones,
                clock.AstronomicalMinutes),
            new StudioProgressionDraft(
                progression.RulesetId,
                HumanRulesetName(progression.RulesetId),
                progression.Curve,
                progression.GroupMultiplier,
                progression.DifficultyMultiplier,
                progression.SourceMultipliers.Select(item => new StudioProgressionSourceDraft(
                    item.Key,
                    HumanProgressionSourceName(item.Key),
                    item.Value)).ToArray(),
                progression.ShareWithParty));
    }

    internal StudioProjectEventPreview PreviewProjectEventConfiguration(
        StudioProjectEventConfigurationDraft draft) =>
        MutateProjectEventConfiguration(draft, apply: false);

    internal StudioProjectEventPreview ApplyProjectEventConfiguration(
        StudioProjectEventConfigurationDraft draft) =>
        MutateProjectEventConfiguration(draft, apply: true);

    internal StudioUx0FSmokeResult RunUx0FContractSmoke()
    {
        if (Project is null)
        {
            return new StudioUx0FSmokeResult(false, false, false, false, false, false, false, false,
                false, false, false);
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux0f-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(tempRoot, "event-workspace");
        var controller = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(workspace, Project);
            if (!created.Succeeded || created.Session is null)
            {
                return new StudioUx0FSmokeResult(false, false, false, false, false, false, false, false,
                    false, false, false);
            }
            var scriptsDirectory = Path.Combine(workspace, "scripts");
            Directory.CreateDirectory(scriptsDirectory);
            File.WriteAllText(Path.Combine(scriptsDirectory, "ux0_f_event_flow.forge"),
                "void main() { }" + Environment.NewLine);
            File.WriteAllText(Path.Combine(scriptsDirectory, "not_an_event_entry.forge"),
                "void helper() { }" + Environment.NewLine);
            if (!controller.Open(workspace).Succeeded)
            {
                return new StudioUx0FSmokeResult(false, false, false, false, false, false, false, false,
                    false, false, false);
            }

            var scripts = controller.FindForgeScripts();
            var selected = scripts.SingleOrDefault(item => item.RelativePath == "scripts/ux0_f_event_flow.forge");
            var scriptDiscovery = scripts.Count == 1 && selected is not null &&
                                  selected.DisplayName.Contains("Ux0 F Event Flow",
                StringComparison.OrdinalIgnoreCase) && !selected.DisplayName.Contains("scripts/", StringComparison.Ordinal);
            if (selected is null)
            {
                return new StudioUx0FSmokeResult(false, scriptDiscovery, false, false, false, false, false,
                    false, false, false, false);
            }

            var beforeProject = controller.Project!;
            var beforePolicies = beforeProject.Module.RuntimePolicies;
            var source = controller.CaptureProjectEventConfiguration();
            var lifecycle = source.LifecycleBindings.Select(binding => binding with
            {
                Handler = "main",
                ScriptPath = selected.RelativePath,
                RequiredAtRuntime = binding.Event == RuntimeLifecycleEventKind.ProjectLoaded &&
                                    binding.Authority == RuntimeAuthority.Server,
            }).ToArray();
            var gameplay = source.GameplayBindings.Select(binding => Ux0FGameplayEvents.Contains(binding.EventName)
                ? binding with
                {
                    Handler = "main",
                    ScriptPath = selected.RelativePath,
                    RequiredAtRuntime = binding.EventName == "life.dead",
                }
                : binding).ToArray();
            var candidate = source with
            {
                LifecycleBindings = lifecycle,
                GameplayBindings = gameplay,
                UseDownedState = false,
                AutoRespawn = false,
            };
            var missingCandidate = candidate with
            {
                LifecycleBindings = candidate.LifecycleBindings.Select(binding =>
                    binding.Event == RuntimeLifecycleEventKind.ProjectLoaded &&
                    binding.Authority == RuntimeAuthority.Editor
                        ? binding with { ScriptPath = "scripts/missing-event-script.forge" }
                        : binding).ToArray(),
            };
            var missing = controller.PreviewProjectEventConfiguration(missingCandidate);
            var wrongHandlerCandidate = candidate with
            {
                LifecycleBindings = candidate.LifecycleBindings.Select(binding =>
                    binding.Event == RuntimeLifecycleEventKind.ProjectLoaded &&
                    binding.Authority == RuntimeAuthority.Editor
                        ? binding with { Handler = "OnMissingEntry" }
                        : binding).ToArray(),
            };
            var wrongHandler = controller.PreviewProjectEventConfiguration(wrongHandlerCandidate);
            var missingScriptBlocked = !missing.Succeeded && !wrongHandler.Succeeded;
            var humanDiagnostics = missingScriptBlocked &&
                                   !missing.Message.Contains("project.", StringComparison.OrdinalIgnoreCase) &&
                                   !missing.Message.Contains("module/", StringComparison.OrdinalIgnoreCase) &&
                                   !missing.Message.Contains("scripts/", StringComparison.OrdinalIgnoreCase) &&
                                   !Guid.TryParse(missing.Message, out _) &&
                                   !wrongHandler.Message.Contains("project.", StringComparison.OrdinalIgnoreCase) &&
                                   !wrongHandler.Message.Contains("module/", StringComparison.OrdinalIgnoreCase);

            var preview = controller.PreviewProjectEventConfiguration(candidate);
            var dryRun = preview.Succeeded && ReferenceEquals(beforeProject, controller.Project);
            var applied = controller.ApplyProjectEventConfiguration(candidate);
            var appliedPolicies = controller.Project!.Module.RuntimePolicies;
            var lifecycleContexts = applied.Succeeded && appliedPolicies.LifecycleEvents.Bindings.Count == 8 &&
                                    appliedPolicies.LifecycleEvents.Bindings.All(binding =>
                                        binding.ScriptPath == selected.RelativePath && binding.Handler == "main");
            var targetedGameplay = appliedPolicies.GameplayEvents.Bindings
                .Where(binding => Ux0FGameplayEvents.Contains(binding.EventName)).ToArray();
            var gameplayGroups = targetedGameplay.Length == 12 && targetedGameplay.All(binding =>
                binding.ScriptPath == selected.RelativePath && binding.Handler == "main") &&
                                 !appliedPolicies.GameplayEvents.UseDownedState &&
                                 !appliedPolicies.GameplayEvents.AutoRespawn;
            var serverAuthority = targetedGameplay.All(binding => binding.Authority == EventAuthorityScope.Server);
            var neighborPoliciesPreserved = ReferenceEquals(beforePolicies.Extensions, appliedPolicies.Extensions) &&
                                            ReferenceEquals(beforePolicies.Rules, appliedPolicies.Rules) &&
                                            ReferenceEquals(beforePolicies.World, appliedPolicies.World) &&
                                            ReferenceEquals(beforePolicies.Gameplay, appliedPolicies.Gameplay) &&
                                            appliedPolicies.GameplayEvents.Bindings
                                                .Where(binding => !Ux0FGameplayEvents.Contains(binding.EventName))
                                                .All(binding => binding.ScriptPath is null);

            var undo = controller.Undo();
            var undone = undo.Succeeded && controller.Project!.Module.RuntimePolicies.LifecycleEvents.Bindings
                .All(binding => binding.ScriptPath is null);
            var redo = controller.Redo();
            var redone = redo.Succeeded && controller.Project!.Module.RuntimePolicies.LifecycleEvents.Bindings
                .All(binding => binding.ScriptPath == selected.RelativePath);
            var atomicUndoRedo = applied.Succeeded && undone && redone;

            var saved = controller.Save();
            controller.Close();
            var opened = reopened.Open(workspace);
            var persisted = reopened.Project?.Module.RuntimePolicies;
            var persistenceRoundTrip = saved.Succeeded && opened.Succeeded && persisted is not null &&
                                       persisted.LifecycleEvents.Bindings.All(binding =>
                                           binding.ScriptPath == selected.RelativePath) &&
                                       persisted.GameplayEvents.Bindings
                                           .Where(binding => Ux0FGameplayEvents.Contains(binding.EventName))
                                           .All(binding => binding.ScriptPath == selected.RelativePath) &&
                                       !persisted.GameplayEvents.AutoRespawn;

            var succeeded = scriptDiscovery && missingScriptBlocked && lifecycleContexts && gameplayGroups &&
                            serverAuthority && dryRun && atomicUndoRedo && persistenceRoundTrip &&
                            neighborPoliciesPreserved && humanDiagnostics;
            return new StudioUx0FSmokeResult(succeeded, scriptDiscovery, missingScriptBlocked,
                lifecycleContexts, gameplayGroups, serverAuthority, dryRun, atomicUndoRedo,
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

    private StudioProjectEventPreview MutateProjectEventConfiguration(
        StudioProjectEventConfigurationDraft draft,
        bool apply)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (History is null || Project is null)
        {
            return new StudioProjectEventPreview(false, "Aucun projet n’est ouvert.", string.Empty);
        }
        if (!ValidateEventScriptReferences(draft, out var referenceMessage))
        {
            return new StudioProjectEventPreview(false, referenceMessage, string.Empty);
        }

        var source = Project.Module.RuntimePolicies;
        ProjectLifecycleEventProfile lifecycle;
        ProjectGameplayEventProfile gameplay;
        try
        {
            var lifecycleDrafts = draft.LifecycleBindings.ToDictionary(item => (item.Event, item.Authority));
            if (lifecycleDrafts.Count != source.LifecycleEvents.Bindings.Count)
            {
                return new StudioProjectEventPreview(false,
                    "Rechargez les événements du module : leur liste a changé.", string.Empty);
            }
            var lifecycleBindings = source.LifecycleEvents.Bindings.Select(binding =>
            {
                var configured = lifecycleDrafts[(binding.Event, binding.Authority)];
                return binding with
                {
                    Handler = configured.Handler,
                    ScriptPath = NormalizeScriptPath(configured.ScriptPath),
                    RequiredAtRuntime = configured.RequiredAtRuntime,
                    Provenance = "studio:ux0-f-events",
                };
            }).ToArray();
            lifecycle = new ProjectLifecycleEventProfile(source.LifecycleEvents.Id,
                source.LifecycleEvents.SchemaVersion, "studio:ux0-f-events", draft.LifecycleEnabled,
                draft.LifecycleRequiredAtRuntime, lifecycleBindings);

            var gameplayDrafts = draft.GameplayBindings.ToDictionary(item => item.EventName, StringComparer.Ordinal);
            if (gameplayDrafts.Count != source.GameplayEvents.Bindings.Count)
            {
                return new StudioProjectEventPreview(false,
                    "Rechargez les événements de jeu : leur liste a changé.", string.Empty);
            }
            var gameplayBindings = source.GameplayEvents.Bindings.Select(binding =>
            {
                var configured = gameplayDrafts[binding.EventName];
                return binding with
                {
                    Handler = configured.Handler,
                    ScriptPath = NormalizeScriptPath(configured.ScriptPath),
                    RequiredAtRuntime = configured.RequiredAtRuntime,
                    Provenance = "studio:ux0-f-events",
                };
            }).ToArray();
            gameplay = new ProjectGameplayEventProfile(source.GameplayEvents.Id,
                source.GameplayEvents.SchemaVersion, "studio:ux0-f-events", draft.GameplayEnabled,
                draft.GameplayRequiredAtRuntime, draft.UseDownedState, draft.AutoRespawn,
                draft.ReplicateEquipmentAndProgression, gameplayBindings);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return new StudioProjectEventPreview(false,
                $"La configuration des événements doit être rechargée : {exception.Message}", string.Empty);
        }

        if (!TryBuildUx0GPolicies(draft, source, out var heartbeats, out var rest, out var world,
                out var ux0GMessage))
        {
            return new StudioProjectEventPreview(false, ux0GMessage, string.Empty);
        }

        if (!TryBuildUx0HPolicies(draft, source, world, out var sequenceAbort, out world,
                out var rules, out var ux0HMessage))
        {
            return new StudioProjectEventPreview(false, ux0HMessage, string.Empty);
        }

        var candidate = new ModuleRuntimePolicyProfile(source.Id, source.SchemaVersion, heartbeats,
            rest, sequenceAbort, "studio:ux0-h-project", source.Cinematics, source.Gameplay,
            rules, source.Extensions, gameplay, world, lifecycle);
        var command = new SetModuleRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()),
            History.State.Revision, candidate, DryRun: !apply);
        var preview = _setModuleRuntimePolicies.Handle(History.State, command with { DryRun = true });
        if (!preview.Succeeded)
        {
            return new StudioProjectEventPreview(false,
                HumanizeEventDiagnostic(preview.Diagnostics.FirstOrDefault()?.Code), string.Empty);
        }

        var summary = EventSummary(draft);
        if (!apply)
        {
            return new StudioProjectEventPreview(true,
                "Aperçu validé · le projet n’a pas été modifié", summary);
        }

        var applied = _setModuleRuntimePolicies.Handle(History.State, command with { DryRun = false });
        if (!applied.Succeeded)
        {
            return new StudioProjectEventPreview(false,
                HumanizeEventDiagnostic(applied.Diagnostics.FirstOrDefault()?.Code), string.Empty);
        }
        History.Record(applied);
        return new StudioProjectEventPreview(true,
            "Événements appliqués · Ctrl+Z pour annuler · Ctrl+S pour enregistrer", summary);
    }

    private bool ValidateEventScriptReferences(
        StudioProjectEventConfigurationDraft draft,
        out string message)
    {
        var references = draft.LifecycleBindings.Select(item =>
                (item.ScriptPath, item.Handler, item.RequiredAtRuntime))
            .Concat(draft.GameplayBindings.Select(item =>
                (item.ScriptPath, item.Handler, item.RequiredAtRuntime)))
            .Concat(Ux0GScriptReferences(draft))
            .Append((draft.SequenceAbort.ScriptPath, draft.SequenceAbort.Handler,
                draft.SequenceAbort.RequiredAtRuntime));
        foreach (var (scriptPath, handler, required) in references)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                if (required)
                {
                    message = "Choisissez un script avant de rendre ce comportement obligatoire.";
                    return false;
                }
                continue;
            }
            if (!TryResolveForgeScript(scriptPath, requireExisting: true, out var fullPath))
            {
                message = $"Le script « {HumanScriptName(scriptPath)} » n’est pas disponible dans ce projet.";
                return false;
            }
            if (!HasForgeScriptHandler(fullPath, handler))
            {
                message = $"Le script « {HumanScriptName(scriptPath)} » ne contient pas le point d’entrée attendu.";
                return false;
            }
        }
        message = string.Empty;
        return true;
    }

    private bool TryResolveForgeScript(string relativePath, bool requireExisting, out string fullPath)
    {
        fullPath = string.Empty;
        if (Session is null || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }
        var normalized = NormalizeScriptPath(relativePath);
        if (normalized is null || !normalized.StartsWith("scripts/", StringComparison.Ordinal) ||
            !normalized.EndsWith(".forge", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("../", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            return false;
        }
        try
        {
            var root = Path.GetFullPath(Session.RootDirectory);
            var scriptsRoot = Path.GetFullPath(Path.Combine(root, "scripts"));
            var candidate = Path.GetFullPath(Path.Combine(root,
                normalized.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = scriptsRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            fullPath = candidate;
            return !requireExisting || File.Exists(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    private static string? NormalizeScriptPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim().Replace('\\', '/');

    private static bool HasForgeScriptHandler(string fullPath, string handler)
    {
        if (string.IsNullOrWhiteSpace(handler))
        {
            return false;
        }
        try
        {
            var information = new FileInfo(fullPath);
            if (!information.Exists || information.Length > 1024 * 1024)
            {
                return false;
            }
            var source = File.ReadAllText(fullPath);
            return System.Text.RegularExpressions.Regex.IsMatch(
                source,
                @"\bvoid\s+" + System.Text.RegularExpressions.Regex.Escape(handler) + @"\s*\(",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string EventSummary(StudioProjectEventConfigurationDraft draft)
    {
        var configured = draft.LifecycleBindings.Count(item => item.ScriptPath is not null) +
                         draft.GameplayBindings.Count(item => item.ScriptPath is not null) +
                         draft.RestBindings.Count(item => item.ScriptPath is not null) +
                         draft.Heartbeats.Count(item => item.ScriptPath is not null) +
                         draft.CustomWorldEvents.Count(item => item.ScriptPath is not null) +
                         (draft.SequenceAbort.ScriptPath is null ? 0 : 1);
        var required = draft.LifecycleBindings.Count(item => item.RequiredAtRuntime) +
                       draft.GameplayBindings.Count(item => item.RequiredAtRuntime) +
                       draft.RestBindings.Count(item => item.RequiredAtRuntime) +
                       draft.Heartbeats.Count(item => item.RequiredAtRuntime) +
                       draft.CustomWorldEvents.Count(item => item.RequiredAtRuntime) +
                       (draft.SequenceAbort.RequiredAtRuntime ? 1 : 0);
        return $"{configured} comportement(s) relié(s) · {required} obligatoire(s) · " +
               $"{draft.Heartbeats.Count} rythme(s) global(aux) · {draft.CustomWorldEvents.Count} signal(aux) utilisateur · " +
               $"calendrier {draft.WorldClock.DaysPerMonth}×{draft.WorldClock.MonthsPerYear} · " +
               $"{draft.Progression.Curve.Count} palier(s) de progression";
    }

    private static string HumanizeEventDiagnostic(string? code) => code switch
    {
        ProjectLifecycleEventProfileValidator.RequiredDisabledCode or
            ProjectGameplayEventProfileValidator.RequiredDisabledCode =>
            "Un ensemble d’événements obligatoire doit rester activé.",
        ProjectLifecycleEventProfileValidator.BindingContractInvalidCode or
            ProjectGameplayEventProfileValidator.BindingContractInvalidCode =>
            "Choisissez un script ForgeScript valide ou revenez au comportement automatique.",
        ProjectLifecycleEventProfileValidator.BindingSetInvalidCode or
            ProjectGameplayEventProfileValidator.BindingSetInvalidCode =>
            "Rechargez la page : la liste des événements n’est plus complète.",
        null => "La configuration des événements n’a pas pu être validée.",
        _ => "Une valeur des événements n’est pas valide. Vérifiez les cartes signalées.",
    };

    private static string HumanScriptName(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath ?? string.Empty);
        var words = stem.Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? "Script sans nom"
            : string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static string HumanScriptFolder(string relativePath)
    {
        var directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(directory) || string.Equals(directory, "scripts", StringComparison.Ordinal))
        {
            return "dossier principal";
        }
        return HumanScriptName(directory.Split('/').Last());
    }

    private static string HumanRulesetName(string rulesetId) => rulesetId switch
    {
        "forge.generic" => "Règles génériques Forge",
        _ => "Ruleset existant",
    };

    private static string HumanProgressionSourceName(string source) => source switch
    {
        "combat" => "Combat",
        "exploration" => "Exploration",
        "quest" => "Quêtes",
        _ => HumanScriptName(source),
    };
}

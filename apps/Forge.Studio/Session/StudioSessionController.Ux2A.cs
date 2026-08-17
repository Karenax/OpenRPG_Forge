using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Lifecycle;
using Forge.Application.Projects;
using Forge.Application.Rules;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2AEvidenceProvenance = "studio:ux2-a:evidence";
    private const string Ux2AProvenance = "studio:ux2-a:living-world";
    private const string Ux2AEvidenceScript = "scripts/area-heartbeat.forge";

    private readonly SetAreaRuntimePoliciesHandler _setAreaRuntimePoliciesUx2A = new(new SystemClock());
    private StudioUx2ADraft? _approvedAreaPoliciesUx2A;
    private ProjectRevision? _approvedAreaPoliciesRevisionUx2A;
    private string? _approvedAreaPoliciesFingerprintUx2A;

    internal IReadOnlyList<StudioUx2AAreaChoice> AreaPolicyChoicesUx2A
    {
        get
        {
            var project = Project;
            return project is null ? [] : BuildAreaPolicyChoicesUx2A(project);
        }
    }

    private static IReadOnlyList<StudioUx2AAreaChoice> BuildAreaPolicyChoicesUx2A(ProjectDefinition project)
    {
        var duplicateNames = project.Areas
            .GroupBy(area => area.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);
        return project.Areas.Select(area =>
            {
                var policies = area.RuntimePolicies;
                var displayName = ContainsTechnicalIdentifierUx2A(area.DisplayName)
                    ? "Zone sans nom lisible"
                    : area.DisplayName.Trim();
                var name = duplicateNames[area.DisplayName] > 1
                    ? $"{displayName} · {HumanTokenUx2A(area.Slug)}"
                    : displayName;
                var rest = HumanRestModeUx2A(policies.Rest.Mode);
                var heartbeat = policies.Heartbeats.Count == 0
                    ? "aucun rythme local"
                    : $"{policies.Heartbeats.Count} rythme(s) local(aux)";
                var perception = policies.Rules.Perception.Modifiers.Count == 0
                    ? "perception vide"
                    : $"{policies.Rules.Perception.Modifiers.Count} modificateur(s) de perception";
                return new StudioUx2AAreaChoice(
                    area.Id,
                    $"{name} · {heartbeat} · repos {rest} · {perception}",
                    string.Equals(policies.Provenance, Ux2AEvidenceProvenance, StringComparison.Ordinal));
            })
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal StudioUx2ADraft? CreateAreaPolicyDraftUx2A(AreaId? requestedAreaId = null)
    {
        var history = History;
        var project = Project;
        if (history is null || project is null || requestedAreaId is null)
        {
            return null;
        }

        var matches = project.Areas.Where(area => area.Id == requestedAreaId).ToArray();
        return matches.Length == 1
            ? CreateAreaPolicyDraftUx2A(project, history.State.Revision, matches[0])
            : null;
    }

    private static StudioUx2ADraft CreateAreaPolicyDraftUx2A(
        ProjectDefinition project,
        ProjectRevision revision,
        AreaDefinition area)
    {
        var policies = area.RuntimePolicies;
        var rest = policies.Rest;
        return new StudioUx2ADraft(
            area.Id,
            revision,
            AreaPolicySourceFingerprintUx2A(area),
            area.DisplayName,
            area.Slug,
            area.Dimensions.Width,
            area.Dimensions.Depth,
            project.Module.RuntimePolicies.Rest.Enabled,
            policies.Heartbeats.Select(schedule => new StudioUx2AHeartbeatDraft(
                schedule.Id,
                schedule.Name,
                schedule.Enabled,
                schedule.IntervalMilliseconds,
                schedule.BudgetMilliseconds,
                schedule.SuspendWhenInactive,
                schedule.ForgeScriptApiVersion,
                schedule.Handler,
                schedule.ScriptPath,
                schedule.RequiredAtRuntime)).ToArray(),
            policies.Rules.Perception.RulesetId,
            policies.Rules.Perception.Modifiers.Select(modifier => new StudioUx2APerceptionModifierDraft(
                modifier.Id,
                modifier.SenseId,
                modifier.Multiplier,
                modifier.AdditiveModifier,
                modifier.ConditionTag)).ToArray(),
            rest.Mode,
            rest.DenialReason,
            rest.Condition,
            rest.CooldownSeconds,
            rest.RequiredSecureRadius,
            rest.Volumes.Select(volume => new StudioUx2ARestVolumeDraft(
                volume.Id,
                volume.Name,
                volume.Center,
                volume.Size,
                volume.Mode,
                volume.DenialReason,
                volume.Condition,
                volume.CooldownSeconds,
                volume.RequiredSecureRadius,
                volume.Priority)).ToArray());
    }

    internal StudioUx2APreview PreviewAreaPoliciesUx2A(StudioUx2ADraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAreaPoliciesUx2A();
        var history = History;
        if (history is null || Project is null)
        {
            return FailedAreaPoliciesPreviewUx2A("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedAreaPoliciesPreviewUx2A(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez la zone avant de prévisualiser.");
        }
        if (!TryBuildAreaPoliciesUx2A(draft, true, out var command, out var sourceArea,
                out var candidateArea, out var candidateProfile, out var diagnostics, out var message))
        {
            return new StudioUx2APreview(false, message, string.Empty, false,
                EmptyAreaPolicyDiffUx2A(), GroupAreaPolicyDiagnosticsUx2A(diagnostics),
                !draft.ProjectRestEnabled, false, false, false);
        }

        var dryRun = _setAreaRuntimePoliciesUx2A.Handle(history.State, command!);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2ADiagnostic(
                item.Code,
                $"areas/{draft.AreaId}/runtimePolicies",
                HumanizeAreaPolicyDiagnosticUx2A(item.Code, item.Message))).ToArray();
            return new StudioUx2APreview(false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande de zone a refusé cet aperçu.",
                string.Empty,
                false,
                BuildAreaPolicyDiffUx2A(sourceArea!.RuntimePolicies, candidateProfile!),
                GroupAreaPolicyDiagnosticsUx2A(commandDiagnostics),
                !draft.ProjectRestEnabled,
                false,
                false,
                false);
        }

        var diff = BuildAreaPolicyDiffUx2A(sourceArea!.RuntimePolicies, candidateProfile!);
        var runtime = EvaluateAreaPoliciesUx2A(
            candidateArea!,
            candidateProfile!,
            Project!.Module.RuntimePolicies.Rest);
        if (diff.HasChanges)
        {
            _approvedAreaPoliciesUx2A = draft;
            _approvedAreaPoliciesRevisionUx2A = history.State.Revision;
            _approvedAreaPoliciesFingerprintUx2A = AreaPolicyDraftFingerprintUx2A(draft);
        }
        return new StudioUx2APreview(
            true,
            diff.HasChanges
                ? "Aperçu validé · aucun document, script, nœud moteur ni état réseau n’a été modifié."
                : "Aperçu validé · aucune différence sémantique à appliquer.",
            runtime.Summary,
            diff.HasChanges,
            diff,
            GroupAreaPolicyDiagnosticsUx2A([]),
            !draft.ProjectRestEnabled,
            runtime.Scheduler,
            runtime.Perception,
            runtime.RestPolicy);
    }

    internal StudioUx2AActionResult ApplyApprovedAreaPoliciesUx2A()
    {
        var draft = _approvedAreaPoliciesUx2A;
        var history = History;
        if (draft is null || history is null ||
            _approvedAreaPoliciesRevisionUx2A != history.State.Revision ||
            !string.Equals(_approvedAreaPoliciesFingerprintUx2A, AreaPolicyDraftFingerprintUx2A(draft),
                StringComparison.Ordinal))
        {
            CancelAreaPoliciesUx2A();
            return new StudioUx2AActionResult(
                false,
                "Prévisualisez de nouveau les politiques de la zone avant de les appliquer.");
        }

        if (!TryBuildAreaPoliciesUx2A(draft, false, out var command, out _, out _, out _,
                out _, out var message))
        {
            CancelAreaPoliciesUx2A();
            return new StudioUx2AActionResult(false, message);
        }

        var preview = _setAreaRuntimePoliciesUx2A.Handle(history.State, command! with { DryRun = true });
        if (!preview.Succeeded)
        {
            CancelAreaPoliciesUx2A();
            return new StudioUx2AActionResult(false, HumanizeAreaPolicyDiagnosticUx2A(
                preview.Diagnostics.FirstOrDefault()?.Code,
                preview.Diagnostics.FirstOrDefault()?.Message));
        }

        var applied = _setAreaRuntimePoliciesUx2A.Handle(history.State, command!);
        CancelAreaPoliciesUx2A();
        if (!applied.Succeeded)
        {
            return new StudioUx2AActionResult(false, HumanizeAreaPolicyDiagnosticUx2A(
                applied.Diagnostics.FirstOrDefault()?.Code,
                applied.Diagnostics.FirstOrDefault()?.Message));
        }

        history.Record(applied);
        InvalidateContentIndexes();
        return new StudioUx2AActionResult(
            true,
            "Politiques de la zone appliquées · Ctrl+Z pour annuler, Ctrl+Y pour rétablir");
    }

    internal void CancelAreaPoliciesUx2A()
    {
        _approvedAreaPoliciesUx2A = null;
        _approvedAreaPoliciesRevisionUx2A = null;
        _approvedAreaPoliciesFingerprintUx2A = null;
    }

    internal void CloseUx2A() => CancelAreaPoliciesUx2A();

    internal StudioUx2AApprovalSnapshot CaptureAreaPolicyApprovalUx2A() => new(
        _approvedAreaPoliciesUx2A,
        _approvedAreaPoliciesRevisionUx2A,
        _approvedAreaPoliciesFingerprintUx2A);

    internal void RestoreAreaPolicyApprovalUx2A(StudioUx2AApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAreaPoliciesUx2A = snapshot.Draft;
        _approvedAreaPoliciesRevisionUx2A = snapshot.Revision;
        _approvedAreaPoliciesFingerprintUx2A = snapshot.Fingerprint;
    }

    private bool TryBuildAreaPoliciesUx2A(
        StudioUx2ADraft draft,
        bool dryRun,
        out SetAreaRuntimePoliciesCommand? command,
        out AreaDefinition? sourceArea,
        out AreaDefinition? candidateArea,
        out AreaRuntimePolicyProfile? candidateProfile,
        out IReadOnlyList<StudioUx2ADiagnostic> diagnostics,
        out string message)
    {
        command = null;
        sourceArea = null;
        candidateArea = null;
        candidateProfile = null;
        diagnostics = [];
        message = string.Empty;
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            message = "Aucun module n’est ouvert.";
            return false;
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            message = "Le projet a changé. Rechargez la zone avant de prévisualiser.";
            return false;
        }

        var matches = project.Areas.Where(area => area.Id == draft.AreaId).ToArray();
        if (matches.Length != 1)
        {
            message = "La zone choisie n’existe plus ou n’est plus unique.";
            return false;
        }
        sourceArea = matches[0];
        if (!string.Equals(draft.SourceFingerprint, AreaPolicySourceFingerprintUx2A(sourceArea),
                StringComparison.Ordinal))
        {
            message = "Les politiques de la zone ont changé. Rechargez la fiche avant de prévisualiser.";
            return false;
        }

        var inputDiagnostics = ValidateAreaPolicyDraftUx2A(draft).ToList();
        foreach (var schedule in draft.Heartbeats)
        {
            if (string.IsNullOrWhiteSpace(schedule.ScriptPath))
            {
                if (schedule.RequiredAtRuntime)
                {
                    inputDiagnostics.Add(new StudioUx2ADiagnostic(
                        "studio.area_heartbeat.script_required",
                        $"areas/{draft.AreaId}/runtimePolicies/heartbeats/{schedule.Id}/scriptPath",
                        $"Choisissez un script réel pour le rythme « {HumanHeartbeatNameUx2A(schedule.Name)} »."));
                }
                continue;
            }
            if (!TryResolveForgeScript(schedule.ScriptPath, requireExisting: true, out var fullPath))
            {
                inputDiagnostics.Add(new StudioUx2ADiagnostic(
                    "studio.area_heartbeat.script_missing",
                    $"areas/{draft.AreaId}/runtimePolicies/heartbeats/{schedule.Id}/scriptPath",
                    $"Le script choisi pour « {HumanHeartbeatNameUx2A(schedule.Name)} » n’est plus disponible dans ce projet."));
            }
            else if (!HasForgeScriptHandler(fullPath, schedule.Handler))
            {
                inputDiagnostics.Add(new StudioUx2ADiagnostic(
                    "studio.area_heartbeat.handler_missing",
                    $"areas/{draft.AreaId}/runtimePolicies/heartbeats/{schedule.Id}/handler",
                    $"Le script choisi ne contient pas le point d’entrée « {HumanTokenUx2A(schedule.Handler)} »."));
            }
        }
        if (inputDiagnostics.Count > 0)
        {
            diagnostics = inputDiagnostics;
            message = inputDiagnostics[0].Message;
            return false;
        }

        try
        {
            var source = sourceArea.RuntimePolicies;
            var sourceHeartbeats = source.Heartbeats.ToDictionary(item => item.Id);
            var heartbeats = draft.Heartbeats.Select(item =>
            {
                if (sourceHeartbeats.TryGetValue(item.Id, out var existing) &&
                    SameHeartbeatUx2A(item, existing))
                {
                    return existing;
                }
                return new HeartbeatScheduleDefinition(
                    item.Id,
                    HeartbeatScheduleDefinition.CurrentSchemaVersion,
                    item.Name,
                    HeartbeatScope.Area,
                    item.Enabled,
                    item.IntervalMilliseconds,
                    item.BudgetMilliseconds,
                    item.SuspendWhenInactive,
                    item.ForgeScriptApiVersion,
                    item.Handler,
                    Ux2AProvenance,
                    NormalizeScriptPath(item.ScriptPath),
                    item.RequiredAtRuntime);
            }).ToArray();
            var sourceVolumes = source.Rest.Volumes.ToDictionary(item => item.Id);
            var volumes = draft.RestVolumes.Select(item =>
            {
                if (sourceVolumes.TryGetValue(item.Id, out var existing) && SameRestVolumeUx2A(item, existing))
                {
                    return existing;
                }
                return new RestPolicyVolume(
                    item.Id,
                    item.Name,
                    item.Center,
                    item.Size,
                    item.Mode,
                    item.DenialReason,
                    item.Condition,
                    item.CooldownSeconds,
                    item.RequiredSecureRadius,
                    item.Priority,
                    Ux2AProvenance);
            }).ToArray();
            var restUnchanged = draft.RestMode == source.Rest.Mode &&
                                string.Equals(draft.RestDenialReason.Trim(), source.Rest.DenialReason,
                                    StringComparison.Ordinal) &&
                                string.Equals(draft.RestCondition.Trim(), source.Rest.Condition,
                                    StringComparison.Ordinal) &&
                                draft.RestCooldownSeconds == source.Rest.CooldownSeconds &&
                                Math.Abs(draft.RestRequiredSecureRadius - source.Rest.RequiredSecureRadius) < .0001f &&
                                volumes.Length == source.Rest.Volumes.Count &&
                                volumes.Select(item => item.Id).SequenceEqual(
                                    source.Rest.Volumes.Select(item => item.Id)) &&
                                volumes.Zip(source.Rest.Volumes, ReferenceEquals).All(same => same);
            var rest = restUnchanged
                ? source.Rest
                : new AreaRestPolicy(
                    source.Rest.Id,
                    source.Rest.SchemaVersion,
                    draft.RestMode,
                    draft.RestDenialReason,
                    draft.RestCondition,
                    draft.RestCooldownSeconds,
                    draft.RestRequiredSecureRadius,
                    volumes,
                    Ux2AProvenance);
            var sourceModifiers = source.Rules.Perception.Modifiers.ToDictionary(item => item.Id);
            var modifiers = draft.PerceptionModifiers.Select(item =>
            {
                if (sourceModifiers.TryGetValue(item.Id, out var existing) &&
                    SamePerceptionModifierUx2A(item, existing))
                {
                    return existing;
                }
                return new EnvironmentPerceptionModifier(
                    item.Id,
                    item.SenseId.Trim(),
                    item.Multiplier,
                    item.AdditiveModifier,
                    item.ConditionTag.Trim());
            }).ToArray();
            var perceptionUnchanged = string.Equals(
                                          draft.PerceptionRulesetId.Trim(),
                                          source.Rules.Perception.RulesetId,
                                          StringComparison.Ordinal) &&
                                      modifiers.Length == source.Rules.Perception.Modifiers.Count &&
                                      modifiers.Select(item => item.Id).SequenceEqual(
                                          source.Rules.Perception.Modifiers.Select(item => item.Id)) &&
                                      modifiers.Zip(source.Rules.Perception.Modifiers, ReferenceEquals)
                                          .All(same => same);
            var perception = perceptionUnchanged
                ? source.Rules.Perception
                : new EnvironmentPerceptionProfile(
                    source.Rules.Perception.Id,
                    source.Rules.Perception.SchemaVersion,
                    draft.PerceptionRulesetId,
                    modifiers,
                    Ux2AProvenance);
            var rules = ReferenceEquals(perception, source.Rules.Perception)
                ? source.Rules
                : new AreaRulesProfile(
                    source.Rules.Id,
                    source.Rules.SchemaVersion,
                    perception,
                    source.Rules.Combat,
                    Ux2AProvenance);
            var heartbeatUnchanged = heartbeats.Length == source.Heartbeats.Count &&
                                     heartbeats.Select(item => item.Id).SequenceEqual(
                                         source.Heartbeats.Select(item => item.Id)) &&
                                     heartbeats.Zip(source.Heartbeats, ReferenceEquals).All(same => same);
            candidateProfile = heartbeatUnchanged && ReferenceEquals(rest, source.Rest) &&
                               ReferenceEquals(rules, source.Rules)
                ? source
                : new AreaRuntimePolicyProfile(
                    source.Id,
                    source.SchemaVersion,
                    heartbeats,
                    rest,
                    Ux2AProvenance,
                    source.Cinematics,
                    rules,
                    source.Experience);
            var sourceAreaId = sourceArea.Id;
            var updatedArea = sourceArea.WithRuntimePolicies(candidateProfile);
            candidateArea = updatedArea;

            var candidateProject = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module,
                project.Blueprints,
                project.Areas.Select(area => area.Id == sourceAreaId ? updatedArea : area).ToArray(),
                project.Dialogues,
                project.Quests);
            var canonicalDiagnostics = ProjectValidator.Validate(candidateProject)
                .Select(item => new StudioUx2ADiagnostic(
                    item.Code,
                    item.Path,
                    HumanizeAreaPolicyDiagnosticUx2A(item.Code, item.Message)))
                .ToArray();
            if (canonicalDiagnostics.Length > 0)
            {
                diagnostics = canonicalDiagnostics;
                message = canonicalDiagnostics[0].Message;
                return false;
            }

            command = new SetAreaRuntimePoliciesCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                sourceArea.Id,
                candidateProfile,
                dryRun);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           KeyNotFoundException)
        {
            var diagnostic = new StudioUx2ADiagnostic(
                "studio.area_policy.invalid",
                $"areas/{draft.AreaId}/runtimePolicies",
                HumanizeAreaPolicyConstructionUx2A(exception));
            diagnostics = [diagnostic];
            message = diagnostic.Message;
            return false;
        }
    }

    private static IEnumerable<StudioUx2ADiagnostic> ValidateAreaPolicyDraftUx2A(StudioUx2ADraft draft)
    {
        var root = $"areas/{draft.AreaId}/runtimePolicies";
        if (draft.Heartbeats.Select(item => item.Id).Any(id => id == Guid.Empty) ||
            draft.Heartbeats.Select(item => item.Id).Distinct().Count() != draft.Heartbeats.Count)
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_heartbeat.identity_invalid",
                $"{root}/heartbeats",
                "Rechargez les rythmes : une identité stable est vide ou dupliquée.");
        }
        if (draft.Heartbeats.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                                        string.IsNullOrWhiteSpace(item.ForgeScriptApiVersion) ||
                                        string.IsNullOrWhiteSpace(item.Handler)))
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_heartbeat.required_field",
                $"{root}/heartbeats",
                "Chaque rythme doit conserver un nom, une version ForgeScript et un point d’entrée.");
        }
        if (draft.Heartbeats.Select(item => item.Name.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase).Count() != draft.Heartbeats.Count)
        {
            yield return new StudioUx2ADiagnostic(
                RuntimePolicyValidator.HeartbeatNameCode,
                $"{root}/heartbeats",
                "Donnez un nom distinct à chaque rythme local de la zone.");
        }
        if (draft.Heartbeats.Any(item => item.IntervalMilliseconds is < 100 or > 86_400_000 ||
                                        item.BudgetMilliseconds is < 1 or > 1_000 ||
                                        item.BudgetMilliseconds > item.IntervalMilliseconds))
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_heartbeat.timing_invalid",
                $"{root}/heartbeats",
                "La cadence doit rester entre 100 ms et 24 h ; le budget entre 1 et 1 000 ms, sans dépasser la cadence.");
        }

        if (string.IsNullOrWhiteSpace(draft.PerceptionRulesetId))
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_perception.ruleset_required",
                $"{root}/rules/perception/rulesetId",
                "Nommez le profil de règles qui interprète ces sens.");
        }
        if (draft.PerceptionModifiers.Select(item => item.Id).Any(id => id == Guid.Empty) ||
            draft.PerceptionModifiers.Select(item => item.Id).Distinct().Count() != draft.PerceptionModifiers.Count)
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_perception.identity_invalid",
                $"{root}/rules/perception/modifiers",
                "Rechargez les modificateurs : une identité stable est vide ou dupliquée.");
        }
        if (draft.PerceptionModifiers.Any(item => string.IsNullOrWhiteSpace(item.SenseId) ||
                                                  !double.IsFinite(item.Multiplier) ||
                                                  item.Multiplier is < 0 or > 10 ||
                                                  !double.IsFinite(item.AdditiveModifier) ||
                                                  item.AdditiveModifier is < -10 or > 10))
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_perception.value_invalid",
                $"{root}/rules/perception/modifiers",
                "Chaque sens doit être nommé ; multiplicateur et addition doivent rester dans les limites sûres.");
        }
        if (draft.PerceptionModifiers.Select(item => $"{item.SenseId.Trim()}\u001f{item.ConditionTag.Trim()}")
            .Distinct(StringComparer.Ordinal).Count() != draft.PerceptionModifiers.Count)
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_perception.duplicate",
                $"{root}/rules/perception/modifiers",
                "Un même sens ne peut avoir deux fois la même condition de milieu.");
        }

        if (draft.RestMode != RestPermissionMode.Allowed && string.IsNullOrWhiteSpace(draft.RestDenialReason))
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_rest.reason_required",
                $"{root}/rest/denialReason",
                "Expliquez au joueur pourquoi le repos est refusé ou conditionnel.");
        }
        if (draft.RestMode == RestPermissionMode.Conditional && string.IsNullOrWhiteSpace(draft.RestCondition))
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_rest.condition_required",
                $"{root}/rest/condition",
                "Ajoutez la condition qui autorise le repos dans cette zone.");
        }
        if (draft.RestCooldownSeconds is < 0 or > 86_400 ||
            !float.IsFinite(draft.RestRequiredSecureRadius) || draft.RestRequiredSecureRadius is < 0 or > 512)
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_rest.value_invalid",
                $"{root}/rest",
                "Recharge et rayon sécurisé dépassent les limites sûres.");
        }
        if (draft.RestVolumes.Select(item => item.Id).Any(id => id == Guid.Empty) ||
            draft.RestVolumes.Select(item => item.Id).Distinct().Count() != draft.RestVolumes.Count)
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_rest.volume_identity_invalid",
                $"{root}/rest/volumes",
                "Rechargez les volumes : une identité stable est vide ou dupliquée.");
        }
        if (draft.RestVolumes.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                                          !FiniteUx2A(item.Center) || !FiniteUx2A(item.Size) ||
                                          item.Size.X <= 0 || item.Size.Y <= 0 || item.Size.Z <= 0 ||
                                          item.CooldownSeconds is < 0 or > 86_400 ||
                                          !float.IsFinite(item.RequiredSecureRadius) ||
                                          item.RequiredSecureRadius is < 0 or > 512 ||
                                          item.Priority is < -10_000 or > 10_000 ||
                                          item.Mode != RestPermissionMode.Allowed &&
                                          string.IsNullOrWhiteSpace(item.DenialReason) ||
                                          item.Mode == RestPermissionMode.Conditional &&
                                          string.IsNullOrWhiteSpace(item.Condition)))
        {
            yield return new StudioUx2ADiagnostic(
                "studio.area_rest.volume_invalid",
                $"{root}/rest/volumes",
                "Chaque volume doit avoir un nom, une taille positive et une règle complète dans les limites sûres.");
        }
    }

    private static bool SameHeartbeatUx2A(
        StudioUx2AHeartbeatDraft draft,
        HeartbeatScheduleDefinition source) =>
        draft.Id == source.Id &&
        string.Equals(draft.Name.Trim(), source.Name, StringComparison.Ordinal) &&
        source.Scope == HeartbeatScope.Area &&
        draft.Enabled == source.Enabled &&
        draft.IntervalMilliseconds == source.IntervalMilliseconds &&
        draft.BudgetMilliseconds == source.BudgetMilliseconds &&
        draft.SuspendWhenInactive == source.SuspendWhenInactive &&
        string.Equals(draft.ForgeScriptApiVersion.Trim(), source.ForgeScriptApiVersion,
            StringComparison.Ordinal) &&
        string.Equals(draft.Handler.Trim(), source.Handler, StringComparison.Ordinal) &&
        string.Equals(NormalizeScriptPath(draft.ScriptPath), source.ScriptPath, StringComparison.Ordinal) &&
        draft.RequiredAtRuntime == source.RequiredAtRuntime;

    private static bool SameRestVolumeUx2A(
        StudioUx2ARestVolumeDraft draft,
        RestPolicyVolume source) =>
        draft.Id == source.Id &&
        string.Equals(draft.Name.Trim(), source.Name, StringComparison.Ordinal) &&
        draft.Center == source.Center &&
        draft.Size == source.Size &&
        draft.Mode == source.Mode &&
        string.Equals(draft.DenialReason.Trim(), source.DenialReason, StringComparison.Ordinal) &&
        string.Equals(draft.Condition.Trim(), source.Condition, StringComparison.Ordinal) &&
        draft.CooldownSeconds == source.CooldownSeconds &&
        Math.Abs(draft.RequiredSecureRadius - source.RequiredSecureRadius) < .0001f &&
        draft.Priority == source.Priority;

    private static bool SamePerceptionModifierUx2A(
        StudioUx2APerceptionModifierDraft draft,
        EnvironmentPerceptionModifier source) =>
        draft.Id == source.Id &&
        string.Equals(draft.SenseId, source.SenseId, StringComparison.Ordinal) &&
        Math.Abs(draft.Multiplier - source.Multiplier) < .0000001d &&
        Math.Abs(draft.AdditiveModifier - source.AdditiveModifier) < .0000001d &&
        string.Equals(draft.ConditionTag, source.ConditionTag, StringComparison.Ordinal);

    private static bool SameAreaAuthoringUx2A(AreaDefinition source, AreaDefinition candidate) =>
        string.Equals(
            AreaJsonSerializer.Serialize(source.WithAuthoring(source.Authoring)),
            AreaJsonSerializer.Serialize(source.WithAuthoring(candidate.Authoring)),
            StringComparison.Ordinal);

    private static bool FiniteUx2A(Vector3Value value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static StudioUx2ASemanticDiff BuildAreaPolicyDiffUx2A(
        AreaRuntimePolicyProfile source,
        AreaRuntimePolicyProfile candidate)
    {
        var heartbeats = new List<string>();
        DiffNamedUx2A(
            source.Heartbeats,
            candidate.Heartbeats,
            item => item.Id,
            item => HumanHeartbeatNameUx2A(item.Name),
            (left, right) => left == right,
            heartbeats,
            "Rythme ajouté",
            "Rythme retiré",
            "Rythme modifié");

        var perception = new List<string>();
        if (!string.Equals(source.Rules.Perception.RulesetId, candidate.Rules.Perception.RulesetId,
                StringComparison.Ordinal))
        {
            perception.Add(
                $"Profil de règles : {HumanTokenUx2A(source.Rules.Perception.RulesetId)} → " +
                HumanTokenUx2A(candidate.Rules.Perception.RulesetId));
        }
        DiffNamedUx2A(
            source.Rules.Perception.Modifiers,
            candidate.Rules.Perception.Modifiers,
            item => item.Id,
            HumanPerceptionModifierUx2A,
            (left, right) => left == right,
            perception,
            "Modificateur ajouté",
            "Modificateur retiré",
            "Modificateur modifié");

        var rest = new List<string>();
        if (source.Rest.Mode != candidate.Rest.Mode)
        {
            rest.Add($"Permission : {HumanRestModeUx2A(source.Rest.Mode)} → {HumanRestModeUx2A(candidate.Rest.Mode)}");
        }
        if (!string.Equals(source.Rest.DenialReason, candidate.Rest.DenialReason, StringComparison.Ordinal))
        {
            rest.Add("Explication joueur modifiée");
        }
        if (!string.Equals(source.Rest.Condition, candidate.Rest.Condition, StringComparison.Ordinal))
        {
            rest.Add("Condition de repos modifiée");
        }
        if (source.Rest.CooldownSeconds != candidate.Rest.CooldownSeconds)
        {
            rest.Add($"Recharge : {source.Rest.CooldownSeconds} s → {candidate.Rest.CooldownSeconds} s");
        }
        if (Math.Abs(source.Rest.RequiredSecureRadius - candidate.Rest.RequiredSecureRadius) > .0001f)
        {
            rest.Add(
                $"Rayon sécurisé : {source.Rest.RequiredSecureRadius:0.##} m → " +
                $"{candidate.Rest.RequiredSecureRadius:0.##} m");
        }
        DiffNamedUx2A(
            source.Rest.Volumes,
            candidate.Rest.Volumes,
            item => item.Id,
            item => HumanNamedValueUx2A(item.Name, "Volume sans nom lisible"),
            (left, right) => left == right,
            rest,
            "Volume ajouté",
            "Volume retiré",
            "Volume modifié");
        return new StudioUx2ASemanticDiff(heartbeats, perception, rest);
    }

    private static void DiffNamedUx2A<T>(
        IReadOnlyList<T> source,
        IReadOnlyList<T> candidate,
        Func<T, Guid> id,
        Func<T, string> name,
        Func<T, T, bool> equal,
        ICollection<string> destination,
        string added,
        string removed,
        string changed)
    {
        var before = source.ToDictionary(id);
        var after = candidate.ToDictionary(id);
        foreach (var item in candidate.Where(item => !before.ContainsKey(id(item))))
        {
            destination.Add($"{added} · {name(item)}");
        }
        foreach (var item in source.Where(item => !after.ContainsKey(id(item))))
        {
            destination.Add($"{removed} · {name(item)}");
        }
        foreach (var item in candidate.Where(item => before.ContainsKey(id(item))))
        {
            var previous = before[id(item)];
            if (!equal(previous, item))
            {
                destination.Add($"{changed} · {name(item)}");
            }
        }
    }

    private static (string Summary, bool Scheduler, bool Perception, bool RestPolicy) EvaluateAreaPoliciesUx2A(
        AreaDefinition area,
        AreaRuntimePolicyProfile profile,
        RestLifecycleProfile projectRest)
    {
        var enabled = profile.Heartbeats.Where(item => item.Enabled).ToArray();
        var schedulerEvaluated = true;
        var aggressive = 0;
        var suspended = 0;
        foreach (var schedule in enabled)
        {
            var active = new HeartbeatScheduler().Advance(
                [schedule],
                schedule.IntervalMilliseconds,
                true,
                area.Id);
            var inactive = new HeartbeatScheduler().Advance(
                [schedule],
                schedule.IntervalMilliseconds,
                false,
                area.Id);
            schedulerEvaluated &= active.Dispatches.Count == 1 &&
                                  active.Dispatches[0].BudgetMilliseconds == schedule.BudgetMilliseconds;
            if (schedule.SuspendWhenInactive && inactive.Dispatches.Count == 0)
            {
                suspended++;
            }
            if (active.Diagnostics.Any(item => item.Code == HeartbeatScheduler.AggressiveIntervalCode))
            {
                aggressive++;
            }
        }

        var tags = profile.Rules.Perception.Modifiers
            .Select(item => item.ConditionTag)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);
        var senses = profile.Rules.Perception.Modifiers.Select(item => item.SenseId)
            .Distinct(StringComparer.Ordinal).ToArray();
        var perceptionEvaluated = senses.All(sense =>
        {
            var result = RulesRuntimeService.ResolvePerception(
                profile.Rules.Perception,
                sense,
                10,
                .8,
                tags);
            return double.IsFinite(result.Range) && double.IsFinite(result.Confidence);
        });
        var restEvaluated = profile.Rest.Volumes.All(volume =>
            profile.Rest.ResolveAt(volume.Center).Mode == volume.Mode);
        var boundaryPolicy = profile.Rest.ResolveAt(new Vector3Value(
            area.Dimensions.Width / 2f - .01f,
            0,
            area.Dimensions.Depth / 2f - .01f));
        restEvaluated &= Enum.IsDefined(boundaryPolicy.Mode);
        var probePosition = profile.Rest.Volumes.FirstOrDefault()?.Center ?? Vector3Value.Zero;
        var resolvedRest = profile.Rest.ResolveAt(probePosition);
        var restProbe = new RestRuntimeCoordinator(() => Guid.Parse("2a000000-0000-4000-8000-0000000000a1"))
            .Request(
                "studio-preview",
                "studio-preview",
                area.Id,
                probePosition,
                projectRest,
                profile.Rest,
                secure: true,
                conditionEvaluator: _ => true,
                serverRevision: 0,
                occurredAt: DateTimeOffset.UnixEpoch);
        restEvaluated &= !projectRest.Enabled
            ? !restProbe.Accepted && restProbe.DiagnosticCode == RestRuntimeCoordinator.DisabledCode &&
              restProbe.Events.Count == 0
            : resolvedRest.Mode == RestPermissionMode.Forbidden
                ? !restProbe.Accepted && restProbe.DiagnosticCode == RestRuntimeCoordinator.ForbiddenCode
                : restProbe.Accepted;
        var heartbeatText = profile.Heartbeats.Count == 0
            ? "Heartbeat · état vide, aucun rythme local"
            : $"Heartbeat · {enabled.Length} actif(s), {suspended} suspendu(s) hors zone, " +
              $"{aggressive} avertissement(s) de cadence · politique calculée sans exécuter de handler";
        var perceptionText = senses.Length == 0
            ? $"Perception · profil {HumanTokenUx2A(profile.Rules.Perception.RulesetId)} · état vide"
            : $"Perception · {senses.Length} sens relu(s), " +
              $"{profile.Rules.Perception.Modifiers.Count} modificateur(s) · aucun effet visuel simulé";
        var restText = projectRest.Enabled
            ? $"Repos · {HumanRestModeUx2A(profile.Rest.Mode)} · {profile.Rest.Volumes.Count} volume(s) prioritaire(s) · " +
              "politique relue en mémoire, aucun événement distribué"
            : $"Repos dormant · règle {HumanRestModeUx2A(profile.Rest.Mode)} conservée, " +
              "mais le cycle de repos du projet reste désactivé et n’a pas été modifié";
        return ($"{heartbeatText}\n{perceptionText}\n{restText}", schedulerEvaluated, perceptionEvaluated,
            restEvaluated);
    }

    private static IReadOnlyList<StudioUx2ADiagnosticGroup> GroupAreaPolicyDiagnosticsUx2A(
        IReadOnlyList<StudioUx2ADiagnostic> diagnostics)
    {
        StudioUx2ADiagnostic[] Select(string token) => diagnostics
            .Where(item => item.Path.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                           item.Code.Contains(token, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var heartbeat = Select("heartbeat");
        var perception = Select("perception");
        var rest = Select("rest");
        var known = heartbeat.Concat(perception).Concat(rest).ToHashSet();
        var command = diagnostics.Where(item => !known.Contains(item)).ToArray();
        return
        [
            DiagnosticGroupUx2A("heartbeat", "Rythmes locaux", heartbeat),
            DiagnosticGroupUx2A("perception", "Perception", perception),
            DiagnosticGroupUx2A("rest", "Repos et volumes", rest),
            DiagnosticGroupUx2A("command", "Transaction de zone", command),
        ];
    }

    private static StudioUx2ADiagnosticGroup DiagnosticGroupUx2A(
        string key,
        string title,
        IReadOnlyList<StudioUx2ADiagnostic> diagnostics) =>
        new(key, title, diagnostics.Count == 0 ? "Conforme" : "À corriger", diagnostics);

    private static StudioUx2APreview FailedAreaPoliciesPreviewUx2A(string message) => new(
        false,
        message,
        string.Empty,
        false,
        EmptyAreaPolicyDiffUx2A(),
        GroupAreaPolicyDiagnosticsUx2A([]),
        false,
        false,
        false,
        false);

    private static StudioUx2ASemanticDiff EmptyAreaPolicyDiffUx2A() => new([], [], []);

    private static string HumanizeAreaPolicyConstructionUx2A(Exception exception) => exception switch
    {
        ArgumentOutOfRangeException =>
            "Une cadence, un budget, un multiplicateur, une taille ou une règle de repos dépasse les limites sûres.",
        _ => "La fiche est incomplète. Vérifiez les noms, les scripts, les sens et les règles de repos.",
    };

    private static string HumanizeAreaPolicyDiagnosticUx2A(string? code, string? fallback = null) => code switch
    {
        "command.revision_conflict" =>
            "Le projet a changé depuis l’aperçu. Rechargez la zone et prévisualisez à nouveau.",
        "runtime.policy.area_missing" => "La zone choisie n’existe plus.",
        RuntimePolicyValidator.AreaIdentityCode =>
            "L’identité des politiques ne correspond plus à la zone. Rechargez sa fiche.",
        RuntimePolicyValidator.HeartbeatNameCode => "Donnez un nom distinct à chaque rythme local.",
        RuntimePolicyValidator.RestVolumeBoundsCode =>
            "Un volume de repos dépasse les limites visibles de la zone.",
        RuntimePolicyValidator.AreaRulesIdentityCode or RuntimePolicyValidator.RulesProfileIdentityCode =>
            "L’identité du profil de perception n’est plus cohérente avec la zone.",
        null => "La commande de zone n’a pas pu être validée.",
        _ => string.IsNullOrWhiteSpace(fallback) || ContainsTechnicalIdentifierUx2A(fallback)
            ? "Une politique de la zone doit être corrigée avant application."
            : fallback,
    };

    private static string HumanRestModeUx2A(RestPermissionMode mode) => mode switch
    {
        RestPermissionMode.Allowed => "autorisé",
        RestPermissionMode.Forbidden => "interdit",
        RestPermissionMode.Conditional => "conditionnel",
        _ => "non reconnu",
    };

    private static string HumanHeartbeatNameUx2A(string value) =>
        HumanNamedValueUx2A(value, "Rythme sans nom lisible");

    private static string HumanNamedValueUx2A(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || ContainsTechnicalIdentifierUx2A(value)
            ? fallback
            : value.Trim();

    private static string HumanPerceptionModifierUx2A(EnvironmentPerceptionModifier modifier)
    {
        var condition = string.IsNullOrWhiteSpace(modifier.ConditionTag)
            ? "toujours"
            : $"si {HumanTokenUx2A(modifier.ConditionTag)}";
        return $"{HumanTokenUx2A(modifier.SenseId)} · ×{modifier.Multiplier:0.###} " +
               $"{modifier.AdditiveModifier:+0.###;-0.###;0} · {condition}";
    }

    private static string HumanTokenUx2A(string value)
    {
        if (ContainsTechnicalIdentifierUx2A(value ?? string.Empty))
        {
            return "Référence interne masquée";
        }
        var words = (value ?? string.Empty).Replace('_', ' ').Replace('-', ' ').Replace(':', ' ').Trim();
        return words.Length == 0 ? "Non renseigné" : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static string AreaPolicySourceFingerprintUx2A(AreaDefinition area)
    {
        var bytes = Encoding.UTF8.GetBytes(AreaJsonSerializer.Serialize(area));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    internal static bool ProjectRoundTripMatchesUx2A(
        ProjectDefinition expected,
        ProjectDefinition restored,
        AreaId targetAreaId)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(restored);
        ArgumentNullException.ThrowIfNull(targetAreaId);
        if (!string.Equals(
                ProjectJsonSerializer.Serialize(expected),
                ProjectJsonSerializer.Serialize(restored),
                StringComparison.Ordinal))
        {
            return false;
        }

        var restoredAreas = restored.Areas.ToDictionary(item => item.Id);
        return expected.Module.Id == restored.Module.Id &&
               expected.Module.EntryAreaId == restored.Module.EntryAreaId &&
               expected.Areas.Where(item => item.Id != targetAreaId).All(item =>
                   restoredAreas.TryGetValue(item.Id, out var restoredArea) &&
                   string.Equals(
                       AreaJsonSerializer.Serialize(item),
                       AreaJsonSerializer.Serialize(restoredArea),
                       StringComparison.Ordinal)) &&
               expected.Blueprints.Select(item => item.Id).SequenceEqual(
                   restored.Blueprints.Select(item => item.Id)) &&
               expected.Dialogues.Select(item => item.Id).SequenceEqual(
                   restored.Dialogues.Select(item => item.Id)) &&
               expected.Quests.Select(item => item.Id).SequenceEqual(restored.Quests.Select(item => item.Id));
    }

    private static string AreaPolicyDraftFingerprintUx2A(StudioUx2ADraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)1);
            WriteGuidUx2A(writer, draft.AreaId.Value);
            writer.Write(draft.BaseRevision.Value);
            WriteStringUx2A(writer, draft.SourceFingerprint);
            WriteStringUx2A(writer, draft.AreaDisplayName);
            WriteStringUx2A(writer, draft.AreaSlug);
            writer.Write(draft.AreaWidth);
            writer.Write(draft.AreaDepth);
            writer.Write(draft.ProjectRestEnabled);
            writer.Write(draft.Heartbeats.Count);
            foreach (var item in draft.Heartbeats)
            {
                WriteGuidUx2A(writer, item.Id);
                WriteStringUx2A(writer, item.Name);
                writer.Write(item.Enabled);
                writer.Write(item.IntervalMilliseconds);
                writer.Write(item.BudgetMilliseconds);
                writer.Write(item.SuspendWhenInactive);
                WriteStringUx2A(writer, item.ForgeScriptApiVersion);
                WriteStringUx2A(writer, item.Handler);
                WriteOptionalStringUx2A(writer, item.ScriptPath);
                writer.Write(item.RequiredAtRuntime);
            }
            WriteStringUx2A(writer, draft.PerceptionRulesetId);
            writer.Write(draft.PerceptionModifiers.Count);
            foreach (var item in draft.PerceptionModifiers)
            {
                WriteGuidUx2A(writer, item.Id);
                WriteStringUx2A(writer, item.SenseId);
                writer.Write(item.Multiplier);
                writer.Write(item.AdditiveModifier);
                WriteStringUx2A(writer, item.ConditionTag);
            }
            writer.Write((int)draft.RestMode);
            WriteStringUx2A(writer, draft.RestDenialReason);
            WriteStringUx2A(writer, draft.RestCondition);
            writer.Write(draft.RestCooldownSeconds);
            writer.Write(draft.RestRequiredSecureRadius);
            writer.Write(draft.RestVolumes.Count);
            foreach (var item in draft.RestVolumes)
            {
                WriteGuidUx2A(writer, item.Id);
                WriteStringUx2A(writer, item.Name);
                WriteVectorUx2A(writer, item.Center);
                WriteVectorUx2A(writer, item.Size);
                writer.Write((int)item.Mode);
                WriteStringUx2A(writer, item.DenialReason);
                WriteStringUx2A(writer, item.Condition);
                writer.Write(item.CooldownSeconds);
                writer.Write(item.RequiredSecureRadius);
                writer.Write(item.Priority);
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteGuidUx2A(BinaryWriter writer, Guid value)
    {
        var bytes = value.ToByteArray();
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteStringUx2A(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteOptionalStringUx2A(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
        {
            WriteStringUx2A(writer, value);
        }
    }

    private static void WriteVectorUx2A(BinaryWriter writer, Vector3Value value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    internal ProjectDefinition? CreateUx2AEvidenceProject()
    {
        var source = Project;
        return source is null ? null : CreateUx2AEvidenceProject(source.Id.Value);
    }

    internal static bool WriteUx2AEvidenceScript(string rootDirectory)
    {
        try
        {
            var root = Path.GetFullPath(rootDirectory);
            var script = Path.GetFullPath(Path.Combine(root, Ux2AEvidenceScript.Replace(
                '/', Path.DirectorySeparatorChar)));
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            if (!script.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(script)!);
            File.WriteAllText(script,
                "void main()\n{\n    // Preuve hors ligne : le Studio ne déclenche jamais ce handler.\n}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static ProjectDefinition CreateUx2AEvidenceProject(Guid projectId)
    {
        var targetId = AreaId.From(Guid.Parse("2a000000-0000-4000-8000-000000000001"));
        var neighborId = AreaId.From(Guid.Parse("2a000000-0000-4000-8000-000000000002"));
        var target = new AreaDefinition(
            targetId,
            "clairiere-vivante",
            "Clairière vivante",
            [],
            AreaKind.Exterior,
            new AreaDimensions(64, 64));
        var source = target.RuntimePolicies;
        var heartbeats = new[]
        {
            new HeartbeatScheduleDefinition(
                Guid.Parse("2a000000-0000-4000-8000-000000000011"),
                HeartbeatScheduleDefinition.CurrentSchemaVersion,
                "Respiration de la clairière",
                HeartbeatScope.Area,
                true,
                1_500,
                24,
                true,
                "1.0",
                "main",
                Ux2AEvidenceProvenance,
                Ux2AEvidenceScript,
                true),
            new HeartbeatScheduleDefinition(
                Guid.Parse("2a000000-0000-4000-8000-000000000012"),
                HeartbeatScheduleDefinition.CurrentSchemaVersion,
                "Veille des lucioles",
                HeartbeatScope.Area,
                false,
                4_000,
                12,
                false,
                "1.0",
                "main",
                Ux2AEvidenceProvenance),
        };
        var volumes = new[]
        {
            new RestPolicyVolume(
                Guid.Parse("2a000000-0000-4000-8000-000000000021"),
                "Sous-bois dangereux",
                new Vector3Value(-12, 40_000.125f, 8),
                new Vector3Value(12, 70_000.125f, 10),
                RestPermissionMode.Forbidden,
                "Le sous-bois est trop exposé pour installer le camp.",
                string.Empty,
                45,
                .1375f,
                80,
                Ux2AEvidenceProvenance),
            new RestPolicyVolume(
                Guid.Parse("2a000000-0000-4000-8000-000000000022"),
                "Cercle des pierres",
                new Vector3Value(10, 0, -9),
                new Vector3Value(14, 3, 14),
                RestPermissionMode.Conditional,
                "Le cercle doit être sécurisé avant le repos.",
                "camp.secure",
                30,
                6,
                40,
                Ux2AEvidenceProvenance),
        };
        var rest = new AreaRestPolicy(
            source.Rest.Id,
            source.Rest.SchemaVersion,
            RestPermissionMode.Conditional,
            "La clairière doit être sécurisée.",
            "area.secure",
            60,
            8.125f,
            volumes,
            Ux2AEvidenceProvenance);
        var perception = new EnvironmentPerceptionProfile(
            targetId.Value,
            EnvironmentPerceptionProfile.CurrentSchemaVersion,
            "forge.generic",
            [
                new EnvironmentPerceptionModifier(
                    Guid.Parse("2a000000-0000-4000-8000-000000000031"),
                    "hearing",
                    .8123456789,
                    .0375,
                    "wind"),
                new EnvironmentPerceptionModifier(
                    Guid.Parse("2a000000-0000-4000-8000-000000000032"),
                    "vision",
                    .55,
                    -.1,
                    "fog"),
                new EnvironmentPerceptionModifier(
                    Guid.Parse("2a000000-0000-4000-8000-000000000033"),
                    "vision",
                    1.1,
                    0,
                    string.Empty),
            ],
            Ux2AEvidenceProvenance);
        var rules = new AreaRulesProfile(
            source.Rules.Id,
            source.Rules.SchemaVersion,
            perception,
            source.Rules.Combat,
            Ux2AEvidenceProvenance);
        var policies = new AreaRuntimePolicyProfile(
            source.Id,
            source.SchemaVersion,
            heartbeats,
            rest,
            Ux2AEvidenceProvenance,
            source.Cinematics,
            rules,
            source.Experience);
        target = target.WithRuntimePolicies(policies);

        var neighbor = new AreaDefinition(
            neighborId,
            "lisiere-preservee",
            "Lisière préservée",
            [],
            AreaKind.Exterior,
            new AreaDimensions(48, 40));
        var neighborSource = neighbor.RuntimePolicies;
        var neighborHeartbeat = new HeartbeatScheduleDefinition(
            Guid.Parse("2a000000-0000-4000-8000-000000000041"),
            HeartbeatScheduleDefinition.CurrentSchemaVersion,
            "Mémoire de la lisière",
            HeartbeatScope.Area,
            false,
            8_000,
            8,
            true,
            "1.0",
            "main",
            "test:ux2-a-neighbor");
        neighbor = neighbor.WithRuntimePolicies(new AreaRuntimePolicyProfile(
            neighborSource.Id,
            neighborSource.SchemaVersion,
            [neighborHeartbeat],
            neighborSource.Rest,
            "test:ux2-a-neighbor",
            neighborSource.Cinematics,
            neighborSource.Rules,
            neighborSource.Experience));

        return new ProjectDefinition(
            ProjectId.From(projectId),
            "Studio UX2-A · Monde vivant",
            [],
            [target, neighbor]);
    }

    internal StudioUx2ASmokeResult RunUx2AContractSmoke()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux2a-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var fixture = CreateUx2AEvidenceProject(
                Guid.Parse("2a000000-0000-4000-8000-000000000099"));
            var created = ProjectSession.Create(tempRoot, fixture);
            if (!created.Succeeded || !WriteUx2AEvidenceScript(tempRoot) || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx2ASmoke();
            }

            var initialProject = editing.Project!;
            var target = initialProject.Areas.Single(area =>
                string.Equals(area.Slug, "clairiere-vivante", StringComparison.Ordinal));
            var neighbor = initialProject.Areas.Single(area =>
                string.Equals(area.Slug, "lisiere-preservee", StringComparison.Ordinal));
            var choices = editing.AreaPolicyChoicesUx2A;
            var choiceText = string.Join(' ', choices.Select(item => item.Label));
            var explicitSelection = editing.CreateAreaPolicyDraftUx2A() is null &&
                                    choices.Count == 2 &&
                                    choices.Select(item => item.Label)
                                        .Distinct(StringComparer.CurrentCultureIgnoreCase).Count() == 2 &&
                                    !ContainsTechnicalIdentifierUx2A(choiceText);
            var originalDraft = editing.CreateAreaPolicyDraftUx2A(target.Id)!;
            var completeCollections = originalDraft.Heartbeats.Count == 2 &&
                                      originalDraft.PerceptionModifiers.Count == 3 &&
                                      originalDraft.RestVolumes.Count == 2 &&
                                      originalDraft.Heartbeats.Select(item => item.Id).Distinct().Count() == 2 &&
                                      originalDraft.PerceptionModifiers.Select(item => item.Id).Distinct().Count() == 3 &&
                                      originalDraft.RestVolumes.Select(item => item.Id).Distinct().Count() == 2;
            var preservedVolume = originalDraft.RestVolumes.Single(item =>
                item.Id == Guid.Parse("2a000000-0000-4000-8000-000000000021"));
            var preservedPerception = originalDraft.PerceptionModifiers.Single(item =>
                item.Id == Guid.Parse("2a000000-0000-4000-8000-000000000031"));
            var numericValuesPreserved =
                preservedVolume.Center.Y.Equals(40_000.125f) &&
                preservedVolume.Size.Y.Equals(70_000.125f) &&
                preservedVolume.RequiredSecureRadius.Equals(.1375f) &&
                originalDraft.RestRequiredSecureRadius.Equals(8.125f) &&
                preservedPerception.Multiplier.Equals(.8123456789) &&
                preservedPerception.AdditiveModifier.Equals(.0375);
            var realScriptCatalog = editing.FindForgeScripts().Any(item =>
                                        string.Equals(item.RelativePath, Ux2AEvidenceScript,
                                            StringComparison.Ordinal)) &&
                                    originalDraft.Heartbeats.Any(item =>
                                        string.Equals(item.ScriptPath, Ux2AEvidenceScript,
                                            StringComparison.Ordinal));

            var invalidHeartbeat = editing.PreviewAreaPoliciesUx2A(originalDraft with
            {
                Heartbeats = originalDraft.Heartbeats.Select((item, index) => index == 0
                    ? item with { IntervalMilliseconds = 100, BudgetMilliseconds = 101 }
                    : item).ToArray(),
            });
            var missingScript = editing.PreviewAreaPoliciesUx2A(originalDraft with
            {
                Heartbeats = originalDraft.Heartbeats.Select((item, index) => index == 0
                    ? item with { ScriptPath = "scripts/missing-area-heartbeat.forge" }
                    : item).ToArray(),
            });
            var invalidHeartbeatBlocked = !invalidHeartbeat.Succeeded && !missingScript.Succeeded &&
                                          invalidHeartbeat.Groups.SelectMany(group => group.Diagnostics)
                                              .Any(item => item.Code ==
                                                  "studio.area_heartbeat.timing_invalid") &&
                                          missingScript.Groups.SelectMany(group => group.Diagnostics)
                                              .Any(item => item.Code ==
                                                  "studio.area_heartbeat.script_missing");

            var duplicatePerception = originalDraft.PerceptionModifiers[0] with
            {
                Id = Guid.Parse("2a000000-0000-4000-8000-000000000034"),
            };
            var invalidPerception = editing.PreviewAreaPoliciesUx2A(originalDraft with
            {
                PerceptionModifiers = originalDraft.PerceptionModifiers.Append(duplicatePerception).ToArray(),
            });
            var invalidPerceptionBlocked = !invalidPerception.Succeeded &&
                                           invalidPerception.Groups.SelectMany(group => group.Diagnostics)
                                               .Any(item => item.Code == "studio.area_perception.duplicate");

            var invalidRest = editing.PreviewAreaPoliciesUx2A(originalDraft with
            {
                RestVolumes = originalDraft.RestVolumes.Select((item, index) => index == 0
                    ? item with { Center = new Vector3Value(31, item.Center.Y, item.Center.Z) }
                    : item).ToArray(),
            });
            var invalidRestBlocked = !invalidRest.Succeeded &&
                                     invalidRest.Groups.SelectMany(group => group.Diagnostics)
                                         .Any(item => item.Code == RuntimePolicyValidator.RestVolumeBoundsCode);
            var structuredDiagnostics = invalidHeartbeat.Groups.Concat(invalidPerception.Groups)
                .Concat(invalidRest.Groups)
                .SelectMany(group => group.Diagnostics)
                .All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));

            StudioUx2ADraft Configure(StudioUx2ADraft sourceDraft) => sourceDraft with
            {
                Heartbeats = sourceDraft.Heartbeats.Select((item, index) => index == 0
                    ? item with { IntervalMilliseconds = 1_750, BudgetMilliseconds = 28 }
                    : item).ToArray(),
                PerceptionRulesetId = "forge.weather-aware",
                PerceptionModifiers = sourceDraft.PerceptionModifiers.Select(item =>
                    item.SenseId == "vision" && item.ConditionTag == "fog"
                        ? item with { Multiplier = .42, AdditiveModifier = -.15 }
                        : item).ToArray(),
                RestMode = RestPermissionMode.Forbidden,
                RestDenialReason = "La clairière reste dangereuse pendant l’alerte.",
                RestCondition = string.Empty,
                RestCooldownSeconds = 75,
            };

            var configured = Configure(editing.CreateAreaPolicyDraftUx2A(target.Id)!);
            var historyBeforePreview = editing.History!;
            var projectBeforePreview = editing.Project;
            var revisionBeforePreview = historyBeforePreview.State.Revision;
            var journalBeforePreview = historyBeforePreview.Journal.ToArray();
            var undoBeforePreview = historyBeforePreview.CanUndo;
            var redoBeforePreview = historyBeforePreview.CanRedo;
            var dirtyBeforePreview = editing.IsDirty;
            var dryPreview = editing.PreviewAreaPoliciesUx2A(configured);
            var dryRun = dryPreview.Succeeded && dryPreview.CanApply &&
                         ReferenceEquals(projectBeforePreview, editing.Project) &&
                         ReferenceEquals(historyBeforePreview, editing.History) &&
                         historyBeforePreview.State.Revision == revisionBeforePreview &&
                         historyBeforePreview.Journal.SequenceEqual(journalBeforePreview) &&
                         historyBeforePreview.CanUndo == undoBeforePreview &&
                         historyBeforePreview.CanRedo == redoBeforePreview &&
                         editing.IsDirty == dirtyBeforePreview;
            var humanPerceptionDiff = dryPreview.Diff.Perception.Count >= 2 &&
                                      dryPreview.Diff.Perception.Any(item => item.Contains(
                                          "Profil de règles",
                                          StringComparison.Ordinal)) &&
                                      dryPreview.Diff.Perception.Any(item => item.Contains(
                                          "Modificateur modifié",
                                          StringComparison.Ordinal));
            var dormantRestHonest = dryPreview.ProjectRestDormant &&
                                    dryPreview.Summary.Contains("Repos dormant", StringComparison.Ordinal) &&
                                    !editing.Project!.Module.RuntimePolicies.Rest.Enabled;
            var schedulerPolicy = dryPreview.SchedulerPolicyEvaluated &&
                                  dryPreview.Summary.Contains("sans exécuter", StringComparison.Ordinal);
            var perceptionPolicy = dryPreview.PerceptionPolicyEvaluated;
            var restPolicy = dryPreview.RestPolicyEvaluated;

            var mutableHeartbeats = configured.Heartbeats.ToArray();
            var mutableDraft = configured with { Heartbeats = mutableHeartbeats };
            var mutablePreview = editing.PreviewAreaPoliciesUx2A(mutableDraft);
            mutableHeartbeats[0] = mutableHeartbeats[0] with { BudgetMilliseconds = 29 };
            var staleFingerprintApply = editing.ApplyApprovedAreaPoliciesUx2A();
            var staleFingerprintBlocked = mutablePreview.Succeeded && !staleFingerprintApply.Succeeded &&
                                          staleFingerprintApply.Message.Contains(
                                              "Prévisualisez",
                                              StringComparison.Ordinal);

            var staleDraft = Configure(editing.CreateAreaPolicyDraftUx2A(target.Id)!);
            var staleDraftMutation = ApplyUx2ANeighborMutation(editing, neighbor.Id, "stale-draft");
            var staleDraftPreview = editing.PreviewAreaPoliciesUx2A(staleDraft);
            var staleDraftBlocked = staleDraftMutation && !staleDraftPreview.Succeeded &&
                                    staleDraftPreview.Message.Contains("projet a changé",
                                        StringComparison.OrdinalIgnoreCase) &&
                                    editing.Undo().Succeeded;

            var staleApprovalDraft = Configure(editing.CreateAreaPolicyDraftUx2A(target.Id)!);
            var staleApprovalPreview = editing.PreviewAreaPoliciesUx2A(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2ANeighborMutation(editing, neighbor.Id, "stale-approval");
            var staleApprovalApply = editing.ApplyApprovedAreaPoliciesUx2A();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded &&
                                       staleApprovalApply.Message.Contains("Prévisualisez",
                                           StringComparison.Ordinal) &&
                                       editing.Undo().Succeeded;

            var sourceProject = editing.Project!;
            var sourceTarget = sourceProject.Areas.Single(area => area.Id == target.Id);
            var sourceNeighbor = sourceProject.Areas.Single(area => area.Id == neighbor.Id);
            var freshDraft = Configure(editing.CreateAreaPolicyDraftUx2A(target.Id)!);
            var freshPreview = editing.PreviewAreaPoliciesUx2A(freshDraft);
            var applied = editing.ApplyApprovedAreaPoliciesUx2A();
            var appliedProject = editing.Project!;
            var appliedTarget = appliedProject.Areas.Single(area => area.Id == target.Id);
            var appliedNeighbor = appliedProject.Areas.Single(area => area.Id == neighbor.Id);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName ==
                                   "set_area_runtime_policies" &&
                                   appliedTarget.RuntimePolicies.Heartbeats[0].IntervalMilliseconds == 1_750 &&
                                   appliedTarget.RuntimePolicies.Rules.Perception.RulesetId ==
                                   "forge.weather-aware" &&
                                   appliedTarget.RuntimePolicies.Rest.Mode == RestPermissionMode.Forbidden &&
                                   appliedTarget.RuntimePolicies.Heartbeats.Select(item => item.Id).SequenceEqual(
                                       sourceTarget.RuntimePolicies.Heartbeats.Select(item => item.Id)) &&
                                   appliedTarget.RuntimePolicies.Rules.Perception.Modifiers.Select(item => item.Id)
                                       .SequenceEqual(sourceTarget.RuntimePolicies.Rules.Perception.Modifiers
                                           .Select(item => item.Id)) &&
                                   appliedTarget.RuntimePolicies.Rest.Volumes.Select(item => item.Id).SequenceEqual(
                                       sourceTarget.RuntimePolicies.Rest.Volumes.Select(item => item.Id));
            var targetNeighborsPreserved =
                ReferenceEquals(sourceTarget.RuntimePolicies.Cinematics,
                    appliedTarget.RuntimePolicies.Cinematics) &&
                ReferenceEquals(sourceTarget.RuntimePolicies.Rules.Combat,
                    appliedTarget.RuntimePolicies.Rules.Combat) &&
                ReferenceEquals(sourceTarget.RuntimePolicies.Experience,
                    appliedTarget.RuntimePolicies.Experience) &&
                ReferenceEquals(
                    sourceTarget.RuntimePolicies.Heartbeats.Single(item =>
                        item.Id == Guid.Parse("2a000000-0000-4000-8000-000000000012")),
                    appliedTarget.RuntimePolicies.Heartbeats.Single(item =>
                        item.Id == Guid.Parse("2a000000-0000-4000-8000-000000000012"))) &&
                sourceTarget.RuntimePolicies.Rest.Volumes.All(sourceVolume =>
                    ReferenceEquals(sourceVolume, appliedTarget.RuntimePolicies.Rest.Volumes.Single(item =>
                        item.Id == sourceVolume.Id))) &&
                sourceTarget.RuntimePolicies.Rules.Perception.Modifiers
                    .Where(item => !(item.SenseId == "vision" && item.ConditionTag == "fog"))
                    .All(sourceModifier => ReferenceEquals(
                        sourceModifier,
                        appliedTarget.RuntimePolicies.Rules.Perception.Modifiers.Single(item =>
                            item.Id == sourceModifier.Id))) &&
                ReferenceEquals(sourceTarget.Navigation, appliedTarget.Navigation) &&
                ReferenceEquals(sourceTarget.Environment, appliedTarget.Environment) &&
                SameAreaAuthoringUx2A(sourceTarget, appliedTarget) &&
                ReferenceEquals(sourceTarget.World, appliedTarget.World) &&
                ReferenceEquals(sourceTarget.Foundation, appliedTarget.Foundation) &&
                ReferenceEquals(sourceTarget.Playtest, appliedTarget.Playtest);
            var projectNeighborsPreserved = ReferenceEquals(sourceProject.Module, appliedProject.Module) &&
                                            ReferenceEquals(sourceNeighbor, appliedNeighbor) &&
                                            sourceProject.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                            sourceProject.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                            sourceProject.Quests.SequenceEqual(appliedProject.Quests) &&
                                            sourceProject.Areas.Select(area => area.Id)
                                                .SequenceEqual(appliedProject.Areas.Select(area => area.Id));

            var appliedAreaJson = AreaJsonSerializer.Serialize(appliedTarget);
            var appliedProjectSnapshot = appliedProject;
            var undo = editing.Undo();
            var undone = ReferenceEquals(sourceProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            var reopen = reopened.Open(tempRoot, target.Id.ToString());
            var restoredProject = reopened.Project;
            var restoredTarget = restoredProject?.Areas.SingleOrDefault(area => area.Id == target.Id);
            var persistence = save.Succeeded && reopen.Succeeded && restoredTarget is not null &&
                               string.Equals(appliedAreaJson, AreaJsonSerializer.Serialize(restoredTarget),
                                   StringComparison.Ordinal) &&
                              restoredProject is not null &&
                              ProjectRoundTripMatchesUx2A(appliedProjectSnapshot, restoredProject, target.Id);

            var creature = new CreaturePerceptionProfile(
                [new(SenseKind.Vision, 10, 120, true, new HashSet<string> { "light" })],
                15);
            var detected = EnvironmentPerceptionService.Detect(
                creature,
                appliedTarget.RuntimePolicies.Rules.Perception,
                new HashSet<string> { "fog" },
                Vector3Value.Zero,
                [new Stimulus("lantern", "light", new Vector3Value(3, 0, 0), 1)],
                5);
            perceptionPolicy &= detected.Count == 1 && double.IsFinite(detected[0].Confidence);

            var humanText = !ContainsTechnicalIdentifierUx2A(choiceText + " " + dryPreview.Summary + " " +
                            string.Join(' ', dryPreview.Diff.Heartbeats) + " " +
                            string.Join(' ', dryPreview.Diff.Perception) + " " +
                            string.Join(' ', dryPreview.Diff.Rest));
            var succeeded = explicitSelection && realScriptCatalog && completeCollections &&
                            numericValuesPreserved &&
                            invalidHeartbeatBlocked && invalidPerceptionBlocked && invalidRestBlocked && dryRun &&
                            humanPerceptionDiff && dormantRestHonest && schedulerPolicy && perceptionPolicy &&
                            restPolicy && staleDraftBlocked && staleFingerprintBlocked && staleApprovalBlocked &&
                            canonicalCommand && undoRedo && persistence && targetNeighborsPreserved &&
                            projectNeighborsPreserved && structuredDiagnostics && humanText;
            return new StudioUx2ASmokeResult(
                succeeded,
                explicitSelection,
                realScriptCatalog,
                completeCollections,
                numericValuesPreserved,
                invalidHeartbeatBlocked,
                invalidPerceptionBlocked,
                invalidRestBlocked,
                dryRun,
                humanPerceptionDiff,
                dormantRestHonest,
                schedulerPolicy,
                perceptionPolicy,
                restPolicy,
                staleDraftBlocked,
                staleFingerprintBlocked,
                staleApprovalBlocked,
                canonicalCommand,
                undoRedo,
                persistence,
                targetNeighborsPreserved,
                projectNeighborsPreserved,
                structuredDiagnostics,
                humanText);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2A_CONTRACT_FAILED type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2ASmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            DeleteUx2ATemporaryWorkspace(tempRoot);
        }
    }

    internal static bool DeleteUx2ATemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return true;
        }
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2a-", StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool ApplyUx2ANeighborMutation(
        StudioSessionController controller,
        AreaId areaId,
        string marker)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null)
        {
            return false;
        }
        var source = area.RuntimePolicies;
        var candidate = new AreaRuntimePolicyProfile(
            source.Id,
            source.SchemaVersion,
            source.Heartbeats,
            source.Rest,
            $"studio:ux2-a:{marker}",
            source.Cinematics,
            source.Rules,
            source.Experience);
        var result = controller._setAreaRuntimePoliciesUx2A.Handle(
            history.State,
            new SetAreaRuntimePoliciesCommand(
                CommandId.From(Guid.NewGuid()),
                history.State.Revision,
                area.Id,
                candidate,
                false));
        if (!result.Succeeded)
        {
            return false;
        }
        history.Record(result);
        return true;
    }

    private static bool ContainsTechnicalIdentifierUx2A(string text) =>
        text.Contains("UUID", StringComparison.OrdinalIgnoreCase) ||
        text.Contains(".json", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static StudioUx2ASmokeResult FailedUx2ASmoke() => new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6MProvenance = "studio:ux6-m:script-automation";
    private readonly SetWorldFeatureCatalogHandler _setScriptAutomationCatalogUx6M = new(new SystemClock());
    private StudioUx6MAutomationDraft? _approvedScriptAutomationUx6M;
    private ProjectRevision? _approvedScriptAutomationRevisionUx6M;
    private string? _approvedScriptAutomationSourceFingerprintUx6M;
    private string? _approvedScriptAutomationDraftFingerprintUx6M;

    internal IReadOnlyList<StudioUx6MModuleChoice> ModuleChoicesUx6M()
    {
        var project = Project;
        return project is null
            ? []
            : [new(project.Module.Id.Value, project.Module.DisplayName,
                $"Module {project.Module.Slug} · automatisation ForgeScript")];
    }

    internal StudioUx6MAutomationDraft? CreateScriptAutomationDraftUx6M(Guid? moduleId)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || moduleId != project.Module.Id.Value) return null;
        var profile = project.Module.WorldFeatures.Scripts;
        return new(history.State.Revision, ProjectFingerprintUx6M(project), profile.Id, profile.SchemaVersion,
            profile.Provenance, profile.Camera,
            new(profile.TagRouting.Enabled, profile.TagRouting.ExplicitOnly,
                profile.TagRouting.Routes.ToArray()),
            profile.ModuleFlags.ToArray(), profile.AiSettings.ToArray(), profile);
    }

    internal StudioUx6MPreview PreviewScriptAutomationUx6M(StudioUx6MAutomationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelScriptAutomationPreviewUx6M();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedPreviewUx6M("Aucun module n’est ouvert.");
        if (draft.BaseRevision != history.State.Revision)
            return FailedPreviewUx6M("Le projet a changé. Rechargez l’automatisation avant l’aperçu.",
                [DiagnosticUx6M("command.revision_conflict", "module/worldFeatures/scripts",
                    "La révision du projet n’est plus celle du brouillon.")]);
        if (!string.Equals(draft.SourceFingerprint, ProjectFingerprintUx6M(project), StringComparison.Ordinal))
            return FailedPreviewUx6M("Le catalogue a changé. Rechargez l’automatisation.",
                [DiagnosticUx6M("world.script.source_stale", "module/worldFeatures/scripts",
                    "Le catalogue source n’est plus identique.")]);

        var sourceProject = project;
        var sourceRevision = history.State.Revision;
        var sourceJournal = history.Journal.Count;
        if (!TryBuildScriptAutomationCandidateUx6M(draft, true, out var command, out var candidate,
                out var diagnostics))
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "L’automatisation contient une valeur invalide.",
                string.Empty, BuildScriptAutomationDiffUx6M(draft.OriginalProfile, candidate), diagnostics,
                null, true);

        var result = _setScriptAutomationCatalogUx6M.Handle(history.State, command!);
        if (!result.Succeeded)
        {
            var commandDiagnostics = result.Diagnostics.Select(item => DiagnosticUx6M(item.Code,
                "module/worldFeatures/scripts", HumanScriptAutomationDiagnosticUx6M(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé l’automatisation.",
                result.Patch?.Summary ?? string.Empty,
                BuildScriptAutomationDiffUx6M(draft.OriginalProfile, candidate), commandDiagnostics, null, true);
        }

        var resolution = ResolveScriptAutomationUx6M(candidate!);
        var diff = BuildScriptAutomationDiffUx6M(draft.OriginalProfile, candidate);
        var changed = !string.Equals(AutomationProfileFingerprintUx6M(draft.OriginalProfile),
            AutomationProfileFingerprintUx6M(candidate!), StringComparison.Ordinal);
        var dryRunPure = ReferenceEquals(sourceProject, Project) && history.State.Revision == sourceRevision &&
                         history.Journal.Count == sourceJournal;
        if (changed && dryRunPure)
        {
            _approvedScriptAutomationUx6M = draft;
            _approvedScriptAutomationRevisionUx6M = history.State.Revision;
            _approvedScriptAutomationSourceFingerprintUx6M = draft.SourceFingerprint;
            _approvedScriptAutomationDraftFingerprintUx6M = ScriptAutomationDraftFingerprintUx6M(draft);
        }
        return new(true, changed,
            changed
                ? "Aperçu sec valide · caméra, tags et switches peuvent être appliqués."
                : "Aperçu sec valide · le profil est déjà identique au document canonique.",
            result.Patch?.Summary ?? string.Empty, diff, [], resolution, dryRunPure);
    }

    internal StudioUx6MActionResult ApplyApprovedScriptAutomationUx6M(StudioUx6MAutomationDraft draft)
    {
        var project = Project;
        var history = History;
        if (project is null || history is null || _approvedScriptAutomationUx6M is null ||
            _approvedScriptAutomationRevisionUx6M != history.State.Revision ||
            !string.Equals(_approvedScriptAutomationSourceFingerprintUx6M,
                ProjectFingerprintUx6M(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedScriptAutomationDraftFingerprintUx6M,
                ScriptAutomationDraftFingerprintUx6M(draft), StringComparison.Ordinal) ||
            !string.Equals(ScriptAutomationDraftFingerprintUx6M(_approvedScriptAutomationUx6M),
                ScriptAutomationDraftFingerprintUx6M(draft), StringComparison.Ordinal))
        {
            CancelScriptAutomationPreviewUx6M();
            return new(false, "Relancez l’aperçu sec avant d’appliquer l’automatisation.");
        }
        if (!TryBuildScriptAutomationCandidateUx6M(draft, false, out var command, out _, out var diagnostics))
        {
            CancelScriptAutomationPreviewUx6M();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "L’automatisation est invalide.");
        }
        var result = _setScriptAutomationCatalogUx6M.Handle(history.State, command!);
        CancelScriptAutomationPreviewUx6M();
        if (!result.Succeeded)
            return new(false, HumanScriptAutomationDiagnosticUx6M(result.Diagnostics.FirstOrDefault()?.Code));
        history.Record(result);
        InvalidateContentIndexes();
        return new(true, "Automatisation appliquée · Ctrl+Z pour annuler, Ctrl+Y pour rétablir.");
    }

    internal StudioUx6MActionResult UndoScriptAutomationUx6M()
    {
        CancelScriptAutomationPreviewUx6M();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6MActionResult RedoScriptAutomationUx6M()
    {
        CancelScriptAutomationPreviewUx6M();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6MActionResult SaveScriptAutomationUx6M()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal void CancelScriptAutomationPreviewUx6M()
    {
        _approvedScriptAutomationUx6M = null;
        _approvedScriptAutomationRevisionUx6M = null;
        _approvedScriptAutomationSourceFingerprintUx6M = null;
        _approvedScriptAutomationDraftFingerprintUx6M = null;
    }

    internal StudioUx6MApprovalSnapshot CaptureScriptAutomationApprovalUx6M() => new(
        _approvedScriptAutomationUx6M, _approvedScriptAutomationRevisionUx6M,
        _approvedScriptAutomationSourceFingerprintUx6M, _approvedScriptAutomationDraftFingerprintUx6M);

    internal void RestoreScriptAutomationApprovalUx6M(StudioUx6MApprovalSnapshot snapshot)
    {
        _approvedScriptAutomationUx6M = snapshot.Draft;
        _approvedScriptAutomationRevisionUx6M = snapshot.Revision;
        _approvedScriptAutomationSourceFingerprintUx6M = snapshot.SourceFingerprint;
        _approvedScriptAutomationDraftFingerprintUx6M = snapshot.DraftFingerprint;
    }

    internal void CloseUx6M() => CancelScriptAutomationPreviewUx6M();

    internal ProjectDefinition? CreateUx6MEvidenceProject()
    {
        var project = Project;
        if (project is null) return null;
        var moduleId = project.Module.Id.Value;
        var scripts = new ScriptAutomationProfile(moduleId, 1, "studio:ux6-m:evidence",
            new([
                    new(StableUx6M(moduleId, "camera-shot"), "rig.dialogue", "shot.ash-gate", 3, .5,
                        true, true),
                ], "restore-camera-and-control", true, true),
            new(true, true,
                [new("door.ash", "opened", "scripts/door_ash_opened", true)]),
            [new("rest.enabled", ScriptAutomationProfile.SettingKind.Boolean, "true",
                ScriptAutomationProfile.FlagScope.Module, "Autorise le repos sûr du module.", 1)],
            [new("combat.aggression", ScriptAutomationProfile.SettingKind.Choice, "balanced",
                ["passive", "balanced", "aggressive"], "Aperçu borné du comportement de combat.", true)]);
        return ReplaceScriptsUx6M(project, scripts);
    }

    internal StudioUx6MSmokeResult RunUx6MContractSmoke()
    {
        var evidence = CreateUx6MEvidenceProject();
        if (evidence is null) return FailedSmokeUx6M();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-m-contract",
            Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        StudioSessionController? reopened = null;
        try
        {
            if (!ProjectSession.Create(root, evidence).Succeeded || !editing.Open(root).Succeeded)
                return FailedSmokeUx6M();
            var initial = editing.Project!;
            var initialScripts = initial.Module.WorldFeatures.Scripts;
            var draft = editing.CreateScriptAutomationDraftUx6M(initial.Module.Id.Value);
            if (draft is null) return FailedSmokeUx6M();

            var invalidCamera = !editing.PreviewScriptAutomationUx6M(draft with
            {
                Camera = draft.Camera with
                {
                    Shots = [draft.Camera.Shots[0] with { DurationSeconds = double.NaN }],
                },
            }).Succeeded;
            var invalidTag = !editing.PreviewScriptAutomationUx6M(draft with
            {
                TagRouting = draft.TagRouting with
                {
                    Routes = [draft.TagRouting.Routes[0] with { Event = string.Empty }],
                },
            }).Succeeded;
            var invalidFlag = !editing.PreviewScriptAutomationUx6M(draft with
            {
                ModuleFlags = [draft.ModuleFlags[0] with { DefaultValue = "not-a-boolean" }],
            }).Succeeded;
            var invalidAi = !editing.PreviewScriptAutomationUx6M(draft with
            {
                AiSettings = [draft.AiSettings[0] with { DefaultValue = "unsupported" }],
            }).Succeeded;

            var candidate = draft with
            {
                Camera = draft.Camera with
                {
                    Shots = [draft.Camera.Shots[0] with { DurationSeconds = 4.25 }],
                    AbortRecovery = "restore-camera-and-control-reviewed",
                },
                TagRouting = draft.TagRouting with
                {
                    Routes = [draft.TagRouting.Routes[0] with
                        { ScriptId = "scripts/door_ash_opened_reviewed" }],
                },
                ModuleFlags = [draft.ModuleFlags[0] with { DefaultValue = "false" }],
                AiSettings = [draft.AiSettings[0] with { DefaultValue = "aggressive" }],
            };
            var sourceProject = editing.Project;
            var sourceRevision = editing.History!.State.Revision;
            var sourceJournal = editing.History.Journal.Count;
            var preview = editing.PreviewScriptAutomationUx6M(candidate);
            var dryRun = preview.Succeeded && preview.CanApply && preview.DryRunPure &&
                         ReferenceEquals(sourceProject, editing.Project) &&
                         editing.History.State.Revision == sourceRevision &&
                         editing.History.Journal.Count == sourceJournal;
            var camera = preview.Resolution is
            {
                CameraSequencePlanned: true, CameraControlLocked: true, CameraControlRestored: true,
                AccessibilitySkip: true
            };
            var tags = preview.Resolution is { TagRouteResolved: true } resolution &&
                       resolution.ResolvedScript == "scripts/door_ash_opened_reviewed";
            var flags = preview.Resolution is { ModuleFlagResolved: true, ModuleFlag: "rest.enabled" };
            var ai = preview.Resolution is
            { AiSettingResolved: true, AiSetting: "combat.aggression", InvalidAiFallback: true };
            var staleSource = !editing.PreviewScriptAutomationUx6M(candidate with
            { BaseRevision = candidate.BaseRevision.Next() }).Succeeded;
            _ = editing.PreviewScriptAutomationUx6M(candidate);
            var staleApproval = !editing.ApplyApprovedScriptAutomationUx6M(candidate with
            {
                Camera = candidate.Camera with { AbortRecovery = "changed-after-approval" },
            }).Succeeded;
            _ = editing.PreviewScriptAutomationUx6M(candidate);
            var applied = editing.ApplyApprovedScriptAutomationUx6M(candidate);
            var appliedProfile = editing.Project!.Module.WorldFeatures.Scripts;
            var canonical = applied.Succeeded && appliedProfile.Camera.Shots[0].DurationSeconds == 4.25 &&
                            appliedProfile.TagRouting.Routes[0].ScriptId.EndsWith("_reviewed",
                                StringComparison.Ordinal) && appliedProfile.ModuleFlags[0].DefaultValue == "false" &&
                            appliedProfile.AiSettings[0].DefaultValue == "aggressive";
            var onlyScriptsChanged = string.Equals(ProjectJsonSerializer.Serialize(
                    ReplaceScriptsUx6M(editing.Project, initialScripts)),
                ProjectJsonSerializer.Serialize(initial), StringComparison.Ordinal);
            var appliedFingerprint = AutomationProfileFingerprintUx6M(appliedProfile);
            var undo = editing.UndoScriptAutomationUx6M();
            var undoRestored = undo.Succeeded && string.Equals(
                AutomationProfileFingerprintUx6M(editing.Project!.Module.WorldFeatures.Scripts),
                AutomationProfileFingerprintUx6M(initialScripts), StringComparison.Ordinal);
            var redo = editing.RedoScriptAutomationUx6M();
            var redoRestored = redo.Succeeded && string.Equals(
                AutomationProfileFingerprintUx6M(editing.Project!.Module.WorldFeatures.Scripts),
                appliedFingerprint, StringComparison.Ordinal);
            var saved = editing.SaveScriptAutomationUx6M();
            var expected = ProjectJsonSerializer.Serialize(editing.Project!);
            reopened = new StudioSessionController();
            var persistence = saved.Succeeded && reopened.Open(root).Succeeded && reopened.Project is not null &&
                              string.Equals(ProjectJsonSerializer.Serialize(reopened.Project), expected,
                                  StringComparison.Ordinal);
            reopened.Close();
            reopened = null;
            editing.Close();
            var released = DeleteUx6FTemporaryWorkspace(root);
            var succeeded = camera && tags && flags && ai && invalidCamera && invalidTag && invalidFlag && invalidAi &&
                            dryRun && staleSource && staleApproval && canonical && undoRestored && redoRestored &&
                            persistence && onlyScriptsChanged && released;
            return new(succeeded, true, camera, tags, flags, ai, invalidCamera, invalidTag, invalidFlag,
                invalidAi, dryRun, staleSource, staleApproval, canonical, undoRestored && redoRestored,
                persistence, onlyScriptsChanged, released);
        }
        catch
        {
            return FailedSmokeUx6M();
        }
        finally
        {
            reopened?.Close();
            editing.Close();
            if (Directory.Exists(root)) _ = DeleteUx6FTemporaryWorkspace(root);
        }
    }

    private bool TryBuildScriptAutomationCandidateUx6M(StudioUx6MAutomationDraft draft, bool dryRun,
        out SetWorldFeatureCatalogCommand? command, out ScriptAutomationProfile? candidate,
        out IReadOnlyList<StudioUx6MDiagnostic> diagnostics)
    {
        command = null;
        candidate = null;
        var project = Project;
        var history = History;
        var found = ValidateScriptAutomationDraftUx6M(draft).ToList();
        if (project is null || history is null)
            found.Add(DiagnosticUx6M("project.missing", "module/worldFeatures/scripts",
                "Aucun module n’est ouvert."));
        else if (draft.ProfileId != project.Module.Id.Value)
            found.Add(DiagnosticUx6M("world.script.identity_invalid", "module/worldFeatures/scripts/id",
                "Le profil doit appartenir au module ouvert."));
        if (found.Count > 0)
        {
            diagnostics = found;
            return false;
        }
        candidate = new(draft.ProfileId, draft.SchemaVersion, draft.Provenance, draft.Camera,
            new(draft.TagRouting.Enabled, draft.TagRouting.ExplicitOnly, draft.TagRouting.Routes.ToArray()),
            draft.ModuleFlags.ToArray(), draft.AiSettings.ToArray());
        var source = project!.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, source.Encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, candidate);
        command = new(CommandId.From(Guid.NewGuid()), history!.State.Revision, catalog, dryRun);
        diagnostics = [];
        return true;
    }

    private static IReadOnlyList<StudioUx6MDiagnostic> ValidateScriptAutomationDraftUx6M(
        StudioUx6MAutomationDraft draft)
    {
        var diagnostics = new List<StudioUx6MDiagnostic>();
        const string root = "module/worldFeatures/scripts";
        if (draft.ProfileId == Guid.Empty || draft.SchemaVersion != 1 || string.IsNullOrWhiteSpace(draft.Provenance))
            diagnostics.Add(DiagnosticUx6M("world.script.identity_invalid", root,
                "Identité, schéma et provenance du profil sont requis."));
        if (string.IsNullOrWhiteSpace(draft.Camera.AbortRecovery))
            diagnostics.Add(DiagnosticUx6M("world.script.camera_recovery_missing", $"{root}/camera/abortRecovery",
                "Décrivez la restauration après interruption."));
        if (draft.Camera.Shots.Select(item => item.Id).Distinct().Count() != draft.Camera.Shots.Count ||
            draft.Camera.Shots.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.RigId) ||
                string.IsNullOrWhiteSpace(item.ShotId) || !double.IsFinite(item.DurationSeconds) ||
                item.DurationSeconds <= 0 || !double.IsFinite(item.BlendSeconds) || item.BlendSeconds < 0))
            diagnostics.Add(DiagnosticUx6M("world.script.camera_invalid", $"{root}/camera/shots",
                "Chaque plan exige une identité protégée, un rig, un nom, une durée positive et un fondu fini."));
        var tagKeys = draft.TagRouting.Routes.Select(item => $"{item.BlueprintTag}\u001f{item.Event}").ToArray();
        if (tagKeys.Distinct(StringComparer.Ordinal).Count() != tagKeys.Length ||
            draft.TagRouting.Routes.Any(item => !ValidAutomationKeyUx6M(item.BlueprintTag) ||
                !ValidAutomationKeyUx6M(item.Event) || !ValidScriptPathUx6M(item.ScriptId)))
            diagnostics.Add(DiagnosticUx6M("world.script.tag_route_invalid", $"{root}/tagRouting/routes",
                "Chaque route tag/événement doit être unique et pointer vers un script sûr."));
        if (draft.ModuleFlags.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() !=
            draft.ModuleFlags.Count || draft.ModuleFlags.Any(item => !ValidAutomationKeyUx6M(item.Id) ||
                !Enum.IsDefined(item.Kind) || !Enum.IsDefined(item.Scope) ||
                !ValidSettingValueUx6M(item.Kind, item.DefaultValue, null) ||
                string.IsNullOrWhiteSpace(item.Documentation) || item.IntroducedSchemaVersion < 1))
            diagnostics.Add(DiagnosticUx6M("world.script.flag_invalid", $"{root}/moduleFlags",
                "Les switches module exigent un nom unique, un type, une valeur compatible et une documentation."));
        if (draft.AiSettings.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() !=
            draft.AiSettings.Count || draft.AiSettings.Any(item => !ValidAutomationKeyUx6M(item.Id) ||
                !Enum.IsDefined(item.Kind) || string.IsNullOrWhiteSpace(item.Preview) ||
                item.AllowedValues.Distinct(StringComparer.Ordinal).Count() != item.AllowedValues.Count ||
                item.AllowedValues.Any(string.IsNullOrWhiteSpace) ||
                !ValidSettingValueUx6M(item.Kind, item.DefaultValue, item.AllowedValues)))
            diagnostics.Add(DiagnosticUx6M("world.script.ai_setting_invalid", $"{root}/aiSettings",
                "Les switches IA exigent un nom unique, une valeur typée et un aperçu borné."));
        return diagnostics;
    }

    private static bool ValidAutomationKeyUx6M(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Contains("..", StringComparison.Ordinal) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or ':');

    private static bool ValidScriptPathUx6M(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 240 && !value.Contains("..", StringComparison.Ordinal) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or ':' or '/');

    private static bool ValidSettingValueUx6M(ScriptAutomationProfile.SettingKind kind, string value,
        IReadOnlyList<string>? allowed) => kind switch
        {
            ScriptAutomationProfile.SettingKind.Boolean => bool.TryParse(value, out _),
            ScriptAutomationProfile.SettingKind.Integer => long.TryParse(value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out _),
            ScriptAutomationProfile.SettingKind.Decimal => double.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number) && double.IsFinite(number),
            ScriptAutomationProfile.SettingKind.Text => value is not null,
            ScriptAutomationProfile.SettingKind.Choice => !string.IsNullOrWhiteSpace(value) &&
                (allowed is null || allowed.Count == 0 || allowed.Contains(value, StringComparer.Ordinal)),
            _ => false,
        };

    private static StudioUx6MResolution ResolveScriptAutomationUx6M(ScriptAutomationProfile profile)
    {
        var route = profile.TagRouting.Routes.FirstOrDefault(item => item.Enabled);
        var flag = profile.ModuleFlags.FirstOrDefault();
        var ai = profile.AiSettings.FirstOrDefault();
        var flagValue = flag is null ? null : AlternativeFlagValueUx6M(flag);
        var aiValue = ai?.AllowedValues.Skip(1).FirstOrDefault() ?? ai?.AllowedValues.FirstOrDefault() ??
                      ai?.DefaultValue;
        var resolution = WorldFeatureRuntimeResolver.ResolveScript(profile, route?.BlueprintTag ?? string.Empty,
            route?.Event ?? string.Empty,
            flag is null ? null : new Dictionary<string, string> { [flag.Id] = flagValue! },
            ai is null ? null : new Dictionary<string, string> { [ai.Id] = aiValue! },
            cameraSequence: profile.Camera.Shots.Count > 0);
        var fallback = ai is null ? null : WorldFeatureRuntimeResolver.ResolveScript(profile, string.Empty,
            string.Empty, aiOverrides: new Dictionary<string, string> { [ai.Id] = "__invalid__" });
        var trace = new List<string>
        {
            $"Caméra · {profile.Camera.Shots.Count} plan(s) · récupération {profile.Camera.AbortRecovery}",
            route is null ? "Tags · aucune route active" : $"Tags · {route.BlueprintTag}/{route.Event} → {route.ScriptId}",
            flag is null ? "Module · aucun switch" : $"Module · {flag.Id} = {resolution.Flags[flag.Id]}",
            ai is null ? "IA · aucun switch" : $"IA · {ai.Id} = {resolution.AiSettings[ai.Id]}",
        };
        return new(profile.Camera.Shots.Count > 0, resolution.CameraControlLocked,
            profile.Camera.RestorePlayerControl && !string.IsNullOrWhiteSpace(resolution.AbortRecovery),
            profile.Camera.AccessibilitySkipEnabled,
            route is not null && string.Equals(resolution.ScriptId, route.ScriptId, StringComparison.Ordinal),
            resolution.ScriptId ?? string.Empty,
            flag is not null && string.Equals(resolution.Flags.GetValueOrDefault(flag.Id), flagValue,
                StringComparison.Ordinal), flag?.Id ?? string.Empty,
            flag is null ? string.Empty : resolution.Flags.GetValueOrDefault(flag.Id) ?? string.Empty,
            ai is not null && string.Equals(resolution.AiSettings.GetValueOrDefault(ai.Id), aiValue,
                StringComparison.Ordinal), ai?.Id ?? string.Empty,
            ai is null ? string.Empty : resolution.AiSettings.GetValueOrDefault(ai.Id) ?? string.Empty,
            ai is not null && string.Equals(fallback!.AiSettings.GetValueOrDefault(ai.Id), ai.DefaultValue,
                StringComparison.Ordinal), trace);
    }

    private static string AlternativeFlagValueUx6M(ScriptAutomationProfile.FeatureFlag flag) => flag.Kind switch
    {
        ScriptAutomationProfile.SettingKind.Boolean => bool.TryParse(flag.DefaultValue, out var boolean) && boolean
            ? "false" : "true",
        ScriptAutomationProfile.SettingKind.Integer => long.TryParse(flag.DefaultValue, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var integer) ? (integer + 1).ToString(CultureInfo.InvariantCulture) : "1",
        ScriptAutomationProfile.SettingKind.Decimal => double.TryParse(flag.DefaultValue, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var number) ? (number + 1).ToString("R", CultureInfo.InvariantCulture) : "1",
        ScriptAutomationProfile.SettingKind.Text => flag.DefaultValue + ".preview",
        _ => flag.DefaultValue,
    };

    private static IReadOnlyList<string> BuildScriptAutomationDiffUx6M(ScriptAutomationProfile source,
        ScriptAutomationProfile? candidate)
    {
        if (candidate is null) return ["Automatisation · candidat indisponible"];
        return
        [
            $"Caméra · {source.Camera.Shots.Count} → {candidate.Camera.Shots.Count} plan(s) · récupération {source.Camera.AbortRecovery} → {candidate.Camera.AbortRecovery}",
            $"Routage tags · {source.TagRouting.Routes.Count} → {candidate.TagRouting.Routes.Count} route(s) · actif {source.TagRouting.Enabled} → {candidate.TagRouting.Enabled}",
            $"Switches module · {source.ModuleFlags.Count} → {candidate.ModuleFlags.Count}",
            $"Switches IA · {source.AiSettings.Count} → {candidate.AiSettings.Count}",
        ];
    }

    private static StudioUx6MPreview FailedPreviewUx6M(string message,
        IReadOnlyList<StudioUx6MDiagnostic>? diagnostics = null) =>
        new(false, false, message, string.Empty, [], diagnostics ?? [], null, true);

    private static StudioUx6MDiagnostic DiagnosticUx6M(string code, string path, string message) =>
        new(code, path, "error", message);

    private static string HumanScriptAutomationDiagnosticUx6M(string? code) => code switch
    {
        "command.revision_conflict" => "Le projet a changé. Rechargez le profil.",
        "world.script.camera_invalid" => "Un plan caméra est invalide.",
        "world.script.tag_route_invalid" => "Une route tag/événement est invalide.",
        "world.script.flag_invalid" => "Un switch module est invalide.",
        "world.script.ai_setting_invalid" => "Un switch IA est invalide.",
        _ => "La commande canonique a refusé l’automatisation.",
    };

    private static ProjectDefinition ReplaceScriptsUx6M(ProjectDefinition project,
        ScriptAutomationProfile scripts)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, source.Encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, scripts);
        var module = project.Module.WithWorldFeatures(catalog);
        return new(project.Id, project.Title, module, project.Blueprints, project.Areas, project.Dialogues,
            project.Quests);
    }

    private static string ProjectFingerprintUx6M(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string ScriptAutomationDraftFingerprintUx6M(StudioUx6MAutomationDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(draft.BaseRevision.Value);
        WriteUx6M(writer, draft.SourceFingerprint);
        WriteAutomationProfileUx6M(writer, new(draft.ProfileId, draft.SchemaVersion, draft.Provenance,
            draft.Camera, draft.TagRouting, draft.ModuleFlags, draft.AiSettings));
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string AutomationProfileFingerprintUx6M(ScriptAutomationProfile profile)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        WriteAutomationProfileUx6M(writer, profile);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteAutomationProfileUx6M(BinaryWriter writer, ScriptAutomationProfile profile)
    {
        writer.Write(profile.Id.ToByteArray());
        writer.Write(profile.SchemaVersion);
        WriteUx6M(writer, profile.Provenance);
        writer.Write(profile.Camera.Shots.Count);
        foreach (var shot in profile.Camera.Shots)
        {
            writer.Write(shot.Id.ToByteArray());
            WriteUx6M(writer, shot.RigId);
            WriteUx6M(writer, shot.ShotId);
            writer.Write(shot.DurationSeconds);
            writer.Write(shot.BlendSeconds);
            writer.Write(shot.LockPlayerControl);
            writer.Write(shot.Skippable);
        }
        WriteUx6M(writer, profile.Camera.AbortRecovery);
        writer.Write(profile.Camera.RestorePlayerControl);
        writer.Write(profile.Camera.AccessibilitySkipEnabled);
        writer.Write(profile.TagRouting.Enabled);
        writer.Write(profile.TagRouting.ExplicitOnly);
        writer.Write(profile.TagRouting.Routes.Count);
        foreach (var route in profile.TagRouting.Routes)
        {
            WriteUx6M(writer, route.BlueprintTag);
            WriteUx6M(writer, route.Event);
            WriteUx6M(writer, route.ScriptId);
            writer.Write(route.Enabled);
        }
        writer.Write(profile.ModuleFlags.Count);
        foreach (var flag in profile.ModuleFlags)
        {
            WriteUx6M(writer, flag.Id);
            writer.Write((int)flag.Kind);
            WriteUx6M(writer, flag.DefaultValue);
            writer.Write((int)flag.Scope);
            WriteUx6M(writer, flag.Documentation);
            writer.Write(flag.IntroducedSchemaVersion);
        }
        writer.Write(profile.AiSettings.Count);
        foreach (var setting in profile.AiSettings)
        {
            WriteUx6M(writer, setting.Id);
            writer.Write((int)setting.Kind);
            WriteUx6M(writer, setting.DefaultValue);
            writer.Write(setting.AllowedValues.Count);
            foreach (var value in setting.AllowedValues) WriteUx6M(writer, value);
            WriteUx6M(writer, setting.Preview);
            writer.Write(setting.ServerAuthoritative);
        }
    }

    private static void WriteUx6M(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static Guid StableUx6M(Guid owner, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner:D}:{key}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new(guid);
    }

    private static StudioUx6MSmokeResult FailedSmokeUx6M() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false);
}

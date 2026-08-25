using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Gameplay;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6BSelectionCode = "studio.gameplay.selection_required";
    private const string Ux6BSourceCode = "studio.gameplay.source_changed";
    private const string Ux6BProfileCode = "studio.gameplay.foundation_invalid";
    private const string Ux6BProvenance = "studio:ux6-b:gameplay-foundation";

    private readonly SetGameplayControlProfileHandler _setGameplayUx6B = new(new SystemClock());
    private StudioUx6BDraft? _approvedGameplayUx6B;
    private ProjectRevision? _approvedGameplayRevisionUx6B;
    private string? _approvedGameplayFingerprintUx6B;

    internal IReadOnlyList<StudioUx6BProfileChoice> GameplayFoundationChoicesUx6B
    {
        get
        {
            var project = Project;
            if (project is null) return [];
            return project.Areas.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(area => new StudioUx6BProfileChoice(
                    $"{HumanUx6B(project.Module.DisplayName)} · contexte {HumanUx6B(area.DisplayName)}", area.Id))
                .ToArray();
        }
    }

    internal StudioUx6BDraft? CreateGameplayFoundationDraftUx6B(
        AreaId? contextAreaId,
        bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null || contextAreaId is null ||
            project.Areas.All(item => item.Id != contextAreaId)) return null;

        var source = project.Module.RuntimePolicies.Gameplay;
        var foundation = source.Foundation;
        var movement = foundation.GroundedMovement;
        var interaction = foundation.Interaction;
        var cameras = foundation.Cameras;
        var input = foundation.Input;
        return new StudioUx6BDraft(
            history.State.Revision,
            SourceFingerprintUx6B(project),
            contextAreaId,
            source,
            new StudioUx6BMovementDraft(movement.Enabled, movement.Required, movement.WalkSpeed,
                movement.RunSpeed, movement.Acceleration, movement.Deceleration,
                movement.MaximumSlopeDegrees, movement.StepHeight, movement.JumpVelocity,
                movement.CrouchSpeedMultiplier, movement.RootMotion, movement.NetworkPrediction),
            new StudioUx6BInteractionDraft(interaction.Enabled, interaction.Required,
                interaction.InputAction, interaction.Targeting, interaction.FocusDistance,
                interaction.ViewAngleDegrees, interaction.ShowPrompt,
                interaction.Verbs.Select(item => new StudioUx6BVerbDraft(item.Id, item.Key,
                    item.Label, item.Command, item.HoldSeconds, item.TargetTags.ToArray(),
                    item.ServerAuthoritative)).ToArray()),
            new StudioUx6BCameraCatalogDraft(cameras.Required, cameras.DefaultProfileKey,
                cameras.Profiles.Select(item => new StudioUx6BCameraDraft(item.Id, item.Key,
                    item.DisplayName, item.Mode, item.FieldOfViewDegrees, item.Distance,
                    item.MinimumDistance, item.MaximumDistance, item.PitchDegrees,
                    item.FocusOffset, item.CollisionAvoidance, item.AlignCharacterFacing,
                    item.TerrainClearance, item.TerrainProbeSamples, item.InitialYawDegrees,
                    item.MinimumPitchDegrees, item.MaximumPitchDegrees)).ToArray()),
            new StudioUx6BInputDraft(input.Required, input.AllowRebinding, input.PerPlayerLocal,
                input.Contexts.ToArray(),
                input.Actions.Select(item => new StudioUx6BActionDraft(item.Id, item.Key,
                    item.Label, item.Context, item.AccessibilityRequired)).ToArray(),
                input.Bindings.Select(item => new StudioUx6BBindingDraft(item.Id, item.ActionKey,
                    item.Device, item.Binding, item.Glyph, item.Primary)).ToArray()));
    }

    internal StudioUx6BPreview PreviewGameplayFoundationUx6B(StudioUx6BDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUx6BApproval();
        if (!TryGetCurrentUx6B(draft, out var history, out var project, out var failure)) return failure!;
        if (!TryBuildGameplayCandidateUx6B(draft, out var candidate, out var diagnostic))
            return FailedPreviewUx6B(diagnostic?.Message ?? "Le profil gameplay est invalide.", diagnostic!);

        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history!.State.Revision, candidate!, DryRun: true);
        var dryRun = _setGameplayUx6B.Handle(history.State, command);
        if (!dryRun.Succeeded) return FailedCommandUx6B(dryRun);

        var sourceFoundation = project!.Module.RuntimePolicies.Gameplay.Foundation;
        var foundation = candidate!.Foundation;
        var diff = BuildDiffUx6B(sourceFoundation, foundation);
        var runtime = BuildRuntimePreviewUx6B(foundation);
        if (diff.Count > 0)
        {
            _approvedGameplayUx6B = draft;
            _approvedGameplayRevisionUx6B = history.State.Revision;
            _approvedGameplayFingerprintUx6B = CandidateFingerprintUx6B(project, draft);
        }
        return new(true, diff.Count > 0,
            diff.Count > 0
                ? "Aperçu sec du gameplay validé · aucune mutation appliquée."
                : "Aperçu sec validé · le profil est déjà identique.",
            "Remplacement canonique de la fondation gameplay; sauvegarde et profils voisins strictement conservés.",
            diff, [], runtime, true, true);
    }

    internal StudioUx6BActionResult ApplyApprovedGameplayFoundationUx6B()
    {
        var draft = _approvedGameplayUx6B;
        var history = History;
        var project = Project;
        if (draft is null || history is null || project is null ||
            _approvedGameplayRevisionUx6B != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6B(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedGameplayFingerprintUx6B, CandidateFingerprintUx6B(project, draft),
                StringComparison.Ordinal) ||
            !TryBuildGameplayCandidateUx6B(draft, out var candidate, out _))
        {
            CancelUx6BApproval();
            return new(false, "Relancez l’aperçu sec après avoir rechargé le profil gameplay.");
        }

        var command = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, candidate!, DryRun: true);
        var dryRun = _setGameplayUx6B.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelUx6BApproval();
            return new(false, HumanCommandFailureUx6B(dryRun));
        }
        var applied = _setGameplayUx6B.Handle(history.State, command with { DryRun = false });
        CancelUx6BApproval();
        if (!applied.Succeeded) return new(false, HumanCommandFailureUx6B(applied));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Fondation gameplay appliquée · déplacement, interactions, caméras et entrées sont enregistrés.");
    }

    internal StudioUx6BApprovalSnapshot CaptureGameplayFoundationApprovalUx6B() =>
        new(_approvedGameplayUx6B, _approvedGameplayRevisionUx6B, _approvedGameplayFingerprintUx6B);

    internal void RestoreGameplayFoundationApprovalUx6B(StudioUx6BApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedGameplayUx6B = snapshot.Draft;
        _approvedGameplayRevisionUx6B = snapshot.Revision;
        _approvedGameplayFingerprintUx6B = snapshot.Fingerprint;
    }

    internal void CancelUx6BApproval()
    {
        _approvedGameplayUx6B = null;
        _approvedGameplayRevisionUx6B = null;
        _approvedGameplayFingerprintUx6B = null;
    }

    internal string GameplayFoundationDraftFingerprintUx6B(StudioUx6BDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return Project is null ? string.Empty : CandidateFingerprintUx6B(Project, draft);
    }

    private bool TryGetCurrentUx6B(StudioUx6BDraft draft, out ProjectCommandHistory? history,
        out ProjectDefinition? project, out StudioUx6BPreview? failure)
    {
        history = History;
        project = Project;
        failure = null;
        if (history is null || project is null ||
            project.Areas.All(item => item.Id != draft.ContextAreaId))
        {
            failure = FailedPreviewUx6B("Ouvrez un module et choisissez explicitement un contexte de zone.",
                new(Ux6BSelectionCode, "module/runtimePolicies/gameplay/foundation",
                    "Sélection explicite requise."));
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6B(project), StringComparison.Ordinal))
        {
            failure = FailedPreviewUx6B("Le projet a changé. Rechargez le profil avant l’aperçu.",
                new(Ux6BSourceCode, "module/runtimePolicies/gameplay/foundation",
                    "La révision ou la source ne correspond plus."));
            return false;
        }
        return true;
    }

    private static bool TryBuildGameplayCandidateUx6B(StudioUx6BDraft draft,
        out ModuleGameplayControlProfile? candidate, out StudioUx6BDiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        try
        {
            var source = draft.SourceGameplay;
            var sourceFoundation = source.Foundation;
            EnsureFiniteUx6B(draft);
            var movement = new GroundedMovementProfile(sourceFoundation.GroundedMovement.Id,
                sourceFoundation.GroundedMovement.SchemaVersion, Ux6BProvenance,
                draft.Movement.Enabled, draft.Movement.Required, draft.Movement.WalkSpeed,
                draft.Movement.RunSpeed, draft.Movement.Acceleration, draft.Movement.Deceleration,
                draft.Movement.MaximumSlopeDegrees, draft.Movement.StepHeight,
                draft.Movement.JumpVelocity, draft.Movement.CrouchSpeedMultiplier,
                draft.Movement.RootMotion, draft.Movement.NetworkPrediction);
            var interaction = new InteractionProfile(sourceFoundation.Interaction.Id,
                sourceFoundation.Interaction.SchemaVersion, Ux6BProvenance,
                draft.Interaction.Enabled, draft.Interaction.Required,
                draft.Interaction.InputAction.Trim(), draft.Interaction.Targeting,
                draft.Interaction.FocusDistance, draft.Interaction.ViewAngleDegrees,
                draft.Interaction.ShowPrompt,
                draft.Interaction.Verbs.Select(item => new InteractionVerbDefinition(item.Id,
                    item.Key.Trim(), item.Label.Trim(), item.Command.Trim(), item.HoldSeconds,
                    item.TargetTags.Select(value => value.Trim()).Where(value => value.Length > 0)
                        .Distinct(StringComparer.Ordinal).ToArray(), item.ServerAuthoritative)).ToArray());
            var cameras = new GameplayCameraCatalog(sourceFoundation.Cameras.Id,
                sourceFoundation.Cameras.SchemaVersion, Ux6BProvenance, draft.Cameras.Required,
                draft.Cameras.DefaultProfileKey.Trim(),
                draft.Cameras.Profiles.Select(item => new GameplayCameraProfile(item.Id,
                    item.Key.Trim(), item.DisplayName.Trim(), item.Mode, item.FieldOfViewDegrees,
                    item.Distance, item.MinimumDistance, item.MaximumDistance, item.PitchDegrees,
                    item.FocusOffset, item.CollisionAvoidance, item.AlignCharacterFacing,
                    item.TerrainClearance, item.TerrainProbeSamples, item.InitialYawDegrees,
                    item.MinimumPitchDegrees, item.MaximumPitchDegrees)).ToArray());
            var input = new RuntimeInputProfile(sourceFoundation.Input.Id,
                sourceFoundation.Input.SchemaVersion, Ux6BProvenance, draft.Input.Required,
                draft.Input.AllowRebinding, draft.Input.PerPlayerLocal,
                draft.Input.Contexts.Select(value => value.Trim()).Where(value => value.Length > 0)
                    .Distinct(StringComparer.Ordinal).ToArray(),
                draft.Input.Actions.Select(item => new RuntimeInputActionDefinition(item.Id,
                    item.Key.Trim(), item.Label.Trim(), item.Context.Trim(),
                    item.AccessibilityRequired)).ToArray(),
                draft.Input.Bindings.Select(item => new RuntimeInputBindingDefinition(item.Id,
                    item.ActionKey.Trim(), item.Device, item.Binding.Trim(), item.Glyph.Trim(),
                    item.Primary)).ToArray());
            var foundation = new GameplayFoundationProfile(sourceFoundation.Id,
                sourceFoundation.SchemaVersion, Ux6BProvenance, movement, interaction, cameras,
                input, sourceFoundation.Save);
            var invalid = GameplayFoundationValidator.Validate(foundation).FirstOrDefault();
            if (invalid is not null)
            {
                diagnostic = new(invalid.Code, invalid.Path, HumanDiagnosticUx6B(invalid.Code, invalid.Message));
                return false;
            }
            candidate = new ModuleGameplayControlProfile(source.Id, source.SchemaVersion,
                source.Swimming, source.Climbing, source.RadialMenu, source.TimeControl,
                source.CombatModes, Ux6BProvenance, foundation, source.Simulation, source.Session);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or
                                              InvalidOperationException)
        {
            diagnostic = new(Ux6BProfileCode, "module/runtimePolicies/gameplay/foundation",
                $"Le contrat canonique refuse le profil : {exception.Message}");
            return false;
        }
    }

    private static void EnsureFiniteUx6B(StudioUx6BDraft draft)
    {
        var values = new List<float>
        {
            draft.Movement.WalkSpeed, draft.Movement.RunSpeed, draft.Movement.Acceleration,
            draft.Movement.Deceleration, draft.Movement.MaximumSlopeDegrees,
            draft.Movement.StepHeight, draft.Movement.JumpVelocity,
            draft.Movement.CrouchSpeedMultiplier, draft.Interaction.FocusDistance,
            draft.Interaction.ViewAngleDegrees,
        };
        values.AddRange(draft.Interaction.Verbs.Select(item => item.HoldSeconds));
        values.AddRange(draft.Cameras.Profiles.SelectMany(item => new[]
        {
            item.FieldOfViewDegrees, item.Distance, item.MinimumDistance,
            item.MaximumDistance, item.PitchDegrees, item.FocusOffset.X,
            item.FocusOffset.Y, item.FocusOffset.Z, item.TerrainClearance, item.InitialYawDegrees,
            item.MinimumPitchDegrees, item.MaximumPitchDegrees,
        }));
        if (values.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Les valeurs numériques doivent être finies.");
    }

    private static IReadOnlyList<string> BuildDiffUx6B(
        GameplayFoundationProfile source,
        GameplayFoundationProfile candidate)
    {
        var entries = new List<string>();
        if (source.GroundedMovement != candidate.GroundedMovement)
            entries.Add($"Déplacement · marche {source.GroundedMovement.WalkSpeed:0.###} → {candidate.GroundedMovement.WalkSpeed:0.###} m/s · course {source.GroundedMovement.RunSpeed:0.###} → {candidate.GroundedMovement.RunSpeed:0.###} m/s.");
        if (!source.Interaction.Verbs.SequenceEqual(candidate.Interaction.Verbs) ||
            source.Interaction with { Verbs = [] } != candidate.Interaction with { Verbs = [] })
            entries.Add($"Interactions · {candidate.Interaction.Verbs.Count} verbe(s) · ciblage {HumanUx6B(candidate.Interaction.Targeting.ToString())} · portée {candidate.Interaction.FocusDistance:0.###} m.");
        if (!source.Cameras.Profiles.SequenceEqual(candidate.Cameras.Profiles) ||
            source.Cameras with { Profiles = [] } != candidate.Cameras with { Profiles = [] })
            entries.Add($"Caméras · {candidate.Cameras.Profiles.Count} profil(s) · défaut {HumanUx6B(candidate.Cameras.DefaultProfileKey)}.");
        if (!source.Input.Contexts.SequenceEqual(candidate.Input.Contexts) ||
            !source.Input.Actions.SequenceEqual(candidate.Input.Actions) ||
            !source.Input.Bindings.SequenceEqual(candidate.Input.Bindings) ||
            source.Input with { Contexts = [], Actions = [], Bindings = [] } !=
            candidate.Input with { Contexts = [], Actions = [], Bindings = [] })
            entries.Add($"Entrées · {candidate.Input.Contexts.Count} contexte(s) · {candidate.Input.Actions.Count} action(s) · {candidate.Input.Bindings.Count} liaison(s).");
        return entries;
    }

    private static IReadOnlyList<string> BuildRuntimePreviewUx6B(GameplayFoundationProfile foundation)
    {
        var movement = GameplayFoundationRuntime.Step(foundation.GroundedMovement,
            new GroundedMovementState(Vector3Value.Zero, true, false),
            new GroundedMovementInput(new(1, 0, 0), true, true, false), .1f);
        var verb = foundation.Interaction.Verbs.FirstOrDefault();
        InteractionFocus? focus = null;
        if (verb is not null && verb.TargetTags.Count > 0)
        {
            var candidate = new InteractionCandidate(Guid.Parse("f6400000-0000-4000-8000-000000000001"),
                new(0, 0, -Math.Min(2, foundation.Interaction.FocusDistance / 2)),
                new HashSet<string>([verb.TargetTags[0]], StringComparer.Ordinal),
                new HashSet<string>([verb.Key], StringComparer.Ordinal));
            focus = GameplayFoundationRuntime.ResolveFocus(foundation.Interaction, Vector3Value.Zero,
                new(0, 0, -1), [candidate], verb.Key, verb.HoldSeconds);
        }
        var camera = GameplayFoundationRuntime.ResolveCamera(foundation.Cameras);
        var action = foundation.Input.Actions.FirstOrDefault(item =>
            foundation.Input.Bindings.Any(binding => binding.ActionKey == item.Key));
        var binding = action is null ? null : GameplayFoundationRuntime.ResolveInput(foundation.Input,
            action.Key, foundation.Input.Bindings.First(item => item.ActionKey == action.Key).Device,
            new HashSet<string>([action.Context], StringComparer.Ordinal));
        return
        [
            $"Déplacement déterministe · vitesse demandée {movement.AppliedSpeed:0.###} m/s · saut {(movement.Jumped ? "accepté" : "non déclenché")} · prédiction {(movement.Predicted ? "configurée" : "désactivée")}.",
            focus is null
                ? "Interaction déterministe · aucune cible éligible avec les réglages actuels."
                : $"Interaction déterministe · {HumanUx6B(focus.Verb.Label)} à {focus.Distance:0.###} m · maintien {focus.HoldProgress:P0} · {(focus.Ready ? "prête" : "en attente")}.",
            $"Caméra résolue · {HumanUx6B(camera.DisplayName)} · champ {camera.FieldOfViewDegrees:0.###}° · distance {camera.Distance:0.###} m · verticale {camera.MinimumPitchDegrees:0.###}° à {camera.MaximumPitchDegrees:0.###}°.",
            binding is null
                ? "Entrées déterministes · aucune liaison résolue dans le premier contexte."
                : $"Entrée résolue · {HumanUx6B(binding.Action.Label)} · {HumanUx6B(binding.Binding.Binding)} · glyphe {HumanUx6B(binding.Glyph)}.",
            "Aucun acteur, Camera3D, InputMap, paquet natif, réseau ou prédiction live n’est exécuté dans Studio.",
        ];
    }

    private static StudioUx6BPreview FailedPreviewUx6B(string message, StudioUx6BDiagnostic diagnostic) =>
        new(false, false, message, string.Empty, [], [diagnostic], [], false, true);

    private static StudioUx6BPreview FailedCommandUx6B(CommandResult result)
    {
        var diagnostics = result.Diagnostics.Select(item => new StudioUx6BDiagnostic(item.Code,
            "module/runtimePolicies/gameplay/foundation", HumanDiagnosticUx6B(item.Code, item.Message))).ToArray();
        return new(false, false,
            diagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé le profil.",
            string.Empty, [], diagnostics, [], false, true);
    }

    private static string HumanCommandFailureUx6B(CommandResult result) =>
        HumanDiagnosticUx6B(result.Diagnostics.FirstOrDefault()?.Code,
            result.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.");

    private static string HumanDiagnosticUx6B(string? code, string message) => code switch
    {
        "command.revision_conflict" => "Le projet a changé; rechargez le profil.",
        GameplayFoundationValidator.MovementCode => "Le déplacement exige des vitesses, pentes et accélérations dans les bornes du runtime.",
        GameplayFoundationValidator.InteractionCode => "Les interactions exigent une portée bornée et des verbes nommés avec des cibles.",
        GameplayFoundationValidator.InteractionReferenceCode => "L’action d’entrée choisie pour interagir n’existe pas.",
        GameplayFoundationValidator.CameraCode => "Chaque caméra exige un mode, un nom et des valeurs optiques cohérentes.",
        GameplayFoundationValidator.CameraDefaultCode => "La caméra par défaut doit désigner un profil existant.",
        GameplayFoundationValidator.InputCode => "Les contextes, actions et liaisons d’entrée doivent être nommés et uniques.",
        GameplayFoundationValidator.InputReferenceCode => "Chaque action accessible doit posséder une liaison dans un contexte existant.",
        _ => string.IsNullOrWhiteSpace(message) ? "La validation canonique a refusé le profil." : message,
    };

    private static ProjectDefinition BuildGameplayProjectUx6B(
        ProjectDefinition source,
        ModuleGameplayControlProfile candidate)
    {
        var previous = source.Module.RuntimePolicies;
        var policies = new Forge.Domain.RuntimePolicies.ModuleRuntimePolicyProfile(previous.Id,
            previous.SchemaVersion, previous.Heartbeats, previous.Rest, previous.SequenceAbort,
            "gameplay-command", previous.Cinematics, candidate, previous.Rules,
            previous.Extensions, previous.GameplayEvents, previous.World, previous.LifecycleEvents);
        return new ProjectDefinition(source.Id, source.Title, source.Module.WithRuntimePolicies(policies),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
    }

    private static string CandidateFingerprintUx6B(ProjectDefinition project, StudioUx6BDraft draft)
    {
        if (!TryBuildGameplayCandidateUx6B(draft, out var candidate, out _) || candidate is null)
            return string.Empty;
        return HashUx6B(ProjectJsonSerializer.Serialize(BuildGameplayProjectUx6B(project, candidate)));
    }

    private static string SourceFingerprintUx6B(ProjectDefinition project) =>
        HashUx6B(ProjectJsonSerializer.Serialize(project));

    private static string HashUx6B(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string HumanUx6B(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "aucun";
        if (Guid.TryParse(value, out _)) return "identifiant protégé";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    internal StudioUx6BSmokeResult RunUx6BContractSmoke()
    {
        var real = Project;
        if (real is null || real.Areas.Count == 0) return FailedUx6BSmoke();
        var realFoundation = real.Module.RuntimePolicies.Gameplay.Foundation;
        var realData = realFoundation.Cameras.Profiles.Count == 8 &&
                       realFoundation.Input.Actions.Count > 0 && realFoundation.Interaction.Verbs.Count > 0;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-b-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(root, real);
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedUx6BSmoke();
            var areaId = editing.Project!.Areas[0].Id;
            var explicitSelection = editing.CreateGameplayFoundationDraftUx6B(areaId) is null &&
                                    editing.CreateGameplayFoundationDraftUx6B(areaId, true) is not null;
            var baseline = editing.CreateGameplayFoundationDraftUx6B(areaId, true)!;
            var movementValidation = !editing.PreviewGameplayFoundationUx6B(baseline with
            {
                Movement = baseline.Movement with { RunSpeed = baseline.Movement.WalkSpeed - .1f },
            }).Succeeded;
            var interactionValidation = !editing.PreviewGameplayFoundationUx6B(baseline with
            {
                Interaction = baseline.Interaction with { InputAction = "action absente" },
            }).Succeeded;
            var missingCameraValidation = !editing.PreviewGameplayFoundationUx6B(baseline with
            {
                Cameras = baseline.Cameras with { DefaultProfileKey = "caméra absente" },
            }).Succeeded;
            var invalidPitchProfiles = baseline.Cameras.Profiles.Select((item, index) => index == 0
                ? item with { MinimumPitchDegrees = 20, MaximumPitchDegrees = -20 }
                : item).ToArray();
            var cameraValidation = missingCameraValidation &&
                !editing.PreviewGameplayFoundationUx6B(baseline with
                {
                    Cameras = baseline.Cameras with { Profiles = invalidPitchProfiles },
                }).Succeeded;
            var firstAccessible = baseline.Input.Actions.First(item => item.AccessibilityRequired);
            var inputValidation = !editing.PreviewGameplayFoundationUx6B(baseline with
            {
                Input = baseline.Input with
                {
                    Bindings = baseline.Input.Bindings.Where(item => item.ActionKey != firstAccessible.Key).ToArray(),
                },
            }).Succeeded;

            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var changed = ChangedDraftUx6B(baseline);
            var preview = editing.PreviewGameplayFoundationUx6B(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;
            var runtimePreview = preview.RuntimePreview.Count == 5;

            var externalDraft = ChangedDraftUx6B(baseline) with
            {
                Movement = changed.Movement with { WalkSpeed = changed.Movement.WalkSpeed + .1f },
            };
            if (!TryBuildGameplayCandidateUx6B(externalDraft, out var externalCandidate, out _) ||
                externalCandidate is null) return FailedUx6BSmoke();
            var external = editing._setGameplayUx6B.Handle(history.State,
                new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    externalCandidate, DryRun: false));
            if (!external.Succeeded) return FailedUx6BSmoke();
            history.Record(external);
            var staleSource = editing.ApplyApprovedGameplayFoundationUx6B();
            var staleSourceBlocked = !staleSource.Succeeded && editing.Undo().Succeeded;

            var tamperDraft = ChangedDraftUx6B(editing.CreateGameplayFoundationDraftUx6B(areaId, true)!);
            var tamperPreview = editing.PreviewGameplayFoundationUx6B(tamperDraft);
            var approval = editing.CaptureGameplayFoundationApprovalUx6B();
            editing.RestoreGameplayFoundationApprovalUx6B(approval with
            {
                Draft = approval.Draft! with
                {
                    Movement = approval.Draft.Movement with
                    {
                        RunSpeed = approval.Draft.Movement.RunSpeed + .2f,
                    },
                },
            });
            var staleApproval = editing.ApplyApprovedGameplayFoundationUx6B();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var sourceGameplay = sourceProject.Module.RuntimePolicies.Gameplay;
            var fresh = ChangedDraftUx6B(editing.CreateGameplayFoundationDraftUx6B(areaId, true)!);
            if (!TryBuildGameplayCandidateUx6B(fresh, out var expectedGameplay, out _) ||
                expectedGameplay is null) return FailedUx6BSmoke();
            var freshPreview = editing.PreviewGameplayFoundationUx6B(fresh);
            var applied = editing.ApplyApprovedGameplayFoundationUx6B();
            var finalProject = editing.Project!;
            var finalGameplay = finalProject.Module.RuntimePolicies.Gameplay;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.Last().CommandName == "set_gameplay_control_profile";
            var savePolicy = ReferenceEquals(sourceGameplay.Foundation.Save, finalGameplay.Foundation.Save);
            var gameplayTails = ReferenceEquals(sourceGameplay.Swimming, finalGameplay.Swimming) &&
                                ReferenceEquals(sourceGameplay.Climbing, finalGameplay.Climbing) &&
                                ReferenceEquals(sourceGameplay.RadialMenu, finalGameplay.RadialMenu) &&
                                ReferenceEquals(sourceGameplay.TimeControl, finalGameplay.TimeControl) &&
                                ReferenceEquals(sourceGameplay.CombatModes, finalGameplay.CombatModes) &&
                                ReferenceEquals(sourceGameplay.Simulation, finalGameplay.Simulation) &&
                                ReferenceEquals(sourceGameplay.Session, finalGameplay.Session);
            var expectedProject = BuildGameplayProjectUx6B(sourceProject, expectedGameplay);
            var projectNeighbors = string.Equals(ProjectJsonSerializer.Serialize(expectedProject),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal);

            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceProject, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var undoRedo = undone && redone;
            var saved = editing.Save();
            var opened = reopened.Open(root);
            var persistence = saved.Succeeded && opened.Succeeded && reopened.Project is not null &&
                              string.Equals(finalJson, ProjectJsonSerializer.Serialize(reopened.Project),
                                  StringComparison.Ordinal);
            var humanText = !ContainsUuidUx6B(string.Join(' ',
                editing.GameplayFoundationChoicesUx6B.Select(item => item.Label)) + " " +
                string.Join(' ', preview.Diff) + " " + string.Join(' ', preview.RuntimePreview));
            var bounded = preview.RuntimeClaimsBounded && preview.RuntimePreview.Any(item =>
                item.Contains("Aucun acteur", StringComparison.Ordinal));

            editing.Close();
            reopened.Close();
            cleanup = DeleteUx6BTemporaryWorkspace(root);
            var succeeded = explicitSelection && realData && movementValidation && interactionValidation &&
                            cameraValidation && inputValidation && canonicalDryRun && runtimePreview &&
                            staleSourceBlocked && staleApprovalBlocked && canonicalCommand && undoRedo &&
                            persistence && savePolicy && gameplayTails && projectNeighbors && humanText &&
                            bounded && cleanup;
            return new(succeeded, explicitSelection, realData, movementValidation, interactionValidation,
                cameraValidation, inputValidation, canonicalDryRun, runtimePreview, staleSourceBlocked,
                staleApprovalBlocked, canonicalCommand, undoRedo, persistence, savePolicy,
                gameplayTails, projectNeighbors, humanText, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_B_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx6BSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteUx6BTemporaryWorkspace(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_B_CLEANUP_FAILED");
        }
    }

    internal static StudioUx6BDraft ChangedDraftUx6B(StudioUx6BDraft source)
    {
        var verbs = source.Interaction.Verbs.Select((item, index) => index == 0
            ? item with { HoldSeconds = item.HoldSeconds >= 29.9f ? item.HoldSeconds - .1f : item.HoldSeconds + .1f }
            : item).ToArray();
        var cameras = source.Cameras.Profiles.Select((item, index) => index == 0
            ? item with { FieldOfViewDegrees = item.FieldOfViewDegrees >= 139 ? item.FieldOfViewDegrees - 1 : item.FieldOfViewDegrees + 1 }
            : item).ToArray();
        var bindings = source.Input.Bindings.Select((item, index) => index == 0
            ? item with
            {
                Binding = item.Binding.EndsWith(" + Studio", StringComparison.Ordinal)
                    ? item.Binding.Replace(" + Studio", string.Empty, StringComparison.Ordinal)
                    : item.Binding + " + Studio",
                Glyph = item.Glyph.EndsWith("-studio", StringComparison.Ordinal)
                    ? item.Glyph.Replace("-studio", string.Empty, StringComparison.Ordinal)
                    : item.Glyph + "-studio",
            }
            : item).ToArray();
        return source with
        {
            Movement = source.Movement with
            {
                RunSpeed = source.Movement.RunSpeed >= 99 ? source.Movement.RunSpeed - .25f :
                    source.Movement.RunSpeed + .25f,
            },
            Interaction = source.Interaction with { Verbs = verbs },
            Cameras = source.Cameras with { Profiles = cameras },
            Input = source.Input with { Bindings = bindings },
        };
    }

    private static bool ContainsUuidUx6B(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    private static StudioUx6BSmokeResult FailedUx6BSmoke() => new(false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false);

    internal static bool DeleteUx6BTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return true;
        try
        {
            var full = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux6-b-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private void CloseUx6B() => CancelUx6BApproval();
}

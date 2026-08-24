using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6SProvenance = "studio:ux6-s:creator-technical";
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx6S = new(new SystemClock());
    private StudioUx6SDraft? _approvedCreatorPolicyUx6S;
    private ProjectRevision? _approvedCreatorPolicyRevisionUx6S;
    private string? _approvedCreatorPolicyFingerprintUx6S;

    internal IReadOnlyList<StudioUx6SProfileChoice> CreatorPolicyChoicesUx6S()
    {
        var project = Project;
        if (project is null) return [];
        var creator = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience;
        return [new($"{HumanUx6S(project.Module.DisplayName)} · " +
                    $"{creator.DeviceQueries.AllowedQueries.Count} requête(s) · " +
                    $"{creator.ResourceCommands.AllowedNamespaces.Count} espace(s) · " +
                    $"groupe {creator.PartyControl.MaximumControlledMembers} · " +
                    $"progression {creator.ProgressionTracks.MaximumTracks}")];
    }

    internal StudioUx6SDraft? CreateCreatorPolicyDraftUx6S(bool explicitlySelected)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var creator = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience;
        return new(
            history.State.Revision,
            SourceFingerprintUx6S(project),
            creator.DeviceQueries.Enabled,
            creator.DeviceQueries.AllowedQueries.ToArray(),
            creator.DeviceQueries.RequireConsent,
            creator.DeviceQueries.RedactStableIdentifiers,
            creator.ResourceCommands.Enabled,
            creator.ResourceCommands.MaximumResults,
            creator.ResourceCommands.MaximumBytes,
            creator.ResourceCommands.AllowedNamespaces.ToArray(),
            creator.PartyControl.Enabled,
            creator.PartyControl.MaximumControlledMembers,
            creator.PartyControl.AllowDirectSwitch,
            creator.PartyControl.Commands.ToArray(),
            creator.ProgressionTracks.MaximumTracks,
            creator.ProgressionTracks.DataDriven,
            creator.ProgressionTracks.SupportLargeSetUi,
            creator.DeviceQueries.AllowedQueries.Take(1).ToArray(),
            Math.Min(32, creator.ResourceCommands.MaximumResults),
            Math.Min(8, creator.PartyControl.MaximumControlledMembers),
            Math.Min(8, creator.ProgressionTracks.MaximumTracks));
    }

    internal StudioUx6SPreview PreviewCreatorPolicyUx6S(StudioUx6SDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelCreatorPolicyApprovalUx6S();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedPreviewUx6S("Ouvrez un module et sélectionnez son profil Creator.",
                "studio.creator.selection_required", "module/runtimePolicies/extensions/postLaunch/creatorExperience");
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6S(project), StringComparison.Ordinal))
            return FailedPreviewUx6S("Le projet a changé · rechargez la politique Creator.",
                "studio.creator.source_changed", "module/runtimePolicies/extensions/postLaunch/creatorExperience");
        if (!TryBuildCreatorPolicyCandidateUx6S(project, draft, out var candidate, out var diagnostic))
            return FailedPreviewUx6S(diagnostic!.Message, diagnostic.Code, diagnostic.Path);

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6S.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return FailedPreviewUx6S(HumanCommandFailureUx6S(dryRun),
                dryRun.Diagnostics.FirstOrDefault()?.Code ?? "studio.creator.command_refused",
                "module/runtimePolicies/extensions/postLaunch/creatorExperience");

        var creator = candidate!.PostLaunch.CreatorExperience;
        var technical = PostLaunchCapabilityResolver.ResolveCreatorTechnical(creator,
            draft.RequestedDeviceQueries, draft.RequestedResourceResults, requestedAreaInstances: 0);
        var distribution = PostLaunchCapabilityResolver.ResolveRpgDistribution(creator,
            draft.RequestedPartyMembers, draft.RequestedProgressionTracks);
        var expectedQueries = creator.DeviceQueries.Enabled
            ? draft.RequestedDeviceQueries.Select(NormalizeUx6S)
                .Intersect(creator.DeviceQueries.AllowedQueries, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray()
            : [];
        var deviceResolved = technical.DeviceQueries.SequenceEqual(expectedQueries) &&
                             (!creator.DeviceQueries.Enabled ||
                              creator.DeviceQueries.RequireConsent && creator.DeviceQueries.RedactStableIdentifiers);
        var resourceResolved = technical.Accepted ==
                               (creator.ResourceCommands.Enabled && draft.RequestedResourceResults >= 0 &&
                                draft.RequestedResourceResults <= creator.ResourceCommands.MaximumResults);
        var partyAccepted = draft.RequestedPartyMembers <= creator.PartyControl.MaximumControlledMembers &&
                            draft.RequestedProgressionTracks <= creator.ProgressionTracks.MaximumTracks;
        var partyResolved = distribution.PartyControl == (partyAccepted && creator.PartyControl.Enabled) &&
                            distribution.MaximumParty == creator.PartyControl.MaximumControlledMembers;
        var progressionResolved = distribution.ProgressionTracks == creator.ProgressionTracks.MaximumTracks &&
                                  creator.ProgressionTracks.DataDriven &&
                                  distribution.Diagnostic == (partyAccepted
                                      ? "POST_RPG_DISTRIBUTION_READY"
                                      : "POST_RPG_DISTRIBUTION_LIMIT");
        var runtime = BuildRuntimePreviewUx6S(creator, draft, technical, distribution);
        var sourceCreator = project.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience;
        var diff = BuildDiffUx6S(sourceCreator, creator);
        var changed = diff.Count > 0;
        if (changed)
        {
            _approvedCreatorPolicyUx6S = draft;
            _approvedCreatorPolicyRevisionUx6S = history.State.Revision;
            _approvedCreatorPolicyFingerprintUx6S = CandidateFingerprintUx6S(project, draft);
        }

        return new(true, changed, true,
            changed
                ? "Aperçu sec canonique validé · aucune politique runtime n’a encore été appliquée."
                : "Aperçu sec validé · la politique Creator est déjà identique.",
            diff, [], runtime, deviceResolved, resourceResolved, partyResolved, progressionResolved, true);
    }

    internal StudioUx6SActionResult ApplyApprovedCreatorPolicyUx6S()
    {
        var draft = _approvedCreatorPolicyUx6S;
        var project = Project;
        var history = History;
        if (draft is null || project is null || history is null ||
            _approvedCreatorPolicyRevisionUx6S != history.State.Revision ||
            draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint, SourceFingerprintUx6S(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedCreatorPolicyFingerprintUx6S,
                CandidateFingerprintUx6S(project, draft), StringComparison.Ordinal) ||
            !TryBuildCreatorPolicyCandidateUx6S(project, draft, out var candidate, out _))
        {
            CancelCreatorPolicyApprovalUx6S();
            return new(false, "Le projet ou le brouillon a changé · relancez l’aperçu sec.");
        }

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            candidate!, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx6S.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelCreatorPolicyApprovalUx6S();
            return new(false, HumanCommandFailureUx6S(dryRun));
        }
        var applied = _setRuntimeExtensionsUx6S.Handle(history.State, command with { DryRun = false });
        CancelCreatorPolicyApprovalUx6S();
        if (!applied.Succeeded) return new(false, HumanCommandFailureUx6S(applied));
        history.Record(applied);
        InvalidateContentIndexes();
        return new(true, "Politique Creator appliquée par une commande canonique annulable.");
    }

    internal StudioUx6SActionResult UndoCreatorPolicyUx6S()
    {
        CancelCreatorPolicyApprovalUx6S();
        var result = Undo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6SActionResult RedoCreatorPolicyUx6S()
    {
        CancelCreatorPolicyApprovalUx6S();
        var result = Redo();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6SActionResult SaveCreatorPolicyUx6S()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx6SApprovalSnapshot CaptureCreatorPolicyApprovalUx6S() =>
        new(_approvedCreatorPolicyUx6S, _approvedCreatorPolicyRevisionUx6S,
            _approvedCreatorPolicyFingerprintUx6S);

    internal void RestoreCreatorPolicyApprovalUx6S(StudioUx6SApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedCreatorPolicyUx6S = snapshot.Draft;
        _approvedCreatorPolicyRevisionUx6S = snapshot.Revision;
        _approvedCreatorPolicyFingerprintUx6S = snapshot.Fingerprint;
    }

    internal void CancelCreatorPolicyApprovalUx6S()
    {
        _approvedCreatorPolicyUx6S = null;
        _approvedCreatorPolicyRevisionUx6S = null;
        _approvedCreatorPolicyFingerprintUx6S = null;
    }

    internal void CloseUx6S() => CancelCreatorPolicyApprovalUx6S();

    internal static StudioUx6SDraft ChangedCreatorPolicyDraftUx6S(StudioUx6SDraft source)
    {
        var queries = source.AllowedDeviceQueries.Concat(["audio-output"])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var namespaces = source.AllowedResourceNamespaces.Concat(["studio"])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var commands = source.PartyCommands.Concat(["assist"])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var maximumResults = source.MaximumResourceResults >= 9_900
            ? source.MaximumResourceResults - 1
            : source.MaximumResourceResults + 1;
        var maximumBytes = source.MaximumResourceBytes >= 1_072_693_248
            ? source.MaximumResourceBytes - 1_048_576
            : source.MaximumResourceBytes + 1_048_576;
        var maximumParty = source.MaximumControlledMembers >= 64
            ? source.MaximumControlledMembers - 1
            : source.MaximumControlledMembers + 1;
        var maximumTracks = source.MaximumProgressionTracks >= 256
            ? source.MaximumProgressionTracks - 1
            : source.MaximumProgressionTracks + 1;
        return source with
        {
            DeviceQueriesEnabled = true,
            AllowedDeviceQueries = queries,
            RequireConsent = true,
            RedactStableIdentifiers = true,
            ResourceCommandsEnabled = true,
            MaximumResourceResults = maximumResults,
            MaximumResourceBytes = maximumBytes,
            AllowedResourceNamespaces = namespaces,
            PartyControlEnabled = true,
            MaximumControlledMembers = maximumParty,
            AllowDirectSwitch = !source.AllowDirectSwitch,
            PartyCommands = commands,
            MaximumProgressionTracks = maximumTracks,
            DataDrivenProgression = true,
            SupportLargeSetUi = true,
            RequestedDeviceQueries = [queries[0], "hardware-serial"],
            RequestedResourceResults = Math.Min(32, maximumResults),
            RequestedPartyMembers = Math.Min(8, maximumParty),
            RequestedProgressionTracks = Math.Min(8, maximumTracks),
        };
    }

    internal static ProjectDefinition CreateEvidenceProjectUx6S(ProjectDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }

    internal StudioUx6SSmokeResult RunUx6SContractSmoke()
    {
        var real = Project;
        if (real is null) return FailedSmokeUx6S();
        var realCreator = real.Module.RuntimePolicies.Extensions.PostLaunch.CreatorExperience;
        var realReadable = realCreator.Id != Guid.Empty && realCreator.DeviceQueries.Id != Guid.Empty &&
                           realCreator.ResourceCommands.Id != Guid.Empty && realCreator.PartyControl.Id != Guid.Empty &&
                           realCreator.ProgressionTracks.Id != Guid.Empty;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-s-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var cleanup = false;
        try
        {
            var created = ProjectSession.Create(root, CreateEvidenceProjectUx6S(real));
            if (!created.Succeeded || !editing.Open(root).Succeeded) return FailedSmokeUx6S();
            var explicitSelection = editing.CreateCreatorPolicyDraftUx6S(false) is null &&
                                    editing.CreateCreatorPolicyDraftUx6S(true) is not null;
            var baseline = editing.CreateCreatorPolicyDraftUx6S(true)!;
            var deviceValidation = !editing.PreviewCreatorPolicyUx6S(baseline with
            {
                DeviceQueriesEnabled = true,
                RequireConsent = false,
            }).Succeeded;
            var resourceValidation = !editing.PreviewCreatorPolicyUx6S(baseline with
            {
                MaximumResourceResults = 0,
            }).Succeeded;
            var partyValidation = !editing.PreviewCreatorPolicyUx6S(baseline with
            {
                MaximumControlledMembers = 0,
            }).Succeeded;
            var progressionValidation = !editing.PreviewCreatorPolicyUx6S(baseline with
            {
                MaximumProgressionTracks = 7,
                DataDrivenProgression = false,
            }).Succeeded;

            var history = editing.History!;
            var beforeProject = editing.Project;
            var beforeRevision = history.State.Revision;
            var beforeJournal = history.Journal.ToArray();
            var changed = ChangedCreatorPolicyDraftUx6S(baseline);
            var preview = editing.PreviewCreatorPolicyUx6S(changed);
            var canonicalDryRun = preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true } &&
                                  ReferenceEquals(beforeProject, editing.Project) &&
                                  history.State.Revision == beforeRevision &&
                                  history.Journal.SequenceEqual(beforeJournal) && !editing.IsDirty;

            var externalSource = editing.Project!;
            var externalExtensions = externalSource.Module.RuntimePolicies.Extensions;
            var externalCandidate = new ModuleRuntimeExtensionProfile(
                externalExtensions.Id, externalExtensions.SchemaVersion, externalExtensions.RuleSchemas,
                externalExtensions.UiDocuments, externalExtensions.JsonDocuments, externalExtensions.SnapshotPolicy,
                Ux6SProvenance + ":external", externalExtensions.ModernTooling,
                externalExtensions.OperationalReadiness, externalExtensions.PostLaunch);
            var external = editing._setRuntimeExtensionsUx6S.Handle(history.State,
                new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                    externalCandidate, DryRun: false));
            if (!external.Succeeded) return FailedSmokeUx6S();
            history.Record(external);
            var staleSource = editing.ApplyApprovedCreatorPolicyUx6S();
            var staleSourceBlocked = !staleSource.Succeeded && editing.Undo().Succeeded;

            var tamperDraft = ChangedCreatorPolicyDraftUx6S(editing.CreateCreatorPolicyDraftUx6S(true)!);
            var tamperPreview = editing.PreviewCreatorPolicyUx6S(tamperDraft);
            var approval = editing.CaptureCreatorPolicyApprovalUx6S();
            editing.RestoreCreatorPolicyApprovalUx6S(approval with
            {
                Draft = approval.Draft! with
                {
                    RequestedPartyMembers = approval.Draft.RequestedPartyMembers + 1,
                },
            });
            var staleApproval = editing.ApplyApprovedCreatorPolicyUx6S();
            var staleApprovalBlocked = tamperPreview.Succeeded && !staleApproval.Succeeded;

            var sourceProject = editing.Project!;
            var sourcePolicies = sourceProject.Module.RuntimePolicies;
            var sourceExtensions = sourcePolicies.Extensions;
            var sourcePost = sourceExtensions.PostLaunch;
            var sourceCreator = sourcePost.CreatorExperience;
            var fresh = ChangedCreatorPolicyDraftUx6S(editing.CreateCreatorPolicyDraftUx6S(true)!);
            if (!TryBuildCreatorPolicyCandidateUx6S(sourceProject, fresh, out var expectedExtensions, out _) ||
                expectedExtensions is null)
                return FailedSmokeUx6S();
            var freshPreview = editing.PreviewCreatorPolicyUx6S(fresh);
            var applied = editing.ApplyApprovedCreatorPolicyUx6S();
            var finalProject = editing.Project!;
            var finalPolicies = finalProject.Module.RuntimePolicies;
            var finalExtensions = finalPolicies.Extensions;
            var finalPost = finalExtensions.PostLaunch;
            var finalCreator = finalPost.CreatorExperience;
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded &&
                                   editing.History!.Journal.Last().CommandName == "set_runtime_extensions";
            var creatorNeighbors = sourceCreator.Water == finalCreator.Water &&
                                   sourceCreator.DynamicAreas == finalCreator.DynamicAreas &&
                                   sourceCreator.RepositoryMetadata == finalCreator.RepositoryMetadata &&
                                   sourceCreator.Observability == finalCreator.Observability &&
                                   sourceCreator.Graphics == finalCreator.Graphics &&
                                   sourceCreator.Id == finalCreator.Id;
            var postNeighbors = ReferenceEquals(sourcePost.Gameplay, finalPost.Gameplay) &&
                                ReferenceEquals(sourcePost.LiveDirector, finalPost.LiveDirector) &&
                                ReferenceEquals(sourcePost.Network, finalPost.Network) &&
                                ReferenceEquals(sourcePost.Production, finalPost.Production) &&
                                sourcePost.Id == finalPost.Id;
            var extensionNeighbors = sourceExtensions.RuleSchemas.SequenceEqual(finalExtensions.RuleSchemas) &&
                                     sourceExtensions.UiDocuments.SequenceEqual(finalExtensions.UiDocuments) &&
                                     sourceExtensions.JsonDocuments.SequenceEqual(finalExtensions.JsonDocuments) &&
                                     ReferenceEquals(sourceExtensions.SnapshotPolicy, finalExtensions.SnapshotPolicy) &&
                                     ReferenceEquals(sourceExtensions.ModernTooling, finalExtensions.ModernTooling) &&
                                     ReferenceEquals(sourceExtensions.OperationalReadiness,
                                         finalExtensions.OperationalReadiness) &&
                                     sourceExtensions.Id == finalExtensions.Id;
            var expectedProject = BuildProjectUx6S(sourceProject, expectedExtensions);
            var projectNeighbors = string.Equals(ProjectJsonSerializer.Serialize(expectedProject),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal);
            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceProject, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(finalProject, editing.Project);
            var persistence = editing.Save().Succeeded && reopened.Open(root).Succeeded &&
                              reopened.Project is not null && string.Equals(finalJson,
                                  ProjectJsonSerializer.Serialize(reopened.Project), StringComparison.Ordinal);
            var human = !ContainsUuidUx6S(string.Join(' ', editing.CreatorPolicyChoicesUx6S()
                                .Select(item => item.Label)) + " " + string.Join(' ', freshPreview.Diff) + " " +
                            string.Join(' ', freshPreview.RuntimePreview));
            var bounded = freshPreview.RuntimeClaimsBounded && freshPreview.RuntimePreview.Any(item =>
                item.Contains("Aucun périphérique", StringComparison.Ordinal));

            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6S(root);
            var succeeded = explicitSelection && realReadable && deviceValidation && resourceValidation &&
                            partyValidation && progressionValidation && canonicalDryRun &&
                            freshPreview.DevicePolicyResolved && freshPreview.ResourcePolicyResolved &&
                            freshPreview.PartyPolicyResolved && freshPreview.ProgressionPolicyResolved &&
                            staleSourceBlocked && staleApprovalBlocked && canonicalCommand && undone && redone &&
                            persistence && creatorNeighbors && postNeighbors && extensionNeighbors &&
                            projectNeighbors && human && bounded && cleanup;
            return new(succeeded, explicitSelection, realReadable, deviceValidation, resourceValidation,
                partyValidation, progressionValidation, canonicalDryRun, freshPreview.DevicePolicyResolved,
                freshPreview.ResourcePolicyResolved, freshPreview.PartyPolicyResolved,
                freshPreview.ProgressionPolicyResolved, staleSourceBlocked, staleApprovalBlocked,
                canonicalCommand, undone && redone, persistence, creatorNeighbors, postNeighbors,
                extensionNeighbors, projectNeighbors, human, bounded, cleanup);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_S_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedSmokeUx6S();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6S(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_S_CLEANUP_FAILED");
        }
    }

    private static bool TryBuildCreatorPolicyCandidateUx6S(ProjectDefinition project, StudioUx6SDraft draft,
        out ModuleRuntimeExtensionProfile? candidate, out StudioUx6SDiagnostic? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        if (draft.AllowedDeviceQueries.Any(string.IsNullOrWhiteSpace) ||
            draft.DeviceQueriesEnabled && (!draft.RequireConsent || !draft.RedactStableIdentifiers))
        {
            diagnostic = new("studio.creator.device_policy_invalid",
                "module/runtimePolicies/extensions/postLaunch/creatorExperience/deviceQueries",
                "Les requêtes de périphérique actives exigent consentement, anonymisation et liste autorisée.");
            return false;
        }
        if (draft.MaximumResourceResults is < 1 or > 10_000 ||
            draft.MaximumResourceBytes is < 1_024 or > 1_073_741_824 ||
            draft.AllowedResourceNamespaces.Any(string.IsNullOrWhiteSpace) ||
            draft.ResourceCommandsEnabled && draft.AllowedResourceNamespaces.Count == 0 ||
            draft.RequestedResourceResults < 0)
        {
            diagnostic = new("studio.creator.resource_policy_invalid",
                "module/runtimePolicies/extensions/postLaunch/creatorExperience/resourceCommands",
                "Les commandes de ressources exigent quotas bornés et espaces de noms explicites.");
            return false;
        }
        if (draft.MaximumControlledMembers is < 1 or > 64 ||
            draft.PartyCommands.Any(string.IsNullOrWhiteSpace) ||
            draft.PartyControlEnabled && draft.PartyCommands.Count == 0 || draft.RequestedPartyMembers < 0)
        {
            diagnostic = new("studio.creator.party_policy_invalid",
                "module/runtimePolicies/extensions/postLaunch/creatorExperience/partyControl",
                "Le contrôle de groupe exige une capacité bornée et des commandes explicites.");
            return false;
        }
        if (draft.MaximumProgressionTracks is < 8 or > 256 || !draft.DataDrivenProgression ||
            draft.RequestedProgressionTracks < 0)
        {
            diagnostic = new("studio.creator.progression_policy_invalid",
                "module/runtimePolicies/extensions/postLaunch/creatorExperience/progressionTracks",
                "La progression exige au moins huit parcours data-driven et au plus 256.");
            return false;
        }

        try
        {
            var extensions = project.Module.RuntimePolicies.Extensions;
            var post = extensions.PostLaunch;
            var creator = post.CreatorExperience;
            var device = new CreatorExperienceProductionProfile.DeviceQuerySettings(
                creator.DeviceQueries.Id, draft.DeviceQueriesEnabled, draft.AllowedDeviceQueries,
                draft.RequireConsent, draft.RedactStableIdentifiers);
            var resources = new CreatorExperienceProductionProfile.ResourceCommandSettings(
                creator.ResourceCommands.Id, draft.ResourceCommandsEnabled, draft.MaximumResourceResults,
                draft.MaximumResourceBytes, draft.AllowedResourceNamespaces);
            var party = new CreatorExperienceProductionProfile.PartyControlSettings(
                creator.PartyControl.Id, draft.PartyControlEnabled, draft.MaximumControlledMembers,
                draft.AllowDirectSwitch, draft.PartyCommands);
            var progression = new CreatorExperienceProductionProfile.ProgressionTrackSettings(
                creator.ProgressionTracks.Id, draft.MaximumProgressionTracks, draft.DataDrivenProgression,
                draft.SupportLargeSetUi);
            var updatedCreator = new CreatorExperienceProductionProfile(
                creator.Id, creator.SchemaVersion, creator.Water, device, resources, creator.DynamicAreas,
                party, progression, creator.RepositoryMetadata, creator.Observability, creator.Graphics,
                Ux6SProvenance);
            var updatedPost = new PostLaunchCapabilityProfile(post.Id, post.SchemaVersion, post.Gameplay,
                post.LiveDirector, post.Network, updatedCreator, post.Production, Ux6SProvenance);
            candidate = new ModuleRuntimeExtensionProfile(extensions.Id, extensions.SchemaVersion,
                extensions.RuleSchemas, extensions.UiDocuments, extensions.JsonDocuments,
                extensions.SnapshotPolicy, Ux6SProvenance, extensions.ModernTooling,
                extensions.OperationalReadiness, updatedPost);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            diagnostic = new("studio.creator.command_validation_failed",
                "module/runtimePolicies/extensions/postLaunch/creatorExperience",
                $"Le contrat canonique refuse cette politique : {exception.Message}");
            return false;
        }
    }

    private static IReadOnlyList<string> BuildDiffUx6S(CreatorExperienceProductionProfile source,
        CreatorExperienceProductionProfile candidate)
    {
        var diff = new List<string>();
        Add(source.DeviceQueries.Enabled, candidate.DeviceQueries.Enabled, "Requêtes de périphérique");
        AddList(source.DeviceQueries.AllowedQueries, candidate.DeviceQueries.AllowedQueries,
            "Liste de requêtes autorisées");
        Add(source.DeviceQueries.RequireConsent, candidate.DeviceQueries.RequireConsent, "Consentement");
        Add(source.DeviceQueries.RedactStableIdentifiers, candidate.DeviceQueries.RedactStableIdentifiers,
            "Anonymisation des identifiants");
        Add(source.ResourceCommands.Enabled, candidate.ResourceCommands.Enabled, "Commandes de ressources");
        Add(source.ResourceCommands.MaximumResults, candidate.ResourceCommands.MaximumResults,
            "Résultats maximum");
        Add(source.ResourceCommands.MaximumBytes, candidate.ResourceCommands.MaximumBytes, "Octets maximum");
        AddList(source.ResourceCommands.AllowedNamespaces, candidate.ResourceCommands.AllowedNamespaces,
            "Espaces de noms autorisés");
        Add(source.PartyControl.Enabled, candidate.PartyControl.Enabled, "Contrôle de groupe");
        Add(source.PartyControl.MaximumControlledMembers, candidate.PartyControl.MaximumControlledMembers,
            "Membres contrôlables");
        Add(source.PartyControl.AllowDirectSwitch, candidate.PartyControl.AllowDirectSwitch,
            "Bascule directe");
        AddList(source.PartyControl.Commands, candidate.PartyControl.Commands, "Commandes de groupe");
        Add(source.ProgressionTracks.MaximumTracks, candidate.ProgressionTracks.MaximumTracks,
            "Parcours de progression");
        Add(source.ProgressionTracks.SupportLargeSetUi, candidate.ProgressionTracks.SupportLargeSetUi,
            "Interface grand ensemble");
        return diff;

        void Add<T>(T before, T after, string label) where T : notnull
        {
            if (!EqualityComparer<T>.Default.Equals(before, after)) diff.Add($"{label} · {before} → {after}");
        }

        void AddList(IReadOnlyList<string> before, IReadOnlyList<string> after, string label)
        {
            if (!before.SequenceEqual(after)) diff.Add($"{label} · {before.Count} → {after.Count}");
        }
    }

    private static IReadOnlyList<string> BuildRuntimePreviewUx6S(CreatorExperienceProductionProfile creator,
        StudioUx6SDraft draft, PostLaunchCapabilityResolver.CreatorTechnicalDecision technical,
        PostLaunchCapabilityResolver.RpgDistributionDecision distribution)
    {
        var granted = technical.DeviceQueries.Count == 0
            ? "aucune requête accordée"
            : string.Join(", ", technical.DeviceQueries.Select(HumanUx6S));
        var resourceState = technical.Accepted
            ? $"requête de {draft.RequestedResourceResults} résultat(s) dans le quota"
            : $"requête de {draft.RequestedResourceResults} résultat(s) refusée";
        var partyState = distribution.PartyControl
            ? $"{draft.RequestedPartyMembers} membre(s) contrôlables sur {distribution.MaximumParty}"
            : "contrôle direct refusé par la politique";
        var progressionState = draft.RequestedProgressionTracks <= distribution.ProgressionTracks
            ? $"{draft.RequestedProgressionTracks} classe(s)/parcours simulés sur {distribution.ProgressionTracks}"
            : $"{draft.RequestedProgressionTracks} classe(s)/parcours dépassent la limite";
        return
        [
            $"Périphériques · {granted} · consentement {(creator.DeviceQueries.RequireConsent ? "requis" : "absent")} · identifiants anonymisés",
            $"Ressources VM · {resourceState} · plafond {creator.ResourceCommands.MaximumResults} résultat(s) et {creator.ResourceCommands.MaximumBytes / 1_048_576d:0.##} Mio",
            $"Groupe · {partyState} · commandes {string.Join(", ", creator.PartyControl.Commands.Select(HumanUx6S))}",
            $"Progression · {progressionState} · data-driven {(creator.ProgressionTracks.DataDriven ? "oui" : "non")}",
            "Aucun périphérique interrogé, aucune ressource chargée, aucun membre piloté et aucune progression créée dans Studio.",
        ];
    }

    private static StudioUx6SPreview FailedPreviewUx6S(string message, string code, string path) =>
        new(false, false, false, message, [], [new(code, path, message)], [], false, false, false, false, true);

    private static string HumanCommandFailureUx6S(CommandResult result)
    {
        var diagnostic = result.Diagnostics.FirstOrDefault();
        return diagnostic is null
            ? "La commande canonique a refusé la politique Creator."
            : $"La commande canonique a refusé la politique ({diagnostic.Code}).";
    }

    private static ProjectDefinition BuildProjectUx6S(ProjectDefinition source,
        ModuleRuntimeExtensionProfile extensions)
    {
        var current = source.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(current.Id, current.SchemaVersion, current.Heartbeats,
            current.Rest, current.SequenceAbort, "runtime-extension-command", current.Cinematics,
            current.Gameplay, current.Rules, extensions, current.GameplayEvents, current.World,
            current.LifecycleEvents);
        return new ProjectDefinition(source.Id, source.Title, source.Module.WithRuntimePolicies(policies),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
    }

    private static string SourceFingerprintUx6S(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string CandidateFingerprintUx6S(ProjectDefinition project, StudioUx6SDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            WriteTextUx6S(writer, "ux6-s-draft-v1");
            writer.Write(draft.BaseRevision.Value);
            WriteTextUx6S(writer, draft.SourceFingerprint);
            writer.Write(draft.DeviceQueriesEnabled);
            WriteManyUx6S(writer, draft.AllowedDeviceQueries);
            writer.Write(draft.RequireConsent);
            writer.Write(draft.RedactStableIdentifiers);
            writer.Write(draft.ResourceCommandsEnabled);
            writer.Write(draft.MaximumResourceResults);
            writer.Write(draft.MaximumResourceBytes);
            WriteManyUx6S(writer, draft.AllowedResourceNamespaces);
            writer.Write(draft.PartyControlEnabled);
            writer.Write(draft.MaximumControlledMembers);
            writer.Write(draft.AllowDirectSwitch);
            WriteManyUx6S(writer, draft.PartyCommands);
            writer.Write(draft.MaximumProgressionTracks);
            writer.Write(draft.DataDrivenProgression);
            writer.Write(draft.SupportLargeSetUi);
            WriteManyUx6S(writer, draft.RequestedDeviceQueries);
            writer.Write(draft.RequestedResourceResults);
            writer.Write(draft.RequestedPartyMembers);
            writer.Write(draft.RequestedProgressionTracks);
            WriteTextUx6S(writer, ProjectJsonSerializer.Serialize(project));
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteManyUx6S(BinaryWriter writer, IReadOnlyList<string> values)
    {
        writer.Write(values.Count);
        foreach (var value in values) WriteTextUx6S(writer, value);
    }

    private static void WriteTextUx6S(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string NormalizeUx6S(string value) => value.Trim().ToLowerInvariant();

    private static string HumanUx6S(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "aucun";
        if (Guid.TryParse(value, out _)) return "identité protégée";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsUuidUx6S(string value) =>
        value.Split([' ', '\n', '\r', '\t', ',', ';', '(', ')', '[', ']', '{', '}'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim('.', ':', '«', '»'))
            .Any(item => Guid.TryParse(item, out _));

    internal static bool DeleteTemporaryUx6S(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux6-s-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static StudioUx6SSmokeResult FailedSmokeUx6S() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false, false);
}

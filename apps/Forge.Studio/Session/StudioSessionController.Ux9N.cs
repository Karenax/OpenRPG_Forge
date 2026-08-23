using System.Globalization;
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
using Forge.Network.Operations;
using Forge.Network.Production;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux9NProvenance = "studio:ux9-n:network-production";
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsUx9N = new(new SystemClock());
    private StudioUx9NNetworkProductionDraft? _approvedNetworkProductionUx9N;
    private ProjectRevision? _approvedNetworkProductionRevisionUx9N;
    private string? _approvedNetworkProductionFingerprintUx9N;
    private StudioUx9NNetworkProductionProbe _networkProductionProbeUx9N = EmptyProbeUx9N();
    private readonly List<string> _networkProductionAuditUx9N = [];

    internal StudioUx9NNetworkProductionProbe NetworkProductionProbeUx9N =>
        _networkProductionProbeUx9N;
    internal IReadOnlyList<string> NetworkProductionAuditUx9N =>
        _networkProductionAuditUx9N.ToArray();
    internal NetworkProductionProfile? CurrentNetworkProductionUx9N =>
        Project?.Module.RuntimePolicies.Extensions.PostLaunch.Network;
    internal IReadOnlyList<StudioUx9NAreaChoice> NetworkProductionAreaChoicesUx9N =>
        Project?.Areas.Select(area => new StudioUx9NAreaChoice(area.Id.Value,
                HumanUx8N(area.DisplayName)))
            .ToArray() ?? [];

    internal StudioUx9NNetworkProductionDraft? CreateNetworkProductionDraftUx9N(
        bool explicitlySelected = false)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || project is null || history is null) return null;
        var post = project.Module.RuntimePolicies.Extensions.PostLaunch;
        var gameplay = post.Gameplay;
        var network = post.Network;
        var shard = network.Shards.FirstOrDefault();
        var areaId = shard?.AreaIds.FirstOrDefault() ??
                     project.Areas.FirstOrDefault()?.Id.Value ??
                     project.Module.EntryAreaId.Value;
        return new(history.State.Revision,
            NetworkProductionSourceFingerprintUx9N(project),
            gameplay.CuratedPacks.Channel,
            gameplay.CuratedPacks.Compatibility,
            string.Join(", ", gameplay.CuratedPacks.AllowedLicenses),
            gameplay.CuratedPacks.RequireTrustedSignature,
            gameplay.CuratedPacks.AllowOfflineFallback,
            network.Voice.Enabled,
            network.Voice.Codec,
            network.Voice.MaximumBitrateKbps,
            network.Voice.Positional,
            network.Voice.PushToTalk,
            network.Voice.ModerationEnabled,
            shard is not null,
            shard?.Key ?? "serveur-principal",
            areaId,
            shard?.MaximumPlayers ?? 64,
            shard?.Region ?? "local",
            network.HotReload.Enabled,
            network.HotReload.MaximumPackageMegabytes,
            network.HotReload.DrainTimeoutSeconds,
            network.ServerExtensions.Enabled,
            string.Join(", ", network.ServerExtensions.AllowedCapabilities),
            network.ServerExtensions.CpuBudgetMilliseconds,
            network.Protocol.ProtocolFamily,
            network.Protocol.MinimumVersion,
            network.Protocol.MaximumVersion,
            string.Join(", ", network.Protocol.Platforms),
            network.Protocol.RejectUnknownFields);
    }

    internal StudioUx9NNetworkProductionPreview PreviewNetworkProductionUx9N(
        StudioUx9NNetworkProductionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelNetworkProductionApprovalUx9N();
        var project = Project;
        var history = History;
        if (project is null || history is null)
            return FailedPreviewUx9N("Ouvrez d’abord un module.");
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceFingerprint,
                NetworkProductionSourceFingerprintUx9N(project), StringComparison.Ordinal))
            return FailedPreviewUx9N(
                "Le module a changé ; rechargez les politiques avant l’aperçu sec.");
        if (!TryBuildNetworkProductionProfileUx9N(project, draft, out var extensions,
                out var diagnostic) || extensions is null)
            return FailedPreviewUx9N(diagnostic ?? "Les politiques réseau sont invalides.");

        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, extensions, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx9N.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return FailedPreviewUx9N(HumanDiagnosticsUx9N(dryRun.Diagnostics));
        var sourcePost = project.Module.RuntimePolicies.Extensions.PostLaunch;
        var candidatePost = extensions.PostLaunch;
        var changes = BuildNetworkProductionDiffUx9N(sourcePost.Gameplay,
            sourcePost.Network, candidatePost.Gameplay, candidatePost.Network);
        StudioUx9NNetworkProductionProbe probe;
        try
        {
            probe = RunNetworkProductionProbeUx9N(candidatePost.Gameplay,
                candidatePost.Network);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            return FailedPreviewUx9N(
                $"La sandbox locale refuse ces politiques : {exception.Message}");
        }
        _networkProductionProbeUx9N = probe;
        var canApply = changes.Count > 0 && ProbeReadyUx9N(probe);
        if (canApply)
        {
            _approvedNetworkProductionUx9N = draft with { };
            _approvedNetworkProductionRevisionUx9N = history.State.Revision;
            _approvedNetworkProductionFingerprintUx9N = DraftFingerprintUx9N(draft);
        }
        return new(true, canApply, true, ProbeReadyUx9N(probe),
            changes.Count == 0
                ? "Aperçu sec validé · ces politiques sont déjà actives."
                : "Aperçu sec validé · aucun contenu, appel vocal ou serveur réel n’a été lancé.",
            changes, [], probe);
    }

    internal StudioUx9NActionResult ApplyApprovedNetworkProductionUx9N(
        StudioUx9NNetworkProductionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var approved = _approvedNetworkProductionUx9N;
        var project = Project;
        var history = History;
        if (approved is null || project is null || history is null ||
            _approvedNetworkProductionRevisionUx9N != history.State.Revision ||
            _approvedNetworkProductionFingerprintUx9N is null ||
            !string.Equals(DraftFingerprintUx9N(draft),
                _approvedNetworkProductionFingerprintUx9N, StringComparison.Ordinal) ||
            !string.Equals(DraftFingerprintUx9N(approved),
                _approvedNetworkProductionFingerprintUx9N, StringComparison.Ordinal) ||
            approved.BaseRevision != history.State.Revision ||
            !string.Equals(approved.SourceFingerprint,
                NetworkProductionSourceFingerprintUx9N(project), StringComparison.Ordinal))
        {
            CancelNetworkProductionApprovalUx9N();
            return new(false, "Relancez l’aperçu sec avant d’appliquer.");
        }
        if (!TryBuildNetworkProductionProfileUx9N(project, approved,
                out var extensions, out var diagnostic) || extensions is null)
        {
            CancelNetworkProductionApprovalUx9N();
            return new(false, diagnostic ?? "Les politiques ne peuvent pas être reconstruites.");
        }
        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()),
            history.State.Revision, extensions, DryRun: true);
        var dryRun = _setRuntimeExtensionsUx9N.Handle(history.State, command);
        if (!dryRun.Succeeded)
        {
            CancelNetworkProductionApprovalUx9N();
            return new(false, HumanDiagnosticsUx9N(dryRun.Diagnostics));
        }
        var applied = _setRuntimeExtensionsUx9N.Handle(history.State,
            command with { DryRun = false });
        CancelNetworkProductionApprovalUx9N();
        if (!applied.Succeeded)
            return new(false, HumanDiagnosticsUx9N(applied.Diagnostics));
        history.Record(applied);
        InvalidateContentIndexes();
        _networkProductionAuditUx9N.Add(
            $"applied:studio.network-production:revision={history.State.Revision.Value}");
        return new(true,
            "Politiques réseau appliquées par une commande canonique annulable.");
    }

    internal StudioUx9NActionResult UndoNetworkProductionUx9N()
    {
        CancelNetworkProductionApprovalUx9N();
        var result = Undo();
        if (result.Succeeded) _networkProductionAuditUx9N.Add("undo:studio.network-production");
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx9NActionResult RedoNetworkProductionUx9N()
    {
        CancelNetworkProductionApprovalUx9N();
        var result = Redo();
        if (result.Succeeded) _networkProductionAuditUx9N.Add("redo:studio.network-production");
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx9NActionResult SaveNetworkProductionUx9N()
    {
        CancelNetworkProductionApprovalUx9N();
        var result = Save();
        if (result.Succeeded) _networkProductionAuditUx9N.Add("saved:studio.network-production");
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx9NActionResult RunLocalNetworkProductionUx9N(bool confirmed,
        NetworkRole role)
    {
        if (!confirmed)
        {
            _networkProductionAuditUx9N.Add(
                "rejected:studio.network-production:confirmation-required");
            return new(false, "Confirmez explicitement la sandbox locale restaurée.");
        }
        if (role != NetworkRole.Administrator)
        {
            _networkProductionAuditUx9N.Add(
                "rejected:studio.network-production:permission-denied");
            return new(false, "La permission Administrateur est requise.");
        }
        var post = Project?.Module.RuntimePolicies.Extensions.PostLaunch;
        if (post is null) return new(false, "Ouvrez d’abord un module.");
        try
        {
            var probe = RunNetworkProductionProbeUx9N(post.Gameplay, post.Network);
            _networkProductionProbeUx9N = probe;
            _networkProductionAuditUx9N.Add(
                $"sandbox:content={probe.ContentCode}:reload={probe.ReloadCode}:protocol={probe.ProtocolCode}");
            return new(ProbeReadyUx9N(probe), ProbeReadyUx9N(probe)
                ? "Sandbox terminée · contenu, voix, shard, reload, extensions et protocole validés."
                : "La sandbox n’a pas validé les six politiques.");
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               InvalidOperationException or InvalidDataException)
        {
            _networkProductionAuditUx9N.Add(
                $"rejected:studio.network-production:{exception.Message}");
            return new(false, $"La sandbox locale a échoué : {exception.Message}");
        }
    }

    internal StudioUx9NApprovalSnapshot CaptureNetworkProductionApprovalUx9N() => new(
        _approvedNetworkProductionUx9N, _approvedNetworkProductionRevisionUx9N,
        _approvedNetworkProductionFingerprintUx9N);

    internal void RestoreNetworkProductionApprovalUx9N(StudioUx9NApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedNetworkProductionUx9N = snapshot.Draft;
        _approvedNetworkProductionRevisionUx9N = snapshot.Revision;
        _approvedNetworkProductionFingerprintUx9N = snapshot.Fingerprint;
    }

    internal void CancelNetworkProductionApprovalUx9N()
    {
        _approvedNetworkProductionUx9N = null;
        _approvedNetworkProductionRevisionUx9N = null;
        _approvedNetworkProductionFingerprintUx9N = null;
    }

    internal void CloseUx9N()
    {
        CancelNetworkProductionApprovalUx9N();
        _networkProductionProbeUx9N = EmptyProbeUx9N();
        _networkProductionAuditUx9N.Clear();
    }

    private static bool TryBuildNetworkProductionProfileUx9N(ProjectDefinition project,
        StudioUx9NNetworkProductionDraft draft,
        out ModuleRuntimeExtensionProfile? candidate, out string? diagnostic)
    {
        candidate = null;
        diagnostic = null;
        var licenses = TokensUx9N(draft.AllowedLicensesText);
        var capabilities = TokensUx9N(draft.AllowedCapabilitiesText);
        var platforms = TokensUx9N(draft.ProtocolPlatformsText);
        if (licenses.Length == 0)
        {
            diagnostic = "Ajoutez au moins une licence autorisée pour le contenu curaté.";
            return false;
        }
        if (draft.ExtensionsEnabled && capabilities.Length == 0)
        {
            diagnostic = "Ajoutez au moins une capacité autorisée pour les extensions.";
            return false;
        }
        if (platforms.Length == 0)
        {
            diagnostic = "Ajoutez au moins une plateforme compatible.";
            return false;
        }
        if (draft.VoiceCodec is not ("opus" or "pcm"))
        {
            diagnostic = "Choisissez Opus ou PCM comme codec vocal.";
            return false;
        }
        var availableAreas = project.Areas.Select(area => area.Id.Value)
            .Append(project.Module.EntryAreaId.Value).ToHashSet();
        if (draft.HasShard && (draft.ShardAreaId == Guid.Empty ||
                               !availableAreas.Contains(draft.ShardAreaId)))
        {
            diagnostic = "Choisissez une zone réelle du module pour le shard.";
            return false;
        }
        try
        {
            var extensions = project.Module.RuntimePolicies.Extensions;
            var post = extensions.PostLaunch;
            var gameplay = post.Gameplay;
            var network = post.Network;
            var updatedGameplay = new GameplayProductionProfile(gameplay.Id,
                gameplay.SchemaVersion, gameplay.ZeroGravity, gameplay.Vehicles,
                gameplay.Mounts, gameplay.Achievements,
                gameplay.CuratedPacks with
                {
                    Channel = draft.CuratedChannel,
                    Compatibility = draft.CuratedCompatibility,
                    AllowedLicenses = licenses,
                    RequireTrustedSignature = draft.RequireTrustedSignature,
                    AllowOfflineFallback = draft.AllowOfflineFallback
                }, Ux9NProvenance);
            var shards = network.Shards.Skip(1).ToList();
            if (draft.HasShard)
            {
                var id = network.Shards.FirstOrDefault()?.Id ??
                         StableUx9N(network.Id, "primary-shard");
                shards.Insert(0, new NetworkProductionProfile.ShardDefinition(id,
                    draft.ShardKey, [draft.ShardAreaId], draft.ShardMaximumPlayers,
                    draft.ShardRegion));
            }
            var updatedNetwork = new NetworkProductionProfile(network.Id,
                network.SchemaVersion,
                network.Voice with
                {
                    Enabled = draft.VoiceEnabled,
                    Codec = draft.VoiceCodec,
                    MaximumBitrateKbps = draft.VoiceBitrateKbps,
                    Positional = draft.VoicePositional,
                    PushToTalk = draft.VoicePushToTalk,
                    ModerationEnabled = draft.VoiceModeration
                },
                shards,
                network.HotReload with
                {
                    Enabled = draft.HotReloadEnabled,
                    AtomicSwap = true,
                    RequireRollback = true,
                    MaximumPackageMegabytes = draft.MaximumPackageMegabytes,
                    DrainTimeoutSeconds = draft.DrainTimeoutSeconds
                },
                network.ServerExtensions with
                {
                    Enabled = draft.ExtensionsEnabled,
                    RequireSignature = true,
                    IsolateProcesses = true,
                    AllowedCapabilities = capabilities,
                    CpuBudgetMilliseconds = draft.ExtensionCpuBudgetMilliseconds
                },
                network.Protocol with
                {
                    ProtocolFamily = draft.ProtocolFamily,
                    MinimumVersion = draft.ProtocolMinimumVersion,
                    MaximumVersion = draft.ProtocolMaximumVersion,
                    Platforms = platforms,
                    RejectUnknownFields = draft.RejectUnknownFields
                }, Ux9NProvenance);
            var updatedPost = new PostLaunchCapabilityProfile(post.Id, post.SchemaVersion,
                updatedGameplay, post.LiveDirector, updatedNetwork,
                post.CreatorExperience, post.Production, Ux9NProvenance);
            candidate = new ModuleRuntimeExtensionProfile(extensions.Id,
                extensions.SchemaVersion, extensions.RuleSchemas, extensions.UiDocuments,
                extensions.JsonDocuments, extensions.SnapshotPolicy, Ux9NProvenance,
                extensions.ModernTooling, extensions.OperationalReadiness, updatedPost);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                               ArgumentOutOfRangeException)
        {
            diagnostic = HumanProfileErrorUx9N(exception.Message);
            return false;
        }
    }

    private static IReadOnlyList<string> BuildNetworkProductionDiffUx9N(
        GameplayProductionProfile sourceGameplay, NetworkProductionProfile sourceNetwork,
        GameplayProductionProfile candidateGameplay, NetworkProductionProfile candidateNetwork)
    {
        var entries = new List<string>();
        if (sourceGameplay.CuratedPacks != candidateGameplay.CuratedPacks)
            entries.Add("Contenu curaté · canal, compatibilité, licences et signature");
        if (sourceNetwork.Voice != candidateNetwork.Voice)
            entries.Add("Voix · codec, débit, position, push-to-talk et modération");
        if (!sourceNetwork.Shards.SequenceEqual(candidateNetwork.Shards))
            entries.Add("Shard · zone, capacité et région");
        if (sourceNetwork.HotReload != candidateNetwork.HotReload)
            entries.Add("Hot reload · paquet borné, drain, bascule atomique et rollback");
        if (sourceNetwork.ServerExtensions != candidateNetwork.ServerExtensions)
            entries.Add("Extensions · signature, isolation, capacités et quota CPU");
        if (sourceNetwork.Protocol != candidateNetwork.Protocol)
            entries.Add("Protocole · famille, versions, plateformes et champs inconnus");
        return entries;
    }

    private static StudioUx9NNetworkProductionProbe RunNetworkProductionProbeUx9N(
        GameplayProductionProfile gameplay, NetworkProductionProfile network)
    {
        var license = gameplay.CuratedPacks.AllowedLicenses.First();
        var trusted = PostLaunchCapabilityResolver.ResolveNetworkContent(gameplay, network,
            signerTrusted: true, license);
        var untrusted = PostLaunchCapabilityResolver.ResolveNetworkContent(gameplay, network,
            signerTrusted: false, license);
        var contentReady = trusted.Diagnostic == "POST_NETWORK_CONTENT_READY";
        var untrustedRespected = gameplay.CuratedPacks.RequireTrustedSignature
            ? untrusted.Diagnostic == "POST_NETWORK_SIGNER_UNTRUSTED"
            : untrusted.Diagnostic == "POST_NETWORK_CONTENT_READY";
        var voiceReady = trusted.VoiceEnabled == network.Voice.Enabled &&
                         trusted.BitrateKbps ==
                         (network.Voice.Enabled ? network.Voice.MaximumBitrateKbps : 0);

        var service = new ServerProductionService(network);
        var shard = network.Shards.FirstOrDefault();
        var shardKey = shard?.Key ?? "shard-absent";
        var request = new ServerProductionService.ReloadRequest(Guid.NewGuid(), shardKey,
            Math.Min(32, network.HotReload.MaximumPackageMegabytes), 0, 0,
            new string('a', 64));
        var preview = service.Reload(request, apply: false);
        var reload = shard is not null && network.HotReload.Enabled
            ? service.Reload(request, apply: true)
            : preview;
        var shardReady = shard is null
            ? reload.Code == "POST_SHARD_UNKNOWN"
            : shard.AreaIds.Count > 0 && shard.MaximumPlayers > 0 &&
              !string.IsNullOrWhiteSpace(shard.Region);
        var reloadReady = shard is null
            ? reload.Code == "POST_SHARD_UNKNOWN"
            : network.HotReload.Enabled
                ? preview.Code == "NET_PRODUCTION_PREVIEW" && reload.Applied &&
                  reload.RollbackAvailable && reload.SemanticDiff.Contains("atomic=True",
                      StringComparison.Ordinal)
                : reload.Code == "POST_HOT_RELOAD_DENIED";

        var platform = network.Protocol.Platforms.First();
        var allowed = network.ServerExtensions.AllowedCapabilities.FirstOrDefault() ??
                      "world-read";
        var negotiated = service.Negotiate(platform, network.Protocol.MinimumVersion,
            network.Protocol.MaximumVersion, [allowed, "unsafe-shell"],
            signedExtension: true);
        var unsigned = service.Negotiate(platform, network.Protocol.MinimumVersion,
            network.Protocol.MaximumVersion, [allowed], signedExtension: false);
        var protocolReady = negotiated.Compatible && negotiated.NegotiatedVersion >=
            network.Protocol.MinimumVersion;
        var extensionReady = network.ServerExtensions.Enabled
            ? negotiated.ExtensionAllowed && negotiated.GrantedCapabilities.Contains(allowed,
                StringComparer.Ordinal)
            : !negotiated.ExtensionAllowed;
        var unsignedBlocked = !unsigned.ExtensionAllowed && unsigned.GrantedCapabilities.Count == 0;
        var ready = contentReady && untrustedRespected && voiceReady && shardReady &&
                    reloadReady && protocolReady && extensionReady && unsignedBlocked;
        return new(ready ? "Six politiques vérifiées" : "Sandbox incomplète",
            trusted.Diagnostic, contentReady, untrustedRespected, voiceReady,
            trusted.BitrateKbps, reload.Code, shardReady, reloadReady,
            reload.RollbackAvailable || !network.HotReload.Enabled,
            negotiated.Diagnostic, protocolReady, extensionReady, unsignedBlocked, true);
    }

    internal StudioUx9NSmokeResult RunUx9NContractSmoke()
    {
        var sourceProject = Project;
        var sourceHistory = History;
        var sourceSession = Session;
        if (sourceProject is null || sourceHistory is null || sourceSession is null)
            return FailedSmokeUx9N();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var sourceCanUndo = sourceHistory.CanUndo;
        var sourceCanRedo = sourceHistory.CanRedo;
        var sourceArea = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelected = SelectedEntityId;
        var sourceApproval = CaptureNetworkProductionApprovalUx9N();
        var root = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-n-{Guid.NewGuid():N}");
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var result = FailedSmokeUx9N();
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return result;
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded) return result;
            var explicitSelection = editing.CreateNetworkProductionDraftUx9N() is null &&
                                    editing.CreateNetworkProductionDraftUx9N(true) is not null;
            var baseline = editing.CreateNetworkProductionDraftUx9N(true)!;
            var fixture = ChangedDraftUx9N(baseline);
            var invalid = !editing.PreviewNetworkProductionUx9N(fixture with
            { AllowedLicensesText = "" }).Succeeded &&
                          !editing.PreviewNetworkProductionUx9N(fixture with
                          { VoiceBitrateKbps = 7 }).Succeeded &&
                          !editing.PreviewNetworkProductionUx9N(fixture with
                          { ShardMaximumPlayers = 0 }).Succeeded &&
                          !editing.PreviewNetworkProductionUx9N(fixture with
                          { MaximumPackageMegabytes = 0 }).Succeeded &&
                          !editing.PreviewNetworkProductionUx9N(fixture with
                          { AllowedCapabilitiesText = "" }).Succeeded &&
                          !editing.PreviewNetworkProductionUx9N(fixture with
                          { ProtocolMaximumVersion = 0 }).Succeeded &&
                          !editing.PreviewNetworkProductionUx9N(fixture with
                          { ProtocolPlatformsText = "" }).Succeeded;
            var staleSource = !editing.PreviewNetworkProductionUx9N(fixture with
            {
                BaseRevision = new ProjectRevision(fixture.BaseRevision.Value + 1)
            }).Succeeded;
            var beforeDryRun = editing.Project;
            var revisionBeforeDryRun = editing.History!.State.Revision;
            var journalBeforeDryRun = editing.History.Journal.ToArray();
            var dirtyBeforeDryRun = editing.IsDirty;
            var preview = editing.PreviewNetworkProductionUx9N(fixture);
            var canonicalDryRun = preview.Succeeded && preview.CanApply &&
                                  preview.CanonicalDryRun &&
                                  ReferenceEquals(beforeDryRun, editing.Project) &&
                                  editing.History.State.Revision == revisionBeforeDryRun &&
                                  editing.History.Journal.SequenceEqual(journalBeforeDryRun) &&
                                  editing.IsDirty == dirtyBeforeDryRun;
            var staleApproval = !editing.ApplyApprovedNetworkProductionUx9N(fixture with
            {
                VoiceBitrateKbps = fixture.VoiceBitrateKbps == 512
                    ? 504 : fixture.VoiceBitrateKbps + 8
            }).Succeeded;
            preview = editing.PreviewNetworkProductionUx9N(fixture);
            var sourceState = editing.Project!;
            var sourcePolicies = sourceState.Module.RuntimePolicies;
            var sourceExtensions = sourcePolicies.Extensions;
            var sourcePost = sourceExtensions.PostLaunch;
            if (!TryBuildNetworkProductionProfileUx9N(sourceState, fixture,
                    out var expectedExtensions, out _) || expectedExtensions is null)
                return result;
            var applied = editing.ApplyApprovedNetworkProductionUx9N(fixture);
            var finalProject = editing.Project!;
            var finalPost = finalProject.Module.RuntimePolicies.Extensions.PostLaunch;
            var primaryShard = finalPost.Network.Shards.FirstOrDefault();
            var sixRoutes =
                finalPost.Gameplay.CuratedPacks.Channel == NormalizeKeyUx9N(fixture.CuratedChannel) &&
                finalPost.Gameplay.CuratedPacks.AllowedLicenses.SequenceEqual(
                    TokensUx9N(fixture.AllowedLicensesText).Select(value =>
                        value.ToUpperInvariant()).Order(StringComparer.Ordinal)) &&
                finalPost.Network.Voice.Enabled == fixture.VoiceEnabled &&
                finalPost.Network.Voice.MaximumBitrateKbps == fixture.VoiceBitrateKbps &&
                primaryShard is not null && primaryShard.Key == NormalizeKeyUx9N(fixture.ShardKey) &&
                primaryShard.MaximumPlayers == fixture.ShardMaximumPlayers &&
                finalPost.Network.HotReload.Enabled == fixture.HotReloadEnabled &&
                finalPost.Network.ServerExtensions.Enabled == fixture.ExtensionsEnabled &&
                finalPost.Network.Protocol.MaximumVersion == fixture.ProtocolMaximumVersion;
            var command = applied.Succeeded && editing.History!.Journal.LastOrDefault()?.CommandName ==
                          "set_runtime_extensions";
            var expectedPolicies = new ModuleRuntimePolicyProfile(sourcePolicies.Id,
                sourcePolicies.SchemaVersion, sourcePolicies.Heartbeats, sourcePolicies.Rest,
                sourcePolicies.SequenceAbort, "runtime-extension-command",
                sourcePolicies.Cinematics, sourcePolicies.Gameplay, sourcePolicies.Rules,
                expectedExtensions, sourcePolicies.GameplayEvents, sourcePolicies.World,
                sourcePolicies.LifecycleEvents);
            var expectedProject = new ProjectDefinition(sourceState.Id, sourceState.Title,
                sourceState.Module.WithRuntimePolicies(expectedPolicies), sourceState.Blueprints,
                sourceState.Areas, sourceState.Dialogues, sourceState.Quests);
            var neighbors = string.Equals(ProjectJsonSerializer.Serialize(expectedProject),
                ProjectJsonSerializer.Serialize(finalProject), StringComparison.Ordinal) &&
                            ReferenceEquals(sourcePost.LiveDirector, finalPost.LiveDirector) &&
                            ReferenceEquals(sourcePost.CreatorExperience,
                                finalPost.CreatorExperience) &&
                            ReferenceEquals(sourcePost.Production, finalPost.Production);
            var finalJson = ProjectJsonSerializer.Serialize(finalProject);
            var undo = editing.UndoNetworkProductionUx9N().Succeeded;
            var redo = editing.RedoNetworkProductionUx9N().Succeeded;
            var saved = editing.SaveNetworkProductionUx9N().Succeeded;
            var permission = !editing.RunLocalNetworkProductionUx9N(true,
                NetworkRole.Player).Succeeded;
            var confirmation = !editing.RunLocalNetworkProductionUx9N(false,
                NetworkRole.Administrator).Succeeded;
            var probed = editing.RunLocalNetworkProductionUx9N(true,
                NetworkRole.Administrator).Succeeded;
            var probe = editing.NetworkProductionProbeUx9N;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded &&
                              reopened.Project is not null &&
                              string.Equals(finalJson,
                                  ProjectJsonSerializer.Serialize(reopened.Project),
                                  StringComparison.Ordinal);
            result = new(true, sixRoutes, explicitSelection, invalid, permission,
                confirmation, canonicalDryRun, command, staleSource, staleApproval,
                undo && redo, persistence,
                probe.CuratedContent && probe.UntrustedContentBlocked,
                probe.Voice && probe.VoiceBitrateKbps == fixture.VoiceBitrateKbps,
                probe.Shard && primaryShard is not null,
                probe.HotReload && probe.Rollback,
                probe.Extensions && probe.UnsignedExtensionBlocked,
                probe.Protocol, neighbors,
                SourceSessionMatchesUx9N(sourceProject, sourceHistory, sourceRevision,
                    sourceJournal, sourceDirty, sourceCanUndo, sourceCanRedo, sourceArea,
                    sourceSelection, sourceSelected, sourceApproval),
                probed && probe.ResourcesReleased);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX9_N_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            result = FailedSmokeUx9N();
        }
        finally
        {
            reopened?.Close();
            editing?.Close();
            if (!DeleteTemporaryWorkspaceUx9N(root))
                result = result with { TemporaryWorkspaceReleased = false };
        }
        var sourceUnchanged = SourceSessionMatchesUx9N(sourceProject, sourceHistory,
            sourceRevision, sourceJournal, sourceDirty, sourceCanUndo, sourceCanRedo,
            sourceArea, sourceSelection, sourceSelected, sourceApproval);
        var released = result.TemporaryWorkspaceReleased && !Directory.Exists(root);
        var succeeded = result.SixRoutes && result.ExplicitSelection &&
                        result.InvalidBlocked && result.PermissionBlocked &&
                        result.ConfirmationRequired && result.CanonicalDryRun &&
                        result.CanonicalCommand && result.StaleSourceBlocked &&
                        result.StaleApprovalBlocked && result.UndoRedo && result.Persistence &&
                        result.CuratedContent && result.Voice && result.Shard &&
                        result.HotReload && result.Extensions && result.Protocol &&
                        result.ProjectNeighborsPreserved && sourceUnchanged && released;
        return result with
        {
            Succeeded = succeeded,
            SourceUnchanged = sourceUnchanged,
            TemporaryWorkspaceReleased = released
        };
    }

    private static StudioUx9NNetworkProductionDraft ChangedDraftUx9N(
        StudioUx9NNetworkProductionDraft source) => source with
        {
            CuratedChannel = source.CuratedChannel == "stable" ? "candidate" : "stable",
            CuratedCompatibility = source.CuratedCompatibility == "2.x" ? "1.x" : "2.x",
            AllowedLicensesText = "CC0-1.0, MIT",
            RequireTrustedSignature = true,
            AllowOfflineFallback = true,
            VoiceEnabled = true,
            VoiceCodec = "opus",
            VoiceBitrateKbps = source.VoiceBitrateKbps == 96 ? 112 : 96,
            VoicePositional = true,
            VoicePushToTalk = true,
            VoiceModeration = true,
            HasShard = true,
            ShardKey = source.HasShard ? source.ShardKey : "shard-preuve",
            ShardMaximumPlayers = source.ShardMaximumPlayers == 48 ? 64 : 48,
            ShardRegion = source.HasShard ? source.ShardRegion : "local",
            HotReloadEnabled = true,
            MaximumPackageMegabytes = source.MaximumPackageMegabytes == 256 ? 512 : 256,
            DrainTimeoutSeconds = source.DrainTimeoutSeconds == 45 ? 60 : 45,
            ExtensionsEnabled = true,
            AllowedCapabilitiesText = "metrics, world-read",
            ExtensionCpuBudgetMilliseconds = source.ExtensionCpuBudgetMilliseconds == 75 ? 50 : 75,
            ProtocolFamily = source.ProtocolFamily,
            ProtocolMinimumVersion = 1,
            ProtocolMaximumVersion = source.ProtocolMaximumVersion == 2 ? 3 : 2,
            ProtocolPlatformsText = "windows, linux, macos",
            RejectUnknownFields = true
        };

    private bool SourceSessionMatchesUx9N(ProjectDefinition project,
        ProjectCommandHistory history, ProjectRevision revision,
        IReadOnlyList<CommandHistoryJournalEntry> journal, bool dirty, bool canUndo,
        bool canRedo, Forge.Domain.Areas.AreaId? area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> selection,
        Forge.Domain.Entities.EntityId? selected, StudioUx9NApprovalSnapshot approval) =>
        ReferenceEquals(Project, project) && ReferenceEquals(History, history) &&
        history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
        IsDirty == dirty && history.CanUndo == canUndo && history.CanRedo == canRedo &&
        ActiveAreaId == area && SelectedEntityIds.SequenceEqual(selection) &&
        SelectedEntityId == selected && CaptureNetworkProductionApprovalUx9N() == approval;

    private static bool DeleteTemporaryWorkspaceUx9N(string root)
    {
        try
        {
            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), temp,
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-n-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string NetworkProductionSourceFingerprintUx9N(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            ProjectJsonSerializer.Serialize(project))));

    private static string DraftFingerprintUx9N(StudioUx9NNetworkProductionDraft draft)
    {
        var text = string.Join('|', draft.BaseRevision.Value, draft.SourceFingerprint,
            draft.CuratedChannel, draft.CuratedCompatibility, draft.AllowedLicensesText,
            draft.RequireTrustedSignature, draft.AllowOfflineFallback, draft.VoiceEnabled,
            draft.VoiceCodec, draft.VoiceBitrateKbps, draft.VoicePositional,
            draft.VoicePushToTalk, draft.VoiceModeration, draft.HasShard, draft.ShardKey,
            draft.ShardAreaId, draft.ShardMaximumPlayers, draft.ShardRegion,
            draft.HotReloadEnabled, draft.MaximumPackageMegabytes,
            draft.DrainTimeoutSeconds, draft.ExtensionsEnabled,
            draft.AllowedCapabilitiesText, draft.ExtensionCpuBudgetMilliseconds,
            draft.ProtocolFamily, draft.ProtocolMinimumVersion,
            draft.ProtocolMaximumVersion, draft.ProtocolPlatformsText,
            draft.RejectUnknownFields);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static string[] TokensUx9N(string value) => value.Split([',', ';'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Guid StableUx9N(Guid ownerId, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:studio:ux9-n:{ownerId:D}:{discriminator}"));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static string NormalizeKeyUx9N(string value) => string.Join('-', value.Trim()
        .ToLowerInvariant().Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries));

    private static bool ProbeReadyUx9N(StudioUx9NNetworkProductionProbe probe) =>
        probe.CuratedContent && probe.UntrustedContentBlocked && probe.Voice &&
        probe.Shard && probe.HotReload && probe.Protocol && probe.Extensions &&
        probe.UnsignedExtensionBlocked && probe.ResourcesReleased;

    private static string HumanProfileErrorUx9N(string message) =>
        message.Contains("voice profile", StringComparison.Ordinal)
            ? "Le codec vocal et le débit doivent rester dans les bornes prises en charge."
            : message.Contains("Shards require", StringComparison.Ordinal)
                ? "Le shard exige une zone, une région et une capacité de 1 à 100 000 joueurs."
                : message.Contains("hot reload", StringComparison.OrdinalIgnoreCase)
                    ? "Le hot reload doit rester atomique, réversible et borné."
                    : message.Contains("Server extensions", StringComparison.Ordinal)
                        ? "Les extensions doivent rester signées, isolées et limitées en CPU."
                        : message.Contains("Protocol compatibility", StringComparison.Ordinal)
                            ? "Le protocole exige une famille, des versions ordonnées et des plateformes."
                            : message.Contains("Curated packs", StringComparison.Ordinal)
                                ? "Le contenu curaté exige canal, compatibilité et licences explicites."
                                : $"Le contrat canonique refuse le profil : {message}";

    private static string HumanDiagnosticsUx9N(IReadOnlyList<CommandDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "La commande canonique a refusé les politiques."
            : string.Join(" · ", diagnostics.Select(item => item.Message));

    private static StudioUx9NNetworkProductionPreview FailedPreviewUx9N(string message) =>
        new(false, false, false, false, message, [], [message], EmptyProbeUx9N());

    private static StudioUx9NNetworkProductionProbe EmptyProbeUx9N() => new(
        "Aucune sandbox exécutée", "—", false, false, false, 0, "—", false,
        false, false, "—", false, false, false, true);

    private static StudioUx9NSmokeResult FailedSmokeUx9N() => new(false, false,
        false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false);
}

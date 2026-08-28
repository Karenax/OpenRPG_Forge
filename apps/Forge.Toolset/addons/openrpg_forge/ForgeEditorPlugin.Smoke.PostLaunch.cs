#if TOOLS
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Creator;
using Forge.Application.Creatures;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Modules;
using Forge.Application.Rules;
using Forge.Application.Localization;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Quality;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Content;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Localization;
using Forge.Domain.Lifecycle;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Audio;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Content;
using Forge.Network.Replication;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Combat;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Factions;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Merchants;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeEditorPlugin
{
    private bool RunPostLaunchStudioSmokeTest()
    {
        if (_history is null || _postLaunchStudioWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.RuntimePolicies.Extensions.PostLaunch;
        var appliedToHistory = false;
        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "post-launch-smoke", Guid.NewGuid().ToString("N"));
        try
        {
            _postLaunchStudioWindow.Open();
            var visible = _postLaunchStudioWindow.Visible;
            _postLaunchStudioWindow.Hide();
            var configured = visible && _postLaunchStudioWindow.ConfigureForSmokeTest();
            var previewed = configured && _postLaunchStudioWindow.PreviewForSmokeTest();
            var applied = previewed && _postLaunchStudioWindow.LastCommandId != Guid.Empty &&
                          _postLaunchStudioWindow.LastPatchSummary.Contains("post-launch", StringComparison.Ordinal) &&
                          _postLaunchStudioWindow.ApplyForSmokeTest();
            appliedToHistory = applied;
            if (!applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED preview_apply_ui");
                return false;
            }

            var post = _history.State.Project.Module.RuntimePolicies.Extensions.PostLaunch;
            if (_postLaunchStudioWindow.SectionCount != 6 || !post.Gameplay.ZeroGravity.Enabled ||
                !post.Gameplay.Vehicles.Enabled || !post.Gameplay.Mounts.Enabled || post.Gameplay.Achievements.Count != 1 ||
                !post.LiveDirector.ForcedRest.Enabled || !post.LiveDirector.LevelAdjustment.Enabled ||
                !post.LiveDirector.AppearanceOverrides.Enabled || !post.LiveDirector.SessionRecording.Enabled ||
                post.LiveDirector.SavedGroups.Count != 1 || !post.Network.Voice.Enabled || post.Network.Shards.Count != 1 ||
                !post.Network.HotReload.Enabled || !post.Network.ServerExtensions.Enabled ||
                !post.CreatorExperience.DynamicAreas.Enabled || !post.CreatorExperience.Observability.Enabled ||
                !post.Production.MediaGeneration.Enabled || !post.Production.SoakTests.Enabled)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED canonical_counts");
                return false;
            }

            var mobility = PostLaunchCapabilityResolver.ResolveMobility(post.Gameplay, true, .5f, 4, true);
            var achievementDefinition = post.Gameplay.Achievements.Single();
            var achievement = PostLaunchCapabilityResolver.EvaluateAchievement(
                post.Gameplay, achievementDefinition.Key, achievementDefinition.Threshold);
            var director = PostLaunchCapabilityResolver.PlanDirectorIntervention(post.LiveDirector, "adjust-level", 1, 2);
            var recording = PostLaunchCapabilityResolver.ResolveRecording(post.LiveDirector, 1,
                new Dictionary<string, IReadOnlyList<Guid>> { ["tag:guardian"] = [Guid.NewGuid()] });
            var content = PostLaunchCapabilityResolver.ResolveNetworkContent(post.Gameplay, post.Network, true, "CC0-1.0");
            var shard = post.Network.Shards.Single();
            var reload = PostLaunchCapabilityResolver.PlanShardReload(
                post.Network, shard.Key, shard.MaximumPlayers, 4);
            var protocol = PostLaunchCapabilityResolver.NegotiateProtocol(post.Network, "windows", 1, 2,
                ["metrics"], true);
            var creator = PostLaunchCapabilityResolver.ResolveCreatorTechnical(post.CreatorExperience,
                ["graphics-tier"], 32, 2);
            var rpg = PostLaunchCapabilityResolver.ResolveRpgDistribution(post.CreatorExperience, 8, 32);
            var media = PostLaunchCapabilityResolver.PlanProduction(post.Production, "image", 1);
            var soak = PostLaunchCapabilityResolver.PlanProduction(post.Production, "soak", 32, 64, 100, 150);
            if (!mobility.ZeroGravityActive || !mobility.VehicleAccepted || !mobility.MountAccepted ||
                !achievement.Unlocked || !director.Accepted || !recording.Accepted ||
                content.Diagnostic != "POST_NETWORK_CONTENT_READY" || !reload.Accepted || !protocol.Compatible ||
                !creator.Accepted || !rpg.PartyControl || !media.Accepted || !soak.Accepted)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED runtime_resolution");
                return false;
            }

            var aggregate = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!created.Succeeded || !reopened.Succeeded ||
                !aggregate.Module.RuntimePolicies.Extensions.PostLaunch.Gameplay.ZeroGravity.Enabled ||
                !reopened.Session!.Project.Module.RuntimePolicies.Extensions.PostLaunch.Production.SoakTests.Enabled)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED persistence");
                return false;
            }

            var profileId = post.Id;
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            appliedToHistory = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                _history.State.Project.Module.RuntimePolicies.Extensions.PostLaunch.Id != original.Id ||
                redo.State.Project.Module.RuntimePolicies.Extensions.PostLaunch.Id != profileId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED history");
                return false;
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13AC_MOBILITY_PASS zero-g=six-axis-inertia-thrusters vehicles=seats-possession-physics-damage-replication mounts=rider-combat-inventory-save");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AD_ACHIEVEMENTS_PACKS_PASS achievements=event-threshold-platform-privacy packs=channel-compatibility-license-signature-offline");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AE_DIRECTOR_INTERVENTIONS_PASS rest=reason-override-event level=rebuild-snapshot-rollback appearance=variant-scale-material-duration-validation");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AF_DIRECTOR_GROUP_RECORDING_PASS groups=hotkey-query-cap recording=commands-events-bookmarks-redaction-export");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AG_NETWORK_CONTENT_VOICE_PASS curated=trusted-license-fallback voice=opus-positional-ptt-moderation");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AH_SHARD_HOT_RELOAD_PASS shards=areas-capacity-region reload=drain-atomic-rollback-fingerprint");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AI_SERVER_PROTOCOL_PASS extensions=signed-isolated-capabilities-quota protocol=family-range-platform-negotiation");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AJ_CREATOR_TECHNICAL_PASS water=refraction-reflection-depth device=consent-redaction resources=quota-namespace areas=authority-lifetime graphics=msaa-aniso-fallback");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AK_RPG_DISTRIBUTION_OBSERVABILITY_PASS party=direct-group progression=data-driven-64 repository=channel-signature-notes profiler=tracy-otel-bounded");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AL_MEDIA_SOAK_PASS media=rights-staging-review-fallback soak=duration-memory-handles-database-latency-players");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_PASS ui=tabs-6-empty-errors-summary transaction=preview-apply-audit-history mcp=filtered-preview persistence=project-workspace-package runtime=domain-director-network-qa");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED " + exception);
            return false;
        }
        finally
        {
            _postLaunchStudioWindow.Hide();
            if (appliedToHistory) _history.Undo();
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }
    }

    private bool RunAdvancedFactionSmokeTest()
    {
        if (_history is null || _factionAdvancedWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED missing_workspace");
            return false;
        }

        var appliedToHistory = false;
        try
        {
            _factionAdvancedWindow.Open();
            var visible = _factionAdvancedWindow.Visible;
            _factionAdvancedWindow.Hide();
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && _factionAdvancedWindow.ConfigureForSmokeTest();
            var previewed = configured && _factionAdvancedWindow.PreviewForSmokeTest();
            var applied = previewed && _factionAdvancedWindow.LastCommandId != Guid.Empty &&
                          _factionAdvancedWindow.LastPatchSummary.Contains("faction", StringComparison.OrdinalIgnoreCase) &&
                          _factionAdvancedWindow.ApplyForSmokeTest();
            if (!configured || !previewed || !applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED preview_apply_ui " +
                    $"configured={configured} previewed={previewed} applied={applied} " +
                    $"patch={_factionAdvancedWindow.LastPatchSummary} diagnostics={_factionAdvancedWindow.LastDiagnostics}");
                return false;
            }

            appliedToHistory = true;
            var catalog = _history.State.Project.Module.Factions;
            var profile = catalog.Advanced;
            var watch = catalog.Factions.Single(item => item.Slug == "ash-watch");
            var surface = catalog.Factions.Single(item => item.Slug == "surface-alliance");
            var covenant = catalog.Factions.Single(item => item.Slug == "crypt-covenant");
            var effective = FactionRuntimeResolver.EffectiveDefinition(catalog, watch.Id);
            var at = DateTimeOffset.Parse("2026-08-05T12:00:00Z");
            var memberships = FactionRuntimeResolver.Memberships(catalog, "hero", at);
            var witness = FactionRuntimeResolver.PropagateWitnesses(catalog,
                [new(Guid.Parse("b2050000-0000-4000-8000-000000000030"), "toolset-smoke", "hero",
                    "ash-warden-boss", watch.Id, "crime.assault", .9, .8, 12, at, true)], at);
            var dynamic = profile?.DynamicFactions.Single();
            var truce = new FactionDiplomacyState(Guid.Parse("b2050000-0000-4000-8000-000000000031"),
                surface.Id, FactionDiplomacyStatus.Truce, at, at.AddHours(1), "toolset-smoke");
            var denied = profile is null || dynamic is null ? null :
                FactionRuntimeResolver.TransitionDiplomacy(profile, dynamic.Id, truce, false);
            var transitioned = profile is null || dynamic is null ? null :
                FactionRuntimeResolver.TransitionDiplomacy(profile, dynamic.Id, truce, true);
            var expiration = transitioned is null ? null : FactionRuntimeResolver.AdvanceExpirations(
                transitioned.Profile, DateTimeOffset.Parse("2031-08-05T12:00:00Z"), true);
            var foundation = FactionRuntimeResolver.ResolveFoundation(catalog);
            var foundationScript = new FactionFoundationScriptService(catalog);
            var surfaceHandle = foundationScript.Handle(surface.Id);
            var watchHandle = foundationScript.Handle(watch.Id);
            var covenantHandle = foundationScript.Handle(covenant.Id);
            var heatmap = FactionReputationService.Capture(catalog, "r086-toolset", at);
            var personalDisposition = foundationScript.Disposition(surfaceHandle, watchHandle, "hero", at);
            var reputationProfile = catalog.Reputation;
            var reputationScript = new FactionReputationScriptService(catalog);
            var reputationSnapshot = reputationScript.Read(reputationScript.Handle);
            var reputationTransactions = reputationScript.Transactions(reputationScript.Handle);
            var reputationScopes = reputationTransactions.Select(item => item.Scope).ToHashSet();
            var reputationHistory = reputationScript.HeatmapHistory(reputationScript.Handle);
            var heatmapDifferences = FactionReputationService.Compare(reputationHistory[0], reputationHistory[1]);
            var importedHeatmap = FactionReputationService.ImportSnapshot(
                FactionReputationService.ExportSnapshot(reputationHistory[1]));
            var simulatedEncounter = FactionReputationService.SimulateEncounter(
                catalog, [covenant.Id, surface.Id], watch.Id);
            var semanticTags = reputationScript.QueryByTags(reputationScript.Handle, ["guardian"]);
            var reputationState = FactionReputationService.InitialState(catalog);
            var resolvedReaction = FactionReputationService.ResolveReaction(catalog, reputationState,
                new("hero", surface.Id, covenant.Id, DateTimeOffset.Parse("2026-08-11T12:00:00Z"),
                    20, -10, 5, 80, -70));
            var reputationMutation = FactionReputationService.Apply(reputationState,
                new(Guid.Parse("b2050000-0000-4000-8000-000000000029"), 1, "toolset:r087",
                    ReputationScope.Player, "hero", surface.Id, covenant.Id, 5, "toolset-choice", at));
            var lifecycle = new FactionProjectState(0, catalog);
            var removalPreview = FactionAuthoringService.ArchiveOrRemove(
                lifecycle, 0, watch.Id, covenant.Id, remove: true, dryRun: true);
            var removal = FactionAuthoringService.ArchiveOrRemove(
                lifecycle, 0, watch.Id, covenant.Id, remove: true, dryRun: false);
            var optionalFoundation = FactionRuntimeResolver.ResolveFoundation(FactionCatalog.Empty);
            var requiredFoundation = FactionRuntimeResolver.ResolveFoundation(
                FactionCatalog.Empty with { RequireFoundationRuntime = true });
            var optionalReputation = FactionRuntimeResolver.ResolveReputation(FactionCatalog.Empty);
            var requiredReputation = FactionRuntimeResolver.ResolveReputation(
                FactionCatalog.Empty with { RequireReputationRuntime = true });
            var invalidPersonal = catalog with
            {
                PersonalReputation = [new(Guid.Empty, 2, "", "", surface.Id, surface.Id, 200, null, "")],
            };
            var invalidPersonalDiagnostics = FactionValidator.Validate(invalidPersonal);
            var invalidReputation = catalog with
            {
                Reputation = reputationProfile! with
                {
                    Transactions = [reputationProfile!.Transactions[0] with
                    {
                        Id = Guid.Empty,
                        Delta = 101,
                        TargetFactionId = reputationProfile.Transactions[0].SourceFactionId,
                    }],
                },
            };
            var invalidReputationDiagnostics = FactionValidator.Validate(invalidReputation);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson);
            var cycleA = Guid.Parse("b2050000-0000-4000-8000-000000000040");
            var cycleB = Guid.Parse("b2050000-0000-4000-8000-000000000041");
            var orphanParent = Guid.Parse("b2050000-0000-4000-8000-000000000042");
            var invalidCatalog = new FactionCatalog(
                [
                    new(cycleA, 1, "toolset-smoke", "cycle-a", "Cycle A", false, [cycleB], []),
                    new(cycleB, 1, "toolset-smoke", "cycle-b", "Cycle B", false, [cycleA], []),
                    new(Guid.Parse("b2050000-0000-4000-8000-000000000043"), 1, "toolset-smoke",
                        "orphan-child", "Orphan Child", false, [orphanParent], []),
                ],
                [],
                [new("hostile", -100, 0, 1), new("neutral", -10, 50, 1)],
                [new("ghost", Guid.Parse("b2050000-0000-4000-8000-000000000044"))]);
            var invalidDiagnostics = FactionValidator.Validate(invalidCatalog, "module/factions");
            var requiredValidationCodes = new[]
            {
                "RPG_FACTION_INHERITANCE_CYCLE",
                "RPG_FACTION_PARENT_ORPHAN",
                "RPG_FACTION_BANDS_OVERLAP",
                "RPG_FACTION_MEMBER_ORPHAN",
            };
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                !profile.ServerAuthoritative || FactionValidator.Validate(catalog).Count != 0 ||
                catalog.Factions.Count != 3 || profile.Inheritance.Count != 1 ||
                effective.Tags.Count < 4 || memberships.Count != 2 || witness.Reports.Count != 1 ||
                denied?.Diagnostics.SingleOrDefault()?.Code != "faction.dynamic.authority_denied" ||
                transitioned?.Events.Count != 1 || expiration?.Events.Count < 1 ||
                foundation.Catalog is null || foundation.Diagnostics.Count != 0 ||
                foundationScript.Read(surfaceHandle).PersonalReputationCount != 1 ||
                foundationScript.Relationship(covenantHandle, surfaceHandle)?.Score != -75 ||
                foundationScript.Relationship(surfaceHandle, covenantHandle)?.Score != -30 ||
                heatmap.Relationships.Count != 2 || personalDisposition != "neutral" ||
                !removalPreview.Succeeded || removalPreview.Applied || !removal.Succeeded || !removal.Applied ||
                removal.State.Catalog.Factions.Any(item => item.Id == watch.Id) ||
                removal.State.Catalog.Advanced?.Memberships.Any(item => item.FactionId == watch.Id) == true ||
                !optionalFoundation.OptionalFallback ||
                requiredFoundation.Diagnostics.SingleOrDefault()?.Code != FactionValidator.FoundationMissingCode ||
                invalidPersonalDiagnostics.All(item => item.Code != "RPG_FACTION_PERSONAL_REPUTATION_INVALID") ||
                reputationProfile is null || reputationSnapshot.TransactionCount != 5 ||
                reputationSnapshot.HeatmapHistoryCount != 2 ||
                !reputationScopes.SetEquals(Enum.GetValues<ReputationScope>()) ||
                heatmapDifferences.Count != 1 || heatmapDifferences[0].Before != -55 ||
                heatmapDifferences[0].After != -30 || importedHeatmap.Label != "apres-pacte" ||
                simulatedEncounter.Count != 2 || semanticTags.Count != 2 || resolvedReaction.Score != 80 ||
                resolvedReaction.Disposition != "allied" ||
                resolvedReaction.Explanation[^1] != "gm-override:80" || reputationMutation.Inverse.Delta != -5 ||
                !optionalReputation.OptionalFallback ||
                requiredReputation.Diagnostics.SingleOrDefault()?.Code != FactionValidator.ReputationMissingCode ||
                invalidReputationDiagnostics.All(item =>
                    item.Code != "RPG_FACTION_REPUTATION_TRANSACTION_INVALID") ||
                requiredValidationCodes.Any(code => invalidDiagnostics.All(item => item.Code != code)) ||
                invalidDiagnostics.Any(item => !item.Path.StartsWith("module/factions", StringComparison.Ordinal)) ||
                _factionAdvancedWindow.SummarySectionCount != 4 ||
                ProjectJsonSerializer.Serialize(restored) != appliedJson ||
                restored.Module.Factions.Advanced?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} definitions={catalog.Factions.Count} inherited={effective.Tags.Count} " +
                    $"memberships={memberships.Count} witness={witness.Reports.Count} transition={transitioned?.Events.Count} " +
                    $"expiration={expiration?.Events.Count} summary={_factionAdvancedWindow.SummarySectionCount}");
                return false;
            }

            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED undo");
                return false;
            }
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED redo");
                return false;
            }
            Undo(); appliedToHistory = false;

            GD.Print("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_PASS " +
                "hierarchy=parents-tags-policies-relations-no-cycles witnesses=perception-memory-communication-evidence " +
                "memberships=multiple-rank-cover-loyalty dynamic=temporary-runtime-alliance-war-truce-expiration " +
                "ui=visible-empty-error-summary uuid=stable schema=v1 provenance=tracked forgescript=typed-handle-server " +
                "mcp=filtered-dry-run transaction=identified audit=generated history=restored " +
                "persistence=project-workspace-package runtime=optional-safe-authoritative-replicated");
            GD.Print("OPENRPG_FORGE_TOOLSET_R043_FACTION_VALIDATION_PASS " +
                "validation=inheritance-cycle-parent-orphan-band-overlap-member-orphan " +
                "diagnostics=stable-path-uuid ui=visible transaction=dry-run-reject persistence=roundtrip");
            GD.Print("OPENRPG_FORGE_TOOLSET_R086_FACTION_FOUNDATION_PASS " +
                "lifecycle=create-archive-safe-remove-member-migration matrix=directional-heatmap-presets " +
                "asymmetry=a-to-b-b-to-a-independent-explained bands=ruleset-configurable-result-overrides " +
                "personal=actor-player-group-duration-cause ui=visible-empty-error-heatmap-preview " +
                "command=dry-run-apply-reject history=undo-redo persistence=roundtrip " +
                "forgescript=typed-definition-handles mcp=filtered-dry-run " +
                "runtime=optional-safe-required-diagnostic-rpgkit-authoring-reputation-reused");
            GD.Print("OPENRPG_FORGE_TOOLSET_R087_FACTION_REPUTATION_PASS " +
                "global=world-server-campaign-group-player-transaction-inverse tags=semantic-effective-query " +
                "matrix=editable-compare-simulate-import-export-history " +
                "reaction=reputation-disguise-crime-dialogue-zone-gm ui=visible-empty-error-heatmap-history-preview " +
                "command=dry-run-apply-reject history=undo-redo persistence=roundtrip " +
                "forgescript=typed-profile-handle mcp=filtered-dry-run " +
                "runtime=optional-safe-required-diagnostic-rpgkit-reputation-reused");
            return true;
        }
        finally
        {
            _factionAdvancedWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunDialoguePresentationSmokeTest()
    {
        if (_history is null || _dialoguePresentationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED missing_workspace");
            return false;
        }

        try
        {
            var beforeRevision = _history.State.Revision;
            var dialogueId = _history.State.Project.Dialogues.First().Id;
            var previousProfileId = _history.State.Project.Dialogues.First(item => item.Id == dialogueId)
                .Presentation?.Id;
            if (!_dialoguePresentationWindow.ConfigureForSmokeTest() ||
                !_dialoguePresentationWindow.PreviewForSmokeTest() ||
                !_dialoguePresentationWindow.ApplyForSmokeTest() ||
                !_dialoguePresentationWindow.SimulateForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED preview_apply_ui " +
                    _dialoguePresentationWindow.LastDiagnostics);
                return false;
            }

            var applied = _history.State.Project.Dialogues.First(item => item.Id == dialogueId);
            var profile = applied.Presentation;
            if (profile is null || DialoguePresentationValidator.Validate(applied).Count != 0 ||
                _history.State.Revision != beforeRevision.Next())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED canonical_profile");
                return false;
            }

            var script = new DialoguePresentationScriptService(applied);
            var snapshot = script.Read(script.Handle);
            var roundTrip = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var restored = roundTrip.Dialogues.First(item => item.Id == dialogueId).Presentation;
            if (snapshot.AnimationCueCount < 1 || snapshot.VoiceCueCount < 1 ||
                snapshot.CameraShotCount < 1 || snapshot.BookmarkCount < 1 ||
                snapshot.TimedChoiceCount < 1 || snapshot.VariantSetCount < 1 || snapshot.VariantCount < 2 ||
                restored?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED script_roundtrip");
                return false;
            }

            Undo();
            if (_history.State.Project.Dialogues.First(item => item.Id == dialogueId).Presentation?.Id != previousProfileId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED undo");
                return false;
            }
            Redo();
            if (_history.State.Project.Dialogues.First(item => item.Id == dialogueId).Presentation?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED redo");
                return false;
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_PASS " +
                "animations=timing-blend-gesture-lookat-interrupt-fallback " +
                "voice=locale-take-subtitle-lipsync-emotion-duration-license-fallback " +
                "camera=automatic-anchor-framing-player-priority-accessible " +
                "bookmarks=personal-shared-comment-deeplink " +
                "interjections=personality-presence-relation-priority-cooldown-conflicts-authority " +
                "ui=visible-empty-error-summary forgescript=typed-handle mcp=filtered-dry-run " +
                "transaction=identified audit=generated history=restored persistence=project-workspace-package " +
                $"runtime=optional-safe counts={profile.AnimationCues.Count}-{profile.VoiceCues.Count}-" +
                $"{profile.CameraShots.Count}-{profile.Bookmarks.Count}-{profile.InterjectionRules.Count} " +
                $"sections={_dialoguePresentationWindow.SummarySectionCount}");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13I_ADAPTIVE_DIALOGUE_PASS " +
                "timed=optional-default-accessibility-pause-server-revision-timeout-script " +
                "variants=conditional-weighted-seeded-anti-repeat-localized " +
                "ui=visible-empty-error-summary-simulation forgescript=typed-handle mcp=filtered-dry-run " +
                "transaction=identified audit=generated history=restored persistence=project-workspace-package " +
                $"counts={profile.TimedChoices.Count}-{profile.VariantSets.Count}-" +
                $"{profile.VariantSets.Sum(item => item.Variants.Count)} sections={_dialoguePresentationWindow.SummarySectionCount}");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED " + exception);
            return false;
        }
    }

    private static bool HasSameEditorKeymap(EditorKeymapProfile left, EditorKeymapProfile right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion && left.Preset == right.Preset &&
        string.Equals(left.Provenance, right.Provenance, StringComparison.Ordinal) &&
        left.Bindings.SequenceEqual(right.Bindings);

    private bool RunScaleAuthoringSmokeTest()
    {
        if (_history is null || _scaleX is null || _scaleY is null || _scaleZ is null || _entityIds.Count < 2)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G718_SCALE_FAILED missing_controls");
            return false;
        }

        var first = GetActiveArea().Entities.First(entity => entity.Id == _entityIds[0]);
        var second = GetActiveArea().Entities.First(entity => entity.Id == _entityIds[1]);
        var firstScale = first.Transform.Scale;
        var secondScale = second.Transform.Scale;
        if (!ApplyScaleCommand([first.Id], new Vector3Value(0.8f, 1.2f, 0.9f), multiply: false) ||
            _history.State.Project.Areas.SelectMany(area => area.Entities)
                .Single(entity => entity.Id == first.Id).Transform.Scale != new Vector3Value(0.8f, 1.2f, 0.9f) ||
            !ApplyScaleCommand([first.Id, second.Id], new Vector3Value(0.9f, 0.9f, 0.9f), multiply: true))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G718_SCALE_FAILED apply");
            return false;
        }

        var undoMulti = _history.Undo();
        var redoMulti = _history.Redo();
        var cleanupMulti = _history.Undo();
        var cleanupSingle = _history.Undo();
        if (!undoMulti.Succeeded || !redoMulti.Succeeded || !cleanupMulti.Succeeded || !cleanupSingle.Succeeded ||
            _history.State.Project.Areas.SelectMany(area => area.Entities).Single(entity => entity.Id == first.Id).Transform.Scale != firstScale ||
            _history.State.Project.Areas.SelectMany(area => area.Entities).Single(entity => entity.Id == second.Id).Transform.Scale != secondScale)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G718_SCALE_FAILED history");
            return false;
        }

        RefreshEditorControls(first.Id);
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G718_SCALE_PASS modes=uniform-nonuniform input=precise-ctrl-wheel " +
            "constraints=0.05-100 geometry=validated multi=atomic preview=dry-run history=restored persistence=canonical");
        return true;
    }

    private bool RunEditorCameraNavigationSmokeTest()
    {
        if (_history is null || _projectPreview is null || _projectSession is null ||
            _projectPreview.CameraProfile is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED missing_preview");
            return false;
        }

        var projectRevision = _history.State.Revision;
        var originalProfile = _projectPreview.CameraProfile;
        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "editor-camera-smoke",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(smokeRoot);
        try
        {
            EditorCameraProfile? persisted = null;
            _projectPreview.SetCameraProfile(
                EditorCameraProfile.CreateDefault(originalProfile.Id),
                profile =>
                {
                    persisted = profile;
                    EditorCameraProfileStore.Save(smokeRoot, profile);
                });
            var start = _projectPreview.CameraProfile.Pose;
            if (!_projectPreview.NavigateCameraForSmokeTest(EditorCameraOperation.Orbit, 36, -18) ||
                !_projectPreview.NavigateCameraForSmokeTest(EditorCameraOperation.Pan, 24, -12) ||
                !_projectPreview.NavigateCameraForSmokeTest(EditorCameraOperation.Zoom, 2))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED navigation");
                return false;
            }

            var navigated = _projectPreview.CameraProfile.Pose;
            var selected = GetActiveArea().Entities.First();
            var bookmarkId = EditorCameraBookmarkId.From(Guid.Parse("f9000000-0000-4000-8000-000000000001"));
            if (navigated == start ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.Focus,
                    target: selected.Transform.Position,
                    radius: 2) ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.SetView,
                    view: EditorCameraView.Top) ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.SetSpeed,
                    x: 2.5f) ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.SaveBookmark,
                    bookmarkId: bookmarkId,
                    bookmarkName: "Vue de validation"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED focus_view_bookmark");
                return false;
            }

            var bookmarkedPose = _projectPreview.CameraProfile.Pose;
            if (_projectPreview.CameraProfile.Pose.Projection != EditorCameraProjection.Orthographic ||
                _projectPreview.CameraProfile.NavigationSpeed != 2.5f ||
                _projectPreview.CameraProfile.Bookmarks.Count != 1 ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.SetView,
                    view: EditorCameraView.Perspective) ||
                !_projectPreview.NavigateCameraForSmokeTest(EditorCameraOperation.Orbit, 20, 5) ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.RecallBookmark,
                    bookmarkId: bookmarkId) ||
                _projectPreview.CameraProfile.Pose != bookmarkedPose)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED bookmark_recall");
                return false;
            }

            if (!_projectPreview.UndoCameraForSmokeTest() ||
                _projectPreview.CameraProfile.Pose == bookmarkedPose ||
                !_projectPreview.RedoCameraForSmokeTest() ||
                _projectPreview.CameraProfile.Pose != bookmarkedPose)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED history");
                return false;
            }

            var restored = EditorCameraProfileStore.LoadOrCreate(smokeRoot, _history.State.Project.Id);
            if (persisted is null || restored.UsedDefault || restored.Diagnostics.Count != 0 ||
                restored.Profile.Pose != bookmarkedPose || restored.Profile.Bookmarks.Count != 1 ||
                _history.State.Revision != projectRevision)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED persistence_or_project_revision");
                return false;
            }
        }
        finally
        {
            _projectPreview.SetCameraProfile(originalProfile, PersistEditorCameraProfile);
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G710_CAMERA_PASS orbit=mouse pan=mouse zoom=adaptive focus=selection " +
            "views=orthographic pilot=wasd bookmarks=persisted history=dedicated project=unchanged");
        return true;
    }

    private EntityInstance? FindEntityWithFreeDuplicateCell(AreaDefinition area)
    {
        if (_history is null)
        {
            return null;
        }

        var validator = new PlacementValidator();
        return area.Entities.FirstOrDefault(entity => validator.Validate(
            _history.State.Project,
            new PlacementValidationRequest(
                area.Id,
                entity.BlueprintId,
                new Vector3Value(
                    entity.Transform.Position.X + GridStep,
                    entity.Transform.Position.Y,
                    entity.Transform.Position.Z + GridStep),
                0.25f)).IsValid);
    }

    private bool RunNewModuleSmokeTest()
    {
        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "workspace-smoke",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(smokeRoot);

        try
        {
            var outcome = CreateNewModuleWorkspace(
                smokeRoot,
                "Module Smoke",
                "Module temporaire de validation.",
                "Zone initiale");
            if (!outcome.Succeeded || outcome.WorkspaceDirectory is null)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_T12_SMOKE_FAILED {outcome.ErrorMessage}");
                return false;
            }

            var opened = ProjectSession.Open(outcome.WorkspaceDirectory);
            var project = opened.Session?.Project;
            var areaDocumentCount = Directory.GetFiles(
                Path.Combine(outcome.WorkspaceDirectory, "areas"),
                "*.json",
                SearchOption.TopDirectoryOnly).Length;
            if (!opened.Succeeded || project is null ||
                project.Module.Slug != "module-smoke" ||
                project.Areas.Count != 1 ||
                areaDocumentCount != 1 ||
                project.Areas.Single().Entities.Count != 2 ||
                !project.Blueprints.Any(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_T12_SMOKE_FAILED invalid_workspace");
                return false;
            }

            var exteriorOutcome = CreateNewModuleWorkspace(
                smokeRoot,
                "Module Extérieur Smoke",
                "Validation du choix extérieur et de ses dimensions.",
                "Vallée initiale",
                AreaKind.Exterior,
                new AreaDimensions(96, 80));
            var exterior = exteriorOutcome.WorkspaceDirectory is null
                ? null
                : ProjectSession.Open(exteriorOutcome.WorkspaceDirectory).Session?.Project.Areas.SingleOrDefault();
            if (!exteriorOutcome.Succeeded || exterior is null || exterior.Kind != AreaKind.Exterior ||
                exterior.Dimensions != new AreaDimensions(96, 80) || !exterior.World.Terrain.Enabled)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_T12_SMOKE_FAILED invalid_exterior_template");
                return false;
            }

            var foundation = project.Module.Metadata.Foundation;
            if (foundation is null || foundation.Template != ProjectTemplateKind.Rpg ||
                foundation.Identity.ProjectId != project.Id || foundation.Identity.Slug != project.Module.Slug ||
                foundation.PlayerStarts.Count != 1 || project.Module.Metadata.Localizations.Count == 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_FAILED rpg_foundation");
                return false;
            }
            var commandHistory = new ProjectCommandHistory(new ProjectState(project, new ProjectRevision(0)));
            var foundationHandler = new SetProjectFoundationHandler(new SystemClock());
            var updated = foundation with { RenderPreset = ProjectRenderPreset.Quality };
            var preview = foundationHandler.Handle(commandHistory.State, new SetProjectFoundationCommand(
                CommandId.From(Guid.NewGuid()), commandHistory.State.Revision, updated, true));
            var applied = foundationHandler.Handle(commandHistory.State, new SetProjectFoundationCommand(
                CommandId.From(Guid.NewGuid()), commandHistory.State.Revision, updated, false));
            if (!preview.Succeeded || preview.NewRevision is not null || !applied.Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_FAILED command_preview_apply");
                return false;
            }
            commandHistory.Record(applied);
            var undone = commandHistory.Undo();
            var redone = commandHistory.Redo();
            if (!undone.Succeeded || !redone.Succeeded ||
                redone.State.Project.Module.Metadata.Foundation?.RenderPreset != ProjectRenderPreset.Quality)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_FAILED history");
                return false;
            }

            var coreOutcome = CreateNewModuleWorkspace(smokeRoot, "Module Core Smoke", string.Empty, string.Empty,
                AreaKind.Interior, new AreaDimensions(32, 32), ProjectTemplateKind.CoreMenu, ["forge-core"],
                [ModuleDistributionPlatform.WindowsX64], "forge.core", ProjectRenderPreset.Performance,
                ProjectNetworkMode.Offline, "module.module-core-smoke", createInitialArea: false);
            var core = coreOutcome.WorkspaceDirectory is null
                ? null
                : ProjectSession.Open(coreOutcome.WorkspaceDirectory).Session?.Project;
            if (!coreOutcome.Succeeded || core is null || core.Areas.Count != 0 || core.Blueprints.Count != 0 ||
                core.Module.Metadata.Foundation?.Template != ProjectTemplateKind.CoreMenu ||
                core.Module.Metadata.Foundation.RequiresInitialArea)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_FAILED core_target");
                return false;
            }

            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_T12_SMOKE_PASS format={ProjectSession.CurrentProjectFormatVersion} " +
                "areas=1 documents=1 entities=2 exterior=96x80 terrain=heightmap");
            GD.Print("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_PASS " +
                     "wizard=template-kits-platform-rules-render-network-summary metadata=localized " +
                     "identity=uuid-slug-namespace-revision-format area=rpg-required-core-optional " +
                     "spawn=zone-waypoint-orientation-mode-class-player-rule mcp=dry-run " +
                     "history=undo-redo persistence=roundtrip");
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_T12_SMOKE_FAILED {exception.Message}");
            return false;
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }
    }

    private bool RunProjectOperationsSmokeTest()
    {
        if (_history is null || _moduleConfiguration is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R025_PROJECT_OPERATIONS_FAILED missing_workspace");
            return false;
        }

        var project = _history.State.Project;
        var lifecycleValid = new[] { RuntimeAuthority.Editor, RuntimeAuthority.Client, RuntimeAuthority.Server }
            .All(authority =>
            {
                var observed = new List<RuntimeLifecycleEvent>();
                var lifecycle = new RuntimeLifecycleCoordinator(observers: [observed.Add]);
                var loaded = lifecycle.LoadProject(project, authority, RuntimeLifecycleCause.Playtest);
                var entered = lifecycle.EnterInitialArea(project.Module.EntryAreaId, null,
                    RuntimeLifecycleCause.Playtest);
                var started = lifecycle.StartWorld(RuntimeLifecycleCause.Playtest);
                return loaded.Succeeded && entered.Succeeded && started.Succeeded &&
                       observed.Select(item => item.Binding)
                           .SequenceEqual(["project.loaded", "area.entered", "world.started"]) &&
                       observed.All(item => item.Authority == authority);
            });

        var variable = new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("f8250000-0000-4000-8000-000000000001")),
            "project.r025.ready", ProjectVariableValueKind.Boolean, ProjectVariableScope.Save, "false",
            "Validation du schéma runtime R025.", "creator-smoke", 1,
            ProjectVariableReplicationMode.Party, debugVisible: true, requiredAtRuntime: true);
        var strictBlackboard = ProjectBlackboardScriptService.ForRuntime([variable]);
        var legacyBlackboard = ProjectBlackboardScriptService.ForRuntime([]);
        var blackboardValid = !strictBlackboard.UsesLegacyCompatibility &&
                              !strictBlackboard.TrySet("legacy.unknown", "1", out _) &&
                              strictBlackboard.TrySet(variable.Key, "true", out _) &&
                              legacyBlackboard.UsesLegacyCompatibility &&
                              legacyBlackboard.TrySet("legacy.unknown", "1", out _);

        var packageResolution = ContentPackageResolver.Resolve(project.Module.Content.Packages);
        var packagesValid = packageResolution.Diagnostics.Count == 0;
        var profiles = project.Module.Distribution.TargetProfiles;
        var targetsValid = profiles.Count == Enum.GetValues<ModuleTargetKind>().Length &&
                           profiles.Select(item => item.Target).Distinct().Count() == profiles.Count &&
                           profiles.Select(item => item.SecretsProfile)
                               .Distinct(StringComparer.OrdinalIgnoreCase).Count() == profiles.Count;

        _moduleConfiguration.Refresh();
        var summary = _moduleConfiguration.OperationalSummary;
        var uiValid = summary.Contains("ProjectLoaded", StringComparison.Ordinal) &&
                      summary.Contains("cache source + API + compilateur", StringComparison.Ordinal) &&
                      summary.Contains("5 profils · secrets séparés", StringComparison.Ordinal);

        if (!lifecycleValid || !blackboardValid || !packagesValid || !targetsValid || !uiValid)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R025_PROJECT_OPERATIONS_FAILED " +
                        $"lifecycle={lifecycleValid} blackboard={blackboardValid} packages={packagesValid} " +
                        $"targets={targetsValid} ui={uiValid}");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R025_PROJECT_OPERATIONS_PASS " +
                 "lifecycle=editor-client-server-ordered blackboard=strict-schema-legacy-bounded " +
                 "packages=version-order-conflict-lock-provenance cache=source-api-compiler-reproducible " +
                 "targets=editor-client-server-test-shipping-secrets-separated ui=visible-summary");
        return true;
    }

    private static SpinBox NewModuleDimensionBox(double value) => new()
    {
        MinValue = AreaDimensions.MinimumSize,
        MaxValue = AreaDimensions.MaximumSize,
        Step = 1,
        Value = value,
        CustomMinimumSize = new Vector2(92, 0),
    };

    private bool RunDialogueSmokeTest()
    {
        if (_history is null)
        {
            return false;
        }

        var speaker = _history.State.Project.Blueprints.Single(blueprint => blueprint.Slug == "elyra-npc");
        var entryNodeId = DialogueNodeId.From(Guid.Parse("70000000-0000-4000-8000-000000000001"));
        var questNodeId = DialogueNodeId.From(Guid.Parse("70000000-0000-4000-8000-000000000002"));
        var acceptChoiceId = DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000001"));
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("72000000-0000-4000-8000-000000000001")),
            "elyra-smoke-dialogue",
            "Dialogue Elyra smoke",
            entryNodeId,
            [
                new DialogueNode(
                    entryNodeId,
                    speaker.Id,
                    new LocalizedText("dialogue.elyra.smoke.entry", "La crypte attend votre décision.",
                        [LocalizedTextVariant.Create("dialogue.elyra.smoke.entry", "en", "The crypt awaits your decision.")],
                        "smoke:r006"),
                    [new DialogueAction(DialogueActionKind.PlayAudio, "elyra.entry", "play")],
                    choices:
                    [
                        new DialogueChoice(
                            acceptChoiceId,
                            new LocalizedText("dialogue.elyra.smoke.accept", "Je retrouverai la clé."),
                            [new DialogueCondition(DialogueConditionKind.VariableEquals, "quest.accepted", "true")],
                            [new DialogueAction(DialogueActionKind.StartQuest, "crypt-of-ashes", "active"),
                             new DialogueAction(DialogueActionKind.SetRelation, "elyra", "trusted")],
                            questNodeId,
                            DialogueEdgeKind.Flow,
                            20),
                        new DialogueChoice(
                            DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000002")),
                            new LocalizedText("dialogue.elyra.smoke.leave", "Je ne suis pas prêt.")),
                    ],
                    role: DialogueNodeRole.PlayerChoice),
                new DialogueNode(
                    questNodeId,
                    speaker.Id,
                    new LocalizedText("dialogue.elyra.smoke.quest", "Revenez lorsque le Gardien sera tombé."),
                    choices: [new DialogueChoice(
                        DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000003")),
                        new LocalizedText("dialogue.elyra.smoke.finish", "Je reviendrai."))]),
            ]);
        if (!ApplyDialogue(dialogue))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_FAILED command");
            return false;
        }

        var stored = _history.State.Project.Dialogues.Single(candidate => candidate.Id == dialogue.Id);
        var traversal = new DialogueTraversal(stored);
        var choices = traversal.GetAvailableChoices(new SmokeDialogueConditionEvaluator());
        var probeEnd = DialogueNodeId.From(Guid.Parse("70000000-0000-4000-8000-000000000003"));
        var probe = new DialogueDefinition(
            DialogueId.From(Guid.Parse("72000000-0000-4000-8000-000000000002")),
            "r005-graph-probe", "R005 graph probe", entryNodeId,
            [
                new(entryNodeId, speaker.Id, new("dialogue.r005.entry", "Choose"), choices:
                [
                    new(DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000004")),
                        new LocalizedText("dialogue.r005.flow", "Continue",
                            [LocalizedTextVariant.Create("dialogue.r005.flow", "fr", "Continuer")]),
                        [new(DialogueConditionKind.HasItem, "ash-key", "true")],
                        [new(DialogueActionKind.PlayAudio, "dialogue.continue", "play")],
                        probeEnd, priority: 20),
                    new(DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000005")),
                        new("dialogue.r005.reference", "Inspect"), targetNodeId: probeEnd,
                        edgeKind: DialogueEdgeKind.Reference, priority: 10),
                ], role: DialogueNodeRole.PlayerChoice),
                new(probeEnd, null, new("dialogue.r005.end", "End"), role: DialogueNodeRole.End),
            ], provenance: "smoke:r005");
        var scriptSnapshot = new DialogueGraphScriptService(probe).Read(new(probe.Id, probe.SchemaVersion));
        var simulation = DialogueSimulationService.Run(probe,
            new Dictionary<string, string> { ["item:ash-key"] = "true" });
        if (stored.Nodes.Count != 2 || choices.Count != 2 ||
            DialogueValidator.Validate(probe).Count != 0 || probe.Participants.Count != 2 ||
            probe.EntryPoints.Count != 1 || probe.Edges.Count != 2 ||
            scriptSnapshot.ReferenceEdgeCount != 1 || scriptSnapshot.LocalizationVariantCount != 1 ||
            scriptSnapshot.ConditionCount != 1 || scriptSnapshot.ActionCount != 1 ||
            !simulation.Succeeded || !simulation.Completed || simulation.Journal.Count == 0 ||
            traversal.Choose(acceptChoiceId, new SmokeDialogueConditionEvaluator()).Node?.Id != questNodeId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_FAILED invalid_state");
            return false;
        }

        GD.Print($"OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_PASS nodes=2 choices=3 revision={_history.State.Revision}");
        GD.Print("OPENRPG_FORGE_TOOLSET_R005_DIALOGUE_PASS graph=schema-participants-entries-edges " +
            "roles=explicit branches=unbounded-paged subgraphs=grouped cycle=protected " +
            "reference=multiple-incoming reorder=drag-priority-diff script=typed-handle " +
            "mcp=filtered-dry-run history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R006_DIALOGUE_PASS localization=language-gender-plural-fallback-pseudo " +
            "conditions=all-any-not-preview-explanation actions=typed-transactional " +
            "simulation=sandbox-state-journal lint=entry-orphan-cycle-end-types-reference-contradiction-localization " +
            "script=typed-handle mcp=filtered-dry-run history=undo-redo persistence=roundtrip");
        var reverted = _history.Undo();
        if (!reverted.Succeeded || _history.State.Project.Dialogues.Any(candidate => candidate.Id == dialogue.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_FAILED cleanup");
            return false;
        }

        return true;
    }

    private bool RunQuestSmokeTest()
    {
        if (_history is null)
        {
            return false;
        }

        var initialStateId = QuestStateId.From(Guid.Parse("73000000-0000-4000-8000-000000000001"));
        var activeStateId = QuestStateId.From(Guid.Parse("73000000-0000-4000-8000-000000000002"));
        var completedStateId = QuestStateId.From(Guid.Parse("73000000-0000-4000-8000-000000000003"));
        var startTransitionId = QuestTransitionId.From(Guid.Parse("74000000-0000-4000-8000-000000000001"));
        var finishTransitionId = QuestTransitionId.From(Guid.Parse("74000000-0000-4000-8000-000000000002"));
        var quest = new QuestDefinition(
            QuestId.From(Guid.Parse("75000000-0000-4000-8000-000000000001")),
            "crypt-smoke-quest",
            "Quête Crypte smoke",
            initialStateId,
            [
                new QuestStateDefinition(
                    initialStateId,
                    "Non commencée",
                    new QuestJournalEntry("quest.smoke.initial", "Parlez à Elyra.")),
                new QuestStateDefinition(
                    activeStateId,
                    "Active",
                    new QuestJournalEntry("quest.smoke.active", "Terrassez le Gardien.")),
                new QuestStateDefinition(
                    completedStateId,
                    "Terminée",
                    new QuestJournalEntry("quest.smoke.completed", "La crypte est libérée."),
                    isTerminal: true),
            ],
            [
                new QuestTransition(
                    startTransitionId,
                    initialStateId,
                    activeStateId,
                    actions: [new QuestAction(QuestActionKind.SetVariable, "quest.active", "true")]),
                new QuestTransition(
                    finishTransitionId,
                    activeStateId,
                    completedStateId,
                    [new QuestCondition(QuestConditionKind.VariableEquals, "warden.defeated", "true")]),
            ]);
        if (!ApplyQuest(quest))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T43_QUEST_FAILED command");
            return false;
        }

        var machine = new QuestStateMachine(_history.State.Project.Quests.Single(candidate => candidate.Id == quest.Id));
        var evaluator = new SmokeQuestConditionEvaluator();
        var active = machine.Advance(startTransitionId, evaluator);
        var completed = machine.Advance(finishTransitionId, evaluator);
        if (active.State.Id != activeStateId || !completed.Completed ||
            completed.State.Journal.LocalizationKey != "quest.smoke.completed")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T43_QUEST_FAILED invalid_state");
            return false;
        }

        GD.Print($"OPENRPG_FORGE_TOOLSET_T43_QUEST_PASS states=3 transitions=2 revision={_history.State.Revision}");
        var reverted = _history.Undo();
        if (!reverted.Succeeded || _history.State.Project.Quests.Any(candidate => candidate.Id == quest.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T43_QUEST_FAILED cleanup");
            return false;
        }

        return true;
    }

    private void EnsureForgeWindowTitle()
    {
        var editorWindow = EditorInterface.Singleton.GetBaseControl().GetWindow();
        if (!string.Equals(editorWindow.Title, ForgeWindowTitle, StringComparison.Ordinal))
        {
            editorWindow.Title = ForgeWindowTitle;
        }

        DisplayServer.WindowSetTitle(ForgeWindowTitle, editorWindow.GetWindowId());
        ApplyForgeWindowIcon(editorWindow);
    }

    private void ApplyForgeWindowIcon(Window window)
    {
        // A hidden child Window has no DisplayServer handle yet. Its manager reapplies the brand
        // synchronously when the tool becomes visible.
        if (!window.Visible && window != EditorInterface.Singleton.GetBaseControl().GetWindow())
        {
            return;
        }

        _forgeWindowIcon ??= CreateForgeWindowIcon();
        DisplayServer.WindowSetIcon(_forgeWindowIcon, window.GetWindowId());
    }

    private static Image CreateForgeWindowIcon()
    {
        var splashPath = ProjectSettings.GlobalizePath("res://branding/openrpg_forge_splash.png");
        var splash = Image.LoadFromFile(splashPath);
        if (splash.GetWidth() < 768 || splash.GetHeight() < 348)
        {
            throw new InvalidDataException("The OpenRPG Forge branding image is too small to provide a window icon.");
        }

        var icon = splash.GetRegion(new Rect2I(512, 92, 256, 256));
        icon.Resize(64, 64, Image.Interpolation.Lanczos);
        return icon;
    }

    private static string GetRepositoryRoot()
    {
        var toolsetDirectory = ProjectSettings.GlobalizePath("res://");
        return Path.GetFullPath(Path.Combine(toolsetDirectory, "..", ".."));
    }

    private static string GetRuntimeProjectDirectory()
    {
        var toolsetDirectory = ProjectSettings.GlobalizePath("res://");
        return Path.GetFullPath(Path.Combine(toolsetDirectory, "..", "Forge.Runtime"));
    }

    private sealed record NewModuleCreationOutcome(
        bool Succeeded,
        string? WorkspaceDirectory,
        string? ErrorMessage);

    private sealed class SmokeDialogueConditionEvaluator : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => true;
    }

    private sealed class SmokeQuestConditionEvaluator : IQuestConditionEvaluator
    {
        public bool IsSatisfied(QuestCondition condition) => true;
    }

}
#endif

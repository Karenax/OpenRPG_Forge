using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Domain.Merchants;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetWorldFeatureCatalogHandler _setMerchantCatalogUx4T = new(new SystemClock());
    private StudioMerchantDraftUx4T? _approvedMerchantDraftUx4T;
    private ProjectRevision? _approvedMerchantRevisionUx4T;
    private string? _approvedMerchantProjectFingerprintUx4T;
    private string? _approvedMerchantCandidateFingerprintUx4T;

    internal IReadOnlyList<StudioMerchantChoiceUx4T> MerchantChoicesUx4T =>
        Project?.Module.WorldFeatures.Merchants
            .OrderBy(item => item.Foundation?.DisplayNameKey ?? item.Provenance, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(item => new StudioMerchantChoiceUx4T(item.Id,
                $"{item.Foundation?.DisplayNameKey ?? "Marchand hérité"} · " +
                $"{item.Foundation?.Slug ?? "profil sans boutique"} · " +
                (MerchantCompleteUx4T(item) ? "profil complet" : "profil à compléter"),
                MerchantCompleteUx4T(item)))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioMerchantBlueprintChoiceUx4T> MerchantVendorChoicesUx4T =>
        Project?.Blueprints.Where(item => item.Kind == BlueprintKind.Creature)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioMerchantBlueprintChoiceUx4T(item.Id, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioMerchantBlueprintChoiceUx4T> MerchantItemChoicesUx4T =>
        Project?.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioMerchantBlueprintChoiceUx4T(item.Id, $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal IReadOnlyList<StudioMerchantDialogueChoiceUx4T> MerchantDialogueChoicesUx4T =>
        Project?.Dialogues.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => new StudioMerchantDialogueChoiceUx4T(item.Id.Value,
                $"{item.DisplayName} · {item.Slug}"))
            .ToArray() ?? [];

    internal StudioMerchantDraftUx4T? CreateMerchantDraftUx4T(Guid? merchantId = null)
    {
        var project = Project; var history = History;
        if (project is null || history is null || MerchantVendorChoicesUx4T.Count == 0 ||
            MerchantItemChoicesUx4T.Count == 0 || MerchantDialogueChoicesUx4T.Count == 0) return null;
        var original = merchantId is null
            ? null
            : project.Module.WorldFeatures.Merchants.SingleOrDefault(item => item.Id == merchantId.Value);
        if (merchantId is not null && original is null) return null;
        var id = original?.Id ?? StableMerchantGuidUx4T(project.Id.Value, NextMerchantSlugUx4T(project));
        var complete = CompleteMerchantProfileUx4T(project, id, original);
        return new(history.State.Revision, FingerprintMerchantProjectUx4T(project), id, original, complete);
    }

    internal StudioMerchantDraftUx4T CompleteMerchantDraftUx4T(StudioMerchantDraftUx4T draft)
    {
        var project = Project ?? throw new InvalidOperationException("Aucun projet ouvert.");
        return draft with { Profile = CompleteMerchantProfileUx4T(project, draft.MerchantId, draft.Profile) };
    }

    internal StudioMerchantPreviewUx4T PreviewMerchantUx4T(StudioMerchantDraftUx4T draft)
    {
        ArgumentNullException.ThrowIfNull(draft); CancelMerchantUx4T();
        var project = Project; var history = History;
        if (project is null || history is null)
            return FailedMerchantUx4T("Aucun module n’est ouvert.", "studio.merchant.project_missing");
        if (history.State.Revision != draft.BaseRevision ||
            !string.Equals(FingerprintMerchantProjectUx4T(project), draft.SourceProjectFingerprint,
                StringComparison.Ordinal))
            return FailedMerchantUx4T("Le projet a changé ; reconstruisez l’aperçu.", "command.revision_conflict");

        var frozen = FreezeMerchantDraftUx4T(draft);
        var command = MerchantCommandUx4T(project.Module.WorldFeatures, frozen.Profile, history.State.Revision, true);
        var dryRun = _setMerchantCatalogUx4T.Handle(history.State, command);
        if (!dryRun.Succeeded)
            return new(false, false,
                HumanMerchantMessageUx4T(dryRun.Diagnostics.FirstOrDefault()?.Code,
                    dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Le marchand est invalide."),
                string.Empty, string.Empty,
                dryRun.Diagnostics.Select(item => new StudioMerchantDiagnosticUx4T(item.Code,
                    DiagnosticPathUx4T(item.Message), HumanMerchantMessageUx4T(item.Code, item.Message))).ToArray(),
                "Boutique non évaluée.", "Économie non évaluée.", "Transaction non évaluée.",
                "Interface non évaluée.", MerchantRuntimeBoundaryUx4T);

        var probe = _setMerchantCatalogUx4T.Handle(history.State, command with { DryRun = false });
        if (!probe.Succeeded)
            return FailedMerchantUx4T("Le candidat marchand n’a pas pu être construit.",
                "studio.merchant.candidate_invalid");
        var runtime = ResolveMerchantRuntimeUx4T(frozen.Profile, project);
        var canApply = runtime.Diagnostics.Count == 0;
        if (canApply)
        {
            _approvedMerchantDraftUx4T = frozen;
            _approvedMerchantRevisionUx4T = history.State.Revision;
            _approvedMerchantProjectFingerprintUx4T = FingerprintMerchantProjectUx4T(project);
            _approvedMerchantCandidateFingerprintUx4T = FingerprintMerchantProjectUx4T(probe.State.Project);
        }
        return new(true, canApply,
            canApply ? "Marchand complet prêt à appliquer." :
                "Le dry-run passe mais une résolution métier refuse le marchand.",
            dryRun.Patch?.Summary ?? "Prévisualiser le catalogue marchand.",
            dryRun.Inverse?.Summary ?? "Restaurer le catalogue précédent.", runtime.Diagnostics,
            runtime.Store, runtime.Economy, runtime.Transaction, runtime.Experience, MerchantRuntimeBoundaryUx4T);
    }

    internal StudioMerchantActionResultUx4T ApplyApprovedMerchantUx4T(StudioMerchantDraftUx4T current)
    {
        var project = Project; var history = History; var approved = _approvedMerchantDraftUx4T;
        if (project is null || history is null || approved is null || _approvedMerchantRevisionUx4T is null ||
            _approvedMerchantProjectFingerprintUx4T is null || _approvedMerchantCandidateFingerprintUx4T is null)
            return new(false, "Prévisualisez d’abord le marchand complet.");
        var frozenCurrent = FreezeMerchantDraftUx4T(current);
        if (history.State.Revision != _approvedMerchantRevisionUx4T ||
            !string.Equals(FingerprintMerchantProjectUx4T(project), _approvedMerchantProjectFingerprintUx4T,
                StringComparison.Ordinal) ||
            !string.Equals(FingerprintMerchantDraftUx4T(frozenCurrent),
                FingerprintMerchantDraftUx4T(approved), StringComparison.Ordinal))
        {
            CancelMerchantUx4T();
            return new(false, "Le brouillon ou le projet a changé ; reconstruisez l’aperçu.");
        }

        var command = MerchantCommandUx4T(project.Module.WorldFeatures, approved.Profile,
            history.State.Revision, true);
        var dryRun = _setMerchantCatalogUx4T.Handle(history.State, command);
        var probe = dryRun.Succeeded
            ? _setMerchantCatalogUx4T.Handle(history.State, command with { DryRun = false })
            : dryRun;
        if (!dryRun.Succeeded || !probe.Succeeded ||
            !string.Equals(FingerprintMerchantProjectUx4T(probe.State.Project),
                _approvedMerchantCandidateFingerprintUx4T, StringComparison.Ordinal))
        {
            CancelMerchantUx4T();
            return new(false, "Le re-dry-run ne correspond plus au candidat approuvé.");
        }
        var result = _setMerchantCatalogUx4T.Handle(history.State, command with { DryRun = false });
        CancelMerchantUx4T();
        if (!result.Succeeded)
            return new(false, HumanMerchantMessageUx4T(result.Diagnostics.FirstOrDefault()?.Code,
                result.Diagnostics.FirstOrDefault()?.Message ?? "Application refusée."));
        history.Record(result);
        return new(true, result.Patch?.Summary ?? "Marchand complet appliqué.");
    }

    internal StudioMerchantActionResultUx4T UndoMerchantUx4T()
    { CancelMerchantUx4T(); var result = Undo(); return new(result.Succeeded, result.Message); }

    internal StudioMerchantActionResultUx4T RedoMerchantUx4T()
    { CancelMerchantUx4T(); var result = Redo(); return new(result.Succeeded, result.Message); }

    internal void CancelMerchantUx4T()
    {
        _approvedMerchantDraftUx4T = null; _approvedMerchantRevisionUx4T = null;
        _approvedMerchantProjectFingerprintUx4T = null; _approvedMerchantCandidateFingerprintUx4T = null;
    }

    internal void CloseUx4T() => CancelMerchantUx4T();

    internal StudioMerchantApprovalSnapshotUx4T CaptureMerchantApprovalUx4T() => new(
        _approvedMerchantDraftUx4T, _approvedMerchantRevisionUx4T,
        _approvedMerchantProjectFingerprintUx4T, _approvedMerchantCandidateFingerprintUx4T);

    internal void RestoreMerchantApprovalUx4T(StudioMerchantApprovalSnapshotUx4T snapshot)
    {
        _approvedMerchantDraftUx4T = snapshot.Draft; _approvedMerchantRevisionUx4T = snapshot.Revision;
        _approvedMerchantProjectFingerprintUx4T = snapshot.ProjectFingerprint;
        _approvedMerchantCandidateFingerprintUx4T = snapshot.CandidateFingerprint;
    }

    internal StudioMerchantEvidenceProjectUx4T? CreateUx4TEvidenceProject()
    {
        var source = Project;
        if (source is null || source.Areas.Count == 0 || MerchantVendorChoicesUx4T.Count == 0 ||
            MerchantItemChoicesUx4T.Count == 0 || MerchantDialogueChoicesUx4T.Count == 0) return null;
        var real = source.Module.WorldFeatures.Merchants.FirstOrDefault(MerchantCompleteUx4T);
        if (real is not null) return new(source, real.Id, true, false);
        var id = StableMerchantGuidUx4T(source.Id.Value, "ux4-t:merchant");
        var profile = CompleteMerchantProfileUx4T(source, id, null);
        var catalog = ReplaceMerchantUx4T(source.Module.WorldFeatures, profile);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module.WithWorldFeatures(catalog),
            source.Blueprints, source.Areas, source.Dialogues, source.Quests);
        return new(project, id, false, true);
    }

    internal StudioUx4TSmokeResult RunUx4TContractSmoke()
    {
        var empty = new StudioUx4TSmokeResult(false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false);
        var evidence = CreateUx4TEvidenceProject(); if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4t-smoke-" +
            Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController(); var reopened = new StudioSessionController();
        StudioUx4TSmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded || !editing.Open(root).Succeeded)
                    return empty;
                var draft = editing.CreateMerchantDraftUx4T(evidence.MerchantId); if (draft is null) return empty;
                draft = editing.CompleteMerchantDraftUx4T(draft);
                draft = TouchMerchantDraftUx4T(draft, " · preuve contractuelle");
                var profile = draft.Profile; var foundation = profile.Foundation!;
                var explicitSelection = editing.MerchantChoicesUx4T.Any(item => item.MerchantId == evidence.MerchantId);
                var storeWizard = foundation.Wizard is { CreateStoreDefinition: true, CreateVendorBinding: true,
                    CreateDialogueChoice: true, CreateInteraction: true, CreateTests: true } &&
                    foundation.Vendors.Count > 0;
                var stockPricing = foundation.Stock.Count >= 2 &&
                                   foundation.Stock.Any(item => item.Supply == MerchantSupplyPolicy.Infinite) &&
                                   foundation.Pricing.BuyCurve.Count > 1 && foundation.Restrictions.WhitelistTypes.Count > 0;
                var treasuryPurchase = profile.Treasury.Count == profile.Currencies.Count &&
                                       profile.Purchase.RequireTreasuryFunds && profile.Purchase.MaximumPerItem > 0;
                var stolenRestrictions = !profile.StolenGoods.Accepted &&
                                         foundation.Restrictions.BlacklistTags.Contains("stolen", StringComparer.Ordinal);
                var restockEvents = profile.Restock.Persistent && ExperienceReadyUx4T(foundation);
                var atomic = profile.Transactions is { ServerAuthoritative: true, Stock.Count: > 0 };
                var currencies = profile.Currencies.Count > 1 && profile.Negotiation.ServerAuthoritative &&
                                 profile.Market is not null;
                var services = profile.Services.Count > 0 && profile.Ledger.Enabled &&
                               profile.Experience?.Ui is { KeyboardNavigation: true, GamepadNavigation: true,
                                   ScreenReaderLabels: true, HighContrast: true };
                var validation = WorldFeatureCatalogValidator.Validate(editing.Project!).Count == 0;
                var before = editing.Project!; var history = editing.History!; var revision = history.State.Revision;
                var journal = history.Journal.ToArray(); var preview = editing.PreviewMerchantUx4T(draft);
                var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(before, editing.Project) &&
                             ReferenceEquals(history, editing.History) && history.State.Revision == revision &&
                             history.Journal.SequenceEqual(journal);

                var changedCatalog = new WorldFeatureCatalog(before.Module.WorldFeatures.Id,
                    before.Module.WorldFeatures.SchemaVersion, "studio:ux4-t-stale-source",
                    before.Module.WorldFeatures.Placeables, before.Module.WorldFeatures.Doors,
                    before.Module.WorldFeatures.Triggers, before.Module.WorldFeatures.Encounters,
                    before.Module.WorldFeatures.VehicleRoutes, before.Module.WorldFeatures.Merchants,
                    before.Module.WorldFeatures.Companions, before.Module.WorldFeatures.Scripts);
                var changed = editing._setMerchantCatalogUx4T.Handle(history.State,
                    new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
                        changedCatalog, false));
                if (changed.Succeeded) history.Record(changed);
                var staleSource = changed.Succeeded && !editing.ApplyApprovedMerchantUx4T(draft).Succeeded;
                if (changed.Succeeded) editing.Undo();

                draft = editing.CompleteMerchantDraftUx4T(editing.CreateMerchantDraftUx4T(evidence.MerchantId)!);
                preview = editing.PreviewMerchantUx4T(draft);
                var staleApproval = preview.Succeeded &&
                    !editing.ApplyApprovedMerchantUx4T(TouchMerchantDraftUx4T(draft, " altérée")).Succeeded;

                draft = editing.CompleteMerchantDraftUx4T(editing.CreateMerchantDraftUx4T(evidence.MerchantId)!);
                draft = TouchMerchantDraftUx4T(draft, " · application");
                var source = editing.Project!; var sourceMerchant = source.Module.WorldFeatures.Merchants
                    .Single(item => item.Id == evidence.MerchantId);
                var finalPreview = editing.PreviewMerchantUx4T(draft);
                var appliedResult = finalPreview.CanApply
                    ? editing.ApplyApprovedMerchantUx4T(draft)
                    : new StudioMerchantActionResultUx4T(false, "preview failed");
                var applied = editing.Project; var appliedJson = applied is null ? string.Empty :
                    ProjectJsonSerializer.Serialize(applied);
                var appliedMerchant = applied?.Module.WorldFeatures.Merchants
                    .SingleOrDefault(item => item.Id == evidence.MerchantId);
                var canonical = appliedResult.Succeeded && appliedMerchant == draft.Profile &&
                                editing.History!.Journal.LastOrDefault()?.CommandName == "set_world_feature_catalog";
                var restoredProject = applied is null ? null : new ProjectDefinition(applied.Id, applied.Title,
                    applied.Module.WithWorldFeatures(ReplaceMerchantUx4T(applied.Module.WorldFeatures, sourceMerchant)),
                    applied.Blueprints, applied.Areas, applied.Dialogues, applied.Quests);
                var neighbors = restoredProject is not null &&
                                ProjectJsonSerializer.Serialize(restoredProject) == ProjectJsonSerializer.Serialize(source);
                var undo = editing.UndoMerchantUx4T();
                var undone = undo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) ==
                    ProjectJsonSerializer.Serialize(source);
                var redo = editing.RedoMerchantUx4T();
                var redone = redo.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == appliedJson;
                var saved = redone && editing.Save().Succeeded; editing.Close();
                var persistence = saved && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
                reopened.Close();
                var succeeded = explicitSelection && storeWizard && stockPricing && treasuryPurchase &&
                                stolenRestrictions && restockEvents && atomic && currencies && services && validation &&
                                dryRun && staleSource && staleApproval && canonical && undone && redone && persistence &&
                                neighbors;
                return new(succeeded, explicitSelection, storeWizard, stockPricing, treasuryPurchase,
                    stolenRestrictions, restockEvents, atomic, currencies, services, validation, dryRun,
                    staleSource, staleApproval, canonical, undone && redone, persistence, neighbors, false);
            }
            catch { return empty; }
            finally { editing.Close(); reopened.Close(); }
        }
        var result = Execute(); var released = DeleteMerchantRootUx4T(root);
        return result with { Succeeded = result.Succeeded && released, TemporaryWorkspaceReleased = released };
    }

    private (IReadOnlyList<StudioMerchantDiagnosticUx4T> Diagnostics, string Store, string Economy,
        string Transaction, string Experience) ResolveMerchantRuntimeUx4T(
        MerchantWorldProfile profile, ProjectDefinition project)
    {
        var diagnostics = new List<StudioMerchantDiagnosticUx4T>();
        try
        {
            var foundationAdapter = new MerchantFoundationRuntimeAdapter();
            var foundationResolution = foundationAdapter.Resolve(profile);
            if (!foundationResolution.Available || foundationResolution.FoundationHandle is null)
                diagnostics.Add(new(foundationResolution.DiagnosticCode,
                    foundationResolution.DiagnosticPath, "La fondation marchand requise est absente."));
            var experienceAdapter = new MerchantStoreExperienceRuntimeAdapter();
            var experienceResolution = experienceAdapter.Resolve(profile);
            if (!experienceResolution.Available || experienceResolution.ExperienceHandle is null)
                diagnostics.Add(new(experienceResolution.DiagnosticCode,
                    experienceResolution.DiagnosticPath, "L’expérience boutique requise est absente."));
            if (diagnostics.Count > 0)
                return (diagnostics, "Boutique indisponible.", "Économie indisponible.",
                    "Transaction indisponible.", "Interface indisponible.");

            var foundationHandle = foundationResolution.FoundationHandle!;
            var experienceHandle = experienceResolution.ExperienceHandle!;
            var knownScripts = FindForgeScripts().Select(item => item.RelativePath)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var script in (profile.Transactions?.Scripts.Values ?? []).Concat(
                         profile.Foundation!.Restrictions.CustomRuleBinding is { Length: > 0 } custom
                             ? [custom]
                             : []))
                if (!knownScripts.Contains(script))
                    diagnostics.Add(new("studio.merchant.script_missing", "merchant/scripts",
                        $"Le script {script} n’existe pas dans le workspace."));
            var context = new MerchantValidationContext(
                project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
                    .Select(item => item.Id.Value).ToHashSet(),
                project.Blueprints.Where(item => item.Kind == BlueprintKind.Creature)
                    .Select(item => item.Id.ToString()).ToHashSet(StringComparer.Ordinal),
                profile.Currencies.Select(item => item.Id).ToHashSet(StringComparer.Ordinal),
                knownScripts,
                profile.Foundation.Stock.Where(item => item.SupplierId is { Length: > 0 })
                    .Select(item => item.SupplierId!).ToHashSet(StringComparer.Ordinal), 500);
            var wizard = foundationAdapter.PreviewWizard(foundationHandle, profile, context);
            diagnostics.AddRange(wizard.Diagnostics.Select(item => new StudioMerchantDiagnosticUx4T(
                item.Code, item.Path, HumanMerchantMessageUx4T(item.Code, item.Message))));
            var store = foundationAdapter.BuildStore(foundationHandle, profile);
            var first = store.Stock.First(); var currency = profile.Currencies.First().Id;
            var price = foundationAdapter.Price(foundationHandle, profile, first.ItemDefinitionId,
                first.UnitPrice, new(currency, .25, .2, .9, .1, new HashSet<string>()));
            var itemBlueprint = project.Blueprints.Single(item => item.Id.Value == first.ItemDefinitionId);
            var acceptedByQuery = foundationAdapter.Matches(foundationHandle, profile,
                new(first.ItemDefinitionId, profile.Foundation.Restrictions.WhitelistTypes.FirstOrDefault() ?? "item",
                    profile.Foundation.Restrictions.WhitelistTags.ToHashSet(StringComparer.Ordinal),
                    Math.Max(1, profile.Foundation.Restrictions.MinimumQuality), "project-content", 1, "customer"));
            var quote = WorldFeatureRuntimeResolver.QuoteMerchant(profile, currency, Math.Max(1, first.UnitPrice),
                false, "outside", .5, 5, .1, .2);
            var quoteAudit = quote.Audit.StartsWith("ledger:", StringComparison.Ordinal)
                ? "ledger enregistré"
                : quote.Audit;
            var buyer = new TradePartyState("customer",
                new Dictionary<string, decimal> { [currency] = 100_000 },
                new Dictionary<Guid, int>(), 100);
            var seller = new TradePartyState("store",
                new Dictionary<string, decimal> { [currency] = 10_000 },
                new Dictionary<Guid, int>(), 1_000);
            var line = new TradeLine(first.ItemDefinitionId, first.VariantId, 1, first.UnitPrice,
                currency);
            var trade = MerchantService.Execute(new(store, buyer, seller),
                new(Guid.NewGuid(), TradeKind.Buy, buyer.PartyId, seller.PartyId, [line], []));
            if (!trade.Committed)
                diagnostics.AddRange(trade.Diagnostics.Select(item => new StudioMerchantDiagnosticUx4T(
                    item.Code, item.Path, HumanMerchantMessageUx4T(item.Code, item.Message))));
            var ui = experienceAdapter.Ui(experienceHandle, profile);
            var prices = new Dictionary<Guid, PricedItem> { [first.ItemDefinitionId] = price };
            var opened = experienceAdapter.Open(experienceHandle, profile, "customer", store.Vendors.First().VendorId,
                new(currency, .25, prices), DateTimeOffset.UnixEpoch);
            var closed = experienceAdapter.Close(experienceHandle, profile, opened,
                [Guid.NewGuid()], trade.Committed ? line.UnitPrice : 0, 0, DateTimeOffset.UnixEpoch.AddMinutes(1));
            return (diagnostics,
                $"{store.DisplayName} · {store.Vendors.Count} vendeur(s) · {store.Stock.Count} stock(s) · " +
                $"filtre {(acceptedByQuery ? "accepté" : "refusé")} pour {itemBlueprint.DisplayName}.",
                $"Achat {price.BuyPrice} / vente {price.SellPrice} {price.CurrencyId} · devis " +
                $"{(quote.Accepted ? "accepté" : "refusé")} · {profile.Treasury.Count} compte(s).",
                $"Transaction atomique {(trade.Committed ? "validée" : "refusée")} · " +
                $"stock source inchangé pendant l’aperçu · {quoteAudit}.",
                $"Session ouverte/fermée · {closed.TransactionIds.Count} transaction(s) · " +
                $"clavier={ui.Keyboard.ToString().ToLowerInvariant()} · manette={ui.Gamepad.ToString().ToLowerInvariant()}.");
        }
        catch (Exception exception)
        {
            diagnostics.Add(new("studio.merchant.runtime_preview_failed", "merchant/runtime", exception.Message));
            return (diagnostics, "Boutique non simulée.", "Économie non simulée.",
                "Transaction non simulée.", "Interface non simulée.");
        }
    }

    private static MerchantWorldProfile CompleteMerchantProfileUx4T(ProjectDefinition project, Guid merchantId,
        MerchantWorldProfile? original)
    {
        var vendor = project.Blueprints.First(item => item.Kind == BlueprintKind.Creature);
        var item = project.Blueprints.First(candidate => candidate.Kind == BlueprintKind.Item);
        var dialogue = project.Dialogues.First(); const string provenance = "studio:ux4-t";
        var currencyId = original?.Currencies.FirstOrDefault()?.Id ?? "gold";
        var currencies = original?.Currencies.Count > 0 ? original.Currencies :
            [new MerchantWorldProfile.CurrencyDefinition("gold", "currency.gold", 1, true, false),
             new MerchantWorldProfile.CurrencyDefinition("token", "currency.token", 2, true, true)];
        currencyId = currencies[0].Id;
        var treasury = original?.Treasury.Count > 0 ? original.Treasury : currencies.Select((currency, index) =>
            new MerchantWorldProfile.TreasuryAccount(currency.Id, index == 0 ? 1_000 : 500, 100, 0, 50,
                index == 0 ? 2_000 : 1_000)).ToArray();
        var foundation = original?.Foundation ?? new MerchantFoundationProfile(
            StableMerchantGuidUx4T(merchantId, "foundation"), 1, provenance, "echoppe-des-braises",
            "merchant.ember-shop.name", ["fr", "en"],
            [new(StableMerchantGuidUx4T(merchantId, "vendor"), vendor.Id, dialogue.Id.Value,
                "choice.trade", "interaction.trade", "npc")],
            new(StableMerchantGuidUx4T(merchantId, "wizard"), 1, provenance, true, true, true, true,
                true, "merchant-ember-shop-nominal"),
            [new(StableMerchantGuidUx4T(merchantId, "stock:infinite"), item.Id, "standard",
                    MerchantSupplyPolicy.Infinite, 12, null, 24, 0, ["quest.market.closed"], 25, currencyId),
             new(StableMerchantGuidUx4T(merchantId, "stock:finite"), item.Id, "limited",
                    MerchantSupplyPolicy.Finite, 4, null, 12, 1, [], 40, currencyId)],
            new(StableMerchantGuidUx4T(merchantId, "pricing"), 1, provenance, 1.2m, .6m,
                [new(0, 1), new(100, 1.1m)], [new(0, 1), new(100, .9m)],
                currencies.ToDictionary(value => value.Id, value => value.ExchangeRate, StringComparer.Ordinal),
                .1m, .2m, .2m, .1m, 1, 5_000),
            new(StableMerchantGuidUx4T(merchantId, "query"), 1, provenance, ["relic", "consumable"],
                ["merchant-approved"], ["stolen", "cursed"], 1, 5, ["project-content"], .5,
                null, null));
        var transactionStock = foundation.Stock.Select(entry => new MerchantWorldProfile.StockEntry(
            StableMerchantGuidUx4T(merchantId, $"transaction-stock:{entry.VariantId}"), entry.ItemBlueprintId,
            entry.VariantId, entry.Supply == MerchantSupplyPolicy.Infinite ? 999 : entry.Quantity,
            entry.BaseValue, entry.CurrencyId, ["item.allowed"])).ToArray();
        var transactions = original?.Transactions ?? new MerchantWorldProfile.TransactionPolicy(
            StableMerchantGuidUx4T(merchantId, "transactions"), 1, provenance,
            foundation.Vendors.First().InteractionId, foundation.Vendors.First().DialogueChoiceId,
            transactionStock, foundation.Locales, ["item.allowed"],
            new Dictionary<string, string>(StringComparer.Ordinal), 100, 1_000, true);
        var experience = original?.Experience ?? new MerchantStoreExperienceProfile(
            StableMerchantGuidUx4T(merchantId, "experience"), 1, provenance,
            new(StableMerchantGuidUx4T(merchantId, "sessions"), 1, provenance, "1.0.0",
                "StoreSessionOpened", "StoreSessionClosed", true, true, true, true),
            new(StableMerchantGuidUx4T(merchantId, "ui"), 1, provenance, true, true, true, true, true,
                true, true, true, ["name", "compare", "price", "quantity"]));
        return new(merchantId, 1, original?.Provenance ?? provenance, original?.BlueprintId ?? vendor.Id,
            currencies, treasury,
            original?.Purchase ?? new(500, 800, new Dictionary<string, decimal> { ["weapon"] = 300 }, -.5, true),
            original?.StolenGoods ?? new(false, ["capital"], true, true, .25, "crime.stolen-goods"),
            original?.Restock ?? new(24, "market.new-day", null, true, true),
            original?.Negotiation ?? new("bargain", 12, .2, .2, 60, true),
            original is null ? new("heartlands", .25, .5,
                new Dictionary<string, double> { ["festival"] = -.1 }, true) : original.Market,
            original is null ?
                [new(StableMerchantGuidUx4T(merchantId, "service:repair"), "repair", "service.repair",
                    currencyId, 25, "repair.item", "item.damaged")] : original.Services,
            original?.Ledger ?? new(true, 90, 500, true, true, true), transactions, true,
            foundation, true, experience, true);
    }

    private static SetWorldFeatureCatalogCommand MerchantCommandUx4T(WorldFeatureCatalog source,
        MerchantWorldProfile profile, ProjectRevision revision, bool dryRun) =>
        new(CommandId.From(Guid.NewGuid()), revision, ReplaceMerchantUx4T(source, profile), dryRun);

    private static WorldFeatureCatalog ReplaceMerchantUx4T(WorldFeatureCatalog source,
        MerchantWorldProfile profile)
    {
        var merchants = source.Merchants.Any(item => item.Id == profile.Id)
            ? source.Merchants.Select(item => item.Id == profile.Id ? profile : item).ToArray()
            : source.Merchants.Append(profile).ToArray();
        return new(source.Id, source.SchemaVersion, source.Provenance, source.Placeables, source.Doors,
            source.Triggers, source.Encounters, source.VehicleRoutes, merchants, source.Companions,
            source.Scripts);
    }

    private static StudioMerchantDraftUx4T TouchMerchantDraftUx4T(StudioMerchantDraftUx4T draft,
        string suffix)
    {
        var foundation = draft.Profile.Foundation! with
        { DisplayNameKey = draft.Profile.Foundation!.DisplayNameKey + suffix };
        return draft with { Profile = draft.Profile with { Foundation = foundation } };
    }

    private static bool MerchantCompleteUx4T(MerchantWorldProfile item) =>
        item.Foundation is not null && item.Transactions is not null && item.Experience is not null &&
        item.FoundationRequiredAtRuntime && item.TransactionsRequiredAtRuntime && item.ExperienceRequiredAtRuntime;

    private static bool ExperienceReadyUx4T(MerchantFoundationProfile foundation) =>
        foundation.Wizard.CreateTests && foundation.Vendors.Count > 0;

    private static StudioMerchantDraftUx4T FreezeMerchantDraftUx4T(StudioMerchantDraftUx4T draft) => draft;

    private static StudioMerchantPreviewUx4T FailedMerchantUx4T(string message, string code) =>
        new(false, false, HumanMerchantMessageUx4T(code, message), string.Empty, string.Empty,
            [new(code, "merchants/preview", HumanMerchantMessageUx4T(code, message))],
            "Boutique non évaluée.", "Économie non évaluée.", "Transaction non évaluée.",
            "Interface non évaluée.", MerchantRuntimeBoundaryUx4T);

    private static string HumanMerchantMessageUx4T(string? code, string fallback) => code switch
    {
        "command.revision_conflict" => "Le projet a changé ; reconstruisez l’aperçu.",
        "world.merchant.vendor_reference_missing" => "Choisissez un vendeur créature existant.",
        "world.merchant.dialogue_reference_missing" => "Choisissez un dialogue existant.",
        "world.merchant.foundation_stock_item_missing" => "Chaque stock doit viser un objet existant.",
        "world.merchant.stock_item_invalid" => "Chaque transaction doit viser un objet existant.",
        "world.merchant.store_ui_invalid" => "La boutique doit rester accessible au clavier et à la manette.",
        _ => fallback,
    };

    private static string DiagnosticPathUx4T(string message)
    {
        var separator = message.IndexOf(':');
        return separator > 0 ? message[..separator] : "merchants/preview";
    }

    private static string NextMerchantSlugUx4T(ProjectDefinition project)
    {
        const string root = "echoppe-des-braises"; var slug = root; var index = 2;
        var known = project.Module.WorldFeatures.Merchants.Select(item => item.Foundation?.Slug)
            .Where(item => item is not null).ToHashSet(StringComparer.Ordinal);
        while (known.Contains(slug)) slug = $"{root}-{index++}";
        return slug;
    }

    private static string FingerprintMerchantProjectUx4T(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string FingerprintMerchantDraftUx4T(StudioMerchantDraftUx4T draft) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));

    internal static Guid StableMerchantGuidUx4T(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> value = stackalloc byte[16]; hash.AsSpan(0, 16).CopyTo(value);
        value[6] = (byte)((value[6] & 0x0f) | 0x40); value[8] = (byte)((value[8] & 0x3f) | 0x80);
        return new Guid(value);
    }

    private static bool DeleteMerchantRootUx4T(string root)
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); return !Directory.Exists(root); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }

    private const string MerchantRuntimeBoundaryUx4T =
        "Aperçu pur en mémoire : aucun stock, portefeuille, crime, script, acteur ou transport réseau live n’est modifié.";
}

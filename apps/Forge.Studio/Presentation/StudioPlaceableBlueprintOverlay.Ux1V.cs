using Forge.Studio.Session;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableBlueprintOverlay
{
    internal void OpenLifecycleEvents()
    {
        Open();
        if (_draft is null)
        {
            return;
        }

        SetPresentation(staticFocus: false);
        _lifecycleEventsFocus = true;
        _eyebrow.Text = "ÉVÉNEMENTS DU PLACEABLE";
        _heading.Text = "Configurer OnUsed, OnDisturbed, OnDamaged et OnDeath";
        _subtitle.Text =
            "Version ForgeScript · liaisons · scripts avant/après · payloads typés · exigences runtime · aperçus sans écriture";
        _scenePreviewTitle.Text = "Aperçu des événements du cycle de vie";
        _scenePreviewDescription.Text =
            "Utilisation, contenu modifié, dégâts et mort sont produits par les mêmes contrats et adaptateurs que le runtime.";
        _updating = true;
        _role.Select((int)StudioPlaceableRole.Interactable);
        _onUsedEnabled.ButtonPressed = true;
        _contentsChanged.ButtonPressed = true;
        _health.ButtonPressed = true;
        _damageEvents.ButtonPressed = true;
        _updating = false;
        UpdateRoleFields();
        UpdateDamageFields();
        UpdateContentsChangedFields();
        UpdateDamageEventsFields();
        _onUsedEventEditor.FocusBindings();
        SetStatus(
            "Événements prêts · nommez les liaisons, vérifiez les payloads puis prévisualisez chaque issue sans toucher à la scène.",
            error: false);
    }

    internal bool RunLifecycleEventsUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        OpenLifecycleEvents();
        if (_draft is null)
        {
            Close();
            return false;
        }

        var blueprintId = _draft.BlueprintId;
        _onUsedEventEditor.SetEvidenceValues();
        _contentsChangedEditor.SetEvidenceValues();
        _damageEventsEditor.SetEvidenceValues("ux1-v-death");
        Preview();
        var visibleContract = LifecycleEventsPreviewReady && ShowsHumanSurface;
        var applied = _session.ApplyApprovedPlaceable();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var damage = profile?.DamageEvents ?? profile?.CompositionEvents?.DamageEvents;
        var foundation = profile?.Foundation;
        var securityEvents = profile?.SecurityEvents;
        var canonical = applied.Succeeded && profile?.Foundation?.OnUsed is
        {
            ForgeScriptApiVersion: "1.1.0",
            EventBinding: "ObjetUtilise",
            BeforeScriptId: "scripts/placeable_before_use",
            AfterScriptId: "scripts/placeable_after_use",
        } && securityEvents?.ContentsChanged is
        {
            ForgeScriptApiVersion: "1.1.0",
            EventBinding: "ContenuObjetModifie",
            IncludeTransactionalDelta: true,
            IncludeCause: true,
            IncludeRevision: true,
            IncludeActor: true,
        } && damage is
        {
            ForgeScriptApiVersion: "1.1.0",
            DamagedBinding: "ObjetEndommage",
            DestroyedBinding: "ObjetDetruit",
            IncludeSource: true,
            IncludeAmount: true,
            IncludeDamageType: true,
            IncludePreviousHealth: true,
            IncludeCurrentHealth: true,
            IncludeContext: true,
        } && foundation?.OnUsedRequiredAtRuntime == true &&
             profile?.ContentsChangedRequiredAtRuntime == true && profile?.DamageEventsRequiredAtRuntime == true;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        Close();
        return visibleContract && canonical && restored;
    }
}

using Forge.Domain.Entities;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioPlaceableBlueprintOverlay _placeableBlueprintOverlay = null!;
    private Button _placeableBlueprintButton = null!;

    private void BuildUx1EActions(HBoxContainer headingRow)
    {
        _placeableBlueprintButton = StudioStyle.Button("Blueprint de décor", compact: true);
        _placeableBlueprintButton.TooltipText =
            "Composer visuel, collision, rôle, composants, scripts et transformation avec aperçu obligatoire.";
        _placeableBlueprintButton.Pressed += OpenPlaceableBlueprint;
        headingRow.AddChild(_placeableBlueprintButton);
    }

    private void InitializeUx1EOverlay()
    {
        _placeableBlueprintOverlay = new StudioPlaceableBlueprintOverlay(_session);
        _placeableBlueprintOverlay.StatusChanged += SetStatus;
        _placeableBlueprintOverlay.ProjectChanged += RefreshAfterUx1EMutation;
        _placeableBlueprintOverlay.InstancesRequested += SelectPlaceableInstances;
        AddChild(_placeableBlueprintOverlay);
    }

    private void OpenPlaceableBlueprint() =>
        OpenPlaceableBlueprint(staticFocus: false, interactionFocus: false, containerFocus: false);

    private void OpenPlaceableBlueprint(
        bool staticFocus,
        bool interactionFocus = false,
        bool containerFocus = false,
        bool identityFocus = false,
        bool transformFocus = false,
        bool initialAnimationFocus = false,
        bool destructibilityFocus = false,
        bool plotProtectionFocus = false,
        bool factionOwnershipFocus = false,
        bool conversationFocus = false,
        bool lockFocus = false,
        bool credentialFocus = false,
        bool trapFocus = false,
        bool lootFocus = false,
        bool containerEventsFocus = false,
        bool lockEventsFocus = false)
    {
        _globalSearchOverlay.Close();
        _safeRenameOverlay.Close();
        _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close();
        _viewportToolsOverlay.Close();
        _areaCreationOverlay.Close();
        _areaDocumentOperationsOverlay.Close();
        _areaLoadingOverlay.Close();
        _areaLayersOverlay.Close();
        SwitchPage("placement", notify: false);
        if (lockEventsFocus)
        {
            _placeableBlueprintOverlay.OpenLockEvents();
            SetStatus("Verrouillage et déverrouillage ouverts · événements avant et après, contexte canonique, veto et aperçu sans écriture disponibles");
        }
        else if (containerEventsFocus)
        {
            _placeableBlueprintOverlay.OpenContainerEvents();
            SetStatus("Ouverture et fermeture ouvertes · liaisons, contexte canonique et aperçu sans écriture disponibles");
        }
        else if (lootFocus)
        {
            _placeableBlueprintOverlay.OpenInitialTreasure();
            SetStatus("Trésor initial ouvert · objets, catégories, poids, quantités, qualité, moment, graine et sauvegarde disponibles");
        }
        else if (trapFocus)
        {
            _placeableBlueprintOverlay.OpenTrap();
            SetStatus("Piège ouvert · déclencheur, effet, difficultés, réarmement, propriétaire, alliés et VFX disponibles");
        }
        else if (credentialFocus)
        {
            _placeableBlueprintOverlay.OpenCredentialPolicy();
            SetStatus("Clé consommée ouverte · conservation, consommation, charges, partage, permission et audit disponibles");
        }
        else if (lockFocus)
        {
            _placeableBlueprintOverlay.OpenLock();
            SetStatus("Verrouillage ouvert · état, clés, compétence, code, piratage, droits et cycle de vie disponibles");
        }
        else if (conversationFocus)
        {
            _placeableBlueprintOverlay.OpenConversation();
            SetStatus("Conversation ouverte · dialogues, cinq déclencheurs, points d’entrée, priorités et aperçu disponibles");
        }
        else if (factionOwnershipFocus)
        {
            _placeableBlueprintOverlay.OpenFactionOwnership();
            SetStatus("Faction ouverte · propriétaire, appartenances, hostilité, crime, accès et ciblage disponibles");
        }
        else if (plotProtectionFocus)
        {
            _placeableBlueprintOverlay.OpenPlotProtection();
            SetStatus("Protection plot ouverte · actions, droits, feedback, override MJ et audit disponibles");
        }
        else if (destructibilityFocus)
        {
            _placeableBlueprintOverlay.OpenDestructibility();
            SetStatus("Destructibilité ouverte · santé, résistances, étapes, débris, réparation et aperçu disponibles");
        }
        else if (initialAnimationFocus)
        {
            _placeableBlueprintOverlay.OpenInitialAnimation();
            SetStatus("Animation initiale ouverte · états, apparences, transitions et aperçu disponibles");
        }
        else if (transformFocus)
        {
            _placeableBlueprintOverlay.OpenPreciseTransform();
            SetStatus("Transformation précise ouverte · pose, pivot, grille, parent, surface, animation et gizmo disponibles");
        }
        else if (identityFocus)
        {
            _placeableBlueprintOverlay.OpenIdentityAppearance();
            SetStatus("Identité et apparence ouvertes · langues et ressources visuelles disponibles");
        }
        else if (containerFocus)
        {
            _placeableBlueprintOverlay.OpenContainer();
            SetStatus("Conteneur ouvert · limites, accès, sauvegarde, butin et multijoueur disponibles");
        }
        else if (interactionFocus)
        {
            _placeableBlueprintOverlay.OpenInteractable();
            SetStatus("Objet utilisable ouvert · actions, conditions et événement OnUsed disponibles");
        }
        else if (staticFocus)
        {
            _placeableBlueprintOverlay.OpenStaticDecoration();
            SetStatus("Décor statique ouvert · impact de rendu, sélection 3D et historique disponibles");
        }
        else
        {
            _placeableBlueprintOverlay.Open();
            SetStatus("Blueprint de décor ouvert · aperçu sec, sélection 3D et historique disponibles");
        }
    }

    private void SelectPlaceableInstances(IReadOnlyList<EntityId> entityIds, EntityId? activeEntityId)
    {
        var activeIds = _session.ActiveArea?.Entities
            .Where(entity => entityIds.Contains(entity.Id))
            .Select(entity => entity.Id)
            .ToArray() ?? [];
        if (activeIds.Length == 0)
        {
            SetStatus("Aucune instance de ce blueprint dans la zone active.", error: true);
            return;
        }
        SelectEntities(activeIds, activeIds.Contains(activeEntityId) ? activeEntityId : activeIds[0]);
        SetStatus($"{activeIds.Length} instance(s) du blueprint sélectionnée(s) dans la vue 3D");
    }

    private void RefreshAfterUx1EMutation()
    {
        if (_session.ActiveArea is null)
        {
            WorldViewport.ShowNoArea(_session);
        }
        else
        {
            WorldViewport.Rebuild(_session);
            WorldViewport.ApplyCameraProfile(_session.CameraProfile);
        }
        RefreshComparisonViewport();
        RebuildExplorer();
        RefreshInspector();
        UpdateUx1EActionState();
        UpdateCommandState();
        NavigationChanged?.Invoke();
    }

    private void UpdateUx1EActionState()
    {
        if (_placeableBlueprintButton is null)
        {
            return;
        }
        _placeableBlueprintButton.Visible = PageId == "placement";
    }

    internal bool PrepareUx1EEvidence(string mode)
    {
        SwitchPage("placement", notify: false);
        return _placeableBlueprintOverlay.PrepareEvidence(mode);
    }

    internal void FinalizeEditingEvidenceLayout() => _placeableBlueprintOverlay.FinalizeEvidenceLayout();

    internal bool RunUx1ESmoke()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var uiTransaction = _placeableBlueprintOverlay.RunUiTransactionEvidence();
        var uiReady = PrepareUx1EEvidence("ux1-e-blueprint") &&
                      PageId == "placement" &&
                      _placeableBlueprintOverlay.IsOpen &&
                      _placeableBlueprintOverlay.BlueprintCount >= 1 &&
                      _placeableBlueprintOverlay.AssetOptionCount >= 1 &&
                      _placeableBlueprintOverlay.HasEmptyState &&
                      _placeableBlueprintOverlay.PreviewReady &&
                      _placeableBlueprintOverlay.InstanceSelectionAvailable &&
                      _placeableBlueprintOverlay.ShowsHumanSurface;
        _placeableBlueprintOverlay.Close();
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var result = _session.RunUx1EContractSmoke();
        var currentProjectUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                      _session.ActiveAreaId == originalAreaId &&
                                      !_session.IsDirty;
        var valid = result.Succeeded && uiReady && uiTransaction && currentProjectUntouched;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX1E_{(valid ? "PASS" : "FAILED")} " +
            $"preview={result.Preview.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"composition={result.Composition.ToString().ToLowerInvariant()} " +
            $"asset_reference={result.AssetReference.ToString().ToLowerInvariant()} " +
            $"instance_selection={result.InstanceSelection.ToString().ToLowerInvariant()} " +
            $"neighbor_preserved={result.NeighborPreserved.ToString().ToLowerInvariant()} " +
            $"other_catalog_preserved={result.OtherCatalogPreserved.ToString().ToLowerInvariant()} " +
            $"runtime={result.RuntimeSemantics.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"persistence={result.Persistence.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidBlocked.ToString().ToLowerInvariant()} " +
            $"human_no_uuid={result.HumanDiagnostics.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"project_restored={currentProjectUntouched.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}

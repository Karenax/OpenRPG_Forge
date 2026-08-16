using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableBlueprintOverlay
{
    private StudioPlaceableAssemblyValidationOverlay? _assemblyValidationOverlay;
    private bool _assemblyValidationVisibilityHooked;

    internal bool AssemblyValidationOpen => _assemblyValidationOverlay?.IsOpen == true;
    internal int AssemblyValidationPlaceableCount => _assemblyValidationOverlay?.PlaceableCount ?? 0;
    internal bool AssemblyValidationPreviewReady => _assemblyValidationOverlay?.PreviewReady == true;
    internal bool AssemblyValidationShowsHumanSurface => _assemblyValidationOverlay?.ShowsHumanSurface == true;
    internal bool AssemblyValidationUsesIsolatedEvidence =>
        _assemblyValidationOverlay?.UsesIsolatedEvidence == true;
    internal bool AssemblyValidationEvidenceSourceUntouched =>
        _assemblyValidationOverlay?.EvidenceSourceUntouched == true;
    internal bool AssemblyValidationEvidenceResourcesReleased =>
        _assemblyValidationOverlay?.EvidenceResourcesReleased == true;

    internal void OpenAssemblyValidation()
    {
        Open();
        EnsureAssemblyValidationOverlay();
        _assemblyValidationOverlay!.Open();
        SetStatus(
            "Sockets, actions et validation ouverts sans sélection implicite · choisissez un placeable puis prévisualisez avant application.",
            error: false);
    }

    internal void CloseAssemblyValidation() => Close();

    internal bool PrepareAssemblyValidationEvidence(string mode)
    {
        Open();
        EnsureAssemblyValidationOverlay();
        return _assemblyValidationOverlay!.PrepareEvidence(mode);
    }

    internal bool RunAssemblyValidationUiTransactionEvidence()
    {
        Open();
        EnsureAssemblyValidationOverlay();
        return _assemblyValidationOverlay!.RunUiTransactionEvidence();
    }

    private void EnsureAssemblyValidationOverlay()
    {
        if (_assemblyValidationOverlay is not null)
        {
            return;
        }
        _assemblyValidationOverlay = new StudioPlaceableAssemblyValidationOverlay(_session);
        _assemblyValidationOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _assemblyValidationOverlay.CloseRequested += Close;
        _assemblyValidationOverlay.ProjectChanged += () => ProjectChanged?.Invoke();
        _assemblyValidationOverlay.StatusChanged += SetStatus;
        AddChild(_assemblyValidationOverlay);
        if (!_assemblyValidationVisibilityHooked)
        {
            VisibilityChanged += () =>
            {
                if (!Visible)
                {
                    _assemblyValidationOverlay?.Close();
                }
            };
            _assemblyValidationVisibilityHooked = true;
        }
    }
}

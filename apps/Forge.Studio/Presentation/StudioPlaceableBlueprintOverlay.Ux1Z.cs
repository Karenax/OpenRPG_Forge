using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableBlueprintOverlay
{
    private StudioPlaceableReactivePhysicsOverlay? _reactivePhysicsOverlay;
    private bool _reactivePhysicsVisibilityHooked;

    internal bool ReactivePhysicsOpen => _reactivePhysicsOverlay?.IsOpen == true;
    internal int ReactivePhysicsPlaceableCount => _reactivePhysicsOverlay?.PlaceableCount ?? 0;
    internal bool ReactivePhysicsPreviewReady => _reactivePhysicsOverlay?.PreviewReady == true;
    internal bool ReactivePhysicsShowsHumanSurface => _reactivePhysicsOverlay?.ShowsHumanSurface == true;

    internal void OpenReactivePhysics()
    {
        Open();
        EnsureReactivePhysicsOverlay();
        _reactivePhysicsOverlay!.Open();
        SetStatus(
            "Événements réactifs ouverts sans sélection implicite · choisissez un placeable puis prévisualisez avant application.",
            error: false);
    }

    internal void CloseReactivePhysics() => Close();

    internal bool PrepareReactivePhysicsEvidence(string mode)
    {
        Open();
        EnsureReactivePhysicsOverlay();
        return _reactivePhysicsOverlay!.PrepareEvidence(mode);
    }

    internal bool RunReactivePhysicsUiTransactionEvidence()
    {
        Open();
        EnsureReactivePhysicsOverlay();
        return _reactivePhysicsOverlay!.RunUiTransactionEvidence();
    }

    private void EnsureReactivePhysicsOverlay()
    {
        if (_reactivePhysicsOverlay is not null)
        {
            return;
        }
        _reactivePhysicsOverlay = new StudioPlaceableReactivePhysicsOverlay(_session);
        _reactivePhysicsOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _reactivePhysicsOverlay.CloseRequested += Close;
        _reactivePhysicsOverlay.ProjectChanged += () => ProjectChanged?.Invoke();
        _reactivePhysicsOverlay.StatusChanged += SetStatus;
        AddChild(_reactivePhysicsOverlay);
        if (!_reactivePhysicsVisibilityHooked)
        {
            VisibilityChanged += () =>
            {
                if (!Visible)
                {
                    _reactivePhysicsOverlay?.Close();
                }
            };
            _reactivePhysicsVisibilityHooked = true;
        }
    }
}

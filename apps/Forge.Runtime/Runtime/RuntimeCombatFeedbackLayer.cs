using Forge.Application.Combat;
using Godot;

namespace Forge.Runtime;

internal sealed partial class RuntimeCombatFeedbackLayer : CanvasLayer
{
    internal const int FloatingNumberCapacity = 48;
    internal const int NameplateCapacity = 32;
    private const float RegularNameplateDistance = 32;
    private const float TargetNameplateDistance = 45;
    private const double OcclusionRefreshSeconds = .25;
    private readonly Camera3D _camera;
    private readonly RuntimeUiMotionPolicy _policy;
    private readonly Dictionary<Node3D, CombatantRegistration> _combatants = [];
    private readonly Stack<NameplateWidget> _availableNameplates = [];
    private readonly List<FloatingWidget> _floatingWidgets = [];
    private Control? _root;
    private NameplateWidget? _targetNameplate;
    private Node3D? _target;
    private double _nameplateAccumulator;
    private int _floatingSequence;

    internal RuntimeCombatFeedbackLayer(Camera3D camera, RuntimeUiMotionPolicy policy)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    internal int RegisteredCount => _combatants.Count;

    internal int ActiveFloatingCount => _floatingWidgets.Count(item => item.Active);

    internal int PeakActiveFloatingCount { get; private set; }

    internal Guid LastTransactionId { get; private set; }

    internal int LastPresentedDamage { get; private set; }

    internal int FlashCount { get; private set; }

    internal string TargetCombatantId => _target is not null && _combatants.TryGetValue(_target, out var target)
        ? target.CombatantId
        : string.Empty;

    public override void _Ready()
    {
        Layer = 18;
        EnsureUi();
    }

    internal void Register(
        Node3D node,
        string combatantId,
        string displayName,
        int currentHealth,
        int maximumHealth,
        bool boss,
        float anchorHeight)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(combatantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (maximumHealth <= 0 || currentHealth < 0 || currentHealth > maximumHealth ||
            !float.IsFinite(anchorHeight) || anchorHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHealth));

        EnsureUi();
        if (_combatants.TryGetValue(node, out var existing))
        {
            existing.DisplayName = displayName.Trim();
            existing.CurrentHealth = currentHealth;
            existing.MaximumHealth = maximumHealth;
            existing.Boss = boss;
            existing.AnchorHeight = anchorHeight;
            existing.Nameplate?.Apply(existing, target: false);
            return;
        }

        var nameplate = _availableNameplates.Count > 0 ? _availableNameplates.Pop() : null;
        var registration = new CombatantRegistration(
            combatantId.Trim(), displayName.Trim(), currentHealth, maximumHealth, boss, anchorHeight, nameplate);
        _combatants.Add(node, registration);
        nameplate?.Apply(registration, target: false);
    }

    internal void UpdateCombatant(Node3D node, int currentHealth, int maximumHealth)
    {
        if (!_combatants.TryGetValue(node, out var registration)) return;
        if (maximumHealth <= 0 || currentHealth < 0 || currentHealth > maximumHealth)
            throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        registration.CurrentHealth = currentHealth;
        registration.MaximumHealth = maximumHealth;
        registration.Nameplate?.Apply(registration, target: false);
        if (node == _target) _targetNameplate?.Apply(registration, target: true);
    }

    internal void Unregister(Node3D node)
    {
        if (!_combatants.Remove(node, out var registration)) return;
        if (registration.Nameplate is not null)
        {
            registration.Nameplate.Visible = false;
            _availableNameplates.Push(registration.Nameplate);
        }
        if (node == _target) ClearTarget();
    }

    internal void SetTarget(Node3D? node)
    {
        if (node is null || !GodotObject.IsInstanceValid(node) || !_combatants.TryGetValue(node, out var target))
        {
            ClearTarget();
            return;
        }

        _target = node;
        _targetNameplate?.Apply(target, target: true);
        UpdateNameplates();
    }

    internal void ClearTarget()
    {
        _target = null;
        if (_targetNameplate is not null) _targetNameplate.Visible = false;
    }

    internal void Present(CombatFeedbackPresentation presentation, Node3D targetNode)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(targetNode);
        EnsureUi();
        LastTransactionId = presentation.TransactionId;
        LastPresentedDamage = presentation.Amounts
            .Where(item => item.Kind == CombatFeedbackAmountKind.Damage)
            .Sum(item => item.Value);
        UpdateCombatant(targetNode, presentation.TargetHealth, presentation.TargetMaximumHealth);
        SetTarget(targetNode);
        foreach (var amount in presentation.Amounts)
        {
            PresentAmount(targetNode, amount);
        }
        PeakActiveFloatingCount = Math.Max(PeakActiveFloatingCount, ActiveFloatingCount);
    }

    internal void Flash(Node3D targetNode, CombatFeedbackAmountKind kind)
    {
        if (!_combatants.TryGetValue(targetNode, out var registration)) return;
        var semantic = kind switch
        {
            CombatFeedbackAmountKind.Healing => RuntimeUiSemanticColor.Healing,
            CombatFeedbackAmountKind.ResourceRestored => RuntimeUiSemanticColor.Resource,
            _ => RuntimeUiSemanticColor.Damage,
        };
        var color = _policy.ResolveColor(semantic);
        registration.Nameplate?.Flash(color, _policy.ReducedMotion);
        if (targetNode == _target) _targetNameplate?.Flash(color, _policy.ReducedMotion);
        FlashCount++;
    }

    public override void _Process(double delta)
    {
        _nameplateAccumulator += delta;
        if (_nameplateAccumulator >= _policy.NameplateUpdateSeconds)
        {
            _nameplateAccumulator %= _policy.NameplateUpdateSeconds;
            UpdateNameplates();
        }
        UpdateFloatingWidgets(delta);
        foreach (var registration in _combatants.Values) registration.Nameplate?.AdvanceFlash(delta);
        _targetNameplate?.AdvanceFlash(delta);
    }

    private void EnsureUi()
    {
        if (_root is not null) return;
        _root = new Control
        {
            Name = "CombatFeedbackRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        for (var index = 0; index < NameplateCapacity - 1; index++)
        {
            var nameplate = new NameplateWidget(_policy, target: false) { Visible = false };
            _root.AddChild(nameplate);
            _availableNameplates.Push(nameplate);
        }
        _targetNameplate = new NameplateWidget(_policy, target: true) { Visible = false };
        _root.AddChild(_targetNameplate);

        for (var index = 0; index < FloatingNumberCapacity; index++)
        {
            var widget = new FloatingWidget(_policy) { Sequence = index };
            _root.AddChild(widget.Label);
            _floatingWidgets.Add(widget);
        }
    }

    private void PresentAmount(Node3D targetNode, CombatFeedbackAmount amount)
    {
        for (var index = 0; index < _floatingWidgets.Count; index++)
        {
            var candidate = _floatingWidgets[index];
            if (candidate.Active && candidate.Target == targetNode && candidate.Kind == amount.Kind &&
                candidate.Age <= .12)
            {
                candidate.Amount += amount.Value;
                candidate.Age = 0;
                candidate.RefreshText();
                return;
            }
        }

        FloatingWidget? selected = null;
        for (var index = 0; index < _floatingWidgets.Count; index++)
        {
            if (_floatingWidgets[index].Active) continue;
            selected = _floatingWidgets[index];
            break;
        }
        if (selected is null)
        {
            selected = _floatingWidgets[0];
            for (var index = 1; index < _floatingWidgets.Count; index++)
            {
                if (_floatingWidgets[index].Age > selected.Age) selected = _floatingWidgets[index];
            }
        }

        var anchorHeight = _combatants.TryGetValue(targetNode, out var registration)
            ? registration.AnchorHeight
            : 1.4f;
        var stackLane = 0;
        for (var index = 0; index < _floatingWidgets.Count; index++)
        {
            var active = _floatingWidgets[index];
            if (active.Active && active.Target == targetNode && active.Age <= .16) stackLane++;
        }
        selected.Activate(targetNode, amount.Kind, amount.Value, anchorHeight, _floatingSequence++,
            Math.Min(stackLane, 3));
        if (TryProject(targetNode.GlobalPosition + Vector3.Up * anchorHeight, out var initialPosition,
                checkOcclusion: false))
            selected.LastScreenPosition = initialPosition;
    }

    private void UpdateNameplates()
    {
        foreach (var pair in _combatants)
        {
            var node = pair.Key;
            var registration = pair.Value;
            var widget = registration.Nameplate;
            if (widget is null) continue;
            if (node == _target)
            {
                widget.Visible = false;
                continue;
            }
            UpdateNameplate(node, registration, widget, RegularNameplateDistance);
        }

        if (_target is not null && _targetNameplate is not null &&
            _combatants.TryGetValue(_target, out var targetRegistration))
            UpdateNameplate(_target, targetRegistration, _targetNameplate, TargetNameplateDistance);
        else if (_targetNameplate is not null)
            _targetNameplate.Visible = false;
    }

    private void UpdateNameplate(
        Node3D node,
        CombatantRegistration registration,
        NameplateWidget widget,
        float maximumDistance)
    {
        if (!GodotObject.IsInstanceValid(node) || _camera.GlobalPosition.DistanceTo(node.GlobalPosition) > maximumDistance)
        {
            widget.Visible = false;
            return;
        }
        var anchor = node.GlobalPosition + Vector3.Up * registration.AnchorHeight;
        if (!TryProject(anchor, out var screenPosition, checkOcclusion: false))
        {
            widget.Visible = false;
            return;
        }
        registration.OcclusionRemainingSeconds -= _policy.NameplateUpdateSeconds;
        if (registration.OcclusionRemainingSeconds <= 0)
        {
            registration.Occluded = IsOccluded(anchor);
            registration.OcclusionRemainingSeconds = OcclusionRefreshSeconds;
        }
        if (registration.Occluded)
        {
            widget.Visible = false;
            return;
        }
        widget.Apply(registration, node == _target);
        widget.Position = screenPosition - new Vector2(widget.Size.X * .5f, widget.Size.Y + 8);
        widget.Visible = true;
    }

    private bool TryProject(Vector3 worldPosition, out Vector2 screenPosition, bool checkOcclusion)
    {
        screenPosition = Vector2.Zero;
        if (!GodotObject.IsInstanceValid(_camera) || _camera.IsPositionBehind(worldPosition)) return false;
        var projected = _camera.UnprojectPosition(worldPosition);
        var viewportSize = _camera.GetViewport().GetVisibleRect().Size;
        if (projected.X < 0 || projected.Y < 0 || projected.X > viewportSize.X || projected.Y > viewportSize.Y)
            return false;
        if (checkOcclusion && IsOccluded(worldPosition)) return false;
        screenPosition = projected;
        return true;
    }

    private bool IsOccluded(Vector3 worldPosition)
    {
        var origin = _camera.GlobalPosition;
        var query = PhysicsRayQueryParameters3D.Create(origin, worldPosition);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (!hit.TryGetValue("position", out var position)) return false;
        var hitDistance = origin.DistanceTo(position.AsVector3());
        var targetDistance = origin.DistanceTo(worldPosition);
        return hitDistance + .8f < targetDistance;
    }

    private void UpdateFloatingWidgets(double delta)
    {
        for (var index = 0; index < _floatingWidgets.Count; index++)
        {
            var widget = _floatingWidgets[index];
            if (!widget.Active) continue;
            widget.Age += delta;
            if (widget.Age >= _policy.FloatingNumberSeconds)
            {
                widget.Deactivate();
                continue;
            }

            if (widget.Target is not null && GodotObject.IsInstanceValid(widget.Target) &&
                TryProject(widget.Target.GlobalPosition + Vector3.Up * widget.AnchorHeight,
                    out var projected, checkOcclusion: false))
                widget.LastScreenPosition = projected;
            var progress = widget.Age / _policy.FloatingNumberSeconds;
            var vertical = _policy.ReducedMotion ? 0 : (float)(progress * 46);
            var horizontal = _policy.ReducedMotion ? 0 : ((widget.Sequence % 5) - 2) * 4;
            widget.Label.Position = widget.LastScreenPosition +
                                    new Vector2(
                                        horizontal - widget.Label.Size.X * .5f,
                                        -vertical - 60 - widget.Label.Size.Y -
                                        widget.StackLane * widget.Label.Size.Y * .8f);
            var alpha = progress < .72 ? 1 : (float)Math.Clamp((1 - progress) / .28, 0, 1);
            widget.Label.Modulate = new Color(1, 1, 1, alpha);
        }
    }

    private sealed class CombatantRegistration(
        string combatantId,
        string displayName,
        int currentHealth,
        int maximumHealth,
        bool boss,
        float anchorHeight,
        NameplateWidget? nameplate)
    {
        internal string CombatantId { get; } = combatantId;
        internal string DisplayName { get; set; } = displayName;
        internal int CurrentHealth { get; set; } = currentHealth;
        internal int MaximumHealth { get; set; } = maximumHealth;
        internal bool Boss { get; set; } = boss;
        internal float AnchorHeight { get; set; } = anchorHeight;
        internal NameplateWidget? Nameplate { get; } = nameplate;
        internal bool Occluded { get; set; }
        internal double OcclusionRemainingSeconds { get; set; }
    }

    private sealed partial class NameplateWidget : Control
    {
        private readonly RuntimeAnimatedBar _health;
        private readonly Label _identity;
        private readonly Label _healthText;
        private readonly Label _state;
        private Color _flashColor = Colors.White;
        private double _flashRemaining;
        private double _flashDuration;
        private bool _reducedFlash;
        private string _displayName = string.Empty;
        private int _currentHealth = -1;
        private int _maximumHealth = -1;
        private bool _boss;
        private bool _target;

        internal NameplateWidget(RuntimeUiMotionPolicy policy, bool target)
        {
            MouseFilter = MouseFilterEnum.Ignore;
            var largeText = policy.TextScale > 1.5f;
            Size = target
                ? largeText ? new Vector2(300, 68) : new Vector2(190, 47)
                : largeText ? new Vector2(250, 60) : new Vector2(166, 40);
            CustomMinimumSize = Size;

            var panel = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            panel.AddThemeStyleboxOverride("panel", RuntimeRpgUiTheme.Frame(
                policy.HighContrast ? new Color("050403ed") : new Color("15100de8"),
                target ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.BorderMuted,
                7, 5, target ? 2 : 1));
            AddChild(panel);

            var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            stack.OffsetLeft = 8;
            stack.OffsetTop = 4;
            stack.OffsetRight = -8;
            stack.OffsetBottom = -4;
            stack.AddThemeConstantOverride("separation", 2);
            AddChild(stack);

            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            stack.AddChild(row);
            _identity = new Label
            {
                ClipText = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            RuntimeRpgUiTheme.StyleTitle(_identity,
                Math.Max(9, (int)Math.Round((target ? 11 : 9) * policy.TextScale)), RuntimeRpgUiTheme.Bone);
            row.AddChild(_identity);
            _state = new Label { MouseFilter = MouseFilterEnum.Ignore };
            RuntimeRpgUiTheme.StyleBody(_state,
                Math.Max(8, (int)Math.Round(8 * policy.TextScale)),
                policy.ResolveColor(RuntimeUiSemanticColor.Alert));
            row.AddChild(_state);

            var healthRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            healthRow.AddThemeConstantOverride("separation", 5);
            stack.AddChild(healthRow);
            _health = new RuntimeAnimatedBar(policy, RuntimeUiSemanticColor.Damage, target ? 5 : 4)
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            healthRow.AddChild(_health);
            _healthText = new Label { MouseFilter = MouseFilterEnum.Ignore };
            RuntimeRpgUiTheme.StyleBody(_healthText,
                Math.Max(8, (int)Math.Round(8 * policy.TextScale)), RuntimeRpgUiTheme.Parchment);
            healthRow.AddChild(_healthText);
        }

        internal void Apply(CombatantRegistration registration, bool target)
        {
            if (string.Equals(_displayName, registration.DisplayName, StringComparison.Ordinal) &&
                _currentHealth == registration.CurrentHealth &&
                _maximumHealth == registration.MaximumHealth &&
                _boss == registration.Boss &&
                _target == target)
                return;
            _displayName = registration.DisplayName;
            _currentHealth = registration.CurrentHealth;
            _maximumHealth = registration.MaximumHealth;
            _boss = registration.Boss;
            _target = target;
            _identity.Text = registration.DisplayName;
            _state.Text = registration.CurrentHealth == 0 ? "KO" : registration.Boss ? "BOSS" : target ? "CIBLE" : string.Empty;
            _healthText.Text = $"{registration.CurrentHealth}/{registration.MaximumHealth}";
            _health.SetAuthoritativeValue(registration.CurrentHealth, registration.MaximumHealth);
            SetMeta("accessible_label",
                $"{registration.DisplayName}, {registration.CurrentHealth} sur {registration.MaximumHealth} points de vie" +
                 (registration.Boss ? ", boss" : string.Empty));
        }

        internal void Flash(Color color, bool reducedMotion)
        {
            _flashColor = color;
            _flashDuration = reducedMotion ? .38 : .18;
            _flashRemaining = _flashDuration;
            _reducedFlash = reducedMotion;
            var amount = reducedMotion ? .24f : .42f;
            Modulate = Colors.White.Lerp(_flashColor, amount);
        }

        internal void AdvanceFlash(double delta)
        {
            if (_flashRemaining <= 0) return;
            _flashRemaining = Math.Max(0, _flashRemaining - delta);
            if (_flashRemaining <= 0)
            {
                Modulate = Colors.White;
                return;
            }
            if (_reducedFlash) return;
            var intensity = (float)Math.Clamp(_flashRemaining / Math.Max(.01, _flashDuration), 0, 1);
            Modulate = Colors.White.Lerp(_flashColor, .42f * intensity);
        }
    }

    private sealed class FloatingWidget
    {
        private readonly RuntimeUiMotionPolicy _policy;

        internal FloatingWidget(RuntimeUiMotionPolicy policy)
        {
            _policy = policy;
            Label = new Label
            {
                Visible = false,
                Size = new Vector2(120 * Math.Max(1, policy.TextScale), 34 * Math.Max(1, policy.TextScale)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            Label.AddThemeFontOverride("font", RuntimeRpgUiTheme.BodyFont);
            Label.AddThemeFontSizeOverride("font_size", Math.Max(13, (int)Math.Round(16 * policy.TextScale)));
            Label.AddThemeConstantOverride("outline_size", policy.HighContrast ? 6 : 4);
            Label.AddThemeColorOverride("font_outline_color", new Color("050302e8"));
        }

        internal Label Label { get; }
        internal bool Active { get; private set; }
        internal Node3D? Target { get; private set; }
        internal CombatFeedbackAmountKind Kind { get; private set; }
        internal int Amount { get; set; }
        internal double Age { get; set; }
        internal float AnchorHeight { get; private set; }
        internal int Sequence { get; set; }
        internal int StackLane { get; private set; }
        internal Vector2 LastScreenPosition { get; set; }

        internal void Activate(
            Node3D target,
            CombatFeedbackAmountKind kind,
            int amount,
            float anchorHeight,
            int sequence,
            int stackLane)
        {
            Active = true;
            Target = target;
            Kind = kind;
            Amount = amount;
            Age = 0;
            AnchorHeight = anchorHeight;
            Sequence = sequence;
            StackLane = stackLane;
            Label.Modulate = Colors.White;
            Label.Visible = true;
            RefreshText();
        }

        internal void RefreshText()
        {
            var semantic = Kind switch
            {
                CombatFeedbackAmountKind.Damage => RuntimeUiSemanticColor.Damage,
                CombatFeedbackAmountKind.Healing => RuntimeUiSemanticColor.Healing,
                CombatFeedbackAmountKind.ResourceRestored => RuntimeUiSemanticColor.Resource,
                _ => RuntimeUiSemanticColor.Neutral,
            };
            Label.Text = Kind switch
            {
                CombatFeedbackAmountKind.Damage => $"\u2212{Amount}",
                CombatFeedbackAmountKind.Healing => $"+{Amount} PV",
                CombatFeedbackAmountKind.ResourceRestored => $"+{Amount} ressource",
                _ => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            Label.AddThemeColorOverride("font_color", _policy.ResolveColor(semantic).Lightened(.16f));
            Label.SetMeta("accessible_label", Label.Text);
        }

        internal void Deactivate()
        {
            Active = false;
            Target = null;
            Age = 0;
            StackLane = 0;
            Label.Visible = false;
            Label.Text = string.Empty;
        }
    }
}

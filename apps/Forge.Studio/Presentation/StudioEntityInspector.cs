using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Spatial;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioEntityInspector : VBoxContainer
{
    private readonly StudioSessionController _session;
    private readonly EntityId _entityId;
    private readonly List<PropertyRow> _propertyRows = [];
    private SpinBox[] _position = [];
    private SpinBox[] _rotation = [];
    private SpinBox[] _scale = [];
    private LineEdit? _tags;
    private VBoxContainer? _propertyList;
    private CheckButton? _clickEnabled;
    private OptionButton? _clickAction;
    private LineEdit? _clickMessage;
    private Label? _clickTargetLabel;
    private OptionButton? _clickTarget;
    private SpinBox? _clickDistance;
    private Label? _clickHint;

    internal event Action<EntityId, bool, string>? Applied;

    internal event Action<string>? Failed;

    internal StudioEntityInspector(
        StudioSessionController session,
        EntityId entityId,
        StudioEntityEditorSection section)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _entityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        var entity = ResolveEntity();
        var blueprint = _session.Project!.Blueprints.FirstOrDefault(item => item.Id == entity.BlueprintId);
        AddChild(StudioStyle.Label(blueprint?.DisplayName ?? "Objet sans blueprint", 14));
        AddChild(StudioStyle.Label(
            $"{blueprint?.Kind.ToString() ?? "Type inconnu"} · {_session.ActiveArea!.DisplayName}",
            10,
            StudioStyle.Muted));

        if (section is StudioEntityEditorSection.All or StudioEntityEditorSection.Transform)
        {
            BuildTransform(entity);
        }
        if (section is StudioEntityEditorSection.All or StudioEntityEditorSection.Tags)
        {
            BuildTags(entity);
        }
        if (section is StudioEntityEditorSection.All or StudioEntityEditorSection.ClickTrigger)
        {
            BuildClickTrigger(entity);
        }
        if (section is StudioEntityEditorSection.All or StudioEntityEditorSection.Properties)
        {
            BuildProperties(entity);
        }

        var technicalDetails = new VBoxContainer
        {
            Visible = false,
        };
        technicalDetails.AddThemeConstantOverride("separation", 4);
        technicalDetails.AddChild(StudioStyle.SectionLabel("IDENTITÉ TECHNIQUE"));
        technicalDetails.AddChild(StudioStyle.Label(
            $"Entité {entity.Id}\nBlueprint {entity.BlueprintId}\nVersion {entity.BlueprintVersionRequirement}",
            9,
            StudioStyle.Muted,
            wrap: true));
        var technicalToggle = StudioStyle.Button("Afficher les détails techniques", compact: true);
        technicalToggle.TooltipText = "Afficher les identifiants utiles au diagnostic avancé.";
        technicalToggle.Pressed += () =>
        {
            technicalDetails.Visible = !technicalDetails.Visible;
            technicalToggle.Text = technicalDetails.Visible
                ? "Masquer les détails techniques"
                : "Afficher les détails techniques";
        };
        AddChild(technicalToggle);
        AddChild(technicalDetails);
    }

    private void BuildTransform(EntityInstance entity)
    {
        AddChild(StudioStyle.SectionLabel("POSITION · ROTATION · ÉCHELLE"));
        var panel = StudioStyle.Panel("TransformEditor", StudioStyle.Surface, 6);
        AddChild(panel);
        var margin = StudioStyle.Margin(7, 7, 7, 7);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        margin.AddChild(column);

        var euler = EngineValueConverter.ToEngine(entity.Transform.Rotation).GetEuler() * (180f / MathF.PI);
        _position = CreateVectorRow(
            column,
            "Position",
            [entity.Transform.Position.X, entity.Transform.Position.Y, entity.Transform.Position.Z],
            -100000,
            100000,
            .05);
        _rotation = CreateVectorRow(
            column,
            "Rotation °",
            [euler.X, euler.Y, euler.Z],
            -3600,
            3600,
            .5);
        _scale = CreateVectorRow(
            column,
            "Échelle",
            [entity.Transform.Scale.X, entity.Transform.Scale.Y, entity.Transform.Scale.Z],
            .01,
            100,
            .01);

        var apply = StudioStyle.Button("Appliquer la transformation", primary: true, compact: true);
        apply.Pressed += ApplyTransform;
        column.AddChild(apply);
        column.AddChild(StudioStyle.Label(
            "Le gizmo déplace directement X, Y ou Z. Ces champs règlent aussi rotation et échelle.",
            9,
            StudioStyle.Muted,
            wrap: true));
    }

    private void BuildTags(EntityInstance entity)
    {
        AddChild(StudioStyle.SectionLabel("TAGS"));
        var panel = StudioStyle.Panel("TagEditor", StudioStyle.Surface, 6);
        AddChild(panel);
        var margin = StudioStyle.Margin(7, 7, 7, 7);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        margin.AddChild(column);
        _tags = new LineEdit
        {
            Text = string.Join(", ", entity.Tags),
            PlaceholderText = "porte, décor, interactif",
        };
        StudioStyle.ApplyLineEdit(_tags);
        column.AddChild(_tags);
        var apply = StudioStyle.Button("Appliquer les tags", compact: true);
        apply.Pressed += ApplyTags;
        column.AddChild(apply);
    }

    private void BuildClickTrigger(EntityInstance entity)
    {
        var settings = EntityClickTriggerSettings.Read(entity.Overrides);
        AddChild(StudioStyle.SectionLabel("DÉCLENCHEUR AU CLIC GAUCHE"));
        var panel = StudioStyle.Panel("ClickTriggerEditor", StudioStyle.Surface, 6);
        AddChild(panel);
        var margin = StudioStyle.Margin(7, 7, 7, 7);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        margin.AddChild(column);

        _clickEnabled = new CheckButton
        {
            Text = "Cet objet réagit au clic dans le jeu",
            ButtonPressed = settings.Enabled,
        };
        _clickEnabled.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        column.AddChild(_clickEnabled);
        column.AddChild(StudioStyle.Label("Action", 10, StudioStyle.Muted));
        _clickAction = new OptionButton();
        StudioStyle.ApplyOptionButton(_clickAction);
        _clickAction.AddItem("Afficher un message", (int)EntityClickTriggerAction.ShowMessage);
        _clickAction.AddItem("Lancer un dialogue", (int)EntityClickTriggerAction.StartDialogue);
        _clickAction.AddItem("Afficher / masquer un autre objet", (int)EntityClickTriggerAction.ToggleVisibility);
        _clickAction.Select(_clickAction.GetItemIndex((int)settings.Action));
        _clickAction.ItemSelected += _ => RefreshClickTriggerFields();
        column.AddChild(_clickAction);

        _clickMessage = new LineEdit
        {
            Text = settings.Message,
            PlaceholderText = "Texte affiché au joueur…",
        };
        StudioStyle.ApplyLineEdit(_clickMessage);
        column.AddChild(_clickMessage);
        _clickTargetLabel = StudioStyle.Label("Cible", 10, StudioStyle.Muted);
        column.AddChild(_clickTargetLabel);
        _clickTarget = new OptionButton();
        StudioStyle.ApplyOptionButton(_clickTarget);
        column.AddChild(_clickTarget);

        column.AddChild(StudioStyle.Label("Distance maximale du joueur", 10, StudioStyle.Muted));
        _clickDistance = new SpinBox
        {
            MinValue = EntityClickTriggerSettings.MinimumDistance,
            MaxValue = EntityClickTriggerSettings.MaximumDistance,
            Step = .25,
            Value = settings.MaximumDistanceMeters,
            Suffix = " m",
        };
        StudioStyle.ApplySpinBox(_clickDistance);
        column.AddChild(_clickDistance);
        _clickHint = StudioStyle.Label(string.Empty, 9, StudioStyle.Muted, wrap: true);
        column.AddChild(_clickHint);
        var apply = StudioStyle.Button("Appliquer le déclencheur", primary: true, compact: true);
        apply.Pressed += ApplyClickTrigger;
        column.AddChild(apply);
        var disable = StudioStyle.Button("Désactiver le clic sur cet objet", compact: true);
        disable.Pressed += () =>
        {
            _clickEnabled.ButtonPressed = false;
            ApplyClickTrigger();
        };
        column.AddChild(disable);
        RefreshClickTriggerFields(settings.TargetId);
    }

    private void RefreshClickTriggerFields(string? preferredTarget = null)
    {
        if (_clickAction is null || _clickMessage is null || _clickTarget is null ||
            _clickTargetLabel is null || _clickHint is null)
        {
            return;
        }
        var action = (EntityClickTriggerAction)_clickAction.GetItemId(_clickAction.Selected);
        _clickMessage.Visible = action == EntityClickTriggerAction.ShowMessage;
        _clickTarget.Visible = action != EntityClickTriggerAction.ShowMessage;
        _clickTargetLabel.Visible = _clickTarget.Visible;
        _clickTarget.Clear();
        if (action == EntityClickTriggerAction.ShowMessage)
        {
            _clickHint.Text = "Un clic gauche sur l'objet affiche immédiatement ce texte dans le HUD du jeu.";
            return;
        }

        if (action == EntityClickTriggerAction.StartDialogue)
        {
            _clickTargetLabel.Text = "Dialogue à lancer";
            foreach (var dialogue in _session.Project!.Dialogues.OrderBy(item => item.DisplayName, StringComparer.Ordinal))
            {
                AddTarget(dialogue.DisplayName, dialogue.Id.ToString());
            }
            _clickHint.Text = "Le clic ouvre le dialogue choisi avec ses lignes et ses choix.";
        }
        else
        {
            _clickTargetLabel.Text = "Objet à afficher / masquer";
            foreach (var candidate in _session.ActiveArea!.Entities.Where(item => item.Id != _entityId))
            {
                var blueprint = _session.Project!.Blueprints.FirstOrDefault(item => item.Id == candidate.BlueprintId);
                AddTarget(blueprint?.DisplayName ?? "Objet sans nom", candidate.Id.ToString());
            }
            _clickHint.Text = "Chaque clic inverse la visibilité et les collisions de l'objet cible dans la zone.";
        }

        if (_clickTarget.ItemCount == 0)
        {
            _clickTarget.AddItem("Aucune cible disponible");
            _clickTarget.SetItemMetadata(0, string.Empty);
            _clickTarget.Disabled = true;
        }
        else
        {
            _clickTarget.Disabled = false;
            var selected = Enumerable.Range(0, _clickTarget.ItemCount)
                .FirstOrDefault(index => string.Equals(
                    _clickTarget.GetItemMetadata(index).AsString(),
                    preferredTarget,
                    StringComparison.OrdinalIgnoreCase));
            _clickTarget.Select(selected);
        }

        void AddTarget(string title, string id)
        {
            var index = _clickTarget.ItemCount;
            _clickTarget.AddItem(title);
            _clickTarget.SetItemMetadata(index, id);
        }
    }

    private void BuildProperties(EntityInstance entity)
    {
        AddChild(StudioStyle.SectionLabel("PROPRIÉTÉS LOCALES"));
        _propertyList = new VBoxContainer();
        _propertyList.AddThemeConstantOverride("separation", 5);
        AddChild(_propertyList);
        foreach (var property in entity.Overrides.Properties.Where(property =>
                     !property.Key.StartsWith(EntityClickTriggerSettings.PropertyPrefix, StringComparison.Ordinal)))
        {
            AddPropertyRow(property.Key, property.Kind, property.Value);
        }

        if (_propertyRows.Count == 0)
        {
            _propertyList.AddChild(StudioStyle.Label(
                "Aucune propriété locale. L'objet utilise uniquement son blueprint.",
                9,
                StudioStyle.Muted,
                wrap: true));
        }

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 5);
        var add = StudioStyle.Button("+ Propriété", compact: true);
        add.Pressed += AddEmptyProperty;
        actions.AddChild(add);
        var apply = StudioStyle.Button("Appliquer", primary: true, compact: true);
        apply.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        apply.Pressed += ApplyProperties;
        actions.AddChild(apply);
        AddChild(actions);
    }

    private static SpinBox[] CreateVectorRow(
        Container parent,
        string title,
        IReadOnlyList<float> values,
        double minimum,
        double maximum,
        double step)
    {
        parent.AddChild(StudioStyle.Label(title, 10, StudioStyle.Muted));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        parent.AddChild(row);
        var result = new SpinBox[3];
        var axes = new[] { "X", "Y", "Z" };
        for (var index = 0; index < result.Length; index++)
        {
            var spin = new SpinBox
            {
                MinValue = minimum,
                MaxValue = maximum,
                Step = step,
                Value = values[index],
                Prefix = axes[index] + " ",
                AllowGreater = true,
                AllowLesser = true,
                UpdateOnTextChanged = false,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            StudioStyle.ApplySpinBox(spin);
            row.AddChild(spin);
            result[index] = spin;
        }
        return result;
    }

    private void AddEmptyProperty()
    {
        if (_propertyList is null)
        {
            return;
        }

        foreach (Node child in _propertyList.GetChildren())
        {
            if (child is Label)
            {
                _propertyList.RemoveChild(child);
                child.QueueFree();
            }
        }

        var suffix = 1;
        var key = "property.new";
        while (_propertyRows.Any(row => string.Equals(row.Key.Text, key, StringComparison.Ordinal)))
        {
            key = $"property.new{++suffix}";
        }
        AddPropertyRow(key, EntityPropertyValueKind.String, string.Empty);
    }

    private void AddPropertyRow(string key, EntityPropertyValueKind kind, string value)
    {
        if (_propertyList is null)
        {
            return;
        }

        var panel = StudioStyle.Panel("PropertyRow", StudioStyle.Surface, 5);
        var margin = StudioStyle.Margin(6, 6, 6, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);

        var keyRow = new HBoxContainer();
        keyRow.AddThemeConstantOverride("separation", 4);
        column.AddChild(keyRow);
        var keyEdit = new LineEdit { Text = key, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(keyEdit);
        keyRow.AddChild(keyEdit);
        var remove = StudioStyle.Button("×", compact: true);
        remove.CustomMinimumSize = new Vector2(28, 28);
        keyRow.AddChild(remove);

        var valueRow = new HBoxContainer();
        valueRow.AddThemeConstantOverride("separation", 4);
        column.AddChild(valueRow);
        var kindSelect = new OptionButton { CustomMinimumSize = new Vector2(82, 0) };
        StudioStyle.ApplyOptionButton(kindSelect);
        AddKind(kindSelect, "Texte", EntityPropertyValueKind.String);
        AddKind(kindSelect, "Oui/non", EntityPropertyValueKind.Boolean);
        AddKind(kindSelect, "Entier", EntityPropertyValueKind.Integer);
        AddKind(kindSelect, "Nombre", EntityPropertyValueKind.Number);
        kindSelect.Select((int)kind);
        valueRow.AddChild(kindSelect);
        var valueEdit = new LineEdit { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(valueEdit);
        valueRow.AddChild(valueEdit);

        var row = new PropertyRow(panel, keyEdit, kindSelect, valueEdit);
        remove.Pressed += () =>
        {
            _propertyRows.Remove(row);
            _propertyList.RemoveChild(panel);
            panel.QueueFree();
        };
        _propertyRows.Add(row);
        _propertyList.AddChild(panel);
    }

    private static void AddKind(OptionButton select, string title, EntityPropertyValueKind kind)
    {
        select.AddItem(title, (int)kind);
        select.SetItemMetadata(select.ItemCount - 1, (int)kind);
    }

    private void ApplyTransform()
    {
        try
        {
            var quaternion = Quaternion.FromEuler(new Vector3(
                DegreesToRadians((float)_rotation[0].Value),
                DegreesToRadians((float)_rotation[1].Value),
                DegreesToRadians((float)_rotation[2].Value)));
            var transform = new TransformValue(
                new Vector3Value(
                    (float)_position[0].Value,
                    (float)_position[1].Value,
                    (float)_position[2].Value),
                new QuaternionValue(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W),
                new Vector3Value(
                    (float)_scale[0].Value,
                    (float)_scale[1].Value,
                    (float)_scale[2].Value));
            Report(_session.TransformEntity(_entityId, transform), rebuildVisual: false);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            Failed?.Invoke(exception.Message);
        }
    }

    private void ApplyTags()
    {
        var tags = (_tags?.Text ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Report(_session.SetEntityTags(_entityId, tags), rebuildVisual: true);
    }

    private void ApplyClickTrigger()
    {
        if (_clickEnabled is null || _clickAction is null || _clickMessage is null ||
            _clickTarget is null || _clickDistance is null)
        {
            return;
        }
        try
        {
            var action = (EntityClickTriggerAction)_clickAction.GetItemId(_clickAction.Selected);
            var target = _clickTarget.Selected >= 0
                ? _clickTarget.GetItemMetadata(_clickTarget.Selected).AsString()
                : string.Empty;
            var settings = new EntityClickTriggerSettings(
                _clickEnabled.ButtonPressed,
                action,
                _clickMessage.Text,
                target,
                (float)_clickDistance.Value);
            var overrides = EntityClickTriggerSettings.Merge(ResolveEntity().Overrides, settings);
            Report(_session.SetEntityOverrides(_entityId, overrides), rebuildVisual: false);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            Failed?.Invoke(exception.Message);
        }
    }

    private void ApplyProperties()
    {
        try
        {
            if (_propertyRows.Any(row => row.Key.Text.Trim().StartsWith(
                    EntityClickTriggerSettings.PropertyPrefix,
                    StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Utilisez la rubrique Déclencheur au clic pour les propriétés interaction.click.*.");
            }
            var clickProperties = ResolveEntity().Overrides.Properties.Where(property =>
                property.Key.StartsWith(EntityClickTriggerSettings.PropertyPrefix, StringComparison.Ordinal));
            var properties = _propertyRows.Select(row => new EntityPropertyOverride(
                    row.Key.Text.Trim(),
                    (EntityPropertyValueKind)(int)row.Kind.GetItemMetadata(row.Kind.Selected),
                    row.Value.Text))
                .Concat(clickProperties)
                .ToArray();
            Report(
                _session.SetEntityOverrides(
                    _entityId,
                    new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, properties)),
                rebuildVisual: true);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            Failed?.Invoke(exception.Message);
        }
    }

    private void Report(StudioActionResult result, bool rebuildVisual)
    {
        if (!result.Succeeded)
        {
            Failed?.Invoke(result.Message);
            return;
        }
        Applied?.Invoke(_entityId, rebuildVisual, result.Message);
    }

    private EntityInstance ResolveEntity() =>
        _session.ActiveArea?.Entities.FirstOrDefault(entity => entity.Id == _entityId)
        ?? throw new InvalidOperationException("The selected entity no longer exists in the active area.");

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;

    private sealed record PropertyRow(
        PanelContainer Panel,
        LineEdit Key,
        OptionButton Kind,
        LineEdit Value);
}

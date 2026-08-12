#if TOOLS
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Toolset.Editor;

public partial class RecoveryWindow : Window
{
    private Func<IReadOnlyList<RecoverySnapshotInfo>>? _list;
    private Func<CrashRecoveryPolicy>? _policy;
    private Func<RecoverySessionInspection?>? _session;
    private Func<RecoverySnapshotInfo, RecoveryRestoreSelection, RecoveryRestorePreview?>? _preview;
    private Func<RecoverySnapshotInfo, RecoveryRestoreSelection, RecoveryComparison?>? _restore;
    private Func<CrashRecoveryPolicy, bool>? _configure;
    private Action? _capture;
    private Action? _clear;
    private Action<Window>? _brand;
    private ItemList? _items;
    private SpinBox? _interval;
    private SpinBox? _retention;
    private CheckBox? _offer;
    private CheckBox? _quarantine;
    private CheckBox? _compareBeforeRestore;
    private RichTextLabel? _sessionStatus;
    private RichTextLabel? _comparison;
    private Label? _status;
    private readonly Dictionary<RecoveryRestoreSection, CheckBox> _sections = [];
    private IReadOnlyList<RecoverySnapshotInfo> _snapshots = [];
    private string? _previewedKey;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;

    public void Initialize(
        Func<IReadOnlyList<RecoverySnapshotInfo>> list,
        Func<CrashRecoveryPolicy> policy,
        Func<RecoverySessionInspection?> session,
        Func<RecoverySnapshotInfo, RecoveryRestoreSelection, RecoveryRestorePreview?> preview,
        Func<RecoverySnapshotInfo, RecoveryRestoreSelection, RecoveryComparison?> restore,
        Func<CrashRecoveryPolicy, bool> configure,
        Action capture,
        Action clear,
        Action<Window> brand)
    {
        _list = list;
        _policy = policy;
        _session = session;
        _preview = preview;
        _restore = restore;
        _configure = configure;
        _capture = capture;
        _clear = clear;
        _brand = brand;
        Title = "Récupération · OpenRPG Forge";
        MinSize = new Vector2I(1180, 760);
        Size = MinSize;
        Transient = true;
        Unresizable = true;
        Visible = false;
        BuildUi();

        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r060-recovery-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null)
        {
            _capturePath = captureArgument["--r060-recovery-ui-capture=".Length..].Trim();
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1.5)
        {
            Open();
            PrepareCapture();
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 4) return;
        if (string.IsNullOrWhiteSpace(_capturePath))
        {
            GD.PushError("OPENRPG_FORGE_R060_RECOVERY_UI_CAPTURE_FAILED reason=empty-path");
            _capturePath = null;
            SetProcess(false);
            GetTree().Quit(1);
            return;
        }
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_R060_RECOVERY_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        PopupCentered(MinSize);
        _brand?.Invoke(this);
    }

    internal void RefreshForSmokeTest() => Refresh();

    private void BuildUi()
    {
        var panel = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        panel.AddThemeStyleboxOverride("panel", Card("0d0915", "62458a"));
        AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 22);
        panel.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var heading = new Label
        {
            Text = "SAUVEGARDE AUTOMATIQUE & RÉCUPÉRATION",
            ThemeTypeVariation = "HeaderSmall",
        };
        heading.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(heading);
        root.AddChild(new Label
        {
            Text = "Préserver le travail local, détecter une interruption puis comparer avant toute restauration explicite.",
        });

        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        root.AddChild(columns);
        var policyCard = Column(columns, "POLITIQUE VERSIONNÉE", 370);
        _interval = Spin(10, 3600, 60, 10);
        _retention = Spin(1, 100, 12, 1);
        policyCard.AddChild(Row("Intervalle autosave (s)", _interval));
        policyCard.AddChild(Row("Instantanés conservés", _retention));
        _offer = new CheckBox { Text = "Proposer la récupération à l’ouverture" };
        _quarantine = new CheckBox { Text = "Mettre les fichiers corrompus en quarantaine" };
        _compareBeforeRestore = new CheckBox { Text = "Exiger un aperçu avant restauration" };
        policyCard.AddChild(_offer);
        policyCard.AddChild(_quarantine);
        policyCard.AddChild(_compareBeforeRestore);
        var savePolicy = new Button { Text = "Valider et enregistrer la politique" };
        savePolicy.Pressed += ConfigurePolicy;
        policyCard.AddChild(savePolicy);
        policyCard.AddChild(new Label
        {
            Text = "La configuration suit la commande canonique et reste annulable. Les snapshots n’écrivent jamais project.json.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        policyCard.AddChild(new HSeparator());
        policyCard.AddChild(new Label { Text = "ÉTAT DE LA SESSION" });
        _sessionStatus = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            CustomMinimumSize = new Vector2(0, 115),
        };
        policyCard.AddChild(_sessionStatus);

        var recoveryCard = Column(columns, "INSTANTANÉS & RESTAURATION SÉLECTIVE", 700);
        _items = new ItemList { CustomMinimumSize = new Vector2(0, 205) };
        _items.ItemSelected += _ => InvalidatePreview();
        recoveryCard.AddChild(_items);
        recoveryCard.AddChild(new Label { Text = "Sections à restaurer" });
        var sectionGrid = new GridContainer { Columns = 3 };
        foreach (var section in new[]
                 {
                     RecoveryRestoreSection.ProjectAndModule,
                     RecoveryRestoreSection.Blueprints,
                     RecoveryRestoreSection.Areas,
                     RecoveryRestoreSection.Dialogues,
                     RecoveryRestoreSection.Quests,
                 })
        {
            var check = new CheckBox { Text = SectionName(section), ButtonPressed = true };
            check.Toggled += _ => InvalidatePreview();
            _sections.Add(section, check);
            sectionGrid.AddChild(check);
        }
        recoveryCard.AddChild(sectionGrid);
        _comparison = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 145),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        recoveryCard.AddChild(_comparison);
        var snapshotActions = new HBoxContainer();
        var capture = new Button { Text = "Créer un instantané" };
        capture.Pressed += () => { _capture?.Invoke(); Refresh(); };
        snapshotActions.AddChild(capture);
        var compare = new Button { Text = "Comparer la sélection" };
        compare.Pressed += Preview;
        snapshotActions.AddChild(compare);
        var restore = new Button { Text = "Restaurer la sélection" };
        restore.Pressed += Restore;
        snapshotActions.AddChild(restore);
        var clear = new Button { Text = "Effacer snapshots et journal" };
        clear.Pressed += () => { _clear?.Invoke(); Refresh(); };
        snapshotActions.AddChild(clear);
        recoveryCard.AddChild(snapshotActions);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeColorOverride("font_color", new Color("79d5ad"));
        root.AddChild(_status);
        var footer = new HBoxContainer();
        footer.AddChild(new Label
        {
            Text = "La restauration charge le document en mémoire ; Enregistrer reste une action séparée.",
        });
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        footer.AddChild(close);
        root.AddChild(footer);
    }

    private void Refresh()
    {
        var policy = _policy?.Invoke();
        if (policy is null)
        {
            _status!.Text = "✕ Aucun projet actif · politique indisponible";
            return;
        }
        _interval!.Value = policy.AutosaveIntervalSeconds;
        _retention!.Value = policy.RetainedSnapshots;
        _offer!.ButtonPressed = policy.OfferRecoveryOnOpen;
        _quarantine!.ButtonPressed = policy.QuarantineCorruptFiles;
        _compareBeforeRestore!.ButtonPressed = policy.CompareBeforeRestore;
        _snapshots = _list?.Invoke() ?? [];
        _items!.Clear();
        foreach (var item in _snapshots)
            _items.AddItem($"{item.CreatedAt.ToLocalTime():g} · révision {item.Revision} · SHA-256 {item.ProjectSha256[..12]}");
        if (_snapshots.Count > 0) _items.Select(0);
        _comparison!.Text = _snapshots.Count == 0
            ? "[color=#a99bbb]État vide · aucun instantané récupérable. Créez-en un après une modification non enregistrée.[/color]"
            : "[color=#a99bbb]Sélectionnez les sections puis comparez. Aucune restauration n’est automatique.[/color]";
        var session = _session?.Invoke();
        var sessionDetail = session is null ? string.Empty : SessionDetail(session.Code);
        _sessionStatus!.Text = session is null
            ? "[color=#a99bbb]Session locale non initialisée.[/color]"
            : session.PreviousSessionWasUnclean
                ? $"[color=#ffb16f]⚠ Session précédente interrompue[/color]\n{sessionDetail}\nUUID session · {session.Current.Id:D}"
                : $"[color=#79d5ad]✓ Aucune interruption précédente détectée[/color]\n{sessionDetail}\nUUID session · {session.Current.Id:D}";
        _previewedKey = null;
        _status!.Text = $"✓ Politique chargée · schéma v{policy.SchemaVersion} · {_snapshots.Count} instantané(s) vérifié(s)";
    }

    private void ConfigurePolicy()
    {
        var current = _policy?.Invoke();
        if (current is null) return;
        try
        {
            var candidate = new CrashRecoveryPolicy(current.Id, CrashRecoveryPolicy.CurrentSchemaVersion,
                (int)_interval!.Value, (int)_retention!.Value, _offer!.ButtonPressed,
                _quarantine!.ButtonPressed, _compareBeforeRestore!.ButtonPressed, "creator-r060-ui");
            _status!.Text = _configure?.Invoke(candidate) == true
                ? "✓ Politique appliquée par commande · undo/redo disponible"
                : "✕ Politique refusée avant mutation";
        }
        catch (ArgumentException exception)
        {
            _status!.Text = $"✕ recovery.policy.invalid · {exception.Message}";
        }
    }

    private void Preview()
    {
        var selected = SelectedSnapshot();
        if (selected is null) return;
        RecoveryRestoreSelection selection;
        try { selection = BuildSelection(); }
        catch (ArgumentException exception) { _status!.Text = $"✕ recovery.selection.invalid · {exception.Message}"; return; }
        var preview = _preview?.Invoke(selected, selection);
        if (preview is null)
        {
            _status!.Text = "✕ Aperçu refusé · hash, format ou références invalides";
            return;
        }
        _previewedKey = PreviewKey(selected, selection);
        _comparison!.Text = $"[color=#79d5ad]✓ Aperçu validé · {string.Join(" · ", preview.Comparison.SelectedSections ?? [])}[/color]\n" +
                            $"Zones {preview.Comparison.CurrentAreas} → {preview.Comparison.SnapshotAreas} · " +
                            $"entités {preview.Comparison.CurrentEntities} → {preview.Comparison.SnapshotEntities} · " +
                            $"blueprints {preview.Comparison.CurrentBlueprints} → {preview.Comparison.SnapshotBlueprints}\n" +
                            $"SHA-256 {preview.Comparison.CurrentSha256[..12]} → {preview.Comparison.SnapshotSha256[..12]}";
        _status!.Text = preview.Comparison.Changed
            ? "✓ Diff calculé · restaurez explicitement pour appliquer en mémoire"
            : "✓ La sélection est identique au document actif";
    }

    private void Restore()
    {
        var selected = SelectedSnapshot();
        if (selected is null) return;
        RecoveryRestoreSelection selection;
        try { selection = BuildSelection(); }
        catch (ArgumentException exception) { _status!.Text = $"✕ recovery.selection.invalid · {exception.Message}"; return; }
        if (_compareBeforeRestore!.ButtonPressed && _previewedKey != PreviewKey(selected, selection))
        {
            _status!.Text = "✕ recovery.preview.required · comparez cette sélection avant de la restaurer";
            return;
        }
        var comparison = _restore?.Invoke(selected, selection);
        _status!.Text = comparison is null
            ? "✕ Restauration refusée avant mutation"
            : "✓ Restauration transactionnelle appliquée en mémoire · undo/redo disponible";
    }

    private RecoverySnapshotInfo? SelectedSnapshot()
    {
        var index = _items!.GetSelectedItems().FirstOrDefault(-1);
        if (index >= 0 && index < _snapshots.Count) return _snapshots[index];
        _status!.Text = "✕ recovery.snapshot.selection_missing · sélectionnez un instantané";
        return null;
    }

    private RecoveryRestoreSelection BuildSelection()
    {
        var sections = _sections.Where(item => item.Value.ButtonPressed)
            .Aggregate(RecoveryRestoreSection.None, (current, item) => current | item.Key);
        return new RecoveryRestoreSelection(_policy!.Invoke().Id, RecoveryRestoreSelection.CurrentSchemaVersion,
            sections, "creator-r060-ui");
    }

    private void InvalidatePreview()
    {
        _previewedKey = null;
        if (_comparison is not null)
            _comparison.Text = "[color=#a99bbb]Sélection modifiée · recalculez l’aperçu avant restauration.[/color]";
    }

    private void PrepareCapture()
    {
        foreach (var section in _sections.Values) section.ButtonPressed = true;
        _comparison!.Text = _snapshots.Count == 0
            ? "[color=#a99bbb]État vide explicite · aucune récupération en attente. Les cinq sections peuvent être restaurées indépendamment.[/color]"
            : _comparison.Text;
        _status!.Text = "✓ R060 prêt · autosave versionné, session surveillée, comparaison et restauration sélective";
    }

    private static string PreviewKey(RecoverySnapshotInfo snapshot, RecoveryRestoreSelection selection) =>
        $"{snapshot.Id}:{(int)selection.Sections}";

    private static string SectionName(RecoveryRestoreSection section) => section switch
    {
        RecoveryRestoreSection.ProjectAndModule => "Projet & module",
        RecoveryRestoreSection.Blueprints => "Blueprints",
        RecoveryRestoreSection.Areas => "Zones",
        RecoveryRestoreSection.Dialogues => "Dialogues",
        RecoveryRestoreSection.Quests => "Quêtes",
        _ => section.ToString(),
    };

    private static string SessionDetail(string code) => code switch
    {
        "recovery.session.interrupted" => "La fermeture propre de la session précédente n’a pas été enregistrée.",
        "recovery.session.marker_corrupt" => "Le marqueur précédent est illisible ; la récupération reste proposée.",
        "recovery.session.clean" => "La session précédente s’est terminée proprement.",
        "recovery.session.open" => "Une session précédente est encore marquée comme ouverte.",
        _ => "Première ouverture suivie dans cet espace de travail.",
    };

    private static VBoxContainer Column(Container parent, string heading, float minimumWidth)
    {
        var box = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(minimumWidth, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        box.AddThemeConstantOverride("separation", 9);
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171122", "46325f"));
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 14);
        panel.AddChild(margin);
        margin.AddChild(box);
        var title = new Label { Text = heading };
        title.AddThemeColorOverride("font_color", new Color("ffb16f"));
        box.AddChild(title);
        parent.AddChild(panel);
        return box;
    }

    private static HBoxContainer Row(string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        row.AddChild(control);
        return row;
    }

    private static SpinBox Spin(double min, double max, double value, double step) => new()
    {
        MinValue = min,
        MaxValue = max,
        Value = value,
        Step = step,
        CustomMinimumSize = new Vector2(120, 0),
    };

    private static StyleBoxFlat Card(string background, string border) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
    };
}
#endif

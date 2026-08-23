using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptFoundationPageUx6F : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6FEvidenceSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _sourceChoice;
    private readonly LineEdit _newSourcePath;
    private readonly Button _createSource;
    private readonly Button _reloadSources;
    private readonly Label _selection;
    private readonly Label _identity;
    private readonly OptionButton _distribution;
    private readonly CheckBox _required;
    private readonly LineEdit _dependencies;
    private readonly CodeEdit _source;
    private readonly OptionButton _bindingChoice;
    private readonly Button _addBinding;
    private readonly Button _removeBinding;
    private readonly LineEdit _component;
    private readonly LineEdit _handler;
    private readonly SpinBox _priority;
    private readonly LineEdit _condition;
    private readonly OptionButton _authority;
    private readonly CheckBox _enabled;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _console;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _compile;
    private readonly Button _sandbox;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;

    private IReadOnlyList<StudioUx6FSourceChoice> _choices = [];
    private StudioUx6FDraft? _draft;
    private List<StudioUx6FBindingDraft> _bindings = [];
    private int _bindingIndex = -1;
    private bool _explicitSource;
    private bool _updating;
    private bool _previewReady;
    private StudioUx6FPreview? _previewResult;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ScriptSourceChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSource && _draft is not null;
    internal bool PreviewReady => _previewReady && _previewResult is
    { Succeeded: true, CanApply: true, CanonicalDryRun: true, LegacySourcesProtected: true };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6F();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool LegacySourcesVisible => _choices.Any(item => item.Kind == StudioUx6FSourceKind.LegacyProtected);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6F(this));
            return text.Contains("ForgeScript", StringComparison.Ordinal) &&
                   text.Contains("Source canonique", StringComparison.Ordinal) &&
                   text.Contains("Compilateur", StringComparison.Ordinal) &&
                   text.Contains("Événements", StringComparison.Ordinal) &&
                   text.Contains("Fonction d’entrée", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   !ContainsUuidUx6F(text);
        }
    }

    internal StudioForgeScriptFoundationPageUx6F(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.009f, .015f, .024f, .982f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6FForgeScriptFoundation", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("ForgeScript · fondation de source", 19));
        headingText.AddChild(StudioStyle.Label(
            "Source canonique C-like · Compilateur intégré · Événements par objet · Fonction d’entrée main().",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var sourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceChoice = StudioUx2EnvBControls.Options();
        _sourceChoice.CustomMinimumSize = new Vector2(430, 34);
        _sourceChoice.ItemSelected += SelectSourceUx6F;
        sourceRow.AddChild(FieldUx6F("Source · sélection explicite", _sourceChoice, 430));
        _newSourcePath = StudioUx2EnvBControls.Text("scripts/nouveau-script.as");
        _newSourcePath.CustomMinimumSize = new Vector2(300, 34);
        sourceRow.AddChild(FieldUx6F("Nouvelle source canonique", _newSourcePath, 300));
        _createSource = StudioStyle.Button("Créer en mémoire", compact: true);
        _createSource.CustomMinimumSize = new Vector2(150, 34);
        _createSource.Pressed += CreateSourceUx6F;
        sourceRow.AddChild(_createSource);
        _reloadSources = StudioStyle.Button("Recharger la liste", compact: true);
        _reloadSources.CustomMinimumSize = new Vector2(150, 34);
        _reloadSources.Pressed += RefreshSourcesUx6F;
        sourceRow.AddChild(_reloadSources);
        root.AddChild(sourceRow);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une source .as. Les anciens .forge restent visibles mais protégés.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var body = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 430),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 9);
        root.AddChild(body);

        var left = PanelColumnUx6F(body, 285);
        left.AddChild(StudioStyle.Label("IDENTITÉ & GOUVERNANCE", 10, StudioStyle.Accent));
        _identity = StudioStyle.Label("Aucune source active.", 10, StudioStyle.Muted, wrap: true);
        left.AddChild(_identity);
        left.AddChild(StudioStyle.Label("Langage", 9, StudioStyle.Muted));
        left.AddChild(StudioStyle.Label("AngelScript 2.38 · API ForgeScript 1.0.0", 10,
            StudioStyle.Success, wrap: true));
        _distribution = StudioUx2EnvBControls.Options(
            "Authoring uniquement", "Source + bytecode", "Source uniquement");
        left.AddChild(FieldUx6F("Distribution", _distribution, 250));
        _required = StudioUx2EnvBControls.Check("Requise au runtime");
        left.AddChild(_required);
        _dependencies = StudioUx2EnvBControls.Text("scripts/dependance.as");
        left.AddChild(FieldUx6F("Dépendances .as (séparées par ;)", _dependencies, 250));
        left.AddChild(StudioStyle.Label(
            "Fichiers et métadonnées préservés.\nIdentités et capacités cachées conservées.\nAucun .forge historique n’est migré.",
            9, StudioStyle.Muted, wrap: true));

        var center = PanelColumnUx6F(body, 610, expand: true);
        center.AddChild(StudioStyle.Label("CODE C-LIKE · FONCTION D’ENTRÉE", 10, StudioStyle.Accent));
        _source = new CodeEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(590, 350),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        var highlighter = new CodeHighlighter
        {
            SymbolColor = new Color("89ddff"),
            NumberColor = new Color("f78c6c"),
            FunctionColor = new Color("82aaff"),
        };
        foreach (var keyword in new[]
                 {
                     "void", "bool", "int", "uint", "float", "double", "string", "if", "else", "for",
                     "while", "return", "const", "in", "true", "false",
                 })
            highlighter.AddKeywordColor(keyword, new Color("c792ea"));
        _source.SyntaxHighlighter = highlighter;
        center.AddChild(_source);
        center.AddChild(StudioStyle.Label(
            "Le dry-run compile la copie en mémoire. Enregistrer écrit atomiquement .as + .meta.json ; aucun accès fichier, réseau, processus, moteur ou .NET n’est exposé au script.",
            9, StudioStyle.Muted, wrap: true));

        var right = PanelColumnUx6F(body, 365);
        right.AddChild(StudioStyle.Label("ÉVÉNEMENTS PAR OBJET", 10, StudioStyle.Accent));
        var bindingHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bindingChoice = StudioUx2EnvBControls.Options();
        _bindingChoice.CustomMinimumSize = new Vector2(190, 34);
        _bindingChoice.ItemSelected += SelectBindingUx6F;
        bindingHeader.AddChild(_bindingChoice);
        _addBinding = StudioStyle.Button("Ajouter", compact: true);
        _addBinding.Pressed += AddBindingUx6F;
        bindingHeader.AddChild(_addBinding);
        _removeBinding = StudioStyle.Button("Retirer", compact: true);
        _removeBinding.Pressed += RemoveBindingUx6F;
        bindingHeader.AddChild(_removeBinding);
        right.AddChild(bindingHeader);
        right.AddChild(StudioStyle.Label(
            "Contrat disponible · créature endommagée · charge entière typée.",
            9, StudioStyle.Success, wrap: true));
        _component = StudioUx2EnvBControls.Text("creature.combat");
        right.AddChild(FieldUx6F("Composant propriétaire", _component, 330));
        _handler = StudioUx2EnvBControls.Text("OnDamaged");
        right.AddChild(FieldUx6F("Fonction handler", _handler, 330));
        _priority = StudioUx2EnvBControls.Number(-10_000, 10_000, 1);
        right.AddChild(FieldUx6F("Priorité", _priority, 160));
        _condition = StudioUx2EnvBControls.Text("optionnelle");
        right.AddChild(FieldUx6F("Condition d’activation", _condition, 330));
        _authority = StudioUx2EnvBControls.Options("Locale", "Propriétaire", "Serveur");
        right.AddChild(FieldUx6F("Autorité", _authority, 230));
        _enabled = StudioUx2EnvBControls.Check("Binding actif");
        right.AddChild(_enabled);
        right.AddChild(StudioStyle.Label("DIAGNOSTICS LOCALISÉS", 10, StudioStyle.Accent));
        var diagnosticScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 105),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        diagnosticScroll.AddChild(_diagnostics);
        right.AddChild(diagnosticScroll);

        _console = StudioStyle.Label(
            "CONSOLE · prévisualisez, compilez ou testez la sandbox ; ces trois actions ne modifient aucune source.",
            10, StudioStyle.Muted, wrap: true);
        _console.CustomMinimumSize = new Vector2(0, 70);
        root.AddChild(_console);
        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        _preview = StudioStyle.Button("Aperçu sec", compact: true);
        _preview.Pressed += PreviewUx6F;
        _compile = StudioStyle.Button("Compiler", compact: true);
        _compile.Pressed += CompileUx6F;
        _sandbox = StudioStyle.Button("Tester sandbox", compact: true);
        _sandbox.Pressed += SandboxUx6F;
        _apply = StudioStyle.Button("Appliquer et enregistrer", primary: true, compact: true);
        _apply.Pressed += ApplyUx6F;
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += UndoUx6F;
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += RedoUx6F;
        foreach (var button in new[] { _preview, _compile, _sandbox, _apply, _undo, _redo })
        {
            button.CustomMinimumSize = new Vector2(button == _apply ? 185 : 126, 35);
            actions.AddChild(button);
        }
        root.AddChild(actions);

        WireInvalidationUx6F();
        ResetControlsUx6F();
        UpdateButtonsUx6F();
    }

    internal void Open() => OpenBoundUx6F(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx6F(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null) return;
        if (_evidenceSession is not null || _evidenceRoot is not null)
            ReleaseEvidenceUx6F();
        else
        {
            _session.CancelUx6FApproval();
            _session = _sourceSession;
        }
        Visible = false;
        ResetControlsUx6F();
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        ReleaseEvidenceUx6F();
        Visible = false;
        ResetControlsUx6F();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        CloseEvidence();
        if (_sourceSession.Project is null || _sourceSession.History is null ||
            _sourceSession.Session is null) return false;
        _sourceSnapshot = CaptureSourceUx6F();
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-f-{Guid.NewGuid():N}");
        _evidenceSession = new StudioSessionController();
        _evidenceReleased = false;
        var created = ProjectSession.Create(_evidenceRoot, _sourceSession.Project);
        if (!created.Succeeded)
        {
            ReleaseEvidenceUx6F();
            return false;
        }
        StudioSessionController.CopyLegacyScriptsUx6F(_sourceSession.Session.RootDirectory, _evidenceRoot);
        new ForgeScriptSourceStore(_evidenceRoot).Save(StudioSessionController.EvidenceDocumentUx6F());
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
        {
            ReleaseEvidenceUx6F();
            return false;
        }
        OpenBoundUx6F(_evidenceSession, evidence: true);
        var sourceIndex = _choices.ToList().FindIndex(item =>
            string.Equals(item.RelativePath, StudioSessionController.EvidenceDocumentUx6F().RelativePath,
                StringComparison.Ordinal));
        if (sourceIndex < 0)
        {
            CloseEvidence();
            return false;
        }
        _sourceChoice.Select(sourceIndex + 1);
        SelectSourceUx6F(sourceIndex + 1);
        if (_draft is null)
        {
            CloseEvidence();
            return false;
        }
        _source.Text = _source.Text.Replace("say(\"ready\");",
            "say(\"ready\");\n    int studio_value = 17;", StringComparison.Ordinal);
        PreviewUx6F();
        var ready = PreviewReady && UsesIsolatedEvidence && HasExplicitSelection &&
                    LegacySourcesVisible && ShowsHumanSurface && SourceMatchesUx6F();
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        var source = CaptureSourceUx6F();
        try
        {
            if (!PrepareEvidence("ux6-f-script-foundation") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !PreviewReady || _draft is null) return false;
            var candidate = ReadDraftUx6F();
            if (candidate is null) return false;
            var expectedSource = candidate.Source;
            var applied = _session.ApplyApprovedScriptSourceUx6F(candidate);
            var appliedDocument = new ForgeScriptSourceStore(_evidenceRoot!).Load(candidate.RelativePath);
            var undone = _session.UndoScriptSourceUx6F();
            var undoDocument = new ForgeScriptSourceStore(_evidenceRoot!).Load(candidate.RelativePath);
            var redone = _session.RedoScriptSourceUx6F();
            var redoDocument = new ForgeScriptSourceStore(_evidenceRoot!).Load(candidate.RelativePath);
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded &&
                            reopened.TryOpenCanonicalSourceUx6F(candidate.RelativePath, out var reopenedDraft, out _) &&
                            reopenedDraft?.Source == expectedSource;
            reopened.Close();
            var valid = applied.Succeeded && appliedDocument.Source == expectedSource && undone.Succeeded &&
                        !undoDocument.Source.Contains("studio_value", StringComparison.Ordinal) && redone.Succeeded &&
                        redoDocument.Source == expectedSource && persisted && SourceMatchesUx6F(source);
            if (!valid)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_F_UI_TRANSACTION_FAILED " +
                            $"apply={applied.Succeeded.ToString().ToLowerInvariant()} " +
                            $"undo={undone.Succeeded.ToString().ToLowerInvariant()} " +
                            $"redo={redone.Succeeded.ToString().ToLowerInvariant()} " +
                            $"persisted={persisted.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatchesUx6F(source).ToString().ToLowerInvariant()}");
            return valid;
        }
        finally
        {
            CloseEvidence();
        }
    }

    private void OpenBoundUx6F(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6FApproval();
        ResetControlsUx6F();
        RefreshSourcesUx6F();
        _selection.Text = evidence
            ? "Copie temporaire isolée · sources et cache séparés · module source strictement inchangé."
            : "Choisissez explicitement une source .as. Les anciens .forge restent visibles mais protégés.";
        Visible = true;
        MoveToFront();
        UpdateBoundsUx6F();
        UpdateButtonsUx6F();
    }

    private void RefreshSourcesUx6F()
    {
        _choices = _session.ScriptSourceChoicesUx6F();
        _updating = true;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir explicitement une source…");
        foreach (var item in _choices) _sourceChoice.AddItem(item.Label);
        _sourceChoice.Select(0);
        _updating = false;
        _explicitSource = false;
        SetStatusUx6F($"{_choices.Count(item => item.Kind == StudioUx6FSourceKind.Canonical)} source(s) canonique(s) · " +
                      $"{_choices.Count(item => item.Kind == StudioUx6FSourceKind.LegacyProtected)} historique(s) protégé(s).",
            false);
        UpdateButtonsUx6F();
    }

    private void SelectSourceUx6F(long selected)
    {
        if (_updating) return;
        ResetDraftUx6F();
        if (selected <= 0 || selected > _choices.Count)
        {
            UpdateButtonsUx6F();
            return;
        }
        var choice = _choices[(int)selected - 1];
        if (choice.Kind != StudioUx6FSourceKind.Canonical)
        {
            _selection.Text = choice.Detail;
            SetStatusUx6F(choice.Detail, true);
            UpdateButtonsUx6F();
            return;
        }
        if (!_session.TryOpenCanonicalSourceUx6F(choice.RelativePath, out var draft, out var message) ||
            draft is null)
        {
            SetStatusUx6F(message, true);
            return;
        }
        _explicitSource = true;
        PopulateDraftUx6F(draft);
        _selection.Text = message;
        SetStatusUx6F(message, false);
    }

    private void CreateSourceUx6F()
    {
        ResetDraftUx6F();
        if (!_session.TryCreateCanonicalSourceUx6F(_newSourcePath.Text, out var draft, out var message) ||
            draft is null)
        {
            SetStatusUx6F(message, true);
            return;
        }
        _explicitSource = true;
        PopulateDraftUx6F(draft);
        _selection.Text = message;
        SetStatusUx6F(message, false);
    }

    private void PopulateDraftUx6F(StudioUx6FDraft draft)
    {
        _updating = true;
        _draft = draft;
        _bindings = draft.Bindings.ToList();
        _identity.Text = $"{HumanPathUx6F(draft.RelativePath)}\nSchéma source v{draft.SchemaVersion} · révision {draft.BaseRevision}";
        _distribution.Select((int)draft.Distribution);
        _required.ButtonPressed = draft.RequiredAtRuntime;
        _dependencies.Text = string.Join("; ", draft.Dependencies);
        _source.Editable = true;
        _source.Text = draft.Source;
        RebuildBindingsUx6F(select: _bindings.Count == 0 ? -1 : 0);
        _updating = false;
        InvalidatePreviewUx6F(cancelApproval: false);
        UpdateButtonsUx6F();
    }

    private StudioUx6FDraft? ReadDraftUx6F()
    {
        if (_draft is null) return null;
        FlushBindingUx6F();
        var dependencies = _dependencies.Text.Split([';', ',', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim().Replace('\\', '/'))
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _draft = _draft with
        {
            Source = _source.Text,
            Distribution = (ForgeScriptSourceDistributionPolicy)Math.Clamp(_distribution.Selected, 0, 2),
            RequiredAtRuntime = _required.ButtonPressed,
            Dependencies = dependencies,
            Bindings = _bindings.ToArray(),
        };
        return _draft;
    }

    private void RebuildBindingsUx6F(int select)
    {
        _bindingChoice.Clear();
        foreach (var binding in _bindings)
            _bindingChoice.AddItem($"{HumanValueUx6F(binding.Handler)} · priorité {binding.Priority}");
        _bindingIndex = -1;
        if (select >= 0 && select < _bindings.Count)
        {
            _bindingChoice.Select(select);
            _bindingIndex = select;
            PopulateBindingUx6F(_bindings[select]);
        }
        else
        {
            ClearBindingUx6F();
        }
    }

    private void SelectBindingUx6F(long selected)
    {
        if (_updating) return;
        FlushBindingUx6F();
        if (selected < 0 || selected >= _bindings.Count)
        {
            _bindingIndex = -1;
            ClearBindingUx6F();
            return;
        }
        _bindingIndex = (int)selected;
        PopulateBindingUx6F(_bindings[_bindingIndex]);
    }

    private void PopulateBindingUx6F(StudioUx6FBindingDraft binding)
    {
        _updating = true;
        _component.Text = binding.ComponentId;
        _handler.Text = binding.Handler;
        _priority.Value = binding.Priority;
        _condition.Text = binding.EnableCondition ?? string.Empty;
        _authority.Select((int)binding.Authority);
        _enabled.ButtonPressed = binding.Enabled;
        _updating = false;
    }

    private void FlushBindingUx6F()
    {
        if (_updating || _bindingIndex < 0 || _bindingIndex >= _bindings.Count) return;
        _bindings[_bindingIndex] = _bindings[_bindingIndex] with
        {
            ComponentId = _component.Text,
            Handler = _handler.Text,
            Priority = (int)_priority.Value,
            EnableCondition = string.IsNullOrWhiteSpace(_condition.Text) ? null : _condition.Text,
            Authority = (ForgeScriptEventAuthority)Math.Clamp(_authority.Selected, 0, 2),
            Enabled = _enabled.ButtonPressed,
        };
    }

    private void AddBindingUx6F()
    {
        if (_draft is null) return;
        FlushBindingUx6F();
        var binding = _session.NewDamageBindingUx6F(_bindings.Count + 1);
        _bindings.Add(binding);
        var declaration = ForgeScriptSourceValidator.EntryDeclaration(binding.Handler, binding.PayloadType);
        if (!_source.Text.Contains(declaration, StringComparison.Ordinal))
            _source.Text = _source.Text.TrimEnd() +
                           $"\n\n{declaration}\n{{\n    int observed = e;\n    say(\"damage\");\n}}\n";
        _updating = true;
        RebuildBindingsUx6F(_bindings.Count - 1);
        _updating = false;
        InvalidatePreviewUx6F();
    }

    private void RemoveBindingUx6F()
    {
        if (_bindingIndex < 0 || _bindingIndex >= _bindings.Count) return;
        _bindings.RemoveAt(_bindingIndex);
        _updating = true;
        RebuildBindingsUx6F(Math.Min(_bindingIndex, _bindings.Count - 1));
        _updating = false;
        InvalidatePreviewUx6F();
    }

    private void PreviewUx6F()
    {
        var draft = ReadDraftUx6F();
        if (draft is null)
        {
            SetStatusUx6F("Choisissez ou créez explicitement une source canonique.", true);
            return;
        }
        var preview = _session.PreviewScriptSourceUx6F(draft);
        _previewResult = preview;
        _previewReady = preview.Succeeded && preview.CanApply && preview.CanonicalDryRun;
        RenderDiagnosticsUx6F(preview.Diagnostics);
        _console.Text = $"APERÇU SEC · {preview.Message}\n{preview.SemanticDiff}\n" +
                        $"{preview.Language} {preview.DialectVersion} · API {preview.ApiVersion} · " +
                        $"{preview.SourceBytes:N0} octets · aucun fichier écrit.";
        SetStatusUx6F(preview.Message, !preview.Succeeded);
        UpdateButtonsUx6F();
    }

    private void CompileUx6F()
    {
        var draft = ReadDraftUx6F();
        if (draft is null) return;
        var result = _session.CompileScriptSourceUx6F(draft);
        RenderDiagnosticsUx6F(result.Diagnostics);
        _console.Text = $"COMPILATEUR · {result.Message}\nVM {HumanValueUx6F(result.VirtualMachineVersion)} · " +
                        $"API {result.ApiVersion} · bytecode {result.BytecodeBytes:N0} octets · " +
                        $"cache {(result.CacheHit ? "réutilisé" : "calculé")} (clé technique protégée).";
        SetStatusUx6F(result.Message, !result.Succeeded);
    }

    private void SandboxUx6F()
    {
        var draft = ReadDraftUx6F();
        if (draft is null) return;
        var result = _session.RunScriptSandboxUx6F(draft);
        RenderDiagnosticsUx6F(result.Diagnostics);
        _console.Text = $"SANDBOX · {result.Message}\nStatut {result.Status} · " +
                        $"{result.Instructions:N0} instructions · mémoire crête {result.PeakMemoryBytes:N0} octets" +
                        (result.Events.Count == 0 ? "." : $"\nÉvénements : {string.Join(" · ", result.Events)}");
        SetStatusUx6F(result.Message, !result.Succeeded);
    }

    private void ApplyUx6F()
    {
        var draft = ReadDraftUx6F();
        if (draft is null || !_previewReady)
        {
            SetStatusUx6F("Relancez un aperçu sec frais avant d’enregistrer.", true);
            return;
        }
        var result = _session.ApplyApprovedScriptSourceUx6F(draft);
        SetStatusUx6F(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            InvalidatePreviewUx6F(cancelApproval: false);
            return;
        }
        var current = _session.CurrentScriptDraftUx6F();
        if (current is not null) PopulateDraftUx6F(current);
        RefreshSourcesPreservingActiveUx6F(current?.RelativePath);
        ScriptSourceChanged?.Invoke();
    }

    private void UndoUx6F()
    {
        var result = _session.UndoScriptSourceUx6F();
        SetStatusUx6F(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        var current = _session.CurrentScriptDraftUx6F();
        if (current is not null) PopulateDraftUx6F(current);
        ScriptSourceChanged?.Invoke();
    }

    private void RedoUx6F()
    {
        var result = _session.RedoScriptSourceUx6F();
        SetStatusUx6F(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        var current = _session.CurrentScriptDraftUx6F();
        if (current is not null) PopulateDraftUx6F(current);
        ScriptSourceChanged?.Invoke();
    }

    private void RefreshSourcesPreservingActiveUx6F(string? relativePath)
    {
        _choices = _session.ScriptSourceChoicesUx6F();
        _updating = true;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir explicitement une source…");
        foreach (var item in _choices) _sourceChoice.AddItem(item.Label);
        var index = _choices.ToList().FindIndex(item =>
            string.Equals(item.RelativePath, relativePath, StringComparison.Ordinal));
        _sourceChoice.Select(index < 0 ? 0 : index + 1);
        _updating = false;
    }

    private void RenderDiagnosticsUx6F(IReadOnlyList<StudioUx6FDiagnostic> diagnostics)
    {
        ClearChildrenUx6F(_diagnostics);
        if (diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Conforme · aucun diagnostic bloquant.",
                9, StudioStyle.Success, wrap: true));
            return;
        }
        foreach (var item in diagnostics.Take(12))
            _diagnostics.AddChild(StudioStyle.Label(
                $"• {HumanValueUx6F(item.Code)} · {HumanPathUx6F(item.Path)}\n  {item.Message}",
                9, string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase)
                    ? StudioStyle.Danger
                    : StudioStyle.Warning, wrap: true));
    }

    private void WireInvalidationUx6F()
    {
        _source.TextChanged += () => InvalidatePreviewUx6F();
        _distribution.ItemSelected += _ => InvalidatePreviewUx6F();
        _required.Toggled += _ => InvalidatePreviewUx6F();
        _dependencies.TextChanged += _ => InvalidatePreviewUx6F();
        _component.TextChanged += _ => InvalidatePreviewUx6F();
        _handler.TextChanged += _ => InvalidatePreviewUx6F();
        _priority.ValueChanged += _ => InvalidatePreviewUx6F();
        _condition.TextChanged += _ => InvalidatePreviewUx6F();
        _authority.ItemSelected += _ => InvalidatePreviewUx6F();
        _enabled.Toggled += _ => InvalidatePreviewUx6F();
    }

    private void InvalidatePreviewUx6F(bool cancelApproval = true)
    {
        if (_updating) return;
        _previewReady = false;
        _previewResult = null;
        if (cancelApproval) _session.CancelUx6FApproval();
        UpdateButtonsUx6F();
    }

    private void UpdateButtonsUx6F()
    {
        var active = _draft is not null && _explicitSource;
        _preview.Disabled = !active;
        _compile.Disabled = !active;
        _sandbox.Disabled = !active;
        _apply.Disabled = !active || !_previewReady;
        _undo.Disabled = !active;
        _redo.Disabled = !active;
        _addBinding.Disabled = !active;
        _removeBinding.Disabled = !active || _bindingIndex < 0;
        _distribution.Disabled = !active;
        _required.Disabled = !active;
        _dependencies.Editable = active;
        _source.Editable = active;
        _component.Editable = active && _bindingIndex >= 0;
        _handler.Editable = active && _bindingIndex >= 0;
        _priority.Editable = active && _bindingIndex >= 0;
        _condition.Editable = active && _bindingIndex >= 0;
        _authority.Disabled = !active || _bindingIndex < 0;
        _enabled.Disabled = !active || _bindingIndex < 0;
    }

    private void ResetControlsUx6F()
    {
        _updating = true;
        _draft = null;
        _bindings = [];
        _bindingIndex = -1;
        _explicitSource = false;
        _previewReady = false;
        _previewResult = null;
        _identity.Text = "Aucune source active.";
        _distribution.Select(1);
        _required.ButtonPressed = false;
        _dependencies.Text = string.Empty;
        _source.Text = string.Empty;
        _source.Editable = false;
        _bindingChoice.Clear();
        ClearBindingUx6F();
        ClearChildrenUx6F(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic.", 9, StudioStyle.Muted));
        _console.Text = "CONSOLE · prévisualisez, compilez ou testez la sandbox ; aucune source n’est modifiée.";
        _updating = false;
        UpdateButtonsUx6F();
    }

    private void ResetDraftUx6F()
    {
        _session.CancelUx6FApproval();
        _draft = null;
        _bindings = [];
        _bindingIndex = -1;
        _explicitSource = false;
        _previewReady = false;
        _previewResult = null;
        _updating = true;
        _identity.Text = "Aucune source active.";
        _source.Text = string.Empty;
        _bindingChoice.Clear();
        ClearBindingUx6F();
        _updating = false;
    }

    private void ClearBindingUx6F()
    {
        _component.Text = string.Empty;
        _handler.Text = string.Empty;
        _priority.Value = 0;
        _condition.Text = string.Empty;
        _authority.Select(2);
        _enabled.ButtonPressed = false;
    }

    private StudioUx6FEvidenceSourceSnapshot CaptureSourceUx6F()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptApprovalUx6F());
    }

    private bool SourceMatchesUx6F(StudioUx6FEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureScriptApprovalUx6F() == snapshot.Approval;
    }

    private bool ReleaseEvidenceUx6F()
    {
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var sessionReleased = true;
        try
        {
            isolated?.Close();
        }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_F_RELEASE_FAILED type={exception.GetType().Name}");
        }
        var rootReleased = StudioSessionController.DeleteUx6FTemporaryWorkspace(root);
        _evidenceSession = null;
        _evidenceRoot = null;
        _session = _sourceSession;
        _evidenceReleased = sessionReleased && rootReleased;
        return _evidenceReleased;
    }

    private void SetStatusUx6F(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateBoundsUx6F()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.68f : viewport.X >= 2_300 ? 1.22f : 1f;
        var width = Math.Clamp((viewport.X - 48f) / scale, 1_280f, 1_720f);
        var height = Math.Clamp((viewport.Y - 38f) / scale, 790f, 1_070f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(14f, (viewport.Y - height * scale) * .48f));
    }

    private static VBoxContainer PanelColumnUx6F(HBoxContainer host, int minimum, bool expand = false)
    {
        var panel = StudioStyle.Panel("Ux6FColumn", StudioStyle.Chrome, 8, 1);
        panel.CustomMinimumSize = new Vector2(minimum, 0);
        panel.SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        host.AddChild(panel);
        var margin = StudioStyle.Margin(9, 9, 9, 9);
        panel.AddChild(margin);
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 7);
        margin.AddChild(content);
        return content;
    }

    private static VBoxContainer FieldUx6F(string label, Control control, int minimum = 118) =>
        StudioUx2EnvBControls.Field(label, control, minimum);

    private static void ClearChildrenUx6F(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static string HumanPathUx6F(string value) =>
        string.Join(" › ", (value ?? string.Empty).Replace('\\', '/').Trim('/').Split('/',
            StringSplitOptions.RemoveEmptyEntries).Select(HumanValueUx6F));

    private static string HumanValueUx6F(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Non renseigné";
        if (Guid.TryParse(value, out _)) return "Identité technique protégée";
        if (value.Length == 64 && value.All(Uri.IsHexDigit)) return "Empreinte technique protégée";
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static IEnumerable<string> DescendantTextUx6F(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is CheckBox { Visible: true } check) yield return check.Text;
            if (child is LineEdit { Visible: true } line) yield return line.Text;
            if (child is OptionButton { Visible: true } option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var nested in DescendantTextUx6F(child)) yield return nested;
        }
    }

    private static bool ContainsUuidUx6F(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx6F();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
        else if (key.CtrlPressed && key.Keycode == Key.S)
        {
            ApplyUx6F();
            GetViewport().SetInputAsHandled();
        }
    }
}

#if TOOLS
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Factions;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class FactionAdvancedWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<FactionCatalog, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private CheckBox? _required;
    private TextEdit? _definitions;
    private TextEdit? _relationships;
    private TextEdit? _inheritance;
    private TextEdit? _witnesses;
    private TextEdit? _memberships;
    private TextEdit? _dynamics;
    private TextEdit? _diplomacy;
    private Label? _status;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private Guid _profileId;
    private IReadOnlyList<DispositionBand> _bands = [];
    private IReadOnlyList<FactionMember> _legacyMembers = [];
    private Dictionary<string, Guid> _knownIds = new(StringComparer.Ordinal);
    private bool _lastCommandSucceeded;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SummarySectionCount { get; private set; }

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        if (_required is null || _definitions is null || string.IsNullOrWhiteSpace(_definitions.Text)) return false;
        _required.ButtonPressed = true;
        RebuildSummary();
        return SummarySectionCount == 4 && FactionValidator.Validate(BuildCatalog()).Count == 0;
    }

    public bool PreviewForSmokeTest()
    {
        Mutate(true);
        return _lastCommandSucceeded;
    }

    public bool ApplyForSmokeTest()
    {
        Mutate(false);
        return _lastCommandSucceeded;
    }

    public void Initialize(
        Func<ProjectState?> load,
        Func<FactionCatalog, bool, CommandResult> mutate,
        Action undo,
        Action redo,
        Action<Window> applyIcon)
    {
        _load = load;
        _mutate = mutate;
        _undo = undo;
        _redo = redo;
        _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Factions avancées · OpenRPG Forge";
        MinSize = new Vector2I(1160, 740);
        Size = new Vector2I(1420, 880);
        CloseRequested += Hide;
        Build();
        Refresh();
    }

    public void Open()
    {
        Refresh();
        PopupCentered();
        _applyIcon?.Invoke(this);
    }

    public void Refresh()
    {
        var state = _load?.Invoke();
        if (state is null)
        {
            SetEmpty("Aucun projet Forge ouvert.");
            return;
        }

        var catalog = state.Project.Module.Factions;
        var profile = catalog.Advanced;
        _profileId = profile?.Id ?? Guid.NewGuid();
        _bands = catalog.Bands.Count == 0
            ? [new("hostile", -100, -1, 1), new("neutral", 0, 50, 1), new("allied", 51, 100, 1)]
            : catalog.Bands;
        _legacyMembers = catalog.Members;
        _knownIds = catalog.Factions.ToDictionary(item => item.Slug, item => item.Id, StringComparer.Ordinal);
        var slugById = catalog.Factions.ToDictionary(item => item.Id, item => item.Slug);
        _required!.ButtonPressed = catalog.RequireAdvancedRuntime;
        _definitions!.Text = string.Join('\n', catalog.Factions.OrderBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => $"{item.Slug}|{item.DisplayName}|" +
                $"{string.Join(',', item.ParentIds.Select(id => slugById.GetValueOrDefault(id, id.ToString("D"))))}|" +
                $"{string.Join(',', item.Tags)}|" +
                $"{string.Join(',', (item.Policies ?? new Dictionary<string, string>()).Select(pair => $"{pair.Key}={pair.Value}"))}"));
        _relationships!.Text = string.Join('\n', catalog.Relationships.Select(item =>
            $"{slugById.GetValueOrDefault(item.SourceId, item.SourceId.ToString("D"))}|" +
            $"{slugById.GetValueOrDefault(item.TargetId, item.TargetId.ToString("D"))}|" +
            $"{item.Score.ToString(CultureInfo.InvariantCulture)}|{item.Preset}|{item.Explanation}"));
        _inheritance!.Text = string.Join('\n', (profile?.Inheritance ?? []).Select(item =>
            $"{slugById.GetValueOrDefault(item.FactionId, item.FactionId.ToString("D"))}|" +
            $"{Lower(item.InheritTags)}|{Lower(item.InheritPolicies)}|{Lower(item.InheritRelationships)}"));
        _witnesses!.Text = string.Join('\n', (profile?.WitnessPolicies ?? []).Select(item =>
            $"{slugById.GetValueOrDefault(item.FactionId, item.FactionId.ToString("D"))}|" +
            $"{Number(item.MinimumPerception)}|{Number(item.MinimumEvidence)}|" +
            $"{Number(item.CommunicationRadiusMeters)}|{item.MemorySeconds}|{Number(item.ReputationDelta)}|" +
            $"{Lower(item.ShareWithAllies)}"));
        _memberships!.Text = string.Join('\n', (profile?.Memberships ?? []).Select(item =>
            $"{item.ActorId}|{slugById.GetValueOrDefault(item.FactionId, item.FactionId.ToString("D"))}|" +
            $"{item.Rank}|{item.CoverIdentity}|{Number(item.Loyalty)}|{Number(item.Weight)}|" +
            $"{Lower(item.IsPrimary)}|{Timestamp(item.ExpiresAt)}"));
        _dynamics!.Text = string.Join('\n', (profile?.DynamicFactions ?? []).Select(item =>
            $"{slugById.GetValueOrDefault(item.FactionId, item.FactionId.ToString("D"))}|{item.Kind}|" +
            $"{Timestamp(item.CreatedAt)}|{Timestamp(item.ExpiresAt)}|{Lower(item.Active)}|{item.Revision}"));
        _diplomacy!.Text = string.Join('\n', (profile?.DynamicFactions ?? []).SelectMany(source =>
            source.Diplomacy.Select(item =>
                $"{slugById.GetValueOrDefault(source.FactionId, source.FactionId.ToString("D"))}|" +
                $"{slugById.GetValueOrDefault(item.TargetFactionId, item.TargetFactionId.ToString("D"))}|" +
                $"{item.Status}|{Timestamp(item.Since)}|{Timestamp(item.ExpiresAt)}|{item.Cause}")));
        _status!.Text = $"{state.Project.Module.DisplayName} · {catalog.Factions.Count} faction(s) · " +
            (profile is null ? "profil avancé non configuré" : $"UUID {profile.Id:D} · schéma v{profile.SchemaVersion}");
        _feedback!.Text = profile is null
            ? "[color=#b8a8ce]État vide : décrivez les factions puis prévisualisez la transaction.[/color]"
            : "[color=#8ce0bd]Profil canonique chargé.[/color]";
        RebuildSummary();
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 22);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "FACTIONS AVANCÉES" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Hiérarchie, témoins, affiliations multiples et diplomatie dynamique — document canonique, autorité serveur."
        });
        _status = new Label(); _status.AddThemeColorOverride("font_color", new Color("8ce0bd")); root.AddChild(_status);

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [860],
        };
        root.AddChild(split);
        var editor = Panel("PROFIL CANONIQUE"); split.AddChild(editor);
        var editorColumn = editor.GetChild<VBoxContainer>(0);
        _required = new CheckBox { Text = "Profil avancé requis par le runtime" }; editorColumn.AddChild(_required);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        editorColumn.AddChild(tabs);
        BuildHierarchyTab(tabs);
        BuildWitnessTab(tabs);
        BuildDynamicTab(tabs);

        var summaryPanel = Panel("APERÇU & DIFF"); split.AddChild(summaryPanel);
        var summaryColumn = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(380, 430),
        };
        summaryColumn.AddChild(_summary);
        summaryColumn.AddChild(Button("Actualiser l’aperçu", RebuildSummary));
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(380, 150) };
        summaryColumn.AddChild(_feedback);

        var actions = new HBoxContainer(); root.AddChild(actions);
        actions.AddChild(Button("Prévisualiser la transaction", () => Mutate(true)));
        actions.AddChild(Button("Appliquer au projet", () => Mutate(false), true));
        actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Fermer", Hide));
    }

    private void BuildHierarchyTab(TabContainer tabs)
    {
        var form = Tab(tabs, "HIÉRARCHIE");
        _definitions = Multi(form,
            "slug|nom|parents séparés par ,|tags séparés par ,|politiques clé=valeur séparées par ,", 185);
        _relationships = Multi(form, "source|cible|score -100..100|preset|explication", 115);
        _inheritance = Multi(form,
            "faction|hérite tags|hérite politiques|hérite relations — booléens true/false", 115);
        form.AddChild(new Label { Text = "Les cycles et références orphelines sont refusés avant application." });
    }

    private void BuildWitnessTab(TabContainer tabs)
    {
        var form = Tab(tabs, "TÉMOINS & AFFILIATIONS");
        _witnesses = Multi(form,
            "faction|perception min|preuve min|rayon communication m|mémoire s|delta réputation|partage alliés", 180);
        _memberships = Multi(form,
            "acteur|faction|rang|couverture|loyauté -100..100|poids 0..1|primaire|expiration ISO ou vide", 220);
        form.AddChild(new Label
        {
            Text = "Une seule affiliation primaire par acteur ; les couvertures et loyautés restent explicites."
        });
    }

    private void BuildDynamicTab(TabContainer tabs)
    {
        var form = Tab(tabs, "DYNAMIQUE");
        _dynamics = Multi(form,
            "faction|Temporary ou Runtime|créée ISO|expiration ISO ou vide|active|révision", 190);
        _diplomacy = Multi(form,
            "source|cible|Neutral/Alliance/War/Truce|depuis ISO|expiration ISO ou vide|cause", 190);
        form.AddChild(new Label
        {
            Text = "Création, guerre, alliance, trêve et expiration sont répliquées par la session autoritaire."
        });
    }

    private void Mutate(bool dryRun)
    {
        _lastCommandSucceeded = false;
        LastCommandId = Guid.Empty; LastPatchSummary = string.Empty; LastDiagnostics = string.Empty;
        if (_mutate is null || _feedback is null) return;
        try
        {
            var result = _mutate(BuildCatalog(), dryRun);
            LastCommandId = result.Audit.CommandId.Value;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            if (!result.Succeeded)
            {
                LastDiagnostics = string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
                _feedback.Text = $"[color=#ff7777]Configuration refusée[/color]\n" +
                    string.Join('\n', result.Diagnostics.Select(item => $"{item.Code} · {item.Message}"));
                return;
            }
            _lastCommandSucceeded = true;
            _feedback.Text = dryRun
                ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                : $"[color=#8ce0bd]APPLIQUÉ AU PROJET[/color]\n{result.Patch?.Summary}\n" +
                  $"Révision {result.NewRevision} · sauvegardez pour écrire sur disque";
            if (!dryRun) Refresh();
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            LastDiagnostics = exception.Message;
            _feedback.Text = $"[color=#ff7777]Erreur de formulaire[/color]\n{exception.Message}";
        }
    }

    private FactionCatalog BuildCatalog()
    {
        var definitionRows = Rows(_definitions!.Text, 5, "faction").ToArray();
        var ids = definitionRows.Select((parts, index) => new
        {
            Slug = parts[0],
            Id = _knownIds.GetValueOrDefault(parts[0], StableId(_profileId, "faction", parts[0], index)),
        })
            .ToDictionary(item => item.Slug, item => item.Id, StringComparer.Ordinal);
        Guid FactionId(string slug) => ids.TryGetValue(slug, out var id)
            ? id
            : throw new FormatException($"Faction inconnue : '{slug}'.");

        var definitions = definitionRows.Select((parts, index) => new FactionDefinition(
            ids[parts[0]], 1, "toolset:factions", parts[0], Required(parts[1], "nom"), false,
            Csv(parts[2]).Select(FactionId).ToArray(), Csv(parts[3]), ParseMap(parts[4], ','))).ToArray();
        var relationships = Rows(_relationships!.Text, 5, "relation").Select(parts => new FactionRelationship(
            FactionId(parts[0]), FactionId(parts[1]), Double(parts[2], "score"),
            Required(parts[4], "explication"), Required(parts[3], "preset"))).ToArray();
        var inheritance = Rows(_inheritance!.Text, 4, "héritage").Select((parts, index) =>
            new FactionInheritancePolicy(StableId(_profileId, "inheritance", parts[0], index),
                FactionId(parts[0]), Boolean(parts[1], "hérite tags"),
                Boolean(parts[2], "hérite politiques"), Boolean(parts[3], "hérite relations"))).ToArray();
        var witnesses = Rows(_witnesses!.Text, 7, "témoin").Select((parts, index) =>
            new FactionWitnessPolicy(StableId(_profileId, "witness", parts[0], index), FactionId(parts[0]),
                Double(parts[1], "perception"), Double(parts[2], "preuve"), Double(parts[3], "rayon"),
                Integer(parts[4], "mémoire"), Double(parts[5], "réputation"),
                Boolean(parts[6], "partage alliés"))).ToArray();
        var memberships = Rows(_memberships!.Text, 8, "affiliation").Select((parts, index) =>
            new FactionMembership(StableId(_profileId, "membership", $"{parts[0]}:{parts[1]}", index), 1,
                "toolset:factions", Required(parts[0], "acteur"), FactionId(parts[1]),
                Required(parts[2], "rang"), Optional(parts[3]), Double(parts[4], "loyauté"),
                Double(parts[5], "poids"), Boolean(parts[6], "primaire"), OptionalTime(parts[7]))).ToArray();

        var diplomacy = Rows(_diplomacy!.Text, 6, "diplomatie").Select((parts, index) => new
        {
            Source = parts[0],
            State = new FactionDiplomacyState(StableId(_profileId, "diplomacy",
                    $"{parts[0]}:{parts[1]}", index), FactionId(parts[1]),
                EnumValue<FactionDiplomacyStatus>(parts[2], "statut"), RequiredTime(parts[3], "depuis"),
                OptionalTime(parts[4]), Required(parts[5], "cause")),
        }).ToArray();
        var dynamics = Rows(_dynamics!.Text, 6, "faction dynamique").Select((parts, index) =>
            new DynamicFactionDefinition(StableId(_profileId, "dynamic", parts[0], index), 1,
                "toolset:factions", FactionId(parts[0]), EnumValue<DynamicFactionKind>(parts[1], "type"),
                RequiredTime(parts[2], "création"), OptionalTime(parts[3]), Boolean(parts[4], "active"), true,
                Long(parts[5], "révision"), diplomacy.Where(item => item.Source == parts[0])
                    .Select(item => item.State).ToArray())).ToArray();
        var dynamicSlugs = dynamics.Select(item => definitions.Single(definition => definition.Id == item.FactionId).Slug)
            .ToHashSet(StringComparer.Ordinal);
        var orphanDiplomacy = diplomacy.FirstOrDefault(item => !dynamicSlugs.Contains(item.Source));
        if (orphanDiplomacy is not null)
            throw new FormatException($"Diplomatie sans faction dynamique : '{orphanDiplomacy.Source}'.");

        var profile = new FactionAdvancedProfile(_profileId, 1, "toolset:factions", true,
            inheritance, witnesses, memberships, dynamics);
        return new(definitions, relationships, _bands, _legacyMembers, profile, _required!.ButtonPressed);
    }

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var catalog = BuildCatalog();
            var diagnostics = FactionValidator.Validate(catalog);
            var profile = catalog.Advanced!;
            var inherited = catalog.Factions.Count == 0 ? null :
                FactionRuntimeResolver.EffectiveDefinition(catalog, catalog.Factions[0].Id);
            SummarySectionCount = 4;
            _summary.Text =
                $"[font_size=22][color=#ff9858]{catalog.Factions.Count} faction(s)[/color][/font_size]\n" +
                $"[color=#8ce0bd]UUID {profile.Id:D} · schéma v{profile.SchemaVersion} · autorité serveur[/color]\n\n" +
                $"[b]HIÉRARCHIE[/b]\n{profile.Inheritance.Count} politique(s) · " +
                $"{catalog.Relationships.Count} relation(s) · {inherited?.Tags.Count ?? 0} tag(s) effectif(s)\n\n" +
                $"[b]TÉMOINS[/b]\n{profile.WitnessPolicies.Count} politique(s) perception/preuve/mémoire/communication\n\n" +
                $"[b]AFFILIATIONS[/b]\n{profile.Memberships.Count} appartenance(s), rangs, couvertures et loyautés\n\n" +
                $"[b]DYNAMIQUE[/b]\n{profile.DynamicFactions.Count} faction(s) temporaire(s)/runtime · " +
                $"{profile.DynamicFactions.Sum(item => item.Diplomacy.Count)} état(s) diplomatique(s)\n\n" +
                (diagnostics.Count == 0
                    ? "[color=#8ce0bd]✓ Profil valide, migrable et réplicable[/color]"
                    : $"[color=#ff7777]⚠ {diagnostics.Count} diagnostic(s)[/color]\n" +
                      Lines(diagnostics.Select(item => $"{item.Code} · {item.Path}")));
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            SummarySectionCount = 0;
            _summary.Text = $"[color=#ff7777]Formulaire incomplet[/color]\n{exception.Message}";
        }
    }

    private void SetEmpty(string message)
    {
        _status!.Text = message;
        _feedback!.Text = $"[color=#b8a8ce]{message}[/color]";
        if (_summary is not null) _summary.Text = "[color=#b8a8ce]Ouvrez un module pour configurer ses factions.[/color]";
        SummarySectionCount = 0;
    }

    private static Guid StableId(Guid profileId, string group, string value, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{profileId:D}|{group}|{value}|{index}"));
        return new Guid(bytes[..16]);
    }

    private static IEnumerable<string[]> Rows(string text, int count, string label)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != count)
                throw new FormatException($"Ligne {label} invalide : '{line}' ({count} colonnes attendues).");
            yield return parts;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseMap(string text, char separator)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var value in text.Split(separator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = value.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !result.TryAdd(parts[0], parts[1]))
                throw new FormatException($"Politique clé=valeur invalide : '{value}'.");
        }
        return result;
    }

    private static string[] Csv(string text) => text.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string Required(string value, string label) => !string.IsNullOrWhiteSpace(value)
        ? value.Trim() : throw new FormatException($"{label} requis.");
    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static double Double(string value, string label) => double.TryParse(value,
        NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed
        : throw new FormatException($"{label} invalide : '{value}'.");
    private static int Integer(string value, string label) => int.TryParse(value,
        NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed
        : throw new FormatException($"{label} invalide : '{value}'.");
    private static long Long(string value, string label) => long.TryParse(value,
        NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed
        : throw new FormatException($"{label} invalide : '{value}'.");
    private static bool Boolean(string value, string label) => bool.TryParse(value, out var parsed) ? parsed
        : throw new FormatException($"{label} invalide : '{value}'.");
    private static T EnumValue<T>(string value, string label) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed
        : throw new FormatException($"{label} invalide : '{value}'.");
    private static DateTimeOffset RequiredTime(string value, string label) => DateTimeOffset.TryParse(value,
        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed
        : throw new FormatException($"{label} invalide : '{value}'.");
    private static DateTimeOffset? OptionalTime(string value) => string.IsNullOrWhiteSpace(value)
        ? null : RequiredTime(value, "date");
    private static string Timestamp(DateTimeOffset? value) => value?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Lower(bool value) => value.ToString().ToLowerInvariant();
    private static PanelContainer Panel(string title) { var panel = new PanelContainer(); var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 9); panel.AddChild(column); var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new Color("ffad72")); column.AddChild(label); return panel; }
    private static VBoxContainer Tab(TabContainer tabs, string title) { var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; form.AddThemeConstantOverride("separation", 8); scroll.AddChild(form); tabs.AddChild(scroll); return form; }
    private static TextEdit Multi(VBoxContainer parent, string label, float height) { parent.AddChild(new Label { Text = label }); var field = new TextEdit { CustomMinimumSize = new Vector2(0, height) }; parent.AddChild(field); return field; }
    private static Button Button(string text, Action pressed, bool primary = false) { var button = new Button { Text = text }; if (primary) button.AddThemeColorOverride("font_color", new Color("fff1e8")); button.Pressed += pressed; return button; }
    private static string Lines(IEnumerable<string> values) { var items = values.ToArray(); return items.Length == 0 ? "[color=#776b88]Aucun[/color]" : string.Join('\n', items.Select(item => $"• {item}")); }
}
#endif

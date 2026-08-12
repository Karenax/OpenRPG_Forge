#if TOOLS
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Factions;
using Forge.RpgKit.Domain.Factions;
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
    private CheckBox? _foundationRequired;
    private CheckBox? _reputationConfigured;
    private CheckBox? _reputationRequired;
    private TextEdit? _definitions;
    private TextEdit? _relationships;
    private TextEdit? _bandsEditor;
    private TextEdit? _personal;
    private TextEdit? _globalReputation;
    private TextEdit? _heatmapHistory;
    private TextEdit? _reactionPolicy;
    private TextEdit? _inheritance;
    private TextEdit? _witnesses;
    private TextEdit? _memberships;
    private TextEdit? _dynamics;
    private TextEdit? _diplomacy;
    private Label? _status;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private TabContainer? _tabs;
    private Guid _profileId;
    private Guid _reputationProfileId;
    private Guid _reactionPolicyId;
    private IReadOnlyList<FactionMember> _legacyMembers = [];
    private Dictionary<string, Guid> _knownIds = new(StringComparer.Ordinal);
    private Dictionary<string, Guid> _knownPersonalIds = new(StringComparer.Ordinal);
    private Dictionary<string, Guid> _knownTransactionIds = new(StringComparer.Ordinal);
    private Dictionary<string, Guid> _knownSnapshotIds = new(StringComparer.Ordinal);
    private string? _capturePath;
    private string _captureMarker = "OPENRPG_FORGE_R086_FACTION_FOUNDATION_UI_CAPTURE";
    private int _captureTab;
    private double _captureDelay;
    private bool _captureOpened;
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
        _reputationConfigured!.ButtonPressed = true;
        _reputationRequired!.ButtonPressed = true;
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
        Title = "Factions & réputation · OpenRPG Forge";
        MinSize = new Vector2I(1160, 740);
        Size = new Vector2I(1540, 940);
        CloseRequested += Hide;
        Build();
        Refresh();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r086-faction-foundation-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r087-faction-reputation-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            var r087 = capture.StartsWith("--r087-faction-reputation-ui-capture=", StringComparison.Ordinal);
            var prefix = r087
                ? "--r087-faction-reputation-ui-capture="
                : "--r086-faction-foundation-ui-capture=";
            _capturePath = capture[prefix.Length..];
            _captureMarker = r087
                ? "OPENRPG_FORGE_R087_FACTION_REPUTATION_UI_CAPTURE"
                : "OPENRPG_FORGE_R086_FACTION_FOUNDATION_UI_CAPTURE";
            _captureTab = r087 ? 1 : 0;
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            Open();
            ConfigureForSmokeTest();
            if (_tabs is not null) _tabs.CurrentTab = _captureTab;
            _captureOpened = true;
        }
        if (!_captureOpened || _captureDelay < 3) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture()?.GetImage();
        var error = image is null ? Error.Unavailable : image.SavePng(destination);
        GD.Print($"{_captureMarker}_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
        _capturePath = null;
        GetTree().Quit(error == Error.Ok ? 0 : 1);
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
        var reputation = catalog.Reputation;
        _profileId = profile?.Id ?? Guid.NewGuid();
        _reputationProfileId = reputation?.Id ?? Guid.NewGuid();
        _reactionPolicyId = reputation?.Reaction.Id ?? Guid.NewGuid();
        _legacyMembers = catalog.Members;
        _knownIds = catalog.Factions.ToDictionary(item => item.Slug, item => item.Id, StringComparer.Ordinal);
        var slugById = catalog.Factions.ToDictionary(item => item.Id, item => item.Slug);
        _required!.ButtonPressed = catalog.RequireAdvancedRuntime;
        _foundationRequired!.ButtonPressed = catalog.RequireFoundationRuntime;
        _reputationConfigured!.ButtonPressed = reputation is not null;
        _reputationRequired!.ButtonPressed = catalog.RequireReputationRuntime;
        _definitions!.Text = string.Join('\n', catalog.Factions.OrderBy(item => item.Slug, StringComparer.Ordinal)
            .Select(item => $"{item.Slug}|{item.DisplayName}|{Lower(item.Archived)}|" +
                $"{string.Join(',', item.ParentIds.Select(id => slugById.GetValueOrDefault(id, id.ToString("D"))))}|" +
                $"{string.Join(',', item.Tags)}|" +
                $"{string.Join(',', (item.Policies ?? new Dictionary<string, string>()).Select(pair => $"{pair.Key}={pair.Value}"))}"));
        _relationships!.Text = string.Join('\n', catalog.Relationships.Select(item =>
            $"{slugById.GetValueOrDefault(item.SourceId, item.SourceId.ToString("D"))}|" +
            $"{slugById.GetValueOrDefault(item.TargetId, item.TargetId.ToString("D"))}|" +
            $"{item.Score.ToString(CultureInfo.InvariantCulture)}|{item.Preset}|{item.Explanation}"));
        _bandsEditor!.Text = string.Join('\n', catalog.Bands.Select(item =>
            $"{item.Id}|{Number(item.Minimum)}|{Number(item.Maximum)}|{item.Priority}"));
        _knownPersonalIds = catalog.EffectivePersonalReputation.ToDictionary(item =>
            PersonalKey(item.SubjectKind, item.SubjectId, item.SourceFactionId, item.TargetFactionId),
            item => item.Id, StringComparer.Ordinal);
        _personal!.Text = string.Join('\n', catalog.EffectivePersonalReputation.Select(item =>
            $"{item.SubjectKind}|{item.SubjectId}|" +
            $"{slugById.GetValueOrDefault(item.SourceFactionId, item.SourceFactionId.ToString("D"))}|" +
            $"{slugById.GetValueOrDefault(item.TargetFactionId, item.TargetFactionId.ToString("D"))}|" +
            $"{Number(item.Delta)}|{Timestamp(item.ExpiresAt)}|{item.Cause}"));
        _knownTransactionIds = (reputation?.Transactions ?? []).ToDictionary(TransactionKey,
            item => item.Id, StringComparer.Ordinal);
        _globalReputation!.Text = string.Join('\n', (reputation?.Transactions ?? []).Select(item =>
            $"{item.Scope}|{item.ScopeId}|" +
            $"{slugById.GetValueOrDefault(item.SourceFactionId, item.SourceFactionId.ToString("D"))}|" +
            $"{slugById.GetValueOrDefault(item.TargetFactionId, item.TargetFactionId.ToString("D"))}|" +
            $"{Number(item.Delta)}|{item.Cause}|{Timestamp(item.OccurredAt)}"));
        _knownSnapshotIds = (reputation?.HeatmapHistory ?? []).ToDictionary(
            item => SnapshotKey(item.Label, item.CapturedAt), item => item.Id, StringComparer.Ordinal);
        _heatmapHistory!.Text = string.Join('\n', (reputation?.HeatmapHistory ?? []).Select(item =>
            $"{item.Label}|{Timestamp(item.CapturedAt)}|" +
            string.Join(',', item.Relationships.Select(relationship =>
                $"{slugById.GetValueOrDefault(relationship.SourceId, relationship.SourceId.ToString("D"))}>" +
                $"{slugById.GetValueOrDefault(relationship.TargetId, relationship.TargetId.ToString("D"))}=" +
                $"{Number(relationship.Score)}~{relationship.Preset}~{relationship.Explanation}"))));
        _reactionPolicy!.Text = reputation is null ? string.Empty :
            $"{reputation.Reaction.DefaultScope}|{reputation.Reaction.DefaultScopeId}|" +
            $"{Lower(reputation.Reaction.ApplyDisguise)}|{Lower(reputation.Reaction.ApplyCrimes)}|" +
            $"{Lower(reputation.Reaction.ApplyDialogue)}|{Lower(reputation.Reaction.ApplyZoneRules)}|" +
            $"{Lower(reputation.Reaction.AllowGameMasterOverride)}";
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
        var title = new Label { Text = "FACTIONS & RÉPUTATION" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Création, matrice directionnelle, seuils, réputation personnelle et extensions avancées — un document canonique."
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
        _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        editorColumn.AddChild(_tabs);
        BuildHierarchyTab(_tabs);
        BuildReputationTab(_tabs);
        BuildWitnessTab(_tabs);
        BuildDynamicTab(_tabs);

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
        var form = Tab(tabs, "FONDATION P1");
        _foundationRequired = new CheckBox { Text = "Fondations factionnelles requises par le runtime" };
        form.AddChild(_foundationRequired);
        _definitions = Multi(form,
            "slug|nom|archivée|parents séparés par ,|tags séparés par ,|politiques clé=valeur", 120);
        _relationships = Multi(form, "source|cible|score -100..100|preset|explication", 90);
        _bandsEditor = Multi(form, "état|minimum|maximum|priorité", 78);
        _personal = Multi(form,
            "Actor/Player/Group|sujet|source|cible|delta|expiration ISO ou vide|cause", 92);
        form.AddChild(new Label
        {
            Text = "La matrice reste asymétrique ; archivage, UUID, références, seuils et surcharges sont validés avant application."
        });
    }

    private void BuildWitnessTab(TabContainer tabs)
    {
        var form = Tab(tabs, "TÉMOINS & AFFILIATIONS");
        _inheritance = Multi(form,
            "faction|hérite tags|hérite politiques|hérite relations — booléens true/false", 100);
        _witnesses = Multi(form,
            "faction|perception min|preuve min|rayon communication m|mémoire s|delta réputation|partage alliés", 180);
        _memberships = Multi(form,
            "acteur|faction|rang|couverture|loyauté -100..100|poids 0..1|primaire|expiration ISO ou vide", 220);
        form.AddChild(new Label
        {
            Text = "Une seule affiliation primaire par acteur ; les couvertures et loyautés restent explicites."
        });
    }

    private void BuildReputationTab(TabContainer tabs)
    {
        var form = Tab(tabs, "RÉPUTATION P1");
        _reputationConfigured = new CheckBox { Text = "Configurer le profil canonique de réputation" };
        form.AddChild(_reputationConfigured);
        _reputationRequired = new CheckBox { Text = "Profil de réputation requis par le runtime" };
        form.AddChild(_reputationRequired);
        _globalReputation = Multi(form,
            "scope World/Server/Campaign/Group/Player|scope ID|source|cible|delta|cause|date ISO", 135);
        _heatmapHistory = Multi(form,
            "label|capture ISO|source>cible=score~preset~explication, ... (historique import/export)", 135);
        _reactionPolicy = Multi(form,
            "scope défaut|scope ID|déguisement|crimes|dialogue|règles zone|ordre MJ — booléens", 68);
        form.AddChild(new Label
        {
            Text = "La matrice se modifie dans Fondation ; l’aperçu compare les captures et simule une rencontre."
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
        var definitionRows = Rows(_definitions!.Text, 6, "faction").ToArray();
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
            ids[parts[0]], 1, "toolset:factions", parts[0], Required(parts[1], "nom"),
            Boolean(parts[2], "archivée"), Csv(parts[3]).Select(FactionId).ToArray(), Csv(parts[4]),
            ParseMap(parts[5], ','))).ToArray();
        var relationships = Rows(_relationships!.Text, 5, "relation").Select(parts => new FactionRelationship(
            FactionId(parts[0]), FactionId(parts[1]), Double(parts[2], "score"),
            Required(parts[4], "explication"), Required(parts[3], "preset"))).ToArray();
        var bands = Rows(_bandsEditor!.Text, 4, "seuil").Select(parts => new DispositionBand(
            Required(parts[0], "état"), Double(parts[1], "minimum"), Double(parts[2], "maximum"),
            Integer(parts[3], "priorité"))).ToArray();
        var personal = Rows(_personal!.Text, 7, "réputation personnelle").Select((parts, index) =>
        {
            var kind = EnumValue<PersonalReputationSubjectKind>(parts[0], "type de sujet");
            var subject = Required(parts[1], "sujet");
            var source = FactionId(parts[2]);
            var target = FactionId(parts[3]);
            var key = PersonalKey(kind, subject, source, target);
            return new PersonalReputationOverlay(
                _knownPersonalIds.GetValueOrDefault(key, StableId(_profileId, "personal", key, index)),
                1, "toolset:r086", subject, source, target, Double(parts[4], "delta"),
                OptionalTime(parts[5]), Required(parts[6], "cause"), kind);
        }).ToArray();
        FactionReputationProfile? reputation = null;
        if (_reputationConfigured!.ButtonPressed)
        {
            var transactions = Rows(_globalReputation!.Text, 7, "transaction globale")
                .Select((parts, index) =>
                {
                    var scope = EnumValue<ReputationScope>(parts[0], "scope");
                    var scopeId = Required(parts[1], "scope ID");
                    var source = FactionId(parts[2]);
                    var target = FactionId(parts[3]);
                    var occurredAt = RequiredTime(parts[6], "date transaction");
                    var key = TransactionKey(scope, scopeId, source, target, parts[5], occurredAt);
                    return new ReputationTransaction(
                        _knownTransactionIds.GetValueOrDefault(
                            key, StableId(_reputationProfileId, "transaction", key, index)),
                        1, "toolset:r087", scope, scopeId, source, target,
                        Double(parts[4], "delta"), Required(parts[5], "cause"), occurredAt);
                }).ToArray();
            var snapshots = Rows(_heatmapHistory!.Text, 3, "capture heatmap").Select((parts, index) =>
            {
                var label = Required(parts[0], "label capture");
                var capturedAt = RequiredTime(parts[1], "date capture");
                var relationshipsSnapshot = Csv(parts[2]).Select(encoded =>
                {
                    var assignment = encoded.Split('=', 2, StringSplitOptions.TrimEntries);
                    var direction = assignment[0].Split('>', 2, StringSplitOptions.TrimEntries);
                    var values = assignment.Length == 2
                        ? assignment[1].Split('~', 3, StringSplitOptions.TrimEntries)
                        : [];
                    if (direction.Length != 2 || values.Length != 3)
                        throw new FormatException($"Relation de capture invalide : '{encoded}'.");
                    return new FactionRelationship(FactionId(direction[0]), FactionId(direction[1]),
                        Double(values[0], "score capture"), Required(values[2], "explication capture"),
                        Required(values[1], "preset capture"));
                }).ToArray();
                var key = SnapshotKey(label, capturedAt);
                return new FactionHeatmapSnapshot(
                    _knownSnapshotIds.GetValueOrDefault(
                        key, StableId(_reputationProfileId, "heatmap", key, index)),
                    1, "toolset:r087", capturedAt, label, relationshipsSnapshot);
            }).ToArray();
            var reactionRows = Rows(_reactionPolicy!.Text, 7, "politique de réaction").ToArray();
            if (reactionRows.Length != 1)
                throw new FormatException("Une politique de réaction est requise pour le profil de réputation.");
            var reactionParts = reactionRows[0];
            var reaction = new FactionReactionPolicy(_reactionPolicyId, 1, "toolset:r087",
                EnumValue<ReputationScope>(reactionParts[0], "scope par défaut"),
                Required(reactionParts[1], "scope ID par défaut"),
                Boolean(reactionParts[2], "déguisement"), Boolean(reactionParts[3], "crimes"),
                Boolean(reactionParts[4], "dialogue"), Boolean(reactionParts[5], "règles de zone"),
                Boolean(reactionParts[6], "ordre MJ"));
            reputation = new(_reputationProfileId, 1, "toolset:r087", transactions, snapshots, reaction);
        }
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
        return new(definitions, relationships, bands, _legacyMembers, profile, _required!.ButtonPressed,
            personal, _foundationRequired!.ButtonPressed, reputation, _reputationRequired!.ButtonPressed);
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
            var heatmap = catalog.Relationships.OrderBy(item => item.SourceId).ThenBy(item => item.TargetId)
                .Take(4).Select(item =>
                    $"{catalog.Factions.Single(faction => faction.Id == item.SourceId).Slug} → " +
                    $"{catalog.Factions.Single(faction => faction.Id == item.TargetId).Slug}: " +
                    $"{Number(item.Score)} ({FactionValidator.ResolveBand(catalog, item.SourceId, item.TargetId)})");
            var reputation = catalog.Reputation;
            var orderedHistory = (reputation?.HeatmapHistory ?? []).OrderBy(item => item.CapturedAt).ToArray();
            var differences = orderedHistory.Length < 2
                ? []
                : FactionReputationService.Compare(orderedHistory[^2], orderedHistory[^1]);
            var simulation = catalog.Factions.Count < 2
                ? []
                : FactionReputationService.SimulateEncounter(catalog,
                    catalog.Factions.Take(2).Select(item => item.Id), catalog.Factions[^1].Id);
            SummarySectionCount = 4;
            _summary.Text =
                $"[font_size=22][color=#ff9858]{catalog.Factions.Count} faction(s)[/color][/font_size]\n" +
                $"[color=#8ce0bd]UUID {profile.Id:D} · schéma v{profile.SchemaVersion} · autorité serveur[/color]\n\n" +
                $"[b]MATRICE & SEUILS[/b]\n{catalog.Relationships.Count} relation(s) directionnelle(s) · " +
                $"{catalog.Bands.Count} bande(s) · {catalog.EffectivePersonalReputation.Count} surcharge(s) personnelle(s)\n" +
                $"{Lines(heatmap)}\n\n" +
                $"[b]RÉPUTATION GLOBALE[/b]\n" +
                (reputation is null
                    ? "État vide : profil optionnel non configuré\n\n"
                    : $"UUID {reputation.Id:D} · {reputation.Transactions.Count} transaction(s) · " +
                      $"{reputation.HeatmapHistory.Count} capture(s) · {differences.Count} différence(s)\n" +
                      $"Réaction {reputation.Reaction.DefaultScope}/{reputation.Reaction.DefaultScopeId} · " +
                      $"simulation {string.Join(", ", simulation.Select(item => $"{item.Disposition}:{Number(item.Score)}"))}\n\n") +
                $"[b]HIÉRARCHIE[/b]\n{profile.Inheritance.Count} politique(s) · " +
                $"{inherited?.Tags.Count ?? 0} tag(s) effectif(s)\n\n" +
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

    private static string PersonalKey(
        PersonalReputationSubjectKind kind,
        string subjectId,
        Guid sourceId,
        Guid targetId) => $"{kind}:{subjectId}:{sourceId:D}:{targetId:D}";

    private static string TransactionKey(ReputationTransaction item) => TransactionKey(
        item.Scope, item.ScopeId, item.SourceFactionId, item.TargetFactionId, item.Cause, item.OccurredAt);

    private static string TransactionKey(
        ReputationScope scope,
        string scopeId,
        Guid sourceId,
        Guid targetId,
        string cause,
        DateTimeOffset occurredAt) =>
        $"{scope}:{scopeId}:{sourceId:D}:{targetId:D}:{cause}:{occurredAt:O}";

    private static string SnapshotKey(string label, DateTimeOffset capturedAt) => $"{label}:{capturedAt:O}";

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

using System.Security.Cryptography;
using System.Text;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6FProvenance = "studio:ux6-f:script-foundation";
    private const string Ux6FDefaultPath = "scripts/studio-event.as";

    private static readonly ForgeScriptEventContract DamageEventUx6F = new(
        Guid.Parse("12000000-0000-4000-8000-000000000010"),
        1,
        "forge:events",
        "creature.damaged",
        "int",
        true);

    private ForgeScriptSourceHistory? _sourceHistoryUx6F;
    private string? _sourcePathUx6F;
    private string? _sourceDiskFingerprintUx6F;
    private bool _sourceWasNewUx6F;
    private StudioUx6FDraft? _approvedSourceUx6F;
    private long? _approvedSourceRevisionUx6F;
    private string? _approvedSourceFingerprintUx6F;

    internal IReadOnlyList<StudioUx6FSourceChoice> ScriptSourceChoicesUx6F()
    {
        var session = Session;
        if (session is null) return [];
        var root = Path.Combine(session.RootDirectory, "scripts");
        if (!Directory.Exists(root)) return [];

        var canonical = Directory.EnumerateFiles(root, "*.as", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relative = RelativeUx6F(session.RootDirectory, path);
                var metadata = path + ".meta.json";
                return File.Exists(metadata)
                    ? new StudioUx6FSourceChoice(
                        $"Source canonique · {HumanPathUx6F(relative)}",
                        relative,
                        StudioUx6FSourceKind.Canonical,
                        "Source AngelScript gouvernée avec métadonnées versionnées.")
                    : new StudioUx6FSourceChoice(
                        $"Source incomplète protégée · {HumanPathUx6F(relative)}",
                        relative,
                        StudioUx6FSourceKind.InvalidCanonical,
                        "Le fichier .as existe sans métadonnées canoniques; aucune réécriture automatique.");
            });
        var legacy = Directory.EnumerateFiles(root, "*.forge", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relative = RelativeUx6F(session.RootDirectory, path);
                return new StudioUx6FSourceChoice(
                    $"Historique protégé · {HumanPathUx6F(relative)}",
                    relative,
                    StudioUx6FSourceKind.LegacyProtected,
                    "Script .forge historique en lecture seule; créez une source .as séparée pour l’authoring canonique.");
            });
        return canonical.Concat(legacy).ToArray();
    }

    internal bool TryOpenCanonicalSourceUx6F(
        string relativePath,
        out StudioUx6FDraft? draft,
        out string message)
    {
        draft = null;
        var session = Session;
        if (session is null)
        {
            message = "Ouvrez un module avant de choisir une source ForgeScript.";
            return false;
        }
        var choice = ScriptSourceChoicesUx6F().FirstOrDefault(item =>
            string.Equals(item.RelativePath, relativePath, StringComparison.Ordinal));
        if (choice is null)
        {
            message = "La source choisie n’existe plus dans le workspace.";
            return false;
        }
        if (choice.Kind == StudioUx6FSourceKind.LegacyProtected)
        {
            message = "Ce script .forge historique reste protégé. Créez explicitement une source .as canonique.";
            return false;
        }
        if (choice.Kind == StudioUx6FSourceKind.InvalidCanonical)
        {
            message = "Cette source .as n’a pas de métadonnées canoniques et ne sera pas réparée silencieusement.";
            return false;
        }

        try
        {
            var document = new ForgeScriptSourceStore(session.RootDirectory).Load(relativePath);
            SetCurrentSourceUx6F(document, newSource: false);
            draft = CurrentScriptDraftUx6F();
            message = $"Source canonique chargée · {HumanPathUx6F(relativePath)}.";
            return draft is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException or
                                           System.Text.Json.JsonException)
        {
            CloseUx6F();
            message = $"La source canonique est invalide : {exception.Message}";
            return false;
        }
    }

    internal bool TryCreateCanonicalSourceUx6F(
        string relativePath,
        out StudioUx6FDraft? draft,
        out string message)
    {
        draft = null;
        var session = Session;
        var normalized = NormalizePathUx6F(relativePath);
        if (session is null)
        {
            message = "Ouvrez un module avant de créer une source ForgeScript.";
            return false;
        }
        if (!ForgeScriptSourceValidator.IsSafeSourcePath(normalized) ||
            !normalized.StartsWith("scripts/", StringComparison.Ordinal))
        {
            message = "Choisissez un chemin relatif sûr sous scripts, avec l’extension .as.";
            return false;
        }
        var destination = Path.Combine(session.RootDirectory,
            normalized.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(destination) || File.Exists(destination + ".meta.json"))
        {
            message = "Une source ou ses métadonnées utilisent déjà ce chemin.";
            return false;
        }

        var document = new ForgeScriptSourceDocument(
            Guid.NewGuid(),
            ForgeScriptSourceDocument.CurrentSchemaVersion,
            Ux6FProvenance,
            normalized,
            "void main()\n{\n    say(\"Nouveau ForgeScript\");\n}\n",
            "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode,
            false,
            [],
            []);
        SetCurrentSourceUx6F(document, newSource: true);
        draft = CurrentScriptDraftUx6F();
        message = "Nouvelle source préparée en mémoire · prévisualisez avant tout enregistrement.";
        return draft is not null;
    }

    internal StudioUx6FDraft? CurrentScriptDraftUx6F()
    {
        var history = _sourceHistoryUx6F;
        var disk = _sourceDiskFingerprintUx6F;
        if (history is null || disk is null) return null;
        var document = history.Document;
        return new StudioUx6FDraft(
            document.Id,
            document.SchemaVersion,
            document.Provenance,
            document.RelativePath,
            document.Source,
            document.ApiVersion,
            document.Distribution,
            document.RequiredAtRuntime,
            document.Dependencies.ToArray(),
            document.EventBindings.Select(ToDraftUx6F).ToArray(),
            document.RequiredCapabilities is null
                ? null
                : document.RequiredCapabilities.ToHashSet(StringComparer.Ordinal),
            history.Revision,
            disk,
            _sourceWasNewUx6F);
    }

    internal StudioUx6FBindingDraft NewDamageBindingUx6F(int ordinal) => new(
        Guid.NewGuid(),
        "creature.combat",
        DamageEventUx6F.EventName,
        ordinal <= 1 ? "OnDamaged" : $"OnDamaged{ordinal}",
        DamageEventUx6F.PayloadType,
        ordinal <= 1 ? 100 : Math.Max(-10_000, 100 - ((ordinal - 1) * 10)),
        null,
        ForgeScriptEventAuthority.Server,
        true);

    internal StudioUx6FPreview PreviewScriptSourceUx6F(StudioUx6FDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelUx6FApproval();
        var preview = EvaluateScriptDraftUx6F(draft);
        if (preview.Succeeded && preview.CanApply)
        {
            _approvedSourceUx6F = draft;
            _approvedSourceRevisionUx6F = draft.BaseRevision;
            _approvedSourceFingerprintUx6F = CandidateFingerprintUx6F(ToDocumentUx6F(draft));
        }
        return preview;
    }

    internal StudioUx6FCompileResult CompileScriptSourceUx6F(StudioUx6FDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var validation = ValidateDraftDocumentUx6F(draft);
        if (validation.Count > 0)
        {
            return new(false,
                "Compilation refusée · corrigez d’abord les diagnostics de source et de bindings.",
                string.Empty, draft.ApiVersion, 0, string.Empty, false, validation);
        }
        var session = Session;
        if (session is null)
            return new(false, "Aucun module ouvert.", string.Empty, draft.ApiVersion, 0, string.Empty,
                false, []);

        try
        {
            var runtime = new NativeForgeScriptRuntime();
            var cacheRoot = Path.Combine(session.RootDirectory, ".forge", "cache", "studio-forgescript");
            var result = new ForgeScriptArtifactCache(runtime, cacheRoot).Compile(draft.Source);
            var diagnostics = result.Compilation.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray();
            return new(result.Compilation.Success,
                result.Compilation.Success
                    ? $"Compilation réussie · bytecode jetable {result.Compilation.Artifact!.Bytecode.Length:N0} octets."
                    : "Compilation refusée · la source canonique reste inchangée.",
                runtime.VirtualMachineVersion,
                runtime.ApiVersion,
                result.Compilation.Artifact?.Bytecode.Length ?? 0,
                result.CacheKey,
                result.CacheHit,
                diagnostics);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or DllNotFoundException or
                                           BadImageFormatException)
        {
            return new(false, $"Le compilateur natif est indisponible : {exception.Message}",
                string.Empty, draft.ApiVersion, 0, string.Empty, false,
                [new("script.compile.unavailable", draft.RelativePath, "error", exception.Message)]);
        }
    }

    internal StudioUx6FSandboxResult RunScriptSandboxUx6F(StudioUx6FDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var validation = ValidateDraftDocumentUx6F(draft);
        if (validation.Count > 0)
            return new(false, "Test refusé · la source n’est pas valide.", ForgeScriptExecutionStatus.Failed,
                0, 0, [], validation);
        try
        {
            var runtime = new NativeForgeScriptRuntime();
            var compilation = runtime.Compile(draft.Source);
            if (!compilation.Success || compilation.Artifact is null)
            {
                var diagnostics = compilation.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray();
                return new(false, "Test refusé · compilation impossible.", ForgeScriptExecutionStatus.Failed,
                    0, 0, [], diagnostics);
            }
            var execution = runtime.Execute(compilation.Artifact, new ForgeScriptQuotas(
                instructionLimit: 10_000,
                timeoutMilliseconds: 100,
                recursionLimit: 32,
                spawnCap: 32,
                memoryLimitBytes: 8 * 1024 * 1024,
                deterministicSeed: 2026));
            return new(execution.Status == ForgeScriptExecutionStatus.Completed,
                execution.Status == ForgeScriptExecutionStatus.Completed
                    ? "Test sandbox terminé · aucun fichier, réseau, processus ou objet moteur exposé."
                    : $"Test interrompu proprement · {execution.Status}.",
                execution.Status,
                execution.Telemetry?.Instructions ?? 0,
                execution.Telemetry?.PeakMemoryBytes ?? 0,
                execution.Events.Select(item => $"{item.Kind} · {item.Value}").ToArray(),
                execution.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray());
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException or
                                           BadImageFormatException)
        {
            return new(false, $"Le sandbox natif est indisponible : {exception.Message}",
                ForgeScriptExecutionStatus.Failed, 0, 0, [],
                [new("script.sandbox.unavailable", draft.RelativePath, "error", exception.Message)]);
        }
    }

    internal StudioUx6FActionResult ApplyApprovedScriptSourceUx6F(StudioUx6FDraft currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var history = _sourceHistoryUx6F;
        var approved = _approvedSourceUx6F;
        if (history is null || approved is null ||
            _approvedSourceRevisionUx6F != history.Revision ||
            currentDraft.BaseRevision != history.Revision ||
            !string.Equals(_approvedSourceFingerprintUx6F,
                CandidateFingerprintUx6F(ToDocumentUx6F(currentDraft)), StringComparison.Ordinal) ||
            !string.Equals(CandidateFingerprintUx6F(ToDocumentUx6F(approved)),
                CandidateFingerprintUx6F(ToDocumentUx6F(currentDraft)), StringComparison.Ordinal) ||
            !CurrentDiskMatchesUx6F(currentDraft))
        {
            CancelUx6FApproval();
            return new(false, "La source ou le brouillon a changé · relancez l’aperçu sec.");
        }

        var preview = EvaluateScriptDraftUx6F(currentDraft);
        if (!preview.Succeeded || !preview.CanApply)
        {
            CancelUx6FApproval();
            return new(false, preview.Message);
        }
        var candidate = ToDocumentUx6F(currentDraft);
        var command = new ForgeScriptSourceCommand(Guid.NewGuid(), history.Revision, candidate,
            "studio-ux6-f-script-source");
        var applied = history.Apply(command);
        if (!applied.Succeeded)
        {
            CancelUx6FApproval();
            return new(false, HumanDiagnosticsUx6F(applied.Diagnostics));
        }
        try
        {
            new ForgeScriptSourceStore(Session!.RootDirectory).Save(applied.Document);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException)
        {
            history.Undo();
            CancelUx6FApproval();
            return new(false, $"Enregistrement atomique refusé : {exception.Message}");
        }

        _sourceWasNewUx6F = false;
        _sourcePathUx6F = applied.Document.RelativePath;
        _sourceDiskFingerprintUx6F = DiskFingerprintUx6F(Session.RootDirectory, applied.Document.RelativePath);
        CancelUx6FApproval();
        return new(true, $"Source enregistrée atomiquement · révision script {applied.Revision}.");
    }

    internal StudioUx6FActionResult UndoScriptSourceUx6F()
    {
        var history = _sourceHistoryUx6F;
        if (history is null || Session is null)
            return new(false, "Aucune source canonique n’est active.");
        if (!CurrentDiskMatchesUx6F(CurrentScriptDraftUx6F()))
            return new(false, "La source a changé sur disque · rechargez-la avant d’annuler.");
        var result = history.Undo();
        return PersistHistoryUx6F(result, "Modification de source annulée");
    }

    internal StudioUx6FActionResult RedoScriptSourceUx6F()
    {
        var history = _sourceHistoryUx6F;
        if (history is null || Session is null)
            return new(false, "Aucune source canonique n’est active.");
        if (!CurrentDiskMatchesUx6F(CurrentScriptDraftUx6F()))
            return new(false, "La source a changé sur disque · rechargez-la avant de rétablir.");
        var result = history.Redo();
        return PersistHistoryUx6F(result, "Modification de source rétablie");
    }

    internal StudioUx6FApprovalSnapshot CaptureScriptApprovalUx6F() =>
        new(_approvedSourceUx6F, _approvedSourceRevisionUx6F, _approvedSourceFingerprintUx6F);

    internal void RestoreScriptApprovalUx6F(StudioUx6FApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedSourceUx6F = snapshot.Draft;
        _approvedSourceRevisionUx6F = snapshot.Revision;
        _approvedSourceFingerprintUx6F = snapshot.CandidateFingerprint;
    }

    internal void CancelUx6FApproval()
    {
        _approvedSourceUx6F = null;
        _approvedSourceRevisionUx6F = null;
        _approvedSourceFingerprintUx6F = null;
    }

    internal void CloseUx6F()
    {
        _sourceHistoryUx6F = null;
        _sourcePathUx6F = null;
        _sourceDiskFingerprintUx6F = null;
        _sourceWasNewUx6F = false;
        CancelUx6FApproval();
    }

    internal StudioUx6FSmokeResult RunUx6FContractSmoke()
    {
        var sourceSession = Session;
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceSession is null || sourceProject is null || sourceHistory is null)
            return FailedSmokeUx6F();

        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-f", Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6FSmokeResult result;
        try
        {
            result = RunIsolatedUx6F();
        }
        finally
        {
            released = DeleteTemporaryUx6F(root);
        }

        return result with
        {
            Succeeded = result.Succeeded && released,
            TemporaryWorkspaceReleased = released,
        };

        StudioUx6FSmokeResult RunIsolatedUx6F()
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded) return FailedSmokeUx6F();
            var copiedLegacy = CopyLegacyScriptsUx6F(sourceSession.RootDirectory, root);
            var initialDocument = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(initialDocument);

            var editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(initialDocument.RelativePath, out var initialDraft,
                    out _) || initialDraft is null)
                return FailedSmokeUx6F();

            var explicitSelection = true;
            var canonicalSource = initialDraft.RelativePath.EndsWith(".as", StringComparison.Ordinal) &&
                                  initialDraft.Id != Guid.Empty && initialDraft.SchemaVersion == 1;
            var legacyProtected = copiedLegacy > 0 && editing.ScriptSourceChoicesUx6F().Any(item =>
                item.Kind == StudioUx6FSourceKind.LegacyProtected);
            var typedBindings = initialDraft.Bindings.Count == 2 &&
                                initialDraft.Bindings.All(item => item.PayloadType == "int" &&
                                                                  item.Authority == ForgeScriptEventAuthority.Server);
            var entryPoint = initialDraft.Source.Contains("void main()", StringComparison.Ordinal) &&
                             initialDraft.Bindings.All(item => initialDraft.Source.Contains(
                                 ForgeScriptSourceValidator.EntryDeclaration(item.Handler, item.PayloadType),
                                 StringComparison.Ordinal));
            var invalidPathBlocked = !editing.TryCreateCanonicalSourceUx6F("../unsafe.as", out _, out _);
            var brokenHandler = initialDraft with
            {
                Bindings = initialDraft.Bindings.Select((item, index) =>
                    index == 0 ? item with { Handler = "bad handler" } : item).ToArray(),
            };
            var invalidHandlerBlocked = !editing.PreviewScriptSourceUx6F(brokenHandler).Succeeded;

            var changed = initialDraft with
            {
                Source = initialDraft.Source.Replace("say(\"ready\");",
                    "say(\"ready\");\n    int authored = 7;", StringComparison.Ordinal),
            };
            var preview = editing.PreviewScriptSourceUx6F(changed);
            var compilation = editing.CompileScriptSourceUx6F(changed);
            var sandbox = editing.RunScriptSandboxUx6F(changed);
            var staleApprovalBlocked = !editing.ApplyApprovedScriptSourceUx6F(changed with
            {
                Source = changed.Source + "\n// stale draft",
            }).Succeeded;

            var originalSourcePath = Path.Combine(root,
                initialDocument.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var originalBytes = File.ReadAllBytes(originalSourcePath);
            editing.PreviewScriptSourceUx6F(changed);
            File.AppendAllText(originalSourcePath, "\n// external change", Encoding.UTF8);
            var staleDiskBlocked = !editing.ApplyApprovedScriptSourceUx6F(changed).Succeeded;
            File.WriteAllBytes(originalSourcePath, originalBytes);
            if (!editing.TryOpenCanonicalSourceUx6F(initialDocument.RelativePath, out var reloadedDraft,
                    out _) || reloadedDraft is null)
                return FailedSmokeUx6F();
            changed = reloadedDraft with
            {
                Source = reloadedDraft.Source.Replace("say(\"ready\");",
                    "say(\"ready\");\n    int authored = 7;", StringComparison.Ordinal),
            };
            var validPreview = editing.PreviewScriptSourceUx6F(changed);
            var applied = editing.ApplyApprovedScriptSourceUx6F(changed);
            var undone = editing.UndoScriptSourceUx6F();
            var undoDocument = new ForgeScriptSourceStore(root).Load(initialDocument.RelativePath);
            var redone = editing.RedoScriptSourceUx6F();
            var redoDocument = new ForgeScriptSourceStore(root).Load(initialDocument.RelativePath);
            var applyUndoRedo = validPreview.Succeeded && applied.Succeeded && undone.Succeeded &&
                                redone.Succeeded &&
                                CandidateFingerprintUx6F(undoDocument) == CandidateFingerprintUx6F(initialDocument) &&
                                CandidateFingerprintUx6F(redoDocument) == CandidateFingerprintUx6F(
                                    ToDocumentUx6F(changed));
            var persistence = redoDocument.Source == changed.Source &&
                              redoDocument.EventBindings.SequenceEqual(initialDocument.EventBindings) &&
                              redoDocument.Id == initialDocument.Id &&
                              File.Exists(originalSourcePath + ".meta.json");
            var cachePath = Path.Combine(root, ".forge", "cache", "studio-forgescript",
                compilation.CacheKey + ".bin");
            var bytecodeCache = compilation.Succeeded && compilation.BytecodeBytes > 0 &&
                                compilation.CacheKey.Length == 64 && File.Exists(cachePath);
            var quotaAbuseBlocked = QuotaAbuseBlockedUx6F();
            var projectNeighbors = ReferenceEquals(editing.Project, editing.Session?.Project) &&
                                   editing.Project?.Areas.Count == sourceProject.Areas.Count &&
                                   editing.Project?.Blueprints.Count == sourceProject.Blueprints.Count &&
                                   editing.Project?.Dialogues.Count == sourceProject.Dialogues.Count &&
                                   editing.Project?.Quests.Count == sourceProject.Quests.Count;
            editing.Close();

            var succeeded = explicitSelection && canonicalSource && legacyProtected && typedBindings &&
                            entryPoint && invalidPathBlocked && invalidHandlerBlocked && preview.Succeeded &&
                            compilation.Succeeded && bytecodeCache && sandbox.Succeeded && quotaAbuseBlocked &&
                            staleDiskBlocked && staleApprovalBlocked && applyUndoRedo && persistence &&
                            projectNeighbors;
            return new(succeeded, explicitSelection, canonicalSource, legacyProtected, typedBindings, entryPoint,
                invalidPathBlocked, invalidHandlerBlocked, preview.CanonicalDryRun, compilation.Succeeded,
                bytecodeCache, sandbox.Succeeded, quotaAbuseBlocked, staleDiskBlocked, staleApprovalBlocked,
                applyUndoRedo, persistence, projectNeighbors, false);
        }
    }

    internal static ForgeScriptSourceDocument EvidenceDocumentUx6F()
    {
        var high = new ForgeScriptEventBinding(
            Guid.Parse("6f000000-0000-4000-8000-000000000011"), 1, Ux6FProvenance,
            "creature.combat", DamageEventUx6F.EventName, "OnDamagedHigh", "int", 100,
            "is_enraged", ForgeScriptEventAuthority.Server, true);
        var low = new ForgeScriptEventBinding(
            Guid.Parse("6f000000-0000-4000-8000-000000000012"), 1, Ux6FProvenance,
            "creature.combat", DamageEventUx6F.EventName, "OnDamagedLow", "int", 20,
            null, ForgeScriptEventAuthority.Server, true);
        return new ForgeScriptSourceDocument(
            Guid.Parse("6f000000-0000-4000-8000-000000000001"), 1, Ux6FProvenance,
            Ux6FDefaultPath,
            "void OnDamagedHigh(const int &in e)\n{\n    int observed = e;\n    say(\"high\");\n}\n\n" +
            "void OnDamagedLow(const int &in e)\n{\n    int observed = e;\n    say(\"low\");\n}\n\n" +
            "void main()\n{\n    say(\"ready\");\n}\n",
            "1.0.0", ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], [high, low]);
    }

    internal static int CopyLegacyScriptsUx6F(string sourceRoot, string destinationRoot)
    {
        var sourceScripts = Path.Combine(sourceRoot, "scripts");
        if (!Directory.Exists(sourceScripts)) return 0;
        var count = 0;
        foreach (var source in Directory.EnumerateFiles(sourceScripts, "*.forge", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, source);
            var destination = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            count++;
        }
        return count;
    }

    internal static bool DeleteUx6FTemporaryWorkspace(string? root) =>
        string.IsNullOrWhiteSpace(root) || DeleteTemporaryUx6F(root);

    private StudioUx6FPreview EvaluateScriptDraftUx6F(StudioUx6FDraft draft)
    {
        var history = _sourceHistoryUx6F;
        if (history is null || Session is null || draft.BaseRevision != history.Revision ||
            !string.Equals(draft.RelativePath, _sourcePathUx6F, StringComparison.Ordinal) ||
            !CurrentDiskMatchesUx6F(draft))
        {
            return FailedPreviewUx6F("La source a changé ou n’est plus active · rechargez-la.",
                new("studio.script.source_changed", "script/source", "error",
                    "La révision, le chemin ou l’empreinte disque ne correspond plus."));
        }
        var diagnostics = ValidateDraftDocumentUx6F(draft);
        if (diagnostics.Count > 0)
            return new(false, false, "Aperçu refusé · corrigez les diagnostics localisés.", "no-change",
                diagnostics, Encoding.UTF8.GetByteCount(draft.Source), ForgeScriptSourceDocument.Language,
                ForgeScriptSourceDocument.DialectVersion, draft.ApiVersion, false, LegacyProtectedUx6F());
        var candidate = ToDocumentUx6F(draft);
        var result = history.Preview(new ForgeScriptSourceCommand(Guid.NewGuid(), history.Revision,
            candidate, "studio-ux6-f-preview"));
        if (!result.Succeeded)
            return new(false, false, "Aperçu refusé · compilation ou contrat invalide.", "no-change",
                result.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray(),
                Encoding.UTF8.GetByteCount(draft.Source), ForgeScriptSourceDocument.Language,
                ForgeScriptSourceDocument.DialectVersion, draft.ApiVersion, false, LegacyProtectedUx6F());
        var changed = draft.NewSource || CandidateFingerprintUx6F(history.Document) !=
            CandidateFingerprintUx6F(candidate);
        return new(true, changed,
            changed
                ? "Aperçu sec validé · source et métadonnées prêtes, aucun fichier écrit."
                : "Aperçu sec validé · aucun changement à appliquer.",
            result.SemanticDiff,
            [],
            Encoding.UTF8.GetByteCount(draft.Source),
            ForgeScriptSourceDocument.Language,
            ForgeScriptSourceDocument.DialectVersion,
            draft.ApiVersion,
            true,
            LegacyProtectedUx6F());
    }

    private IReadOnlyList<StudioUx6FDiagnostic> ValidateDraftDocumentUx6F(StudioUx6FDraft draft)
    {
        ForgeScriptSourceDocument document;
        try
        {
            document = ToDocumentUx6F(draft);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return [new("studio.script.draft_invalid", "script", "error", exception.Message)];
        }
        var available = ScriptSourceChoicesUx6F()
            .Where(item => item.Kind == StudioUx6FSourceKind.Canonical)
            .Select(item => NormalizePathUx6F(item.RelativePath))
            .Append(NormalizePathUx6F(document.RelativePath))
            .ToHashSet(StringComparer.Ordinal);
        return ForgeScriptSourceValidator.Validate(document, ContractsUx6F(document), available)
            .Where(item => item.Severity == "error")
            .Select(ToStudioDiagnosticUx6F)
            .ToArray();
    }

    private StudioUx6FActionResult PersistHistoryUx6F(ForgeScriptSourceMutationResult result, string message)
    {
        if (!result.Succeeded || Session is null)
            return new(false, HumanDiagnosticsUx6F(result.Diagnostics));
        try
        {
            new ForgeScriptSourceStore(Session.RootDirectory).Save(result.Document);
            _sourceWasNewUx6F = false;
            _sourcePathUx6F = result.Document.RelativePath;
            _sourceDiskFingerprintUx6F = DiskFingerprintUx6F(Session.RootDirectory, result.Document.RelativePath);
            CancelUx6FApproval();
            return new(true, $"{message} · révision script {result.Revision}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException)
        {
            return new(false, $"Persistance refusée : {exception.Message}");
        }
    }

    private void SetCurrentSourceUx6F(ForgeScriptSourceDocument document, bool newSource)
    {
        CloseUx6F();
        var contracts = ContractsUx6F(document)
            .Concat([DamageEventUx6F])
            .GroupBy(item => item.EventName, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        _sourceHistoryUx6F = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), contracts);
        _sourcePathUx6F = document.RelativePath;
        _sourceWasNewUx6F = newSource;
        _sourceDiskFingerprintUx6F = DiskFingerprintUx6F(Session!.RootDirectory, document.RelativePath);
    }

    private bool CurrentDiskMatchesUx6F(StudioUx6FDraft? draft)
    {
        if (draft is null || Session is null || _sourceDiskFingerprintUx6F is null) return false;
        return string.Equals(draft.DiskFingerprint, _sourceDiskFingerprintUx6F, StringComparison.Ordinal) &&
               string.Equals(DiskFingerprintUx6F(Session.RootDirectory, draft.RelativePath),
                   _sourceDiskFingerprintUx6F, StringComparison.Ordinal);
    }

    private bool LegacyProtectedUx6F() => ScriptSourceChoicesUx6F().Any(item =>
        item.Kind == StudioUx6FSourceKind.LegacyProtected);

    private static IReadOnlyList<ForgeScriptEventContract> ContractsUx6F(ForgeScriptSourceDocument document) =>
        document.EventBindings.GroupBy(item => item.EventName, StringComparer.Ordinal)
            .Select(group => new ForgeScriptEventContract(group.First().Id, 1,
                string.IsNullOrWhiteSpace(group.First().Provenance) ? Ux6FProvenance : group.First().Provenance,
                group.Key, group.First().PayloadType, document.RequiredAtRuntime))
            .ToArray();

    private static ForgeScriptSourceDocument ToDocumentUx6F(StudioUx6FDraft draft)
    {
        if (draft.Id == Guid.Empty) throw new ArgumentException("L’identité stable de la source est absente.");
        return new ForgeScriptSourceDocument(
            draft.Id,
            draft.SchemaVersion,
            draft.Provenance,
            NormalizePathUx6F(draft.RelativePath),
            draft.Source,
            draft.ApiVersion,
            draft.Distribution,
            draft.RequiredAtRuntime,
            draft.Dependencies.Select(NormalizePathUx6F).ToArray(),
            draft.Bindings.Select(item => new ForgeScriptEventBinding(
                item.Id,
                1,
                Ux6FProvenance,
                item.ComponentId.Trim(),
                item.EventName.Trim(),
                item.Handler.Trim(),
                item.PayloadType.Trim(),
                item.Priority,
                string.IsNullOrWhiteSpace(item.EnableCondition) ? null : item.EnableCondition.Trim(),
                item.Authority,
                item.Enabled)).ToArray(),
            draft.RequiredCapabilities);
    }

    private static StudioUx6FBindingDraft ToDraftUx6F(ForgeScriptEventBinding item) => new(
        item.Id,
        item.ComponentId,
        item.EventName,
        item.Handler,
        item.PayloadType,
        item.Priority,
        item.EnableCondition,
        item.Authority,
        item.Enabled);

    private static StudioUx6FDiagnostic ToStudioDiagnosticUx6F(ForgeScriptSourceDiagnostic item) =>
        new(item.Code, item.Path, item.Severity, item.Message);

    private static StudioUx6FDiagnostic ToStudioDiagnosticUx6F(ForgeScriptDiagnostic item) =>
        new(string.IsNullOrWhiteSpace(item.Source) ? "script.compile" : item.Source,
            $"script/source:{Math.Max(1, item.Line)}:{Math.Max(1, item.Column)}",
            item.Severity,
            item.Message,
            Math.Max(1, item.Line),
            Math.Max(1, item.Column));

    private static StudioUx6FPreview FailedPreviewUx6F(string message, StudioUx6FDiagnostic diagnostic) =>
        new(false, false, message, "no-change", [diagnostic], 0,
            ForgeScriptSourceDocument.Language, ForgeScriptSourceDocument.DialectVersion,
            "1.0.0", false, true);

    private static string HumanDiagnosticsUx6F(IReadOnlyList<ForgeScriptSourceDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "La transaction source a été refusée."
            : string.Join(" · ", diagnostics.Select(item => $"{item.Code} : {item.Message}"));

    private static string CandidateFingerprintUx6F(ForgeScriptSourceDocument document)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(document.Id.ToByteArray());
        writer.Write(document.SchemaVersion);
        WriteStringUx6F(writer, document.Provenance);
        WriteStringUx6F(writer, document.RelativePath);
        WriteStringUx6F(writer, document.Source);
        WriteStringUx6F(writer, document.ApiVersion);
        writer.Write((int)document.Distribution);
        writer.Write(document.RequiredAtRuntime);
        writer.Write(document.Dependencies.Count);
        foreach (var dependency in document.Dependencies) WriteStringUx6F(writer, dependency);
        writer.Write(document.EventBindings.Count);
        foreach (var binding in document.EventBindings)
        {
            writer.Write(binding.Id.ToByteArray());
            writer.Write(binding.SchemaVersion);
            WriteStringUx6F(writer, binding.Provenance);
            WriteStringUx6F(writer, binding.ComponentId);
            WriteStringUx6F(writer, binding.EventName);
            WriteStringUx6F(writer, binding.Handler);
            WriteStringUx6F(writer, binding.PayloadType);
            writer.Write(binding.Priority);
            WriteStringUx6F(writer, binding.EnableCondition ?? string.Empty);
            writer.Write((int)binding.Authority);
            writer.Write(binding.Enabled);
        }
        var capabilities = document.RequiredCapabilities?.Order(StringComparer.Ordinal).ToArray() ?? [];
        writer.Write(capabilities.Length);
        foreach (var capability in capabilities) WriteStringUx6F(writer, capability);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static string DiskFingerprintUx6F(string root, string relativePath)
    {
        var source = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var metadata = source + ".meta.json";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendFileUx6F(hash, source, "source");
        AppendFileUx6F(hash, metadata, "metadata");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendFileUx6F(IncrementalHash hash, string path, string label)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(label));
        if (!File.Exists(path))
        {
            hash.AppendData([0]);
            return;
        }
        hash.AppendData([1]);
        hash.AppendData(File.ReadAllBytes(path));
    }

    private static void WriteStringUx6F(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static bool QuotaAbuseBlockedUx6F()
    {
        var runtime = new NativeForgeScriptRuntime();
        var compilation = runtime.Compile("void main() { while (true) { say(\"loop\"); } }");
        if (!compilation.Success || compilation.Artifact is null) return false;
        var result = runtime.Execute(compilation.Artifact,
            new ForgeScriptQuotas(instructionLimit: 8, timeoutMilliseconds: 1000));
        return result.Status == ForgeScriptExecutionStatus.QuotaExceeded;
    }

    private static bool DeleteTemporaryUx6F(string root)
    {
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string RelativeUx6F(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string NormalizePathUx6F(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim('/').Trim();

    private static string HumanPathUx6F(string path) =>
        NormalizePathUx6F(path).Replace("scripts/", string.Empty, StringComparison.Ordinal)
            .Replace("/", " › ", StringComparison.Ordinal);

    private static StudioUx6FSmokeResult FailedSmokeUx6F() =>
        new(false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false);
}

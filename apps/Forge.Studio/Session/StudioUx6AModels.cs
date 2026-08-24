using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.Rules;

namespace Forge.Studio.Session;

internal sealed record StudioUx6AProfileChoice(string Label, AreaId ContextAreaId);

internal sealed record StudioUx6ADifficultyDraft(
    Guid Id,
    int SchemaVersion,
    string Key,
    string DisplayName,
    double PlayerDamageMultiplier,
    double IncomingDamageMultiplier,
    double AiIntensityMultiplier,
    double EconomyMultiplier,
    double RestRecoveryMultiplier,
    SaveRestrictionPolicy SaveRestrictions,
    FriendlyFireMode FriendlyFire,
    bool PauseAssist,
    bool TelegraphAssist,
    double TimingWindowMultiplier,
    double PerceptionCueMultiplier,
    string Provenance);

internal sealed record StudioUx6AMutatorDraft(
    Guid Id,
    string Key,
    string DisplayName,
    bool Enabled,
    IReadOnlyDictionary<string, double> ScalarOverrides,
    IReadOnlyList<string> Tags);

internal sealed record StudioUx6APluginPackageDraft(
    Guid Id,
    int SchemaVersion,
    string Key,
    string Version,
    string ContentPackageId,
    IReadOnlyList<string> Attributes,
    IReadOnlyList<string> Effects,
    IReadOnlyList<string> Abilities,
    IReadOnlyList<string> Inventories,
    IReadOnlyList<string> ProgressionTracks,
    IReadOnlyList<string> UiPanels,
    string Provenance);

internal sealed record StudioUx6AAreaScaleDraft(
    string Key,
    string Label,
    float Scale,
    bool Enabled,
    bool Protected);

internal sealed record StudioUx6AScheduleDraft(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Key,
    int MinuteOfDay,
    IReadOnlyList<int> DaysOfMonth,
    string EventName,
    string PauseDomain,
    bool ServerAuthoritative);

internal sealed record StudioUx6ADraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    AreaId ContextAreaId,
    ModuleRulesProfile SourceRules,
    IReadOnlyList<StudioUx6ADifficultyDraft> Difficulties,
    string ActiveDifficultyKey,
    IReadOnlyList<StudioUx6AMutatorDraft> Mutators,
    bool PluginsEnabled,
    bool PluginsRequiredAtRuntime,
    string ActivePluginKey,
    IReadOnlyList<StudioUx6APluginPackageDraft> PluginPackages,
    ModuleGameplayControlProfile SourceGameplay,
    bool WorldTimeEnabled,
    bool WorldTimeRequired,
    int DaysPerMonth,
    int MonthsPerYear,
    int HoursPerDay,
    int MinutesPerHour,
    long InitialTotalMinutes,
    float InitialTimeScale,
    bool InitiallyPaused,
    string InitialPauseDomain,
    long InitialClockRevision,
    IReadOnlyList<StudioUx6AAreaScaleDraft> AreaScales,
    IReadOnlyList<string> PauseDomains,
    IReadOnlyList<StudioUx6AScheduleDraft> Schedules);

internal sealed record StudioUx6ADiagnostic(string Code, string Path, string Message, bool Blocking = true);

internal sealed record StudioUx6ABlockPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6ADiagnostic> Diagnostics,
    IReadOnlyList<string> RuntimePreview,
    bool CanonicalDryRun,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6AActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6AApprovalSnapshot(
    StudioUx6ADraft? RulesDraft,
    ProjectRevision? RulesRevision,
    string? RulesFingerprint,
    StudioUx6ADraft? WorldDraft,
    ProjectRevision? WorldRevision,
    string? WorldFingerprint);

internal sealed record StudioUx6AEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioUx6AApprovalSnapshot Approval);

internal sealed record StudioUx6ASmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleData,
    bool DifficultyValidation,
    bool PluginValidation,
    bool WorldTimeValidation,
    bool RulesDryRun,
    bool WorldTimeDryRun,
    bool RuntimePreview,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool RulesTailsPreserved,
    bool GameplayTailsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);

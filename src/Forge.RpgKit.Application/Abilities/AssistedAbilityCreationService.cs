using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Assistant;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.RpgKit.Domain.Abilities;

namespace Forge.RpgKit.Application.Abilities;

public sealed record AssistedAbilityBrief(
    Guid AbilityId,
    Guid OwnerBlueprintId,
    string Slug,
    string DisplayName,
    int ResourceCost,
    double CooldownSeconds,
    AbilityTargeting Targeting,
    IReadOnlyList<AbilityEffect> Effects,
    string VfxCueKey,
    string AudioCueKey,
    string ScriptPath,
    string? RequiredItemSlug,
    IReadOnlySet<string> AiTags,
    string BalanceIntent,
    IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules,
    string AuthorId,
    int Seed);

public sealed record AssistedAbilityProposal(
    bool Succeeded,
    AbilityOrchestrationDefinition Orchestration,
    CombatProfile UpdatedCombat,
    RuntimeJsonDocumentDefinition RuntimeDocument,
    IReadOnlyList<AbilityTestScenario> Tests,
    AbilityBalanceReport Balance,
    IReadOnlyList<AbilityDiagnostic> Diagnostics,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> StyleViolations,
    string SemanticDiff,
    string PromptSha256,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    string ProviderKey,
    string Provenance,
    bool ApprovedForApplication)
{
    public bool CanApply => Succeeded && Diagnostics.Count == 0 && StyleViolations.Count == 0 &&
                            Tests.Count == 5 && Tests.All(item => item.Passed) && Balance.Passed;

    public AssistedAbilityProposal WithApproval(bool approved, string provenance) => this with
    {
        ApprovedForApplication = approved,
        Provenance = string.IsNullOrWhiteSpace(provenance) ? Provenance : provenance.Trim(),
    };
}

public static class AssistedAbilityCreationService
{
    private const string RuntimeSchema = """
        {"type":"object","required":["id","schemaVersion","ownerBlueprintId","ability","scriptPath","audioCueKey","aiTags","tests","balance","balanceIntent","provenance"],"properties":{"schemaVersion":{"const":1},"ability":{"type":"object"},"scriptPath":{"type":"string"},"audioCueKey":{"type":"string"},"aiTags":{"type":"array","minItems":1},"tests":{"type":"array","minItems":5,"maxItems":5},"balance":{"type":"object"}}}
        """;

    public static async Task<AssistedAbilityProposal> ProposeAsync(
        AssistedAbilityBrief brief,
        ProjectDefinition project,
        LoreIndexDefinition lore,
        IReadOnlySet<string> availableScripts,
        IAssistantProvider provider,
        AssistantProviderDefinition providerDefinition,
        bool networkAuthorized,
        bool projectContentAuthorized,
        CancellationToken cancellationToken = default)
    {
        ValidateBrief(brief, project, availableScripts);
        var lorePacket = LoreContextAssembler.Assemble(lore, project,
            $"{brief.DisplayName} {brief.BalanceIntent} {string.Join(' ', brief.AiTags)}", 8);
        const string system = "Create one bounded OpenRPG Forge ability. Return structured design only; never execute code or mutate the project.";
        var user = JsonSerializer.Serialize(new
        {
            brief.DisplayName,
            brief.ResourceCost,
            brief.CooldownSeconds,
            targeting = brief.Targeting.ToString(),
            effects = brief.Effects.Select(item => new { kind = item.Kind.ToString(), item.Magnitude }),
            brief.VfxCueKey,
            brief.AudioCueKey,
            brief.ScriptPath,
            brief.RequiredItemSlug,
            brief.AiTags,
            brief.BalanceIntent,
            brief.StyleRules,
            brief.ContextCitations,
            lore = projectContentAuthorized ? lorePacket.Content : "[project content withheld]",
        });
        const string schema = "{\"type\":\"object\",\"required\":[\"targeting\",\"costs\",\"effects\",\"vfx\",\"audio\",\"aiTags\",\"tests\",\"balance\"]}";
        var hash = Sha256(string.Join('\n', system, user, schema));
        var response = await provider.GenerateAsync(new AssistantProviderRequest(
            brief.AbilityId, Guid.NewGuid(),
            new AssistantPromptRenderResult("ability-creation", "1.0.0", system, user, schema, hash),
            AssistantPermissionLevel.Propose, Math.Min(2048, providerDefinition.MaximumOutputTokens),
            networkAuthorized, projectContentAuthorized), cancellationToken).ConfigureAwait(false);
        return Generate(brief, project, availableScripts, response.ProviderKey, response.EstimatedCost,
            response.Provenance, hash, response.InputTokens, response.OutputTokens);
    }

    public static AssistedAbilityProposal Generate(
        AssistedAbilityBrief brief,
        ProjectDefinition project,
        IReadOnlySet<string> availableScripts,
        string providerKey = "offline-ci",
        decimal estimatedCostUsd = 0,
        string provenance = "assistant:deterministic",
        string promptSha256 = "offline",
        int inputTokens = 0,
        int outputTokens = 0)
    {
        ValidateBrief(brief, project, availableScripts);
        var owner = project.Blueprints.Single(item => item.Id.Value == brief.OwnerBlueprintId);
        var current = owner.Combat!;
        var ability = new AbilityDefinition(AbilityId.From(brief.AbilityId), brief.Slug, brief.DisplayName,
            brief.ResourceCost, brief.CooldownSeconds, brief.Targeting, brief.Effects,
            new AbilityVisualCue(brief.VfxCueKey));
        var orchestration = new AbilityOrchestrationDefinition(brief.AbilityId, 1, provenance, owner.Id, ability,
            brief.ScriptPath.Trim(), string.IsNullOrWhiteSpace(brief.RequiredItemSlug) ? null : brief.RequiredItemSlug.Trim(),
            brief.AudioCueKey.Trim(), brief.AiTags, brief.BalanceIntent.Trim());
        var itemSlugs = project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
        var diagnostics = AbilityOrchestrationService.Validate(orchestration, current, availableScripts, itemSlugs);
        var tests = AbilityOrchestrationService.RunTests(orchestration, current);
        foreach (var failed in tests.Where(item => !item.Passed))
            diagnostics = [.. diagnostics, new AbilityDiagnostic("RPG_ABILITY_TEST_FAILED",
                $"abilities/{brief.AbilityId:D}/tests/{failed.Kind}", failed.Evidence)];
        var balance = AbilityOrchestrationService.Balance(orchestration);
        if (!balance.Passed)
            diagnostics = [.. diagnostics, new AbilityDiagnostic("RPG_ABILITY_BALANCE_FAILED",
                $"abilities/{brief.AbilityId:D}/balance", balance.Notes)];
        var updatedAbilities = current.Abilities.Any(item => item.Id == ability.Id)
            ? current.Abilities.Select(item => item.Id == ability.Id ? ability : item).ToArray()
            : [.. current.Abilities, ability];
        var updatedCombat = new CombatProfile(current.Stats, updatedAbilities, current.Loot);
        var styleViolations = brief.StyleRules.Any(string.IsNullOrWhiteSpace)
            ? new[] { "ability.style.empty" }
            : Array.Empty<string>();
        var runtimeJson = AbilityOrchestrationService.ToCanonicalJson(orchestration, tests, balance);
        var runtimeDocument = new RuntimeJsonDocumentDefinition(StableId(brief.AbilityId, "runtime-document"), 1,
            $"ability.{brief.Slug}", RuntimeSchema, runtimeJson, 32_768, 16, false, "1.0", provenance);
        var semanticDiff = $"batch:set-blueprint-combat:{owner.Id}|ability:{ability.Id}|runtime:{runtimeDocument.Key}|" +
                           $"targeting:{ability.Targeting}|effects:{ability.Effects.Count}|tests:{tests.Count}|balance:1";
        return new(diagnostics.Count == 0 && styleViolations.Length == 0, orchestration, updatedCombat,
            runtimeDocument, tests, balance, diagnostics, brief.ContextCitations.ToArray(), styleViolations,
            semanticDiff, promptSha256, inputTokens, outputTokens, estimatedCostUsd, providerKey, provenance, false);
    }

    private static void ValidateBrief(
        AssistedAbilityBrief brief,
        ProjectDefinition project,
        IReadOnlySet<string> availableScripts)
    {
        if (brief.AbilityId == Guid.Empty || brief.OwnerBlueprintId == Guid.Empty ||
            string.IsNullOrWhiteSpace(brief.Slug) || string.IsNullOrWhiteSpace(brief.DisplayName) ||
            brief.ResourceCost is < 0 or > 1_000_000 || !double.IsFinite(brief.CooldownSeconds) ||
            brief.CooldownSeconds is < 0 or > 300 || brief.Effects.Count is < 1 or > 8 ||
            string.IsNullOrWhiteSpace(brief.VfxCueKey) || string.IsNullOrWhiteSpace(brief.AudioCueKey) ||
            string.IsNullOrWhiteSpace(brief.ScriptPath) || brief.AiTags.Count == 0 ||
            string.IsNullOrWhiteSpace(brief.BalanceIntent) || brief.ContextCitations.Count == 0 ||
            brief.StyleRules.Count == 0 || string.IsNullOrWhiteSpace(brief.AuthorId))
            throw new ArgumentException("Assisted ability identity, costs, effects, cues, script, AI tags, balance, citations and style are invalid.");
        var owner = project.Blueprints.SingleOrDefault(item => item.Id.Value == brief.OwnerBlueprintId);
        if (owner?.Combat is null || owner.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            throw new ArgumentException("Assisted ability owner must resolve to a combat creature or player blueprint.");
        if (owner.Combat.Abilities.Any(item => item.Id.Value != brief.AbilityId && item.Slug == brief.Slug))
            throw new ArgumentException("Assisted ability slug already belongs to another owner ability.");
        if (!availableScripts.Contains(brief.ScriptPath.Trim()))
            throw new ArgumentException("Assisted ability ForgeScript must resolve before composition.");
        if (brief.RequiredItemSlug is { Length: > 0 } item &&
            project.Blueprints.All(candidate => candidate.Kind != BlueprintKind.Item || candidate.Slug != item.Trim()))
            throw new ArgumentException("Assisted ability required item must resolve before composition.");
    }

    private static Guid StableId(Guid root, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{root:D}:{discriminator}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Assistant;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Bosses;
using Forge.RpgKit.Domain.Creatures;
using Forge.RpgKit.Domain.Encounters;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Application.Bosses;

public sealed record AssistedBossBrief(
    Guid BossId,
    Guid BlueprintId,
    string Slug,
    string DisplayName,
    Guid ZoneId,
    string Synopsis,
    int MaximumHealth,
    int Offense,
    int Defense,
    double ThreatRating,
    IReadOnlyList<string> AbilityNames,
    IReadOnlyList<string> PhaseScripts,
    IReadOnlyList<string> SummonBlueprintSlugs,
    IReadOnlyList<string> RewardItemSlugs,
    string DeathScript,
    IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules,
    string AuthorId,
    int Seed);

public sealed record AssistedBossProposal(
    bool Succeeded,
    BossOrchestrationDefinition Boss,
    RuntimeJsonDocumentDefinition RuntimeDocument,
    BossBalanceReport Balance,
    IReadOnlyList<BossDiagnostic> Diagnostics,
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
    public bool CanApply => Succeeded && Diagnostics.Count == 0 && StyleViolations.Count == 0 && Balance.Passed;

    public AssistedBossProposal WithApproval(bool approved, string provenance) => this with
    {
        ApprovedForApplication = approved,
        Provenance = string.IsNullOrWhiteSpace(provenance) ? Provenance : provenance.Trim(),
    };
}

public static class AssistedBossCreationService
{
    private const string RuntimeSchema = """
        {"type":"object","required":["id","schemaVersion","blueprintId","creatureId","encounterId","dialogueId","abilities","phases","rewards","deathScript","balance","provenance"],"properties":{"schemaVersion":{"const":1},"abilities":{"type":"array","minItems":1},"phases":{"type":"array","minItems":2,"maxItems":6},"rewards":{"type":"array","minItems":1},"deathScript":{"type":"string"},"balance":{"type":"object"}}}
        """;

    public static async Task<AssistedBossProposal> ProposeAsync(
        AssistedBossBrief brief,
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
            string.Join(' ', brief.AbilityNames.Prepend(brief.Synopsis)), 8);
        const string system = "Compose a bounded OpenRPG Forge boss package. Return structured design only; never execute code or mutate the project.";
        var user = JsonSerializer.Serialize(new
        {
            brief.Synopsis,
            brief.MaximumHealth,
            brief.Offense,
            brief.Defense,
            brief.ThreatRating,
            brief.AbilityNames,
            brief.PhaseScripts,
            brief.SummonBlueprintSlugs,
            brief.RewardItemSlugs,
            brief.StyleRules,
            brief.ContextCitations,
            lore = projectContentAuthorized ? lorePacket.Content : "[project content withheld]",
        });
        const string schema = "{\"type\":\"object\",\"required\":[\"creature\",\"encounter\",\"abilities\",\"phases\",\"dialogue\",\"rewards\"]}";
        var hash = Sha256(string.Join('\n', system, user, schema));
        var response = await provider.GenerateAsync(new AssistantProviderRequest(
            brief.BossId, Guid.NewGuid(),
            new AssistantPromptRenderResult("boss-creation", "1.0.0", system, user, schema, hash),
            AssistantPermissionLevel.Propose, Math.Min(3072, providerDefinition.MaximumOutputTokens),
            networkAuthorized, projectContentAuthorized), cancellationToken).ConfigureAwait(false);
        return Generate(brief, project, availableScripts, response.ProviderKey, response.EstimatedCost,
            response.Provenance, hash, response.InputTokens, response.OutputTokens);
    }

    public static AssistedBossProposal Generate(
        AssistedBossBrief brief,
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
        var blueprintId = BlueprintId.From(brief.BlueprintId);
        var abilities = brief.AbilityNames.Select((name, index) => new AbilityDefinition(
            AbilityId.From(StableId(brief.BossId, $"ability:{brief.Seed}:{index}")),
            Slug(name, $"boss-ability-{index + 1}"), name.Trim(), 10 + (index * 5), Math.Max(2, 8 - index),
            index % 2 == 0 ? AbilityTargeting.SingleEnemy : AbilityTargeting.PointArea,
            [new AbilityEffect(AbilityEffectKind.Damage, Math.Max(1, brief.Offense + (index * 3)))],
            new AbilityVisualCue($"vfx.boss.{brief.Slug}.{index + 1}"))).ToArray();
        var loot = brief.RewardItemSlugs.Select(item => new LootDrop(item.Trim(), 1, 1)).ToArray();
        var combat = new CombatProfile(
            new CombatStats(brief.MaximumHealth, 100, brief.Offense, brief.Defense, "hostile", "hostile"),
            abilities, loot);
        var blueprint = new BlueprintDefinition(blueprintId, brief.Slug, brief.DisplayName, BlueprintKind.Creature,
            combat, BlueprintPaletteOrigin.ProjectContent, false,
            new BlueprintProvenance(1, $"assistant://boss/{brief.BossId:D}", brief.AuthorId, "UNLICENSED",
                Sha256($"{brief.BossId:D}:{brief.Seed}:{promptSha256}"), true,
                ["assisted-boss-composition", "author-review-required"]));
        var phaseScripts = brief.PhaseScripts.Select(item => item.Trim()).ToArray();
        var creature = new CreatureDefinition(
            StableId(brief.BossId, "creature"), 1, provenance, blueprintId,
            new CreatureIdentity(brief.Slug, new(brief.DisplayName, new Dictionary<string, string>()), [], "iel",
                new(brief.Synopsis, new Dictionary<string, string>()), "Composition boss assistée",
                new HashSet<string> { "boss", "hostile" }),
            new CreatureAuthoringProfile("boss", "core-v1", "humanoid", "hostile", EncounterRole.Boss,
                brief.ThreatRating, "boss-default", new HashSet<string> { "boss", "phase-driven" }),
            TransformValue.Identity,
            [new ResourcePool("health", brief.MaximumHealth, brief.MaximumHealth, 0, true),
             new ResourcePool("power", 100, 100, 2, false)],
            new CreatureMovementProfile(CreatureMovementMode.Grounded, 5, 10, 8, 0, 0, 45),
            "appearance.boss.default", "perception.boss", "combat.boss", null, "inventory.boss",
            new CreatureScriptBindings(null, new Dictionary<string, string>
            {
                ["death"] = brief.DeathScript,
                ["phase"] = phaseScripts[0],
            }),
            Abilities: abilities.Select(item => new CreatureAbilityInstance(item.Id.ToString(), 1, 0,
                item.CooldownSeconds, [], new HashSet<string> { "boss" }, phaseScripts[0])).ToArray());
        CreatureFactory.Validate(creature);

        var shape = new TriggerShape(TriggerShapeKind.Box, [], [], new(14, 5, 14), 0, 0.5);
        var filter = new TriggerFilter(new HashSet<string> { "player" }, new HashSet<string>(),
            new HashSet<string> { "spectator" }, new HashSet<string> { "world" });
        var candidate = new SpawnCandidate(creature.Id, $"creature:{brief.Slug}", EncounterRole.Boss, 1,
            brief.ThreatRating, new HashSet<string>(), ["boss"], 1);
        var encounter = new EncounterDefinition(StableId(brief.BossId, "encounter"), 1, provenance,
            $"{brief.Slug}-encounter", $"{brief.DisplayName} · rencontre", "boss", "phase-driven",
            $"zone:{brief.ZoneId:D}", new EncounterVolume(shape, filter, true, 18, null), [candidate],
            new EncounterDifficultyProfile("core-v1", EncounterDifficulty.Hard, 4,
                Math.Max(1, brief.ThreatRating), brief.ThreatRating, 1, 1), ["boss"], ["boss-anchor"],
            loot.Select(item => item.ItemSlug).ToArray(), 8);
        var thresholds = phaseScripts.Length == 2 ? new[] { 1d, .5d } : new[] { 1d, .66d, .33d };
        var phases = phaseScripts.Select((script, index) => new BossPhasePlan(
            $"phase:{index + 1}", index + 1, thresholds[index],
            abilities.Take(Math.Min(abilities.Length, index + 1)).Select(item => item.Id.ToString()).ToArray(),
            script, index == phaseScripts.Length - 1 ? brief.SummonBlueprintSlugs.ToArray() : [])).ToArray();
        var dialogue = BuildDialogue(brief, blueprintId);
        var boss = new BossOrchestrationDefinition(brief.BossId, 1, provenance, brief.Slug, brief.DisplayName,
            blueprint, creature, encounter, phases, dialogue, loot, brief.DeathScript);
        var availableBlueprints = project.Blueprints.Select(item => item.Slug)
            .Append(brief.Slug).ToHashSet(StringComparer.Ordinal);
        var diagnostics = BossOrchestrationService.Validate(boss, availableScripts, availableBlueprints);
        var balance = BossOrchestrationService.Simulate(boss, 32, brief.Seed);
        if (!balance.Passed)
            diagnostics = [.. diagnostics, new BossDiagnostic("RPG_BOSS_BALANCE_FAILED",
                $"bosses/{brief.BossId:D}/balance", balance.Summary)];
        var styleViolations = brief.StyleRules.Any(string.IsNullOrWhiteSpace)
            ? new[] { "boss.style.empty" }
            : Array.Empty<string>();
        var runtimeJson = BossOrchestrationService.ToCanonicalJson(boss, balance);
        var runtimeDocument = new RuntimeJsonDocumentDefinition(StableId(brief.BossId, "runtime-document"), 1,
            $"boss.{brief.Slug}", RuntimeSchema, runtimeJson, 65_536, 24, false, "1.0", provenance);
        var semanticDiff = $"batch:add-or-replace-blueprint:{blueprint.Id}|dialogue:{dialogue.Id}|runtime:{runtimeDocument.Key}|" +
                           $"abilities:{abilities.Length}|phases:{phases.Length}|rewards:{loot.Length}|balance-runs:{balance.Runs}";
        return new(diagnostics.Count == 0 && styleViolations.Length == 0, boss, runtimeDocument, balance,
            diagnostics, brief.ContextCitations.ToArray(), styleViolations, semanticDiff, promptSha256,
            inputTokens, outputTokens, estimatedCostUsd, providerKey, provenance, false);
    }

    private static DialogueDefinition BuildDialogue(AssistedBossBrief brief, BlueprintId speaker)
    {
        var dialogueId = DialogueId.From(StableId(brief.BossId, "dialogue"));
        var entry = DialogueNodeId.From(StableId(brief.BossId, "dialogue:entry"));
        var combat = DialogueNodeId.From(StableId(brief.BossId, "dialogue:combat"));
        return new DialogueDefinition(dialogueId, $"{brief.Slug}-introduction", $"{brief.DisplayName} · introduction", entry,
        [
            new DialogueNode(entry, speaker, new($"dialogue.{brief.Slug}.intro", brief.Synopsis), [],
            [
                new DialogueChoice(DialogueChoiceId.From(StableId(brief.BossId, "dialogue:challenge")),
                    new($"dialogue.{brief.Slug}.challenge", "Nous mettrons fin à votre règne."), [],
                    [new DialogueAction(DialogueActionKind.SetVariable, $"boss.{brief.Slug}.challenged", "true")], combat),
            ]),
            new DialogueNode(combat, speaker,
                new($"dialogue.{brief.Slug}.combat", "Alors venez affronter votre destin."), [],
            [
                new DialogueChoice(DialogueChoiceId.From(StableId(brief.BossId, "dialogue:start")),
                    new($"dialogue.{brief.Slug}.start", "Au combat."), [],
                    [new DialogueAction(DialogueActionKind.SetVariable, $"boss.{brief.Slug}.active", "true")]),
            ]),
        ]);
    }

    private static void ValidateBrief(AssistedBossBrief brief, ProjectDefinition project,
        IReadOnlySet<string> availableScripts)
    {
        if (brief.BossId == Guid.Empty || brief.BlueprintId == Guid.Empty || string.IsNullOrWhiteSpace(brief.Slug) ||
            string.IsNullOrWhiteSpace(brief.DisplayName) || string.IsNullOrWhiteSpace(brief.Synopsis) ||
            string.IsNullOrWhiteSpace(brief.AuthorId) || brief.MaximumHealth is < 50 or > 1_000_000 ||
            brief.Offense is < 1 or > 100_000 || brief.Defense is < 0 or > 100_000 ||
            brief.ThreatRating is < 1 or > 100_000 || !double.IsFinite(brief.ThreatRating) ||
            brief.AbilityNames.Count is < 2 or > 8 || brief.PhaseScripts.Count is < 2 or > 3 ||
            brief.RewardItemSlugs.Count is < 1 or > 12 || brief.ContextCitations.Count == 0 ||
            brief.StyleRules.Count == 0 || string.IsNullOrWhiteSpace(brief.DeathScript))
            throw new ArgumentException("Assisted boss identity, stats, abilities, phases, rewards, citations and style are invalid.");
        if (project.Areas.All(item => item.Id.Value != brief.ZoneId))
            throw new ArgumentException("Assisted boss zone does not resolve.");
        if (project.Blueprints.Any(item => item.Id.Value != brief.BlueprintId && item.Slug == brief.Slug))
            throw new ArgumentException("Assisted boss slug already belongs to another blueprint.");
        var scripts = brief.PhaseScripts.Append(brief.DeathScript);
        if (scripts.Any(item => string.IsNullOrWhiteSpace(item) || !availableScripts.Contains(item.Trim())))
            throw new ArgumentException("Every boss phase and death script must resolve before composition.");
        if (brief.SummonBlueprintSlugs.Any(slug => project.Blueprints.All(item => item.Slug != slug)))
            throw new ArgumentException("Every boss summon blueprint must resolve before composition.");
    }

    private static string Slug(string value, string fallback)
    {
        var normalized = new string(value.Trim().ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal)) normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
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

    internal static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

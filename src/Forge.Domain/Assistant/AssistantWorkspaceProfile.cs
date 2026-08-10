namespace Forge.Domain.Assistant;

public sealed record AssistantWorkspaceProfile
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultProfileId = Guid.Parse("a1200000-0000-4000-8000-000000000001");
    public static readonly Guid DefaultProviderId = Guid.Parse("a1200000-0000-4000-8000-000000000002");
    public static readonly Guid DefaultPromptId = Guid.Parse("a1200000-0000-4000-8000-000000000003");

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public Guid ActiveProviderId { get; }
    public IReadOnlyList<AssistantProviderDefinition> Providers { get; }
    public IReadOnlyList<AssistantPromptDefinition> Prompts { get; }
    public AssistantPermissionLevel MaximumPermission { get; }
    public bool NetworkAllowed { get; }
    public bool ShareProjectContent { get; }
    public string Provenance { get; }
    public AgentSemanticWorkflow AgentWorkflow { get; }

    public AssistantWorkspaceProfile(
        Guid id,
        int schemaVersion,
        Guid activeProviderId,
        IReadOnlyList<AssistantProviderDefinition>? providers,
        IReadOnlyList<AssistantPromptDefinition>? prompts,
        AssistantPermissionLevel maximumPermission,
        bool networkAllowed,
        bool shareProjectContent,
        string provenance,
        AgentSemanticWorkflow? agentWorkflow = null)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        ActiveProviderId = activeProviderId;
        Providers = (providers ?? []).OrderBy(provider => provider.Key, StringComparer.Ordinal).ToArray();
        Prompts = (prompts ?? [])
            .OrderBy(prompt => prompt.Key, StringComparer.Ordinal)
            .ThenBy(prompt => prompt.Version, StringComparer.Ordinal)
            .ToArray();
        MaximumPermission = maximumPermission;
        NetworkAllowed = networkAllowed;
        ShareProjectContent = shareProjectContent;
        Provenance = (provenance ?? string.Empty).Trim();
        AgentWorkflow = agentWorkflow ?? AgentSemanticWorkflow.CreateDefault();
    }

    public AssistantProviderDefinition ActiveProvider =>
        Providers.Single(provider => provider.Id == ActiveProviderId);

    public static AssistantWorkspaceProfile CreateDefault() =>
        new(
            DefaultProfileId,
            CurrentSchemaVersion,
            DefaultProviderId,
            [new AssistantProviderDefinition(
                DefaultProviderId,
                AssistantProviderDefinition.CurrentSchemaVersion,
                "offline-ci",
                "OpenRPG Forge · hors ligne",
                AssistantProviderKind.DeterministicOffline,
                string.Empty,
                "forge-deterministic-v1",
                string.Empty,
                true,
                true,
                4096,
                1024,
                0,
                0,
                "openrpg-forge:g12-a")],
            [new AssistantPromptDefinition(
                DefaultPromptId,
                AssistantPromptDefinition.CurrentSchemaVersion,
                "assistant.context-summary",
                "1.0.0",
                "Résumé du contexte Creator",
                "Tu assistes un auteur OpenRPG Forge. Réponds uniquement avec un plan JSON sans modifier le projet.",
                "Projet: {{project.title}}\nSélection: {{selection.kind}}\nDemande: {{request.text}}",
                """{"type":"object","required":["summary","semanticDiff","planId","transactionId","permission","promptSha256"],"additionalProperties":false,"properties":{"summary":{"type":"string"},"semanticDiff":{"type":"array","items":{"type":"object"}},"planId":{"type":"string"},"transactionId":{"type":"string"},"permission":{"type":"string"},"promptSha256":{"type":"string"},"toolCalls":{"type":"array","items":{"type":"object","required":["tool","targetObjectId","arguments"],"additionalProperties":false,"properties":{"tool":{"type":"string"},"targetObjectId":{"type":"string"},"arguments":{"type":"object"}}}}}}""",
                ["project.read", "plan.preview"],
                [new AssistantPromptTestCase(
                    Guid.Parse("a1200000-0000-4000-8000-000000000004"),
                    "contexte minimal",
                    new Dictionary<string, string>
                    {
                        ["project.title"] = "Module test",
                        ["selection.kind"] = "Area",
                        ["request.text"] = "Résume la zone",
                    },
                    ["Module test", "Area", "Résume la zone"])],
                [new AssistantPromptChange("1.0.0", "Contrat initial hors ligne avec plan JSON.")],
                "openrpg-forge:g12-a")],
            AssistantPermissionLevel.Configure,
            false,
            false,
            "openrpg-forge:g12-a");
}

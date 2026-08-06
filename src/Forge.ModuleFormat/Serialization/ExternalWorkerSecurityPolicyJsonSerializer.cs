using System.Text.Json;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Serialization;

internal static class ExternalWorkerSecurityPolicyJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";
    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(ExternalWorkerSecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        EnsureValid(policy);
        return JsonSerializer.Serialize(new Document(CurrentFormatVersion, policy), Options);
    }

    internal static ExternalWorkerSecurityPolicy Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<Document>(json, Options)
            ?? throw new JsonException("The external worker security document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
            throw new NotSupportedException($"External worker security format '{document.FormatVersion}' is unsupported.");
        if (document.Policy is null) throw new JsonException("The external worker security policy is missing.");
        EnsureValid(document.Policy);
        return document.Policy;
    }

    private static void EnsureValid(ExternalWorkerSecurityPolicy policy)
    {
        var diagnostic = ExternalWorkerSecurityPolicyValidator.Validate(policy).FirstOrDefault();
        if (diagnostic is not null)
            throw new JsonException($"External worker security policy is invalid: {diagnostic.Code} - {diagnostic.Message}");
    }

    private sealed record Document(string FormatVersion, ExternalWorkerSecurityPolicy? Policy);
}

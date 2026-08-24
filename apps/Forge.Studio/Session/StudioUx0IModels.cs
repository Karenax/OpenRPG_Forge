using Forge.Domain.Modules;

namespace Forge.Studio.Session;

internal sealed record StudioScriptCacheSettings(
    string LocationKey,
    bool IncrementalBuild,
    bool RebuildAffectedOnly,
    bool VerifyReproducibility,
    int ScriptCount,
    bool ExistingConfigurationPreserved);

internal sealed record StudioScriptCacheReport(
    bool Succeeded,
    string Message,
    int ScriptCount,
    int Compiled,
    int Reused,
    string ApiVersion,
    string CompilerVersion);

internal sealed record StudioUx0ISmokeResult(
    bool Succeeded,
    bool VariableScopes,
    bool InvalidVariableBlocked,
    bool PlayerGuide,
    bool ScriptCache,
    bool CacheInvalidation,
    bool DryRun,
    bool AtomicUndoRedo,
    bool PersistenceRoundTrip,
    bool NeighborDataPreserved,
    bool HumanDiagnostics);

internal static class StudioUx0IVariableText
{
    internal static string DisplayName(ProjectVariableDefinition variable) => DisplayName(variable.Key);

    internal static string DisplayName(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var words = key
            .Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TranslateWord)
            .ToArray();
        return words.Length == 0 ? "Variable" : string.Join(" · ", words);
    }

    internal static string BuildKey(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var decomposed = displayName.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var characters = decomposed
            .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
                                System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var slug = string.Join('-', new string(characters)
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (slug.Length == 0)
        {
            throw new ArgumentException("Le nom de la variable doit contenir au moins une lettre ou un chiffre.");
        }
        var key = $"project.{slug}";
        return key.Length <= ProjectVariableDefinition.MaximumKeyLength
            ? key
            : key[..ProjectVariableDefinition.MaximumKeyLength].TrimEnd('-');
    }

    private static string TranslateWord(string word)
    {
        var normalized = word.ToLowerInvariant();
        return normalized switch
        {
            "world" => "Monde",
            "project" => "Projet",
            "session" => "Session",
            "player" => "Joueur",
            "party" => "Groupe",
            "server" => "Serveur",
            "quest" => "Quête",
            "chapter" => "Chapitre",
            "completed" => "Terminée",
            "ready" => "Prête",
            "phase" => "Phase",
            "score" => "Score",
            "difficulty" => "Difficulté",
            _ => char.ToUpperInvariant(word[0]) + word[1..],
        };
    }
}

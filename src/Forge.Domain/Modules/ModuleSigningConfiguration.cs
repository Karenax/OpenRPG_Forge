namespace Forge.Domain.Modules;

public sealed record ModuleSigningConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }

    public ModuleSigningMode Mode { get; }

    public string SigningProfile { get; }

    public IReadOnlyList<string> TrustedKeyFingerprints { get; }

    public IReadOnlyList<string> RevokedKeyFingerprints { get; }

    public bool AllowUnsignedDevelopment { get; }

    public ModuleSigningConfiguration(
        ModuleSigningMode mode,
        string signingProfile,
        IReadOnlyList<string>? trustedKeyFingerprints,
        IReadOnlyList<string>? revokedKeyFingerprints,
        bool allowUnsignedDevelopment,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingProfile);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Module signing schema '{schemaVersion}' is not supported.");
        }

        var trusted = NormalizeFingerprints(trustedKeyFingerprints, nameof(trustedKeyFingerprints));
        var revoked = NormalizeFingerprints(revokedKeyFingerprints, nameof(revokedKeyFingerprints));
        if (mode == ModuleSigningMode.Required && trusted.Count == 0)
        {
            throw new ArgumentException("Required signing must declare at least one trusted key fingerprint.", nameof(trustedKeyFingerprints));
        }

        if (trusted.Intersect(revoked, StringComparer.Ordinal).Any())
        {
            throw new ArgumentException("A signing key cannot be both trusted and revoked.", nameof(revokedKeyFingerprints));
        }

        SchemaVersion = schemaVersion;
        Mode = mode;
        SigningProfile = signingProfile.Trim();
        TrustedKeyFingerprints = trusted;
        RevokedKeyFingerprints = revoked;
        AllowUnsignedDevelopment = allowUnsignedDevelopment;
    }

    private static IReadOnlyList<string> NormalizeFingerprints(IReadOnlyList<string>? values, string parameterName)
    {
        var normalized = (values ?? [])
            .Select(value => value?.Trim().ToLowerInvariant() ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Any(value => value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new ArgumentException("Signing key fingerprints must be 64 hexadecimal SHA-256 characters.", parameterName);
        }

        if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new ArgumentException("Signing key fingerprints must be unique.", parameterName);
        }

        return normalized;
    }
}

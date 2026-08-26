using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Validation;

public sealed class SpdxLicenseValidatorTests
{
    [Theory]
    [InlineData("CC0-1.0")]
    [InlineData("MIT")]
    [InlineData("LicenseRef-StudioRecording-2026")]
    [InlineData("MIT OR (CC0-1.0 AND LicenseRef-StudioRecording-2026)")]
    public void SupportedIdentifiersAndExpressionsAreAccepted(string value)
    {
        Assert.True(SpdxLicenseValidator.IsSupported(value));
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("UNLICENSED")]
    [InlineData("LicenseRef-")]
    [InlineData("MIT OR")]
    [InlineData("MIT CC0-1.0")]
    [InlineData("(MIT OR CC0-1.0")]
    public void UnknownOrMalformedExpressionsAreRejected(string value)
    {
        Assert.False(SpdxLicenseValidator.IsSupported(value));
    }
}

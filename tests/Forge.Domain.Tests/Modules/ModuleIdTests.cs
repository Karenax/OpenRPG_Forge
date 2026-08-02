using Forge.Domain.Modules;

namespace Forge.Domain.Tests.Modules;

public sealed class ModuleIdTests
{
    [Fact]
    public void Parse_RoundTripsCanonicalGuid()
    {
        const string value = "0a8d1557-b84b-424b-8857-e8f0a7ac2765";

        var id = ModuleId.Parse(value);

        Assert.Equal(value, id.ToString());
    }

    [Fact]
    public void From_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => ModuleId.From(Guid.Empty));
    }
}

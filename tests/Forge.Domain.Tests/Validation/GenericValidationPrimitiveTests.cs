using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Validation;

public sealed class GenericValidationPrimitiveTests
{
    [Fact]
    public void TypedReferencesDistinguishMissingWrongTypePackageAndVersion()
    {
        var source = Guid.Parse("a0270000-0000-4000-8000-000000000001");
        var target = Guid.Parse("a0270000-0000-4000-8000-000000000002");
        var resources = new[] { new ValidationResource(target, "area", "private-kit", "1.0.0", false, "areas/target") };
        var references = new[]
        {
            new ValidationReference(source, Guid.NewGuid(), "blueprint", "public-kit", "2.0.0", "refs/missing"),
            new ValidationReference(source, target, "blueprint", "public-kit", "2.0.0", "refs/typed"),
        };

        var diagnostics = TypedReferenceResolver.Validate(resources, references);

        Assert.Contains(diagnostics, item => item.Code == TypedReferenceResolver.MissingCode && item.Path == "refs/missing");
        Assert.Contains(diagnostics, item => item.Code == TypedReferenceResolver.WrongTypeCode);
        Assert.Contains(diagnostics, item => item.Code == TypedReferenceResolver.PackageInaccessibleCode);
        Assert.Contains(diagnostics, item => item.Code == TypedReferenceResolver.VersionMismatchCode);
    }

    [Fact]
    public void GenericGraphReportsMissingUnreachableAndForbiddenCycleDeterministically()
    {
        var graphId = Guid.Parse("a0270000-0000-4000-8000-000000000010");
        var first = Guid.Parse("a0270000-0000-4000-8000-000000000011");
        var second = Guid.Parse("a0270000-0000-4000-8000-000000000012");
        var unreachable = Guid.Parse("a0270000-0000-4000-8000-000000000013");
        var graph = new ValidationGraph(graphId, 1, "test:r027",
            [new(first, "state", "nodes/first"), new(second, "state", "nodes/second"),
             new(unreachable, "state", "nodes/unreachable")],
            [new(Guid.NewGuid(), first, second, "next", "edges/one"),
             new(Guid.NewGuid(), second, first, "next", "edges/two")],
            [first], false);

        var firstRun = GenericGraphValidator.Validate(graph);
        var secondRun = GenericGraphValidator.Validate(graph);

        Assert.Contains(firstRun, item => item.Code == GenericGraphValidator.UnreachableCode);
        Assert.Contains(firstRun, item => item.Code == GenericGraphValidator.CycleCode);
        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public void GeometryPolygonReportsSelfIntersection()
    {
        var diagnostics = GeometryPolygonValidator.Validate(
            [new(0, 0, 0), new(2, 0, 2), new(0, 0, 2), new(2, 0, 0)], "areas/test/collisionPolygon");

        Assert.Equal(GeometryPolygonValidator.SelfIntersectionCode, Assert.Single(diagnostics).Code);
    }
}

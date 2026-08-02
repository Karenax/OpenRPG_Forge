using Forge.Domain.Areas;

namespace Forge.ModuleFormat.Serialization;

internal sealed record AreaDocument(string FormatVersion, AreaDefinition Area);

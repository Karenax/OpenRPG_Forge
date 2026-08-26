using System.Text.Json.Serialization;

namespace Forge.ModuleFormat.Saves;

[JsonConverter(typeof(RuntimeInventorySaveStackJsonConverter))]
public sealed record RuntimeInventorySaveStack(string Slug, int Quantity)
{
    public static implicit operator RuntimeInventorySaveStack(string slug) => new(slug, 1);
}

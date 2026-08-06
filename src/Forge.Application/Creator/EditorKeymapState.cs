using Forge.Domain.Authoring;

namespace Forge.Application.Creator;

public sealed record EditorKeymapState(EditorKeymapProfile Profile, EditorKeymapRevision Revision)
{
    public EditorKeymapState(EditorKeymapProfile? profile = null)
        : this(profile ?? EditorKeymapProfile.CreatePreset(EditorKeymapPreset.Standard), new EditorKeymapRevision(0))
    {
    }
}

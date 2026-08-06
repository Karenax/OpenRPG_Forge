using Forge.Application.Commands;
using Forge.Domain.Authoring;

namespace Forge.Application.Creator;

public sealed record SetEditorKeymapCommand(
    CommandId CommandId,
    EditorKeymapRevision BaseRevision,
    EditorKeymapProfile Profile,
    bool DryRun);

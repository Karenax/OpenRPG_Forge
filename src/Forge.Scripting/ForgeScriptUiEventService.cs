using System.Text.Json;

namespace Forge.Scripting;

public sealed record ScriptUiHandle(Guid Id, string Key);
public sealed record ScriptUiBinding(Guid Id, ScriptUiHandle Document, string WidgetKey, string EventKind,
    string Command, string ForgeScriptApiVersion, string InputContext, string Permission);
public sealed record ScriptUiEvent(Guid CorrelationId, ScriptUiHandle Document, Guid BindingId, string WidgetKey,
    string EventKind, string Command, string InputContext, string Permission, JsonElement Payload, long Sequence);

public sealed class ForgeScriptUiEventService
{
    private readonly Dictionary<(Guid DocumentId, string WidgetKey, string EventKind), ScriptUiBinding> _bindings = [];
    private long _sequence;

    public void Register(ScriptUiBinding binding)
    {
        if (binding.Id == Guid.Empty || binding.Document.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(binding.Document.Key) || string.IsNullOrWhiteSpace(binding.WidgetKey) ||
            string.IsNullOrWhiteSpace(binding.EventKind) || string.IsNullOrWhiteSpace(binding.Command) ||
            !string.Equals(binding.ForgeScriptApiVersion, "1.0", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(binding.InputContext) || string.IsNullOrWhiteSpace(binding.Permission))
            throw new InvalidOperationException("SCRIPT_UI_BINDING_INVALID");
        _bindings[(binding.Document.Id, binding.WidgetKey, binding.EventKind)] = binding;
    }

    public ScriptUiEvent Publish(ScriptUiHandle document, string widgetKey, string eventKind, JsonElement payload,
        string inputContext, string permission)
    {
        if (!_bindings.TryGetValue((document.Id, widgetKey, eventKind), out var binding) ||
            !string.Equals(binding.Document.Key, document.Key, StringComparison.Ordinal))
            throw new InvalidOperationException("SCRIPT_UI_BINDING_MISSING");
        if (!string.Equals(binding.InputContext, inputContext, StringComparison.Ordinal))
            throw new InvalidOperationException("SCRIPT_UI_INPUT_CONTEXT_DENIED");
        if (!string.Equals(binding.Permission, permission, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("SCRIPT_UI_PERMISSION_DENIED");
        return new ScriptUiEvent(Guid.NewGuid(), document, binding.Id, widgetKey, eventKind, binding.Command,
            inputContext, permission, payload.Clone(), checked(++_sequence));
    }
}

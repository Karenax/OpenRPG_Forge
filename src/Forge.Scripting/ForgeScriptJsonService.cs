using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Forge.Scripting;

public sealed record SafeJsonHandle(Guid Id, string Key);
public sealed record SafeJsonQuota(int MaxBytes, int MaxDepth);
public sealed record SafeJsonSnapshot(SafeJsonHandle Handle, long Revision, JsonElement Value);
public sealed record SafeJsonMutationResult(bool Succeeded, SafeJsonSnapshot Snapshot, string Code, string Path);

public sealed class ForgeScriptJsonService
{
    private readonly Dictionary<Guid, Entry> _entries = [];

    public SafeJsonSnapshot Register(Guid id, string key, string schemaJson, string initialValue,
        SafeJsonQuota quota, bool mutable)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(key) || quota.MaxBytes is < 64 or > 1_048_576 ||
            quota.MaxDepth is < 1 or > 64)
            throw new InvalidOperationException("SCRIPT_JSON_DEFINITION_INVALID");
        var schema = Parse(schemaJson, quota.MaxDepth, "SCRIPT_JSON_SCHEMA_INVALID");
        var value = Parse(initialValue, quota.MaxDepth, "SCRIPT_JSON_VALUE_INVALID");
        EnsureQuota(value, quota);
        ValidateSchema(schema, value);
        var handle = new SafeJsonHandle(id, key.Trim().ToLowerInvariant());
        var entry = new Entry(handle, schema, value, quota, mutable, 0);
        _entries[id] = entry;
        return Snapshot(entry);
    }

    public SafeJsonSnapshot Read(SafeJsonHandle handle)
    {
        var entry = Resolve(handle);
        return Snapshot(entry);
    }

    public JsonElement Read(SafeJsonHandle handle, string jsonPointer)
    {
        var current = Resolve(handle).Value;
        foreach (var segment in Segments(jsonPointer))
        {
            current = current switch
            {
                JsonObject obj when obj.TryGetPropertyValue(segment, out var child) && child is not null => child,
                JsonArray array when int.TryParse(segment, out var index) && index >= 0 && index < array.Count &&
                                     array[index] is not null => array[index]!,
                _ => throw new InvalidOperationException($"SCRIPT_JSON_PATH_MISSING:{jsonPointer}"),
            };
        }
        return JsonSerializer.SerializeToElement(current);
    }

    public SafeJsonMutationResult Set(SafeJsonHandle handle, string jsonPointer, JsonElement value,
        long expectedRevision, string permission)
    {
        var entry = Resolve(handle);
        if (!entry.Mutable || permission is not ("module-author" or "server"))
            return new(false, Snapshot(entry), "SCRIPT_JSON_PERMISSION_DENIED", jsonPointer);
        if (expectedRevision != entry.Revision)
            return new(false, Snapshot(entry), "SCRIPT_JSON_REVISION_CONFLICT", jsonPointer);
        try
        {
            var clone = entry.Value.DeepClone();
            SetAtPointer(clone, jsonPointer, JsonNode.Parse(value.GetRawText()));
            EnsureQuota(clone, entry.Quota);
            ValidateSchema(entry.Schema, clone);
            entry.Value = clone;
            entry.Revision = checked(entry.Revision + 1);
            return new(true, Snapshot(entry), "ok", jsonPointer);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return new(false, Snapshot(entry), exception.Message.StartsWith("SCRIPT_JSON_", StringComparison.Ordinal)
                ? exception.Message : "SCRIPT_JSON_MUTATION_INVALID", jsonPointer);
        }
    }

    private Entry Resolve(SafeJsonHandle handle) =>
        !_entries.TryGetValue(handle.Id, out var entry) || !string.Equals(entry.Handle.Key, handle.Key, StringComparison.Ordinal)
            ? throw new InvalidOperationException("SCRIPT_JSON_HANDLE_INVALID") : entry;

    private static SafeJsonSnapshot Snapshot(Entry entry) =>
        new(entry.Handle, entry.Revision, JsonSerializer.SerializeToElement(entry.Value));

    private static JsonNode Parse(string json, int maxDepth, string code)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = maxDepth });
            return JsonNode.Parse(document.RootElement.GetRawText()) ?? throw new InvalidOperationException(code);
        }
        catch (JsonException exception) { throw new InvalidOperationException(code, exception); }
    }

    private static void EnsureQuota(JsonNode value, SafeJsonQuota quota)
    {
        var json = value.ToJsonString();
        if (Encoding.UTF8.GetByteCount(json) > quota.MaxBytes)
            throw new InvalidOperationException("SCRIPT_JSON_BYTE_QUOTA_EXCEEDED");
        using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = quota.MaxDepth });
    }

    private static void ValidateSchema(JsonNode schema, JsonNode value)
    {
        if (schema is not JsonObject schemaObject) throw new InvalidOperationException("SCRIPT_JSON_SCHEMA_INVALID");
        var expectedType = schemaObject["type"]?.GetValue<string>();
        var typeMatches = expectedType switch
        {
            null => true,
            "object" => value is JsonObject,
            "array" => value is JsonArray,
            "string" => value is JsonValue scalar && scalar.TryGetValue<string>(out _),
            "number" => value is JsonValue number && (number.TryGetValue<double>(out _) || number.TryGetValue<decimal>(out _)),
            "integer" => value is JsonValue integer && integer.TryGetValue<long>(out _),
            "boolean" => value is JsonValue boolean && boolean.TryGetValue<bool>(out _),
            "null" => value is null,
            _ => throw new InvalidOperationException("SCRIPT_JSON_SCHEMA_UNSUPPORTED"),
        };
        if (!typeMatches) throw new InvalidOperationException("SCRIPT_JSON_SCHEMA_TYPE_MISMATCH");
        if (value is JsonObject obj && schemaObject["required"] is JsonArray required &&
            required.Any(item => item is not null && !obj.ContainsKey(item.GetValue<string>())))
            throw new InvalidOperationException("SCRIPT_JSON_SCHEMA_REQUIRED_MISSING");
    }

    private static void SetAtPointer(JsonNode root, string pointer, JsonNode? value)
    {
        var segments = Segments(pointer).ToArray();
        if (segments.Length == 0) throw new InvalidOperationException("SCRIPT_JSON_ROOT_MUTATION_DENIED");
        JsonNode current = root;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            current = current switch
            {
                JsonObject obj when obj[segments[index]] is { } child => child,
                JsonArray array when int.TryParse(segments[index], out var itemIndex) && itemIndex >= 0 &&
                                     itemIndex < array.Count && array[itemIndex] is { } child => child,
                _ => throw new InvalidOperationException("SCRIPT_JSON_PATH_MISSING"),
            };
        }
        var last = segments[^1];
        switch (current)
        {
            case JsonObject obj: obj[last] = value; break;
            case JsonArray array when int.TryParse(last, out var itemIndex) && itemIndex >= 0 && itemIndex < array.Count:
                array[itemIndex] = value; break;
            default: throw new InvalidOperationException("SCRIPT_JSON_PATH_INVALID");
        }
    }

    private static IEnumerable<string> Segments(string pointer)
    {
        if (pointer == string.Empty) yield break;
        if (!pointer.StartsWith("/", StringComparison.Ordinal))
            throw new InvalidOperationException("SCRIPT_JSON_POINTER_INVALID");
        foreach (var segment in pointer[1..].Split('/'))
            yield return segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
    }

    private sealed class Entry(SafeJsonHandle handle, JsonNode schema, JsonNode value, SafeJsonQuota quota,
        bool mutable, long revision)
    {
        public SafeJsonHandle Handle { get; } = handle;
        public JsonNode Schema { get; } = schema;
        public JsonNode Value { get; set; } = value;
        public SafeJsonQuota Quota { get; } = quota;
        public bool Mutable { get; } = mutable;
        public long Revision { get; set; } = revision;
    }
}

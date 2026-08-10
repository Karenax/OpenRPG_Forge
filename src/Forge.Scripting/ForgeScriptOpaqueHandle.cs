namespace Forge.Scripting;

public enum ForgeScriptHandleKind : byte
{
    Entity = 1,
    Area = 2,
    Asset = 3,
    Effect = 4,
    MovementTask = 5,
    ContentPackage = 6,
    LocalizationMessage = 7,
    ContentResource = 8,
    AssetGovernance = 9,
}

public readonly record struct ForgeScriptOpaqueHandle(ulong Value)
{
    public ForgeScriptHandleKind Kind => (ForgeScriptHandleKind)(Value >> 56);
    public uint Generation => (uint)((Value >> 32) & 0x00FF_FFFFUL);
    public uint Slot => (uint)(Value & 0xFFFF_FFFFUL);
    public bool IsNull => Value == 0;

    public static ForgeScriptOpaqueHandle Create(ForgeScriptHandleKind kind, uint generation, uint slot)
    {
        if (!Enum.IsDefined(kind) || generation is 0 or > 0x00FF_FFFF || slot == 0)
            throw new ArgumentOutOfRangeException(nameof(generation), "Handle kind, generation and slot must be valid.");
        return new(((ulong)kind << 56) | ((ulong)generation << 32) | slot);
    }
}

public sealed record ForgeScriptHandleDiagnostic(string Code, string Path, Guid? SubjectId, string Message);

public sealed class ForgeScriptHandleRegistry
{
    private sealed record Slot(ForgeScriptHandleKind Kind, uint Generation, Guid SubjectId, bool Active);
    private readonly Dictionary<uint, Slot> _slots = [];
    private readonly Queue<uint> _available = [];
    private uint _nextSlot = 1;

    public ForgeScriptOpaqueHandle Register(ForgeScriptHandleKind kind, Guid subjectId)
    {
        if (!Enum.IsDefined(kind) || subjectId == Guid.Empty)
            throw new ArgumentException("A typed handle needs a valid kind and subject UUID.");
        var slot = _available.Count > 0 ? _available.Dequeue() : _nextSlot++;
        var generation = _slots.TryGetValue(slot, out var previous) ? checked(previous.Generation + 1) : 1;
        if (generation > 0x00FF_FFFF) throw new InvalidOperationException("script.handle.generation_exhausted");
        _slots[slot] = new(kind, generation, subjectId, true);
        return ForgeScriptOpaqueHandle.Create(kind, generation, slot);
    }

    public bool TryResolve(ForgeScriptOpaqueHandle handle, ForgeScriptHandleKind expectedKind, out Guid subjectId,
        out ForgeScriptHandleDiagnostic? diagnostic)
    {
        subjectId = Guid.Empty;
        if (handle.IsNull || handle.Kind != expectedKind)
        {
            diagnostic = new("script.handle.type_invalid", "script/handle", null,
                $"Expected {expectedKind} handle, received {(handle.IsNull ? "null" : handle.Kind)}.");
            return false;
        }
        if (!_slots.TryGetValue(handle.Slot, out var slot) || !slot.Active || slot.Generation != handle.Generation ||
            slot.Kind != expectedKind)
        {
            diagnostic = new("script.handle.stale", $"script/handles/{handle.Slot}", null,
                "The opaque handle is unknown, released or from an earlier generation.");
            return false;
        }
        subjectId = slot.SubjectId;
        diagnostic = null;
        return true;
    }

    public ForgeScriptHandleDiagnostic? Release(ForgeScriptOpaqueHandle handle, ForgeScriptHandleKind expectedKind)
    {
        if (!TryResolve(handle, expectedKind, out var subjectId, out var diagnostic)) return diagnostic;
        var slot = _slots[handle.Slot];
        _slots[handle.Slot] = slot with { Active = false };
        _available.Enqueue(handle.Slot);
        return new("script.handle.released", $"script/handles/{handle.Slot}", subjectId,
            "The handle was released and its slot generation will advance before reuse.");
    }
}

using Forge.Domain.WorldGeneration;
using Godot;

namespace Forge.EngineAdapter.WorldGeneration;

[GlobalClass]
public partial class GodotWorldGenerationBridge : Node
{
    public const string HandlerMethod = "execute_forge_world_generation_step";
    public const string Terrain3DGroup = "forge_world_generation_terrain3d";
    public const string PcgGodotGroup = "forge_world_generation_pcgodot";
    public const string NativeGroup = "forge_world_generation_native";

    [Signal]
    public delegate void StepDispatchedEventHandler(string backend, string operation, string requestJson);

    [Export]
    public bool AllowNativeFallback { get; set; } = true;

    public Godot.Collections.Dictionary Dispatch(string requestJson)
    {
        var response = new Godot.Collections.Dictionary
        {
            ["succeeded"] = false,
            ["fallbackUsed"] = false,
            ["diagnostic"] = string.Empty,
        };
        WorldGenerationBridgeRequest request;
        try
        {
            request = WorldGenerationBridgeProtocol.DeserializeRequest(requestJson);
        }
        catch (Exception exception)
        {
            response["diagnostic"] = exception.Message;
            return response;
        }

        var requestedGroup = GroupFor(request.Step.Backend);
        if (TryDispatch(requestedGroup, requestJson))
        {
            response["succeeded"] = true;
            response["backend"] = request.Step.Backend.ToString();
            EmitSignal(SignalName.StepDispatched, request.Step.Backend.ToString(), request.Step.Operation, requestJson);
            return response;
        }

        if (AllowNativeFallback && request.Step.Backend != WorldGenerationBackend.GodotNative &&
            TryDispatch(NativeGroup, requestJson))
        {
            response["succeeded"] = true;
            response["fallbackUsed"] = true;
            response["backend"] = WorldGenerationBackend.GodotNative.ToString();
            EmitSignal(SignalName.StepDispatched,
                WorldGenerationBackend.GodotNative.ToString(), request.Step.Operation, requestJson);
            return response;
        }

        response["backend"] = request.Step.Backend.ToString();
        response["diagnostic"] = $"No node in group '{requestedGroup}' implements '{HandlerMethod}'.";
        return response;
    }

    private bool TryDispatch(string group, string requestJson)
    {
        var tree = GetTree();
        if (tree is null) return false;
        foreach (var candidate in tree.GetNodesInGroup(group))
        {
            if (candidate is not GodotObject handler || !handler.HasMethod(HandlerMethod)) continue;
            handler.Call(HandlerMethod, requestJson);
            return true;
        }
        return false;
    }

    private static string GroupFor(WorldGenerationBackend backend) => backend switch
    {
        WorldGenerationBackend.Terrain3D => Terrain3DGroup,
        WorldGenerationBackend.PcgGodot => PcgGodotGroup,
        _ => NativeGroup,
    };
}

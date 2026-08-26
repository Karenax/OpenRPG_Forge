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

    public event Action<string, string, string>? StepDispatched;

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
        if (TryDispatch(requestedGroup, requestJson, out var requestedResponse) &&
            Succeeded(requestedResponse))
        {
            requestedResponse["backend"] = request.Step.Backend.ToString();
            StepDispatched?.Invoke(request.Step.Backend.ToString(), request.Step.Operation, requestJson);
            return requestedResponse;
        }

        if (AllowNativeFallback && request.Step.Backend != WorldGenerationBackend.GodotNative &&
            TryDispatch(NativeGroup, requestJson, out var fallbackResponse) && Succeeded(fallbackResponse))
        {
            fallbackResponse["fallbackUsed"] = true;
            fallbackResponse["backend"] = WorldGenerationBackend.GodotNative.ToString();
            StepDispatched?.Invoke(
                WorldGenerationBackend.GodotNative.ToString(), request.Step.Operation, requestJson);
            return fallbackResponse;
        }

        response["backend"] = request.Step.Backend.ToString();
        response["diagnostic"] = $"No node in group '{requestedGroup}' implements '{HandlerMethod}'.";
        return response;
    }

    private bool TryDispatch(
        string group,
        string requestJson,
        out Godot.Collections.Dictionary response)
    {
        response = new();
        var tree = GetTree();
        if (tree is null) return false;
        foreach (var candidate in tree.GetNodesInGroup(group))
        {
            if (candidate is not GodotObject handler || !handler.HasMethod(HandlerMethod)) continue;
            var result = handler.Call(HandlerMethod, requestJson);
            if (result.VariantType != Variant.Type.Dictionary) continue;
            response = result.AsGodotDictionary();
            return true;
        }
        return false;
    }

    private static bool Succeeded(Godot.Collections.Dictionary response) =>
        response.ContainsKey("succeeded") && response["succeeded"].AsBool();

    private static string GroupFor(WorldGenerationBackend backend) => backend switch
    {
        WorldGenerationBackend.Terrain3D => Terrain3DGroup,
        WorldGenerationBackend.PcgGodot => PcgGodotGroup,
        _ => NativeGroup,
    };
}

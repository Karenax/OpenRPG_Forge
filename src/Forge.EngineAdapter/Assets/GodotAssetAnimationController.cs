using Godot;

namespace Forge.EngineAdapter.Assets;

public sealed class GodotAssetAnimationState
{
    internal GodotAssetAnimationState(
        AnimationPlayer player,
        string idleClip,
        string runClip,
        string jumpClip,
        string? attackClip,
        string? hitClip,
        string? deathClip,
        IReadOnlyList<string> clips)
    {
        Player = player;
        IdleClip = idleClip;
        RunClip = runClip;
        JumpClip = jumpClip;
        AttackClip = attackClip;
        HitClip = hitClip;
        DeathClip = deathClip;
        Clips = clips;
    }

    public AnimationPlayer Player { get; }

    public string IdleClip { get; }

    public string RunClip { get; }

    public string JumpClip { get; }

    public string? AttackClip { get; }

    public string? HitClip { get; }

    public string? DeathClip { get; }

    public IReadOnlyList<string> Clips { get; }

    public string? CurrentClip { get; internal set; }
}

public static class GodotAssetAnimationController
{
    public static GodotAssetAnimationState Configure(
        Node root,
        string idle = "idle",
        string run = "run",
        string jump = "jump",
        string? attack = null,
        string? hit = null,
        string? death = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var player = FindAnimationPlayer(root) ?? throw new InvalidDataException(
            "The animated model does not contain an AnimationPlayer.");
        var clips = player.GetAnimationList()
            .Select(name => name.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var idleClip = ResolveClip(clips, idle);
        var runClip = ResolveClip(clips, run);
        var jumpClip = ResolveClip(clips, jump);
        var attackClip = ResolveOptionalClip(clips, attack);
        var hitClip = ResolveOptionalClip(clips, hit);
        var deathClip = ResolveOptionalClip(clips, death);

        player.Active = true;
        SetLoopMode(player, idleClip, Animation.LoopModeEnum.Linear);
        SetLoopMode(player, runClip, Animation.LoopModeEnum.Linear);
        SetLoopMode(player, jumpClip, Animation.LoopModeEnum.None);
        SetOptionalLoopMode(player, attackClip, Animation.LoopModeEnum.None);
        SetOptionalLoopMode(player, hitClip, Animation.LoopModeEnum.None);
        SetOptionalLoopMode(player, deathClip, Animation.LoopModeEnum.None);
        return new GodotAssetAnimationState(
            player,
            idleClip,
            runClip,
            jumpClip,
            attackClip,
            hitClip,
            deathClip,
            clips);
    }

    public static bool Play(
        GodotAssetAnimationState state,
        string clip,
        double blendSeconds = .12,
        bool restart = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(clip);
        if (!state.Clips.Contains(clip, StringComparer.Ordinal)) return false;
        if (!restart && string.Equals(state.CurrentClip, clip, StringComparison.Ordinal)) return true;

        state.Player.Play(clip, blendSeconds);
        state.CurrentClip = clip;
        return true;
    }

    public static double DurationSeconds(GodotAssetAnimationState state, string clip)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(clip);
        var animation = state.Player.GetAnimation(clip) ?? throw new InvalidDataException(
            $"AnimationPlayer could not resolve clip '{clip}'.");
        return animation.Length;
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer player) return player;
        foreach (Node child in node.GetChildren())
        {
            var result = FindAnimationPlayer(child);
            if (result is not null) return result;
        }

        return null;
    }

    internal static string ResolveClip(IReadOnlyList<string> clips, string requested)
    {
        var exact = clips.FirstOrDefault(candidate =>
            string.Equals(candidate, requested, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var qualified = clips.FirstOrDefault(candidate =>
            string.Equals(ClipLeaf(candidate), requested, StringComparison.OrdinalIgnoreCase));
        if (qualified is not null) return qualified;

        // Godot's glTF importer consumes the conventional `_loop` suffix as an
        // import hint and exposes the animation without that suffix.
        var importName = requested.EndsWith("_loop", StringComparison.OrdinalIgnoreCase)
            ? requested[..^5]
            : requested;
        var importedLoop = clips.FirstOrDefault(candidate =>
            string.Equals(ClipLeaf(candidate), importName, StringComparison.OrdinalIgnoreCase));
        return importedLoop ?? throw new InvalidDataException(
            $"Required animation clip '{requested}' is missing. Available clips: {string.Join(", ", clips)}.");
    }

    private static string? ResolveOptionalClip(IReadOnlyList<string> clips, string? requested) =>
        string.IsNullOrWhiteSpace(requested) ? null : ResolveClip(clips, requested);

    private static string ClipLeaf(string clip)
    {
        var separator = clip.LastIndexOfAny(['/', '|', ':']);
        return separator >= 0 ? clip[(separator + 1)..] : clip;
    }

    private static void SetLoopMode(AnimationPlayer player, string clip, Animation.LoopModeEnum loopMode)
    {
        var animation = player.GetAnimation(clip) ?? throw new InvalidDataException(
            $"AnimationPlayer could not resolve clip '{clip}'.");
        animation.LoopMode = loopMode;
    }

    private static void SetOptionalLoopMode(
        AnimationPlayer player,
        string? clip,
        Animation.LoopModeEnum loopMode)
    {
        if (clip is not null) SetLoopMode(player, clip, loopMode);
    }
}

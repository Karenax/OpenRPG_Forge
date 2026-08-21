using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    internal bool IsSystemicRuntimeInitialized => _initializationComplete;

    internal bool IsCryptSystemicModule => _isCryptPrototype;

    internal bool IsCryptSystemicGameplayReady =>
        _initializationComplete &&
        _isCryptPrototype &&
        _player is not null &&
        _camera is not null;

    internal PlayerController? SystemicPlayer => _player;

    internal Camera3D? SystemicCamera => _camera;

    internal IReadOnlyList<Node3D> FindLiveSystemicHostiles(Vector3 origin, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(radius, 0);

        return _hostiles
            .Where(pair =>
                GodotObject.IsInstanceValid(pair.Key) &&
                !pair.Value.IsDead &&
                pair.Key.GlobalPosition.DistanceTo(origin) <= radius)
            .OrderBy(pair => pair.Key.GlobalPosition.DistanceSquaredTo(origin))
            .Select(pair => pair.Key)
            .ToArray();
    }

    internal Node3D? FindFocusedSystemicHostile(float maximumDistance, float minimumAlignment)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumDistance, 0);
        if (!float.IsFinite(minimumAlignment) || minimumAlignment is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAlignment));
        }

        if (_player is null)
        {
            return null;
        }

        var origin = _player.GlobalPosition + (Vector3.Up * .8f);
        var forward = _camera is null
            ? -_player.GlobalTransform.Basis.Z
            : -_camera.GlobalTransform.Basis.Z;
        forward.Y = 0;
        forward = forward.LengthSquared() <= .001f ? Vector3.Forward : forward.Normalized();

        return _hostiles
            .Where(pair => GodotObject.IsInstanceValid(pair.Key) && !pair.Value.IsDead)
            .Select(pair =>
            {
                var offset = pair.Key.GlobalPosition - origin;
                offset.Y = 0;
                var distance = offset.Length();
                var alignment = distance <= .001f ? 1 : forward.Dot(offset / distance);
                return new { pair.Key, Distance = distance, Alignment = alignment };
            })
            .Where(candidate =>
                candidate.Distance <= maximumDistance &&
                candidate.Alignment >= minimumAlignment)
            .OrderByDescending(candidate => candidate.Alignment)
            .ThenBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Key)
            .FirstOrDefault();
    }

    internal void PushSystemicHostile(Node3D hostile, Vector3 direction, float force)
    {
        ArgumentNullException.ThrowIfNull(hostile);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(force, 0);

        if (!_hostiles.TryGetValue(hostile, out var state) ||
            state.IsDead ||
            !GodotObject.IsInstanceValid(hostile))
        {
            return;
        }

        direction.Y = 0;
        if (direction.LengthSquared() <= .001f)
        {
            direction = Vector3.Forward;
        }

        direction = direction.Normalized();
        if (hostile is CharacterBody3D character)
        {
            character.Velocity += (direction * force) + (Vector3.Up * MathF.Min(2.2f, force * .18f));
        }
        else if (hostile is RigidBody3D rigidBody)
        {
            rigidBody.ApplyCentralImpulse((direction * force) + (Vector3.Up * MathF.Min(2.2f, force * .18f)));
        }
        else
        {
            var displacement = direction * Math.Clamp(force * .38f, .65f, 4.5f);
            hostile.GlobalPosition += displacement;
            hostile.ResetPhysicsInterpolation();
        }

        PresentSystemicMessage(
            $"{ResolveSystemicHostileName(hostile)} est repoussé par le décor.",
            "push");
    }

    internal int DamageSystemicHostile(Node3D hostile, int strikes, string cause)
    {
        ArgumentNullException.ThrowIfNull(hostile);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(strikes, 0);
        ArgumentException.ThrowIfNullOrWhiteSpace(cause);

        if (!_hostiles.TryGetValue(hostile, out var target) ||
            target.IsDead ||
            _playerCombatant is null)
        {
            return 0;
        }

        var healthBefore = target.Health;
        for (var strike = 0; strike < strikes && !target.IsDead; strike++)
        {
            PrepareSystemicStrike();
            DamageHostile(hostile);
        }

        var applied = Math.Max(0, healthBefore - target.Health);
        if (applied > 0)
        {
            PresentSystemicMessage(
                $"{cause} inflige {applied} dégât{(applied > 1 ? "s" : string.Empty)} " +
                $"à {ResolveSystemicHostileName(hostile)}.",
                "damage");
        }

        return applied;
    }

    internal bool DefeatSystemicHostile(Node3D hostile, string cause)
    {
        ArgumentNullException.ThrowIfNull(hostile);
        ArgumentException.ThrowIfNullOrWhiteSpace(cause);

        if (!_hostiles.TryGetValue(hostile, out var target) || target.IsDead)
        {
            return false;
        }

        // The existing combat path owns death presentation, loot, scripting and quest progression.
        // Reusing it preserves attribution and avoids a second environmental health model.
        for (var strike = 0; strike < 64 && !target.IsDead; strike++)
        {
            PrepareSystemicStrike();
            DamageHostile(hostile);
        }

        if (!target.IsDead)
        {
            GD.PushWarning(
                $"OPENRPG_FORGE_CRYPT_SYSTEMIC_DEFEAT_INCOMPLETE target={hostile.Name} " +
                $"remaining_health={target.Health}");
            return false;
        }

        PresentSystemicMessage(
            $"{ResolveSystemicHostileName(hostile)} est vaincu par {cause}.",
            "environmental-defeat");
        return true;
    }

    private void PrepareSystemicStrike()
    {
        if (_playerCombatant is null)
        {
            return;
        }

        var ability = _playerCombatant.Profile.Abilities.FirstOrDefault()
            ?? throw new InvalidOperationException("The player combat profile has no attack ability.");
        var requiredResource = Math.Min(
            _playerCombatant.Profile.Stats.MaximumResource,
            Math.Max(_playerCombatant.Resource, ability.ResourceCost));

        // Environmental chains are resolved through the authoritative attack path so that
        // phase changes, creature events, loot, progression and quest transitions stay intact.
        // Clearing the active cooldown between strikes models one environmental impact with
        // several damage pulses; the last pulse still leaves the normal cooldown active.
        _playerCombatant.Restore(_playerCombatant.Health, requiredResource);
    }

    internal void PresentSystemicMessage(string message, string eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        _message = message.Trim();
        UpdateHud();
        GD.Print(
            $"OPENRPG_FORGE_CRYPT_SYSTEMIC_EVENT id={eventId.Trim()} " +
            $"message={message.Trim().Replace(' ', '_')}");
    }

    private string ResolveSystemicHostileName(Node3D hostile) =>
        _hostileNames.TryGetValue(hostile, out var displayName) &&
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : hostile.Name.ToString();
}

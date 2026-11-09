using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Cutsy.Core;

public enum Facing { None, Front, Left, Right }
public enum SurfaceKind { Floor, WindowTop, WindowLeft, WindowRight, WindowBottom }
public enum BehaviorFamily { Idle, Movement, Edge, Reaction }
public enum StepType { PlayFrames, Hold, MoveAlongSurface, Jump, Fall, Attach, Detach, React, PlaySound }
public enum Interaction { Click, Pickup, Drop, LostSupport, Land }
public enum InterruptPolicy { Interruptible, Finish, Loop }

public readonly record struct Bounds(int X, int Y, int Width, int Height)
{
    [JsonIgnore] public int Right => X + Width;
    [JsonIgnore] public int Bottom => Y + Height;
}

public sealed record SpriteSpec(
    string Id, string Path, string SourceAssetId, Facing Facing, Bounds ContentBounds,
    ImmutableDictionary<string, WorldPoint> Anchors, double ScaleCorrection, string MaskPath,
    ImmutableArray<string> BakedInProps = default);
public sealed record SoundSpec(string Id, string Path, double Gain);
public sealed record PackManifest(
    int SchemaVersion, string Id, string DisplayName, string Version, int ReferenceHeightPx,
    string DefaultBehaviorId, ImmutableArray<SpriteSpec> Sprites, ImmutableArray<SoundSpec> Sounds, string BehaviorFile);
public sealed record Frame(string SpriteId, int DurationMs);
public sealed record Transition(string BehaviorId, double Weight);
public sealed record Eligibility(
    SurfaceKind? SurfaceKind = null, Facing? Facing = null, bool? Grounded = null, bool? Attached = null,
    Interaction? Interaction = null, double MinimumWidth = 0, SurfaceKind? NearbySurface = null);
public sealed record Step(
    StepType Type, int DurationMs = 0, string? SpriteId = null, ImmutableArray<Frame> Frames = default,
    double Speed = 0, double Height = 0, SurfaceKind? Target = null, string? Anchor = null,
    string? SoundId = null, bool Clip = false, double PeekSlide = 0);
public sealed record Behavior(
    string Id, BehaviorFamily Family, double SelectionWeight, int CooldownMs,
    ImmutableArray<string> RequiredCapabilities, Eligibility Eligibility, InterruptPolicy InterruptPolicy,
    ImmutableArray<Step> Steps, ImmutableArray<Transition> Next);
public sealed record BehaviorFile(int SchemaVersion, ImmutableArray<Behavior> Behaviors);

public sealed class ValidatedPack
{
    public PackManifest Manifest { get; }
    public ImmutableDictionary<string, SpriteSpec> Sprites { get; }
    public ImmutableDictionary<string, SoundSpec> Sounds { get; }
    public ImmutableDictionary<string, Behavior> Behaviors { get; }
    public ImmutableArray<Behavior> SelectionOrder { get; }
    public ImmutableArray<string> Warnings { get; }

    internal ValidatedPack(PackManifest manifest, BehaviorFile behaviors,
        ImmutableDictionary<string, SoundSpec> sounds, ImmutableArray<string> warnings)
    {
        Manifest = manifest;
        Sprites = manifest.Sprites.ToImmutableDictionary(s => s.Id, StringComparer.Ordinal);
        Sounds = sounds;
        SelectionOrder = behaviors.Behaviors;
        Behaviors = SelectionOrder.ToImmutableDictionary(b => b.Id, StringComparer.Ordinal);
        Warnings = warnings;
    }
}

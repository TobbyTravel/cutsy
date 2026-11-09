using System.Collections.Immutable;

namespace Cutsy.Core;

public sealed record MonitorArea(Bounds WorkArea, double Scale);

public sealed record Surface(string Id, SurfaceKind Kind, WorldPoint Origin,
    double Start, double End, double Line, double Clearance)
{
    public bool Vertical => Kind is SurfaceKind.WindowLeft or SurfaceKind.WindowRight;
    public bool SupportsFeet => Kind is SurfaceKind.Floor or SurfaceKind.WindowTop;

    public WorldPoint Contact(double coordinate) => Vertical
        ? new WorldPoint(Line, coordinate)
        : new WorldPoint(coordinate, Line);

    public double Along(WorldPoint point) => Vertical ? point.Y : point.X;

    public Bounds? PeekClip => Kind switch
    {
        SurfaceKind.WindowLeft => new Bounds(-1000000, -1000000, (int)Line + 1000000, 2000000),
        SurfaceKind.WindowRight => new Bounds((int)Line, -1000000, 1000000 - (int)Line, 2000000),
        SurfaceKind.WindowTop or SurfaceKind.Floor => new Bounds(-1000000, -1000000, 2000000, (int)Line + 1000000),
        SurfaceKind.WindowBottom => new Bounds(-1000000, (int)Line, 2000000, 1000000 - (int)Line),
        _ => null
    };
}

public sealed record DesktopSnapshot(ImmutableArray<MonitorArea> Monitors, ImmutableArray<Surface> Surfaces,
    ImmutableHashSet<string> Capabilities, bool Quiet = false, WorldPoint? AttentionPoint = null,
    string? PreferredSurfaceId = null);

public readonly record struct RenderPose(string SpriteId, WorldPoint Contact, double CharacterHeight,
    string Anchor, Facing Facing, Bounds? Clip);
public readonly record struct SoundRequest(string Id, bool Interaction);

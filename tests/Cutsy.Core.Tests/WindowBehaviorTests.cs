using System.Collections.Immutable;
using System.Text;

namespace Cutsy.Core.Tests;

public sealed class WindowBehaviorTests
{
    [Fact]
    public void PeekTracksMovingWindowAndLosesItsClipWhenWindowCloses()
    {
        var floor = new Surface("floor", SurfaceKind.Floor, new WorldPoint(0, 800), 0, 1200, 800, 800);
        var edge = new Surface("active", SurfaceKind.WindowLeft, new WorldPoint(900, 220), 220, 730, 900, 500);
        var desktop = new DesktopSnapshot([new MonitorArea(new Bounds(0, 0, 1200, 800), 1)],
            [floor, edge], ImmutableHashSet<string>.Empty, PreferredSurfaceId: edge.Id);
        var engine = new BehaviorEngine(Load("""
            { "id": "peek", "family": "edge", "selectionWeight": 1, "cooldownMs": 0,
              "requiredCapabilities": [], "eligibility": {}, "interruptPolicy": "interruptible",
              "steps": [{ "type": "attach", "target": "windowLeft", "spriteId": "dot",
                          "durationMs": 1600, "clip": true, "peekSlide": 0.4 }], "next": [] }
            """), desktop, seed: 18);

        Assert.True(engine.Preview("peek"));
        engine.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(900, engine.Pose.Clip?.Right);
        Assert.True(engine.Pose.Contact.X > engine.Contact.X);

        var shifted = edge with { Line = 875, Origin = new WorldPoint(875, 220) };
        engine.SetDesktop(desktop with { Surfaces = [floor, shifted] });
        Assert.Equal(875, engine.Pose.Clip?.Right);
        Assert.Equal(875, engine.Contact.X);

        engine.SetDesktop(desktop with { Surfaces = [floor] });
        Assert.Null(engine.Pose.Clip);
        for (int i = 0; i < 15; i++)
            engine.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(800, engine.Contact.Y);
    }

    [Fact]
    public void ApproachPrefersTheActiveWindowWhenItIsReachable()
    {
        var floor = new Surface("floor", SurfaceKind.Floor, new WorldPoint(0, 800), 0, 1200, 800, 800);
        var nearer = new Surface("other", SurfaceKind.WindowTop, new WorldPoint(800, 610), 800, 1200, 610, 610);
        var active = nearer with { Id = "active", Line = 570, Origin = new WorldPoint(800, 570) };
        var desktop = new DesktopSnapshot([new MonitorArea(new Bounds(0, 0, 1200, 800), 1)],
            [floor, nearer, active], ImmutableHashSet<string>.Empty, PreferredSurfaceId: active.Id);
        var engine = new BehaviorEngine(Load("""
            { "id": "perch", "family": "edge", "selectionWeight": 1, "cooldownMs": 0,
              "requiredCapabilities": [], "eligibility": {}, "interruptPolicy": "interruptible",
              "steps": [{ "type": "jump", "target": "windowTop", "spriteId": "dot",
                          "durationMs": 300, "height": 0.2 }], "next": [] }
            """), desktop, seed: 18);

        Assert.True(engine.Preview("perch"));
        engine.Advance(TimeSpan.FromMilliseconds(190));
        engine.Advance(TimeSpan.FromMilliseconds(150));
        Assert.Equal(active.Line, engine.Contact.Y);
    }

    private static ValidatedPack Load(string recipe) => PackLoader.Load((path, _) => path == "behaviors.json"
        ? Encoding.UTF8.GetBytes("""
            { "schemaVersion": 1, "behaviors": [
              { "id": "idle", "family": "idle", "selectionWeight": 1, "cooldownMs": 0,
                "requiredCapabilities": [], "eligibility": {}, "interruptPolicy": "interruptible",
                "steps": [{ "type": "hold", "spriteId": "dot", "durationMs": 2000 }], "next": [] },
            """ + recipe + " ] }")
        : File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "example", path)));
}

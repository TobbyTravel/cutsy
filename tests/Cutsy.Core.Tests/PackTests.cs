using System.Collections.Immutable;
using System.Text;

namespace Cutsy.Core.Tests;

public sealed class PackTests
{
    [Fact]
    public void ClickJumpsAndReturnsToTheFloor()
    {
        var pack = PackLoader.Load(ReadExample);
        var monitor = new MonitorArea(new Bounds(-1200, -800, 1200, 800), 1.5);
        var floor = new Surface("floor", SurfaceKind.Floor, new WorldPoint(-1200, 0), -1200, 0, 0, 800);
        var desktop = new DesktopSnapshot([monitor], [floor], ImmutableHashSet.Create("pointer"));
        var engine = new BehaviorEngine(pack, desktop, seed: 40);
        Assert.Equal(240, engine.Pose.CharacterHeight);
        engine.Click();
        engine.Advance(TimeSpan.FromMilliseconds(150));
        Assert.True(engine.Contact.Y < -20);
        engine.Advance(TimeSpan.FromMilliseconds(180));
        Assert.Equal(0, engine.Contact.Y);
    }

    [Fact]
    public void RejectsEscapingPaths()
    {
        var exception = Assert.Throws<InvalidDataException>(() => PackLoader.Load((path, limit) =>
            path == "pack.json" ? Patch(path, "dot.png", "../dot.png") : ReadExample(path, limit)));
        Assert.Contains("relative pack path", exception.Message);
    }

    [Fact]
    public void MissingOptionalSoundWarnsAndLeavesPackUsable()
    {
        var pack = PackLoader.Load((path, limit) => path == "pack.json"
            ? Patch(path, "\"sounds\": []", "\"sounds\": [{\"id\":\"hop\",\"path\":\"missing.wav\",\"gain\":1}]")
            : ReadExample(path, limit));
        Assert.Empty(pack.Sounds);
        Assert.Single(pack.Warnings);
    }

    [Fact]
    public void RejectsAnUntimedLoop()
    {
        Assert.Throws<InvalidDataException>(() => PackLoader.Load((path, limit) => path == "behaviors.json"
            ? Patch(path, "\"durationMs\": 2000", "\"durationMs\": 0")
            : ReadExample(path, limit)));
    }

    private static byte[] Patch(string file, string oldValue, string newValue) =>
        Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(ReadExample(file, int.MaxValue)!).Replace(oldValue, newValue));

    private static byte[]? ReadExample(string file, int limit)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "example", file);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
